namespace SoulBind.Core
{
    /// <summary>
    /// One effect that lasts for a number of the owner's turns. It ends at the start of the
    /// owner's turn number ExpiresOnTurn (counted by ApostleState.TurnsStarted).
    /// </summary>
    public readonly struct TimedEffect
    {
        public readonly TimedEffectKind Kind;
        public readonly int Amount;
        public readonly int ExpiresOnTurn;

        public TimedEffect(TimedEffectKind kind, int amount, int expiresOnTurn)
        {
            Kind = kind;
            Amount = amount;
            ExpiresOnTurn = expiresOnTurn;
        }
    }
}