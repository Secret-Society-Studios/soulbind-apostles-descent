using System.Collections.Generic;
using System.Text;
using SoulBind.Core;
using SoulBind.Managers;
using UnityEngine;
using UnityEngine.InputSystem;

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
        private const int CardButtonsPerRow = 2;
        private const float PanelWidth = 440f;
        private const float Margin = 10f;
        private const float MaxBoardShiftFraction = 0.6f;

        [SerializeField] private MatchController matchController;
        [Tooltip("Optional. Found automatically if left empty. The camera keeps the board clear of the panel.")]
        [SerializeField] private BoardView boardView;

        private readonly StringBuilder _text = new StringBuilder(1024);
        private readonly List<ApostleState> _targets = new List<ApostleState>(6);

        // The current hand as buttons. Rebuilt only when the state changes, not every frame.
        private readonly List<ArcanaId> _cardIds = new List<ArcanaId>(12);
        private readonly List<string> _cardLabels = new List<string>(12);

        private Rect _panel;
        private Rect _showButton = new Rect(Margin, Margin, 150f, 28f);
        private Vector2 _scroll;
        private bool _hidden;
        private bool _togglePressed;

        // The screen fraction last given to the camera, and the screen size it was worked out for.
        private float _appliedFraction = -1f;
        private int _lastScreenWidth;
        private int _lastScreenHeight;

        private string _summary = string.Empty;
        private string _strikeLabel = string.Empty;
        private bool _showStrike;
        private bool _attackMode = true;

        // Who the Nocturne combo hits. The button cycles through the living opponents.
        private readonly List<int> _opponentIds = new List<int>(GameConstants.MaxPlayers);
        private int _nocturneTargetId;
        private string _nocturneLabel = string.Empty;
        private bool _nocturneCyclePressed;

        private bool _endTurnPressed;
        private bool _directionPressed;
        private TurnDirection _pressedDirection;
        private bool _cardPressed;
        private ArcanaId _pressedCard;
        private bool _strikePressed;
#if UNITY_EDITOR
        private const int DevSoulStep = 5;
        private const int DevShieldStep = 4;
        private const string DevShieldLabel = "+4 Shield (dev)";
        private bool _devSoulsPressed;
        private int _devSoulsDelta;
        private bool _devShieldPressed;
        private bool _botsTogglePressed;
        private bool _seatCyclePressed;
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

        private void Start()
        {
            if (boardView == null && matchController != null)
                boardView = matchController.BoardView;
        }

        /// <summary>True if a screen position (origin bottom-left) is over this panel (or its Show button).</summary>
        public bool ContainsScreenPoint(Vector2 screenPoint)
        {
            float scale = UiScale;
            var guiPoint = new Vector2(screenPoint.x / scale, (Screen.height - screenPoint.y) / scale);
            return _hidden ? _showButton.Contains(guiPoint) : _panel.Contains(guiPoint);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (_togglePressed || (keyboard != null && keyboard.hKey.wasPressedThisFrame))
                _hidden = !_hidden;
            _togglePressed = false;

            UpdatePanelRect();
            KeepBoardClearOfPanel();

            if (matchController == null)
                return;

            if (_nocturneCyclePressed)
                CycleNocturneTarget();
            _nocturneCyclePressed = false;

            if (_cardPressed)
                matchController.PlayCard(_pressedCard, _attackMode, _nocturneTargetId);
            if (_strikePressed)
                matchController.Strike();
            if (_endTurnPressed)
                matchController.EndTurn();
            if (_directionPressed)
                matchController.BeginNextPhase(_pressedDirection);
#if UNITY_EDITOR
            if (_devSoulsPressed)
                matchController.DevChangeCurrentSouls(_devSoulsDelta);
            if (_devShieldPressed)
                matchController.DevAddShieldToCurrent(DevShieldStep);
            if (_botsTogglePressed)
                matchController.DevSetBots(!matchController.BotsPlayOthers);
            if (_seatCyclePressed)
                matchController.DevCycleLocalApostle();
            if (_botsTogglePressed || _seatCyclePressed)
                Rebuild();
            _devSoulsPressed = false;
            _devShieldPressed = false;
            _botsTogglePressed = false;
            _seatCyclePressed = false;
#endif

            _cardPressed = false;
            _strikePressed = false;
            _endTurnPressed = false;
            _directionPressed = false;
        }

        private void OnGUI()
        {
            if (matchController == null)
                return;

            float scale = UiScale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            if (_hidden)
            {
                if (GUI.Button(_showButton, "Show panel (H)"))
                    _togglePressed = true;
                return;
            }

            UpdatePanelRect();
            GUI.Box(_panel, GUIContent.none);

            GUILayout.BeginArea(new Rect(_panel.x + 8f, _panel.y + 8f, _panel.width - 16f, _panel.height - 16f));
            if (GUILayout.Button("Hide panel (H)"))
                _togglePressed = true;

            // Scrolls when the content is taller than the screen.
            _scroll = GUILayout.BeginScrollView(_scroll);
#if UNITY_EDITOR
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(matchController.BotsPlayOthers ? "Bots: ON (click for hot-seat)" : "Bots: OFF (click for bots)"))
                _botsTogglePressed = true;
            if (matchController.BotsPlayOthers && GUILayout.Button($"You: Apostle {matchController.ViewerId} (switch)"))
                _seatCyclePressed = true;
            GUILayout.EndHorizontal();
#endif
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
                    DrawHandButtons();

                    if (_showStrike && GUILayout.Button(_strikeLabel))
                        _strikePressed = true;
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
                    if (GUILayout.Button(DevShieldLabel))
                        _devShieldPressed = true;
                    GUILayout.EndHorizontal();
