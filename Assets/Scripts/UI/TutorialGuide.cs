using System.Collections.Generic;
using SoulBind.Core;
using SoulBind.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Kit = SoulBind.UI.UiKit;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace SoulBind.UI
{
    /// <summary>
    /// The coach for a tutorial lesson, top-center. Each lesson is a ready-made situation (see
    /// TutorialLessons) taught in one or two short steps. Only the step's own action works; anything
    /// else is refused and the coach flashes. When the player does the step, the next one shows,
    /// and at the end Next lesson loads the following one. MatchHud creates it only in the tutorial,
    /// so it needs no setup. Display only: it reads the match and never changes it.
    /// </summary>
    public sealed class TutorialGuide : MonoBehaviour
    {
        // Sizes in canvas pixels (the canvas is 1920 x 1080).
        private const float Width = 880f;
        private const float Height = 138f;
        private const float Top = 20f;
        private const float Pad = 18f;
        private const float ButtonWidth = 140f;
        private const float ButtonHeight = 44f;
        private const float PhoneScale = 1.1f;
        private const float FlashTime = 0.6f;

        private static readonly Color FlashColor = new Color(0.95f, 0.30f, 0.25f, 1f);

        // What the player must do to finish a step.
        private enum Check { ReachTarget, Engage, Strike, RadianceCombo, EmptyQueue, Awaken, Win }

        private sealed class Step
        {
            public string Title;
            public string Text;
            public TutorialActions Allowed;
            public Check Done;
        }

        private sealed class Lesson
        {
            public string Name;
            public Step[] Steps;
            public string Summary; // shown when the lesson is complete
        }

        private static readonly Lesson[] Lessons =
        {
            new Lesson
            {
                Name = "Move",
                Steps = new[]
                {
                    new Step
                    {
                        Title = "Move: walk to the Soul tile",
                        Text = "Green tiles show how far you can walk this turn. Tap the purple Soul tile: your Apostle " +
                               $"walks there one tile at a time, {GameConstants.MoveCapacityCost} Capacity per step.",
                        Allowed = TutorialActions.Move, Done = Check.ReachTarget
                    }
                },
                Summary = $"Soul tiles give +{GameConstants.SoulTileAmount} Souls. Heal and Purify tiles work the same way, " +
                          "and used tiles come back every Phase."
            },
            new Lesson
            {
                Name = "Attack",
                Steps = new[]
                {
                    new Step
                    {
                        Title = "Attack: start with a card",
                        Text = "The enemy is right next to you, ringed in red. Drag The Chariot onto them to start an attack.",
                        Allowed = TutorialActions.Attack, Done = Check.Engage
                    },
                    new Step
                    {
                        Title = "Attack: add a card and Strike",
                        Text = "Drag The Magician onto them too, for more ATK. Then press ATTACK in the play queue (top-left).",
                        Allowed = TutorialActions.Attack, Done = Check.Strike
                    }
                },
                Summary = $"Breaking their DEF steals {GameConstants.SoulsStolenPerAttack} Soul and gives you " +
                          $"+{GameConstants.AttackCorruption} Corruption. You can attack once per turn."
            },
            new Lesson
            {
                Name = "Combos",
                Steps = new[]
                {
                    new Step
                    {
                        Title = "Combo: two of one element",
                        Text = "Drag The Magician onto the discard pile, then The Lovers. Both are Radiance: two cards " +
                               "of one element in a turn trigger its combo.",
                        Allowed = TutorialActions.Cards, Done = Check.RadianceCombo
                    },
                    new Step
                    {
                        Title = "Combo: use the cards",
                        Text = $"The queue glows: Radiance gives +{ElementCombo.RadianceShield} DEF. " +
                               "Press PLAY in the play queue to use the cards.",
                        Allowed = TutorialActions.Cards, Done = Check.EmptyQueue
                    }
                },
                Summary = $"Celeste cleans {ElementCombo.CelesteCorruptionReduction} Corruption, Crimson adds " +
                          $"+{ElementCombo.CrimsonAttack} ATK to an attack, and Nocturne gives a foe Corruption."
            },
            new Lesson
            {
                Name = "How to win",
                Steps = new[]
                {
                    new Step
                    {
                        Title = "Win: steal your last Soul",
                        Text = $"You have {GameConstants.SoulsToAwaken - 1} Souls. Drag The Chariot onto the enemy and press " +
                               $"ATTACK to steal one more. At {GameConstants.SoulsToAwaken} you Awaken.",
                        Allowed = TutorialActions.Attack, Done = Check.Awaken
                    },
                    new Step
                    {
                        Title = "Win: reach the Ascension tile",
                        Text = "You are Awakened, and the gold Ascension tile has appeared. Walk onto it to win. " +
                               "Too far? Press End Turn and keep walking next turn.",
                        Allowed = TutorialActions.Move | TutorialActions.EndTurn, Done = Check.Win
                    }
                },
                Summary = "You can also win by being the last Apostle standing: at 0 Vessel an Apostle is out."
            }
        };

        private MatchController _controller;
        private CanvasGroup _group;
        private Image _border;
        private TMP_Text _title;
        private TMP_Text _text;
        private GameObject _nextButton;
        private TMP_Text _nextLabel;
        private Lesson _lesson;
        private int _step;          // index into _lesson.Steps; Steps.Length = the lesson is complete
        private int _shownStep = -1; // -1 = nothing shown yet
        private float _flash;       // seconds left of the "follow the step" flash

        private bool IsComplete => _step >= _lesson.Steps.Length;

        public static TutorialGuide Create(RectTransform hudRoot, MatchController controller)
        {
            RectTransform rect = Kit.Box(hudRoot, "TutorialGuide", new Color(0.03f, 0.02f, 0.05f, 0.92f),
                Kit.Gold, 14f, 2f);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -Top);
            rect.sizeDelta = new Vector2(Width, Height);
            if (Application.isMobilePlatform)
                rect.localScale = Vector3.one * PhoneScale;

            TutorialGuide guide = rect.gameObject.AddComponent<TutorialGuide>();
            guide._controller = controller;
            guide._lesson = Lessons[Mathf.Clamp((int)controller.Lesson, 0, Lessons.Length - 1)];
            guide.Build(rect);
            Transform border = rect.Find("Border");
            guide._border = border != null ? border.GetComponent<Image>() : null;
            controller.TutorialActionBlocked += guide.OnBlocked;
            controller.SetTutorialActions(guide._lesson.Steps[0].Allowed);
            return guide;
        }

        private void OnDestroy()
        {
            if (_controller == null)
                return;
            _controller.TutorialActionBlocked -= OnBlocked;
            _controller.SetTutorialActions(TutorialActions.All);
        }

        // ---------- Building ----------

        private void Build(RectTransform root)
        {
            _group = root.gameObject.AddComponent<CanvasGroup>();

            float textWidth = Width - 2f * Pad - ButtonWidth - 16f;
            _title = Kit.Label(root, "Title", "", 22f, Kit.Display, Kit.Title, TextAlignmentOptions.Left);
            Kit.At(_title.rectTransform, Pad, 12f, textWidth, 30f);
            _title.characterSpacing = 2f;
            _text = Kit.Label(root, "Text", "", 17f, Kit.Inter, Kit.Text, TextAlignmentOptions.TopLeft);
            Kit.At(_text.rectTransform, Pad, 46f, textWidth, Height - 56f);

            float x = Width - Pad - ButtonWidth;
            _nextLabel = Kit.Button(root, "Next", "Next lesson", Kit.ButtonStyle.Primary, NextLesson, 15f, 8f);
            _nextButton = _nextLabel.transform.parent.gameObject;
            Kit.At((RectTransform)_nextButton.transform, x, 16f, ButtonWidth, ButtonHeight);
            TMP_Text leave = Kit.Button(root, "Leave", "Leave lesson", Kit.ButtonStyle.Ghost, Leave, 13f, 8f);
            Kit.At((RectTransform)leave.transform.parent, x, 16f + ButtonHeight + 10f, ButtonWidth, 36f);
        }

        // ---------- Steps ----------

        private void Update()
        {
            Match match = _controller != null ? _controller.CurrentMatch : null;
            // A won lesson stays on screen, over the end-of-match screen.
            bool visible = match != null && (!match.IsOver || IsComplete || IsWinStep());
            _group.alpha = visible ? 1f : 0f;
            _group.blocksRaycasts = visible;
            if (!visible)
                return;

            if (!IsComplete && IsStepDone(match, _lesson.Steps[_step].Done))
                GoTo(_step + 1);
            if (_shownStep != _step)
                Show();
            ShowFlash();
        }

        private bool IsWinStep() => !IsComplete && _lesson.Steps[_step].Done == Check.Win;

        // True once the player has done what the step asks.
        private bool IsStepDone(Match match, Check check)
        {
            int you = _controller.ViewerId;
            switch (check)
            {
                case Check.ReachTarget:
                    ApostleState apostle = Find(match, you);
                    return _controller.HasTutorialTarget && apostle != null && !_controller.IsWalking
                           && apostle.Position == _controller.TutorialTarget;
                case Check.Engage:
                    return match.Engagement != EngagementState.Free;
                case Check.Strike:
                    return match.Engagement == EngagementState.Disengaged;
                case Check.RadianceCombo:
                    return match.IsComboTriggered(Element.Radiance);
                case Check.EmptyQueue:
                    return match.AttackCards.Count == 0;
                case Check.Awaken:
                    return match.Ascension.HasAwakened && match.Ascension.AwakenedId == you;
                default:
                    return match.IsOver && match.WinnerId == you;
            }
        }

        private static ApostleState Find(Match match, int id)
        {
            IReadOnlyList<ApostleState> apostles = match.Battlefield.Apostles;
            for (int i = 0; i < apostles.Count; i++)
            {
                if (apostles[i].Id == id)
                    return apostles[i];
            }
            return null;
        }

        // Once the lesson is complete, everything works again: the player can keep playing.
        private void GoTo(int step)
        {
            _step = step;
            _controller.SetTutorialActions(IsComplete ? TutorialActions.All : _lesson.Steps[_step].Allowed);
        }

        private void Show()
        {
            _shownStep = _step;
            bool last = (int)_controller.Lesson >= Lessons.Length - 1;
            if (IsComplete)
            {
                _title.text = _lesson.Name + ": lesson complete!";
                _text.text = _lesson.Summary;
                _nextLabel.text = last ? "Main menu" : "Next lesson";
            }
            else
            {
                Step step = _lesson.Steps[_step];
                _title.text = step.Title;
                _text.text = step.Text;
            }
            _nextButton.SetActive(IsComplete);
        }

        // The player tried something this step does not allow: the coach flashes red.
        private void OnBlocked() => _flash = FlashTime;

        private void ShowFlash()
        {
            if (_border == null)
                return;
            if (_flash > 0f)
                _flash -= Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_flash / FlashTime);
            _border.color = Color.Lerp(Kit.Gold, FlashColor, t);
            _title.color = Color.Lerp(Kit.Title, FlashColor, t);
        }

        // ---------- Buttons ----------

        // Loads the next lesson in this scene, or goes back to the menu after the last one.
        private void NextLesson()
        {
            int next = (int)_controller.Lesson + 1;
            if (next >= Lessons.Length)
            {
                Leave();
                return;
            }
            MatchSettings.SetTutorial((TutorialLesson)next);
            Scene scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0)
            {
                SceneManager.LoadScene(scene.buildIndex);
                return;
            }
#if UNITY_EDITOR
            // A scene that is not in the Build Settings can still be reloaded in the Editor.
            EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#endif
        }

        // The main menu is the first scene in the Build Settings.
        private static void Leave() => SceneManager.LoadScene(0);
    }
}
