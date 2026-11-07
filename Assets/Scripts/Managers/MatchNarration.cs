using System.Collections.Generic;
using SoulBind.Core;
using UnityEngine;

namespace SoulBind.Managers
{
    /// <summary>
    /// Works out what the narrator should say by comparing the match with the last look, so it hears the
    /// same things online as offline. What happens to one Apostle is only said to that player.
    /// MatchController creates it at runtime. Display only.
    /// </summary>
    public sealed class MatchNarration : MonoBehaviour
    {
        private const int Elements = 4; // Radiance, Celeste, Crimson, Nocturne
        private const int IdSlots = GameConstants.MaxPlayers + 1; // ids start at 1

        private readonly int[] _corruption = new int[IdSlots];
        private readonly int[] _souls = new int[IdSlots];
        private readonly bool[] _eliminated = new bool[IdSlots];
        private readonly bool[] _combo = new bool[Elements];

        private MatchController _controller;
        private Match _seen;
        private int _discards;
        private int _specials;
        private int _turnId;
        private int _awakenedId;
        private int _timeStopId;
        private bool _awaitingPhase;
        private bool _over;

        /// <summary>Creates it under parent, listening to controller's match.</summary>
        public static MatchNarration Create(Transform parent, MatchController controller)
        {
            var go = new GameObject("MatchNarration");
            go.transform.SetParent(parent, false);
            MatchNarration narration = go.AddComponent<MatchNarration>();
            narration._controller = controller;
            return narration;
        }

        /// <summary>What a tile did for the player here. Corruption is noticed in Update.</summary>
        public static void TileUsed(MatchEffectKind kind)
        {
            if (kind == MatchEffectKind.HealTile)
                Narrator.Say(NarratorLine.Healed);
            else if (kind == MatchEffectKind.PurifyTile)
                Narrator.Say(NarratorLine.Purified);
            else if (kind == MatchEffectKind.SoulTile)
                Narrator.Say(NarratorLine.SoulClaimed);
        }

        private void Update()
        {
            Match match = _controller.CurrentMatch;
            if (match == null)
                return;
            if (match != _seen)
            {
                Remember(match);
                Narrator.Say(NarratorLine.BoardSet);
                Narrator.Say(NarratorLine.DescentBegins);
                return;
            }

            int viewer = _controller.ViewerId;
            NewCards(match);
            NewCombos(match);
            if (StrikeDisplay.Active)
                return; // what the Strike did is said once its last card has landed
            Phases(match, viewer);
            Apostles(match, viewer);
            Awakening(match, viewer);
            Ending(match);

            int turnId = match.CurrentApostle != null ? match.CurrentApostle.Id : 0;
            if (turnId != _turnId && turnId == viewer && !match.IsOver)
                Narrator.Say(NarratorLine.YourTurn);
            _turnId = turnId;
        }

        // A new match: its starting state, with nothing to announce.
        private void Remember(Match match)
        {
            _seen = match;
            _discards = match.Deck.DiscardCount;
            _specials = match.Battlefield.Specials.Active.Count;
            _turnId = 0;
            _awakenedId = match.Ascension.AwakenedId;
            _timeStopId = match.TimeStopOwnerId;
            _awaitingPhase = match.IsAwaitingPhaseStart;
            _over = match.IsOver;
            System.Array.Clear(_combo, 0, Elements);
            IReadOnlyList<ApostleState> apostles = match.Battlefield.Apostles;
            for (int i = 0; i < apostles.Count; i++)
            {
                ApostleState apostle = apostles[i];
                _corruption[apostle.Id] = apostle.Corruption;
                _souls[apostle.Id] = apostle.Souls;
                _eliminated[apostle.Id] = apostle.IsEliminated;
            }
        }

        // The cards that reached the discard pile since the last look. A reshuffle only ever shrinks it.
        private void NewCards(Match match)
        {
            IReadOnlyList<ArcanaId> pile = match.Deck.DiscardPile;
            for (int i = Mathf.Min(_discards, pile.Count); i < pile.Count; i++)
                SayCard(pile[i]);
            _discards = pile.Count;
        }

        private static void SayCard(ArcanaId card)
        {
            switch (card)
            {
                case ArcanaId.World: Narrator.Say(NarratorLine.TimeStandsStill); break;
                case ArcanaId.WheelOfFortune: Narrator.Say(NarratorLine.WheelTurns); break;
                case ArcanaId.Hierophant: Narrator.Say(NarratorLine.Hierophant); break;
                case ArcanaId.Temperance: Narrator.Say(NarratorLine.Temperance); break;
                case ArcanaId.Death: Narrator.Say(NarratorLine.Judgement); break;
                case ArcanaId.HighPriestess: Narrator.Say(NarratorLine.HighPriestess); break;
            }
        }

