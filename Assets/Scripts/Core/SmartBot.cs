using System;
using System.Collections.Generic;

namespace SoulBind.Core
{
    /// <summary>
    /// A bot that thinks one play ahead. On a free turn it lists every play it could make (each step,
    /// each affordable card on each target, ending the turn), tries each one on a copy of the match,
    /// lets SimpleBot finish the rest of the turn on that copy, and scores where it ends up.
    /// The best score wins. While an attack is under way, or between Phases, it plays like SimpleBot.
    /// The copy sees the whole match (as the host does), but it only plays to the end of this turn,
    /// so nothing it learns about other hands or the draw pile is used.
    /// Pure rules code: no Unity types.
    /// </summary>
    public sealed class SmartBot : IBot
    {
        // ---------- How it scores a position ----------

        private const int WinScore = 1000000;     // winning or losing beats everything else
        private const int SoulValue = 40;         // per Soul of its own (up to 20)
        private const int AwakenedValue = 800;    // being the Awakened Apostle
        private const int AscensionStep = 120;    // per tile still between the Awakened bot and the Ascension tile
        private const int VesselValue = 3;        // per Vessel point of its own
        private const int DefenseValue = 2;       // per point of DEF it is holding
        private const int CorruptionCost = 25;    // per Corruption point
        private const int SoullockedCost = 400;   // Soullocked: it can no longer gain Souls
        private const int HandCardValue = 8;      // a card kept for later is worth something
        private const int FoeSoulCost = 30;       // per Soul an opponent holds
        private const int FoeVesselCost = 2;      // per Vessel point an opponent holds
        private const int FoeAwakenedCost = 1500; // an opponent who is Awakened
        private const int FoeGoneValue = 600;     // an opponent who is eliminated
        private const int ChaseStep = 4;          // per tile to the nearest opponent (keeps it in the fight)

        // One turn never needs this many actions. It stops a broken rollout from looping.
        private const int MaxRolloutActions = 120;

        private readonly SimpleBot _simple = new SimpleBot();
        private readonly List<BotAction> _candidates = new List<BotAction>(48);
        private readonly List<HexCoord> _destinations = new List<HexCoord>(6);
        private readonly List<ApostleState> _targets = new List<ApostleState>(6);
        private Match _scratch;    // the copy plays are tried on
        private Match _scratchFor; // the match that copy was made from

        public string Name => "SmartBot";

        public BotAction Decide(Match match)
        {
            if (match == null)
                throw new ArgumentNullException(nameof(match));

            BotAction simple = _simple.Decide(match);
            if (!CanLookAhead(match))
                return simple;

            int me = match.CurrentApostle.Id;
            BuildCandidates(match, simple);

            MatchSnapshot start = match.CaptureSnapshot(0);
            Match scratch = GetScratch(match, start);
            BotAction best = simple;
            int bestScore = int.MinValue;
            for (int i = 0; i < _candidates.Count; i++)
            {
                scratch.ApplySnapshot(start);
                if (!SimpleBot.Apply(scratch, _candidates[i]))
                    continue;
                FinishTurn(scratch, me);
                int score = Evaluate(scratch, me);
                // Strictly better only, so a tie keeps the earlier choice (SimpleBot's comes first).
                if (score > bestScore)
                {
                    bestScore = score;
                    best = _candidates[i];
                }
            }
            return best;
        }

        // Only a free turn with nothing queued is copied exactly by a snapshot, so only then it looks ahead.
        private static bool CanLookAhead(Match match) =>
            !match.IsOver
            && !match.IsAwaitingPhaseStart
            && match.CurrentApostle != null
            && match.Engagement == EngagementState.Free
            && match.AttackCards.Count == 0;

        // ---------- What it could do ----------

