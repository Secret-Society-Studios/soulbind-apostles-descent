using SoulBind.Core;
using UnityEngine;

namespace SoulBind.Utility
{
    /// <summary>
    /// The palette of Apostle colours, the one this player picked (saved on the device) and which seat
    /// has which colour in this match. Board discs, the HUD and name plates all read Get.
    /// </summary>
    public static class ApostleColors
    {
        private const string SaveKey = "SoulBind.ApostleColor";

        private static readonly Color[] Colors =
        {
            new Color(0.85f, 0.25f, 0.25f),
            new Color(0.25f, 0.50f, 0.95f),
            new Color(0.30f, 0.80f, 0.40f),
            new Color(0.95f, 0.80f, 0.20f),
            new Color(0.70f, 0.40f, 0.90f),
            new Color(0.95f, 0.55f, 0.20f)
        };

        private static readonly string[] Names = { "Crimson", "Azure", "Emerald", "Gold", "Violet", "Ember" };

        // Index = Apostle id - 1. Value = index into Colors.
        private static readonly int[] Seats = new int[SeatColors.Count];

        static ApostleColors() => ResetOnPlay();

        // Starts in seat order on every play, even with domain reload off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            for (int i = 0; i < Seats.Length; i++)
                Seats[i] = i;
        }

        /// <summary>The color of the Apostle with this id in this match (ids start at 1).</summary>
        public static Color Get(int apostleId) => Colors[Seats[(apostleId - 1 + Seats.Length) % Seats.Length]];

        /// <summary>One colour of the palette.</summary>
        public static Color Palette(int index) => Colors[index];

        /// <summary>The name of one colour of the palette.</summary>
        public static string PaletteName(int index) => Names[index];

        /// <summary>The palette colour this player picked. Saved at once.</summary>
        public static int LocalChoice
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(SaveKey, 0), 0, SeatColors.Count - 1);
            set
            {
                PlayerPrefs.SetInt(SaveKey, Mathf.Clamp(value, 0, SeatColors.Count - 1));
                PlayerPrefs.Save();
            }
        }

        /// <summary>A match on this device: your seat gets your colour and the others share the rest.</summary>
        public static void AssignOffline(int localApostleId)
        {
            var wanted = new int[SeatColors.Count];
            for (int i = 0; i < wanted.Length; i++)
                wanted[i] = SeatColors.None;
            wanted[localApostleId - 1] = LocalChoice;
            SeatColors.Resolve(wanted, Seats);
        }

        /// <summary>An online match: the colours the host worked out. Anything malformed is ignored.</summary>
        public static void SetSeats(int[] colors)
        {
            if (SeatColors.IsValid(colors))
                colors.CopyTo(Seats, 0);
        }
    }
}
