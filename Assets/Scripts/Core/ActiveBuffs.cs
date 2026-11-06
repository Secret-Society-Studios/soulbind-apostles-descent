using System.Collections.Generic;

namespace SoulBind.Core
{
    /// <summary>One line of an Apostle's buff list: a timed effect in force now, or one starting next round.</summary>
    public readonly struct ActiveBuff
    {
        public readonly TimedEffectKind Kind;
        public readonly int Amount;

        /// <summary>The rounds this still counts for, the current round included when it is active.</summary>
        public readonly int RoundsLeft;

        /// <summary>True when it has not started yet and starts next round.</summary>
        public readonly bool IsUpcoming;

        public ActiveBuff(TimedEffectKind kind, int amount, int roundsLeft, bool isUpcoming)
        {
            Kind = kind;
            Amount = amount;
            RoundsLeft = roundsLeft;
            IsUpcoming = isUpcoming;
        }
    }

    /// <summary>
    /// Which timed effects an Apostle has that matter this round or next, for the HUD. A round here is one
    /// of the Apostle's own turns, the same count timed effects use. Pure rules data: no Unity types.
    /// </summary>
    public static class ActiveBuffs
    {
        /// <summary>
        /// Fills results with the Apostle's buffs: those active now, then those starting next round.
        /// Effects of the same kind that end together become one line with their amounts added.
        /// </summary>
        public static void Collect(ApostleState apostle, List<ActiveBuff> results)
        {
            results.Clear();
            if (apostle.IsEliminated)
                return;

            AddWindow(apostle, results, upcoming: false);
            AddWindow(apostle, results, upcoming: true);
        }

        /// <summary>Short text for one buff, such as "+4 DEF (2r)" or "Next +5 ATK (1r)".</summary>
        public static string Describe(ActiveBuff buff)
        {
            string text = $"{Label(buff)} ({buff.RoundsLeft}r)";
            return buff.IsUpcoming ? "Next " + text : text;
        }

        private static void AddWindow(ApostleState apostle, List<ActiveBuff> results, bool upcoming)
        {
            int now = apostle.TurnsStarted;
            IReadOnlyList<TimedEffect> effects = apostle.TimedEffects;
            for (int i = 0; i < effects.Count; i++)
            {
                TimedEffect effect = effects[i];
                bool inWindow = upcoming
                    ? !effect.IsActiveOn(now) && effect.IsActiveOn(now + 1)
                    : effect.IsActiveOn(now);
                if (!inWindow || effect.Amount == 0)
                    continue;

                int firstRound = upcoming ? now + 1 : now;
                Merge(results, new ActiveBuff(effect.Kind, effect.Amount, effect.ExpiresOnTurn - firstRound, upcoming));
            }
        }

        // Adds the buff to a matching line, or as a new line.
        private static void Merge(List<ActiveBuff> results, ActiveBuff buff)
        {
            for (int i = 0; i < results.Count; i++)
            {
                ActiveBuff line = results[i];
                if (line.Kind == buff.Kind && line.RoundsLeft == buff.RoundsLeft && line.IsUpcoming == buff.IsUpcoming)
                {
                    results[i] = new ActiveBuff(line.Kind, line.Amount + buff.Amount, line.RoundsLeft, line.IsUpcoming);
                    return;
                }
            }
            results.Add(buff);
        }

        private static string Label(ActiveBuff buff)
        {
            switch (buff.Kind)
            {
                case TimedEffectKind.Defense: return $"+{buff.Amount} DEF";
                case TimedEffectKind.Attack: return $"+{buff.Amount} ATK";
                case TimedEffectKind.MaxCapacity: return $"+{buff.Amount} CAP";
                case TimedEffectKind.CapacityPenalty: return $"-{buff.Amount} CAP";
                case TimedEffectKind.FailedAttackCorruption: return $"Block +{buff.Amount} COR";
                default: return buff.Kind.ToString();
            }
        }
    }
}
