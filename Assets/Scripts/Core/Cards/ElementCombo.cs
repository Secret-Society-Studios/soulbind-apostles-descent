namespace SoulBind.Core
{
    /// <summary>
    /// Element combos: playing two cards of the same element in one turn triggers that element's
    /// bonus. [Assumed] Each element's combo triggers once per turn, on its 2nd card.
    /// Match applies the bonuses; these are the numbers from the rulebook.
    /// </summary>
    public static class ElementCombo
    {
        public const int ElementCount = 4;
        public const int CardsNeeded = 2;

        /// <summary>Radiance: +2 DEF, added to the shield.</summary>
        public const int RadianceShield = 2;

        /// <summary>Celeste: -1 Corruption to the player.</summary>
        public const int CelesteCorruptionReduction = 1;

        /// <summary>Crimson: +2 ATK to the attack in progress.</summary>
        public const int CrimsonAttack = 2;

        /// <summary>Nocturne: +1 Corruption to the target (or the nearest opponent when not attacking).</summary>
        public const int NocturneCorruption = 1;
    }
}