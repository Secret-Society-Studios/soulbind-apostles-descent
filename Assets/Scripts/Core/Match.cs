using System;
using System.Collections.Generic;

namespace SoulBind.Core
{
    /// <summary>
    /// One match: the battlefield, the turn order, the cards, and the turn flow that ties them together.
    /// Pure rules data: no Unity types. The UI and network layers call these methods and
    /// read the resulting state. They never change Apostle numbers on their own.
    /// </summary>
    public sealed class Match
    {
        // Kept for decisions made during play (such as Ascension tile tie-breaks).
        private readonly Random _random;

        // One hand per Apostle, at the same index as Battlefield.Apostles.
        private readonly Hand[] _hands;

        // Reused for every card effect, so playing a card allocates nothing.
        private readonly CardPlayContext _playContext;

        // Set by Death while Engaged: a target below this Vessel is executed if the attack breaks DEF. 0 = off.
        private int _executeBelowVessel;

        // Corruption-reducing cards played so far this turn (for Strength).
        private int _corruptionReducersThisTurn;

        // A Strength played this turn that has not been paired with another reducer yet.
        private bool _strengthAwaitingPartner;

        // Every card that is part of the current attack, in play order (for The Lovers).
        private readonly List<ArcanaId> _attackCards = new List<ArcanaId>(8);

        // Set by The Emperor: the attack doubles at Strike if the target is more Corrupted than the player.
        private bool _emperorArmed;

        public Battlefield Battlefield { get; }
        public TurnTracker Turns { get; }

        /// <summary>The shared draw and discard piles.</summary>
        public Deck Deck { get; }

        /// <summary>Who is Awakened and where the Ascension tile is.</summary>
        public AscensionTracker Ascension { get; }

        /// <summary>The Apostle whose turn it is. Null while a Phase is waiting to start, or after the match ends.</summary>
        public ApostleState CurrentApostle { get; private set; }

        /// <summary>Capacity left this turn. Null whenever CurrentApostle is null.</summary>
        public TurnCapacity CurrentCapacity { get; private set; }

        /// <summary>Where the current turn is in the engage-once flow: Free, Engaged or Disengaged.</summary>
        public EngagementState Engagement { get; private set; }

        /// <summary>The Apostle being attacked while Engaged, otherwise 0.</summary>
        public int EngagedTargetId { get; private set; }

        /// <summary>Total ATK built up so far while Engaged, otherwise 0.</summary>
        public int PendingAttack { get; private set; }

        public bool IsOver { get; private set; }

        /// <summary>Id of the winner, or 0 if the match ended with no living Apostle.</summary>
        public int WinnerId { get; private set; }

        /// <summary>Why the match ended. None while it is still running.</summary>
        public MatchEndReason EndReason { get; private set; }

        public bool IsAwaitingPhaseStart => Turns.IsAwaitingPhaseStart;

        private Match(Battlefield battlefield, TurnTracker turns, Deck deck, Random random)
        {
            Battlefield = battlefield;
            Turns = turns;
            Deck = deck;
            _random = random;
            Ascension = new AscensionTracker();
            _playContext = new CardPlayContext(this);

            _hands = new Hand[battlefield.Apostles.Count];
            for (int i = 0; i < _hands.Length; i++)
                _hands[i] = new Hand();
        }

        /// <summary>
        /// Creates a match. Apostle i+1 spawns on spawnTiles[i]. The random source is used
        /// first for the seat order, then the special tiles, then the deck shuffle, so the same
        /// seed always gives the same starting player, tiles and cards.
        /// </summary>
        public static Match Create(int playerCount, HexBoard board, IReadOnlyList<HexCoord> spawnTiles, Random random)
            => Create(playerCount, board, spawnTiles, random, null);

