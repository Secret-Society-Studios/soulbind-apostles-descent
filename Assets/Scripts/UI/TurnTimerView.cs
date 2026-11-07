using System.Collections.Generic;
using SoulBind.Managers;
using SoulBind.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Kit = SoulBind.UI.UiKit;

namespace SoulBind.UI
{
    /// <summary>
    /// The turn timer, top-left (above the play queue): whose turn it is, the seconds left and a bar that drains.
    /// It turns red for the last seconds and pulses at the very end. Display only: the
    /// MatchController counts down and ends the turn. MatchHud creates it, so it needs no setup.
    /// </summary>
    public sealed class TurnTimerView : MonoBehaviour
    {
        // Sizes in canvas pixels (the canvas is 1920 x 1080).
        private const float Width = 330f;
        private const float Height = 64f;
        private const float TurnIconWidth = 92f;
        private const float Top = 20f;
        private const float Left = 30f;          // lines up with the play queue below it
        private const float BarHeight = 6f;
        private const float PhoneScale = 1.2f;
        private const float WarnSeconds = 10f;  // red from here down
        private const float PulseSeconds = 5f;  // and pulsing from here down

        private static readonly Color WarnColor = new Color(0.95f, 0.30f, 0.25f, 1f);

        private MatchController _controller;
        private CanvasGroup _group;
        private RectTransform _fill;
        private Image _fillImage;
        private TMP_Text _seconds;
        private TMP_Text _who;
        private int _shownSeconds = -1;  // -1 = nothing shown yet
        private int _shownActor = -1;    // the key of the name shown, -1 = none
        private bool _shownLocal;
        private readonly List<int> _remainingTurns = new List<int>(6);
        private readonly RectTransform[] _turnChips = new RectTransform[6];
        private readonly TMP_Text[] _turnNames = new TMP_Text[6];
        private readonly TMP_Text[] _turnPositions = new TMP_Text[6];
        private readonly int[] _shownOrder = new int[6];
        private int _orderViewer;
        private RectTransform _orderRoot;
        private CanvasGroup _orderGroup;
        private readonly Image[] _turnIcons = new Image[6];
        private readonly Image[] _turnBorders = new Image[6];
        private readonly TMP_Text[] _turnInitials = new TMP_Text[6];
        private Sprite _facePortrait;

        // Creates the turn timer on the HUD.
        public static TurnTimerView Create(RectTransform hudRoot, MatchController controller)
        {
            RectTransform rect = Kit.Box(hudRoot, "TurnTimer", Kit.Panel, Kit.Line, 12f, 2f);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(Left, -Top);
            rect.sizeDelta = new Vector2(Width, Height);
            Kit.UseFrame(rect);
            rect.GetComponent<Image>().raycastTarget = false;
            if (Application.isMobilePlatform)
                rect.localScale = Vector3.one * PhoneScale;

            TurnTimerView view = rect.gameObject.AddComponent<TurnTimerView>();
            view._controller = controller;
            view.Build(rect);
            view.BuildOrder(hudRoot);
            return view;
        }

        // ---------- Building ----------

        // Builds the timer bar and label; hidden until a timed turn starts.
        private void Build(RectTransform root)
        {
            _group = root.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.alpha = 0f;

            _who = Kit.Label(root, "Who", "", 16f, Kit.InterSemiBold, Kit.Soft, TextAlignmentOptions.Left);
            Kit.At(_who.rectTransform, 16f, 6f, Width - 110f, 30f);
            _who.textWrappingMode = TextWrappingModes.NoWrap;
            _who.overflowMode = TextOverflowModes.Ellipsis;

            _seconds = Kit.Label(root, "Seconds", "", 30f, Kit.Display, Kit.Title, TextAlignmentOptions.Right);
            Kit.At(_seconds.rectTransform, Width - 100f, 4f, 84f, 34f);

            // The bar along the bottom: a dark track and a fill that shrinks from the right.
            RectTransform track = Kit.At(Kit.CreateRect("Track", root), 12f, Height - BarHeight - 6f,
                Width - 24f, BarHeight);
            Image trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = Kit.Inset;
            trackImage.raycastTarget = false;

            _fill = Kit.CreateRect("Fill", track);
            _fill.anchorMin = Vector2.zero;
            _fill.anchorMax = Vector2.one;
            _fill.pivot = new Vector2(0f, 0.5f);
            _fill.offsetMin = Vector2.zero;
            _fill.offsetMax = Vector2.zero;
            _fillImage = _fill.gameObject.AddComponent<Image>();
            _fillImage.color = Kit.Gold;
            _fillImage.raycastTarget = false;
        }

