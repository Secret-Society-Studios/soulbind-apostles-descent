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
    ///
    /// Online (started from the menu's Host / Join): the host runs the real Match, plays the bots and
    /// applies the commands other players send; after every change NetworkMatch sends each player a
    /// snapshot of what they may see. A player who joined keeps a copy built from those snapshots and
    /// sends every action to the host as a MatchCommand instead of applying it. All local actions go
    /// through Submit, so the same buttons work in every mode.
    /// </summary>
    public sealed class MatchController : MonoBehaviour
    {
        [SerializeField] private BoardView boardView;
        [SerializeField] private GameObject apostlePrefab;
        [Tooltip("Optional. Clicks on this panel are not treated as board clicks.")]
        [SerializeField] private DevTurnHud hud;
        [SerializeField, Range(2, 6)] private int playerCount = 4;
        [Tooltip("0: a different match every time (the seed used is printed in the Console). Any other number: the same match every time, for repeating a bug.")]
        [SerializeField] private int seed;
        [SerializeField] private float apostleDiameter = 0.6f;

        [Tooltip("On: before the first turn, each Apostle picks a starting edge tile (bots pick at random). Off: tiles are spread out automatically.")]
        [SerializeField] private bool chooseSpawns = true;

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
        [Tooltip("Speed of every animation: 1 = normal, 2 = twice as fast, 0.5 = half speed.")]
        [SerializeField, Range(0.5f, 2f)] private float animationSpeed = 1f;
        [Tooltip("A short, subtle camera shake when a Strike lands.")]
        [SerializeField] private bool screenShake = true;
        [Tooltip("How fast pieces glide from tile to tile, in world units per second (before easing in and out).")]
        [SerializeField] private float discMoveSpeed = 7f;
        [Tooltip("How far an attacker's piece lunges toward its target when it Strikes.")]
        [SerializeField] private float lungeDistance = 0.4f;
        [Tooltip("Length of the Strike's quickest part. The whole Strike (wind-up, strike, recovery) takes about three times this.")]
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

        /// <summary>Offline and on the host: raised after the match itself changed (NetworkMatch sends snapshots then).</summary>
        public event System.Action MatchChanged;

        /// <summary>On a player who joined online: an action to send to the host.</summary>
        public event System.Action<MatchCommand> CommandSubmitted;

        private enum OnlineRole { Offline, Waiting, Host, Client }
        private OnlineRole _online = OnlineRole.Offline;
        private readonly bool[] _remoteSeat = new bool[GameConstants.MaxPlayers + 1];
        private int _lastSequence = -1;

        // Messages from the host that arrived before Start ran (kept until then).
        private int _pendingSeat;
        private int _pendingSeats;
        private byte[] _pendingSnapshot;

        /// <summary>True in an online match (as host or as a player who joined).</summary>
        public bool IsOnline => _online != OnlineRole.Offline;

        /// <summary>True on a player who joined online: their match is a copy and the host decides everything.</summary>
        public bool IsOnlineClient => _online == OnlineRole.Client;

        /// <summary>True on the host until NetworkMatch has seated everyone and started the match.</summary>
        public bool IsWaitingForOnlineStart => _online == OnlineRole.Waiting;

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

        // Picking starting tiles, before the match exists.
        private System.Random _random;
        private System.Random _botRandom;
        private SpawnDraft _draft;
        private int[] _seatOrder;
        private Sprite _disc;

        /// <summary>True while the Apostles are picking their starting tiles (before the first turn).</summary>
        public bool IsChoosingSpawns => _draft != null;

        /// <summary>Who picks a starting tile now (0 when nobody is picking).</summary>
        public int SpawnPickerId => _draft != null ? _draft.CurrentPickerId : 0;

        /// <summary>True when the Apostle picking a starting tile now is played on this screen.</summary>
        public bool IsLocalSpawnPick => _draft != null && _draft.CurrentPickerId != 0
                                        && (!botsPlayOthers || _draft.CurrentPickerId == localApostleId);

        // Disc animation: each disc slides toward its tile; a Striking disc lunges at its target.
        private readonly List<Vector3> _discTargets = new List<Vector3>(GameConstants.MaxPlayers);
        private readonly List<Vector3> _discPositions = new List<Vector3>(GameConstants.MaxPlayers);
        private int _lungeIndex = -1;
        private Vector3 _lungeDirection;
        private float _lungeElapsed;
        private float _lungeDuration;
        private bool _impactDone;
        private bool _strikeStartedNow;
        private readonly List<Vector3> _impactTargets = new List<Vector3>(GameConstants.MaxPlayers);

        // Gliding pieces: each move eases in and out, leaving a little smoke behind.
        private readonly Vector3[] _moveFrom = new Vector3[GameConstants.MaxPlayers];
        private readonly Vector3[] _moveTo = new Vector3[GameConstants.MaxPlayers];
        private readonly float[] _moveElapsed = new float[GameConstants.MaxPlayers];
        private readonly float[] _moveDuration = new float[GameConstants.MaxPlayers];
        private readonly float[] _smokeTimer = new float[GameConstants.MaxPlayers];
        private TableEffects _effects;
        private int _auraIndex = -1;

        // A Strike, as shares of its time: wind-up (pull back), strike (impact at the end), recovery.
        private const float WindUpEnd = 0.4f;
        private const float ImpactAt = 0.55f;
        private float StrikeDesignedSeconds => Mathf.Max(0.15f, lungeTime) * 3.2f;
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

            // Started from the main menu: its choices replace the Inspector values.
            if (MatchSettings.IsSet)
            {
                playerCount = MatchSettings.PlayerCount;
                botsPlayOthers = MatchSettings.BotsPlayOthers;
                localApostleId = MatchSettings.LocalApostleId;
                chooseSpawns = MatchSettings.ChooseSpawns;
            }
            bool online = MatchSettings.IsSet && MatchSettings.IsOnline;

            localApostleId = Mathf.Clamp(localApostleId, 1, playerCount);
            _lastViewerId = localApostleId;
            // Seed 0 means a new match every time. The seed actually used is logged, so an interesting match
            // can be played again by typing that number into Seed.
            int usedSeed = seed != 0 ? seed : (System.Environment.TickCount & int.MaxValue) | 1;
            _random = new System.Random(usedSeed);
            _botRandom = new System.Random(usedSeed + 1);
            Debug.Log(seed != 0
                ? $"MatchController: fixed Seed {usedSeed}, so this match is the same every time. Set Seed to 0 for a new match each time."
                : $"MatchController: seed {usedSeed}. To replay this exact match, type it into Seed on MatchController.");
            _disc = DiscSpriteFactory.Create(apostleDiameter);
            MotionStyle.Speed = animationSpeed;
            MotionStyle.ScreenShake = screenShake;
            _effects = TableEffects.Create(transform);
            if (showStatPopups)
                _numbers = FloatingNumbers.Create(transform, 1.3f, 0.9f, popupFontSize);

            if (online)
            {
                // NetworkMatch seats everyone, then calls StartOnlineHost or StartOnlineClient.
                _online = OnlineRole.Waiting;
                ShowOnlineMessage("Waiting for every player to arrive...");
                if (_pendingSeat > 0)
                    StartOnlineClient(_pendingSeat, _pendingSeats);
                if (_pendingSnapshot != null)
                    ReceiveSnapshot(_pendingSnapshot);
                _pendingSnapshot = null;
                return;
            }

            CreateViews();
            if (chooseSpawns)
            {
                // The turn order is drawn first (as before), and the Apostles pick their tiles in that order.
                _seatOrder = TurnTracker.CreateRandomSeatOrder(playerCount, _random);
                _draft = new SpawnDraft(boardView.Board, _seatOrder);
                _botWait = botTurnStartDelay + botActionDelay;
                AfterDraftChange();
            }
            else
            {
                StartMatch(SpawnPlanner.PickSpread(boardView.Board, playerCount), null);
            }
        }

        // One disc per Apostle, hidden until its starting tile is known.
        private void CreateViews()
        {
            Vector3 center = boardView.transform.position;
            for (int id = 1; id <= playerCount; id++)
            {
                GameObject instance = Instantiate(apostlePrefab, center, Quaternion.identity, transform);
                instance.name = $"Apostle {id}";
                var view = instance.GetComponent<ApostleView>();
                if (view == null)
                    view = instance.AddComponent<ApostleView>();
                view.Initialize(id, _disc, ApostleColors.Get(id));
                instance.SetActive(false);
                _views.Add(view);
                _discTargets.Add(center);
                _discPositions.Add(center);
                _moveTo[id - 1] = center;
            }
        }

        private void StartMatch(List<HexCoord> spawns, int[] seatOrder)
        {
            _draft = null;
            CurrentMatch = seatOrder == null
                ? Match.Create(playerCount, boardView.Board, spawns, _random)
                : Match.Create(playerCount, boardView.Board, spawns, seatOrder, _random);

            Debug.Log($"MatchController: {playerCount} players. Apostle {CurrentMatch.CurrentApostle.Id} " +
                      $"plays first with {CurrentMatch.CurrentCapacity.Remaining} Capacity.");
            AfterMatchChange();
        }

        // ---- Online (called by NetworkMatch) ----

        /// <summary>
        /// Host: starts the match. This screen is Apostle 1; remoteSeats are the players who joined;
        /// bots play every other seat.
        /// </summary>
        public void StartOnlineHost(int seats, IReadOnlyList<int> remoteSeats)
        {
            if (_online != OnlineRole.Waiting)
                return;

            _online = OnlineRole.Host;
            playerCount = Mathf.Clamp(seats, 2, GameConstants.MaxPlayers);
            localApostleId = 1;
            _lastViewerId = 1;
            botsPlayOthers = true;
            System.Array.Clear(_remoteSeat, 0, _remoteSeat.Length);
            for (int i = 0; i < remoteSeats.Count; i++)
                _remoteSeat[remoteSeats[i]] = true;

            Debug.Log($"MatchController: hosting online. {remoteSeats.Count + 1} players and " +
                      $"{playerCount - remoteSeats.Count - 1} bots.");
            CreateViews();
            StartMatch(SpawnPlanner.PickSpread(boardView.Board, playerCount), null);
        }

        /// <summary>Player who joined: this screen is Apostle seat. The match arrives with the host's first snapshot.</summary>
        public void StartOnlineClient(int seat, int seats)
        {
            if (_online == OnlineRole.Offline)
            {
                _pendingSeat = seat; // Start hasn't run yet
                _pendingSeats = seats;
                return;
            }
            if (_online != OnlineRole.Waiting)
                return;

            _online = OnlineRole.Client;
            playerCount = Mathf.Clamp(seats, 2, GameConstants.MaxPlayers);
            localApostleId = Mathf.Clamp(seat, 1, playerCount);
            _lastViewerId = localApostleId;
            botsPlayOthers = true;
            Debug.Log($"MatchController: joined online as Apostle {localApostleId} of {playerCount}.");
            CreateViews();
            ShowOnlineMessage("Waiting for the host...");
        }

        /// <summary>Player who joined: loads the host's latest snapshot. Older or damaged ones are ignored.</summary>
        public void ReceiveSnapshot(byte[] bytes)
        {
            if (_online != OnlineRole.Client)
            {
                if (_online == OnlineRole.Offline)
                    _pendingSnapshot = bytes; // Start hasn't run yet
                return;
            }

            MatchSnapshot snapshot = MatchSnapshot.FromBytes(bytes);
            if (snapshot == null)
            {
                Debug.LogWarning("MatchController: a snapshot from the host could not be read.");
                return;
            }
            if (snapshot.Sequence <= _lastSequence)
                return;
            _lastSequence = snapshot.Sequence;

            if (CurrentMatch == null)
                CurrentMatch = Match.FromSnapshot(boardView.Board, snapshot);
            else
                CurrentMatch.ApplySnapshot(snapshot);

            // A choice in progress ends when the turn moved on or the card left the hand.
            if (!IsLocalTurn || (HasSelectedCard && !HandContains(SelectedCard))
                || (IsChoosingComboTarget && !HandContains(_comboCard)))
            {
                HasSelectedCard = false;
                IsChoosingComboTarget = false;
            }
            AfterStateChange();
        }

        /// <summary>Host: applies an action sent by the player in this seat (the seat comes from the connection, never from the message).</summary>
        public CommandResult ApplyRemoteCommand(int seat, byte[] bytes)
        {
            if (_online != OnlineRole.Host || CurrentMatch == null)
                return CommandResult.Refused;
            if (!MatchCommand.TryDecode(bytes, 0, out MatchCommand command))
                return CommandResult.Malformed;

            CommandResult result = MatchCommands.Apply(CurrentMatch, seat, command);
            if (result == CommandResult.Applied)
            {
                Debug.Log($"MatchController: Apostle {seat} (online): {command}.");
                LogIfOver();
                AfterMatchChange();
            }
            return result;
        }

        /// <summary>Host: the player in this seat left, so a bot plays it from now on.</summary>
        public void ReleaseSeat(int seat)
        {
            if (seat <= 0 || seat >= _remoteSeat.Length || !_remoteSeat[seat])
                return;
            _remoteSeat[seat] = false;
            _botTurnOwner = -1;
            Debug.Log($"MatchController: Apostle {seat}'s player left. A bot plays that seat now.");
        }

        /// <summary>Shows a line in the hint area while there is no match to describe.</summary>
        public void ShowOnlineMessage(string message)
        {
            Hint = message;
            StateChanged?.Invoke();
        }

        private bool IsRemoteSeat(int apostleId) =>
            _online == OnlineRole.Host && apostleId > 0 && apostleId < _remoteSeat.Length && _remoteSeat[apostleId];

        private bool HandContains(ArcanaId card)
        {
            IReadOnlyList<ArcanaId> hand = CurrentMatch.GetHand(localApostleId);
            for (int i = 0; i < hand.Count; i++)
            {
                if (hand[i] == card)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Every action of this screen's player goes through here. Offline and on the host it is applied
        /// at once; on a player who joined it is sent to the host, and the next snapshot shows the result.
        /// </summary>
        private CommandResult Submit(MatchCommand command)
        {
            if (_online == OnlineRole.Client)
            {
                CommandSubmitted?.Invoke(command);
                return CommandResult.Applied;
            }

            CommandResult result = MatchCommands.Apply(CurrentMatch, MatchCommands.GetActorId(CurrentMatch), command);
            if (result == CommandResult.Applied)
            {
                LogIfOver();
                AfterMatchChange();
            }
            return result;
        }

        // Changing the speed or shake in the Inspector during Play takes effect straight away.
        private void OnValidate()
        {
            MotionStyle.Speed = animationSpeed;
            MotionStyle.ScreenShake = screenShake;
        }

        private void Update()
        {
            AnimateDiscs();
            if (_draft != null)
            {
                UpdateSpawnDraft();
                return;
            }
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

            if (comboTargetId == 0 && CurrentMatch.NeedsComboTarget(card, targetId))
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

            CommandResult result = Submit(MatchCommand.PlayCard(card, targetId, comboTargetId));
            if (result != CommandResult.Applied)
            {
                Debug.Log($"MatchController: {CardCatalog.Get(card).Name} rejected ({result}).");
                AfterStateChange();
                return;
            }

            if (IsOnlineClient)
                AfterStateChange(); // the selection marks go now; the card moves when the host answers
            else
                Debug.Log($"MatchController: Apostle {playerId} played {CardCatalog.Get(card).Name}.");
        }

        public void TryMove(HexCoord destination)
        {
            if (!IsLocalTurn)
                return;

            // A copy checks the highlighted tiles first, so a click elsewhere sends nothing.
            if (IsOnlineClient && !_legalDestinations.Contains(destination))
                return;

            CommandResult result = Submit(MatchCommand.Move(destination));
            if (result != CommandResult.Applied)
                Debug.Log($"MatchController: move to {destination} rejected ({result}).");
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
            CommandResult result = Submit(MatchCommand.PlayCard(card, targetId, comboTargetId));
            if (result != CommandResult.Applied)
            {
                Debug.Log($"MatchController: {CardCatalog.Get(card).Name} rejected ({result}).");
                return;
            }

            if (!IsOnlineClient)
                Debug.Log($"MatchController: Apostle {playerId} played {CardCatalog.Get(card).Name}. " +
                          $"State {CurrentMatch.Engagement}, pending ATK {CurrentMatch.PendingAttack}.");
        }

        /// <summary>Resolves the built-up attack and disengages.</summary>
        public void Strike()
        {
            if (!IsLocalTurn)
                return;
            if (CurrentMatch.Engagement != EngagementState.Engaged)
            {
                Debug.Log("MatchController: not engaged, so there is nothing to strike.");
                return;
            }

            ApostleState attacker = CurrentMatch.CurrentApostle;
            int corruptionBefore = attacker.Corruption;
            if (Submit(MatchCommand.Strike()) != CommandResult.Applied || IsOnlineClient)
                return;

            foreach (StrikeHit hit in CurrentMatch.LastStrikeHits)
            {
                if (hit.BrokeDefense)
                    Debug.Log($"MatchController: Apostle {attacker.Id} hit Apostle {hit.TargetId} with {hit.Attack} ATK " +
                              $"for {hit.Damage} damage (stole {hit.SoulsStolen} Soul).");
                else
                    Debug.Log($"MatchController: {hit.Attack} ATK failed to break Apostle {hit.TargetId}'s DEF.");
            }
            Debug.Log($"MatchController: Apostle {attacker.Id} gained {attacker.Corruption - corruptionBefore} Corruption from the attack.");
        }

        public void EndTurn()
        {
            if (!IsLocalTurn)
                return;

            HasSelectedCard = false;
            IsChoosingComboTarget = false;
            Submit(MatchCommand.EndTurn());
            if (IsOnlineClient)
                AfterStateChange();
        }

        public void BeginNextPhase(TurnDirection direction)
        {
            if (!IsLocalPhaseChooser)
                return;

            HasSelectedCard = false;
            IsChoosingComboTarget = false;
            Submit(MatchCommand.ChooseDirection(direction));
            if (IsOnlineClient)
                AfterStateChange();
        }

#if UNITY_EDITOR
        // DEV ONLY. These bypass the rule that the UI never changes Apostle numbers, so they only
        // exist in the Editor, and they will be removed together with DevTurnHud.

        /// <summary>DEV ONLY: changes the current Apostle's Souls to test Awakening.</summary>
        public void DevChangeCurrentSouls(int delta)
        {
            if (!CanAct || IsOnlineClient)
                return;

            ApostleState apostle = CurrentMatch.CurrentApostle;
            if (delta > 0)
                apostle.AddSouls(delta);
            else if (delta < 0)
                apostle.LoseSouls(-delta);

            CurrentMatch.RefreshGameState();
            AfterMatchChange();
        }

        /// <summary>DEV ONLY: gives the current Apostle extra shield.</summary>
        public void DevAddShieldToCurrent(int amount)
        {
            if (!CanAct || IsOnlineClient)
                return;

            CurrentMatch.CurrentApostle.AddShield(amount);
            AfterMatchChange();
        }
#endif

        // ---- Picking starting tiles ----

        // Whoever picks now: a person clicks a highlighted edge tile; a bot picks a random one after a pause.
        private void UpdateSpawnDraft()
        {
            int picker = _draft.CurrentPickerId;
            if (picker == 0)
                return;

            if (botsPlayOthers && picker != localApostleId)
            {
                _botWait -= Time.deltaTime;
                if (_botWait > 0f)
                    return;
                _botWait = botActionDelay;
                PickSpawn(_draft.ChooseRandomTile(_botRandom));
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
                return;
            Vector2 screenPoint = mouse.position.ReadValue();
            if (hud != null && hud.ContainsScreenPoint(screenPoint))
                return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;
            if (boardView.TryGetTileAtScreen(screenPoint, out HexCoord tile) && _draft.CanPick(tile))
                PickSpawn(tile);
        }

        private void PickSpawn(HexCoord tile)
        {
            int picker = _draft.CurrentPickerId;
            if (!_draft.TryPick(tile))
                return;

            // The disc appears on its tile straight away.
            int index = picker - 1;
            Vector3 position = boardView.GetWorldPosition(tile);
            _discTargets[index] = position;
            _discPositions[index] = position;
            _views[index].transform.position = position;
            _views[index].gameObject.SetActive(true);
            Debug.Log($"MatchController: Apostle {picker} starts at ({tile.Q},{tile.R}).");

            if (_draft.IsComplete)
            {
                boardView.ClearHighlights();
                // The discs are already in place: the match starts from the same tiles.
                for (int i = 0; i < _views.Count; i++)
                    _views[i].gameObject.SetActive(false);
                StartMatch(_draft.GetSpawnTiles(), _seatOrder);
                return;
            }
            _botWait = botActionDelay;
            AfterDraftChange();
        }

        // Highlights the tiles the person picking may choose, and says whose pick it is.
        private void AfterDraftChange()
        {
            int picker = _draft.CurrentPickerId;
            bool person = !botsPlayOthers || picker == localApostleId;
            if (!botsPlayOthers)
                _lastViewerId = picker;

            if (person)
            {
                _draft.GetLegalTiles(_legalDestinations);
                boardView.SetHighlights(_legalDestinations);
                Hint = botsPlayOthers
                    ? "Choose your starting tile: click a highlighted tile on the edge of the board."
                    : $"Apostle {picker}: choose your starting tile on the edge of the board.";
            }
            else
            {
                boardView.ClearHighlights();
                Hint = $"Apostle {picker} is choosing a starting tile...";
            }
            StateChanged?.Invoke();
        }

        // ---- Bots ----

        // Lets the bot whose turn it is (or who chooses the next Phase's direction) take one action
        // every botActionDelay seconds. If the rules refuse an action, the bot's turn ends, so a bot
        // can never get stuck.
        private void RunBots()
        {
            // Only the host (or an offline game) runs bots; a player who joined just shows the host's snapshots.
            if (!botsPlayOthers || CurrentMatch == null || CurrentMatch.IsOver || _online == OnlineRole.Client)
                return;

            int actorId = CurrentMatch.IsAwaitingPhaseStart
                ? CurrentMatch.GetNextPhaseChooserId()
                : CurrentMatch.CurrentApostle != null ? CurrentMatch.CurrentApostle.Id : 0;
            if (actorId == 0)
                return;

            // A new turn: the last bot's action is no longer news, and a bot starts with a longer pause.
            int turnKey = CurrentMatch.IsAwaitingPhaseStart ? -actorId : actorId;
            if (turnKey != _botTurnOwner)
            {
                _botTurnOwner = turnKey;
                _botActionsThisTurn = 0;
                _lastBotAction = string.Empty;
                _botWait = botTurnStartDelay + botActionDelay;
            }

            // People play their own seats: this screen's player, and online, the players who joined.
            if (actorId == localApostleId || IsRemoteSeat(actorId))
                return;

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
            AfterMatchChange();
        }

#if UNITY_EDITOR
        /// <summary>DEV ONLY: switches bots on or off for the other seats.</summary>
        public void DevSetBots(bool on)
        {
            if (IsOnline)
                return; // online, bots always play the seats nobody joined for
            botsPlayOthers = on;
            _botTurnOwner = -1;
            CancelSelection();
        }

        /// <summary>DEV ONLY: makes this screen belong to the next Apostle (with bots on).</summary>
        public void DevCycleLocalApostle()
        {
            if (CurrentMatch == null || IsOnline)
                return; // online, each screen keeps its own seat
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

        // Glides each piece to its tile (easing in and out, trailing smoke), plays the attacker's Strike,
        // and keeps the aura under the Apostle whose turn it is.
        private void AnimateDiscs()
        {
            if (_views.Count == 0)
                return;

            float delta = Time.deltaTime;
            float lunge = 0f;
            if (_lungeIndex >= 0)
            {
                _lungeElapsed += delta;
                float t = _lungeElapsed / _lungeDuration;
                if (t >= 1f)
                {
                    _lungeIndex = -1;
                }
                else
                {
                    lunge = StrikeOffset(t);
                    if (!_impactDone && t >= ImpactAt)
                    {
                        _impactDone = true;
                        Impact();
                    }
                }
            }

            for (int i = 0; i < _views.Count; i++)
            {
                // A new destination: start a move from wherever the piece is now.
                if (_discTargets[i] != _moveTo[i])
                {
                    _moveTo[i] = _discTargets[i];
                    _moveFrom[i] = _discPositions[i];
                    _moveElapsed[i] = 0f;
                    float distance = Vector3.Distance(_moveFrom[i], _moveTo[i]);
                    _moveDuration[i] = distance < 0.001f ? 0f
                        : MotionStyle.Seconds(Mathf.Clamp(distance / Mathf.Max(0.1f, discMoveSpeed) * 1.6f, 0.35f, 1.1f));
                    _smokeTimer[i] = 0f;
                }

                if (_moveElapsed[i] < _moveDuration[i])
                {
                    _moveElapsed[i] += delta;
                    float u = Mathf.Clamp01(_moveElapsed[i] / _moveDuration[i]);
                    _discPositions[i] = Vector3.Lerp(_moveFrom[i], _moveTo[i], MotionStyle.InOut(u));
                    _smokeTimer[i] -= delta;
                    if (_effects != null && _smokeTimer[i] <= 0f && u > 0.05f && u < 0.95f)
                    {
                        _smokeTimer[i] = MotionStyle.Seconds(0.07f);
                        _effects.Smoke(_discPositions[i], apostleDiameter);
                    }
                }
                else
                {
                    _discPositions[i] = _discTargets[i];
                }

                Vector3 position = _discPositions[i];
                if (i == _lungeIndex)
                    position += _lungeDirection * lunge;
                _views[i].transform.position = position;
            }

            if (_effects != null)
            {
                bool aura = _auraIndex >= 0 && _auraIndex < _views.Count && _views[_auraIndex].gameObject.activeSelf;
                _effects.SetAura(aura, aura ? _views[_auraIndex].transform.position : Vector3.zero,
                    aura ? ApostleColors.Get(_auraIndex + 1) : Color.clear, apostleDiameter * 2f);
            }
        }

        // The attacker's offset along the line of attack: it draws back, holds for a breath, strikes fast,
        // then returns slowly. No bounce.
        private float StrikeOffset(float t)
        {
            if (t < WindUpEnd)
                return -0.35f * lungeDistance * MotionStyle.Out(t / WindUpEnd);
            if (t < ImpactAt)
                return Mathf.Lerp(-0.35f * lungeDistance, lungeDistance, MotionStyle.In((t - WindUpEnd) / (ImpactAt - WindUpEnd)));
            return lungeDistance * (1f - MotionStyle.InOut((t - ImpactAt) / (1f - ImpactAt)));
        }

        // The Strike lands: a slash on every Apostle hit, a shudder and a dark pulse at the screen's edges.
        private void Impact()
        {
            if (_effects == null)
                return;
            for (int i = 0; i < _impactTargets.Count; i++)
                _effects.Strike(_impactTargets[i], _lungeDirection, apostleDiameter * 1.4f);
            _effects.Shake(0.12f, 0.35f);
            _effects.DarkPulse(0.6f);
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
                    _lungeDuration = MotionStyle.Seconds(StrikeDesignedSeconds);
                    _impactDone = false;
                    _strikeStartedNow = true; // the damage numbers wait for the impact
                    _impactTargets.Clear();
                    for (int h = 0; h < hits.Count; h++)
                    {
                        int hit = IndexOfApostle(hits[h].TargetId);
                        if (hit >= 0)
                            _impactTargets.Add(_discTargets[hit]);
                    }
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

        // Rising numbers for whatever changed since the last update: crimson damage, green healing,
        // gold Souls and violet Corruption. After a Strike they wait for its impact.
        private void ShowStatChanges()
        {
            float delay = _strikeStartedNow ? StrikeDesignedSeconds * ImpactAt : 0f;
            _strikeStartedNow = false;
            IReadOnlyList<ApostleState> apostles = CurrentMatch.Battlefield.Apostles;
            for (int i = 0; i < apostles.Count && i < _lastVessel.Length; i++)
            {
                ApostleState apostle = apostles[i];
                if (_statsTaken && _numbers != null)
                {
                    Vector3 above = _discTargets[i] + new Vector3(0f, 0.45f, -2f); // in front of the standing pieces
                    int line = 0;
                    int vessel = apostle.Vessel - _lastVessel[i];
                    if (vessel != 0)
                        _numbers.Show(above + Vector3.up * (0.4f * line++), vessel > 0 ? $"+{vessel}" : vessel.ToString(),
                            vessel > 0 ? new Color(0.50f, 0.85f, 0.60f) : new Color(0.90f, 0.14f, 0.18f), delay);
                    int souls = apostle.Souls - _lastSouls[i];
                    if (souls != 0)
                        _numbers.Show(above + Vector3.up * (0.4f * line++), souls > 0 ? $"+{souls} Soul" : $"{souls} Soul",
                            new Color(0.95f, 0.78f, 0.35f), delay);
                    int corruption = apostle.Corruption - _lastCorruption[i];
                    if (corruption != 0)
                        _numbers.Show(above + Vector3.up * (0.4f * line), corruption > 0 ? $"+{corruption} Corr" : $"{corruption} Corr",
                            new Color(0.68f, 0.42f, 0.95f), delay);
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

        // The match itself changed: online, the host sends everyone a snapshot; then the screen updates.
        private void AfterMatchChange()
        {
            MatchChanged?.Invoke();
            AfterStateChange();
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
            _auraIndex = -1;
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
                // No size pop for the Apostle whose turn it is: a slow aura under its piece instead.
                view.SetActiveTurn(false);
                if (apostle == CurrentMatch.CurrentApostle && !CurrentMatch.IsOver)
                    _auraIndex = i;
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