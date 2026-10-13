namespace SoulBind.Core
{
    /// <summary>
    /// The Emperor: "The higher the opponent's Corruption than yours, multiply your ATK by 2."
    /// [Assumed] Only as part of an attack: when the attack resolves, its whole ATK total doubles
    /// if the target has more Corruption than the player at that moment.
    /// </summary>
    public sealed class EmperorEffect : CardEffect
    {
        public const int AttackMultiplier = 2;

        public override void OnPlayed(CardPlayContext context)
        {
            if (context.IsAttacking)
                context.ArmEmperor();
        }
    }
}