        private void NewCombos(Match match)
        {
            for (int i = 0; i < Elements; i++)
            {
                var element = (SoulBind.Core.Element)i;
                bool triggered = match.IsComboTriggered(element);
                if (triggered && !_combo[i])
                    Narrator.Say(ComboLine(element));
                _combo[i] = triggered;
            }
        }

        private static NarratorLine ComboLine(SoulBind.Core.Element element)
        {
            switch (element)
            {
                case SoulBind.Core.Element.Radiance: return NarratorLine.ComboRadiance;
                case SoulBind.Core.Element.Celeste: return NarratorLine.ComboCeleste;
                case SoulBind.Core.Element.Crimson: return NarratorLine.ComboCrimson;
                default: return NarratorLine.ComboNocturne;
            }
        }

        private void Phases(Match match, int viewer)
        {
            bool awaiting = match.IsAwaitingPhaseStart;
            if (awaiting && !_awaitingPhase)
            {
                Narrator.Say(NarratorLine.NewPhase);
                Narrator.Say(NarratorLine.WeakestLeads);
                if (match.GetNextPhaseChooserId() == viewer)
                    Narrator.Say(NarratorLine.ChooseDirection);
            }
            _awaitingPhase = awaiting;

            int specials = match.Battlefield.Specials.Active.Count;
            if (specials > _specials)
                Narrator.Say(NarratorLine.TilesReturn);
            _specials = specials;

            int timeStop = match.TimeStopOwnerId;
            if (timeStop != _timeStopId && timeStop != 0 && timeStop != viewer)
                Narrator.Say(NarratorLine.TurnLost);
            _timeStopId = timeStop;
        }

        private void Apostles(Match match, int viewer)
        {
            IReadOnlyList<ApostleState> apostles = match.Battlefield.Apostles;
            for (int i = 0; i < apostles.Count; i++)
            {
                ApostleState apostle = apostles[i];
                int id = apostle.Id;
                if (id == viewer)
                {
                    SayCorruption(match, apostle, _corruption[id]);
                    SaySouls(apostle, _souls[id]);
                }
                if (apostle.IsEliminated && !_eliminated[id])
                    Narrator.Say(id == viewer ? NarratorLine.VesselBroken : NarratorLine.ApostleFallen);
                _corruption[id] = apostle.Corruption;
                _souls[id] = apostle.Souls;
                _eliminated[id] = apostle.IsEliminated;
            }
        }

        private static void SayCorruption(Match match, ApostleState apostle, int before)
        {
            int now = apostle.Corruption;
            if (now <= before)
                return;
            if (before < GameConstants.SevereCorruptionThreshold && now >= GameConstants.SevereCorruptionThreshold)
                Narrator.Say(NarratorLine.Consumed);
            else if (before < GameConstants.CorruptionThreshold && now >= GameConstants.CorruptionThreshold)
                Narrator.Say(NarratorLine.Corrupted);
            else if (match.GetHand(apostle.Id).Count > GameConstants.MaxHandSize)
                Narrator.Say(NarratorLine.TooManyCards);
            else
                Narrator.Say(NarratorLine.CorruptionSpreads);
        }

        // Only a Strike takes Souls away.
        private static void SaySouls(ApostleState apostle, int before)
        {
            if (apostle.Souls >= before)
                return;
            Narrator.Say(apostle.Souls == 0 ? NarratorLine.NoSouls : NarratorLine.SoulTaken);
        }

        private void Awakening(Match match, int viewer)
        {
            int awakened = match.Ascension.AwakenedId;
            if (awakened != _awakenedId && awakened != 0 && !match.IsOver)
            {
                Narrator.Say(NarratorLine.Awakened);
                Narrator.Say(awakened == viewer ? NarratorLine.ContractFulfilled : NarratorLine.StopThem);
            }
            _awakenedId = awakened;
        }

        private void Ending(Match match)
        {
            if (match.IsOver && !_over)
            {
                if (match.EndReason == MatchEndReason.Ascension)
                    Narrator.Say(NarratorLine.Ascension);
                else if (match.EndReason == MatchEndReason.LastApostleStanding)
                    Narrator.Say(NarratorLine.LastStands);
                Narrator.Say(NarratorLine.ContractComplete);
            }
            _over = match.IsOver;
        }
    }
}
