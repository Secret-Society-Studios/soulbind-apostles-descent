using System.Collections.Generic;
using System.Text;
using SoulBind.Core;

namespace SoulBind.UI
{
    /// <summary>Turns an Apostle's active and upcoming buffs into coloured text for the HUD labels.</summary>
    public static class BuffText
    {
        private static readonly List<ActiveBuff> Buffs = new List<ActiveBuff>(8);
        private static readonly StringBuilder Text = new StringBuilder(128);

        /// <summary>The Apostle's buffs joined by the separator, or an empty string when there are none.</summary>
        public static string Format(ApostleState apostle, string separator)
        {
            ActiveBuffs.Collect(apostle, Buffs);
            Text.Clear();
            for (int i = 0; i < Buffs.Count; i++)
            {
                if (i > 0)
                    Text.Append(separator);
                Text.Append("<color=").Append(ColorOf(Buffs[i].Kind)).Append('>')
                    .Append(ActiveBuffs.Describe(Buffs[i])).Append("</color>");
            }
            return Text.ToString();
        }

        private static string ColorOf(TimedEffectKind kind)
        {
            switch (kind)
            {
                case TimedEffectKind.Defense: return "#8FBFFF";
                case TimedEffectKind.Attack: return "#FF9A6B";
                case TimedEffectKind.MaxCapacity: return "#F2C759";
                case TimedEffectKind.CapacityPenalty: return "#FF5C6C";
                case TimedEffectKind.FailedAttackCorruption: return "#C79BE8";
                default: return "#FFFFFF";
            }
        }
    }
}
