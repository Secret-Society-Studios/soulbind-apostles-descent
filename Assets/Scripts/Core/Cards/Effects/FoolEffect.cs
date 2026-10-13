namespace SoulBind.Core
{
    /// <summary>
    /// The Fool: "Reduce Corruption by 1, but reduce DEF by 2 (DEF cannot go below 0)."
    /// The DEF loss comes off the player's shield; timed DEF (such as The Star's) is not touched.
    /// </summary>
    public sealed class FoolEffect : CardEffect
    {
        public const int CorruptionReduction = 1;
        public const int ShieldLoss = 2;

        public override bool ReducesCorruption => true;

        public override void OnPlayed(CardPlayContext context)
        {
            context.Player.ReduceCorruption(CorruptionReduction);
            context.Player.ReduceShield(ShieldLoss);
        }
    }
}