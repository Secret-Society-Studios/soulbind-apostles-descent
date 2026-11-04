namespace SoulBind.Core
{
    /// <summary>
    /// The Chariot: "Phase 1: +3 ATK. Phases 2-3: +2 ATK. Phase 4+: +1 ATK."
    /// [Assumed] Only counts when the card is part of an attack, like The Magician.
    /// </summary>
    public sealed class ChariotEffect : CardEffect
    {
        // The Chariot hits harder early: +3 in Phase 1, +2 in Phases 2 and 3, then +1.
        public static int GetBonus(int phase)
        {
            if (phase <= 1)
                return 3;
            if (phase <= 3)
                return 2;
            return 1;
        }

        // Shows the phase bonus on the card before it is played.
        public override int GetAttackPreview(int phase, int sameBefore) => GetBonus(phase);

        // Adds the phase bonus when the card is part of an attack.
        public override void OnPlayed(CardPlayContext context)
        {
            if (context.IsAttacking)
                context.AddToAttack(GetBonus(context.Phase));
        }
    }
}
