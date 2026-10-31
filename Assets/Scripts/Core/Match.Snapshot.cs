using System;
using System.Collections.Generic;

namespace SoulBind.Core
{
    // Online play: saving a match as a MatchSnapshot on the host, and loading one into a player's copy.
    public sealed partial class Match
    {
        /// <summary>
        /// What this viewer may know right now. Other Apostles' hands and the draw pile keep only their
        /// size (every card becomes MatchSnapshot.HiddenCard). viewerId 0 includes everything (tests only).
        /// </summary>
        public MatchSnapshot CaptureSnapshot(int viewerId, int sequence = 0)
        {
            var s = new MatchSnapshot { Sequence = sequence, ViewerId = viewerId };

            IReadOnlyList<ApostleState> apostles = Battlefield.Apostles;
            for (int i = 0; i < apostles.Count; i++)
            {
                ApostleState apostle = apostles[i];
                var a = new MatchSnapshot.Apostle
                {
                    Id = apostle.Id,
                    Position = apostle.Position,
                    Vessel = apostle.Vessel,
                    Souls = apostle.Souls,
                    Corruption = apostle.Corruption,
                    Shield = apostle.Shield,
                    TurnsStarted = apostle.TurnsStarted,
                };
                a.TimedEffects.AddRange(apostle.TimedEffects);

                bool visible = viewerId == 0 || viewerId == apostle.Id;
                IReadOnlyList<ArcanaId> hand = _hands[i].Cards;
                for (int c = 0; c < hand.Count; c++)
                    a.Hand.Add(visible ? hand[c] : MatchSnapshot.HiddenCard);
                s.Apostles.Add(a);
            }

            s.Seats.AddRange(Turns.Seats);
            for (int i = 0; i < Turns.SequenceCount; i++)
                s.TurnSequence.Add(Turns.GetSequenceEntry(i));
            s.SequenceIndex = Turns.SequenceIndex;
            s.StartSeat = Turns.StartSeat;
            s.RoundNumber = Turns.RoundNumber;
            s.Direction = Turns.Direction;
            s.AwaitingPhaseStart = Turns.IsAwaitingPhaseStart;

            IReadOnlyList<ArcanaId> drawPile = Deck.DrawPileForSnapshot;
            for (int i = 0; i < drawPile.Count; i++)
                s.DrawPile.Add(viewerId == 0 ? drawPile[i] : MatchSnapshot.HiddenCard);
            s.DiscardPile.AddRange(Deck.DiscardPile);

            foreach (KeyValuePair<HexCoord, SpecialTileType> pair in Battlefield.Specials.Active)
            {
                s.SpecialTiles.Add(pair.Key);
                s.SpecialTypes.Add(pair.Value);
            }
            s.AwakenedId = Ascension.AwakenedId;
            s.AscensionTile = Ascension.Tile;

            s.CurrentApostleId = CurrentApostle != null ? CurrentApostle.Id : 0;
            s.CapacityMaximum = CurrentCapacity != null ? CurrentCapacity.Maximum : 0;
            s.CapacityRemaining = CurrentCapacity != null ? CurrentCapacity.Remaining : 0;
            s.Engagement = Engagement;
            s.EngagedTargetId = EngagedTargetId;
            s.PendingAttack = PendingAttack;
            s.ExecuteBelowVessel = _executeBelowVessel;
            s.CorruptionReducersThisTurn = _corruptionReducersThisTurn;
            s.StrengthAwaitingPartner = _strengthAwaitingPartner;
            s.EmperorArmed = _emperorArmed;
            s.SkipRestOfRound = _skipRestOfRound;
            Array.Copy(_elementsPlayedThisTurn, s.ElementsPlayedThisTurn, _elementsPlayedThisTurn.Length);
            s.AttackCards.AddRange(_queuedCards);
            s.LastStrikeHits.AddRange(_lastStrikeHits);

            s.IsOver = IsOver;
            s.WinnerId = WinnerId;
            s.EndReason = EndReason;
            s.LastSwap = LastSwap.SeenBy(viewerId);
            return s;
        }

