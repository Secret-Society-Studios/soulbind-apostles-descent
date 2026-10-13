namespace SoulBind.Core
{
    /// <summary>The Empress: "Reduce Corruption by 2 and heal 1 Vessel."</summary>
    public sealed class EmpressEffect : CardEffect
    {
        public const int CorruptionReduction = 2;
        public const int HealAmount = 1;

        public override bool ReducesCorruption => true;

        public override void OnPlayed(CardPlayContext context)
        {
            context.Player.ReduceCorruption(CorruptionReduction);
            context.Player.Heal(HealAmount);
        }
    }
}