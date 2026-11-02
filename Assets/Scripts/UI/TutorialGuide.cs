using System.Collections.Generic;
using SoulBind.Core;
using SoulBind.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Kit = SoulBind.UI.UiKit;

namespace SoulBind.UI
{
    /// <summary>
    /// The coach for the tutorial match, top-center. The tutorial is one match (see TutorialMatch)
    /// taught step by step: walk to a Soul tile, end the turn, play a combo, attack, Awaken and
    /// ascend. Only the step's own action works; anything else is refused and the coach flashes.
    /// When the player does the step, the next one shows. MatchHud creates it only in the tutorial,
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
        private enum Check { PressNext, ReachTarget, NextTurn, RadianceCombo, EmptyQueue, Engage, Strike, Win }

        private sealed class Step
        {
            public string Title;
            public string Text;
            public TutorialActions Allowed;
            public Check Done;
        }

        private static readonly Step[] Steps =
        {
            new Step
            {
                Title = "Welcome to SoulBind",
                Text = "This is a guided match against a bot that only watches. Follow each step here; anything " +
                       "else is locked until you get to it. Real matches give each turn 30 to 45 seconds.",
                Allowed = TutorialActions.None, Done = Check.PressNext
            },
            new Step
            {
                Title = "1  Move",
                Text = "Green tiles show how far you can walk this turn. Tap the purple Soul tile: your Apostle " +
                       $"walks there one tile at a time, {GameConstants.MoveCapacityCost} Capacity per step.",
                Allowed = TutorialActions.Move, Done = Check.ReachTarget
            },
            new Step
            {
                Title = "2  End your turn",
                Text = $"The Soul tile gave you +{GameConstants.SoulTileAmount} Souls. Heal and Purify tiles work the same " +
                       "way. Press End Turn: you draw a card and the bot plays.",
                Allowed = TutorialActions.EndTurn, Done = Check.NextTurn
            },
            new Step
            {
                Title = "3  Combo",
                Text = "Drag The Magician onto the discard pile, then The Lovers. Both are Radiance, and two cards of " +
                       "one element in a turn trigger its combo.",
                Allowed = TutorialActions.Cards, Done = Check.RadianceCombo
            },
            new Step
            {
                Title = "3  Combo: use the cards",
                Text = $"The play queue glows: Radiance gives +{ElementCombo.RadianceShield} DEF. Press PLAY in the " +
                       "queue (top-left) to use the cards.",
                Allowed = TutorialActions.Cards, Done = Check.EmptyQueue
            },
            new Step
            {
                Title = "4  End your turn",
                Text = "Capacity left over is lost when the turn ends, so spend it well. Press End Turn.",
                Allowed = TutorialActions.EndTurn, Done = Check.NextTurn
            },
            new Step
            {
                Title = "5  Attack",
                Text = "Step next to the enemy, then drag The Chariot onto them (they glow red when you can) " +
                       "to start an attack.",
                Allowed = TutorialActions.Move | TutorialActions.Attack, Done = Check.Engage
            },
            new Step
            {
                Title = "5  Attack: Strike",
                Text = "Drag The Magician onto them too, for more ATK. Then press ATTACK in the play queue. " +
                       "Breaking their DEF steals a Soul.",
                Allowed = TutorialActions.Attack, Done = Check.Strike
            },
            new Step
            {
                Title = "6  Ascend to win",
                Text = $"That was your {GameConstants.SoulsToAwaken}th Soul: you are Awakened and the gold Ascension tile " +
                       "has appeared. Walk onto it to win. Too far? End Turn and keep walking.",
                Allowed = TutorialActions.Move | TutorialActions.EndTurn, Done = Check.Win
            }
        };

        private const string DoneTitle = "Tutorial complete!";
        private static readonly string DoneText = "You can also win by being the last Apostle standing. Watch your Corruption: " +
                                        $"at {GameConstants.CorruptionThreshold} you lose Vessel every turn and cannot gain Souls.";