#endif
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // The panel runs down the left edge at a fixed width and fills the screen's height.
        private void UpdatePanelRect()
        {
            // Fit between the HUD's top and bottom strips, if there are any.
            float top = Margin;
            float bottom = Margin;
            if (boardView != null)
            {
                top += boardView.ReservedTopFraction * Screen.height / UiScale;
                bottom += boardView.ReservedBottomFraction * Screen.height / UiScale;
            }
            float height = Screen.height / UiScale - top - bottom;
            _panel = new Rect(Margin, top, PanelWidth, height);
            _showButton = new Rect(Margin, top, 150f, 28f);
        }

        // Moves the camera so the board sits in the space to the right of the panel.
        // Only re-fits when the panel is shown or hidden, or the screen size changes.
        private void KeepBoardClearOfPanel()
        {
            if (boardView == null || Screen.width <= 0)
                return;

            float fraction = 0f;
            if (!_hidden)
                fraction = Mathf.Min((_panel.xMax + Margin) * UiScale / Screen.width, MaxBoardShiftFraction);

            bool screenChanged = Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight;
            if (!screenChanged && Mathf.Approximately(fraction, _appliedFraction))
                return;

            boardView.SetReservedLeft(fraction);
            _appliedFraction = fraction;
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
        }

        // Keeps the Nocturne pick on a living opponent of the current Apostle.
        private void RefreshNocturneTarget(Match match)
        {
            _opponentIds.Clear();
            if (match.CurrentApostle != null)
            {
                foreach (ApostleState apostle in match.Battlefield.Apostles)
                {
                    if (apostle != match.CurrentApostle && !apostle.IsEliminated)
                        _opponentIds.Add(apostle.Id);
                }
            }

            if (_opponentIds.Count == 0)
                _nocturneTargetId = 0;
            else if (!_opponentIds.Contains(_nocturneTargetId))
                _nocturneTargetId = _opponentIds[0];

            _nocturneLabel = $"Nocturne combo target: A{_nocturneTargetId}  (click to change)";
        }

        private void CycleNocturneTarget()
        {
            if (_opponentIds.Count == 0)
                return;
            int index = _opponentIds.IndexOf(_nocturneTargetId);
            _nocturneTargetId = _opponentIds[(index + 1) % _opponentIds.Count];
            _nocturneLabel = $"Nocturne combo target: A{_nocturneTargetId}  (click to change)";
        }

        private void DrawHandButtons()
        {
            _attackMode = GUILayout.Toggle(_attackMode, " Attack with ATK cards (lowest-Id adjacent target)");
            if (_opponentIds.Count > 0 && GUILayout.Button(_nocturneLabel))
                _nocturneCyclePressed = true;

            for (int i = 0; i < _cardIds.Count; i += CardButtonsPerRow)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < i + CardButtonsPerRow && j < _cardIds.Count; j++)
                {
                    if (GUILayout.Button(_cardLabels[j]))
                    {
                        _cardPressed = true;
                        _pressedCard = _cardIds[j];
                    }
                }
                GUILayout.EndHorizontal();
            }
        }

        private void Rebuild()
        {
            Match match = matchController.CurrentMatch;
            _text.Clear();
            _cardIds.Clear();
            _cardLabels.Clear();
            _showStrike = false;

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

                switch (match.Engagement)
                {
                    case EngagementState.Free:
                        _text.Append("Play: Free (move or play cards)\n");
                        break;
                    case EngagementState.Engaged:
                        _text.Append("Play: Engaged with A").Append(match.EngagedTargetId)
                             .Append(", ATK ").Append(match.PendingAttack).Append(" (no moving; Strike to attack)\n");
                        _showStrike = true;
                        _strikeLabel = $"Strike A{match.EngagedTargetId} (ATK {match.PendingAttack})";
                        break;
                    default:
                        _text.Append("Play: Disengaged (no more cards this turn)\n");
                        break;
                }
            }

            RefreshNocturneTarget(match);

            if (match.CurrentApostle != null)
            {
                _text.Append("Combos this turn:");
                bool anyCombo = false;
                for (int e = 0; e < ElementCombo.ElementCount; e++)
                {
                    if (match.IsComboTriggered((SoulBind.Core.Element)e))
                    {
                        _text.Append(' ').Append(((SoulBind.Core.Element)e).ToString());
                        anyCombo = true;
                    }
                }
                _text.Append(anyCombo ? "\n" : " none\n");
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
                     .Append("  DEF ").Append(apostle.Defense)
                     .Append("  Hand ").Append(match.GetHand(apostle.Id).Count);
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
                    _text.Append("nobody");
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

            _text.Append("Deck ").Append(match.Deck.DrawCount)
                 .Append("   Discard ").Append(match.Deck.DiscardCount);

            // The hand header comes last, so the card buttons appear right under it.
            if (match.CurrentApostle != null)
            {
                IReadOnlyList<ArcanaId> hand = match.GetHand(match.CurrentApostle.Id);
                _text.Append("\nA").Append(match.CurrentApostle.Id).Append(" hand (").Append(hand.Count).Append(')');
                if (hand.Count > GameConstants.MaxHandSize)
                    _text.Append("  over 7!");
                _text.Append(':');

                // One button per card: name, ATK, DEF and CAP.
                for (int i = 0; i < hand.Count; i++)
                {
                    CardDefinition card = CardCatalog.Get(hand[i]);
                    _cardIds.Add(card.Id);
                    _cardLabels.Add($"{card.Name}  A{card.Attack} D{card.Defense} C{card.Cap}");
                }
            }

            _summary = _text.ToString();
        }
    }
}