using System.Collections.Generic;
using SoulBind.Core;
using SoulBind.Managers;
using SoulBind.Utility;
using TMPro;
using UnityEngine;

namespace SoulBind.UI
{
    /// <summary>
    /// The match HUD, laid out like a digital card game seen from the current player's seat:
    /// opponents' portraits along the top, this screen's player's portrait in the bottom-right
    /// corner, the hand along the bottom, and turn info in the top-right. Whoever's turn it is
    /// is highlighted. With bots off (hot-seat testing), "you" follows the turn.
    /// Display only: it reads the Match after every change, and keeps the board clear of the HUD.
    /// </summary>
    public sealed class MatchHud : MonoBehaviour
    {
        [SerializeField] private MatchController matchController;
        [Tooltip("The board stays below this area (the opponents' portraits).")]
        [SerializeField] private RectTransform topReservedArea;
        [Tooltip("The board stays above this area (the hand).")]
        [SerializeField] private RectTransform bottomReservedArea;
        [Tooltip("Where the opponents' portraits go, along the top.")]
        [SerializeField] private RectTransform opponentRow;
        [Tooltip("Where the current Apostle's portrait goes, in the bottom-right corner.")]
        [SerializeField] private RectTransform playerSlot;
        [SerializeField] private ApostlePanelView panelPrefab;
        [SerializeField] private TMP_Text roundText;
        [SerializeField] private TMP_Text turnText;
        [SerializeField] private TMP_Text capacityText;

        private readonly List<ApostlePanelView> _panels = new List<ApostlePanelView>(GameConstants.MaxPlayers);
        private readonly Vector3[] _corners = new Vector3[4]; // reused every frame, so nothing is allocated
        private Canvas _canvas;
        private float _appliedTop = -1f;
        private float _appliedBottom = -1f;
        private int _lastScreenWidth;
        private int _lastScreenHeight;

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
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
                Debug.LogError("MatchHud: assign the Match Controller in the Inspector.", this);
            Refresh();
        }

        private void Update()
        {
            KeepBoardClearOfHud();
        }

        private void Refresh()
        {
            Match match = matchController != null ? matchController.CurrentMatch : null;
            if (match == null)
                return;

            if (_panels.Count == 0)
                CreatePanels(match);

            ShowTurnInfo(match);

            AscensionTracker ascension = match.Ascension;
            IReadOnlyList<ApostleState> apostles = match.Battlefield.Apostles;
            int viewerId = matchController.ViewerId;
            for (int i = 0; i < apostles.Count && i < _panels.Count; i++)
            {
                ApostleState apostle = apostles[i];
                ApostlePanelView panel = _panels[i];
                bool isCurrent = apostle == match.CurrentApostle;
                bool isViewer = apostle.Id == viewerId;

                // This screen's player sits bottom-right; everyone else along the top, in Id order.
                // Whoever's turn it is gets the TURN highlight, wherever they sit.
                Transform home = isViewer ? playerSlot : opponentRow;
                if (panel.transform.parent != home)
                    panel.transform.SetParent(home, false);
                if (!isViewer)
                    panel.transform.SetSiblingIndex(i);

                bool isPickable = matchController.IsChoosingComboTarget && !isCurrent && !apostle.IsEliminated;
                panel.Show(apostle, isCurrent, ascension.HasAwakened && ascension.AwakenedId == apostle.Id, isPickable);
            }
        }

        private void CreatePanels(Match match)
        {
            if (panelPrefab == null || opponentRow == null || playerSlot == null)
            {
                Debug.LogError("MatchHud: assign the Panel Prefab, Opponent Row and Player Slot in the Inspector.", this);
                return;
            }

            foreach (ApostleState apostle in match.Battlefield.Apostles)
            {
                ApostlePanelView panel = Instantiate(panelPrefab, opponentRow);
                panel.name = $"Apostle {apostle.Id} Portrait";
                panel.Initialize(apostle.Id, ApostleColors.Get(apostle.Id));
                panel.Clicked += OnPortraitClicked;
                _panels.Add(panel);
            }
        }

        // A portrait click picks the Nocturne combo's target while the player is choosing one.
        private void OnPortraitClicked(int apostleId)
        {
            if (matchController == null || !matchController.IsChoosingComboTarget)
                return;
            ApostleState current = matchController.CurrentMatch.CurrentApostle;
            if (current != null && current.Id != apostleId)
                matchController.ChooseComboTarget(apostleId);
        }

        private void ShowTurnInfo(Match match)
        {
            if (roundText != null)
                roundText.text = $"Round {match.Turns.RoundNumber}   Phase {match.Turns.Phase}";

            if (turnText != null)
            {
                if (match.IsOver)
                    turnText.text = match.WinnerId == 0 ? "Match over: no winner" : $"Apostle {match.WinnerId} wins!";
                else if (match.IsAwaitingPhaseStart)
                    turnText.text = $"Apostle {match.GetNextPhaseChooserId()} chooses direction";
                else
                    turnText.text = $"Apostle {match.CurrentApostle.Id}'s turn";
            }

            if (capacityText != null)
            {
                capacityText.text = match.CurrentCapacity != null
                    ? $"Capacity {match.CurrentCapacity.Remaining}/{match.CurrentCapacity.Maximum}"
                    : string.Empty;
            }
        }

        // Tells the board how much of the screen the HUD covers at the top and bottom.
        private void KeepBoardClearOfHud()
        {
            if (matchController == null || matchController.BoardView == null || _canvas == null
                || Screen.height <= 0)
                return;

            float top = topReservedArea != null ? CoveredFromTop(topReservedArea) : 0f;
            float bottom = bottomReservedArea != null ? CoveredFromBottom(bottomReservedArea) : 0f;

            bool screenChanged = Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight;
            if (!screenChanged && Mathf.Approximately(top, _appliedTop) && Mathf.Approximately(bottom, _appliedBottom))
                return;

            matchController.BoardView.SetReservedTopAndBottom(top, bottom);
            _appliedTop = top;
            _appliedBottom = bottom;
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
        }

        // How much of the screen height, from the top edge down, this area reaches.
        private float CoveredFromTop(RectTransform area)
        {
            area.GetWorldCorners(_corners); // overlay canvas: world corners are screen pixels
            return Mathf.Clamp01(1f - _corners[0].y / Screen.height);
        }

        // How much of the screen height, from the bottom edge up, this area reaches.
        private float CoveredFromBottom(RectTransform area)
        {
            area.GetWorldCorners(_corners);
            return Mathf.Clamp01(_corners[1].y / Screen.height);
        }
    }
}