using System;
using System.Collections.Generic;
using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests.EditMode
{
    /// <summary>Stage C5e: element combos (two cards of the same element in one turn).</summary>
    public class ElementComboTests
    {
        // Every Apostle gets the same 5-card hand. The draw pile after dealing is all Fools.
        private static Match MatchWithHand(int players, params ArcanaId[] hand)
        {
            Assert.AreEqual(GameConstants.StartingHandSize, hand.Length, "Give exactly 5 cards");

            var stack = new List<ArcanaId>();
            foreach (ArcanaId card in hand)
            {
                for (int p = 0; p < players; p++)
                    stack.Add(card);
            }
            for (int i = 0; i < 20; i++)
                stack.Add(ArcanaId.Fool);

            HexBoard board = StandardBoard.Create();
            return Match.Create(players, board, SpawnPlanner.PickSpread(board, players), new Random(1),
                Deck.CreateStacked(stack, new Random(2)));
        }

        // Moves this Apostle next to the current Apostle.
        private static void MoveNextToCurrent(Match match, ApostleState mover)
        {
            IReadOnlyList<HexCoord> neighbors = match.Battlefield.Board.GetNeighborsOnBoard(match.CurrentApostle.Position);
            for (int i = 0; i < neighbors.Count; i++)
            {
                if (!match.Battlefield.IsOccupied(neighbors[i]))
                {
                    mover.MoveTo(neighbors[i]);
                    return;
                }
            }
            throw new InvalidOperationException("No free tile next to the current Apostle.");
        }

        private static ApostleState FirstOpponent(Match match)
        {
            foreach (ApostleState apostle in match.Battlefield.Apostles)
            {
                if (apostle != match.CurrentApostle)
                    return apostle;
            }
            throw new InvalidOperationException("No opponent.");
        }

        private static void Play(Match match, ArcanaId card, int targetId = 0, int comboTargetId = 0)
        {
            Assert.AreEqual(CardPlayResult.Success, match.PlayCard(card, targetId, comboTargetId), $"Playing {card}");
        }

        // ---- Radiance: +2 DEF ----

        [Test]
        public void Radiance_TwoCards_AddTwoShield()
        {
            Match match = MatchWithHand(2, ArcanaId.Magician, ArcanaId.Empress, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState player = match.CurrentApostle;

            Play(match, ArcanaId.Magician); // 1 DEF
            Assert.IsFalse(match.IsComboTriggered(Element.Radiance));
            Play(match, ArcanaId.Empress);  // 3 DEF + combo 2

            Assert.IsTrue(match.IsComboTriggered(Element.Radiance));
            Assert.AreEqual(6, player.Shield);
        }

        [Test]
        public void Radiance_ThirdCard_DoesNotTriggerAgain()
        {
            Match match = MatchWithHand(2, ArcanaId.Magician, ArcanaId.Empress, ArcanaId.Star, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState player = match.CurrentApostle;

            Play(match, ArcanaId.Magician); // 1
            Play(match, ArcanaId.Empress);  // 3 + 2
            Play(match, ArcanaId.Star);     // 6, no second combo

            Assert.AreEqual(12, player.Shield);
        }

        // ---- Celeste: -1 Corruption ----

        [Test]
        public void Celeste_TwoCards_ReduceOneMoreCorruption()
        {
            Match match = MatchWithHand(2, ArcanaId.Hierophant, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState player = match.CurrentApostle;
            player.AddCorruption(3);

            Play(match, ArcanaId.Hierophant);
            Play(match, ArcanaId.Fool); // the Fool's 1 + the combo's 1

            Assert.AreEqual(1, player.Corruption);
        }

        // ---- Crimson: +2 ATK ----

        [Test]
        public void Crimson_WhileAttacking_AddsTwoAtk()
        {
            Match match = MatchWithHand(2, ArcanaId.Chariot, ArcanaId.Strength, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState target = FirstOpponent(match);
            MoveNextToCurrent(match, target);

            Play(match, ArcanaId.Chariot, target.Id); // 5 + 3
            Play(match, ArcanaId.Strength);           // 0 ATK + combo 2

            Assert.AreEqual(10, match.PendingAttack);
        }

        [Test]
        public void Crimson_NotAttacking_TheBonusIsLost()
        {
            Match match = MatchWithHand(2, ArcanaId.Strength, ArcanaId.Strength, ArcanaId.Magician, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState target = FirstOpponent(match);
            MoveNextToCurrent(match, target);
            Play(match, ArcanaId.Strength);
            Play(match, ArcanaId.Strength); // combo triggers with no attack in progress

            Play(match, ArcanaId.Magician, target.Id);

            Assert.AreEqual(3, match.PendingAttack, "Only the Magician's 2 + 1");
        }

        // ---- Nocturne: +1 Corruption to an opponent ----

        [Test]
        public void Nocturne_WhileAttacking_TheTargetGainsOne()
        {
            Match match = MatchWithHand(2, ArcanaId.Death, ArcanaId.HighPriestess, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState target = FirstOpponent(match);
            MoveNextToCurrent(match, target);

            Play(match, ArcanaId.Death, target.Id);
            Play(match, ArcanaId.HighPriestess);
            Assert.AreEqual(0, target.Corruption, "The combo waits for Strike");
            match.Strike(out _);

            Assert.AreEqual(1, target.Corruption);
        }

        // 3 players: returns the two opponents of the current Apostle, with 'near' moved next to it.
        private static void TwoOpponents(Match match, out ApostleState near, out ApostleState far)
        {
            near = null;
            far = null;
            foreach (ApostleState apostle in match.Battlefield.Apostles)
            {
                if (apostle == match.CurrentApostle)
                    continue;
                if (near == null)
                    near = apostle;
                else
                    far = apostle;
            }
            MoveNextToCurrent(match, near);
        }

        [Test]
        public void Nocturne_NotAttacking_WithoutAPick_IsRejected()
        {
            Match match = MatchWithHand(2, ArcanaId.HighPriestess, ArcanaId.Hermit, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState player = match.CurrentApostle;
            Play(match, ArcanaId.HighPriestess); // hand 5 - 1 + 1 drawn
            int remaining = match.CurrentCapacity.Remaining;

            Assert.AreEqual(CardPlayResult.ComboTargetRequired, match.PlayCard(ArcanaId.Hermit, 0));
            Assert.AreEqual(5, match.GetHand(player.Id).Count, "Nothing changed");
            Assert.AreEqual(remaining, match.CurrentCapacity.Remaining);
            Assert.IsFalse(match.IsComboTriggered(Element.Nocturne));
        }

        [Test]
        public void Nocturne_PickedOpponent_GainsOne_AnywhereOnTheBoard()
        {
            Match match = MatchWithHand(3, ArcanaId.HighPriestess, ArcanaId.Hermit, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            TwoOpponents(match, out ApostleState near, out ApostleState far);

            Play(match, ArcanaId.HighPriestess);
            Play(match, ArcanaId.Hermit, 0, far.Id);

            Assert.AreEqual(1, far.Corruption, "Not adjacent, still hit");
            Assert.AreEqual(0, near.Corruption);
        }

        [Test]
        public void Nocturne_WhileAttacking_CanPickAnotherOpponent()
        {
            Match match = MatchWithHand(3, ArcanaId.Death, ArcanaId.HighPriestess, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            TwoOpponents(match, out ApostleState near, out ApostleState far);

            Play(match, ArcanaId.Death, near.Id);
            Play(match, ArcanaId.HighPriestess, 0, far.Id);
            match.Strike(out _);

            Assert.AreEqual(1, far.Corruption);
            Assert.AreEqual(0, near.Corruption);
        }

        [Test]
        public void Nocturne_InvalidPick_IsRejected()
        {
            Match match = MatchWithHand(3, ArcanaId.HighPriestess, ArcanaId.Hermit, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState player = match.CurrentApostle;
            TwoOpponents(match, out _, out ApostleState far);
            far.TakeDamage(GameConstants.StartingVessel);
            Play(match, ArcanaId.HighPriestess);

            Assert.AreEqual(CardPlayResult.InvalidComboTarget, match.PlayCard(ArcanaId.Hermit, 0, player.Id), "Self");
            Assert.AreEqual(CardPlayResult.InvalidComboTarget, match.PlayCard(ArcanaId.Hermit, 0, 99), "Unknown");
            Assert.AreEqual(CardPlayResult.InvalidComboTarget, match.PlayCard(ArcanaId.Hermit, 0, far.Id), "Eliminated");
        }

        // ---- General ----

        [Test]
        public void Combo_CountsResetEachTurn()
        {
            Match match = MatchWithHand(2, ArcanaId.Magician, ArcanaId.Empress, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState player = match.CurrentApostle;
            Play(match, ArcanaId.Magician);

            match.EndTurn();
            match.EndTurn();
            Assert.AreSame(player, match.CurrentApostle);
            Play(match, ArcanaId.Empress);

            Assert.IsFalse(match.IsComboTriggered(Element.Radiance));
            Assert.AreEqual(3, player.Shield, "Only the Empress's own DEF");
        }

        [Test]
        public void Lovers_PairAlsoTriggersTheRadianceCombo()
        {
            Match match = MatchWithHand(2, ArcanaId.Lovers, ArcanaId.Lovers, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState player = match.CurrentApostle;
            ApostleState target = FirstOpponent(match);
            MoveNextToCurrent(match, target);

            Play(match, ArcanaId.Lovers, target.Id);
            Play(match, ArcanaId.Lovers);

            Assert.AreEqual(10, match.PendingAttack, "4 + 4 + Lovers pair 2");
            match.Strike(out _);
            Assert.AreEqual(10, player.Shield, "4 + 4 + Radiance combo 2");
        }
    }
}