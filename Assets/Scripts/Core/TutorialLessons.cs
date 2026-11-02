using System;
using System.Collections.Generic;

namespace SoulBind.Core
{
    /// <summary>
    /// Builds the tutorial's ready-made situations: a two-Apostle match where you (Apostle 1) play
    /// first, already standing where the lesson needs you, with a hand picked for it. The rules are
    /// the normal ones; only the starting position, hand and numbers are set up.
    /// </summary>
    public static class TutorialLessons
    {
        public const int LessonCount = 4;
        public const int YouId = 1;
        public const int EnemyId = 2;
        private const int PlayerCount = 2;
        private const int MoveTargetSteps = 3;  // the Move lesson's tile is this far away

        private static readonly TimedEffect[] NoEffects = new TimedEffect[0];

        /// <summary>A lesson's match, plus the tile it asks you to reach (if it has one).</summary>
        public sealed class Setup
        {
            public Match Match;
            public bool HasTarget;
            public HexCoord Target;
        }

        // ---------- Lessons ----------

        public static Setup Create(TutorialLesson lesson, HexBoard board, Random random)
        {
            if (board == null)
                throw new ArgumentNullException(nameof(board));
            if (random == null)
                throw new ArgumentNullException(nameof(random));

            switch (lesson)
            {
                case TutorialLesson.Move:
                    return CreateApart(board, random, MoveHand, true);
                case TutorialLesson.Attack:
                    return CreateNextTo(board, random, AttackHand, 0);
                case TutorialLesson.Combo:
                    return CreateApart(board, random, ComboHand, false);
                default:
                    return CreateNextTo(board, random, AttackHand, GameConstants.SoulsToAwaken - 1);
            }
        }

        // Plain cards: nothing here should distract from walking.
        private static readonly ArcanaId[] MoveHand =
            { ArcanaId.Fool, ArcanaId.Magician, ArcanaId.Star, ArcanaId.Empress, ArcanaId.Hierophant };

        // The Chariot and The Magician make a strong first attack.
        private static readonly ArcanaId[] AttackHand =
            { ArcanaId.Chariot, ArcanaId.Magician, ArcanaId.Lovers, ArcanaId.Star, ArcanaId.Fool };

        // Two Radiance cards (The Magician, The Lovers) for the Radiance combo.
        private static readonly ArcanaId[] ComboHand =
            { ArcanaId.Magician, ArcanaId.Lovers, ArcanaId.Chariot, ArcanaId.Strength, ArcanaId.Fool };

        // The bot never attacks in a lesson, so its hand only has to be harmless.
        private static readonly ArcanaId[] EnemyHand =
            { ArcanaId.Fool, ArcanaId.Fool, ArcanaId.Hierophant, ArcanaId.Empress, ArcanaId.Magician };

        // You in the middle of the board and the enemy far away. With a target, one Soul tile a few
        // steps off is the only special tile; without one, there are none.
        private static Setup CreateApart(HexBoard board, Random random, ArcanaId[] hand, bool withTarget)
        {
            HexCoord center = FindCenter(board);
            HexCoord far = FindFarthestEdge(board, center);
            Match match = CreateMatch(board, random, hand);
            Place(match, YouId, center, -1);
            Place(match, EnemyId, far, -1);

            if (!withTarget)
            {
                match.Battlefield.Specials.Restore(new HexCoord[0], new SpecialTileType[0]);
                return new Setup { Match = match };
            }
            HexCoord target = FindTileAt(board, center, MoveTargetSteps, far);
            match.Battlefield.Specials.Restore(new[] { target }, new[] { SpecialTileType.Soul });
            return new Setup { Match = match, HasTarget = true, Target = target };
        }

        // You in the middle of the board with the enemy right next to you. souls -1 keeps the usual count.
        private static Setup CreateNextTo(HexBoard board, Random random, ArcanaId[] hand, int souls)
        {
            HexCoord center = FindCenter(board);
            HexCoord beside = board.GetNeighborsOnBoard(center)[0];
            Match match = CreateMatch(board, random, hand);
            Place(match, YouId, center, souls > 0 ? souls : -1);
            Place(match, EnemyId, beside, -1);
            match.Battlefield.Specials.Restore(new HexCoord[0], new SpecialTileType[0]);
            match.RefreshGameState();
            return new Setup { Match = match };
        }

        // ---------- Building ----------

        // A two-Apostle match where you go first and are dealt exactly this hand.
        private static Match CreateMatch(HexBoard board, Random random, ArcanaId[] hand)
        {
            var spawns = new List<HexCoord> { board.EdgeTiles[0], board.EdgeTiles[board.EdgeTiles.Count / 2] };
            var seatOrder = new[] { YouId, EnemyId };
            Deck deck = Deck.CreateStacked(BuildDeck(hand, random), random);
            return Match.Create(PlayerCount, board, spawns, seatOrder, random, deck);
        }

        // The hands go on top, dealt one card each in turn (you first); the rest of a full deck is shuffled below.
        private static List<ArcanaId> BuildDeck(ArcanaId[] hand, Random random)
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
                top.Add(Take(rest, hand[i]));
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

        private static ArcanaId Take(List<ArcanaId> cards, ArcanaId card)
        {
            if (!cards.Remove(card))
                throw new InvalidOperationException($"A lesson asks for more copies of {card} than the deck has.");
            return card;
        }

        // Puts an Apostle on a tile. souls -1 keeps what it has.
        private static void Place(Match match, int apostleId, HexCoord tile, int souls)
        {
            ApostleState apostle = Find(match, apostleId);
            apostle.Restore(tile, apostle.Vessel, souls >= 0 ? souls : apostle.Souls, apostle.Corruption,
                apostle.Shield, apostle.TurnsStarted, NoEffects);
        }

        private static ApostleState Find(Match match, int apostleId)
        {
            IReadOnlyList<ApostleState> apostles = match.Battlefield.Apostles;
            for (int i = 0; i < apostles.Count; i++)
            {
                if (apostles[i].Id == apostleId)
                    return apostles[i];
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

        private static HexCoord FindFarthestEdge(HexBoard board, HexCoord from)
        {
            HexCoord best = board.EdgeTiles[0];
            int bestDistance = -1;
            for (int i = 0; i < board.EdgeTiles.Count; i++)
            {
                int distance = HexCoord.Distance(from, board.EdgeTiles[i]);
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    best = board.EdgeTiles[i];
                }
            }
            return best;
        }

        // The first tile exactly this many steps away that is not the avoided one.
        private static HexCoord FindTileAt(HexBoard board, HexCoord from, int steps, HexCoord avoid)
        {
            for (int i = 0; i < board.TileCount; i++)
            {
                HexCoord tile = board.Tiles[i];
                if (tile != avoid && HexCoord.Distance(from, tile) == steps)
                    return tile;
            }
            throw new InvalidOperationException($"The board has no tile {steps} steps from {from}.");
        }
    }
}
