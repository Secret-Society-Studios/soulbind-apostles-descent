using System;
using System.Collections.Generic;
using NUnit.Framework;
using SoulBind.Core;

namespace SoulBind.Tests.EditMode
{
    public class CardCatalogTests
    {
        [Test]
        public void All_HasTwentyTwoCards()
        {
            Assert.AreEqual(22, CardCatalog.All.Count);
        }

        [Test]
        public void All_EachCardIsAtItsOwnIndex()
        {
            for (int i = 0; i < CardCatalog.All.Count; i++)
                Assert.AreEqual(i, (int)CardCatalog.All[i].Id, $"Card at index {i}");
        }

        [Test]
        public void DeckSize_IsSixtyThree()
        {
            Assert.AreEqual(63, CardCatalog.DeckSize);
        }

        // Rulebook Section 10: element, ATK, DEF, CAP, count.
        [TestCase(ArcanaId.Fool, Element.Celeste, 0, 0, 0, 6)]
        [TestCase(ArcanaId.Magician, Element.Radiance, 2, 1, 1, 6)]
        [TestCase(ArcanaId.HighPriestess, Element.Nocturne, 2, 2, 2, 4)]
        [TestCase(ArcanaId.Empress, Element.Radiance, 0, 3, 3, 3)]
        [TestCase(ArcanaId.Emperor, Element.Crimson, 6, 3, 7, 1)]
        [TestCase(ArcanaId.Hierophant, Element.Celeste, 0, 0, 2, 3)]
        [TestCase(ArcanaId.Lovers, Element.Radiance, 4, 4, 4, 3)]
        [TestCase(ArcanaId.Chariot, Element.Crimson, 5, 2, 3, 3)]
        [TestCase(ArcanaId.Strength, Element.Crimson, 0, 3, 3, 4)]
        [TestCase(ArcanaId.Hermit, Element.Nocturne, 2, 2, 2, 3)]
        [TestCase(ArcanaId.WheelOfFortune, Element.Celeste, 3, 2, 3, 3)]
        [TestCase(ArcanaId.Justice, Element.Celeste, 0, 2, 4, 3)]
        [TestCase(ArcanaId.HangedMan, Element.Nocturne, 0, 5, 3, 4)]
        [TestCase(ArcanaId.Death, Element.Nocturne, 7, 4, 5, 2)]
        [TestCase(ArcanaId.Temperance, Element.Celeste, 0, 5, 4, 2)]
        [TestCase(ArcanaId.Devil, Element.Nocturne, 8, 3, 6, 2)]
        [TestCase(ArcanaId.Tower, Element.Crimson, 3, 3, 6, 3)]
        [TestCase(ArcanaId.Star, Element.Radiance, 0, 6, 4, 2)]
        [TestCase(ArcanaId.Moon, Element.Nocturne, 4, 10, 6, 1)]
        [TestCase(ArcanaId.Sun, Element.Radiance, 10, 4, 6, 1)]
        [TestCase(ArcanaId.Judgement, Element.Radiance, 2, 4, 3, 3)]
        [TestCase(ArcanaId.World, Element.Celeste, 6, 7, 8, 1)]
        public void Card_MatchesRulebook(ArcanaId id, Element element, int attack, int defense, int cap, int count)
        {
            CardDefinition card = CardCatalog.Get(id);

            Assert.AreEqual(id, card.Id);
            Assert.AreEqual(element, card.Element, "Element");
            Assert.AreEqual(attack, card.Attack, "ATK");
            Assert.AreEqual(defense, card.Defense, "DEF");
            Assert.AreEqual(cap, card.Cap, "CAP");
            Assert.AreEqual(count, card.Count, "Count");
        }

        [Test]
        public void Rarity_TotalsAreFiveCommonThirteenEpicFourLegendary()
        {
            int common = 0, epic = 0, legendary = 0;
            foreach (CardDefinition card in CardCatalog.All)
            {
                switch (card.Rarity)
                {
                    case CardRarity.Common: common++; break;
                    case CardRarity.Epic: epic++; break;
                    default: legendary++; break;
                }
            }

            Assert.AreEqual(5, common, "Common");
            Assert.AreEqual(13, epic, "Epic");
            Assert.AreEqual(4, legendary, "Legendary");
        }

        [Test]
        public void Rarity_LegendariesAreEmperorMoonSunWorld()
        {
            var legendaries = new List<ArcanaId>();
            foreach (CardDefinition card in CardCatalog.All)
            {
                if (card.Rarity == CardRarity.Legendary)
                    legendaries.Add(card.Id);
            }

            CollectionAssert.AreEquivalent(
                new[] { ArcanaId.Emperor, ArcanaId.Moon, ArcanaId.Sun, ArcanaId.World }, legendaries);
        }

        [TestCase(6, CardRarity.Common)]
        [TestCase(4, CardRarity.Common)]
        [TestCase(3, CardRarity.Epic)]
        [TestCase(2, CardRarity.Epic)]
        [TestCase(1, CardRarity.Legendary)]
        public void GetRarity_FollowsCopyCount(int count, CardRarity expected)
        {
            Assert.AreEqual(expected, CardDefinition.GetRarity(count));
        }

        [Test]
        public void GetRarity_LessThanOneCopy_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CardDefinition.GetRarity(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CardDefinition.GetRarity(-1));
        }

        [Test]
        public void Get_UnknownId_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CardCatalog.Get((ArcanaId)22));
        }

        [Test]
        public void AllCards_HaveNameAndEffect()
        {
            foreach (CardDefinition card in CardCatalog.All)
            {
                Assert.IsFalse(string.IsNullOrEmpty(card.Name), $"{card.Id} has no name");
                Assert.IsFalse(string.IsNullOrEmpty(card.Effect), $"{card.Id} has no effect text");
            }
        }

        [Test]
        public void CardDefinition_InvalidValues_Throw()
        {
            Assert.Throws<ArgumentException>(() =>
                new CardDefinition(ArcanaId.Fool, "", Element.Celeste, 0, 0, 0, 1, "x"));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new CardDefinition(ArcanaId.Fool, "A", Element.Celeste, -1, 0, 0, 1, "x"));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new CardDefinition(ArcanaId.Fool, "A", Element.Celeste, 0, -1, 0, 1, "x"));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new CardDefinition(ArcanaId.Fool, "A", Element.Celeste, 0, 0, -1, 1, "x"));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new CardDefinition(ArcanaId.Fool, "A", Element.Celeste, 0, 0, 0, 0, "x"));
        }
    }
}