        // A separate top-centre strip of Apostle faces, independent of the timer box.
        private void BuildOrder(RectTransform hudRoot)
        {
            AvatarSkin skin = AvatarSkin.Current;
            if (skin != null && skin.HasFrames && skin.Idle[0] != null)
            {
                // Only the hood and face of the existing 32x64 sprite; no body or new character art.
                Sprite source = skin.Idle[0];
                Rect area = source.textureRect;
                Rect head = new Rect(area.x, area.y + area.height * (37f / 64f), area.width, area.height * (22f / 64f));
                _facePortrait = Sprite.Create(source.texture, head, new Vector2(0.5f, 0.5f),
                    source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                _facePortrait.name = "ApostleFacePortrait";
            }
            _orderRoot = Kit.CreateRect("ApostleTurnQueue", hudRoot);
            _orderRoot.anchorMin = _orderRoot.anchorMax = _orderRoot.pivot = new Vector2(0.5f, 1f);
            _orderRoot.anchoredPosition = new Vector2(0f, -18f);
            _orderRoot.sizeDelta = new Vector2(TurnIconWidth * 6f, 112f);
            if (Application.isMobilePlatform)
                _orderRoot.localScale = Vector3.one * 1.1f;
            _orderGroup = _orderRoot.gameObject.AddComponent<CanvasGroup>();
            _orderGroup.blocksRaycasts = false;
            _orderGroup.interactable = false;
            _orderGroup.alpha = 0f;
            for (int i = 0; i < _turnChips.Length; i++)
            {
                RectTransform chip = Kit.At(Kit.CreateRect("Turn" + i, _orderRoot),
                    i * TurnIconWidth, 0f, TurnIconWidth, 112f);
                _turnChips[i] = chip;
                _turnPositions[i] = Kit.Label(chip, "Position", "", 13f, Kit.InterBold, Kit.Soft);
                Kit.At(_turnPositions[i].rectTransform, 0f, 0f, TurnIconWidth, 18f);
                RectTransform portrait = Kit.Box(chip, "Portrait", Kit.Inset, Kit.Line, 8f, 2f);
                Kit.At(portrait, 14f, 20f, 64f, 64f);
                Kit.UseFrame(portrait);
                portrait.GetComponent<Image>().raycastTarget = false;
                _turnBorders[i] = portrait.Find("Border").GetComponent<Image>();
                RectTransform art = Kit.At(Kit.CreateRect("Apostle", portrait), 7f, 5f, 50f, 54f);
                _turnIcons[i] = art.gameObject.AddComponent<Image>();
                _turnIcons[i].preserveAspect = true;
                _turnIcons[i].raycastTarget = false;
                _turnInitials[i] = Kit.Label(portrait, "Initial", "", 24f, Kit.Display, Kit.Text);
                Kit.Stretch(_turnInitials[i].rectTransform);
                _turnNames[i] = Kit.Label(chip, "Name", "", 14f, Kit.InterSemiBold, Kit.Text);
                Kit.At(_turnNames[i].rectTransform, 0f, 88f, TurnIconWidth, 22f);
                _turnNames[i].textWrappingMode = TextWrappingModes.NoWrap;
                _turnNames[i].overflowMode = TextOverflowModes.Ellipsis;
            }
        }

        // ---------- Showing ----------

        private void Update()
        {
            var match = _controller != null ? _controller.CurrentMatch : null;
            bool visible = match != null && !match.IsOver;
            bool running = _controller != null && _controller.IsTurnTimerRunning;
            _group.alpha = visible && running ? 1f : 0f;
            _orderGroup.alpha = visible ? 1f : 0f;
            if (!visible)
                return;
            if (StrikeDisplay.Active)
                return; // Keep the acting Apostle visible until their attack finishes.
            ShowWho();
            ShowOrder();
            ShowIcons();
            _seconds.gameObject.SetActive(running);
            _fill.parent.gameObject.SetActive(running);
            if (!running)
                return;

            float left = _controller.TurnSecondsLeft;
            float limit = Mathf.Max(1f, _controller.TurnSecondsLimit);
            _fill.anchorMax = new Vector2(Mathf.Clamp01(left / limit), 1f);

            bool warn = left <= WarnSeconds;
            Color color = warn ? WarnColor : Kit.Gold;
            _fillImage.color = color;
            _seconds.color = warn ? WarnColor : Kit.Title;

            // The last seconds pulse, so the player notices without reading.
            float pulse = left <= PulseSeconds ? 1f + 0.08f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)) : 1f;
            _seconds.rectTransform.localScale = new Vector3(pulse, pulse, 1f);