        private MatchController _controller;
        private CanvasGroup _group;
        private Image _border;
        private TMP_Text _title;
        private TMP_Text _text;
        private GameObject _nextButton;
        private TMP_Text _nextLabel;
        private int _step;           // index into Steps; Steps.Length = the tutorial is complete
        private int _shownStep = -1; // -1 = nothing shown yet
        private int _stepRound;      // the round the step began in (for NextTurn)
        private float _flash;        // seconds left of the "follow the step" flash

        private bool IsComplete => _step >= Steps.Length;

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
            guide.Build(rect);
            Transform border = rect.Find("Border");
            guide._border = border != null ? border.GetComponent<Image>() : null;
            controller.TutorialActionBlocked += guide.OnBlocked;
            controller.SetTutorialActions(Steps[0].Allowed);
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
            _nextLabel = Kit.Button(root, "Next", "Next", Kit.ButtonStyle.Primary, OnNext, 15f, 8f);
            _nextButton = _nextLabel.transform.parent.gameObject;
            Kit.At((RectTransform)_nextButton.transform, x, 16f, ButtonWidth, ButtonHeight);
            TMP_Text leave = Kit.Button(root, "Leave", "Leave tutorial", Kit.ButtonStyle.Ghost, Leave, 13f, 8f);
            Kit.At((RectTransform)leave.transform.parent, x, 16f + ButtonHeight + 10f, ButtonWidth, 36f);
        }

        // ---------- Steps ----------

        private void Update()
        {
            Match match = _controller != null ? _controller.CurrentMatch : null;
            // The last step stays on screen when the match is won, over the end-of-match screen.
            bool visible = match != null && (!match.IsOver || IsComplete || IsWinStep());
            _group.alpha = visible ? 1f : 0f;
            _group.blocksRaycasts = visible;
            if (!visible)
                return;

            if (!IsComplete && IsStepDone(match, Steps[_step].Done))
                GoTo(_step + 1);
            if (_shownStep != _step)
                Show();
            ShowFlash();
        }

        private bool IsWinStep() => !IsComplete && Steps[_step].Done == Check.Win;

        // True once the player has done what the step asks.
        private bool IsStepDone(Match match, Check check)
        {
            int you = _controller.ViewerId;
            switch (check)
            {
                case Check.PressNext:
                    return false;
                case Check.ReachTarget:
                    ApostleState apostle = Find(match, you);
                    return apostle != null && !_controller.IsWalking && apostle.Position == _controller.TutorialTarget;
                case Check.NextTurn:
                    return _controller.IsLocalTurn && match.Turns.RoundNumber > _stepRound;
                case Check.RadianceCombo:
                    return match.IsComboTriggered(SoulBind.Core.Element.Radiance);
                case Check.EmptyQueue:
                    return match.AttackCards.Count == 0;
                case Check.Engage:
                    return match.Engagement != EngagementState.Free;
                case Check.Strike:
                    return match.Engagement == EngagementState.Disengaged;
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

        // Once the tutorial is complete, everything works again.
        private void GoTo(int step)
        {
            _step = step;
            Match match = _controller.CurrentMatch;
            _stepRound = match != null ? match.Turns.RoundNumber : 0;
            _controller.SetTutorialActions(IsComplete ? TutorialActions.All : Steps[_step].Allowed);
        }

        private void Show()
        {
            _shownStep = _step;
            bool waitsForNext = IsComplete || Steps[_step].Done == Check.PressNext;
            if (IsComplete)
            {
                _title.text = DoneTitle;
                _text.text = DoneText;
                _nextLabel.text = "Main menu";
            }
            else
            {
                _title.text = Steps[_step].Title;
                _text.text = Steps[_step].Text;
                _nextLabel.text = "Next";
            }
            _nextButton.SetActive(waitsForNext);
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

        // Next moves past a step that only explains; at the end it goes back to the menu.
        private void OnNext()
        {
            if (IsComplete)
                Leave();
            else
                GoTo(_step + 1);
        }

        // The main menu is the first scene in the Build Settings.
        private static void Leave() => SceneManager.LoadScene(0);
    }
}
