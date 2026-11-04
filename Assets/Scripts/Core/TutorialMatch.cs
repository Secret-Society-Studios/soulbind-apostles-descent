using System;
using System.Collections.Generic;

namespace SoulBind.Core
{
    /// <summary>
    /// Builds the tutorial: one two-Apostle match, set up so that a few turns teach the whole game in
    /// order. Turn 1, walk to a Soul tile. Turn 2, play a Radiance combo. Turn 3, step next to the enemy
    /// and attack: that steals the 20th Soul, so you Awaken. Then reach the Ascension tile to win.
    /// The rules are the normal ones; only the start (tiles, Souls, the order of the cards) is set up.
    /// </summary>
    public static class TutorialMatch
    {
        public const int YouId = 1;
        public const int EnemyId = 2;
        public const int TargetSteps = 3;        // the Soul tile is this far from where you start
        public const int EnemyFromTarget = 2;    // and the enemy this far beyond it: one step to reach
        private const int PlayerCount = 2;

        // The Soul tile brings you to one short of Awakening; the attack's stolen Soul does the rest.
        public const int StartingSouls = GameConstants.SoulsToAwaken - 1 - GameConstants.SoulTileAmount;

        private static readonly TimedEffect[] NoEffects = new TimedEffect[0];

        // Your hand: two Radiance cards for the combo turn, then The Chariot and a Magician for the attack.
        private static readonly ArcanaId[] YourHand =
            { ArcanaId.Magician, ArcanaId.Lovers, ArcanaId.Chariot, ArcanaId.Magician, ArcanaId.Fool };

        // The bot never attacks in the tutorial, so its hand only has to be harmless.
        private static readonly ArcanaId[] EnemyHand =
            { ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Hierophant, ArcanaId.Empress, ArcanaId.Star };

        /// <summary>The tutorial's match, plus the Soul tile it asks you to walk to first.</summary>
        public sealed class Setup
        {
            public Match Match;
            public HexCoord SoulTile;
            public HexCoord EnemyTile;
        }

        // Builds the scripted tutorial match: fixed seats, hands and deck order.
        public static Setup Create(HexBoard board, Random random)
        {
            if (board == null)
                throw new ArgumentNullException(nameof(board));
            if (random == null)
                throw new ArgumentNullException(nameof(random));

            HexCoord start = FindCenter(board);
            HexCoord soulTile = FindTileAt(board, start, TargetSteps);
            HexCoord enemyTile = FindEnemyTile(board, start, soulTile);

            var spawns = new List<HexCoord> { board.EdgeTiles[0], board.EdgeTiles[board.EdgeTiles.Count / 2] };
            var seatOrder = new[] { YouId, EnemyId };
            Deck deck = Deck.CreateStacked(BuildDeck(random), random);
            Match match = Match.Create(PlayerCount, board, spawns, seatOrder, random, deck);

            Place(match, YouId, start, StartingSouls);
            Place(match, EnemyId, enemyTile, -1);
            match.Battlefield.Specials.Restore(new[] { soulTile }, new[] { SpecialTileType.Soul });
            match.RefreshGameState();
            return new Setup { Match = match, SoulTile = soulTile, EnemyTile = enemyTile };
        }

        // ---------- Building ----------

        // The hands go on top, dealt one card each in turn (you first); the rest of a full deck is shuffled below.
        private static List<ArcanaId> BuildDeck(Random random)
        {
            var rest = new List<ArcanaId>(CardCatalog.DeckSize);
            IReadOnlyList<CardDefinition> all = CardCatalog.All;
            for (int i = 0; i < all.Count; i++)
            {
                for (int copy = 0; copy < all[i].Count; copy++)
                    rest.Add(all[i].Id);
            }

            var top = new List<ArcanaId>(2 * GameConstants.StartingHandSize);
            for (int i = 0; i < GameConstants.StartingHandSize; i++)
            {
                top.Add(Take(rest, YourHand[i]));
                top.Add(Take(rest, EnemyHand[i]));
            }

            // Fisher-Yates, with the match's own random source.
            for (int i = rest.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                ArcanaId swap = rest[i];
                rest[i] = rest[j];
                rest[j] = swap;
            }
            top.AddRange(rest);
            return top;
        }

        // Takes one copy of a card out of the deck for a scripted hand.
        private static ArcanaId Take(List<ArcanaId> cards, ArcanaId card)
        {
            if (!cards.Remove(card))
                throw new InvalidOperationException($"The tutorial asks for more copies of {card} than the deck has.");
            return card;
        }

        // Puts an Apostle on a tile. souls -1 keeps what it has.
        private static void Place(Match match, int apostleId, HexCoord tile, int souls)
        {
            IReadOnlyList<ApostleState> apostles = match.Battlefield.Apostles;
            for (int i = 0; i < apostles.Count; i++)
            {
                ApostleState apostle = apostles[i];
                if (apostle.Id != apostleId)
                    continue;
                apostle.Restore(tile, apostle.Vessel, souls >= 0 ? souls : apostle.Souls, apostle.Corruption,
                    apostle.Shield, apostle.TurnsStarted, NoEffects);
                return;
            }
            throw new InvalidOperationException($"No Apostle with id {apostleId}.");
        }

        // ---------- Finding tiles ----------

        /// <summary>The tile deepest inside the board: the one farthest from every edge tile.</summary>
        public static HexCoord FindCenter(HexBoard board)
        {
            HexCoord best = board.Tiles[0];
            int bestDepth = -1;
            for (int i = 0; i < board.TileCount; i++)
            {
                HexCoord tile = board.Tiles[i];
                int depth = int.MaxValue;
                for (int e = 0; e < board.EdgeTiles.Count; e++)
                    depth = Math.Min(depth, HexCoord.Distance(tile, board.EdgeTiles[e]));
                if (depth > bestDepth)
                {
                    bestDepth = depth;
                    best = tile;
                }
            }
            return best;
        }

        // The first tile exactly this many steps away.
        private static HexCoord FindTileAt(HexBoard board, HexCoord from, int steps)
        {
            for (int i = 0; i < board.TileCount; i++)
            {
                if (HexCoord.Distance(from, board.Tiles[i]) == steps)
                    return board.Tiles[i];
            }
            throw new InvalidOperationException($"The board has no tile {steps} steps from {from}.");
        }

        // Two steps past the Soul tile and as far from the start as possible, so the first turns stay calm.
        private static HexCoord FindEnemyTile(HexBoard board, HexCoord start, HexCoord soulTile)
        {
            HexCoord best = soulTile;
            int bestDistance = -1;
            for (int i = 0; i < board.TileCount; i++)
            {
                HexCoord tile = board.Tiles[i];
                if (HexCoord.Distance(soulTile, tile) != EnemyFromTarget)
                    continue;
                int distance = HexCoord.Distance(start, tile);
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    best = tile;
                }
            }
            if (bestDistance < 0)
                throw new InvalidOperationException("The board has no room for the tutorial's enemy.");
            return best;
        }
    }
}
