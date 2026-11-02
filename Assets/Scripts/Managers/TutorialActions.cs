namespace SoulBind.Managers
{
    /// <summary>
    /// What the player may do at the tutorial's current step. Anything else is refused,
    /// so the player follows the coach one step at a time.
    /// </summary>
    [System.Flags]
    public enum TutorialActions
    {
        None = 0,
        Move = 1,
        Cards = 2,    // queue cards without attacking, take them back, PLAY
        Attack = 4,   // play a card onto an enemy, add to the attack, Strike
        EndTurn = 8,
        All = Move | Cards | Attack | EndTurn
    }
}
