using System;
using System.Collections.Generic;


namespace SoulBind.Core
{
    /// <summary>
    /// The bot that plays the empty seats. It thinks about who is the biggest threat instead of just
    /// hitting whoever is closest:
    /// - An Awakened opponent comes first. One hit steals a Soul and drops them below 20, which ends
    ///   their Awakening, so every bot either guards the Ascension tile (if it can get there first)
    ///   or chases them, and attacks them over anyone else in reach.
    /// - Otherwise it hunts the opponent closest to Awakening (most Souls), preferring ones it can
    ///   finish off this turn and weak ones, and weighing how far away they are.
    /// - It looks after itself: low Vessel sends it to a Heal tile, high Corruption to a Purify tile,
    ///   and near 20 Souls it grabs a nearby Soul tile.
    /// - While Engaged it stops adding cards once the attack is already lethal, saving the rest.
    /// - The Awakened bot heads for the Ascension tile.
    /// Decide returns one action at a time, always one the rules allow, so the caller can show each step.
    /// Pure rules code: no Unity types.
    /// </summary>
    public sealed class SimpleBot
    {
        // How the bot weighs things. Higher Priority = attack or chase first.
        private const int AwakenedPriority = 1000; // stop an ascension above everything
        private const int LethalPriority = 500; // a target this turn's cards can eliminate
        private const int SoulPriority = 10; // per Soul: the closer to 20, the bigger the threat
        private const int DistancePenalty = 15; // per tile away, when choosing whom to walk toward

        private const int LowVessel = 20; // at or below this, a nearby heal tile is worth a detour
        private const int HighCorruption = GameConstants.CorruptionThreshold - 1; // one more and it's soullocked
        private const int NearAwakening = GameConstants.SoulsToAwaken - 6; // close enough to want soul tiles
        private const int TileDetourRange = 4; // how far it walks for a special tile

        private readonly List<HexCoord> _destinations = new List<HexCoord>(6);
        private readonly List<ApostleState> _targets = new List<ApostleState>(6);
        private readonly List<int> _caps = new List<int>(8);

        /// <summary>The next action for whoever must act now (the current Apostle, or the Phase chooser).</summary>
        public BotAction Decide(Match match)
        {
            if (match == null)
                throw new ArgumentNullException(nameof(match));
            if (match.IsOver)
                return BotAction.None;
            if (match.IsAwaitingPhaseStart)
                return BotAction.ChooseDirection(TurnDirection.Forward);

            ApostleState me = match.CurrentApostle;
            if (me == null)
                return BotAction.None;
            
            switch (match.Engagement)
            {
                case EngagementState.Engaged:
                    return DecideWhileEngaged(match);
                
                case EngagementState.Free:
                    return DecideWhileFree(match, me);

                default:
                    return BotAction.EndTurn;
            }
        }

        /// <summary>
        /// Carries out an action on the match. Returns false if the rules refused it (nothing changes then);
        /// the caller should end the bot's turn in that case so it can never get stuck.
        /// </summary>
        public static bool Apply(Match match, BotAction action)
        {
            if (match == null)
                throw new ArgumentNullException(nameof(match));

            switch (action.Kind)
            {
                case BotActionKind.ChooseDirection:
                    if (!match.IsAwaitingPhaseStart)
                        return false;
                    match.BeginNextPhase(action.Direction);
                    return true;
                case BotActionKind.Move:
                    return match.Move(action.Destination) == MoveResult.Success;
                case BotActionKind.PlayCard:
                    return match.PlayCard(action.Card, action.TargetId, action.ComboTargetId) == CardPlayResult.Success;
                case BotActionKind.Strike:
                    return match.Strike(out _);
                case BotActionKind.EndTurn:
                    if (match.CurrentApostle == null)
                        return false;
                    match.EndTurn();
                    return true;
                default:
                    return false;
                
            }
        }

        // ---- Engaged: add ATK until the attack is lethal (or nothing more is affordable), then Strike ----

        private BotAction DecideWhileEngaged(Match match)
        {
            ApostleState target = FindById(match, match.EngagedTargetId);
            bool lethal = target != null && match.PendingAttack - target.Defense >= target.Vessel;
            if (!lethal && TryPickCard(match, true, out ArcanaId attackCard))
                return BotAction.PlayCard(attackCard, 0, 0); // the Nocturne combo hits the engaged target
            return BotAction.Strike;
        }

        // ---- Free: chase, attack, look after itself, play the rest ----

