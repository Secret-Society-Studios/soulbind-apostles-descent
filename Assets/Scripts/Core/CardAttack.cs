namespace SoulBind.Core
{
    /// <summary>
    /// How much ATK a card brings to an attack: its printed ATK plus any bonus its effect adds the
    /// moment it is played (The Hierophant's +2). Use this, not CardDefinition.Attack, to decide
    /// whether a card can start an attack.
    /// </summary>
    public static class CardAttack
    {
        public static int Of(ArcanaId card)
        {
            CardEffect effect = CardEffects.Get(card);
            return CardCatalog.Get(card).Attack + (effect != null ? effect.AttackBonus : 0);
        }
    }
}