        private void BuildCandidates(Match match, BotAction simple)
        {
            _candidates.Clear();
            _candidates.Add(simple);
            _candidates.Add(BotAction.EndTurn);

            match.GetLegalDestinations(_destinations);
            for (int i = 0; i < _destinations.Count; i++)
                _candidates.Add(BotAction.Move(_destinations[i]));

            ApostleState me = match.CurrentApostle;
            match.GetAttackTargets(_targets);
            int remaining = match.CurrentCapacity.Remaining;
            IReadOnlyList<ArcanaId> hand = match.GetHand(me.Id);
            for (int i = 0; i < hand.Count; i++)
            {
                ArcanaId card = hand[i];
                if (IsEarlierInHand(hand, i) || CardCatalog.Get(card).Cap > remaining)
                    continue;

                // On each opponent in reach. A card without ATK is refused here and simply skipped.
                for (int t = 0; t < _targets.Count; t++)
                    _candidates.Add(BotAction.PlayCard(card, _targets[t].Id, 0));

                // Outside an attack, for its DEF and effect. Some cards need an opponent picked first.
                if (match.NeedsComboTarget(card, 0))
                    AddPerOpponent(match, card);
                else
                    _candidates.Add(BotAction.PlayCard(card, 0, 0));
            }
        }

        private void AddPerOpponent(Match match, ArcanaId card)
        {
            IReadOnlyList<ApostleState> apostles = match.Battlefield.Apostles;
            for (int i = 0; i < apostles.Count; i++)
            {
                ApostleState other = apostles[i];
                if (other != match.CurrentApostle && !other.IsEliminated)
                    _candidates.Add(BotAction.PlayCard(card, 0, other.Id));
            }
        }

        // Two copies of a card play the same, so only the first is tried.
        private static bool IsEarlierInHand(IReadOnlyList<ArcanaId> hand, int index)
        {
            for (int i = 0; i < index; i++)
            {
                if (hand[i] == hand[index])
                    return true;
            }
            return false;
        }

        // ---------- Trying a play ----------

        private Match GetScratch(Match match, MatchSnapshot start)
        {
            if (_scratch == null || _scratchFor != match)
            {
                _scratch = Match.FromSnapshot(match.Battlefield.Board, start);
                _scratchFor = match;
            }
            return _scratch;
        }

        // SimpleBot plays the rest of this Apostle's turn on the copy.
        private void FinishTurn(Match scratch, int me)
        {
            for (int i = 0; i < MaxRolloutActions; i++)
            {
                if (scratch.IsOver || scratch.IsAwaitingPhaseStart)
                    return;
                ApostleState current = scratch.CurrentApostle;
                if (current == null || current.Id != me)
                    return;
                if (!SimpleBot.Apply(scratch, _simple.Decide(scratch)))
                    scratch.EndTurn();
            }
        }

        // How good this position is for Apostle me. Higher is better.
        private static int Evaluate(Match match, int me)
        {
            if (match.IsOver)
                return match.WinnerId == me ? WinScore : -WinScore;
            ApostleState self = Find(match, me);
            if (self == null || self.IsEliminated)
                return -WinScore;

            int score = Math.Min(self.Souls, GameConstants.SoulsToAwaken) * SoulValue;
            score += self.Vessel * VesselValue + self.Defense * DefenseValue;
            score -= self.Corruption * CorruptionCost;
            score += match.GetHand(me).Count * HandCardValue;
            if (self.IsSoullocked)
                score -= SoullockedCost;

            AscensionTracker ascension = match.Ascension;
            bool iAmAwakened = ascension.HasAwakened && ascension.AwakenedId == me;
            if (iAmAwakened)
                score += AwakenedValue - HexCoord.Distance(self.Position, ascension.Tile) * AscensionStep;

            int nearest = -1; // -1 = no opponent left
            IReadOnlyList<ApostleState> apostles = match.Battlefield.Apostles;
            for (int i = 0; i < apostles.Count; i++)
            {
                ApostleState other = apostles[i];
                if (other.Id == me)
                    continue;
                if (other.IsEliminated)
                {
                    score += FoeGoneValue;
                    continue;
                }
                score -= other.Souls * FoeSoulCost + other.Vessel * FoeVesselCost;
                if (ascension.HasAwakened && ascension.AwakenedId == other.Id)
                    score -= FoeAwakenedCost;
                int distance = HexCoord.Distance(self.Position, other.Position);
                if (nearest < 0 || distance < nearest)
                    nearest = distance;
            }
            if (!iAmAwakened && nearest > 0)
                score -= nearest * ChaseStep;
            return score;
        }

        private static ApostleState Find(Match match, int id)
        {
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
