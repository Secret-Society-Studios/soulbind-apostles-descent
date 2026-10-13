using System;

namespace SoulBind.Core
{
    /// <summary>
    /// The effect of each card, indexed by ArcanaId. A card with no entry yet plays with its
    /// plain ATK, DEF and CAP only. Built once; effects hold no state, so they are shared.
    /// </summary>
    public static class CardEffects
    {
        private static readonly CardEffect[] Effects = Build();

        /// <summary>The card's effect, or null if it has none (yet).</summary>
        public static CardEffect Get(ArcanaId id)
        {
            int index = (int)id;
            if (index < 0 || index >= Effects.Length)
                throw new ArgumentOutOfRangeException(nameof(id), $"Unknown card {id}.");
            return Effects[index];
        }

        private static CardEffect[] Build()
        {
            var effects = new CardEffect[CardCatalog.All.Count];
            effects[(int)ArcanaId.Fool] = new FoolEffect();
            effects[(int)ArcanaId.Magician] = new MagicianEffect();
            effects[(int)ArcanaId.HighPriestess] = new HighPriestessEffect();
            effects[(int)ArcanaId.Empress] = new EmpressEffect();
            effects[(int)ArcanaId.Emperor] = new EmperorEffect();
            effects[(int)ArcanaId.Hierophant] = new HierophantEffect();
            effects[(int)ArcanaId.Lovers] = new LoversEffect();
            effects[(int)ArcanaId.Chariot] = new ChariotEffect();
            effects[(int)ArcanaId.Strength] = new StrengthEffect();
            effects[(int)ArcanaId.Hermit] = new HermitEffect();
            effects[(int)ArcanaId.Justice] = new JusticeEffect();
            effects[(int)ArcanaId.HangedMan] = new HangedManEffect();
            effects[(int)ArcanaId.Death] = new DeathEffect();
            effects[(int)ArcanaId.Devil] = new DevilEffect();
            effects[(int)ArcanaId.Tower] = new TowerEffect();
            effects[(int)ArcanaId.Star] = new StarEffect();
            effects[(int)ArcanaId.Moon] = new MoonEffect();
            effects[(int)ArcanaId.Sun] = new SunEffect();
            effects[(int)ArcanaId.Judgement] = new JudgementEffect();
            return effects;
        }
    }
}