using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoulBind
{
    public enum Element { Celeste, Crimson, Nocturne, Radiance }
    public enum CardType { Attack, Skill, Buff, Debuff }

    public enum CardEffectType
    {
        None,
        ReduceCorruptionSelfDefPenalty,
        SelfBuffNextRound,
        Swap,
        CorruptAllOpponents,
        SkipAndBuffNext,
        FreezeOpponents,
        ConditionalCorruptionMultiplyATK,
        PhaseATK,
        CorruptionReduceCombo,
        CapDebuffOpponents,
        HealAndTempDef,
        DrawExtra,
        CapBuffRounds,
        ConditionalLowestVesselATK,
        ExecuteIfLowVessel,
        CorruptOpponentAndSelf,
        ConditionalDefBreakCorrupt,
        FlatATKBonus,
        HealAndReduceCorruption,
        PairBonus,
        DefBuffHeal
    }

    [CreateAssetMenu(fileName = "NewCard", menuName = "SoulBind/Card")]
    public class Card : ScriptableObject
    {
        [Header("Identity")]
        public string cardId;
        public string cardName;
        public Element element;
        public CardType cardType;
        [TextArea] public string effectDescription;

        [Header("Stats")]
        public int atk;
        public int def;
        public int cap;

        [Header("Deck")]
        public int count; // how many copies exist in the deck

        [Header("Effect")]
        public CardEffectType effectType;
        public CardEffectParams effectParams;
    }

    // Flexible params bag so one struct covers every effect type
    // (matches the "amount / secondary / rounds / threshold" params your tracker uses)
    [System.Serializable]
    public struct CardEffectParams
    {
        public int amount;
        public int secondaryAmount;
        public int rounds;
        public int threshold;
    }
}
