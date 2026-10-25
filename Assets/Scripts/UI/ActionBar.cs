using SoulBind.Core;
using SoulBind.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoulBind.UI
{
    /// <summary>
    /// The buttons above the player's portrait: Strike, End Turn, Play without attacking, Cancel,
    /// and the turn-direction choice after a Phase. Only the buttons that make sense right now are shown.
    /// Display and input only: every button just calls MatchController.
    /// </summary>
    public sealed class ActionBar : MonoBehaviour
    {
        [SerializeField] private MatchController matchController;
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private Button strikeButton;
        [SerializeField] private TMP_Text strikeLabel;
        [SerializeField] private Button endTurnButton;
        [SerializeField] private Button noAttackButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button forwardButton;
        [SerializeField] private Button backwardButton;
        [Header("End Turn button")]
        [Tooltip("Height of End Turn, in canvas pixels. The other buttons are 44.")]
        [SerializeField, Range(44f, 160f)] private float endTurnHeight = 72f;
        [SerializeField, Range(16f, 60f)] private float endTurnFontSize = 30f;
        [Tooltip("On phones and tablets End Turn is this much bigger again (1 = same as on PC).")]
        [SerializeField, Range(1f, 2f)] private float mobileEndTurnScale = 1.3f;
        [Tooltip("Tick to see the phone size while testing in the Editor.")]
        [SerializeField] private bool previewMobileInEditor;

        private void Awake()
        {
            if (matchController == null)
                return;

            if (strikeButton != null)
                strikeButton.onClick.AddListener(matchController.Strike);
            if (endTurnButton != null)
                endTurnButton.onClick.AddListener(matchController.EndTurn);
            if (noAttackButton != null)
                noAttackButton.onClick.AddListener(matchController.PlaySelectedWithoutAttacking);
            if (cancelButton != null)
                cancelButton.onClick.AddListener(matchController.CancelSelection);
            if (forwardButton != null)
                forwardButton.onClick.AddListener(() => matchController.BeginNextPhase(TurnDirection.Forward));
            if (backwardButton != null)
                backwardButton.onClick.AddListener(() => matchController.BeginNextPhase(TurnDirection.Backward));

            EnlargeEndTurn();
        }

        // End Turn is the button pressed every turn, so it is taller with bigger text, more so on phones.
        // The column's layout reads the height from its LayoutElement.
        private void EnlargeEndTurn()
        {
            if (endTurnButton == null)
                return;
            bool phone = Application.isMobilePlatform || (Application.isEditor && previewMobileInEditor);
            float scale = phone ? mobileEndTurnScale : 1f;

            LayoutElement layout = endTurnButton.GetComponent<LayoutElement>();
            if (layout == null)
                layout = endTurnButton.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = endTurnHeight * scale;
            layout.minHeight = endTurnHeight * scale;

            TMP_Text label = endTurnButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.enableAutoSizing = false;
                label.fontSize = endTurnFontSize * scale;
            }
        }

        private void OnEnable()
        {
            if (matchController != null)
                matchController.StateChanged += Refresh;
        }

        private void OnDisable()
        {
            if (matchController != null)
                matchController.StateChanged -= Refresh;
        }

        private void Start()
        {
            if (matchController == null)
                Debug.LogError("ActionBar: assign the Match Controller in the Inspector.", this);
            Refresh();
        }

        private void Refresh()
        {
            Match match = matchController != null ? matchController.CurrentMatch : null;
            bool running = match != null && !match.IsOver;
            // Buttons only work for this screen's player, on their own turn.
            bool canAct = running && matchController.IsLocalTurn;
            bool engaged = canAct && match.Engagement == EngagementState.Engaged;
            bool choosingDirection = running && matchController.IsLocalPhaseChooser;

            SetVisible(strikeButton, engaged);
            if (engaged && strikeLabel != null)
                strikeLabel.text = $"Strike  (ATK {match.PendingAttack})";

            SetVisible(endTurnButton, canAct);
            SetVisible(noAttackButton, canAct && matchController.HasSelectedCard);
            SetVisible(cancelButton, canAct && (matchController.HasSelectedCard || matchController.IsChoosingComboTarget));
            SetVisible(forwardButton, choosingDirection);
            SetVisible(backwardButton, choosingDirection);

            if (hintText != null)
                hintText.text = matchController != null ? matchController.Hint : string.Empty;
        }

        private static void SetVisible(Button button, bool visible)
        {
            if (button != null && button.gameObject.activeSelf != visible)
                button.gameObject.SetActive(visible);
        }
    }
}