using System.Collections.Generic;
using SoulBind.Core;
using SoulBind.Managers;
using SoulBind.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        [SerializeField] private ApostleArtLibrary apostleArt;
        [SerializeField] private TMP_Text roundText;
        [SerializeField] private TMP_Text turnText;
        [SerializeField] private TMP_Text capacityText;

        [Header("Phones")]
        [Tooltip("On phones and tablets the portraits are this much bigger (1 = same as on PC).")]
        [SerializeField, Range(1f, 2f)] private float mobilePortraitScale = 1.25f;
        [Tooltip("Tick to see the phone portrait size while testing in the Editor.")]
        [SerializeField] private bool previewMobileInEditor;
        [Tooltip("On phones the opponents' portraits start this " + "far in from the left (canvas pixels), clear of " + "rounded corners. A camera notch is added on top.")]
        [SerializeField, Range(0f, 200f)]
        private float mobileEdgeInset = 50f;

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
            else if (_canvas != null)
            {
                bool phone = Application.isMobilePlatform || (Application.isEditor && previewMobileInEditor);
                if (phone)
                    EnlargePortraits(mobilePortraitScale);
                KeepClearOfScreenEdges(phone);
                // Banners and the end-of-match screen.
                MatchOverlay overlay = MatchOverlay.Create(_canvas.rootCanvas, matchController);
                // The dark gothic look, over what Build Match HUD made (nothing in the scene is rebuilt).
                GothicTheme.ApplyToHud(transform);
                GothicTheme.ApplyToOverlay(overlay.transform);
                GothicTheme.ApplyToTable(matchController.BoardView);
                // Which element combos are building up or active this turn, under the turn info.
                ComboTracker.Create((RectTransform)transform, matchController,
                    roundText != null ? roundText.transform.parent as RectTransform : null);
            }
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
                GothicTheme.SetPortraitHighlight(panel, isCurrent);
            }
        }

        // Bigger portraits for small screens. Each row grows away from its screen edge, so nothing is pushed
        // off the screen: the opponents' row from the top, your portrait from the bottom-right corner.
        private void EnlargePortraits(float scale)
        {
            if (scale <= 1.001f)
                return;
            ScaleFromCorner(opponentRow, new Vector2(0f, 1f), scale);
            ScaleFromCorner(playerSlot, new Vector2(1f, 0f), scale);
        }

        private static void ScaleFromCorner(RectTransform rect, Vector2 pivot, float scale)
        {
            if (rect == null)
                return;
            // Move the pivot without moving the rect, then scale around it.
            Vector2 size = rect.rect.size;
            Vector2 shift = pivot - rect.pivot;
            rect.pivot = pivot;
            rect.anchoredPosition += new Vector2(shift.x * size.x, shift.y * size.y);
            rect.localScale = new Vector3(scale, scale, 1f);
        }

        // The opponents' row starts at the safe area's left edge
        // (right of a camera notch), plus a margin on phones for
        // the rounded corners.
        private void KeepClearOfScreenEdges(bool phone)
        {
            if (opponentRow == null || Screen.width <= 0)
                return;
            float left = Screen.safeArea.xMin / Screen.width;
            opponentRow.anchorMin = new Vector2(left, opponentRow.anchorMin.y);
            opponentRow.anchorMax = new Vector2(left, opponentRow.anchorMax.y);
            if (phone)
                opponentRow.anchoredPosition += new Vector2(mobileEdgeInset, 0f);
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
                if (apostleArt != null)
                    panel.SetPortrait(apostleArt.GetPortrait(apostle.Id));
                GothicTheme.StylePortrait(panel); // after the portrait art, so the gold frame sits on top

                // a ring in the apostle's colour around the portrait: the same colour as their disc and their hand
                Transform portrait = panel.transform.Find("Portrait");
                if (portrait != null && portrait.GetComponent<Graphic>() != null)
                {
                    Outline ring = portrait.gameObject.AddComponent<Outline>();
                    ring.effectColor = ApostleColors.Get(apostle.Id);
                    ring.effectDistance = new Vector2(4f, -4f);
                }
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
                    turnText.text = match.WinnerId == 0 ? "Match over: no winner" : PlayerNames.Get(match.WinnerId) + " wins!";
                else if (match.IsAwaitingPhaseStart)
                    turnText.text = PlayerNames.Get(match.GetNextPhaseChooserId()) + " chooses direction";
                else 
                    turnText.text = PlayerNames.Get(match.CurrentApostle.Id) + "'s turn";
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