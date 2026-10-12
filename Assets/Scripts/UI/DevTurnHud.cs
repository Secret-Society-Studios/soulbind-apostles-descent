using System.Collections.Generic;
using System.Text;
using SoulBind.Core;
using SoulBind.Managers;
using UnityEngine;

namespace SoulBind.UI
{
    /// <summary>
    /// A temporary development panel built with IMGUI so it needs no Editor wiring.
    /// It will be replaced by the real UI. Button presses are applied in Update, not in OnGUI,
    /// because changing state between IMGUI's layout and repaint passes causes errors.
    /// </summary>
    public sealed class DevTurnHud : MonoBehaviour
    {
        private const float DesignHeight = 720f;

        [SerializeField] private MatchController matchController;

        private readonly StringBuilder _text = new StringBuilder(768);
        private readonly List<ApostleState> _targets = new List<ApostleState>(6);
        private Rect _panel = new Rect(10f, 10f, 470f, 470f);
        private string _summary = string.Empty;
        private bool _endTurnPressed;
        private bool _directionPressed;
        private TurnDirection _pressedDirection;
#if UNITY_EDITOR
        private const int DevSoulStep = 5;
        private const int DevShieldStep = 4;
        private const int DevAttackPower = 5;
        private const string DevShieldLabel = "+4 Shield (dev)";
        private bool _devSoulsPressed;
        private int _devSoulsDelta;
        private bool _devShieldPressed;
        private bool _devAttackPressed;
        private bool _devHasTarget;
        private string _devAttackLabel = "Attack (no target)";
#endif

        private static float UiScale => Mathf.Max(1f, Screen.height / DesignHeight);

        private void OnEnable()
        {
            if (matchController != null)
                matchController.StateChanged += Rebuild;
        }

        private void OnDisable()
        {
            if (matchController != null)
                matchController.StateChanged -= Rebuild;
        }

        /// <summary>True if a screen position (origin bottom-left) is over this panel.</summary>
        public bool ContainsScreenPoint(Vector2 screenPoint)
        {
            float scale = UiScale;
            var guiPoint = new Vector2(screenPoint.x / scale, (Screen.height - screenPoint.y) / scale);
            return _panel.Contains(guiPoint);
        }

        private void Update()
        {
            if (matchController == null)
                return;

            if (_endTurnPressed)
                matchController.EndTurn();
            if (_directionPressed)
                matchController.BeginNextPhase(_pressedDirection);
#if UNITY_EDITOR
            if (_devSoulsPressed)
                matchController.DevChangeCurrentSouls(_devSoulsDelta);
            if (_devShieldPressed)
                matchController.DevAddShieldToCurrent(DevShieldStep);
            if (_devAttackPressed)
                matchController.DevAttackFirstTarget(DevAttackPower);
            _devSoulsPressed = false;
            _devShieldPressed = false;
            _devAttackPressed = false;
#endif

            _endTurnPressed = false;
            _directionPressed = false;
        }

        private void OnGUI()
        {
            if (matchController == null)
                return;

            float scale = UiScale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            GUI.Box(_panel, GUIContent.none);

            GUILayout.BeginArea(new Rect(_panel.x + 8f, _panel.y + 8f, _panel.width - 16f, _panel.height - 16f));
            GUILayout.Label(_summary);

            Match match = matchController.CurrentMatch;
            if (match != null && !match.IsOver)
            {
                if (match.IsAwaitingPhaseStart)
                {
                    if (GUILayout.Button("Turn direction: Forward"))
                    {
                        _directionPressed = true;
                        _pressedDirection = TurnDirection.Forward;
                    }
                    if (GUILayout.Button("Turn direction: Backward"))
                    {
                        _directionPressed = true;
                        _pressedDirection = TurnDirection.Backward;
                    }
                }
                else
                {
                    if (GUILayout.Button("End Turn"))
                        _endTurnPressed = true;
#if UNITY_EDITOR 
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button($"+{DevSoulStep} Souls (dev)"))
                    {
                        _devSoulsPressed = true;
                        _devSoulsDelta = DevSoulStep;
                    }
                    if (GUILayout.Button($"-{DevSoulStep} Souls (dev)"))
                    {
                        _devSoulsPressed = true;
                        _devSoulsDelta = -DevSoulStep;
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button(DevShieldLabel))
                        _devShieldPressed = true;

                    bool wasEnabled = GUI.enabled;
                    GUI.enabled = _devHasTarget;
                    if (GUILayout.Button(_devAttackLabel))
                        _devAttackPressed = true;
                    GUI.enabled = wasEnabled;
                    GUILayout.EndHorizontal();
#endif
                }
            }
            GUILayout.EndArea();
        }

