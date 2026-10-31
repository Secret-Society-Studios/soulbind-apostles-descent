using SoulBind.Core;
using SoulBind.Managers;
using SoulBind.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoulBind.UI
{
    /// <summary>
    /// The play queue, top-left (the match layout mockup). While an attack builds, the cards played
    /// into it line up in order: a number, a small card in its element's colour, the name and stats.
    /// Cards whose element combo is active glow and carry a COMBO tag. Under the list sit the four
    /// element counters (they replace the old combo panel), the total ATK and its target, and the
    /// ATTACK button, which Strikes. MatchHud creates it at runtime, so it needs no setup.
    /// </summary>
    public sealed class PlayQueue : MonoBehaviour
    {
        // Sizes in canvas pixels (the canvas is 1920 x 1080). Phones draw it at PhoneScale.
        private const float Width = 330f;
        private const float Left = 30f;
        private const float Top = 150f;           // below the portraits / turn order
        private const float Pad = 14f;
        private const float HeaderHeight = 26f;
        private const float RowHeight = 64f;
        private const float RowGap = 8f;
        private const float ThumbWidth = 40f;
        private const float ThumbHeight = 56f;
        private const float ChipHeight = 26f;
        private const float TotalHeight = 30f;
        private const float ButtonHeight = 62f;
        private const float PhoneScale = 0.8f;
        private const int MaxRows = 8;             // more cards than this are not listed

        private static readonly string[] ElementNames = { "RADIANCE", "CELESTE", "CRIMSON", "NOCTURNE" };
        private static readonly Color RowColor = new Color(0.13f, 0.10f, 0.18f, 1f);
        private static readonly Color RowEdge = new Color(0.23f, 0.17f, 0.28f, 1f);
        private static readonly Color Parchment = new Color(0.92f, 0.86f, 0.71f, 1f);
        private static readonly Color SubText = new Color(0.81f, 0.76f, 0.68f, 1f);
        private static readonly Color TargetBlue = new Color(0.44f, 0.63f, 1f, 1f);

        private sealed class Row
        {
            public RectTransform Rect;
            public Image Background;
            public Outline Edge;
            public Image ThumbFrame;
            public Image ThumbBand;
            public TMP_Text Number;
            public TMP_Text Name;
            public TMP_Text Stats;
            public GameObject ComboTag;
            public Image ComboTagImage;
            public Button Back;
        }

        private sealed class Chip
        {
            public RectTransform Rect;
            public Image Background;
            public Outline Ring;
            public TMP_Text Label;
            public Color Color;
        }

        private readonly Row[] _rows = new Row[MaxRows];
        private readonly Chip[] _chips = new Chip[ElementCombo.ElementCount];
        private MatchController _controller;
        private RectTransform _root;
        private TMP_Text _title;
        private TMP_Text _info;
        private TMP_Text _empty;
        private TMP_Text _total;
        private TMP_Text _target;
        private Button _attack;
        private TMP_Text _attackLabel;

        public static PlayQueue Create(RectTransform hudRoot, MatchController controller)
        {
            var go = new GameObject("PlayQueue", typeof(RectTransform));
            go.layer = hudRoot.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(hudRoot, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(Left, -Top);
            if (Application.isMobilePlatform)
                rect.localScale = Vector3.one * PhoneScale;

            PlayQueue queue = go.AddComponent<PlayQueue>();
            queue._controller = controller;
            queue._root = rect;
            queue.Build(rect);
            controller.StateChanged += queue.Refresh;
            queue.Refresh();
            return queue;
        }

        private void OnDestroy()
        {
            if (_controller != null)
                _controller.StateChanged -= Refresh;
        }

        // ---------- Building ----------

        private void Build(RectTransform root)
        {
            Image panel = AddImage(root.gameObject);
            GothicTheme.StylePanel(panel, true);

            float inner = Width - 2f * Pad;
            _title = NewText("Title", root, "PLAY QUEUE", 16f, FontStyles.Bold);
            Place(_title.rectTransform, Pad, Pad, inner * 0.5f, HeaderHeight);
            _title.color = GothicTheme.Gold;
            _title.characterSpacing = 4f;
            _info = NewText("Info", root, "", 15f, FontStyles.Normal);
            Place(_info.rectTransform, Pad + inner * 0.4f, Pad, inner * 0.6f, HeaderHeight);
            _info.alignment = TextAlignmentOptions.Right;
            _info.color = SubText;

            for (int i = 0; i < _rows.Length; i++)
                _rows[i] = BuildRow(root, i);

            _empty = NewText("Empty", root, "", 15f, FontStyles.Normal);
            _empty.alignment = TextAlignmentOptions.Center;
            _empty.color = SubText;

            for (int e = 0; e < _chips.Length; e++)
                _chips[e] = BuildChip(root, e);

            _total = NewText("Total", root, "", 18f, FontStyles.Normal);
            _total.color = SubText;
            _target = NewText("Target", root, "", 18f, FontStyles.Bold);
            _target.alignment = TextAlignmentOptions.Right;
            _target.color = TargetBlue;

            RectTransform button = NewRect("AttackButton", root);
            Image buttonImage = AddImage(button.gameObject);
            buttonImage.raycastTarget = true;
            buttonImage.color = GothicTheme.StrikeButtonColor;
            _attack = button.gameObject.AddComponent<Button>();
            _attack.targetGraphic = buttonImage;
            _attack.onClick.AddListener(OnButton);
            GothicTheme.StyleButton(_attack);
            _attackLabel = NewText("Label", button, "ATTACK", 26f, FontStyles.Bold);
            Stretch(_attackLabel.rectTransform);
            _attackLabel.alignment = TextAlignmentOptions.Center;
            _attackLabel.color = GothicTheme.GoldBright;
            _attackLabel.characterSpacing = 6f;
        }

        // ATTACK strikes in an attack; PLAY resolves cards played without one.
        private void OnButton()
        {
            Match match = _controller.CurrentMatch;
            if (match == null)
                return;
            if (match.Engagement == EngagementState.Engaged)
                _controller.Strike();
            else
                _controller.PlayQueued();
        }

        private Row BuildRow(RectTransform root, int index)
        {
            float inner = Width - 2f * Pad;
            var row = new Row { Rect = NewRect("Row" + (index + 1), root) };
            row.Rect.sizeDelta = new Vector2(inner, RowHeight);
            row.Background = AddImage(row.Rect.gameObject);
            row.Edge = row.Rect.gameObject.AddComponent<Outline>();
            row.Edge.effectDistance = new Vector2(2f, -2f);

            // Tapping a row takes that card back into the hand.
            row.Background.raycastTarget = true;
            row.Back = row.Rect.gameObject.AddComponent<Button>();
            row.Back.targetGraphic = row.Background;
            row.Back.transition = Selectable.Transition.None;
            int slot = index;
            row.Back.onClick.AddListener(() => _controller.ReturnQueuedCard(slot));

            // The number in a small gold-rimmed badge.
            RectTransform badge = NewRect("Badge", row.Rect);
            Place(badge, 10f, (RowHeight - 28f) / 2f, 28f, 28f);
            Image badgeImage = AddImage(badge.gameObject);
            badgeImage.color = new Color(0.07f, 0.05f, 0.09f, 1f);
            Outline badgeRing = badge.gameObject.AddComponent<Outline>();
            badgeRing.effectColor = GothicTheme.Gold;
            badgeRing.effectDistance = new Vector2(1.5f, -1.5f);
            row.Number = NewText("Number", badge, "", 15f, FontStyles.Bold);
            Stretch(row.Number.rectTransform);
            row.Number.alignment = TextAlignmentOptions.Center;
            row.Number.color = GothicTheme.GoldBright;

            // A tiny card: element-coloured frame, parchment face, a coloured band on top.
            RectTransform thumb = NewRect("Thumb", row.Rect);
            Place(thumb, 48f, (RowHeight - ThumbHeight) / 2f, ThumbWidth, ThumbHeight);
            row.ThumbFrame = AddImage(thumb.gameObject);
            RectTransform face = NewRect("Face", thumb);
            Stretch(face);
            face.offsetMin = new Vector2(3f, 3f);
            face.offsetMax = new Vector2(-3f, -3f);
            AddImage(face.gameObject).color = Parchment;
            RectTransform band = NewRect("Band", face);
            band.anchorMin = new Vector2(0f, 0.6f);
            band.anchorMax = Vector2.one;
            band.offsetMin = band.offsetMax = Vector2.zero;
            row.ThumbBand = AddImage(band.gameObject);

            float textX = 48f + ThumbWidth + 12f;
            row.Name = NewText("Name", row.Rect, "", 17f, FontStyles.Bold);
            Place(row.Name.rectTransform, textX, 8f, inner - textX - 8f, 26f);
            row.Name.color = GothicTheme.TextColor;
            row.Name.overflowMode = TextOverflowModes.Ellipsis;
            row.Name.textWrappingMode = TextWrappingModes.NoWrap;
            row.Stats = NewText("Stats", row.Rect, "", 14f, FontStyles.Normal);
            Place(row.Stats.rectTransform, textX, 34f, inner - textX - 8f, 22f);
            row.Stats.color = SubText;

            // COMBO tag on the right edge, only on cards whose combo is active.
            RectTransform tag = NewRect("ComboTag", row.Rect);
            tag.anchorMin = tag.anchorMax = new Vector2(1f, 1f);
            tag.pivot = new Vector2(1f, 1f);
            tag.anchoredPosition = new Vector2(-6f, -6f);
            tag.sizeDelta = new Vector2(70f, 20f);
            row.ComboTagImage = AddImage(tag.gameObject);
            TMP_Text tagText = NewText("Text", tag, "COMBO", 12f, FontStyles.Bold);
            Stretch(tagText.rectTransform);
            tagText.alignment = TextAlignmentOptions.Center;
            row.ComboTag = tag.gameObject;
            return row;
        }

        private Chip BuildChip(RectTransform root, int element)
        {
            var chip = new Chip
            {
                Rect = NewRect(ElementNames[element], root),
                Color = CardView.GetElementColor((SoulBind.Core.Element)element),
            };
            chip.Background = AddImage(chip.Rect.gameObject);
            chip.Ring = chip.Rect.gameObject.AddComponent<Outline>();
            chip.Ring.effectColor = Color.white;
            chip.Ring.effectDistance = new Vector2(2f, -2f);
            chip.Label = NewText("Label", chip.Rect, "", 11f, FontStyles.Bold);
            Stretch(chip.Label.rectTransform);
            chip.Label.alignment = TextAlignmentOptions.Center;
            chip.Label.color = new Color(0.07f, 0.05f, 0.10f, 1f);
            return chip;
        }

        // ---------- Showing the turn ----------

        // Reads the turn in progress after every change.
        private void Refresh()
        {
            Match match = _controller != null ? _controller.CurrentMatch : null;
            bool visible = match != null && !match.IsOver && match.CurrentApostle != null;
            _root.gameObject.SetActive(visible);
            if (!visible)
                return;

            bool yours = match.CurrentApostle.Id == _controller.ViewerId;
            bool engaged = match.Engagement == EngagementState.Engaged;
            var cards = match.AttackCards;
            int shown = Mathf.Min(cards.Count, MaxRows);
            float inner = Width - 2f * Pad;
            float y = Pad + HeaderHeight + 8f;

            int capacity = 0;
            for (int i = 0; i < _rows.Length; i++)
            {
                Row row = _rows[i];
                bool used = i < shown;
                row.Rect.gameObject.SetActive(used);
                if (!used)
                    continue;
                CardDefinition card = CardCatalog.Get(cards[i]);
                capacity += card.Cap;
                ShowRow(row, i, card, CardAttack.Of(cards[i]), match.IsComboTriggered(card.Element));
                row.Back.interactable = yours && _controller.IsLocalTurn;
                Place(row.Rect, Pad, y, inner, RowHeight);
                y += RowHeight + RowGap;
            }

            _title.text = yours ? "PLAY QUEUE" : PlayerNames.Get(match.CurrentApostle.Id).ToUpperInvariant();
            _info.text = shown == 1 ? $"1 card · {capacity} Capacity" : $"{shown} cards · {capacity} Capacity";

            // Empty: how to start. Your own queue: how to take a card back.
            bool canReturn = shown > 0 && yours && _controller.IsLocalTurn;
            _empty.gameObject.SetActive(shown == 0 || canReturn);
            if (shown == 0 || canReturn)
            {
                if (canReturn)
                    _empty.text = "Tap a card to take it back";
                else
                    _empty.text = yours ? "Play a card, or drag one onto an enemy" : "No cards yet";
                Place(_empty.rectTransform, Pad, y, inner, 40f);
                y += 40f + RowGap;
            }

            y = ShowChips(match, y + 4f, inner);

            _total.gameObject.SetActive(engaged);
            _target.gameObject.SetActive(engaged);
            if (engaged)
            {
                _total.text = $"Total <b><color=#F5EBD8>ATK {match.PendingAttack}</color></b>";
                _target.text = "on " + PlayerNames.Get(match.EngagedTargetId);
                Place(_total.rectTransform, Pad, y, inner * 0.5f, TotalHeight);
                Place(_target.rectTransform, Pad + inner * 0.4f, y, inner * 0.6f, TotalHeight);
                y += TotalHeight + 6f;
            }

            // Only the player sees the button: ATTACK in an attack, PLAY for cards played without one.
            bool canStrike = yours && shown > 0 && _controller.IsLocalTurn;
            _attack.gameObject.SetActive(canStrike);
            if (canStrike)
            {
                _attackLabel.text = engaged ? "ATTACK" : "PLAY";
                ((Image)_attack.targetGraphic).color = engaged ? GothicTheme.StrikeButtonColor : GothicTheme.ButtonColor;
                Place((RectTransform)_attack.transform, Pad, y, inner, ButtonHeight);
                y += ButtonHeight;
            }
            _root.sizeDelta = new Vector2(Width, y + Pad);
        }

        private static void ShowRow(Row row, int index, CardDefinition card, int attack, bool combo)
        {
            Color element = CardView.GetElementColor(card.Element);
            row.Number.text = (index + 1).ToString();
            row.Name.text = card.Name;
            row.Stats.text = $"{card.Element} · A{attack} D{card.Defense} C{card.Cap}";
            row.ThumbFrame.color = element;
            row.ThumbBand.color = element;

            // A card whose combo is active glows in its element's colour.
            row.Background.color = combo ? Color.Lerp(RowColor, element, 0.35f) : RowColor;
            row.Edge.effectColor = combo ? element : RowEdge;
            row.ComboTag.SetActive(combo);
            row.ComboTagImage.color = element;
        }

        // The four element counters in a row; a triggered combo is bright with a white ring.
        private float ShowChips(Match match, float y, float inner)
        {
            float gap = 4f;
            float width = (inner - gap * (_chips.Length - 1)) / _chips.Length;
            for (int e = 0; e < _chips.Length; e++)
            {
                Chip chip = _chips[e];
                var element = (SoulBind.Core.Element)e;
                bool active = match.IsComboTriggered(element);
                int played = Mathf.Min(match.GetElementsPlayedThisTurn(element), ElementCombo.CardsNeeded);
                chip.Label.text = $"{ElementNames[e].Substring(0, 3)} {played}/{ElementCombo.CardsNeeded}";
                Color color = chip.Color;
                color.a = active ? 1f : 0.55f;
                chip.Background.color = color;
                chip.Ring.enabled = active;
                Place(chip.Rect, Pad + e * (width + gap), y, width, ChipHeight);
            }
            return y + ChipHeight + 10f;
        }

        // ---------- Layout helpers (positions from the top-left corner, in pixels) ----------

        private static Image AddImage(GameObject go)
        {
            var image = (Image)go.AddComponent(typeof(Image));
            image.raycastTarget = false;
            return image;
        }

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

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static TMP_Text NewText(string name, Transform parent, string text, float size, FontStyles style)
        {
            RectTransform rect = NewRect(name, parent);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            if (GothicTheme.TitleFont != null && style == FontStyles.Bold)
                tmp.font = GothicTheme.TitleFont;
            return tmp;
        }
    }
}