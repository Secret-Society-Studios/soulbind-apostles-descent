namespace SoulBind.Core
{
    /// <summary>
    /// The official SoulBind board: 14 rows, 170 tiles, with a notch in the top row
    /// and a notch in the bottom row. Edit the rows below to change the board.
    /// </summary>
    public static class StandardBoard
    {
        // Q coordinate of the first character in each row string.
        public const int FirstColumnQ = -5;

        private static readonly string[] Rows =
        {
            "........#####..###",
            "......############",
            ".....#############",
            "....##############",
            "...##############.",
            "...#############..",
            "..#############...",
            "...############...",
            "..#############...",
            ".#############....",
            ".#############....",
            ".############.....",
            "############......",
            ".####..####......."
        };

        public static HexBoard Create() => HexBoard.FromRows(Rows, FirstColumnQ);
    }
}