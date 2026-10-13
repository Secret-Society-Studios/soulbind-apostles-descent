namespace SoulBind.Core
{
    /// <summary>
    /// One effect that lasts for some of the owner's turns. It is active from the owner's turn
    /// number StartsOnTurn and ends at the start of turn number ExpiresOnTurn
    /// (both counted by ApostleState.TurnsStarted).
    /// </summary>
    public readonly struct TimedEffect
    {
        public readonly TimedEffectKind Kind;
        public readonly int Amount;
        public readonly int StartsOnTurn;
        public readonly int ExpiresOnTurn;

        public TimedEffect(TimedEffectKind kind, int amount, int startsOnTurn, int expiresOnTurn)
        {
            Kind = kind;
            Amount = amount;
            StartsOnTurn = startsOnTurn;
            ExpiresOnTurn = expiresOnTurn;
        }

        public bool IsActiveOn(int turn) => turn >= StartsOnTurn && turn < ExpiresOnTurn;
    }
}