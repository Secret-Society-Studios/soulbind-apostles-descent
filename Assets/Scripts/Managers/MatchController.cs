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
    /// The screen belongs to one Apostle (the local player): only their hand is shown face-up and only
    /// they can act on their turn. Until online play exists, bots play every other seat. With bots off,
    /// the screen follows whoever's turn it is, so one person can play every seat (hot-seat testing).
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

        [Header("Players")]
        [Tooltip("The Apostle this screen belongs to.")]
        [SerializeField, Range(1, 6)] private int localApostleId = 1;
        [Tooltip("On: bots play every other Apostle. Off: you play every Apostle and the screen follows the turn.")]
        [SerializeField] private bool botsPlayOthers = true;
        [Tooltip("Seconds between a bot's actions, so you can follow what it does.")]
        [SerializeField] private float botActionDelay = 0.6f;
        [Tooltip("Extra pause before a bot's first action of a turn.")]
        [SerializeField] private float botTurnStartDelay = 0.6f;

        [Header("Animation")]
        [Tooltip("How fast Apostle discs slide from tile to tile, in world units per second.")]
        [SerializeField] private float discMoveSpeed = 7f;
        [Tooltip("How far an attacker's disc lunges toward its target when it Strikes.")]
        [SerializeField] private float lungeDistance = 0.4f;
        [SerializeField] private float lungeTime = 0.3f;
        [Tooltip("Rising numbers above Apostles for damage, healing, Souls and Corruption.")]
        [SerializeField] private bool showStatPopups = true;
        [SerializeField] private float popupFontSize = 5f;

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

        private readonly SimpleBot _bot = new SimpleBot();
        private float _botWait;
        private int _botTurnOwner = -1;
        private int _botActionsThisTurn;
        private int _lastViewerId = 1;
        private const int MaxBotActionsPerTurn = 60;
        private string _lastBotAction = string.Empty;

        // Disc animation: each disc slides toward its tile; a Striking disc lunges at its target.
        private readonly List<Vector3> _discTargets = new List<Vector3>(GameConstants.MaxPlayers);
        private readonly List<Vector3> _discPositions = new List<Vector3>(GameConstants.MaxPlayers);
        private int _lungeIndex = -1;
        private Vector3 _lungeDirection;
        private float _lungeElapsed;
        private bool _wasEngaged;
        private int _engagedAttackerId;

        // Stat popups: the numbers as they were last shown.
        private readonly int[] _lastVessel = new int[GameConstants.MaxPlayers];
        private readonly int[] _lastSouls = new int[GameConstants.MaxPlayers];
        private readonly int[] _lastCorruption = new int[GameConstants.MaxPlayers];
        private bool _statsTaken;
        private FloatingNumbers _numbers;

        // ---- Whose screen this is ----

        /// <summary>
        /// The Apostle whose hand and portrait this screen shows. Fixed while bots play the other seats;
        /// with bots off it follows the turn (and stays on the last player between Phases).
        /// </summary>
        public int ViewerId
        {
            get
            {
                if (!botsPlayOthers && CurrentMatch != null && CurrentMatch.CurrentApostle != null)
                    _lastViewerId = CurrentMatch.CurrentApostle.Id;
                else if (botsPlayOthers)
                    _lastViewerId = localApostleId;
                return _lastViewerId;
            }
        }

        /// <summary>True while bots play every seat except the local one.</summary>
        public bool BotsPlayOthers => botsPlayOthers;

        /// <summary>True when it is this screen's player's turn, so cards, moves and buttons work.</summary>
        public bool IsLocalTurn => CanAct && (!botsPlayOthers || CurrentMatch.CurrentApostle.Id == localApostleId);

        /// <summary>True when this screen's player must choose the turn direction for the next Phase.</summary>
        public bool IsLocalPhaseChooser =>
            CurrentMatch != null && !CurrentMatch.IsOver && CurrentMatch.IsAwaitingPhaseStart
            && (!botsPlayOthers || CurrentMatch.GetNextPhaseChooserId() == localApostleId);

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

            localApostleId = Mathf.Clamp(localApostleId, 1, playerCount);
            _lastViewerId = localApostleId;
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
                Vector3 position = boardView.GetWorldPosition(apostle.Position);
                _discTargets.Add(position);
                _discPositions.Add(position);
            }

            if (showStatPopups)
                _numbers = FloatingNumbers.Create(transform, 1.3f, 0.9f, popupFontSize);

            Debug.Log($"MatchController: {playerCount} players. Apostle {CurrentMatch.CurrentApostle.Id} " +
                      $"plays first with {CurrentMatch.CurrentCapacity.Remaining} Capacity.");
            AfterStateChange();
        }

        private void Update()
        {
            AnimateDiscs();
            RunBots();
            if (!IsLocalTurn)
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
            if (!IsLocalTurn)
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
            if (!IsLocalTurn || IsChoosingComboTarget)
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

        /// <summary>
        /// A card was dropped on the discard pile (the play area). Same as clicking it, except that
        /// dropping an already selected card keeps it selected instead of cancelling it.
        /// </summary>
        public void DropCardOnPile(ArcanaId card)
        {
            if (HasSelectedCard && SelectedCard == card)
                return;
            ClickCard(card);
        }

        /// <summary>
        /// A card was dropped over the board. An ATK card dropped on a red-ringed Apostle attacks it
        /// (it becomes the primary target); while Engaged, any card dropped on a target joins the attack.
        /// Returns false if the drop means nothing, so the card goes back to the hand and nothing is spent.
        /// </summary>
        public bool TryDropCardOnBoard(ArcanaId card, Vector2 screenPoint)
        {
            if (!IsLocalTurn || IsChoosingComboTarget || GetUnplayableReason(card) != null)
                return false;
            if (!boardView.TryGetTileAtScreen(screenPoint, out HexCoord tile))
                return false;

            ApostleState apostle = CurrentMatch.Battlefield.GetApostleAt(tile);
            if (apostle == null || !IsAttackTarget(apostle))
                return false;

            if (CurrentMatch.Engagement == EngagementState.Engaged)
            {
                PlayFromHand(card, 0, 0);
                return true;
            }

            if (CardCatalog.Get(card).Attack == 0)
                return false;

            PlayFromHand(card, apostle.Id, 0);
            return true;
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
            if (!IsLocalTurn)
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
            if (!IsLocalTurn)
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
            if (!IsLocalTurn)
                return;

            int attackerId = CurrentMatch.CurrentApostle.Id;
            if (!CurrentMatch.Strike(out AttackOutcome outcome))
            {
                Debug.Log("MatchController: not engaged, so there is nothing to strike.");
                return;
            }

            foreach (StrikeHit hit in CurrentMatch.LastStrikeHits)
            {
                if (hit.BrokeDefense)
                    Debug.Log($"MatchController: Apostle {attackerId} hit Apostle {hit.TargetId} with {hit.Attack} ATK " +
                              $"for {hit.Damage} damage (stole {hit.SoulsStolen} Soul).");
                else
                    Debug.Log($"MatchController: {hit.Attack} ATK failed to break Apostle {hit.TargetId}'s DEF.");
            }
            Debug.Log($"MatchController: Apostle {attackerId} gained {outcome.CorruptionGained} Corruption from the attack.");

            LogIfOver();
            AfterStateChange();
        }

        public void EndTurn()
        {
            if (!IsLocalTurn)
                return;

            HasSelectedCard = false;
            IsChoosingComboTarget = false;
            CurrentMatch.EndTurn();
            LogIfOver();
            AfterStateChange();
        }

        public void BeginNextPhase(TurnDirection direction)
        {
            if (!IsLocalPhaseChooser)
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

        // ---- Bots ----

        // Lets the bot whose turn it is (or who chooses the next Phase's direction) take one action
        // every botActionDelay seconds. If the rules refuse an action, the bot's turn ends, so a bot
        // can never get stuck.
        private void RunBots()
        {
            if (!botsPlayOthers || CurrentMatch == null || CurrentMatch.IsOver)
                return;

            int actorId = CurrentMatch.IsAwaitingPhaseStart
                ? CurrentMatch.GetNextPhaseChooserId()
                : CurrentMatch.CurrentApostle != null ? CurrentMatch.CurrentApostle.Id : 0;
            if (actorId == 0 || actorId == localApostleId)
                return;

            // A new bot turn starts with a longer pause.
            int turnKey = CurrentMatch.IsAwaitingPhaseStart ? -actorId : actorId;
            if (turnKey != _botTurnOwner)
            {
                _botTurnOwner = turnKey;
                _botActionsThisTurn = 0;
                _lastBotAction = string.Empty;
                _botWait = botTurnStartDelay + botActionDelay;
            }

            _botWait -= Time.deltaTime;
            if (_botWait > 0f)
                return;
            _botWait = botActionDelay;

            BotAction action = ++_botActionsThisTurn > MaxBotActionsPerTurn ? BotAction.EndTurn : _bot.Decide(CurrentMatch);
            if (action.Kind == BotActionKind.None)
                return;

            if (SimpleBot.Apply(CurrentMatch, action))
            {
                _lastBotAction = Describe(action);
                Debug.Log($"MatchController: bot A{actorId}: {action}.");
            }
            else
            {
                Debug.LogWarning($"MatchController: bot A{actorId} tried '{action}' but the rules refused it. Ending its turn.");
                if (CurrentMatch.CurrentApostle != null && !CurrentMatch.IsOver)
                    CurrentMatch.EndTurn();
            }

            // Turns that end on their own (Temperance, The World) mean a new turn: reset the pause.
            if (CurrentMatch.CurrentApostle == null || CurrentMatch.CurrentApostle.Id != actorId)
                _botTurnOwner = -1;

            LogIfOver();
            AfterStateChange();
        }

#if UNITY_EDITOR
        /// <summary>DEV ONLY: switches bots on or off for the other seats.</summary>
        public void DevSetBots(bool on)
        {
            botsPlayOthers = on;
            _botTurnOwner = -1;
            CancelSelection();
        }

        /// <summary>DEV ONLY: makes this screen belong to the next Apostle (with bots on).</summary>
        public void DevCycleLocalApostle()
        {
            if (CurrentMatch == null)
                return;
            int count = CurrentMatch.Battlefield.Apostles.Count;
            localApostleId = localApostleId % count + 1;
            _botTurnOwner = -1;
            CancelSelection();
        }
#endif

        // ---- Animation ----

        // A short sentence for the hint line, so you can follow what a bot just did.
        private static string Describe(BotAction action)
        {
            switch (action.Kind)
            {
                case BotActionKind.ChooseDirection:
                    return $"chose the {action.Direction} direction";
                case BotActionKind.Move:
                    return "moved";
                case BotActionKind.PlayCard:
                    string name = CardCatalog.Get(action.Card).Name;
                    return action.TargetId != 0 ? $"played {name} on Apostle {action.TargetId}" : $"played {name}";
                case BotActionKind.Strike:
                    return "struck!";
                case BotActionKind.EndTurn:
                    return "ended the turn";
                default:
                    return string.Empty;
            }
        }

        // Slides each disc toward its tile, and plays the attacker's lunge.
        private void AnimateDiscs()
        {
            if (_views.Count == 0)
                return;

            float step = discMoveSpeed * Time.deltaTime;
            float lunge = 0f;
            if (_lungeIndex >= 0)
            {
                _lungeElapsed += Time.deltaTime;
                float t = _lungeElapsed / Mathf.Max(0.05f, lungeTime);
                if (t >= 1f)
                    _lungeIndex = -1;
                else
                    lunge = Mathf.Sin(t * Mathf.PI) * lungeDistance;
            }

            for (int i = 0; i < _views.Count; i++)
            {
                _discPositions[i] = Vector3.MoveTowards(_discPositions[i], _discTargets[i], step);
                Vector3 position = _discPositions[i];
                if (i == _lungeIndex)
                    position += _lungeDirection * lunge;
                _views[i].transform.position = position;
            }
        }

        // A Strike just happened (by a button, a bot, or automatically at End Turn): the attacker lunges.
        private void DetectStrike()
        {
            bool engaged = CurrentMatch.Engagement == EngagementState.Engaged;
            IReadOnlyList<StrikeHit> hits = CurrentMatch.LastStrikeHits;
            if (_wasEngaged && !engaged && hits.Count > 0)
            {
                int attacker = IndexOfApostle(_engagedAttackerId);
                int target = IndexOfApostle(hits[0].TargetId);
                if (attacker >= 0 && target >= 0)
                {
                    Vector3 direction = _discTargets[target] - _discTargets[attacker];
                    _lungeDirection = direction.sqrMagnitude > 0f ? direction.normalized : Vector3.zero;
                    _lungeIndex = attacker;
                    _lungeElapsed = 0f;
                }
            }

            _wasEngaged = engaged;
            if (engaged)
                _engagedAttackerId = CurrentMatch.CurrentApostle.Id;
        }

        private int IndexOfApostle(int id)
        {
            IReadOnlyList<ApostleState> apostles = CurrentMatch.Battlefield.Apostles;
            for (int i = 0; i < apostles.Count; i++)
            {
                if (apostles[i].Id == id)
                    return i;
            }
            return -1;
        }

        // Rising numbers for whatever changed since the last update: red damage, green healing,
        // gold Souls and purple Corruption.
        private void ShowStatChanges()
        {
            IReadOnlyList<ApostleState> apostles = CurrentMatch.Battlefield.Apostles;
            for (int i = 0; i < apostles.Count && i < _lastVessel.Length; i++)
            {
                ApostleState apostle = apostles[i];
                if (_statsTaken && _numbers != null)
                {
                    Vector3 above = _discTargets[i] + new Vector3(0f, 0.45f, -0.1f);
                    int line = 0;
                    int vessel = apostle.Vessel - _lastVessel[i];
                    if (vessel != 0)
                        _numbers.Show(above + Vector3.up * (0.4f * line++), vessel > 0 ? $"+{vessel}" : vessel.ToString(),
                            vessel > 0 ? new Color(0.45f, 1f, 0.5f) : new Color(1f, 0.35f, 0.3f));
                    int souls = apostle.Souls - _lastSouls[i];
                    if (souls != 0)
                        _numbers.Show(above + Vector3.up * (0.4f * line++), souls > 0 ? $"+{souls} Soul" : $"{souls} Soul",
                            new Color(1f, 0.85f, 0.3f));
                    int corruption = apostle.Corruption - _lastCorruption[i];
                    if (corruption != 0)
                        _numbers.Show(above + Vector3.up * (0.4f * line), corruption > 0 ? $"+{corruption} Corr" : $"{corruption} Corr",
                            new Color(0.8f, 0.5f, 1f));
                }
                _lastVessel[i] = apostle.Vessel;
                _lastSouls[i] = apostle.Souls;
                _lastCorruption[i] = apostle.Corruption;
            }
            _statsTaken = true;
        }

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
            DetectStrike();
            ShowStatChanges();
            Hint = BuildHint();
            StateChanged?.Invoke();
        }

        private string BuildHint()
        {
            Match match = CurrentMatch;
            if (match.IsOver)
                return string.Empty;
            if (match.IsAwaitingPhaseStart)
                return IsLocalPhaseChooser
                    ? "Phase over: you choose the turn direction."
                    : $"Phase over: Apostle {match.GetNextPhaseChooserId()} chooses the turn direction.";
            if (!IsLocalTurn)
            {
                if (match.CurrentApostle == null)
                    return string.Empty;
                return _lastBotAction.Length > 0
                    ? $"Apostle {match.CurrentApostle.Id} {_lastBotAction}"
                    : $"Apostle {match.CurrentApostle.Id} is playing...";
            }
            if (IsChoosingComboTarget)
                return "Nocturne combo: click an opponent's portrait or disc. Right-click to cancel.";
            if (HasSelectedCard)
                return "Click a red-ringed Apostle to attack (everyone adjacent is hit), or play it without attacking.";
            if (match.Engagement == EngagementState.Engaged)
                return "Engaged: add cards, or Strike. The ATK is divided between everyone adjacent.";
            if (match.Engagement == EngagementState.Disengaged)
                return "Attack done: you can still move, or end your turn.";
            return "Drag a card to the discard pile to play it, or onto a red-ringed enemy to attack.";
        }

        private void SyncViews()
        {
            AscensionTracker ascension = CurrentMatch.Ascension;
            IReadOnlyList<ApostleState> apostles = CurrentMatch.Battlefield.Apostles;
            for (int i = 0; i < apostles.Count; i++)
            {
                ApostleState apostle = apostles[i];
                ApostleView view = _views[i];

                // Discs slide to their tile (see AnimateDiscs); one coming back into view just appears there.
                bool wasShown = view.gameObject.activeSelf;
                view.gameObject.SetActive(!apostle.IsEliminated);
                _discTargets[i] = boardView.GetWorldPosition(apostle.Position);
                if (!wasShown)
                {
                    _discPositions[i] = _discTargets[i];
                    view.transform.position = _discTargets[i];
                }
                view.SetActiveTurn(apostle == CurrentMatch.CurrentApostle);
                view.SetAwakened(ascension.HasAwakened && apostle.Id == ascension.AwakenedId);
            }
        }

        // Moves and attack marks are only shown to the player whose turn it is on this screen.
        private void RefreshHighlights()
        {
            if (IsLocalTurn)
                CurrentMatch.GetLegalDestinations(_legalDestinations);
            else
                _legalDestinations.Clear();
            if (_legalDestinations.Count == 0)
                boardView.ClearHighlights();
            else
                boardView.SetHighlights(_legalDestinations);
        }

        private void RefreshAttackMarks()
        {
            if (IsLocalTurn)
                CurrentMatch.GetAttackTargets(_attackTargets);
            else
                _attackTargets.Clear();
            _attackTiles.Clear();
            for (int i = 0; i < _attackTargets.Count; i++)
                _attackTiles.Add(_attackTargets[i].Position);
            boardView.SetAttackMarks(_attackTiles);
        }
    }
}