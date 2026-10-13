using System;

namespace SoulBind.Core
{
    public static class GameConstants
    {
        public const int MinPlayers = 2;
        public const int MaxPlayers = 6;

        public const int StartingVessel = 50;
        public const int MaxVessel = 50;
        public const int SoulsToAwaken = 20;
        public const int StartingHandSize = 5;
        public const int MaxHandSize = 7;
        public const int MoveCapacityCost = 2;

        public const int RoundsPerPhase = 3;
        public const int Phase1Capacity = 8;
        public const int Phase2To3Capacity = 12;
        public const int Phase4PlusCapacity = 15;
        
        public const int CorruptionThreshold = 5;        // Corrupted and Soullocked at this value or higher
        public const int SevereCorruptionThreshold = 10; // heavier recurring damage at this value or higher
        public const int CorruptedDamage = 2;
        public const int SevereCorruptionDamage = 4;
        public const int ZeroSoulCorruption = 5;         // gained the moment Souls reach 0
        public const int ZeroSoulDamageMultiplier = 2;   // Corruption damage multiplier while at 0 Souls

        public const int SpecialTilesPerType = 3;
        public const int HealTileAmount = 5;
        public const int PurifyTileAmount = 1;
        public const int SoulTileAmount = 3;

        public const int SoulsStolenPerAttack = 1;
        public const int AttackCorruption = 1;

        public const int CardsDrawnPerTurn = 1;
        public const int HandOverLimitCorruption = 1;

        public static int GetStartingSouls(int playerCount)
        {
            switch (playerCount)
            {
                case 2: return 13;
                case 3: return 9;
                case 4: return 7;
                case 5: return 6;
                case 6: return 5;
                default:
                    throw new ArgumentOutOfRangeException(nameof(playerCount), "Player count must be 2 to 6.");
            }
        }
    }
}