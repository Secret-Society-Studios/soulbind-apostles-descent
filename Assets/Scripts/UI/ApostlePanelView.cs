using System;
using SoulBind.Utility;
using SoulBind.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SoulBind.UI
{
    /// <summary>
    /// One Apostle's portrait box in the HUD: portrait, name, status, Vessel bar, Corruption bar, Souls bar and DEF.
    /// Display only. Bars are fill rectangles whose width is set from the numbers, or Filled images
    /// once UiSkin gives them real sprites.
    /// </summary>
    public sealed class ApostlePanelView : MonoBehaviour, IPointerClickHandler
    {
        // The Corruption bar is full at this value (Corruption itself has no cap).
        private const int CorruptionBarMax = 15;

        [SerializeField] private Image background;
        [Tooltip("The portrait square, tinted in the Apostle's color (placeholder until portrait art exists).")]
        [SerializeField] private Image colorStripe;
        [SerializeField] private TMP_Text portraitText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private RectTransform vesselFill;
        [SerializeField] private TMP_Text vesselText;
        [SerializeField] private RectTransform corruptionFill;
        [SerializeField] private Image corruptionFillImage;
        [SerializeField] private TMP_Text corruptionText;
        [SerializeField] private TMP_Text statsText;

        [Header("Colors")]
        [SerializeField] private Color normalBackground = new Color(0.16f, 0.14f, 0.20f, 0.95f);
        [SerializeField] private Color currentTurnBackground = new Color(0.30f, 0.26f, 0.40f, 0.95f);
        [SerializeField] private Color eliminatedBackground = new Color(0.10f, 0.10f, 0.10f, 0.80f);
        [SerializeField] private Color corruptionColor = new Color(0.55f, 0.30f, 0.70f);
        [SerializeField] private Color corruptedColor = new Color(0.85f, 0.15f, 0.30f);
        [SerializeField] private Color pickableBackground = new Color(0.55f, 0.20f, 0.45f, 0.95f);

        private static readonly Color EliminatedPortrait = new Color(0.35f, 0.35f, 0.35f);
        private static readonly Color SoulsColor = new Color(0.95f, 0.78f, 0.35f);

        // The Souls bar sits on the bottom row, left of the DEF number (pixels from the top, as built).
        private const float SoulsBarTop = 72f;
        private const float SoulsBarHeight = 16f;
        private const float SoulsBarRight = 78f;   // room for "DEF 12" on the right

        private bool _hasPortrait;
        private RectTransform _soulsFill;
        private TMP_Text _soulsText;
        private Image _vesselImage;
        private Image _corruptionImage;
        private Image _soulsImage;
        public int ApostleId { get; private set; }

        /// <summary>Raised with the Apostle's id when the portrait is left-clicked.</summary>
        public event Action<int> Clicked;

        public void Initialize(int apostleId, Color color)
        {
            ApostleId = apostleId;
            if (colorStripe != null)
                colorStripe.color = color;
            if (nameText != null)
                nameText.text = PlayerNames.Get(ApostleId);
            if (portraitText != null)
                portraitText.text = $"A{apostleId}";

            // The background catches clicks, so the portrait can be picked as a target.
            if (background != null)
                background.raycastTarget = true;

            CreateSoulsBar();
            _vesselImage = FillImage(vesselFill);
            _corruptionImage = FillImage(corruptionFill);
            _soulsImage = FillImage(_soulsFill);
        }

        // The Souls bar is a copy of the Vessel bar, made at runtime so the HUD needs no rebuild.
        private void CreateSoulsBar()
        {
            if (vesselFill == null || _soulsFill != null)
                return;
            var vesselBar = (RectTransform)vesselFill.parent;
            var bar = (RectTransform)Instantiate(vesselBar.gameObject, transform).transform;
            bar.name = "SoulsBar";
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.offsetMin = new Vector2(vesselBar.offsetMin.x, -SoulsBarTop - SoulsBarHeight);
            bar.offsetMax = new Vector2(-SoulsBarRight, -SoulsBarTop);

            _soulsFill = (RectTransform)bar.Find("Fill");
            var fill = FillImage(_soulsFill);
            if (fill != null)
                fill.color = SoulsColor;
            Transform label = bar.Find("Label");
            _soulsText = label != null ? (TMP_Text)label.GetComponent(typeof(TMP_Text)) : null;

            // DEF moves to the right end of the same row.
            if (statsText != null)
                statsText.alignment = TextAlignmentOptions.Right;
        }

        private static Image FillImage(RectTransform fill) =>
            fill != null ? (Image)fill.GetComponent(typeof(Image)) : null;
        

        /// <summary>Swaps the placeholder color square for the Apostle's portrait.</summary>
        public void SetPortrait(Sprite portrait)
        {
            if (colorStripe == null || portrait == null)
                return;

            colorStripe.sprite = portrait;
            colorStripe.color = Color.white;
            colorStripe.preserveAspect = true;
            _hasPortrait = true;

            if (portraitText != null)
                portraitText.gameObject.SetActive(false);
        }

        /// <summary>
        /// Shows the Apostle's current numbers. Called whenever the match state changes.
        /// isPickable highlights the portrait when the player is choosing a target for an effect.
        /// </summary>
        public void Show(ApostleState apostle, bool isCurrentTurn, bool isAwakened, bool isPickable = false)
        {
            if (_hasPortrait && colorStripe != null)
                colorStripe.color = apostle.IsEliminated ? EliminatedPortrait : Color.white;

            if (background != null)
            {
                background.color = apostle.IsEliminated ? eliminatedBackground
                    : isPickable ? pickableBackground
                    : isCurrentTurn ? currentTurnBackground
                    : normalBackground;
            }

            if (statusText != null)
            {
                statusText.text = apostle.IsEliminated ? "OUT"
                    : isPickable ? "CLICK TO PICK"
                    : isAwakened ? "AWAKENED"
                    : isCurrentTurn ? "TURN"
                    : string.Empty;
            }

            SetFill(vesselFill, _vesselImage, (float)apostle.Vessel / GameConstants.MaxVessel);
            if (vesselText != null)
                vesselText.text = $"Vessel {apostle.Vessel}/{GameConstants.MaxVessel}";

            SetFill(corruptionFill, _corruptionImage, (float)apostle.Corruption / CorruptionBarMax);
            if (corruptionFillImage != null && KeepsGameColours(corruptionFillImage))
                corruptionFillImage.color = apostle.IsCorrupted ? corruptedColor : corruptionColor;
            if (corruptionText != null)
                corruptionText.text = apostle.IsCorrupted ? $"Corruption {apostle.Corruption}  CORRUPTED"
                                                          : $"Corruption {apostle.Corruption}";

            SetFill(_soulsFill, _soulsImage, (float)apostle.Souls / GameConstants.SoulsToAwaken);
            if (_soulsText != null)
            {
                _soulsText.text = apostle.IsSoullocked
                    ? $"Souls {apostle.Souls}/{GameConstants.SoulsToAwaken}  LOCKED"
                    : $"Souls {apostle.Souls}/{GameConstants.SoulsToAwaken}";
            }

            if (statsText != null)
            {
                if (_soulsFill != null)
                    statsText.text = $"DEF {apostle.Defense}";
                else
                    statsText.text = $"Souls {apostle.Souls}/{GameConstants.SoulsToAwaken}   DEF {apostle.Defense}";
            }
        }

        // A fill drawn from real art keeps its own colours unless the skin says to tint it.
        private static bool KeepsGameColours(Image fill)
        {
            UiSkin skin = UiSkin.Current;
            return fill.type != Image.Type.Filled || skin == null || skin.TintFills;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                Clicked?.Invoke(ApostleId);
        }

        // A placeholder fill stretches from the left edge to this fraction of the bar's width;
        // a skinned (Filled) one stays full width and fills by fillAmount, so its art never squashes.
        private static void SetFill(RectTransform fill, Image image, float fraction)
        {
            if (fill == null)
                return;
            fraction = Mathf.Clamp01(fraction);
            if (image != null && image.type == Image.Type.Filled)
            {
                fill.anchorMax = Vector2.one;
                image.fillAmount = fraction;
                return;
            }
            fill.anchorMax = new Vector2(fraction, 1f);
        }
    }
}