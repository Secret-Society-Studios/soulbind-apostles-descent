namespace SoulBind.Core
{
    /// <summary>
    /// The Magician: "Current attack has +1 ATK."
    /// Only counts when the card is part of an attack (engaging with it, or played while Engaged).
    /// </summary>
    public sealed class MagicianEffect : CardEffect
    {
        public const int BonusAttack = 1;

        public override void OnPlayed(CardPlayContext context)
        {
            if (context.IsAttacking)
                context.AddToAttack(BonusAttack);
        }
    }
}