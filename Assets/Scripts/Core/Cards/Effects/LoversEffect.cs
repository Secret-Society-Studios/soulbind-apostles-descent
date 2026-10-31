namespace SoulBind.Core
{
    /// <summary>
    /// The Lovers: "Combo with another Lovers card to increase ATK by 2."
    /// [Assumed] Within one attack, every second Lovers adds +2 ATK (once per pair).
    /// A Lovers played without attacking does not count toward a pair.
    /// </summary>
    public sealed class LoversEffect : CardEffect
    {
        public const int PairBonus = 2;

        // Every second Lovers in the attack completes a pair.
        public override int GetAttackPreview(int phase, int sameBefore) =>
            sameBefore % 2 == 1 ? PairBonus : 0;

        public override void OnPlayed(CardPlayContext context)
        {
            if (!context.IsAttacking)
                return;

            // An odd number already in the attack means this card completes a pair.
            if (context.CountPlayedInAttack(ArcanaId.Lovers) % 2 == 1)
                context.AddToAttack(PairBonus);
        }
    }
}