            // Text only changes once a second, so the label is not rebuilt every frame.
            int whole = Mathf.CeilToInt(left);
            if (whole != _shownSeconds)
            {
                _shownSeconds = whole;
                _seconds.SetText("{0}s", whole);
            }
            ShowWho();
        }

        // This round's actual sequence, skipping casualties; a phase boundary gets a fresh choice.
        private void ShowOrder()
        {
            var match = _controller.CurrentMatch;
            if (_orderViewer != _controller.ViewerId)
            {
                _orderViewer = _controller.ViewerId;
                System.Array.Clear(_shownOrder, 0, _shownOrder.Length);
            }
            match.Turns.GetRemainingTurns(match.Battlefield.Apostles, _remainingTurns);
            if (match.TimeStopOwnerId != 0 && match.CurrentApostle != null
                && match.TimeStopOwnerId == match.CurrentApostle.Id && _remainingTurns.Count > 1)
                _remainingTurns.RemoveRange(1, _remainingTurns.Count - 1);
            _orderRoot.sizeDelta = new Vector2(Mathf.Max(1, _remainingTurns.Count) * TurnIconWidth, 112f);
            for (int i = 0; i < _turnChips.Length; i++)
            {
                int id = i < _remainingTurns.Count ? _remainingTurns[i] : 0;
                _turnChips[i].gameObject.SetActive(id != 0);
                if (_shownOrder[i] == id)
                    continue;
                _shownOrder[i] = id;
                if (id == 0)
                    continue;
                _turnPositions[i].text = i == 0 ? "NOW" : i == 1 ? "NEXT" : (i + 1).ToString();
                _turnPositions[i].color = i == 0 ? Kit.Text : Kit.Soft;
                _turnNames[i].text = id == _controller.ViewerId ? "You" : PlayerNames.Get(id);
                _turnBorders[i].color = i == 0 ? Kit.Text : ApostleColors.Get(id);
                _turnInitials[i].text = id.ToString();
            }
        }

        private void ShowIcons()
        {
            for (int i = 0; i < _shownOrder.Length; i++)
            {
                int id = _shownOrder[i];
                if (id == 0)
                    continue;
                Sprite icon = _facePortrait;
                _turnIcons[i].enabled = icon != null;
                if (_turnIcons[i].sprite != icon)
                    _turnIcons[i].sprite = icon;
                AvatarSkin skin = AvatarSkin.Current;
                _turnIcons[i].color = skin != null ? skin.Tinted(ApostleColors.Get(id)) : ApostleColors.Get(id);
                _turnInitials[i].gameObject.SetActive(icon == null);
            }
        }

        private void OnDestroy()
        {
            if (_orderRoot != null)
                Destroy(_orderRoot.gameObject);
            if (_facePortrait != null)
                Destroy(_facePortrait);
        }

        // "Your turn" or the acting Apostle's name, updated only when it changes.
        private void ShowWho()
        {
            var match = _controller.CurrentMatch;
            if (match == null)
                return;
            bool choosing = match.IsAwaitingPhaseStart;
            int actor = choosing ? match.GetNextPhaseChooserId()
                : match.CurrentApostle != null ? match.CurrentApostle.Id : 0;
            int key = choosing ? -actor : actor;
            bool local = choosing ? _controller.IsLocalPhaseChooser : _controller.IsLocalTurn;
            if (key == _shownActor && local == _shownLocal)
                return;

            _shownActor = key;
            _shownLocal = local;
            if (local)
                _who.text = choosing ? "Choose a direction" : "Your turn";
            else
                _who.text = PlayerNames.Get(actor) + (choosing ? " chooses" : "'s turn");
            _who.color = local ? Kit.Text : Kit.Soft;
        }
    }
}
