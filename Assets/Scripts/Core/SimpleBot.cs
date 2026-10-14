using System;
using System.Collections.Generic;

namespace SoulBind.Core
{
    /// <summary>
    /// A simple bot for testing: it walks toward the nearest opponent, attacks with its strongest
    /// affordable cards, adds more ATK while Engaged, then Strikes. With nothing in reach it plays
    /// cards without ATK for their DEF and effects, then ends its turn. The Awakened bot heads for the
    /// Ascension tile instead. Decide returns one action at a time, always one the rules allow,
    /// so the caller can show each step. Pure rules code: no Unity types, and nothing is allocated.
    /// </summary>
    public sealed class SimpleBot
    {
        private readonly List<HexCoord> _destinations = new List<HexCoord>(6);
        private readonly List<ApostleState> _targets = new List<ApostleState>(6);

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
                    // Pile on ATK while it can be paid for, then Strike. The Nocturne combo hits the engaged target.
                    if (TryPickCard(match, true, out ArcanaId attackCard))
                        return BotAction.PlayCard(attackCard, 0, 0);
                    return BotAction.Strike;

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

        private BotAction DecideWhileFree(Match match, ApostleState me)
        {
            AscensionTracker ascension = match.Ascension;
            bool awakened = ascension.HasAwakened && ascension.AwakenedId == me.Id;

            // 1. The Awakened bot walks to the Ascension tile.
            if (awakened && TryStepToward(match, me, ascension.Tile, 0, out HexCoord ascensionStep))
                return BotAction.Move(ascensionStep);

            // 2. Attack the weakest Apostle in reach with the strongest affordable card.
            match.GetAttackTargets(_targets);
            if (_targets.Count > 0 && TryPickCard(match, true, out ArcanaId attackCard))
                return BotAction.PlayCard(attackCard, Weakest(_targets).Id, 0);

            // 3. Walk toward the nearest opponent, keeping enough Capacity to attack when it gets there.
            ApostleState nearest = Nearest(match, me);
            if (!awakened && nearest != null && match.CurrentCapacity.Remaining > CheapestAttackCap(match)
                && TryStepToward(match, me, nearest.Position, 1, out HexCoord step))
                return BotAction.Move(step);

            // 4. Nothing to attack: play cards without ATK (none is wasted) for their DEF and effects.
            //    If one triggers the Nocturne combo, it hits the nearest opponent.
            if (TryPickCard(match, false, out ArcanaId otherCard))
                return BotAction.PlayCard(otherCard, 0, nearest != null ? nearest.Id : 0);

            return BotAction.EndTurn;
        }

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

        private static ApostleState Nearest(Match match, ApostleState me)
        {
            ApostleState nearest = null;
            int nearestDistance = int.MaxValue;
            IReadOnlyList<ApostleState> apostles = match.Battlefield.Apostles;
            for (int i = 0; i < apostles.Count; i++)
            {
                ApostleState other = apostles[i];
                if (other == me || other.IsEliminated)
                    continue;
                int distance = HexCoord.Distance(me.Position, other.Position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = other;
                }
            }
            return nearest;
        }

        private static ApostleState Weakest(List<ApostleState> targets)
        {
            ApostleState weakest = targets[0];
            for (int i = 1; i < targets.Count; i++)
            {
                if (targets[i].Vessel + targets[i].Defense < weakest.Vessel + weakest.Defense)
                    weakest = targets[i];
            }
            return weakest;
        }
    }
}