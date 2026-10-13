namespace SoulBind.Core
{
    /// <summary>
    /// Judgement: "Increase DEF by 2 for this round and heal 1 Vessel."
    /// "This round" lasts exactly as long as a shield, so the +2 goes on the shield,
    /// on top of the card's own 4 DEF.
    /// </summary>
    public sealed class JudgementEffect : CardEffect
    {
        public const int ShieldBonus = 2;
        public const int HealAmount = 1;

        public override void OnPlayed(CardPlayContext context)
        {
            context.Player.AddShield(ShieldBonus);
            context.Player.Heal(HealAmount);
        }
    }
}