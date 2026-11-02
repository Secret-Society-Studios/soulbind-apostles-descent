using System.Collections.Generic;
using SoulBind.Core;
using SoulBind.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Kit = SoulBind.UI.UiKit;

namespace SoulBind.UI
{
    /// <summary>
    /// The coach for the tutorial (practice) match, top-center: one short step at a time, from
    /// moving to ending the turn. Only the step's own action works (moving while it says Move, and
    /// so on); anything else is refused and the coach flashes. A step moves on by itself when the
    /// player does it, or with Next. Skip hides the coach and leaves a normal match against the bot. MatchHud creates it only in
    /// the tutorial, so it needs no setup. Display only: it reads the match, it never changes it.
    /// </summary>
    public sealed class TutorialGuide : MonoBehaviour
    {
        // Sizes in canvas pixels (the canvas is 1920 x 1080).
        private const float Width = 860f;
        private const float Height = 138f;
        private const float Top = 20f;
        private const float Pad = 18f;
        private const float ButtonWidth = 120f;
        private const float ButtonHeight = 44f;
        private const float PhoneScale = 1.1f;

        private enum Step { Welcome, Move, PlayCard, Attack, EndTurn, Goal, Done }

        private sealed class Page
        {
            public string Title;
            public string Text;
            public TutorialActions Allowed;
        }

        private static readonly Page[] Pages =
        {
            new Page
            {
                Title = "Practice match",
                Text = "You play one bot here. Nothing counts and there is no turn timer, so take your time. " +
                       "Real matches give each turn 30 to 45 seconds. Press Next to begin.",
                Allowed = TutorialActions.None
            },
            new Page
            {
                Title = "1  Move",
                Text = "The green tiles show how far you can walk this turn. Tap any of them: your Apostle walks there " +
                       $"one tile at a time, and each step costs {GameConstants.MoveCapacityCost} Capacity.",
                Allowed = TutorialActions.Move
            },
            new Page
            {
                Title = "2  Play a card",
                Text = "Drag a card onto the discard pile. It waits in the play queue (top-left) with its Capacity held: " +
                       "tap it there to take it back, or press PLAY to use it.",
                Allowed = TutorialActions.Cards
            },
            new Page
            {
                Title = "3  Attack",
                Text = "Walk next to the enemy. When they glow red, drag a card with ATK onto them, then press ATTACK. " +
                       "Breaking their DEF steals a Soul. You attack once per turn. Too far away? Press Next.",
                Allowed = TutorialActions.Move | TutorialActions.Cards | TutorialActions.Attack
            },
            new Page
            {
                Title = "4  End your turn",
                Text = "Press End Turn when you are done. Anything still queued resolves, you draw a card, " +
                       "and the bot plays.",
                Allowed = TutorialActions.EndTurn
            },
            new Page
            {
                Title = "How to win",
                Text = $"Be the last Apostle standing, or reach {GameConstants.SoulsToAwaken} Souls to Awaken and step " +
                       "onto the Ascension tile. Keep practising, or use Menu > Leave when you are ready.",
                Allowed = TutorialActions.All
            }
        };

        private MatchController _controller;
        private CanvasGroup _group;
        private TMP_Text _title;
        private TMP_Text _text;
        private TMP_Text _nextLabel;
        private Step _step = Step.Welcome;
        private Step _shownStep = Step.Done;   // Done = nothing shown yet

        // What the player had when the step began, to notice when they did it.
        private bool _hasStart;
        private HexCoord _startTile;
        private int _startHand;
        private bool _hadTurn;
        private float _flash;          // seconds left of the "follow the step" flash
        private Image _border;

        private const float FlashTime = 0.6f;
        private static readonly Color FlashColor = new Color(0.95f, 0.30f, 0.25f, 1f);

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
            controller.SetTutorialActions(Pages[0].Allowed);
            return guide;
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
            _nextLabel = Kit.Button(root, "Next", "Next", Kit.ButtonStyle.Primary, Next, 16f, 8f);
            Kit.At((RectTransform)_nextLabel.transform.parent, x, 16f, ButtonWidth, ButtonHeight);
            TMP_Text skip = Kit.Button(root, "Skip", "Skip tutorial", Kit.ButtonStyle.Ghost, Skip, 13f, 8f);
            Kit.At((RectTransform)skip.transform.parent, x, 16f + ButtonHeight + 10f, ButtonWidth, 36f);
        }

        private void OnDestroy()
        {
            if (_controller != null)
            {
                _controller.TutorialActionBlocked -= OnBlocked;
                _controller.SetTutorialActions(TutorialActions.All);
            }
        }

        // The player tried something this step does not allow: the coach flashes red.
        private void OnBlocked() => _flash = FlashTime;

        // ---------- Steps ----------

        private void Update()
        {
            if (_step == Step.Done)
                return;

            Match match = _controller != null ? _controller.CurrentMatch : null;
            // The starting-tile guide has the top of the screen until the match begins.
            bool visible = match != null && !match.IsOver && !_controller.IsChoosingSpawns;
            _group.alpha = visible ? 1f : 0f;
            _group.blocksRaycasts = visible;
            if (!visible)
                return;

            if (IsStepDone(match))
                GoTo(_step + 1);
            if (_step != Step.Done && _shownStep != _step)
                Show();
            ShowFlash();
        }

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

        // True once the player has done what this step asks.
        private bool IsStepDone(Match match)
        {
            ApostleState you = FindYou(match);
            if (you == null)
                return false;

            bool yourTurn = _controller.IsLocalTurn;
            // Each of your turns starts the step afresh (your hand grows by a card between turns).
            if (!yourTurn)
                _hasStart = false;
            else if (!_hasStart)
            {
                _hasStart = true;
                _startTile = you.Position;
                _startHand = match.GetHand(you.Id).Count;
            }

            switch (_step)
            {
                case Step.Move:
                    return _hasStart && you.Position != _startTile && !_controller.IsWalking;
                case Step.PlayCard:
                    return yourTurn && _hasStart && match.GetHand(you.Id).Count < _startHand;
                case Step.Attack:
                    return yourTurn && match.Engagement != EngagementState.Free;
                case Step.EndTurn:
                    if (yourTurn)
                        _hadTurn = true;
                    return _hadTurn && !yourTurn;
                default:
                    return false;
            }
        }

        private ApostleState FindYou(Match match)
        {
            int id = _controller.ViewerId;
            IReadOnlyList<ApostleState> apostles = match.Battlefield.Apostles;
            for (int i = 0; i < apostles.Count; i++)
            {
                if (apostles[i].Id == id)
                    return apostles[i];
            }
            return null;
        }

        private void GoTo(Step step)
        {
            _step = step;
            _hasStart = false;
            _hadTurn = false;
            if (_step == Step.Done)
            {
                _controller.SetTutorialActions(TutorialActions.All);
                gameObject.SetActive(false);
                return;
            }
            _controller.SetTutorialActions(Pages[(int)_step].Allowed);
        }

        private void Show()
        {
            _shownStep = _step;
            Page page = Pages[(int)_step];
            _title.text = page.Title;
            _text.text = page.Text;
            _nextLabel.text = _step == Step.Goal ? "Got it" : "Next";
        }

        private void Next() => GoTo(_step + 1);

        private void Skip() => GoTo(Step.Done);
    }
}
