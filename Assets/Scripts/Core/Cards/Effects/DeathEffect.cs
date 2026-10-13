namespace SoulBind.Core
{
    /// <summary>
    /// Death: "Execute an opponent with less than 5 Vessel."
    /// Only as part of an attack: if the attack breaks DEF and the target had less than
    /// 5 Vessel when the attack resolved, the target is eliminated.
    /// </summary>
    public sealed class DeathEffect : CardEffect
    {
        public const int ExecuteBelowVessel = 5;

        public override void OnPlayed(CardPlayContext context)
        {
            if (context.IsAttacking)
                context.ArmExecute(ExecuteBelowVessel);
        }
    }
}