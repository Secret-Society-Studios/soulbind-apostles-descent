using System.Collections.Generic;

namespace SoulBind.Core
{
    /// <summary>
    /// Shares the palette out among the seats: every seat gets a different colour. Seats pick in order,
    /// so the earlier seat keeps a colour two players want; anyone left over takes the lowest free one.
    /// With nobody asking, seat 1 gets colour 0, seat 2 colour 1, and so on.
    /// </summary>
    public static class SeatColors
    {
        public const int Count = GameConstants.MaxPlayers;
        public const int None = -1; // a seat with no wish, like a bot

        /// <summary>Changes one player's reservation only if nobody else owns the colour.</summary>
        public static bool TryReserve(IDictionary<string, int> reservations, string owner, int color)
        {
            if (color < 0 || color >= Count)
                return false;
            foreach (KeyValuePair<string, int> reservation in reservations)
                if (reservation.Key != owner && reservation.Value == color)
                    return false;
            reservations[owner] = color;
            return true;
        }

        /// <summary>Fills colors (index = seat - 1) from wanted. Both arrays hold Count entries.</summary>
        public static void Resolve(int[] wanted, int[] colors)
        {
            int taken = 0; // one bit per palette colour
            for (int i = 0; i < Count; i++)
            {
                int wish = wanted[i];
                bool free = wish >= 0 && wish < Count && (taken & (1 << wish)) == 0;
                colors[i] = free ? wish : None;
                if (free)
                    taken |= 1 << wish;
            }

            int next = 0;
            for (int i = 0; i < Count; i++)
            {
                if (colors[i] != None)
                    continue;
                while ((taken & (1 << next)) != 0)
                    next++;
                colors[i] = next;
                taken |= 1 << next;
            }
        }

        /// <summary>True when colors gives every seat a different colour of the palette.</summary>
        public static bool IsValid(int[] colors)
        {
            if (colors == null || colors.Length != Count)
                return false;
            int taken = 0;
            foreach (int color in colors)
            {
                if (color < 0 || color >= Count || (taken & (1 << color)) != 0)
                    return false;
                taken |= 1 << color;
            }
            return true;
        }
    }
}
