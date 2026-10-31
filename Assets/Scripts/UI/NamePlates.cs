using System.Collections.Generic;
using SoulBind.Core;
using SoulBind.Entities;
using SoulBind.Managers;
using SoulBind.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoulBind.UI
{
    /// <summary>
    /// A name plate floating above every Apostle's piece, like a MOBA health bar: a Souls badge,
    /// the name, the Vessel bar, a thin Corruption bar and DEF. They replace the opponents' portrait
    /// row at the top. While a combo target is being picked, a plate can be tapped to pick that Apostle.
    /// MatchHud creates it at runtime, so it needs no setup. Bars use UiSkin art when it is there.
    /// </summary>
    public sealed class NamePlates : MonoBehaviour
    {
        // Sizes in canvas pixels (the canvas is 1920 x 1080). Phones draw plates at PhoneScale.
        private const float Width = 200f;
        private const float Height = 62f;
        private const float BadgeSize = 40f;
        private const float BarLeft = 54f;
        private const float BarWidth = 138f;
        private const float GapAbovePiece = 10f;  // between the top of the piece and the plate
        private const float PhoneScale = 1.2f;
        private const int CorruptionBarMax = 15;  // the bar is full here (Corruption has no cap)

        private static readonly Color VesselColor = new Color(0.80f, 0.20f, 0.25f);
        private static readonly Color CorruptionColor = new Color(0.55f, 0.30f, 0.70f);
        private static readonly Color CorruptedColor = new Color(0.85f, 0.15f, 0.30f);
        private static readonly Color SoulsColor = new Color(0.95f, 0.78f, 0.35f);
        private static readonly Color PickableColor = new Color(0.55f, 0.20f, 0.45f, 0.95f);

        private sealed class Plate
        {
            public int ApostleId;
            public RectTransform Rect;
            public Image Background;
            public Image Frame;
            public Button Button;
            public TMP_Text Name;
            public TMP_Text Souls;
            public TMP_Text Tag;
            public TMP_Text Vessel;
            public TMP_Text Defense;
            public RectTransform VesselFill;
            public Image VesselFillImage;
            public RectTransform CorruptionFill;
            public Image CorruptionFillImage;
            public ApostleView Piece;
        }

        private readonly List<Plate> _plates = new List<Plate>(GameConstants.MaxPlayers);
        private MatchController _controller;
        private RectTransform _root;
        private Canvas _canvas;

        public static NamePlates Create(RectTransform hudRoot, MatchController controller)
        {
            var go = new GameObject("NamePlates", typeof(RectTransform));
            go.layer = hudRoot.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(hudRoot, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.SetAsFirstSibling(); // under every other HUD panel

            NamePlates plates = go.AddComponent<NamePlates>();
            plates._controller = controller;
            plates._root = rect;
            plates._canvas = hudRoot.GetComponentInParent<Canvas>();
            controller.StateChanged += plates.Refresh;
            plates.Refresh();
            return plates;
        }

        private void OnDestroy()
        {
            if (_controller != null)
                _controller.StateChanged -= Refresh;
        }

        // ---------- Following the pieces ----------

        // After the pieces moved this frame (MatchController.Update), each plate sits above its piece.
        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null || _canvas == null)
                return;
            Camera uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;

            for (int i = 0; i < _plates.Count; i++)
            {
                Plate plate = _plates[i];
                if (plate.Piece == null)
                    plate.Piece = _controller.GetPieceView(plate.ApostleId);

                bool onBoard = plate.Piece != null && plate.Piece.gameObject.activeInHierarchy;
                Vector3 screen = onBoard ? cam.WorldToScreenPoint(plate.Piece.TopPoint) : Vector3.zero;
                bool visible = onBoard && screen.z > 0f && IsAlive(plate.ApostleId);
                if (plate.Rect.gameObject.activeSelf != visible)
                    plate.Rect.gameObject.SetActive(visible);
                if (!visible)
                    continue;

                RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, uiCamera, out Vector2 local);
                plate.Rect.anchoredPosition = local + new Vector2(0f, GapAbovePiece);
            }
        }

        private bool IsAlive(int apostleId)
        {
            Match match = _controller.CurrentMatch;
            if (match == null || match.IsOver)
                return false;
            IReadOnlyList<ApostleState> apostles = match.Battlefield.Apostles;
            for (int i = 0; i < apostles.Count; i++)
            {
                if (apostles[i].Id == apostleId)
                    return !apostles[i].IsEliminated;
            }
            return false;
        }

        // ---------- Showing the numbers ----------

        // Reads every Apostle after each change.
        private void Refresh()
        {
            Match match = _controller != null ? _controller.CurrentMatch : null;
            if (match == null)
                return;
            if (_plates.Count == 0)
                Build(match);

            AscensionTracker ascension = match.Ascension;
            for (int i = 0; i < _plates.Count; i++)
            {
                Plate plate = _plates[i];
                ApostleState apostle = FindApostle(match, plate.ApostleId);
                if (apostle == null)
                    continue;

                bool isCurrent = apostle == match.CurrentApostle;
                bool isAwakened = ascension.HasAwakened && ascension.AwakenedId == apostle.Id;
                bool isPickable = _controller.IsChoosingComboTarget && !isCurrent && !apostle.IsEliminated;

                plate.Souls.text = apostle.Souls.ToString();
                plate.Vessel.text = $"{apostle.Vessel}/{GameConstants.MaxVessel}";
                plate.Defense.text = apostle.Defense > 0 ? $"DEF {apostle.Defense}" : string.Empty;
                SetFill(plate.VesselFill, plate.VesselFillImage, (float)apostle.Vessel / GameConstants.MaxVessel);
                SetFill(plate.CorruptionFill, plate.CorruptionFillImage, (float)apostle.Corruption / CorruptionBarMax);
                if (KeepsGameColours(plate.CorruptionFillImage))
                    plate.CorruptionFillImage.color = apostle.IsCorrupted ? CorruptedColor : CorruptionColor;

                plate.Tag.text = isPickable ? "TAP TO PICK"
                    : isAwakened ? "AWAKENED"
                    : apostle.IsSoullocked ? "SOULLOCKED"
                    : string.Empty;

                plate.Background.color = isPickable ? PickableColor : GothicTheme.PanelColor;
                plate.Frame.color = isCurrent ? GothicTheme.GoldBright : GothicTheme.GoldDim;
                plate.Background.raycastTarget = isPickable;
                plate.Button.interactable = isPickable;
            }
        }

        private static ApostleState FindApostle(Match match, int id)
        {
            IReadOnlyList<ApostleState> apostles = match.Battlefield.Apostles;
            for (int i = 0; i < apostles.Count; i++)
            {
                if (apostles[i].Id == id)
                    return apostles[i];
            }
            return null;
        }

        // A placeholder fill stretches to the fraction; a skinned (Filled) one uses fillAmount.
        private static void SetFill(RectTransform fill, Image image, float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            if (image != null && image.type == Image.Type.Filled)
            {
                fill.anchorMax = Vector2.one;
                image.fillAmount = fraction;
                return;
            }
            fill.anchorMax = new Vector2(fraction, 1f);
        }

        private static bool KeepsGameColours(Image fill)
        {
            UiSkin skin = UiSkin.Current;
            return fill.type != Image.Type.Filled || skin == null || skin.TintFills;
        }

        // A tap on a pickable plate picks that Apostle for the combo.
        private void OnPlateClicked(int apostleId)
        {
            if (!_controller.IsChoosingComboTarget)
                return;
            ApostleState current = _controller.CurrentMatch.CurrentApostle;
            if (current != null && current.Id != apostleId)
                _controller.ChooseComboTarget(apostleId);
        }

        // ---------- Building ----------

        private void Build(Match match)
        {
            float scale = Application.isMobilePlatform ? PhoneScale : 1f;
            foreach (ApostleState apostle in match.Battlefield.Apostles)
            {
                Plate plate = BuildPlate(apostle.Id);
                plate.Rect.localScale = Vector3.one * scale;
                _plates.Add(plate);
            }

            // Real bar art (UiSkin) goes over the drawn bars, the same as on the portraits.
            UiSkin skin = UiSkin.Current;
            if (skin == null)
                return;
            for (int i = 0; i < _plates.Count; i++)
                skin.ApplyToPortrait(_plates[i].Rect);
        }

        private Plate BuildPlate(int apostleId)
        {
            var plate = new Plate { ApostleId = apostleId };
            plate.Rect = NewRect("Plate " + apostleId, _root);
            plate.Rect.anchorMin = plate.Rect.anchorMax = new Vector2(0.5f, 0.5f);
            plate.Rect.pivot = new Vector2(0.5f, 0f); // bottom centre sits on the piece
            plate.Rect.sizeDelta = new Vector2(Width, Height);

            plate.Background = AddImage(plate.Rect.gameObject);
            GothicTheme.StylePanel(plate.Background, false);
            plate.Frame = GothicTheme.AddThemeFrame(plate.Rect, GothicTheme.GoldDim);
            plate.Button = plate.Rect.gameObject.AddComponent<Button>();
            plate.Button.targetGraphic = plate.Background;
            plate.Button.transition = Selectable.Transition.None;
            int id = apostleId;
            plate.Button.onClick.AddListener(() => OnPlateClicked(id));

            // A colour strip on the left edge: the same colour as the Apostle's disc and hand.
            RectTransform strip = NewRect("Colour", plate.Rect);
            Place(strip, 0f, 0f, 4f, Height);
            AddImage(strip.gameObject).color = ApostleColors.Get(apostleId);

            // Souls in a gold badge on the left.
            RectTransform badge = NewRect("SoulsBadge", plate.Rect);
            Place(badge, 8f, (Height - BadgeSize) / 2f, BadgeSize, BadgeSize);
            AddImage(badge.gameObject).color = new Color(0.07f, 0.05f, 0.09f, 1f);
            Outline ring = badge.gameObject.AddComponent<Outline>();
            ring.effectColor = SoulsColor;
            ring.effectDistance = new Vector2(2f, -2f);
            plate.Souls = NewText("Souls", badge, "0", 20f, FontStyles.Bold);
            Stretch(plate.Souls.rectTransform);
            plate.Souls.alignment = TextAlignmentOptions.Center;
            plate.Souls.color = SoulsColor;

            plate.Name = NewText("Name", plate.Rect, PlayerNames.Get(apostleId), 16f, FontStyles.Bold);
            Place(plate.Name.rectTransform, BarLeft, 4f, BarWidth, 20f);
            plate.Name.color = Color.Lerp(ApostleColors.Get(apostleId), Color.white, 0.45f);
            plate.Name.overflowMode = TextOverflowModes.Ellipsis;
            plate.Name.textWrappingMode = TextWrappingModes.NoWrap;

            // The Vessel bar, with its number on top.
            RectTransform vessel = NewBar("VesselBar", plate.Rect, BarLeft, 26f, BarWidth, 16f, VesselColor,
                out plate.VesselFill, out plate.VesselFillImage);
            plate.Vessel = NewText("Label", vessel, "", 12f, FontStyles.Bold);
            Stretch(plate.Vessel.rectTransform);
            plate.Vessel.alignment = TextAlignmentOptions.Center;

            // A thin Corruption bar under it, and DEF to its right.
            NewBar("CorruptionBar", plate.Rect, BarLeft, 46f, BarWidth - 52f, 8f, CorruptionColor,
                out plate.CorruptionFill, out plate.CorruptionFillImage);
            plate.Defense = NewText("Defense", plate.Rect, "", 12f, FontStyles.Bold);
            Place(plate.Defense.rectTransform, BarLeft + BarWidth - 48f, 41f, 48f, 18f);
            plate.Defense.alignment = TextAlignmentOptions.MidlineRight;
            plate.Defense.color = new Color(0.56f, 0.75f, 1f);

            // A small tag above the plate: AWAKENED, SOULLOCKED, or TAP TO PICK.
            plate.Tag = NewText("Tag", plate.Rect, "", 12f, FontStyles.Bold);
            plate.Tag.rectTransform.anchorMin = new Vector2(0f, 1f);
            plate.Tag.rectTransform.anchorMax = new Vector2(1f, 1f);
            plate.Tag.rectTransform.pivot = new Vector2(0.5f, 0f);
            plate.Tag.rectTransform.anchoredPosition = new Vector2(0f, 2f);
            plate.Tag.rectTransform.sizeDelta = new Vector2(0f, 18f);
            plate.Tag.alignment = TextAlignmentOptions.Center;
            plate.Tag.color = GothicTheme.GoldBright;

            plate.Frame.transform.SetAsLastSibling(); // the gold edge over everything inside
            return plate;
        }

        // A bar: dark back, a "Fill" child and a "ThemeFrame" child (the names UiSkin looks for).
        private static RectTransform NewBar(string name, RectTransform parent, float x, float y, float width,
            float height, Color fillColor, out RectTransform fill, out Image fillImage)
        {
            RectTransform bar = NewRect(name, parent);
            Place(bar, x, y, width, height);
            AddImage(bar.gameObject);
            fill = NewRect("Fill", bar);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            fill.pivot = new Vector2(0f, 0.5f);
            fillImage = AddImage(fill.gameObject);
            fillImage.color = fillColor;
            GothicTheme.StyleRuntimeBar(bar); // dark back, fill sheen and a faint gold "ThemeFrame"
            return bar;
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
