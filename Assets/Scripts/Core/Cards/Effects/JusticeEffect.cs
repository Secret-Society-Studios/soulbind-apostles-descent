namespace SoulBind.Core
{
    /// <summary>
    /// Justice: "Increase all opponents' Corruption by 2."
    /// Every living opponent anywhere on the board, whether or not the player is attacking.
    /// </summary>
    public sealed class JusticeEffect : CardEffect
    {
        public const int CorruptionAdded = 2;

        public override void OnPlayed(CardPlayContext context)
        {
            for (int i = 0; i < context.Apostles.Count; i++)
            {
                ApostleState other = context.Apostles[i];
                if (other != context.Player && !other.IsEliminated)
                    other.AddCorruption(CorruptionAdded);
            }
        }
    }
}