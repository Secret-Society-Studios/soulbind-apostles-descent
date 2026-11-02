using System;
using SoulBind.Core;
using SoulBind.Utility;

namespace SoulBind.Managers
{
    // Profile stats: counts what this screen's player does in a match and saves it to their
    // profile (PlayerProfile) when the match ends. The tutorial and pass-and-play don't count.
    public sealed partial class MatchController
    {
        // ---------- Profile stats ----------

        private readonly int[] _myCardUses = new int[PlayerProfile.CardCount]; // index = ArcanaId
        private int _myCombos;
        private bool _profileSaved;
        private Match _profileMatch; // the match these counts belong to

        // What the command being submitted adds, kept until the rules accept it.
        private int _noteCard = -1; // -1 = no card
        private int _noteDelta;     // +1 played, -1 taken back
        private int _noteCombo;     // +1 triggers a combo, -1 undoes one

        // Only this player's own seat counts, outside the tutorial, with one player per screen.
        private bool CountsForProfile =>
            CurrentMatch != null
            && !IsTutorial
            && (botsPlayOthers || IsOnline)
            && !CurrentMatch.IsOver
            && MatchCommands.GetActorId(CurrentMatch) == localApostleId;

        // Called by Submit before the command is applied: works out what it would add.
        private void PrepareProfileNote(MatchCommand command)
        {
            _noteCard = -1;
            _noteDelta = 0;
            _noteCombo = 0;
            if (!CountsForProfile)
                return;
            SyncProfileMatch();

            switch (command.Kind)
            {
                case CommandKind.PlayCard:
                    NoteCard(command.Card, 1);
                    break;
                case CommandKind.ReturnCard:
                    int index = command.TargetId; // the queue slot rides in TargetId
                    if (index >= 0 && index < CurrentMatch.AttackCards.Count)
                        NoteCard(CurrentMatch.AttackCards[index], -1);
                    break;
            }
        }

        private void NoteCard(ArcanaId card, int delta)
        {
            _noteCard = (int)card;
            _noteDelta = delta;
            SoulBind.Core.Element element = CardCatalog.Get(card).Element;
            int played = CurrentMatch.GetElementsPlayedThisTurn(element);
            // Playing the second card of an element triggers its combo; taking it back undoes it.
            if (delta > 0 && played == ElementCombo.CardsNeeded - 1)
                _noteCombo = 1;
            else if (delta < 0 && played == ElementCombo.CardsNeeded)
                _noteCombo = -1;
        }

        // Called by Submit once the rules accepted it (online: once it was sent to the host).
        private void CommitProfileNote()
        {
            if (_noteCard < 0 || _noteCard >= _myCardUses.Length)
                return;
            _myCardUses[_noteCard] = Math.Max(0, _myCardUses[_noteCard] + _noteDelta);
            _myCombos = Math.Max(0, _myCombos + _noteCombo);
            _noteCard = -1;
        }

        // Called on every state change: saves the match to the profile once, when it ends.
        private void SaveProfileIfOver()
        {
            if (CurrentMatch == null || !CurrentMatch.IsOver || IsTutorial || !(botsPlayOthers || IsOnline))
                return;
            SyncProfileMatch();
            if (_profileSaved)
                return;
            _profileSaved = true;

            bool won = CurrentMatch.WinnerId == localApostleId;
            bool ascended = won && CurrentMatch.EndReason == MatchEndReason.Ascension;
            PlayerProfile.RecordMatch(won, ascended, _myCardUses, _myCombos);
        }

        // A new match starts its counts from zero.
        private void SyncProfileMatch()
        {
            if (_profileMatch == CurrentMatch)
                return;
            _profileMatch = CurrentMatch;
            Array.Clear(_myCardUses, 0, _myCardUses.Length);
            _myCombos = 0;
            _profileSaved = false;
        }
    }
}
