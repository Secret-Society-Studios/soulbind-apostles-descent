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

        private readonly StringBuilder _text = new StringBuilder(512);
        private Rect _panel = new Rect(10f, 10f, 400f, 380f);
        private string _summary = string.Empty;
        private bool _endTurnPressed;
        private bool _directionPressed;
        private TurnDirection _pressedDirection;

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
                else if (GUILayout.Button("End Turn"))
                {
                    _endTurnPressed = true;
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
                if (match.WinnerId == 0)
                    _text.Append("Match over: no winner.\n");
                else
                    _text.Append("Match over: Apostle ").Append(match.WinnerId).Append(" wins!\n");
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

            for (int i = 0; i < match.Battlefield.Apostles.Count; i++)
            {
                ApostleState apostle = match.Battlefield.Apostles[i];
                _text.Append(apostle == match.CurrentApostle ? "> " : "  ")
                     .Append('A').Append(apostle.Id)
                     .Append("  Vessel ").Append(apostle.Vessel)
                     .Append("  Souls ").Append(apostle.Souls)
                     .Append("  Corr ").Append(apostle.Corruption);
                if (apostle.IsEliminated)
                    _text.Append("  (out)");
                _text.Append('\n');
            }

            SpecialTiles specials = match.Battlefield.Specials;
            _text.Append("Tiles left: Heal ").Append(specials.CountOf(SpecialTileType.Heal))
                 .Append(" (red), Purify ").Append(specials.CountOf(SpecialTileType.Purify))
                 .Append(" (blue), Soul ").Append(specials.CountOf(SpecialTileType.Soul))
                 .Append(" (purple)\n");

            _summary = _text.ToString();
        }
    }
}