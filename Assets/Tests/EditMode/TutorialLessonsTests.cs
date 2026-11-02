using System;
using System.Collections.Generic;
using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests.EditMode
{
    public class TutorialLessonsTests
    {
        private static TutorialLessons.Setup Make(TutorialLesson lesson) =>
            TutorialLessons.Create(lesson, StandardBoard.Create(), new Random(1));

        private static ApostleState Get(Match match, int id)
        {
            foreach (ApostleState apostle in match.Battlefield.Apostles)
            {
                if (apostle.Id == id)
                    return apostle;
            }
            throw new InvalidOperationException($"No Apostle with id {id}.");
        }

        [TestCase(TutorialLesson.Move)]
        [TestCase(TutorialLesson.Attack)]
        [TestCase(TutorialLesson.Combo)]
        [TestCase(TutorialLesson.Win)]
        public void EveryLesson_StartsOnYourTurn_WithAFullHand(TutorialLesson lesson)
        {
            Match match = Make(lesson).Match;

            Assert.AreEqual(TutorialLessons.YouId, match.CurrentApostle.Id);
            Assert.AreEqual(GameConstants.StartingHandSize, match.GetHand(TutorialLessons.YouId).Count);
            Assert.IsFalse(match.IsOver);
        }

        [Test]
        public void Move_TargetIsASoulTileInReach()
        {
            TutorialLessons.Setup setup = Make(TutorialLesson.Move);
            var reach = new List<HexCoord>();
            setup.Match.GetReachableTiles(reach);

            Assert.IsTrue(setup.HasTarget);
            CollectionAssert.Contains(reach, setup.Target);
            Assert.AreEqual(SpecialTileType.Soul, setup.Match.Battlefield.Specials.Active[setup.Target]);
        }

        [TestCase(TutorialLesson.Attack)]
        [TestCase(TutorialLesson.Win)]
        public void AttackLessons_PutTheEnemyNextToYou(TutorialLesson lesson)
        {
            Match match = Make(lesson).Match;
            var targets = new List<ApostleState>();

            match.GetAttackTargets(targets);

            Assert.AreEqual(1, targets.Count);
            Assert.AreEqual(TutorialLessons.EnemyId, targets[0].Id);
            CollectionAssert.Contains(match.GetHand(TutorialLessons.YouId), ArcanaId.Chariot);
        }

        [Test]
        public void Combo_HandHasTwoRadianceCards()
        {
            IReadOnlyList<ArcanaId> hand = Make(TutorialLesson.Combo).Match.GetHand(TutorialLessons.YouId);

            CollectionAssert.Contains(hand, ArcanaId.Magician);
            CollectionAssert.Contains(hand, ArcanaId.Lovers);
        }

        [Test]
        public void Win_StartsOneSoulShortOfAwakening()
        {
            Match match = Make(TutorialLesson.Win).Match;

            Assert.AreEqual(GameConstants.SoulsToAwaken - 1, Get(match, TutorialLessons.YouId).Souls);
            Assert.IsFalse(match.Ascension.HasAwakened);
        }

        [Test]
        public void Attack_ChariotThenStrike_StealsASoul()
        {
            Match match = Make(TutorialLesson.Attack).Match;
            int before = Get(match, TutorialLessons.YouId).Souls;

            Assert.AreEqual(CardPlayResult.Success, match.PlayCard(ArcanaId.Chariot, TutorialLessons.EnemyId));
            Assert.IsTrue(match.Strike(out _));

            Assert.AreEqual(before + GameConstants.SoulsStolenPerAttack, Get(match, TutorialLessons.YouId).Souls);
        }
    }
}
