namespace SoulBind.Core
{
    public enum BotActionKind : byte
    {
        None,
        ChooseDirection,
        Move,
        PlayCard,
        Strike,
        EndTurn
    }

    /// <summary>One thing a bot wants to do. Only the fields its Kind needs are meaningful.</summary>
    public readonly struct BotAction
    {
        public readonly BotActionKind Kind;
        public readonly HexCoord Destination;
        public readonly ArcanaId Card;
        public readonly int TargetId;
        public readonly int ComboTargetId;
        public readonly TurnDirection Direction;

        private BotAction(BotActionKind kind, HexCoord destination, ArcanaId card, int targetId, int comboTargetId,
            TurnDirection direction)
        {
            Kind = kind;
            Destination = destination;
            Card = card;
            TargetId = targetId;
            ComboTargetId = comboTargetId;
            Direction = direction;
        }

        public static BotAction None => default;

        public static BotAction ChooseDirection(TurnDirection direction) =>
            new BotAction(BotActionKind.ChooseDirection, default, default, 0, 0, direction);

        public static BotAction Move(HexCoord destination) =>
            new BotAction(BotActionKind.Move, destination, default, 0, 0, default);

        public static BotAction PlayCard(ArcanaId card, int targetId, int comboTargetId) =>
            new BotAction(BotActionKind.PlayCard, default, card, targetId, comboTargetId, default);

        public static BotAction Strike => new BotAction(BotActionKind.Strike, default, default, 0, 0, default);

        public static BotAction EndTurn => new BotAction(BotActionKind.EndTurn, default, default, 0, 0, default);

        public override string ToString()
        {
            switch (Kind)
            {
                case BotActionKind.ChooseDirection: return $"choose {Direction}";
                case BotActionKind.Move: return $"move to ({Destination.Q},{Destination.R})";
                case BotActionKind.PlayCard:
                    return TargetId != 0 ? $"play {Card} on A{TargetId}" : $"play {Card}";
                default: return Kind.ToString();
            }
        }
    }
}