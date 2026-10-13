using SoulBind.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoulBind.UI
{
    /// <summary>
    /// One Apostle's portrait box in the HUD: portrait, name, status, Vessel bar, Corruption bar, Souls and DEF.
    /// Display only. Bars are fill rectangles whose width is set from the numbers, so they work
    /// with placeholder art and with real sprites later.
    /// </summary>
    public sealed class ApostlePanelView : MonoBehaviour
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

        public int ApostleId { get; private set; }

        public void Initialize(int apostleId, Color color)
        {
            ApostleId = apostleId;
            if (colorStripe != null)
                colorStripe.color = color;
            if (nameText != null)
                nameText.text = $"Apostle {apostleId}";
            if (portraitText != null)
                portraitText.text = $"A{apostleId}";
        }

        /// <summary>Shows the Apostle's current numbers. Called whenever the match state changes.</summary>
        public void Show(ApostleState apostle, bool isCurrentTurn, bool isAwakened)
        {
            if (background != null)
            {
                background.color = apostle.IsEliminated ? eliminatedBackground
                    : isCurrentTurn ? currentTurnBackground
                    : normalBackground;
            }

            if (statusText != null)
            {
                statusText.text = apostle.IsEliminated ? "OUT"
                    : isAwakened ? "AWAKENED"
                    : isCurrentTurn ? "TURN"
                    : string.Empty;
            }

            SetFill(vesselFill, (float)apostle.Vessel / GameConstants.MaxVessel);
            if (vesselText != null)
                vesselText.text = $"Vessel {apostle.Vessel}/{GameConstants.MaxVessel}";

            SetFill(corruptionFill, (float)apostle.Corruption / CorruptionBarMax);
            if (corruptionFillImage != null)
                corruptionFillImage.color = apostle.IsCorrupted ? corruptedColor : corruptionColor;
            if (corruptionText != null)
                corruptionText.text = apostle.IsCorrupted ? $"Corruption {apostle.Corruption}  CORRUPTED"
                                                          : $"Corruption {apostle.Corruption}";

            if (statsText != null)
            {
                statsText.text = apostle.IsSoullocked
                    ? $"Souls {apostle.Souls}/{GameConstants.SoulsToAwaken}   DEF {apostle.Defense}   Soullocked"
                    : $"Souls {apostle.Souls}/{GameConstants.SoulsToAwaken}   DEF {apostle.Defense}";
            }
        }

        // A bar's fill stretches from the left edge to this fraction of the bar's width.
        private static void SetFill(RectTransform fill, float fraction)
        {
            if (fill != null)
                fill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
        }
    }
}