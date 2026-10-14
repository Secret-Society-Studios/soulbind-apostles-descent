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
            bool canAct = running && match.CurrentApostle != null;
            bool engaged = canAct && match.Engagement == EngagementState.Engaged;
            bool choosingDirection = running && match.IsAwaitingPhaseStart;

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