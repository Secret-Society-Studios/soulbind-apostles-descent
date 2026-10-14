using System;
using System.Collections.Generic;
using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests.EditMode
{
    /// <summary>A Strike hits every adjacent Apostle, dividing the total ATK between them.</summary>
    public class MultiTargetStrikeTests
    {
        // 3 Apostles, all with the same hand. Magician A2 D1 C1 (+1 when attacking), Death A7 D4 C5,
        // Emperor A6 D3 C7, Devil A8 D3 C6.
        private static Match NewMatch()
        {
            ArcanaId[] hand = { ArcanaId.Death, ArcanaId.Magician, ArcanaId.Emperor, ArcanaId.Devil, ArcanaId.Fool };
            var stack = new List<ArcanaId>();
            foreach (ArcanaId card in hand)
            {
                for (int p = 0; p < 3; p++)
                    stack.Add(card);
            }
            for (int i = 0; i < 20; i++)
                stack.Add(ArcanaId.Fool);

            HexBoard board = StandardBoard.Create();
            return Match.Create(3, board, SpawnPlanner.PickSpread(board, 3), new Random(1),
                Deck.CreateStacked(stack, new Random(2)));
        }

        // Moves both opponents next to the current Apostle. 'primary' has the lower Id.
        private static void BothAdjacent(Match match, out ApostleState primary, out ApostleState other)
        {
            primary = null;
            other = null;
            foreach (ApostleState apostle in match.Battlefield.Apostles)
            {
                if (apostle == match.CurrentApostle)
                    continue;
                if (primary == null)
                    primary = apostle;
                else
                    other = apostle;
            }
            MoveNextToCurrent(match, primary);
            MoveNextToCurrent(match, other);
        }

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

        private static void Play(Match match, ArcanaId card, int targetId = 0)
        {
            Assert.AreEqual(CardPlayResult.Success, match.PlayCard(card, targetId), $"Playing {card}");
        }

        // ---- Dividing the ATK ----

        [TestCase(10, 1, 0, 10)]
        [TestCase(10, 2, 0, 5)]
        [TestCase(10, 2, 1, 5)]
        [TestCase(10, 3, 0, 4)]
        [TestCase(10, 3, 1, 3)]
        [TestCase(10, 3, 2, 3)]
        [TestCase(2, 3, 2, 0)]
        public void GetShare_DividesEvenly_LeftoverToTheFirstTargets(int total, int targets, int index, int expected)
        {
            Assert.AreEqual(expected, Combat.GetShare(total, targets, index));
        }

        // ---- Strikes on two adjacent Apostles ----

        [Test]
        public void Strike_TwoAdjacent_DividesTheAtk()
        {
            Match match = NewMatch();
            BothAdjacent(match, out ApostleState primary, out ApostleState other);
            Play(match, ArcanaId.Death, primary.Id); // 7
            Play(match, ArcanaId.Magician);          // + 3 = 10

            match.Strike(out AttackOutcome outcome);

            Assert.AreEqual(45, primary.Vessel);
            Assert.AreEqual(45, other.Vessel);
            Assert.AreEqual(10, outcome.Damage);
        }

        [Test]
        public void Strike_EachTargetsOwnDefReducesItsShare()
        {
            Match match = NewMatch();
            BothAdjacent(match, out ApostleState primary, out ApostleState other);
            other.AddShield(3);
            Play(match, ArcanaId.Death, primary.Id);
            Play(match, ArcanaId.Magician);

            match.Strike(out _);

            Assert.AreEqual(45, primary.Vessel);
            Assert.AreEqual(48, other.Vessel); // 5 - 3
        }

        [Test]
        public void Strike_LeftoverAtkGoesToThePrimaryTarget()
        {
            Match match = NewMatch();
            BothAdjacent(match, out ApostleState primary, out ApostleState other);
            Play(match, ArcanaId.Magician, primary.Id); // 3 ATK

            match.Strike(out _);

            Assert.AreEqual(48, primary.Vessel); // 2
            Assert.AreEqual(49, other.Vessel);   // 1
        }

        [Test]
        public void Strike_StealsFromEachBrokenTarget_ButCorruptionOnlyOnce()
        {
            Match match = NewMatch();
            ApostleState player = match.CurrentApostle;
            BothAdjacent(match, out ApostleState primary, out ApostleState other);
            int souls = player.Souls;
            Play(match, ArcanaId.Death, primary.Id);
            Play(match, ArcanaId.Magician);

            match.Strike(out AttackOutcome outcome);

            Assert.AreEqual(souls + 2, player.Souls);
            Assert.AreEqual(GameConstants.AttackCorruption, player.Corruption);
            Assert.AreEqual(2, outcome.SoulsStolen);
            Assert.AreEqual(GameConstants.AttackCorruption, outcome.CorruptionGained);
        }

        [Test]
        public void Strike_NoDefenseBroken_NoCorruption()
        {
            Match match = NewMatch();
            ApostleState player = match.CurrentApostle;
            BothAdjacent(match, out ApostleState primary, out ApostleState other);
            primary.AddShield(10);
            other.AddShield(10);
            Play(match, ArcanaId.Death, primary.Id);
            Play(match, ArcanaId.Magician);

            match.Strike(out AttackOutcome outcome);

            Assert.AreEqual(0, outcome.Damage);
            Assert.AreEqual(0, player.Corruption);
            Assert.AreEqual(50, primary.Vessel);
            Assert.AreEqual(50, other.Vessel);
        }

        [Test]
        public void Strike_OneAdjacent_GetsTheWholeAttack()
        {
            Match match = NewMatch();
            ApostleState primary = null;
            ApostleState far = null;
            foreach (ApostleState apostle in match.Battlefield.Apostles)
            {
                if (apostle == match.CurrentApostle)
                    continue;
                if (primary == null)
                    primary = apostle;
                else
                    far = apostle;
            }
            MoveNextToCurrent(match, primary);
            Play(match, ArcanaId.Death, primary.Id);
            Play(match, ArcanaId.Magician);

            match.Strike(out _);

            Assert.AreEqual(40, primary.Vessel);
            Assert.AreEqual(50, far.Vessel, "Not adjacent, not hit");
        }

        [Test]
        public void LastStrikeHits_ListsEachTarget_PrimaryFirst()
        {
            Match match = NewMatch();
            BothAdjacent(match, out ApostleState primary, out ApostleState other);
            // Engage the higher-Id Apostle, so the primary is not simply the first in Id order.
            Play(match, ArcanaId.Magician, other.Id); // 3 ATK: 2 to the primary, 1 to the other

            match.Strike(out _);

            IReadOnlyList<StrikeHit> hits = match.LastStrikeHits;
            Assert.AreEqual(2, hits.Count);
            Assert.AreEqual(other.Id, hits[0].TargetId);
            Assert.AreEqual(2, hits[0].Attack);
            Assert.AreEqual(primary.Id, hits[1].TargetId);
            Assert.AreEqual(1, hits[1].Attack);
        }

        // ---- Card effects with several targets ----

        [Test]
        public void Emperor_DoublesOnlyTheSharesOfMoreCorruptedTargets()
        {
            Match match = NewMatch();
            BothAdjacent(match, out ApostleState primary, out ApostleState other);
            primary.AddCorruption(3);
            Play(match, ArcanaId.Emperor, primary.Id); // 6 ATK: 3 each

            match.Strike(out _);

            Assert.AreEqual(44, primary.Vessel, "3 doubled to 6");
            Assert.AreEqual(47, other.Vessel, "Not more Corrupted than the player");
        }

        [Test]
        public void Death_ExecutesEachLowVesselTargetWhoseDefenseBreaks()
        {
            Match match = NewMatch();
            BothAdjacent(match, out ApostleState primary, out ApostleState other);
            primary.TakeDamage(46);
            other.TakeDamage(46);
            primary.AddShield(2);
            other.AddShield(2);
            Play(match, ArcanaId.Death, primary.Id); // 7 ATK: 4 and 3, so 2 and 1 damage

            match.Strike(out _);

            Assert.IsTrue(primary.IsEliminated);
            Assert.IsTrue(other.IsEliminated);
        }

        [Test]
        public void Devil_OnlyAffectsThePrimaryTarget()
        {
            Match match = NewMatch();
            ApostleState player = match.CurrentApostle;
            BothAdjacent(match, out ApostleState primary, out ApostleState other);

            Play(match, ArcanaId.Devil, primary.Id);

            Assert.AreEqual(3, primary.Corruption);
            Assert.AreEqual(0, other.Corruption);
            Assert.AreEqual(1, player.Corruption);
        }

        [Test]
        public void GetAttackTargets_WhileEngaged_ListsEveryoneAdjacent()
        {
            Match match = NewMatch();
            BothAdjacent(match, out ApostleState primary, out _);
            Play(match, ArcanaId.Magician, primary.Id);

            var targets = new List<ApostleState>();
            match.GetAttackTargets(targets);

            Assert.AreEqual(2, targets.Count);
        }
    }
}