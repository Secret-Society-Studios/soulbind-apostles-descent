namespace SoulBind.Core
{
    /// <summary>
    /// What a card does beyond its plain ATK, DEF and CAP. One small class per card.
    /// Effects only change the game through the CardPlayContext they are given,
    /// so every effect stays inside the rules Match enforces.
    /// </summary>
    public abstract class CardEffect
    {
        /// <summary>
        /// True for cards whose effect reduces the player's Corruption (Fool, Empress, Strength, Sun).
        /// Strength is doubled when it is played in the same turn as another one of these.
        /// </summary>
        public virtual bool ReducesCorruption => false;

        /// <summary>
        /// ATK the card brings to an attack on top of its printed ATK. A card with a bonus here can
        /// start an attack even when its printed ATK is 0 (The Hierophant).
        /// </summary>
        public virtual int AttackBonus => 0;

        /// <summary>
        /// Called right after the card is played: its CAP is paid, it is in the discard pile,
        /// its DEF is on the shield, and engaging (if any) has happened.
        /// </summary>
        public abstract void OnPlayed(CardPlayContext context);
    }
}