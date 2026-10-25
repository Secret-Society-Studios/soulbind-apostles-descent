namespace SoulBind.Core
{
    /// <summary>
    /// What the last Wheel of Fortune swap did, so the screen can point at the two cards that changed hands.
    /// Number goes up by one with every swap in the match, so a new swap is easy to notice.
    /// Online, each player's snapshot carries it; the cards themselves are only filled in for the two
    /// Apostles involved (CardsKnown), everyone else just sees that a swap happened and where.
    /// </summary>
    public readonly struct CardSwap
    {
        /// <summary>Counts the swaps in this match (0 = none yet).</summary>
        public readonly int Number;
        /// <summary>Who played Wheel of Fortune.</summary>
        public readonly int PlayerId;
        /// <summary>Who they swapped with.</summary>
        public readonly int TargetId;
        /// <summary>Position in the player's hand of the card that was swapped (now the received card).</summary>
        public readonly int PlayerIndex;
        /// <summary>Position in the target's hand of the card that was swapped (now the given card).</summary>
        public readonly int TargetIndex;
        /// <summary>The card the player gave away (now in the target's hand). Only meaningful if CardsKnown.</summary>
        public readonly ArcanaId Given;
        /// <summary>The card the player got (taken from the target). Only meaningful if CardsKnown.</summary>
        public readonly ArcanaId Received;
        /// <summary>False when the viewer is not one of the two Apostles: Given and Received are hidden.</summary>
        public readonly bool CardsKnown;

        public CardSwap(int number, int playerId, int targetId, int playerIndex, int targetIndex, ArcanaId given, ArcanaId received, bool cardsKnown)
        {
            Number = number;
            PlayerId = playerId;
            TargetId = targetId;
            PlayerIndex = playerIndex;
            TargetIndex = targetIndex;
            Given = given;
            Received = received;
            CardsKnown = cardsKnown;
        }

        /// <summary>The same swap as one viewer may see it: the cards are hidden unless they took part.</summary>
        public CardSwap SeenBy(int viewerId)
        {
            bool known = CardsKnown && (viewerId == 0 || viewerId == PlayerId || viewerId == TargetId);
            return new CardSwap(Number, PlayerId, TargetId, PlayerIndex, TargetIndex, known ? Given : default, known ? Received : default, known);
        }
    }
}