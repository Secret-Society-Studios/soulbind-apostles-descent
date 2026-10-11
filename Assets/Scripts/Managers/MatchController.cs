using System.Collections.Generic;
using SoulBind.Core;
using SoulBind.Entities;
using SoulBind.UI;
using SoulBind.Utility;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SoulBind.Managers
{
    /// <summary>
    /// Starts a Match, shows its Apostles and passes player input to it.
    /// The Match owns every rule; this script only creates it, forwards actions and displays the result.
    /// </summary>
    public sealed class MatchController : MonoBehaviour
    {
        private static readonly Color[] PlayerColors =
        {
            new Color(0.85f, 0.25f, 0.25f),
            new Color(0.25f, 0.50f, 0.95f),
            new Color(0.30f, 0.80f, 0.40f),
            new Color(0.95f, 0.80f, 0.20f),
            new Color(0.70f, 0.40f, 0.90f),
            new Color(0.95f, 0.55f, 0.20f)
        };

        [SerializeField] private BoardView boardView;
        [SerializeField] private GameObject apostlePrefab;
        [Tooltip("Optional. Clicks on this panel are not treated as board clicks.")]
        [SerializeField] private DevTurnHud hud;
        [SerializeField, Range(2, 6)] private int playerCount = 4;
        [Tooltip("0 means a different random match every time.")]
        [SerializeField] private int seed;
        [SerializeField] private float apostleDiameter = 0.6f;

        private readonly List<ApostleView> _views = new List<ApostleView>(GameConstants.MaxPlayers);
        private readonly List<HexCoord> _legalDestinations = new List<HexCoord>(6);

        /// <summary>Raised after anything that changes what the player should see.</summary>
        public event System.Action StateChanged;

        public Match CurrentMatch { get; private set; }

        private void Start()
        {
            if (boardView == null || apostlePrefab == null)
            {
                Debug.LogError("MatchController: assign the Board View and the Apostle prefab in the Inspector.", this);
                return;
            }
            if (boardView.Board == null)
            {
                Debug.LogError("MatchController: the BoardView has not built the board.", this);
                return;
            }

            var random = seed == 0 ? new System.Random() : new System.Random(seed);
            List<HexCoord> spawns = SpawnPlanner.PickSpread(boardView.Board, playerCount);
            CurrentMatch = Match.Create(playerCount, boardView.Board, spawns, random);

            Sprite disc = DiscSpriteFactory.Create(apostleDiameter);
            IReadOnlyList<ApostleState> apostles = CurrentMatch.Battlefield.Apostles;
            for (int i = 0; i < apostles.Count; i++)
            {
                ApostleState apostle = apostles[i];

                GameObject instance = Instantiate(apostlePrefab, boardView.GetWorldPosition(apostle.Position),
                    Quaternion.identity, transform);
                instance.name = $"Apostle {apostle.Id}";

                var view = instance.GetComponent<ApostleView>();
                if (view == null)
                    view = instance.AddComponent<ApostleView>();

                view.Initialize(apostle.Id, disc, PlayerColors[(apostle.Id - 1) % PlayerColors.Length]);
                _views.Add(view);
            }

            Debug.Log($"MatchController: {playerCount} players. Apostle {CurrentMatch.CurrentApostle.Id} " +
                      $"plays first with {CurrentMatch.CurrentCapacity.Remaining} Capacity.");
            AfterStateChange();
        }

        private void Update()
        {
            if (CurrentMatch == null || CurrentMatch.IsOver || CurrentMatch.CurrentApostle == null)
                return;

            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
                return;

            Vector2 screenPoint = mouse.position.ReadValue();
            if (hud != null && hud.ContainsScreenPoint(screenPoint))
                return;

            Camera cam = Camera.main;
            if (cam == null)
                return;

            Vector3 worldPoint = cam.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, 0f));
            if (boardView.TryGetTileAt(worldPoint, out HexCoord tile))
                TryMove(tile);
        }

        public void TryMove(HexCoord destination)
        {
            if (CurrentMatch == null || CurrentMatch.IsOver || CurrentMatch.CurrentApostle == null)
                return;

            MoveResult result = CurrentMatch.Move(destination);
            if (result == MoveResult.Success)
                AfterStateChange();
            else
                Debug.Log($"MatchController: move to {destination} rejected ({result}).");
        }

        public void EndTurn()
        {
            if (CurrentMatch == null || CurrentMatch.IsOver || CurrentMatch.IsAwaitingPhaseStart)
                return;

            CurrentMatch.EndTurn();
            AfterStateChange();
        }

        public void BeginNextPhase(TurnDirection direction)
        {
            if (CurrentMatch == null || CurrentMatch.IsOver || !CurrentMatch.IsAwaitingPhaseStart)
                return;

            CurrentMatch.BeginNextPhase(direction);
            AfterStateChange();
        }

        private void AfterStateChange()
        {
            SyncViews();
            RefreshHighlights();
            StateChanged?.Invoke();
        }

        private void SyncViews()
        {
            IReadOnlyList<ApostleState> apostles = CurrentMatch.Battlefield.Apostles;
            for (int i = 0; i < apostles.Count; i++)
            {
                ApostleState apostle = apostles[i];
                ApostleView view = _views[i];

                view.gameObject.SetActive(!apostle.IsEliminated);
                view.transform.position = boardView.GetWorldPosition(apostle.Position);
                view.SetActiveTurn(apostle == CurrentMatch.CurrentApostle);
            }
        }

        private void RefreshHighlights()
        {
            if (CurrentMatch.CurrentApostle == null)
            {
                boardView.ClearHighlights();
                return;
            }

            CurrentMatch.Battlefield.GetLegalDestinations(
                CurrentMatch.CurrentApostle, CurrentMatch.CurrentCapacity, _legalDestinations);
            boardView.SetHighlights(_legalDestinations);
        }
    }
}