using System.Collections.Generic;
using UnityEngine;

namespace SoulBind.Managers
{
    /// <summary>
    /// Speaks one line at a time, by the priorities and cooldowns in Docs/08-narrator-lines.csv.
    /// A line said while another plays waits its turn, most important first, and is dropped if it
    /// waits too long. Only the biggest lines cut in. It has its own volume in Settings.
    /// </summary>
    public static class Narrator
    {
        private const string VoiceKey = "narratorVoice";
        private const int InterruptPriority = 5; // this or higher cuts in over anything less important
        private const int MaxWaiting = 3;
        private const float WaitSeconds = 6f;    // a line this old has missed its moment

        private struct Rule
        {
            public readonly int Priority;
            public readonly float Cooldown;

            public Rule(int priority, float cooldown = 0f)
            {
                Priority = priority;
                Cooldown = cooldown;
            }
        }

        private struct Waiting
        {
            public NarratorLine Line;
            public float Time;
        }

        // In NarratorLine's order.
        private static readonly Rule[] Rules =
        {
            new Rule(1), new Rule(1), new Rule(1), new Rule(1), new Rule(1),           // title and lobby
            new Rule(2), new Rule(2), new Rule(3), new Rule(1),                        // setup
            new Rule(1, 8f), new Rule(3), new Rule(3), new Rule(2), new Rule(3),       // turns and Phases
            new Rule(4), new Rule(3), new Rule(3), new Rule(3), new Rule(4), new Rule(3), // cards
            new Rule(2, 4f),                                                           // a Soul stolen
            new Rule(3), new Rule(3), new Rule(3), new Rule(3),                        // combos
            new Rule(2, 4f), new Rule(2, 4f), new Rule(2, 4f), new Rule(2),            // tiles
            new Rule(2, 10f), new Rule(2, 10f), new Rule(4), new Rule(4), new Rule(4), new Rule(2, 6f), // Corruption
            new Rule(5), new Rule(5), new Rule(5), new Rule(5),                        // Awakening
            new Rule(6), new Rule(6), new Rule(6), new Rule(6)                         // deaths finish before the result lines
        };

        private static readonly float[] NextAllowed = new float[Rules.Length];
        private static readonly List<Waiting> Queue = new List<Waiting>(MaxWaiting);
        private static int _speaking;
        private static bool _welcomed;
        private static GameObject _host;

        // Starts clean on every Play, even with domain reload turned off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            System.Array.Clear(NextAllowed, 0, NextAllowed.Length);
            Queue.Clear();
            _speaking = 0;
            _welcomed = false;
            _host = null;
        }

        /// <summary>The voice this player picked.</summary>
        public static NarratorVoice Voice
        {
            get => (NarratorVoice)Mathf.Clamp(PlayerPrefs.GetInt(VoiceKey, 0), 0, (int)NarratorVoice.Off);
            private set
            {
                PlayerPrefs.SetInt(VoiceKey, (int)value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Moves to the next voice: Mixed, Normal, Raspy, Off.</summary>
        public static void CycleVoice()
        {
            Voice = (NarratorVoice)(((int)Voice + 1) % ((int)NarratorVoice.Off + 1));
            Queue.Clear();
            if (Voice == NarratorVoice.Off)
                GameAudio.StopVoice();
        }

        /// <summary>Says a line, or queues it, unless it was said too recently.</summary>
        public static void Say(NarratorLine line)
        {
            if (Voice == NarratorVoice.Off || NarratorLibrary.Current == null)
                return;
            int index = (int)line;
            Rule rule = Rules[index];
            if (Time.unscaledTime < NextAllowed[index])
                return;
            NextAllowed[index] = Time.unscaledTime + rule.Cooldown;

            if (!GameAudio.IsVoicePlaying && Queue.Count == 0)
            {
                Speak(line);
                return;
            }
            if (rule.Priority >= InterruptPriority && rule.Priority > _speaking)
            {
                GameAudio.StopVoice();
                Speak(line);
                return;
            }
            Enqueue(line, rule.Priority);
            EnsureHost();
        }

        /// <summary>The greeting, once per launch.</summary>
        public static void Welcome()
        {
            if (_welcomed)
                return;
            _welcomed = true;
            NarratorLine[] greetings =
            {
                NarratorLine.WelcomeSoulBind, NarratorLine.WelcomeApostle,
                NarratorLine.ContractAwaits, NarratorLine.DeathCurrency
            };
            Say(greetings[Random.Range(0, greetings.Length)]);
        }

        /// <summary>A line straight away, for trying a voice in Settings.</summary>
        public static void Preview()
        {
            if (Voice != NarratorVoice.Off && NarratorLibrary.Current != null)
                Speak(NarratorLine.WelcomeApostle);
        }

        private static void Speak(NarratorLine line)
        {
            AudioClip clip = NarratorLibrary.Current.Pick(line, Voice);
            if (clip == null)
                return;
            _speaking = Rules[(int)line].Priority;
            GameAudio.PlayVoice(clip);
        }

        // Most important first; lines of the same importance stay in order.
        private static void Enqueue(NarratorLine line, int priority)
        {
            int place = Queue.Count;
            while (place > 0 && Rules[(int)Queue[place - 1].Line].Priority < priority)
                place--;
            if (place >= MaxWaiting)
                return;
            Queue.Insert(place, new Waiting { Line = line, Time = Time.unscaledTime });
            if (Queue.Count > MaxWaiting)
                Queue.RemoveAt(MaxWaiting);
        }

        private static void EnsureHost()
        {
            if (_host != null)
                return;
            _host = new GameObject("Narrator");
            Object.DontDestroyOnLoad(_host);
            _host.AddComponent<Host>();
        }

        // Starts the next waiting line when the voice falls quiet.
        private sealed class Host : MonoBehaviour
        {
            private void Update()
            {
                if (GameAudio.IsVoicePlaying)
                    return;
                while (Queue.Count > 0)
                {
                    Waiting next = Queue[0];
                    Queue.RemoveAt(0);
                    if (Time.unscaledTime - next.Time > WaitSeconds)
                        continue;
                    Speak(next.Line);
                    return;
                }
                _speaking = 0;
            }
        }
    }
}
