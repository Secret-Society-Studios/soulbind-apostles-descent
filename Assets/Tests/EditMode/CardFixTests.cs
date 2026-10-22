using System;
using System.Collections.Generic;
using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests.EditMode
{
    /// <summary>
    /// The Hierophant: +2 ATK and +2 DEF right away, until the player's next turn.
    /// Wheel of Fortune: swaps a random card with the attack target, also when it joins an attack.
    /// </summary>
    public class CardFixTests
    {
        private static Match MatchWithHand(params ArcanaId[] hand)
        {
            var stack = new List<ArcanaId>();
            foreach (ArcanaId card in hand)
            {
                stack.Add(card);
                stack.Add(card);
            }
            for (int i = 0; i < 20; i++)
                stack.Add(ArcanaId.Fool);
            HexBoard board = StandardBoard.Create();
            return Match.Create(2, board, SpawnPlanner.PickSpread(board, 2), new Random(1),
                Deck.CreateStacked(stack, new Random(2)));
        }

        private static ApostleState OpponentNextToCurrent(Match match)
        {
            IReadOnlyList<ApostleState> apostles = match.Battlefield.Apostles;
            ApostleState opponent = apostles[0] == match.CurrentApostle ? apostles[1] : apostles[0];
            IReadOnlyList<HexCoord> neighbors = match.Battlefield.Board.GetNeighborsOnBoard(match.CurrentApostle.Position);
            for (int i = 0; i < neighbors.Count; i++)
            {
                if (!match.Battlefield.IsOccupied(neighbors[i]))
                {
                    opponent.MoveTo(neighbors[i]);
                    return opponent;
                }
            }
            throw new InvalidOperationException("No free tile next to the current Apostle.");
        }

        [Test]
        public void Hierophant_CanStartAnAttack_WithItsTwoAtk()
        {
            Match match = MatchWithHand(ArcanaId.Hierophant, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState target = OpponentNextToCurrent(match);

            Assert.AreEqual(2, CardAttack.Of(ArcanaId.Hierophant));
            Assert.AreEqual(CardPlayResult.Success, match.PlayCard(ArcanaId.Hierophant, target.Id));

            Assert.AreEqual(EngagementState.Engaged, match.Engagement);
            Assert.AreEqual(2, match.PendingAttack, "The +2 ATK is in the attack right away");
            Assert.AreEqual(2, match.CurrentApostle.Defense);
            match.Strike(out AttackOutcome outcome);
            Assert.AreEqual(2, outcome.Damage, "Counted once, not twice");
        }

        [Test]
        public void Hierophant_PlayedFirst_ItsAtkShowsInTheNextAttackThisTurn()
        {
            Match match = MatchWithHand(ArcanaId.Hierophant, ArcanaId.Magician, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState target = OpponentNextToCurrent(match);
            Assert.AreEqual(CardPlayResult.Success, match.PlayCard(ArcanaId.Hierophant, 0));

            Assert.AreEqual(CardPlayResult.Success, match.PlayCard(ArcanaId.Magician, target.Id));

            Assert.AreEqual(5, match.PendingAttack, "Magician 2 + 1, plus the Hierophant's 2");
            match.Strike(out AttackOutcome outcome);
            Assert.AreEqual(5, outcome.Damage);
        }

        [Test]
        public void Hierophant_JoiningAnAttack_AddsTwoAtk()
        {
            Match match = MatchWithHand(ArcanaId.Magician, ArcanaId.Hierophant, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            ApostleState target = OpponentNextToCurrent(match);
            Assert.AreEqual(CardPlayResult.Success, match.PlayCard(ArcanaId.Magician, target.Id));

            Assert.AreEqual(CardPlayResult.Success, match.PlayCard(ArcanaId.Hierophant, 0));

            Assert.AreEqual(5, match.PendingAttack, "Magician 2 + 1, plus 2");
        }
    
        private static Match Deal(ArcanaId[] first, ArcanaId[] second)
        {
            var stack = new List<ArcanaId>();
            for (int card = 0; card < GameConstants.StartingHandSize; card++)
            {
                stack.Add(first[card]);
                stack.Add(second[card]);
            }
            for (int i = 0; i < 20; i++)
                stack.Add(ArcanaId.Fool);
            HexBoard board = StandardBoard.Create();
            return Match.Create(2, board, SpawnPlanner.PickSpread(board, 2), new Random(1),
                Deck.CreateStacked(stack, new Random(2)));
        }

        [Test]
        public void WheelOfFortune_JoiningAnAttack_SwapsWithTheTarget()
        {
            ArcanaId[] attacker = { ArcanaId.Magician, ArcanaId.WheelOfFortune, ArcanaId.Sun, ArcanaId.Sun, ArcanaId.Sun };
            ArcanaId[] defender = { ArcanaId.Moon, ArcanaId.Moon, ArcanaId.Moon, ArcanaId.Moon, ArcanaId.Moon };
            // Deal so the Apostle who plays first gets the attacker's hand (the same seed picks the same first player).
            Match match = Deal(attacker, defender);
            if (match.CurrentApostle != match.Battlefield.Apostles[0])
                match = Deal(defender, attacker);
            ApostleState player = match.CurrentApostle;
            ApostleState target = OpponentNextToCurrent(match);

            Assert.AreEqual(CardPlayResult.Success, match.PlayCard(ArcanaId.Magician, target.Id));
            Assert.AreEqual(CardPlayResult.Success, match.PlayCard(ArcanaId.WheelOfFortune, 0));

            int moons = 0;
            foreach (ArcanaId card in match.GetHand(player.Id))
            {
                if (card == ArcanaId.Moon)
                    moons++;
            }
            Assert.AreEqual(1, moons, "The player received one of the target's cards");
        }
    
        private static int CountOf(Match match, int apostleId, ArcanaId card)
        {
            int count = 0;
            foreach (ArcanaId held in match.GetHand(apostleId))
            {
                if (held == card)
                    count++;
            }
            return count;
        }

        [Test]
        public void WheelOfFortune_SwapsWithAPickedOpponent_AnywhereOnTheBoard()
        {
            ArcanaId[] attacker = { ArcanaId.WheelOfFortune, ArcanaId.Sun, ArcanaId.Sun, ArcanaId.Sun, ArcanaId.Sun };
            ArcanaId[] defender = { ArcanaId.Moon, ArcanaId.Moon, ArcanaId.Moon, ArcanaId.Moon, ArcanaId.Moon };
            Match match = Deal(attacker, defender);
            if (match.CurrentApostle != match.Battlefield.Apostles[0])
                match = Deal(defender, attacker);
            ApostleState player = match.CurrentApostle;
            ApostleState other = match.Battlefield.Apostles[0] == player ? match.Battlefield.Apostles[1] : match.Battlefield.Apostles[0];
            Assume.That(HexCoord.AreAdjacent(player.Position, other.Position), Is.False, "The two start apart");

            Assert.IsTrue(match.NeedsComboTarget(ArcanaId.WheelOfFortune, 0), "Outside an attack, the player picks who");
            Assert.AreEqual(CardPlayResult.ComboTargetRequired, match.PlayCard(ArcanaId.WheelOfFortune, 0));
            Assert.AreEqual(CardPlayResult.Success, match.PlayCard(ArcanaId.WheelOfFortune, 0, other.Id));

            Assert.AreEqual(1, CountOf(match, player.Id, ArcanaId.Moon), "The player received one of their cards");
            Assert.AreEqual(1, CountOf(match, other.Id, ArcanaId.Sun), "They received one of the player's");
            Assert.AreEqual(other.Id, match.LastSwapTargetId);
        }

        [Test]
        public void WheelOfFortune_CannotPickItsOwnPlayer()
        {
            Match match = MatchWithHand(ArcanaId.WheelOfFortune, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            Assert.AreEqual(CardPlayResult.InvalidComboTarget,
                match.PlayCard(ArcanaId.WheelOfFortune, 0, match.CurrentApostle.Id));
        }
    
        [Test]
        public void ElementsPlayedThisTurn_CountsUpAndStartsOverNextTurn()
        {
            Match match = MatchWithHand(ArcanaId.Magician, ArcanaId.Empress, ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Fool);
            Assert.AreEqual(0, match.GetElementsPlayedThisTurn(SoulBind.Core.Element.Radiance));

            Assert.AreEqual(CardPlayResult.Success, match.PlayCard(ArcanaId.Magician, 0));
            Assert.AreEqual(1, match.GetElementsPlayedThisTurn(SoulBind.Core.Element.Radiance));
            Assert.IsFalse(match.IsComboTriggered(SoulBind.Core.Element.Radiance));

            Assert.AreEqual(CardPlayResult.Success, match.PlayCard(ArcanaId.Empress, 0));
            Assert.AreEqual(2, match.GetElementsPlayedThisTurn(SoulBind.Core.Element.Radiance));
            Assert.IsTrue(match.IsComboTriggered(SoulBind.Core.Element.Radiance));

            match.EndTurn();
            Assert.AreEqual(0, match.GetElementsPlayedThisTurn(SoulBind.Core.Element.Radiance), "A new turn starts at 0");
        }
    
        [Test]
        public void World_EveryoneElseLosesTheirTurn_ThePlayerGoesAgain()
        {
            // Three Apostles, all with The World. The second to play uses it.
            var stack = new List<ArcanaId>();
            for (int i = 0; i < 3; i++)
                stack.Add(ArcanaId.World);
            for (int i = 0; i < 12 + 30; i++)
                stack.Add(ArcanaId.Fool);
            HexBoard board = StandardBoard.Create();
            Match match = Match.Create(3, board, SpawnPlanner.PickSpread(board, 3), new Random(1),
                Deck.CreateStacked(stack, new Random(2)));
            ApostleState first = match.CurrentApostle;
            match.EndTurn();
            ApostleState player = match.CurrentApostle;
            int firstHand = match.GetHand(first.Id).Count;
            Assert.AreEqual(CardPlayResult.Success, match.PlayCard(ArcanaId.World, 0));

            match.EndTurn();

            Assert.AreSame(player, match.CurrentApostle, "The player goes again straight away");
            Assert.AreEqual(2, match.Turns.RoundNumber);
            Assert.AreEqual(firstHand, match.GetHand(first.Id).Count, "The first Apostle lost its round-2 turn: no draw");
        }
    }
}