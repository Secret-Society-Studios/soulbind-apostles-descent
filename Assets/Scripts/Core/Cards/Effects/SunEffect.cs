namespace SoulBind.Core
{
    /// <summary>The Sun: "Reduce Corruption by 1 and heal 2 Vessel."</summary>
    public sealed class SunEffect : CardEffect
    {
        public const int CorruptionReduction = 1;
        public const int HealAmount = 2;

        public override bool ReducesCorruption => true;

        public override void OnPlayed(CardPlayContext context)
        {
            context.Player.ReduceCorruption(CorruptionReduction);
            context.Player.Heal(HealAmount);
        }
    }
}