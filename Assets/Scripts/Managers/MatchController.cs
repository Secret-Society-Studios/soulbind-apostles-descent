using System.Collections.Generic;
using SoulBind.Core;
using SoulBind.Entities;
using SoulBind.UI;
using SoulBind.Utility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace SoulBind.Managers
{
    /// <summary>
    /// Starts a Match, shows its Apostles and passes player input to it.
    /// The Match owns every rule; this script only creates it, forwards actions and displays the result.
    /// </summary>
    public sealed class MatchController : MonoBehaviour
    {
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
        private readonly List<ApostleState> _attackTargets = new List<ApostleState>(6);
        private readonly List<HexCoord> _attackTiles = new List<HexCoord>(6);

        /// <summary>Raised after anything that changes what the player should see.</summary>
        public event System.Action StateChanged;

        public Match CurrentMatch { get; private set; }

        public BoardView BoardView => boardView;

        // ---- Hand play state (read by the HUD) ----

        /// <summary>True while an ATK card is selected and waiting for a target click.</summary>
        public bool HasSelectedCard { get; private set; }

        /// <summary>The selected card. Only meaningful while HasSelectedCard is true.</summary>
        public ArcanaId SelectedCard { get; private set; }

        /// <summary>True while a played card waits for the player to pick the Nocturne combo's target.</summary>
        public bool IsChoosingComboTarget { get; private set; }

        /// <summary>A one-line instruction for the player (empty when there is nothing special to do).</summary>
        public string Hint { get; private set; } = string.Empty;

        private ArcanaId _comboCard;

        // True while someone's turn is active and the match is still running.
        private bool CanAct => CurrentMatch != null && !CurrentMatch.IsOver && CurrentMatch.CurrentApostle != null;

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

                view.Initialize(apostle.Id, disc, ApostleColors.Get(apostle.Id));
                _views.Add(view);
            }

            Debug.Log($"MatchController: {playerCount} players. Apostle {CurrentMatch.CurrentApostle.Id} " +
                      $"plays first with {CurrentMatch.CurrentCapacity.Remaining} Capacity.");
            AfterStateChange();
        }

        private void Update()
        {
            if (!CanAct)
                return;

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            // Esc or right-click cancels a selected card or a pending combo target.
            if ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                || (mouse != null && mouse.rightButton.wasPressedThisFrame))
            {
                if (HasSelectedCard || IsChoosingComboTarget)
                    CancelSelection();
            }

            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
                return;

            Vector2 screenPoint = mouse.position.ReadValue();
            if (hud != null && hud.ContainsScreenPoint(screenPoint))
                return;

            // Clicks on UI that can be clicked (cards, buttons, portraits) are not board clicks.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            if (!boardView.TryGetTileAtScreen(screenPoint, out HexCoord tile))
                return;

            ApostleState clicked = CurrentMatch.Battlefield.GetApostleAt(tile);

            // A selected ATK card: clicking a red-ringed Apostle attacks it. Other clicks do nothing.
            if (HasSelectedCard)
            {
                if (clicked != null && IsAttackTarget(clicked))
                    PlayFromHand(SelectedCard, clicked.Id, 0);
                return;
            }

            // Picking the Nocturne combo's target: clicking an opponent's disc works too.
            if (IsChoosingComboTarget)
            {
                if (clicked != null && clicked != CurrentMatch.CurrentApostle)
                    ChooseComboTarget(clicked.Id);
                return;
            }

            TryMove(tile);
        }

        // ---- Playing cards from the hand (the real UI) ----

        /// <summary>
        /// Why this card cannot be played right now, or null if it can. Shown when hovering a dimmed card.
        /// </summary>
        public string GetUnplayableReason(ArcanaId card)
        {
            if (!CanAct)
                return "Not your turn";
            if (CurrentMatch.Engagement == EngagementState.Disengaged)
                return "No more cards after Strike";
            int cap = CardCatalog.Get(card).Cap;
            if (cap > CurrentMatch.CurrentCapacity.Remaining)
                return $"Needs {cap} Capacity";
            return null;
        }

        /// <summary>
        /// A card in the hand was clicked. ATK cards with enemies in reach are selected first, so the
        /// player can click a target; everything else is played straight away.
        /// </summary>
        public void ClickCard(ArcanaId card)
        {
            if (!CanAct || IsChoosingComboTarget)
                return;

            string reason = GetUnplayableReason(card);
            if (reason != null)
            {
                Debug.Log($"MatchController: {CardCatalog.Get(card).Name} can't be played: {reason}.");
                return;
            }

            // Clicking the selected card again deselects it.
            if (HasSelectedCard && SelectedCard == card)
            {
                CancelSelection();
                return;
            }

            CardDefinition definition = CardCatalog.Get(card);
            if (CurrentMatch.Engagement == EngagementState.Engaged || definition.Attack == 0)
            {
                PlayFromHand(card, 0, 0);
                return;
            }

            CurrentMatch.GetAttackTargets(_attackTargets);
            if (_attackTargets.Count == 0)
            {
                PlayFromHand(card, 0, 0);
                return;
            }

            HasSelectedCard = true;
            SelectedCard = card;
            AfterStateChange();
        }

        /// <summary>Plays the selected ATK card for its DEF only (its ATK is lost).</summary>
        public void PlaySelectedWithoutAttacking()
        {
            if (HasSelectedCard)
                PlayFromHand(SelectedCard, 0, 0);
        }

        /// <summary>Cancels a selected card or a pending combo target. Nothing is spent.</summary>
        public void CancelSelection()
        {
            HasSelectedCard = false;
            IsChoosingComboTarget = false;
            if (CurrentMatch != null)
                AfterStateChange();
        }

        /// <summary>Finishes playing the card that was waiting for its Nocturne combo target.</summary>
        public void ChooseComboTarget(int apostleId)
        {
            if (IsChoosingComboTarget)
                PlayFromHand(_comboCard, 0, apostleId);
        }

        private bool IsAttackTarget(ApostleState apostle)
        {
            CurrentMatch.GetAttackTargets(_attackTargets);
            return _attackTargets.Contains(apostle);
        }

        private void PlayFromHand(ArcanaId card, int targetId, int comboTargetId)
        {
            int playerId = CurrentMatch.CurrentApostle.Id;
            CardPlayResult result = CurrentMatch.PlayCard(card, targetId, comboTargetId);

            if (result == CardPlayResult.ComboTargetRequired)
            {
                // Ask the player who the Nocturne combo should hit, then play the card.
                HasSelectedCard = false;
                IsChoosingComboTarget = true;
                _comboCard = card;
                AfterStateChange();
                return;
            }

            HasSelectedCard = false;
            IsChoosingComboTarget = false;

            if (result != CardPlayResult.Success)
            {
                Debug.Log($"MatchController: {CardCatalog.Get(card).Name} rejected ({result}).");
                AfterStateChange();
                return;
            }

            Debug.Log($"MatchController: Apostle {playerId} played {CardCatalog.Get(card).Name}.");
            LogIfOver();
            AfterStateChange();
        }

        public void TryMove(HexCoord destination)
        {
            if (!CanAct)
                return;

            MoveResult result = CurrentMatch.Move(destination);
            if (result != MoveResult.Success)
            {
                Debug.Log($"MatchController: move to {destination} rejected ({result}).");
                return;
            }

            LogIfOver();
            AfterStateChange();
        }

        /// <summary>
        /// Plays a card from the current Apostle's hand. If attack is true and the card has ATK,
        /// it attacks: the engaged target while Engaged, otherwise the adjacent Apostle with the
        /// lowest Id. comboTargetId is who the Nocturne combo hits if this card triggers it (0 = no pick).
        /// Temporary targeting until the real UI lets the player pick targets.
        /// </summary>
        public void PlayCard(ArcanaId card, bool attack, int comboTargetId)
        {
            if (!CanAct)
                return;

            int targetId = 0;
            if (attack && CardCatalog.Get(card).Attack > 0)
            {
                CurrentMatch.GetAttackTargets(_attackTargets);
                if (_attackTargets.Count > 0)
                    targetId = _attackTargets[0].Id;
            }

            // Read before playing: some cards (Temperance) end the turn, so CurrentApostle changes.
            int playerId = CurrentMatch.CurrentApostle.Id;
            CardPlayResult result = CurrentMatch.PlayCard(card, targetId, comboTargetId);
            if (result != CardPlayResult.Success)
            {
                Debug.Log($"MatchController: {CardCatalog.Get(card).Name} rejected ({result}).");
                return;
            }

            Debug.Log($"MatchController: Apostle {playerId} played {CardCatalog.Get(card).Name}. " +
                      $"State {CurrentMatch.Engagement}, pending ATK {CurrentMatch.PendingAttack}.");
            LogIfOver();
            AfterStateChange();
        }

        /// <summary>Resolves the built-up attack and disengages.</summary>
        public void Strike()
        {
            if (!CanAct)
                return;

            int attackerId = CurrentMatch.CurrentApostle.Id;
            int targetId = CurrentMatch.EngagedTargetId;
            if (!CurrentMatch.Strike(out AttackOutcome outcome))
            {
                Debug.Log("MatchController: not engaged, so there is nothing to strike.");
                return;
            }

            if (outcome.BrokeDefense)
                Debug.Log($"MatchController: Apostle {attackerId} hit Apostle {targetId} for {outcome.Damage} " +
                          $"(stole {outcome.SoulsStolen} Soul, +{outcome.CorruptionGained} Corruption).");
            else
                Debug.Log($"MatchController: Apostle {attackerId} failed to break Apostle {targetId}'s DEF.");

            LogIfOver();
            AfterStateChange();
        }

        public void EndTurn()
        {
            if (CurrentMatch == null || CurrentMatch.IsOver || CurrentMatch.IsAwaitingPhaseStart)
                return;

            HasSelectedCard = false;
            IsChoosingComboTarget = false;
            CurrentMatch.EndTurn();
            LogIfOver();
            AfterStateChange();
        }

        public void BeginNextPhase(TurnDirection direction)
        {
            if (CurrentMatch == null || CurrentMatch.IsOver || !CurrentMatch.IsAwaitingPhaseStart)
                return;

            HasSelectedCard = false;
            IsChoosingComboTarget = false;
            CurrentMatch.BeginNextPhase(direction);
            AfterStateChange();
        }