        /// <summary>
        /// A player's copy of a match, built from the first snapshot the host sends. Later snapshots are
        /// loaded into the same object with ApplySnapshot, so the screen can see what changed.
        /// A copy only shows the match: the host runs the rules, so its random source is never used.
        /// </summary>
        public static Match FromSnapshot(HexBoard board, MatchSnapshot snapshot)
        {
            if (board == null)
                throw new ArgumentNullException(nameof(board));
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            var battlefield = new Battlefield(board, new SpecialTiles());
            for (int i = 0; i < snapshot.Apostles.Count; i++)
            {
                MatchSnapshot.Apostle a = snapshot.Apostles[i];
                battlefield.AddApostleFromSnapshot(new ApostleState(a.Id, a.Position, 0));
            }

            var turns = new TurnTracker(snapshot.Seats);
            Deck deck = Deck.CreateStacked(Array.Empty<ArcanaId>(), new Random(0));
            var match = new Match(battlefield, turns, deck, new Random(0));
            match.ApplySnapshot(snapshot);
            return match;
        }

        /// <summary>Loads a snapshot of this same match (same Apostles, in the same order) into this copy.</summary>
        public void ApplySnapshot(MatchSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            IReadOnlyList<ApostleState> apostles = Battlefield.Apostles;
            if (snapshot.Apostles.Count != apostles.Count)
                throw new ArgumentException("The snapshot is of a different match.", nameof(snapshot));

            for (int i = 0; i < apostles.Count; i++)
            {
                MatchSnapshot.Apostle a = snapshot.Apostles[i];
                if (a.Id != apostles[i].Id)
                    throw new ArgumentException("The snapshot is of a different match.", nameof(snapshot));
                apostles[i].Restore(a.Position, a.Vessel, a.Souls, a.Corruption, a.Shield, a.TurnsStarted, a.TimedEffects);
                _hands[i].Restore(a.Hand);
            }

            Turns.Restore(snapshot.RoundNumber, snapshot.Direction, snapshot.AwaitingPhaseStart, snapshot.StartSeat,
                snapshot.TurnSequence, snapshot.SequenceIndex);
            Deck.Restore(snapshot.DrawPile, snapshot.DiscardPile);
            Battlefield.Specials.Restore(snapshot.SpecialTiles, snapshot.SpecialTypes);
            Ascension.Restore(snapshot.AwakenedId, snapshot.AscensionTile);

            CurrentApostle = snapshot.CurrentApostleId != 0 ? TryFindApostle(snapshot.CurrentApostleId) : null;
            if (CurrentApostle == null)
            {
                CurrentCapacity = null;
            }
            else
            {
                if (CurrentCapacity == null || CurrentCapacity.Maximum != snapshot.CapacityMaximum)
                    CurrentCapacity = new TurnCapacity(snapshot.CapacityMaximum);
                CurrentCapacity.RestoreRemaining(snapshot.CapacityRemaining);
            }

            Engagement = snapshot.Engagement;
            EngagedTargetId = snapshot.EngagedTargetId;
            PendingAttack = snapshot.PendingAttack;
            _executeBelowVessel = snapshot.ExecuteBelowVessel;
            _corruptionReducersThisTurn = snapshot.CorruptionReducersThisTurn;
            _strengthAwaitingPartner = snapshot.StrengthAwaitingPartner;
            _emperorArmed = snapshot.EmperorArmed;
            _skipRestOfRound = snapshot.SkipRestOfRound;
            _endTurnRequested = false;
            _nocturneVictimId = 0;
            Array.Copy(snapshot.ElementsPlayedThisTurn, _elementsPlayedThisTurn, _elementsPlayedThisTurn.Length);
            _attackCards.Clear();
            _queuedCards.Clear();
            _queuedCards.AddRange(snapshot.AttackCards);
            _queuedPicks.Clear();
            for (int i = 0; i < _queuedCards.Count; i++)
                _queuedPicks.Add(0);
            _lastStrikeHits.Clear();
            _lastStrikeHits.AddRange(snapshot.LastStrikeHits);

            IsOver = snapshot.IsOver;
            WinnerId = snapshot.WinnerId;
            EndReason = snapshot.EndReason;
            LastSwap = snapshot.LastSwap;
        }
    }
}