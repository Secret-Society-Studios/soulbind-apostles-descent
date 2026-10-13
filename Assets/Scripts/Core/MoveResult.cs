namespace SoulBind.Core
{
    /// <summary>
    /// The outcome of a move attempt. Byte-sized because it is small and will
    /// be sent over the network later. The UI can use the reason to show feedback.
    /// </summary>
    public enum MoveResult : byte
    {
        Success,
        ApostleEliminated,
        OffBoard,
        NotAdjacent,
        TileOccupied,
        NotEnoughCapacity,
        AscensionTileReserved,
        EngagedInCombat
    }
}