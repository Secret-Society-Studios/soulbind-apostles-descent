namespace SoulBind.Core
{
    /// <summary>
    /// The Hierophant: "+2 ATK and +2 DEF, right away, until your next turn."
    /// It counts as a 2-ATK card, so it can start an attack on an adjacent Apostle (or join one),
    /// and the +2 shows in the attack straight away. Played without attacking, the +2 ATK waits and
    /// joins the next attack you make this turn. The +2 DEF lasts until the start of your next turn.
    /// </summary>
    public sealed class HierophantEffect : CardEffect
    {
        public const int Bonus = 2;
        public const int DurationTurns = 1;

        public override int AttackBonus => Bonus;

        public override void OnPlayed(CardPlayContext context)
        {
            context.Player.AddTimedEffect(TimedEffectKind.Defense, Bonus, DurationTurns);

            // Already in the attack through AttackBonus when it is part of one.
            if (!context.IsAttacking)
                context.Player.AddTimedEffect(TimedEffectKind.Attack, Bonus, DurationTurns);
        }
    }
}