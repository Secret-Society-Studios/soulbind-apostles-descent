using System;
using System.Collections.Generic;
using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests.EditMode
{
    public class TutorialMatchTests
    {
        private static TutorialMatch.Setup Make() => TutorialMatch.Create(StandardBoard.Create(), new Random(1));

        private static ApostleState Get(Match match, int id)
        {
            foreach (ApostleState apostle in match.Battlefield.Apostles)
            {
                if (apostle.Id == id)
                    return apostle;
            }
            throw new InvalidOperationException($"No Apostle with id {id}.");
        }

        // Walks the current Apostle along the shortest path, one Move per tile.
        private static void WalkTo(Match match, HexCoord tile)
        {
            var path = new List<HexCoord>();
            Assert.IsTrue(match.TryGetPath(tile, path), $"{tile} is out of reach.");
            foreach (HexCoord step in path)
                Assert.AreEqual(MoveResult.Success, match.Move(step));
        }

        // Ends your turn and the bot's, so it is your turn again.
        private static void PassRound(Match match)
        {
            match.EndTurn();
            match.EndTurn();
            Assert.AreEqual(TutorialMatch.YouId, match.CurrentApostle.Id);
        }

        [Test]
        public void Start_YourTurn_SoulTileInReach_EnemyNotNextToYou()
        {
            TutorialMatch.Setup setup = Make();
            Match match = setup.Match;
            var reach = new List<HexCoord>();
            var targets = new List<ApostleState>();
            match.GetReachableTiles(reach);
            match.GetAttackTargets(targets);

            Assert.AreEqual(TutorialMatch.YouId, match.CurrentApostle.Id);
            CollectionAssert.Contains(reach, setup.SoulTile);
            Assert.AreEqual(0, targets.Count);
            Assert.AreEqual(TutorialMatch.StartingSouls, Get(match, TutorialMatch.YouId).Souls);
        }

        [Test]
        public void WholeScript_EndsAwakened()
        {
            TutorialMatch.Setup setup = Make();
            Match match = setup.Match;
            ApostleState you = Get(match, TutorialMatch.YouId);

            // Turn 1: walk to the Soul tile.
            WalkTo(match, setup.SoulTile);
            Assert.AreEqual(GameConstants.SoulsToAwaken - 1, you.Souls);
            PassRound(match);

            // Turn 2: the Radiance combo, then PLAY.
            Assert.AreEqual(CardPlayResult.Success, match.PlayCard(ArcanaId.Magician, 0));
            Assert.AreEqual(CardPlayResult.Success, match.PlayCard(ArcanaId.Lovers, 0));
            Assert.IsTrue(match.IsComboTriggered(SoulBind.Core.Element.Radiance));
            Assert.IsTrue(match.PlayQueued());
            PassRound(match);

            // Turn 3: one step next to the enemy, The Chariot and The Magician, Strike.
            HexCoord beside = FindTileBeside(match, you.Position, setup.EnemyTile);
            WalkTo(match, beside);
            Assert.AreEqual(CardPlayResult.Success, match.PlayCard(ArcanaId.Chariot, TutorialMatch.EnemyId));
            Assert.AreEqual(CardPlayResult.Success, match.PlayCard(ArcanaId.Magician, 0));
            Assert.IsTrue(match.Strike(out _));

            Assert.AreEqual(GameConstants.SoulsToAwaken, you.Souls);
            Assert.IsTrue(match.Ascension.HasAwakened);
            Assert.AreEqual(TutorialMatch.YouId, match.Ascension.AwakenedId);
        }

        // A free tile next to both, so one step puts you beside the enemy.
        private static HexCoord FindTileBeside(Match match, HexCoord from, HexCoord enemy)
        {
            foreach (HexCoord tile in match.Battlefield.Board.GetNeighborsOnBoard(from))
            {
                if (HexCoord.AreAdjacent(tile, enemy) && !match.Battlefield.IsOccupied(tile))
                    return tile;
            }
            Assert.Fail("No tile next to both you and the enemy.");
            return from;
        }
    }
}