        /// <summary>
        /// Creates a match with a prepared deck (for tests and scripted scenarios such as a tutorial).
        /// Pass null to shuffle a full deck as normal.
        /// </summary>
        public static Match Create(int playerCount, HexBoard board, IReadOnlyList<HexCoord> spawnTiles,
            Random random, Deck deck)
        {
            if (board == null)
                throw new ArgumentNullException(nameof(board));
            if (spawnTiles == null)
                throw new ArgumentNullException(nameof(spawnTiles));
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            if (playerCount < GameConstants.MinPlayers || playerCount > GameConstants.MaxPlayers)
                throw new ArgumentOutOfRangeException(nameof(playerCount), "Player count must be 2 to 6.");
            if (spawnTiles.Count != playerCount)
                throw new ArgumentException("There must be exactly one spawn tile per player.", nameof(spawnTiles));

            // The seat order must be the first use of the random source,
            // so the same seed always gives the same starting player.
            int[] seatOrder = TurnTracker.CreateRandomSeatOrder(playerCount, random);
            SpecialTiles specialTiles = SpecialTiles.CreateRandom(board, random);
            if (deck == null)
                deck = Deck.CreateFull(random);

            var battlefield = new Battlefield(board, specialTiles);
            int startingSouls = GameConstants.GetStartingSouls(playerCount);
            for (int i = 0; i < playerCount; i++)
                battlefield.AddApostle(new ApostleState(i + 1, spawnTiles[i], startingSouls));

            var turns = new TurnTracker(seatOrder);
            var match = new Match(battlefield, turns, deck, random);
            match.DealStartingHands();
            match.StartTurn();
            return match;
        }

        /// <summary>
        /// The cards in an Apostle's hand. Hidden information: the network layer must only
        /// ever send a hand to its owner.
        /// </summary>
        public IReadOnlyList<ArcanaId> GetHand(int apostleId) => _hands[IndexOf(apostleId)].Cards;

        /// <summary>
        /// Moves the current Apostle one tile. See MoveResult for the possible outcomes.
        /// Moving is not allowed while Engaged. Only the Awakened Apostle may step onto the Ascension tile.
        /// A successful move can Awaken an Apostle or win the match: check IsOver afterwards.
        /// </summary>
        public MoveResult Move(HexCoord destination)
        {
            RequireActiveTurn();

            if (Engagement == EngagementState.Engaged)
                return MoveResult.EngagedInCombat;

            if (IsAscensionTileClosedTo(CurrentApostle, destination)
                && HexCoord.AreAdjacent(CurrentApostle.Position, destination))
                return MoveResult.AscensionTileReserved;

            MoveResult result = Battlefield.TryMove(CurrentApostle, destination, CurrentCapacity);
            if (result == MoveResult.Success)
                UpdateGameOver();
            return result;
        }