#if UNITY_EDITOR
        // DEV ONLY. These bypass the rule that the UI never changes Apostle numbers, so they only
        // exist in the Editor, and they will be removed together with DevTurnHud.

        /// <summary>DEV ONLY: changes the current Apostle's Souls to test Awakening.</summary>
        public void DevChangeCurrentSouls(int delta)
        {
            if (!CanAct)
                return;

            ApostleState apostle = CurrentMatch.CurrentApostle;
            if (delta > 0)
                apostle.AddSouls(delta);
            else if (delta < 0)
                apostle.LoseSouls(-delta);

            CurrentMatch.RefreshGameState();
            AfterStateChange();
        }

        /// <summary>DEV ONLY: gives the current Apostle extra shield.</summary>
        public void DevAddShieldToCurrent(int amount)
        {
            if (!CanAct)
                return;

            CurrentMatch.CurrentApostle.AddShield(amount);
            AfterStateChange();
        }
#endif

        private void LogIfOver()
        {
            if (CurrentMatch.IsOver)
                Debug.Log($"MatchController: match over. Winner {CurrentMatch.WinnerId} ({CurrentMatch.EndReason}).");
        }

        // The order matters: clear the attack marks first and set them last,
        // so no board layer paints over another.
        private void AfterStateChange()
        {
            boardView.ClearAttackMarks();
            SyncViews();
            boardView.ShowSpecialTiles(CurrentMatch.Battlefield.Specials.Active);
            boardView.ShowAscensionTile(CurrentMatch.Ascension.HasAwakened, CurrentMatch.Ascension.Tile);
            RefreshHighlights();
            RefreshAttackMarks();
            Hint = BuildHint();
            StateChanged?.Invoke();
        }

        private string BuildHint()
        {
            Match match = CurrentMatch;
            if (match.IsOver)
                return string.Empty;
            if (match.IsAwaitingPhaseStart)
                return $"Apostle {match.GetNextPhaseChooserId()}: choose the turn direction.";
            if (IsChoosingComboTarget)
                return "Nocturne combo: click an opponent's portrait or disc. Right-click to cancel.";
            if (HasSelectedCard)
                return "Click a red-ringed Apostle to attack, or play it without attacking.";
            if (match.Engagement == EngagementState.Engaged)
                return $"Engaged with Apostle {match.EngagedTargetId}: add cards, or Strike.";
            if (match.Engagement == EngagementState.Disengaged)
                return "Attack done: you can still move, or end your turn.";
            return string.Empty;
        }

        private void SyncViews()
        {
            AscensionTracker ascension = CurrentMatch.Ascension;
            IReadOnlyList<ApostleState> apostles = CurrentMatch.Battlefield.Apostles;
            for (int i = 0; i < apostles.Count; i++)
            {
                ApostleState apostle = apostles[i];
                ApostleView view = _views[i];

                view.gameObject.SetActive(!apostle.IsEliminated);
                view.transform.position = boardView.GetWorldPosition(apostle.Position);
                view.SetActiveTurn(apostle == CurrentMatch.CurrentApostle);
                view.SetAwakened(ascension.HasAwakened && apostle.Id == ascension.AwakenedId);
            }
        }

        private void RefreshHighlights()
        {
            CurrentMatch.GetLegalDestinations(_legalDestinations);
            if (_legalDestinations.Count == 0)
                boardView.ClearHighlights();
            else
                boardView.SetHighlights(_legalDestinations);
        }

        private void RefreshAttackMarks()
        {
            CurrentMatch.GetAttackTargets(_attackTargets);
            _attackTiles.Clear();
            for (int i = 0; i < _attackTargets.Count; i++)
                _attackTiles.Add(_attackTargets[i].Position);
            boardView.SetAttackMarks(_attackTiles);
        }
    }
}