        private void Rebuild()
        {
            Match match = matchController.CurrentMatch;
            _text.Clear();

            if (match == null)
            {
                _summary = "No match.";
                return;
            }

            _text.Append("Round ").Append(match.Turns.RoundNumber)
                 .Append("   Phase ").Append(match.Turns.Phase).Append('\n');

            if (match.IsOver)
            {
                switch (match.EndReason)
                {
                    case MatchEndReason.Ascension:
                        _text.Append("Match over: Apostle ").Append(match.WinnerId).Append(" Ascends and wins!\n");
                        break;
                    case MatchEndReason.LastApostleStanding:
                        _text.Append("Match over: Apostle ").Append(match.WinnerId)
                             .Append(" is the last one standing and wins!\n");
                        break;
                    default:
                        _text.Append("Match over: no winner.\n");
                        break;
                }
            }
            else if (match.IsAwaitingPhaseStart)
            {
                _text.Append("Phase over. Apostle ").Append(match.GetNextPhaseChooserId())
                     .Append(" chooses the turn direction.\n");
            }
            else
            {
                _text.Append("Apostle ").Append(match.CurrentApostle.Id).Append("'s turn   Capacity ")
                     .Append(match.CurrentCapacity.Remaining).Append('/').Append(match.CurrentCapacity.Maximum)
                     .Append('\n');
            }

            AscensionTracker ascension = match.Ascension;
            if (ascension.HasAwakened)
            {
                _text.Append("Awakened: Apostle ").Append(ascension.AwakenedId)
                     .Append("   Ascension tile (").Append(ascension.Tile.Q).Append(',')
                     .Append(ascension.Tile.R).Append(") gold\n");
            }
            else
            {
                _text.Append("Awakened: nobody (needs ").Append(GameConstants.SoulsToAwaken).Append(" Souls)\n");
            }

            for (int i = 0; i < match.Battlefield.Apostles.Count; i++)
            {
                ApostleState apostle = match.Battlefield.Apostles[i];
                _text.Append(apostle == match.CurrentApostle ? "> " : "  ")
                     .Append('A').Append(apostle.Id)
                     .Append("  Vessel ").Append(apostle.Vessel)
                     .Append("  Souls ").Append(apostle.Souls)
                     .Append("  Corr ").Append(apostle.Corruption)
                     .Append("  Shield ").Append(apostle.Shield);
                if (apostle.IsEliminated)
                    _text.Append("  (out)");
                if (ascension.HasAwakened && apostle.Id == ascension.AwakenedId)
                    _text.Append("  (Awakened)");
                _text.Append('\n');
            }

            // Who the current Apostle can attack (dark red rings on the board).
            match.GetAttackTargets(_targets);
            if (match.CurrentApostle != null)
            {
                _text.Append("Can attack: ");
                if (_targets.Count == 0)
                {
                    _text.Append("nobody adjacent");
                }
                else
                {
                    for (int i = 0; i < _targets.Count; i++)
                    {
                        if (i > 0)
                            _text.Append(", ");
                        _text.Append('A').Append(_targets[i].Id);
                    }
                }
                _text.Append('\n');
            }

            SpecialTiles specials = match.Battlefield.Specials;
            _text.Append("Tiles left: Heal ").Append(specials.CountOf(SpecialTileType.Heal))
                 .Append(" (red), Purify ").Append(specials.CountOf(SpecialTileType.Purify))
                 .Append(" (blue), Soul ").Append(specials.CountOf(SpecialTileType.Soul))
                 .Append(" (purple)\n");

            _summary = _text.ToString();

#if UNITY_EDITOR
            _devHasTarget = _targets.Count > 0;
            _devAttackLabel = _devHasTarget
                ? $"Attack A{_targets[0].Id} (ATK {DevAttackPower}, dev)"
                : "Attack (no target)";
#endif
        }
    }
}