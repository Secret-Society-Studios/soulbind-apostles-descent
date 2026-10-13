namespace SoulBind.Core
{
    /// <summary>The kinds of special tile. The order matters: placement assigns types in this order.</summary>
    public enum SpecialTileType : byte
    {
        Heal,
        Purify,
        Soul
    }
}