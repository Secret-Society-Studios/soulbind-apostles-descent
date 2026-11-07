using System.Collections.Generic;
using SoulBind.Core;
using SoulBind.UI;
using UnityEngine;

namespace SoulBind.Managers
{
    /// <summary>
    /// Plays a Strike on screen one card at a time. The rules resolve it in one go, then each card that
    /// lands on the pile swings at the targets and shows its own damage, while StrikeDisplay holds back
    /// everything else until the last card. Display only: the result never changes.
    /// </summary>
    public sealed partial class MatchController
    {
        private const float QuickSwingShare = 0.6f; // every swing but the last, so a long attack keeps moving
        private const float QuickShake = 0.05f;
        private const float FullShake = 0.12f;
        private const float MaxStrikeSeconds = 20f; // input is locked meanwhile, so it can never hang

        private readonly List<ArcanaId> _engagedCards = new List<ArcanaId>(8);
        private readonly List<int> _cardAttack = new List<int>(8);
        private readonly List<int[]> _stepDamage = new List<int[]>(GameConstants.MaxPlayers); // one per hit, then card
        private readonly List<int[]> _stepAttack = new List<int[]>(GameConstants.MaxPlayers);
        private int _stepCount;
        private int _stepNext;
        private int _stepsLanded;
        private bool _stepSwinging;
        private float _strikeDeadline;

        /// <summary>A card of the play queue reached the pile. TableHandsView calls this.</summary>
        public void NoteCardLanded()
        {
            if (StrikeDisplay.Active)
                _stepsLanded++;
        }

        // A Strike just resolved: works out what each card shows and holds the result back.
        private void BeginStrikeSteps(IReadOnlyList<StrikeHit> hits)
        {
            _cardAttack.Clear();
            for (int i = 0; i < _engagedCards.Count; i++)
                _cardAttack.Add(CardAttack.Of(_engagedCards[i]));
            if (_cardAttack.Count == 0)
                _cardAttack.Add(1); // no card seen (joined online mid-attack): one swing shows it all

            _stepDamage.Clear();
            _stepAttack.Clear();
            for (int h = 0; h < hits.Count; h++)
            {
                int[] attack = StrikeBreakdown.SplitAttack(hits[h].Attack, _cardAttack);
                _stepAttack.Add(attack);
                _stepDamage.Add(StrikeBreakdown.DamagePerCard(attack, hits[h].Damage));
            }

            _stepCount = _cardAttack.Count;
            _stepNext = 0;
            _stepsLanded = 0;
            _stepSwinging = false;
            _strikeDeadline = Time.unscaledTime + MaxStrikeSeconds;

            IReadOnlyList<ApostleState> apostles = CurrentMatch.Battlefield.Apostles;
            for (int i = 0; i < apostles.Count && i < _lastVessel.Length; i++)
                StrikeDisplay.Hold(apostles[i].Id, _statsTaken ? _lastVessel[i] : apostles[i].Vessel);
        }

        // Starts the next card's swing once that card has landed, or ends the Strike.
        private void AdvanceStrikeSteps()
        {
            if (!StrikeDisplay.Active)
                return;
            if (CurrentMatch == null || _lungeIndex < 0 || Time.unscaledTime > _strikeDeadline)
            {
                FinishStrike();
                return;
            }
            if (_stepSwinging)
                return;
            if (_stepNext >= _stepCount)
            {
                FinishStrike();
                return;
            }
            if (_stepNext >= _stepsLanded && CardsPlaying)
                return;

            if (!StepHasAttack(_stepNext))
            {
                // A card with no ATK has no swing, but its effect still comes out in its turn.
                PlayNewEffects(1);
                _stepNext++;
                return;
            }

            _stepSwinging = true;
            _lungeWaiting = false;
            _impactDone = false;
            _lungeElapsed = 0f;
            float seconds = _stepNext == _stepCount - 1 ? StrikeDesignedSeconds : StrikeDesignedSeconds * QuickSwingShare;
            _lungeDuration = MotionStyle.Seconds(seconds);
            if (_lungeIndex < _views.Count)
                _views[_lungeIndex].PlayAttack(_lungeDirection);
        }

        private bool StepHasAttack(int step)
        {
            for (int h = 0; h < _stepAttack.Count; h++)
            {
                if (_stepAttack[h][step] > 0)
                    return true;
            }
            return false;
        }

        // A swing has finished.
        private void EndSwing()
        {
            _stepSwinging = false;
            _lungeWaiting = true;
            _stepNext++;
        }

        // This card lands: its damage on each target, or Blocked where DEF took it.
        private void Impact()
        {
            bool last = _stepNext == _stepCount - 1;
            GameAudio.PlaySfx(Sounds != null ? Sounds.AttackHit : null);

            for (int h = 0; h < _impactViews.Count; h++)
            {
                int view = _impactViews[h];
                int attack = _stepAttack[h][_stepNext];
                if (view < 0 || attack <= 0)
                    continue;

                int damage = _stepDamage[h][_stepNext];
                Vector3 above = _discTargets[view] + new Vector3(0f, 0.45f, -2f); // in front of the standing pieces
                if (damage > 0)
                {
                    _views[view].PlayDamaged(-_lungeDirection);
                    if (_effects != null)
                        _effects.Strike(_discTargets[view], _lungeDirection, apostleDiameter * 1.4f);
                    if (_numbers != null)
                        _numbers.Show(above, (-damage).ToString(), new Color(0.90f, 0.14f, 0.18f));
                    StrikeDisplay.Deal(view + 1, damage);
                }
                else if (_numbers != null)
                {
                    _numbers.Show(above, "Blocked", new Color(0.45f, 0.70f, 1f));
                }
            }

            if (_effects != null)
            {
                _effects.Shake(last ? FullShake : QuickShake, 0.35f);
                if (last)
                    _effects.DarkPulse(0.6f);
            }
            PlayNewEffects(1);
            StateChanged?.Invoke(); // the Vessel bars drop
        }

        // The last card has landed: the screen catches up with the rules.
        private void FinishStrike()
        {
            StrikeDisplay.End();
            _lungeIndex = -1;
            _lungeWaiting = false;
            _stepSwinging = false;
            if (CurrentMatch == null)
                return;

            // Each card already showed its damage, so only Souls and Corruption are left to show.
            IReadOnlyList<ApostleState> apostles = CurrentMatch.Battlefield.Apostles;
            for (int i = 0; i < apostles.Count && i < _lastVessel.Length; i++)
                _lastVessel[i] = apostles[i].Vessel;

            SyncViews(); // anyone eliminated falls now
            ShowStatChanges();
            PlayNewEffects();
            StateChanged?.Invoke();
        }
    }
}