        private BotAction DecideWhileFree(Match match, ApostleState me)
        {
            AscensionTracker ascension = match.Ascension;
            bool iAmAwakened = ascension.HasAwakened && ascension.AwakenedId == me.Id;
            ApostleState awakenedFoe = ascension.HasAwakened && !iAmAwakened ? FindById(match, ascension.AwakenedId) : null;
            if (awakenedFoe != null && awakenedFoe.IsEliminated)
                awakenedFoe = null;
            
            // 1. The Awakened bot walks to the Ascension tile.
            if (iAmAwakened && TryStepToward(match, me, ascension.Tile, 0, out HexCoord ascensionStep))
                return BotAction.Move(ascensionStep);

            match.GetAttackTargets(_targets);
            bool awakenedInReach = awakenedFoe != null && _targets.Contains(awakenedFoe);

            // 2. Someone else is Awakened and out of reach: getting to them matters more than anything else.
            //    If this bot can reach the Ascension tile's surroundings first, it waits there for them;
            //    otherwise it chases them. (The tile itself is closed to everyone but the Awakened.)
            if (awakenedFoe != null && !awakenedInReach)
            {
                HexCoord goal = awakenedFoe.Position;
                if (HexCoord.Distance(me.Position, ascension.Tile) < HexCoord.Distance(awakenedFoe.Position, ascension.Tile))
                    goal = ascension.Tile;
                if (TryStepToward(match, me, goal, 1, out HexCoord interceptStep))
                    return BotAction.Move(interceptStep);
            }

            // 3. Attack the most dangerous Apostle in reach with the strongest affordable card.
            if (_targets.Count > 0 && TryPickCard(match, true, out ArcanaId attackCard))
            {
                int potential = AffordableAttack(match);
                return BotAction.PlayCard(attackCard, MostDangerous(_targets, match.Ascension.AwakenedId, potential).Id, 0);

            }

            // 4. Look after itself on the way: a nearby special tile it needs.
            if (awakenedFoe == null && TryStepToNeededTile(match, me, out HexCoord tileStep))
                return BotAction.Move(tileStep);

            // 5. Walk toward the best opponent to hunt, keeping enough Capacity to attack when it gets there.
            ApostleState prey = BestToHunt(match, me);
            if (!iAmAwakened && prey != null && match.CurrentCapacity.Remaining > CheapestAttackCap(match) && TryStepToward(match, me, prey.Position, 1, out HexCoord step))
                return BotAction.Move(step);

            // 6. Nothing to attack: play cards without ATK (none is wasted) for their DEF and effects.
            //    If one triggers the Nocturne combo (or is Wheel of Fortune), it goes to the biggest threat.
            if (TryPickCard(match, false, out ArcanaId otherCard))
            {
                ApostleState threat = awakenedFoe ?? prey;
                return BotAction.PlayCard(otherCard, 0, threat != null ? threat.Id : 0);
            }

            return BotAction.EndTurn;
        }

        // ---- Choosing targets ----

        // How much this bot wants to hit this Apostle now. potentialAttack = the most ATK its affordable
        // cards add up to this turn, to spot targets it can eliminate.
        private static int Priority(ApostleState target, int awakenedId, int potentialAttack)
        {
            int score = target.Souls * SoulPriority - (target.Vessel + target.Defense);
            if (target.Id == awakenedId)
                score += AwakenedPriority;
            if (potentialAttack - target.Defense >= target.Vessel)
                score += LethalPriority;
            return score;
        }

        private static ApostleState MostDangerous(List<ApostleState> targets, int awakenedId, int potentialAttack)
        {
            ApostleState best = targets[0];
            int bestScore = int.MinValue;
            for (int i = 0; i < targets.Count; i++)
            {
                int score = Priority(targets[i], awakenedId, potentialAttack);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = targets[i];
                }
            }
            return best;
        }

