using System;

namespace SoulBind.Core
{
    public enum CommandKind : byte
    {
        ChooseDirection,
        Move,
        PlayCard,
        Strike,
        EndTurn
    }

    public enum CommandResult : byte
    {
        /// <summary>The rules accepted the command and the match changed.</summary>
        Applied,
        /// <summary>The sender is not the Apostle who acts now. Nothing changed.</summary>
        NotYourTurn,
        /// <summary>The match is over. Nothing changed.</summary>
        MatchOver,
        /// <summary>The rules refused it (not enough Capacity, not adjacent, ...). Nothing changed.</summary>
        Refused,
        /// <summary>The bytes could not be read as a command. Nothing changed.</summary>
        Malformed
    }

    /// <summary>
    /// One player action, small enough to send over the network: what a client asks the host to do.
    /// The host checks who sent it before applying it (see MatchCommands), so a client can never act
    /// for someone else. Only the fields its Kind needs are meaningful.
    /// </summary>
    public readonly struct MatchCommand
    {
        /// <summary>Size of an encoded command, in bytes.</summary>
        public const int EncodedSize = 9;

        public readonly CommandKind Kind;
        public readonly HexCoord Destination;
        public readonly ArcanaId Card;
        public readonly int TargetId;
        public readonly int ComboTargetId;
        public readonly TurnDirection Direction;

        private MatchCommand(CommandKind kind, HexCoord destination, ArcanaId card, int targetId, int comboTargetId,
            TurnDirection direction)
        {
            Kind = kind;
            Destination = destination;
            Card = card;
            TargetId = targetId;
            ComboTargetId = comboTargetId;
            Direction = direction;
        }

        public static MatchCommand ChooseDirection(TurnDirection direction) =>
            new MatchCommand(CommandKind.ChooseDirection, default, default, 0, 0, direction);

        public static MatchCommand Move(HexCoord destination) =>
            new MatchCommand(CommandKind.Move, destination, default, 0, 0, default);

        public static MatchCommand PlayCard(ArcanaId card, int targetId = 0, int comboTargetId = 0) =>
            new MatchCommand(CommandKind.PlayCard, default, card, targetId, comboTargetId, default);

        public static MatchCommand Strike() => new MatchCommand(CommandKind.Strike, default, default, 0, 0, default);

        public static MatchCommand EndTurn() => new MatchCommand(CommandKind.EndTurn, default, default, 0, 0, default);

        /// <summary>Writes the command into buffer at offset (EncodedSize bytes).</summary>
        public void Encode(byte[] buffer, int offset)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || offset + EncodedSize > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            buffer[offset] = (byte)Kind;
            WriteShort(buffer, offset + 1, Destination.Q);
            WriteShort(buffer, offset + 3, Destination.R);
            buffer[offset + 5] = (byte)Card;
            buffer[offset + 6] = (byte)TargetId;
            buffer[offset + 7] = (byte)ComboTargetId;
            buffer[offset + 8] = (byte)Direction;
        }

        /// <summary>Reads a command written by Encode. Returns false for bytes that are not a valid command.</summary>
        public static bool TryDecode(byte[] buffer, int offset, out MatchCommand command)
        {
            command = default;
            if (buffer == null || offset < 0 || offset + EncodedSize > buffer.Length)
                return false;

            byte kind = buffer[offset];
            byte card = buffer[offset + 5];
            byte direction = buffer[offset + 8];
            if (kind > (byte)CommandKind.EndTurn || card >= CardCatalog.All.Count
                || !Enum.IsDefined(typeof(TurnDirection), Enum.ToObject(typeof(TurnDirection), direction)))
                return false;

            var destination = new HexCoord(ReadShort(buffer, offset + 1), ReadShort(buffer, offset + 3));
            command = new MatchCommand((CommandKind)kind, destination, (ArcanaId)card, buffer[offset + 6],
                buffer[offset + 7], (TurnDirection)direction);
            return true;
        }

        private static void WriteShort(byte[] buffer, int offset, int value)
        {
            short s = (short)value;
            buffer[offset] = (byte)(s & 0xFF);
            buffer[offset + 1] = (byte)((s >> 8) & 0xFF);
        }

        private static short ReadShort(byte[] buffer, int offset) =>
            (short)(buffer[offset] | (buffer[offset + 1] << 8));

        public override string ToString()
        {
            switch (Kind)
            {
                case CommandKind.ChooseDirection: return $"choose {Direction}";
                case CommandKind.Move: return $"move to ({Destination.Q},{Destination.R})";
                case CommandKind.PlayCard:
                    return TargetId != 0 ? $"play {Card} on A{TargetId}" : $"play {Card}";
                default: return Kind.ToString();
            }
        }
    }

    /// <summary>
    /// Applies commands on the host, after checking that the sender is the Apostle who acts now:
    /// the current Apostle, or the Phase's direction chooser between Phases.
    /// </summary>
    public static class MatchCommands
    {
        /// <summary>Who must act now: the current Apostle, the Phase direction chooser, or 0 if the match is over.</summary>
        public static int GetActorId(Match match)
        {
            if (match == null)
                throw new ArgumentNullException(nameof(match));
            if (match.IsOver)
                return 0;
            if (match.IsAwaitingPhaseStart)
                return match.GetNextPhaseChooserId();
            return match.CurrentApostle != null ? match.CurrentApostle.Id : 0;
        }

        public static CommandResult Apply(Match match, int senderId, MatchCommand command)
        {
            if (match == null)
                throw new ArgumentNullException(nameof(match));
            if (match.IsOver)
                return CommandResult.MatchOver;
            if (senderId == 0 || senderId != GetActorId(match))
                return CommandResult.NotYourTurn;

            bool betweenPhases = match.IsAwaitingPhaseStart;
            if (betweenPhases != (command.Kind == CommandKind.ChooseDirection))
                return CommandResult.Refused; // choosing a direction only between Phases, everything else only during a turn

            switch (command.Kind)
            {
                case CommandKind.ChooseDirection:
                    match.BeginNextPhase(command.Direction);
                    return CommandResult.Applied;
                case CommandKind.Move:
                    return match.Move(command.Destination) == MoveResult.Success ? CommandResult.Applied : CommandResult.Refused;
                case CommandKind.PlayCard:
                    return match.PlayCard(command.Card, command.TargetId, command.ComboTargetId) == CardPlayResult.Success
                        ? CommandResult.Applied
                        : CommandResult.Refused;
                case CommandKind.Strike:
                    return match.Strike(out _) ? CommandResult.Applied : CommandResult.Refused;
                case CommandKind.EndTurn:
                    match.EndTurn();
                    return CommandResult.Applied;
                default:
                    return CommandResult.Malformed;
            }
        }
    }
}