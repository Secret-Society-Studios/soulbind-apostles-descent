using SoulBind.Core;
using SoulBind.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoulBind.UI
{
    /// <summary>
    /// The element combos of the turn in progress, in a column in the bottom-left corner of the screen: one box
    /// per element showing how
    /// many of its cards were played (0/2, 1/2) and what its combo does. When a combo triggers, its box
    /// lights up in the element's colour, flashes, and "COMBO!" appears; it stays lit for the rest of the
    /// turn. MatchHud creates it at runtime, so it needs no setup. Display only.
    /// </summary>
    public sealed class ComboTracker : MonoBehaviour
    {
        private const float Width = 230f;
        private const float EdgeMargin = 20f;
        private const float BoxHeight = 46f;
        private const float TitleHeight = 26f;
        private const float Gap = 6f;
        private const float FlashTime = 1.6f;

        private static readonly string[] Names = { "Radiance", "Celeste", "Crimson", "Nocturne" };
        private static readonly string[] Effects =
        {
            "+2 shield",
            "-1 Corruption",
            "+2 ATK if attacking",
            "+1 Corruption to a foe",
        };

        private sealed class Box
        {
            public Image Background;
            public Image Frame;
            public TMP_Text Name;
            public TMP_Text Count;
            public TMP_Text Effect;
            public Color Color;
            public bool Triggered;
            public float Flash = -1f;
        }

        private readonly Box[] _boxes = new Box[ElementCombo.ElementCount];
        private MatchController _controller;
        private CanvasGroup _group;
        private TMP_Text _title;
        private bool _picking;
        private int _turnOwner = -1;
        private int _turnRound = -1;

        public static ComboTracker Create(RectTransform hudRoot, MatchController controller, RectTransform below)
        {
            var go = new GameObject("ComboTracker", typeof(RectTransform), typeof(CanvasGroup));
            go.layer = hudRoot.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(below != null ? below.parent : hudRoot, false);

            // A column in the bottom-left corner, just above the hint line (clear of the card preview on the right).
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            float height = TitleHeight + ElementCombo.ElementCount * BoxHeight + (ElementCombo.ElementCount + 1) * Gap + 8f;
            rect.sizeDelta = new Vector2(Width, height);
            float above = 90f;
            Transform hint = rect.parent.Find("HintText");
            if (hint is RectTransform hintRect && hintRect.anchorMin == Vector2.zero && hintRect.anchorMax == Vector2.zero)
                above = hintRect.anchoredPosition.y + hintRect.sizeDelta.y * (1f - hintRect.pivot.y) + 8f;
            rect.anchoredPosition = new Vector2(EdgeMargin, above);

            ComboTracker tracker = go.AddComponent<ComboTracker>();
            tracker._controller = controller;
            tracker._group = go.GetComponent<CanvasGroup>();
            tracker._group.blocksRaycasts = false;
            tracker.Build(rect);
            controller.StateChanged += tracker.Refresh;
            tracker.Refresh();
            return tracker;
        }

        private void OnDestroy()
        {
            if (_controller != null)
                _controller.StateChanged -= Refresh;
        }

        private void Build(RectTransform root)
        {
            Image panel = root.gameObject.AddComponent<Image>();
            panel.raycastTarget = false;
            GothicTheme.StylePanel(panel, true);

            _title = NewText("Title", root, "COMBOS THIS TURN", 15f, FontStyles.Bold);
            Place(_title.rectTransform, 10f, 4f, Width - 20f, TitleHeight);
            _title.alignment = TextAlignmentOptions.Center;
            _title.color = GothicTheme.Gold;

            float boxWidth = Width - 2f * Gap - 8f;
            for (int e = 0; e < _boxes.Length; e++)
            {
                float x = 4f + Gap;
                float y = 4f + TitleHeight + Gap + e * (BoxHeight + Gap);

                var box = new Box { Color = CardView.GetElementColor((SoulBind.Core.Element)e) };
                RectTransform boxRect = NewRect(Names[e], root);
                Place(boxRect, x, y, boxWidth, BoxHeight);
                box.Background = boxRect.gameObject.AddComponent<Image>();
                box.Background.raycastTarget = false;

                RectTransform frameRect = NewRect("Frame", boxRect);
                frameRect.anchorMin = Vector2.zero;
                frameRect.anchorMax = Vector2.one;
                frameRect.offsetMin = Vector2.zero;
                frameRect.offsetMax = Vector2.zero;
                box.Frame = frameRect.gameObject.AddComponent<Image>();
                box.Frame.raycastTarget = false;
                Outline outline = frameRect.gameObject.AddComponent<Outline>();
                outline.effectColor = box.Color;
                outline.effectDistance = new Vector2(2f, -2f);
                outline.useGraphicAlpha = false; // the frame image itself is invisible; only its outline shows
                box.Frame.color = new Color(0f, 0f, 0f, 0f);

                box.Name = NewText("Name", boxRect, Names[e].ToUpperInvariant(), 14f, FontStyles.Bold);
                Place(box.Name.rectTransform, 8f, 3f, boxWidth - 104f, 20f);
                box.Count = NewText("Count", boxRect, "0/2", 14f, FontStyles.Bold);
                Place(box.Count.rectTransform, boxWidth - 96f, 3f, 90f, 20f);
                box.Count.alignment = TextAlignmentOptions.Right;
                box.Effect = NewText("Effect", boxRect, Effects[e], 11.5f, FontStyles.Normal);
                Place(box.Effect.rectTransform, 8f, 23f, boxWidth - 14f, 18f);
                _boxes[e] = box;
                Show(box, 0, false);
            }
        }

        // Reads the turn in progress after every change.
        private void Refresh()
        {
            Match match = _controller != null ? _controller.CurrentMatch : null;
            bool visible = match != null && !match.IsOver && match.CurrentApostle != null;
            _group.alpha = visible ? 1f : 0f;
            if (!visible)
                return;

            // A new turn: the boxes start over, without flashing.
            bool newTurn = match.CurrentApostle.Id != _turnOwner || match.Turns.RoundNumber != _turnRound;
            _turnOwner = match.CurrentApostle.Id;
            _turnRound = match.Turns.RoundNumber;
            _title.text = match.CurrentApostle.Id == _controller.ViewerId
                ? "YOUR COMBOS THIS TURN"
                : $"APOSTLE {match.CurrentApostle.Id}'S COMBOS";

            for (int e = 0; e < _boxes.Length; e++)
            {
                var element = (SoulBind.Core.Element)e;
                Box box = _boxes[e];
                bool triggered = match.IsComboTriggered(element);
                if (triggered && !box.Triggered && !newTurn)
                    box.Flash = 0f; // just triggered: flash
                if (newTurn)
                    box.Flash = -1f;
                box.Triggered = triggered;
                Show(box, Mathf.Min(match.GetElementsPlayedThisTurn(element), ElementCombo.CardsNeeded), triggered);
            }

            // The Nocturne combo is waiting for the player to pick a foe: its box asks for it.
            _picking = _controller.IsChoosingNocturneTarget;
            if (!_picking)
                _boxes[(int)SoulBind.Core.Element.Nocturne].Background.rectTransform.localScale = Vector3.one;
        }

        private void Show(Box box, int played, bool triggered)
        {
            Color dim = Color.Lerp(new Color(0.05f, 0.04f, 0.07f), box.Color, played == 1 ? 0.3f : 0.12f);
            box.Background.color = triggered ? Color.Lerp(box.Color, Color.black, 0.25f) : dim;
            box.Count.text = triggered ? "ACTIVE" : $"{played}/{ElementCombo.CardsNeeded}";
            Color text = triggered ? Color.white : played == 1 ? new Color(1f, 1f, 1f, 0.9f) : new Color(1f, 1f, 1f, 0.45f);
            box.Name.color = text;
            box.Count.color = triggered ? GothicTheme.GoldBright : text;
            box.Effect.color = triggered ? Color.white : new Color(1f, 1f, 1f, played == 1 ? 0.75f : 0.4f);
            box.Frame.color = new Color(0f, 0f, 0f, 0f);
        }

        // A combo that just triggered flashes white in its colour, then settles lit.
        private void Update()
        {
            if (_picking)
            {
                Box nocturne = _boxes[(int)SoulBind.Core.Element.Nocturne];
                float pulse = (Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / 0.9f) + 1f) / 2f;
                nocturne.Background.color = Color.Lerp(Color.Lerp(nocturne.Color, Color.black, 0.2f), Color.white, pulse * 0.35f);
                nocturne.Background.rectTransform.localScale = Vector3.one * (1f + 0.05f * pulse);
                nocturne.Count.text = "PICK A FOE";
                nocturne.Count.color = GothicTheme.GoldBright;
                nocturne.Name.color = Color.white;
                nocturne.Effect.color = Color.white;
            }

            for (int e = 0; e < _boxes.Length; e++)
            {
                Box box = _boxes[e];
                if (box.Flash < 0f)
                    continue;
                box.Flash += Time.unscaledDeltaTime;
                float t = box.Flash / MotionStyle.Seconds(FlashTime);
                if (t >= 1f)
                {
                    box.Flash = -1f;
                    box.Background.color = Color.Lerp(box.Color, Color.black, 0.25f);
                    box.Background.rectTransform.localScale = Vector3.one;
                    box.Count.text = "ACTIVE";
                    continue;
                }
                float pulse = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 3f)) * (1f - t);
                box.Background.color = Color.Lerp(Color.Lerp(box.Color, Color.black, 0.25f), Color.white, pulse * 0.6f);
                box.Background.rectTransform.localScale = Vector3.one * (1f + 0.06f * pulse);
                box.Count.text = "COMBO!";
            }
        }

        // ---- Layout helpers (positions from the top-left corner, in pixels) ----

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static TMP_Text NewText(string name, Transform parent, string text, float size, FontStyles style)
        {
            RectTransform rect = NewRect(name, parent);
            TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            if (GothicTheme.TitleFont != null && style == FontStyles.Bold)
                tmp.font = GothicTheme.TitleFont;
            return tmp;
        }
    }
}