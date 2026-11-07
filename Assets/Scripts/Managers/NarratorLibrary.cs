using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoulBind.Managers
{
    /// <summary>Every recorded narrator line. The numbers are saved in NarratorLibrary.asset.</summary>
    public enum NarratorLine
    {
        WelcomeSoulBind = 0,
        WelcomeApostle = 1,
        DeathCurrency = 2,
        ChooseSeat = 3,
        ContractAwaits = 4,
        ChooseGround = 5,
        BoardSet = 6,
        DescentBegins = 7,
        SoulsCurrency = 8,
        YourTurn = 9,
        NewPhase = 10,
        WeakestLeads = 11,
        ChooseDirection = 12,
        TurnLost = 13,
        TimeStandsStill = 14,
        WheelTurns = 15,
        Hierophant = 16,
        Temperance = 17,
        Judgement = 18,
        HighPriestess = 19,
        SoulTaken = 20,
        ComboRadiance = 21,
        ComboCeleste = 22,
        ComboCrimson = 23,
        ComboNocturne = 24,
        Healed = 25,
        Purified = 26,
        SoulClaimed = 27,
        TilesReturn = 28,
        CorruptionSpreads = 29,
        TooManyCards = 30,
        Corrupted = 31,
        Consumed = 32,
        NoSouls = 33,
        SoulsHeld = 34,
        Awakened = 35,
        ContractFulfilled = 36,
        StopThem = 37,
        Ascension = 38,
        ApostleFallen = 39,
        VesselBroken = 40,
        LastStands = 41,
        ContractComplete = 42
    }

    /// <summary>Which recordings play. Mixed picks from both voices. Saved on the device.</summary>
    public enum NarratorVoice
    {
        Mixed,
        Normal,
        Raspy,
        Off
    }

    /// <summary>
    /// The narrator's recordings, at Assets/Resources/NarratorLibrary.asset. Each line can have several
    /// takes per voice: one is picked at random, from both voices when the player has Mixed. A voice with
    /// no take stays quiet on that line.
    /// The files are in Assets/Audio/Narrator. The script is Docs/08-narrator-lines.csv.
    /// </summary>
    [CreateAssetMenu(menuName = "SoulBind/Narrator Library", fileName = "NarratorLibrary")]
    public sealed class NarratorLibrary : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public NarratorLine line;
            public AudioClip[] normal;
            public AudioClip[] raspy;
        }

        [SerializeField] private List<Entry> lines = new List<Entry>();

        private static NarratorLibrary _current;
        private static bool _looked;

        // Looked up again on every Play, even with domain reload turned off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _looked = false;
            _current = null;
        }

        /// <summary>The library at Resources/NarratorLibrary, or null (no narrator).</summary>
        public static NarratorLibrary Current
        {
            get
            {
                if (!_looked)
                {
                    _looked = true;
                    _current = Resources.Load<NarratorLibrary>("NarratorLibrary");
                }
                return _current;
            }
        }

        /// <summary>A random take of a line in this voice, or null.</summary>
        public AudioClip Pick(NarratorLine line, NarratorVoice voice)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].line != line)
                    continue;
                AudioClip[] normal = voice != NarratorVoice.Raspy ? lines[i].normal : null;
                AudioClip[] raspy = voice != NarratorVoice.Normal ? lines[i].raspy : null;
                return PickFrom(normal, raspy);
            }
            return null;
        }

        // An even choice between the voices that have a take, then a random take of that voice.
        private static AudioClip PickFrom(AudioClip[] first, AudioClip[] second)
        {
            bool hasFirst = first != null && first.Length > 0;
            bool hasSecond = second != null && second.Length > 0;
            if (!hasFirst && !hasSecond)
                return null;
            bool useFirst = hasFirst && (!hasSecond || UnityEngine.Random.value < 0.5f);
            AudioClip[] takes = useFirst ? first : second;
            return takes[UnityEngine.Random.Range(0, takes.Length)];
        }
    }
}