        // the oppoonent most worth walking toward: dangerous and not too far away.
        private static ApostleState BestToHunt(Match match, ApostleState me)
        {
            int awakenedId = match.Ascension.AwakenedId;
            ApostleState best = null;
            int bestScore = int.MinValue;
            IReadOnlyList<ApostleState> apostles = match.Battlefield.Apostles;
            for (int i = 0; i < apostles.Count; i++)
            {
                ApostleState other = apostles[i];
                if (other == me || other.IsEliminated)
                    continue;
                int score = Priority(other, awakenedId, 0) - HexCoord.Distance(me.Position, other.Position) * DistancePenalty;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = other;
                }
            }
            return best;
        }

        // ---- Special tiles ----

        // A step toward the nearest special tile this Apostle needs now, if one is close enough.
        private bool TryStepToNeededTile(Match match, ApostleState me, out HexCoord step)
        {
            step = default;
            bool wantHeal = me.Vessel <= LowVessel;
            bool wantPurify = me.Corruption >= HighCorruption;
            bool wantSoul = !me.IsSoullocked && !me.HasMaxSouls && me.Souls >= NearAwakening;
            if (!wantHeal && !wantPurify && !wantSoul)
                return false;

            bool found = false;
            HexCoord goal = default;
            int nearest = TileDetourRange + 1;
            foreach (KeyValuePair<HexCoord, SpecialTileType> pair in match.Battlefield.Specials.Active)
            {
                bool wanted = (pair.Value == SpecialTileType.Heal && wantHeal) || (pair.Value == SpecialTileType.Purify && wantPurify) || (pair.Value == SpecialTileType.Soul && wantSoul);
                if (!wanted || match.Battlefield.IsOccupied(pair.Key))
                    continue;
                int distance = HexCoord.Distance(me.Position, pair.Key);
                if (distance < nearest)
                {
                    nearest = distance;
                    goal = pair.Key;
                    found = true;
                }
            }
            return found && TryStepToward(match, me, goal, 0, out step);
        }

        // ---- Cards ----

        // The affordable card with the most ATK (withAttack), or the affordable card with the most DEF
        // among cards without ATK (!withAttack).
        private static bool TryPickCard(Match match, bool withAttack, out ArcanaId best)
        {
            best = default;
            int bestScore = -1;
            int remaining = match.CurrentCapacity.Remaining;
            IReadOnlyList<ArcanaId> hand = match.GetHand(match.CurrentApostle.Id);
            for (int i = 0; i < hand.Count; i++)
            {
                CardDefinition card = CardCatalog.Get(hand[i]);
                if (card.Cap > remaining || (card.Attack > 0) != withAttack)
                    continue;
                int score = withAttack ? card.Attack : card.Defense;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = card.Id;
                }
            }
            return bestScore >= 0;
        }

        // Roughly the most ATK this turn's Capacity can pay for: the strongest affordable ATK cards first.
        private int AffordableAttack(Match match)
        {
            int remaining = match.CurrentCapacity.Remaining;
            IReadOnlyList<ArcanaId> hand = match.GetHand(match.CurrentApostle.Id);
            _caps.Clear();
            for (int i = 0; i < hand.Count; i++)
            {
                if (CardCatalog.Get(hand[i]).Attack > 0)
                    _caps.Add(i);
            }

            // strongest first (a few cards at most, so a simple sort is fine)
            _caps.Sort((a, b) => CardAttack.Of(hand[b]).CompareTo(CardAttack.Of(hand[a])));

            int total = 0;
            for (int i = 0; i < _caps.Count; i++)
            {
                CardDefinition card = CardCatalog.Get(hand[_caps[i]]);
                if (card.Cap > remaining)
                    continue;
                remaining -= card.Cap;
                total += CardAttack.Of(card.Id);
            }
            return total;
        }

        private static int CheapestAttackCap(Match match)
        {
            int cheapest = 0;
            bool found = false;
            IReadOnlyList<ArcanaId> hand = match.GetHand(match.CurrentApostle.Id);
            for (int i = 0; i < hand.Count; i++)
            {
                CardDefinition card = CardCatalog.Get(hand[i]);
                if (card.Attack > 0 && (!found || card.Cap < cheapest))
                {
                    cheapest = card.Cap;
                    found = true;
                }
            }
            return cheapest;
        }

        // ---- Moving ----

        // A legal step that gets strictly closer to the goal, stopping once within stopDistance.
        private bool TryStepToward(Match match, ApostleState me, HexCoord goal, int stopDistance, out HexCoord step)
        {
            step = default;
            int current = HexCoord.Distance(me.Position, goal);
            if (current <= stopDistance)
                return false;

            match.GetLegalDestinations(_destinations);
            int best = current;
            for (int i = 0; i < _destinations.Count; i++)
            {
                int distance = HexCoord.Distance(_destinations[i], goal);
                if (distance < best)
                {
                    best = distance;
                    step = _destinations[i];
                }
            }
            return best < current;
        }

        private static ApostleState FindById(Match match, int id)
        {
            if (id == 0)
                return null;
            IReadOnlyList<ApostleState> apostles = match.Battlefield.Apostles;
            for (int i = 0; i < apostles.Count; i++)
            {
                if (apostles[i].Id == id)
                    return apostles[i];
            }
            return null;
        }
    }
}