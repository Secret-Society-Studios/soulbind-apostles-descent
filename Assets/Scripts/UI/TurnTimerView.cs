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
        private const float Width = 300f;
        private const float Height = 64f;
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

        public static TurnTimerView Create(RectTransform hudRoot, MatchController controller)
        {
            RectTransform rect = Kit.Box(hudRoot, "TurnTimer", new Color(0.03f, 0.02f, 0.05f, 0.9f), Kit.GoldLine, 12f, 2f);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(Left, -Top);
            rect.sizeDelta = new Vector2(Width, Height);
            rect.GetComponent<Image>().raycastTarget = false;
            if (Application.isMobilePlatform)
                rect.localScale = Vector3.one * PhoneScale;

            TurnTimerView view = rect.gameObject.AddComponent<TurnTimerView>();
            view._controller = controller;
            view.Build(rect);
            return view;
        }

        // ---------- Building ----------

        private void Build(RectTransform root)
        {
            _group = root.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.alpha = 0f;

            _who = Kit.Label(root, "Who", "", 16f, Kit.InterSemiBold, Kit.Soft, TextAlignmentOptions.Left);
            Kit.At(_who.rectTransform, 16f, 8f, Width - 110f, Height - 20f);
            _who.textWrappingMode = TextWrappingModes.NoWrap;
            _who.overflowMode = TextOverflowModes.Ellipsis;

            _seconds = Kit.Label(root, "Seconds", "", 30f, Kit.Display, Kit.Title, TextAlignmentOptions.Right);
            Kit.At(_seconds.rectTransform, Width - 100f, 4f, 84f, Height - 16f);

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

        // ---------- Showing ----------

        private void Update()
        {
            bool running = _controller != null && _controller.IsTurnTimerRunning;
            _group.alpha = running ? 1f : 0f;
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
