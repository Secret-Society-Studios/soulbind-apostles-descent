namespace SoulBind.Core
{
    /// <summary>
    /// The Chariot: "Phase 1: +3 ATK. Phases 2-3: +2 ATK. Phase 4+: +1 ATK."
    /// [Assumed] Only counts when the card is part of an attack, like The Magician.
    /// </summary>
    public sealed class ChariotEffect : CardEffect
    {
        public static int GetBonus(int phase)
        {
            if (phase <= 1)
                return 3;
            if (phase <= 3)
                return 2;
            return 1;
        }

        public override void OnPlayed(CardPlayContext context)
        {
            if (context.IsAttacking)
                context.AddToAttack(GetBonus(context.Phase));
        }
    }
}