        /// <summary>
        /// Fills results with the tiles the current Apostle can move to right now, leaving out
        /// the Ascension tile for anyone but the Awakened Apostle. Empty when nobody's turn is active
        /// or while Engaged. The caller supplies the list so it can be reused without allocating.
        /// </summary>
        public void GetLegalDestinations(List<HexCoord> results)
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));
            if (CurrentApostle == null || Engagement == EngagementState.Engaged)
            {
                results.Clear();
                return;
            }

            Battlefield.GetLegalDestinations(CurrentApostle, CurrentCapacity, results);
            for (int i = results.Count - 1; i >= 0; i--)
            {
                if (IsAscensionTileClosedTo(CurrentApostle, results[i]))
                    results.RemoveAt(i);
            }
        }

        /// <summary>
        /// Plays a card from the current Apostle's hand. Every played card costs its CAP, goes to
        /// the discard pile and adds its DEF to the shield.
        /// While Free: pass a targetId to attack with a card that has ATK; this engages that target.
        /// Pass 0 to play it without attacking (its ATK is lost).
        /// While Engaged: the card's ATK adds to the attack; targetId must be 0 or the engaged target.
        /// While Disengaged: no more cards can be played this turn.
        /// Nothing changes unless the result is Success.
        /// </summary>
        public CardPlayResult PlayCard(ArcanaId card, int targetId)
        {
            RequireActiveTurn();

            if (Engagement == EngagementState.Disengaged)
                return CardPlayResult.Disengaged;

            Hand hand = _hands[IndexOf(CurrentApostle.Id)];
            if (!hand.Contains(card))
                return CardPlayResult.NotInHand;

            CardDefinition definition = CardCatalog.Get(card);
            if (definition.Cap > 0 && !CurrentCapacity.CanSpend(definition.Cap))
                return CardPlayResult.NotEnoughCapacity;

            bool engagesNow = false;
            if (Engagement == EngagementState.Engaged)
            {
                if (targetId != 0 && targetId != EngagedTargetId)
                    return CardPlayResult.WrongTarget;
            }
            else if (targetId != 0)
            {
                if (definition.Attack == 0)
                    return CardPlayResult.CardHasNoAttack;

                switch (CheckTarget(targetId, out _))
                {
                    case AttackResult.Resolved:
                        engagesNow = true;
                        break;
                    case AttackResult.OutOfRange:
                        return CardPlayResult.TargetOutOfRange;
                    default:
                        return CardPlayResult.InvalidTarget;
                }
            }

            // Every check passed: apply the card.
            if (definition.Cap > 0)
                CurrentCapacity.Spend(definition.Cap);
            hand.Remove(card);
            Deck.Discard(card);
            CurrentApostle.AddShield(definition.Defense);

            if (engagesNow)
            {
                Engagement = EngagementState.Engaged;
                EngagedTargetId = targetId;
                PendingAttack = definition.Attack;
            }
            else if (Engagement == EngagementState.Engaged)
            {
                PendingAttack += definition.Attack;
            }
            // While Free with no target, the card's ATK is lost and only its DEF applies.

            // Then the card's own effect, if it has one.
            CardEffect effect = CardEffects.Get(card);
            if (effect != null)
            {
                // Strength: a Strength played earlier this turn is doubled once another
                // Corruption-reducing card joins it (checked before this card's own effect,
                // so a Strength never pairs with itself).
                if (effect.ReducesCorruption && _strengthAwaitingPartner)
                {
                    CurrentApostle.ReduceCorruption(StrengthEffect.BaseReduction);
                    _strengthAwaitingPartner = false;
                }

                effect.OnPlayed(_playContext);

                if (effect.ReducesCorruption)
                    _corruptionReducersThisTurn++;

                UpdateGameOver();
            }

            // Remember the cards that make up the attack. This happens after the effect,
            // so an effect only ever sees the cards played before it.
            if (Engagement == EngagementState.Engaged)
                _attackCards.Add(card);

            return CardPlayResult.Success;
        }

        // ---- Called by card effects through CardPlayContext ----

        internal ApostleState EngagedTarget =>
            Engagement == EngagementState.Engaged ? FindApostle(EngagedTargetId) : null;

        internal int CorruptionReducersThisTurn => _corruptionReducersThisTurn;

        internal void MarkStrengthAwaitingPartner() => _strengthAwaitingPartner = true;

        // How many copies of this card are already part of the current attack.
        internal int CountInCurrentAttack(ArcanaId card)
        {
            int count = 0;
            for (int i = 0; i < _attackCards.Count; i++)
            {
                if (_attackCards[i] == card)
                    count++;
            }
            return count;
        }

        // Raises the current turn's maximum Capacity; Capacity already spent stays spent.
        internal void IncreaseCurrentCapacity(int amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "Amount cannot be negative.");
            if (amount == 0)
                return;

            int spent = CurrentCapacity.Maximum - CurrentCapacity.Remaining;
            var raised = new TurnCapacity(CurrentCapacity.Maximum + amount);
            if (spent > 0)
                raised.Spend(spent);
            CurrentCapacity = raised;
        }

        internal void ArmEmperor()
        {
            if (Engagement != EngagementState.Engaged)
                throw new InvalidOperationException("The Emperor can only be armed while Engaged.");
            _emperorArmed = true;
        }

        // Draws one card into the current Apostle's hand. Returns false if both piles are empty.
        internal bool DrawCardForCurrent()
        {
            if (!Deck.TryDraw(out ArcanaId card))
                return false;
            _hands[IndexOf(CurrentApostle.Id)].Add(card);
            return true;
        }

        /// <summary>
        /// Resolves the built-up attack against the engaged target, then disengages: no more cards
        /// this turn. Returns false (and changes nothing) if the current Apostle is not Engaged.
        /// A strike can Awaken someone, eliminate someone or end the match: check IsOver afterwards.
        /// </summary>
        public bool Strike(out AttackOutcome outcome)
        {
            RequireActiveTurn();
            outcome = default;
            if (Engagement != EngagementState.Engaged)
                return false;

            ApostleState target = FindApostle(EngagedTargetId);
            int totalAttack = PendingAttack;
            int executeBelowVessel = _executeBelowVessel;
            bool emperorArmed = _emperorArmed;

            ResetEngagement();
            Engagement = EngagementState.Disengaged;

            // The target cannot move while we are engaged, so it is still adjacent.
            // If something else eliminated it meanwhile, the attack has nothing to hit.
            if (!target.IsEliminated)
            {
                // Timed ATK bonuses (Hierophant, Hanged Man) join the attack once, as it resolves.
                totalAttack += CurrentApostle.GetTimedTotal(TimedEffectKind.Attack);

                // The Emperor: compared now, as the attack resolves, and doubles the whole total.
                if (emperorArmed && target.Corruption > CurrentApostle.Corruption)
                    totalAttack *= EmperorEffect.AttackMultiplier;

                int vesselBefore = target.Vessel;
                outcome = Combat.Resolve(CurrentApostle, target, totalAttack);

                // Death: a broken defense on a target that had less than 5 Vessel is an execution.
                if (executeBelowVessel > 0 && outcome.BrokeDefense
                    && vesselBefore < executeBelowVessel && !target.IsEliminated)
                    target.TakeDamage(target.Vessel);
            }

            UpdateGameOver();
            return true;
        }

        // Called by card effects through CardPlayContext.
        internal void AddToPendingAttack(int amount)
        {
            if (Engagement != EngagementState.Engaged)
                throw new InvalidOperationException("ATK can only be added while Engaged.");
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "Amount cannot be negative.");
            PendingAttack += amount;
        }

        // Called by card effects through CardPlayContext.
        internal void ArmExecute(int vesselBelow)
        {
            if (Engagement != EngagementState.Engaged)
                throw new InvalidOperationException("An execute can only be armed while Engaged.");
            _executeBelowVessel = Math.Max(_executeBelowVessel, vesselBelow);
        }

        /// <summary>
        /// LOW-LEVEL: the current Apostle attacks with this total ATK, ignoring cards, Capacity and
        /// the engage-once rule. Players attack through PlayCard and Strike; this stays public only
        /// for tests and will become private once nothing else needs it.
        /// </summary>
        public AttackResult Attack(int targetId, int totalAttack, out AttackOutcome outcome)
        {
            RequireActiveTurn();
            if (totalAttack < 1)
                throw new ArgumentOutOfRangeException(nameof(totalAttack), "An attack needs ATK above 0.");

            outcome = default;
            AttackResult check = CheckTarget(targetId, out ApostleState target);
            if (check != AttackResult.Resolved)
                return check;

            outcome = Combat.Resolve(CurrentApostle, target, totalAttack);
            UpdateGameOver();
            return AttackResult.Resolved;
        }

        /// <summary>
        /// Fills results with the Apostles the current Apostle can attack right now: every adjacent
        /// living Apostle while Free, only the engaged target while Engaged, and nobody once Disengaged.
        /// The caller supplies the list to avoid allocating.
        /// </summary>
        public void GetAttackTargets(List<ApostleState> results)
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));
            if (CurrentApostle == null || Engagement == EngagementState.Disengaged)
            {
                results.Clear();
                return;
            }

            Battlefield.GetAttackTargets(CurrentApostle, results);
            if (Engagement == EngagementState.Engaged)
            {
                for (int i = results.Count - 1; i >= 0; i--)
                {
                    if (results[i].Id != EngagedTargetId)
                        results.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Ends the current turn. If the Apostle is still Engaged, the attack is resolved first.
        /// Then the Apostle draws a card and gains Corruption if its hand is above the limit.
        /// Read CurrentApostle, IsAwaitingPhaseStart and IsOver afterwards for the resulting state:
        /// the strike or Corruption damage can end the match, and eliminated Apostles are skipped.
        /// </summary>
        public TurnEvent EndTurn()
        {
            RequireActiveTurn();

            if (Engagement == EngagementState.Engaged)
            {
                Strike(out _);
                if (IsOver)
                    return TurnEvent.NextTurn;
            }

            FinishTurn(CurrentApostle);

            TurnEvent turnEvent = Turns.EndTurn(Battlefield.Apostles);
            if (turnEvent == TurnEvent.PhaseEnded)
            {
                CurrentApostle = null;
                CurrentCapacity = null;
                ResetEngagement();
            }
            else
            {
                StartTurn();
            }
            return turnEvent;
        }

        /// <summary>The Apostle who chooses the turn direction when the next Phase begins.</summary>
        public int GetNextPhaseChooserId() => Turns.GetLowestVesselApostleId(Battlefield.Apostles);

        /// <summary>Starts the next Phase with the direction chosen by the lowest-Vessel Apostle.</summary>
        public void BeginNextPhase(TurnDirection direction)
        {
            if (UpdateGameOver())
                throw new InvalidOperationException("The match is over.");
            if (!Turns.IsAwaitingPhaseStart)
                throw new InvalidOperationException("The current phase has not ended.");

            Turns.BeginNextPhase(Battlefield.Apostles, direction);
            StartTurn();
        }

        /// <summary>
        /// Re-checks Awakening, Ascension and eliminations. Call this after anything that can
        /// change Souls or eliminate an Apostle (such as combat). Returns true if the match is over.
        /// </summary>
        public bool RefreshGameState() => UpdateGameOver();

        // Deals the starting hand one card at a time, going round the Apostles in Id order.
        private void DealStartingHands()
        {
            for (int round = 0; round < GameConstants.StartingHandSize; round++)
            {
                for (int i = 0; i < _hands.Length; i++)
                {
                    if (Deck.TryDraw(out ArcanaId card))
                        _hands[i].Add(card);
                }
            }
        }

        // Turn step 3 (draw) and the hand-size penalty, applied as the turn ends.
        private void FinishTurn(ApostleState apostle)
        {
            Hand hand = _hands[IndexOf(apostle.Id)];
            for (int i = 0; i < GameConstants.CardsDrawnPerTurn; i++)
            {
                if (Deck.TryDraw(out ArcanaId card))
                    hand.Add(card);
            }

            if (hand.IsOverLimit)
                apostle.AddCorruption(GameConstants.HandOverLimitCorruption);
        }

        // Applies start-of-turn effects to the current Apostle: the shield clears and expired
        // timed effects end (BeginTurn), then
        // Corruption damage applies. If that eliminates them, their turn passes on.
        private void StartTurn()
        {
            ResetEngagement();
            _corruptionReducersThisTurn = 0;
            _strengthAwaitingPartner = false;
            while (true)
            {
                if (UpdateGameOver())
                    return;

                ApostleState apostle = FindApostle(Turns.CurrentApostleId);
                apostle.BeginTurn();
                apostle.ApplyCorruptionDamage();

                if (!apostle.IsEliminated)
                {
                    CurrentApostle = apostle;
                    CurrentCapacity = CreateCapacityFor(apostle);
                    return;
                }

                if (UpdateGameOver())
                    return;

                if (Turns.EndTurn(Battlefield.Apostles) == TurnEvent.PhaseEnded)
                {
                    CurrentApostle = null;
                    CurrentCapacity = null;
                    return;
                }
            }
        }

        // The Phase's Capacity, adjusted by timed effects (The Hermit's bonus, The Tower's penalty).
        private TurnCapacity CreateCapacityFor(ApostleState apostle)
        {
            TurnCapacity phaseCapacity = Turns.CreateTurnCapacity();
            int change = apostle.GetTimedTotal(TimedEffectKind.MaxCapacity)
                       - apostle.GetTimedTotal(TimedEffectKind.CapacityPenalty);
            if (change == 0)
                return phaseCapacity;
            return new TurnCapacity(Math.Max(0, phaseCapacity.Maximum + change));
        }

        private void ResetEngagement()
        {
            Engagement = EngagementState.Free;
            EngagedTargetId = 0;
            PendingAttack = 0;
            _executeBelowVessel = 0;
            _attackCards.Clear();
            _emperorArmed = false;
        }

        private void RequireActiveTurn()
        {
            if (UpdateGameOver())
                throw new InvalidOperationException("The match is over.");
            if (Turns.IsAwaitingPhaseStart)
                throw new InvalidOperationException("The phase has ended; call BeginNextPhase first.");
        }

        // Checks whether the current Apostle could attack this Apostle right now.
        // Returns Resolved when the target is valid.
        private AttackResult CheckTarget(int targetId, out ApostleState target)
        {
            target = TryFindApostle(targetId);
            if (target == null)
                return AttackResult.TargetNotFound;
            if (target == CurrentApostle)
                return AttackResult.TargetIsSelf;
            if (target.IsEliminated)
                return AttackResult.TargetEliminated;
            if (!HexCoord.AreAdjacent(CurrentApostle.Position, target.Position))
                return AttackResult.OutOfRange;
            return AttackResult.Resolved;
        }

        // Updates Awakening, discards eliminated hands, then checks both win conditions.
        // Returns true if the match is over.
        private bool UpdateGameOver()
        {
            if (IsOver)
                return true;

            Ascension.Update(Battlefield, CurrentApostle, _random);

            IReadOnlyList<ApostleState> apostles = Battlefield.Apostles;
            int living = 0;
            int lastLivingId = 0;
            for (int i = 0; i < apostles.Count; i++)
            {
                if (!apostles[i].IsEliminated)
                {
                    living++;
                    lastLivingId = apostles[i].Id;
                }
                else if (_hands[i].Count > 0)
                {
                    // An eliminated Apostle's cards go to the discard pile.
                    _hands[i].DiscardAllTo(Deck);
                }
            }

            if (living <= 1)
            {
                if (living == 1)
                    EndMatch(lastLivingId, MatchEndReason.LastApostleStanding);
                else
                    EndMatch(0, MatchEndReason.NoSurvivors);
                return true;
            }

            if (Ascension.HasAscended(Battlefield))
            {
                EndMatch(Ascension.AwakenedId, MatchEndReason.Ascension);
                return true;
            }

            return false;
        }

        // True if this tile is the Ascension tile and the Apostle is not the Awakened one.
        private bool IsAscensionTileClosedTo(ApostleState apostle, HexCoord tile) =>
            Ascension.HasAwakened && Ascension.Tile == tile && apostle.Id != Ascension.AwakenedId;

        private void EndMatch(int winnerId, MatchEndReason reason)
        {
            IsOver = true;
            WinnerId = winnerId;
            EndReason = reason;
            CurrentApostle = null;
            CurrentCapacity = null;
            ResetEngagement();
        }

        // The index of an Apostle in Battlefield.Apostles (and in _hands).
        private int IndexOf(int apostleId)
        {
            IReadOnlyList<ApostleState> apostles = Battlefield.Apostles;
            for (int i = 0; i < apostles.Count; i++)
            {
                if (apostles[i].Id == apostleId)
                    return i;
            }
            throw new ArgumentException($"No Apostle with id {apostleId}.", nameof(apostleId));
        }

        // Like FindApostle, but returns null instead of throwing (for ids that come from player input).
        private ApostleState TryFindApostle(int id)
        {
            IReadOnlyList<ApostleState> apostles = Battlefield.Apostles;
            for (int i = 0; i < apostles.Count; i++)
            {
                if (apostles[i].Id == id)
                    return apostles[i];
            }
            return null;
        }

        private ApostleState FindApostle(int id)
        {
            ApostleState apostle = TryFindApostle(id);
            if (apostle == null)
                throw new InvalidOperationException($"No Apostle with id {id}.");
            return apostle;
        }
    }
}