using System.Collections.Generic;
using System.Text;
using SoulBind.Core;
using SoulBind.Managers;
using SoulBind.Online;
using SoulBind.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace SoulBind.UI
{
    /// <summary>
    /// Announcements over the table and the end-of-match screen.
    /// Banners (top-center, a few seconds each): an Apostle is eliminated, an Apostle is Awakened
    /// (the Ascension tile appears), an Apostle loses Awakened status.
    /// End screen: who won and how, every Apostle's final Vessel, Souls and Corruption, and
    /// Play Again / View Board buttons.
    /// During the match: a Quit button in the top-right corner, which asks before leaving to the main menu.
    /// MatchHud creates it at runtime on its Canvas, so it needs no setup. Display only.
    /// </summary>
    public sealed class MatchOverlay : MonoBehaviour
    {
        private const float BannerTime = 3.6f;  // at normal animation speed
        private const float BandOpenTime = 0.45f;
        private const float LetterSettleTime = 0.9f;
        private const float FadeTime = 0.6f;
        private const float WideSpacing = 40f;   // the gold letters start spread out and draw together
        private const float RestSpacing = 6f;
        private const float EndScreenDelay = 1.2f;   // lets the last animations finish first

        private static readonly Color PanelColor = new Color(0.07f, 0.06f, 0.10f, 0.92f);
        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.65f);
        private static readonly Color GoldColor = new Color(1f, 0.85f, 0.4f);
        private static readonly Color ButtonColor = new Color(0.22f, 0.18f, 0.30f);

        private MatchController _controller;

        // Banners
        private CanvasGroup _bannerGroup;
        private TMP_Text _bannerText;
        private RectTransform _bannerBand;
        private float _bannerLength;

        // The guide shown while Apostles pick their starting tiles.
        private CanvasGroup _guideGroup;
        private TMP_Text _guideTitle;
        private TMP_Text _guideText;
        private float _guideTime;
        private bool _guideWanted;
        private readonly Queue<string> _bannerQueue = new Queue<string>();
        private float _bannerTimer;

        // End screen
        private GameObject _endScreen;
        private TMP_Text _titleText;
        private TMP_Text _subtitleText;
        private TMP_Text _tableText;
        private GameObject _showResultButton;
        private GameObject _mainMenuButton;
        private GameObject _playAgainButton;
        private float _endScreenTimer = -1f;
        // quit button and its "are you sure?" box
        private const float QuitButtonTop = 124f;
        private GameObject _quitButton;
        private GameObject _quitConfirm;
        private TMP_Text _quitText;
        private bool _endShown;

        // What was last seen, to notice changes.
        private readonly bool[] _wasEliminated = new bool[GameConstants.MaxPlayers + 1];
        private int _lastAwakenedId;
        private int _lastDrawCount;
        private int _lastDiscardCount;
        private int _lastSwapNumber;
        private Match _seenMatch;

        public static MatchOverlay Create(Canvas canvas, MatchController controller)
        {
            var go = new GameObject("MatchOverlay", typeof(RectTransform));
            go.layer = canvas.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(canvas.transform, false);
            Stretch(rect);
            rect.SetAsLastSibling();

            MatchOverlay overlay = go.AddComponent<MatchOverlay>();
            overlay._controller = controller;
            overlay.Build(rect);
            controller.StateChanged += overlay.Refresh;
            overlay.Refresh();
            return overlay;
        }

        private void OnDestroy()
        {
            if (_controller != null)
                _controller.StateChanged -= Refresh;
        }

        // ---- Noticing what happened ----

        private void Refresh()
        {
            RefreshGuide();
            Match match = _controller != null ? _controller.CurrentMatch : null;
            if (match == null)
                return;

            int viewer = _controller.ViewerId;
            IReadOnlyList<ApostleState> apostles = match.Battlefield.Apostles;

            if (match != _seenMatch)
            {
                // A new match: remember its starting state without announcing anything.
                _seenMatch = match;
                for (int i = 0; i < apostles.Count; i++)
                    _wasEliminated[apostles[i].Id] = apostles[i].IsEliminated;
                _lastAwakenedId = match.Ascension.AwakenedId;
                _lastDrawCount = match.Deck.DrawCount;
                _lastDiscardCount = match.Deck.DiscardCount;
                _lastSwapNumber = match.LastSwap.Number;
                _endShown = false;
                _endScreenTimer = -1f;
                _endScreen.SetActive(false);
                _showResultButton.SetActive(false);
                RefreshQuit(match);
                return;
            }

            for (int i = 0; i < apostles.Count; i++)
            {
                ApostleState apostle = apostles[i];
                if (apostle.IsEliminated && !_wasEliminated[apostle.Id])
                    QueueBanner(apostle.Id == viewer ? "You have been eliminated" : $"{Name(apostle.Id)} has been eliminated");
                _wasEliminated[apostle.Id] = apostle.IsEliminated;
            }

            int awakened = match.Ascension.AwakenedId;
            if (awakened != _lastAwakenedId && !match.IsOver)
            {
                if (_lastAwakenedId != 0 && !_wasEliminated[_lastAwakenedId])
                    QueueBanner($"{Possessive(_lastAwakenedId, viewer)} Awakening fades: below {GameConstants.SoulsToAwaken} Souls");
                if (awakened != 0)
                    QueueBanner(awakened == viewer
                        ? "You are Awakened! Reach the Ascension tile to win"
                        : $"{Name(awakened)} is Awakened! The Ascension tile has appeared");
            }
            _lastAwakenedId = awakened;

            // Only a reshuffle (the draw pile ran out) ever grows the draw pile or shrinks the discard pile.
            // Watching the counts works the same for online clients, who only see snapshots.
            int drawCount = match.Deck.DrawCount;
            int discardCount = match.Deck.DiscardCount;
            if ((drawCount > _lastDrawCount || discardCount < _lastDiscardCount) && !match.IsOver)
                QueueBanner("Draw pile empty: the discard pile is shuffled into a new draw pile");
            _lastDrawCount = drawCount;
            _lastDiscardCount = discardCount;
            // Wheel of Fortune: say what changed hands (the cards only to the two Apostles involved).
            CardSwap swap = match.LastSwap;
            if (swap.Number != _lastSwapNumber)
            {
                _lastSwapNumber = swap.Number;
                if (swap.Number > 0)
                    QueueBanner(SwapText(swap, viewer));
            }

            if (match.IsOver && !_endShown && _endScreenTimer < 0f)
                _endScreenTimer = EndScreenDelay;

            RefreshQuit(match);
        }


        // ---- Quit ----

        // Quit is there while the match runs; at the end the result screen has its own Main Menu button.
        // Like that button, it needs the menu scene to be first in the Build Settings.
        private void RefreshQuit(Match match)
        {
            bool canQuit = !match.IsOver && SceneManager.GetActiveScene().buildIndex > 0;
            _quitButton.SetActive(canQuit);
            if (!canQuit)
                _quitConfirm.SetActive(false);
        }

        private void AskToQuit()
        {
            _quitText.text = _controller != null && _controller.IsOnline ? "You will leave the online match and go back to the main menu." : "This match will end and you will go back to the main menu.";
            _quitConfirm.transform.SetAsLastSibling();
            _quitConfirm.SetActive(true);
        }

        private void CancelQuit()
        {
            _quitConfirm.SetActive(false);
        }

        private void Update()
        {
            UpdateBanner(Time.unscaledDeltaTime);
            UpdateGuide(Time.unscaledDeltaTime);

            if (_endScreenTimer >= 0f)
            {
                _endScreenTimer -= Time.unscaledDeltaTime;
                if (_endScreenTimer < 0f)
                    ShowEndScreen();
            }
        }

        // ---- Starting tile guide ----

        // Before the first turn: tells the player to pick an edge tile, or who is picking now.
        private void RefreshGuide()
        {
            if (_controller == null || _guideGroup == null)
                return;
            _guideWanted = _controller.IsChoosingSpawns && _controller.SpawnPickerId != 0;
            if (!_guideWanted)
                return;

            int picker = _controller.SpawnPickerId;
            if (_controller.IsLocalSpawnPick)
            {
                _guideTitle.text = _controller.BotsPlayOthers ? "Pick an edge tile" : Name(picker) + ": pick an edge tile";
                _guideText.text = "Click a highlighted tile on the edge of the board to choose where you start.";
            }
            else
            {
                _guideTitle.text = Name(picker) + " is choosing";
                _guideText.text = "Each Apostle picks a starting tile on the edge, in turn order.";
            }
        }

        // Fades in and out, and breathes gently while it waits for a pick.
        private void UpdateGuide(float delta)
        {
            if (_guideGroup == null)
                return;
            float target = _guideWanted ? 1f : 0f;
            float speed = delta / MotionStyle.Seconds(0.4f);
            _guideGroup.alpha = Mathf.MoveTowards(_guideGroup.alpha, target, speed);
            _guideGroup.gameObject.SetActive(_guideGroup.alpha > 0f || _guideWanted);
            if (!_guideWanted)
                return;
            _guideTime += delta * Mathf.Max(0.1f, MotionStyle.Speed);
            float wave = (Mathf.Sin(_guideTime * Mathf.PI * 2f / 2.4f) + 1f) / 2f;
            _guideTitle.alpha = Mathf.Lerp(0.75f, 1f, wave);
        }

        // ---- Banners ----

        private void QueueBanner(string text)
        {
            _bannerQueue.Enqueue(text);
        }

        private void UpdateBanner(float delta)
        {
            if (_bannerTimer <= 0f)
            {
                if (_bannerQueue.Count == 0)
                {
                    _bannerGroup.alpha = 0f;
                    return;
                }
                _bannerText.text = _bannerQueue.Dequeue();
                _bannerLength = MotionStyle.Seconds(BannerTime);
                _bannerTimer = _bannerLength;
            }

            _bannerTimer -= delta;
            float speed = Mathf.Max(0.1f, MotionStyle.Speed);
            float shown = (_bannerLength - _bannerTimer) * speed;   // in seconds at normal speed
            float left = Mathf.Max(0f, _bannerTimer) * speed;

            // The dark band opens from the middle, the gold letters fade in spread wide and draw
            // together, and at the end everything fades slowly.
            float open = MotionStyle.Out(shown / BandOpenTime);
            _bannerBand.localScale = new Vector3(open, 1f, 1f);
            _bannerText.characterSpacing = Mathf.Lerp(WideSpacing, RestSpacing, MotionStyle.Out(shown / LetterSettleTime));
            float textIn = Mathf.Clamp01((shown - BandOpenTime * 0.5f) / BandOpenTime);
            _bannerText.alpha = textIn;
            _bannerGroup.alpha = left < FadeTime ? MotionStyle.InOut(left / FadeTime) : 1f;
        }

        // ---- End screen ----

        private void ShowEndScreen()
        {
            Match match = _controller.CurrentMatch;
            if (match == null || !match.IsOver)
                return;

            _endShown = true;
            int viewer = _controller.ViewerId;
            int winner = match.WinnerId;
            bool youWon = winner != 0 && winner == viewer && _controller.BotsPlayOthers;

            switch (match.EndReason)
            {
                case MatchEndReason.Ascension:
                    _titleText.text = youWon ? "You Ascended!" : $"{Name(winner)} Ascends!";
                    _subtitleText.text = youWon
                        ? "You reached the Ascension tile while Awakened."
                        : $"{Name(winner)} reached the Ascension tile while Awakened.";
                    break;
                case MatchEndReason.LastApostleStanding:
                    _titleText.text = youWon ? "Victory!" : $"{Name(winner)} Wins!";
                    _subtitleText.text = youWon
                        ? "You are the last Apostle standing."
                        : $"{Name(winner)} is the last Apostle standing.";
                    break;
                default:
                    _titleText.text = "No Survivors";
                    _subtitleText.text = "The last Apostles fell together. Nobody wins.";
                    break;
            }
            if (!youWon && winner != 0 && _controller.BotsPlayOthers)
                _subtitleText.text += "  You were defeated.";
            _titleText.color = youWon ? GoldColor : Color.white;

            _tableText.text = BuildSummary(match, viewer);
            // Main Menu only makes sense when the menu scene is in the build, before this one.
            _mainMenuButton.SetActive(SceneManager.GetActiveScene().buildIndex > 0);
            // Online, a new match starts from the room screen instead.
            _playAgainButton.SetActive(!_controller.IsOnline);
            _endScreen.SetActive(true);
            _showResultButton.SetActive(false);
        }

        private static string BuildSummary(Match match, int viewer)
        {
            var text = new StringBuilder(512);
            text.Append("Rounds played: ").Append(match.Turns.RoundNumber)
                .Append("    Phase ").Append(match.Turns.Phase).Append("\n\n");
            text.Append("<mspace=0.6em>");
            text.Append("Apostle       Vessel  Souls  Corruption\n");
            IReadOnlyList<ApostleState> apostles = match.Battlefield.Apostles;
            for (int i = 0; i < apostles.Count; i++)
            {
                ApostleState apostle = apostles[i];
                string color = ColorUtility.ToHtmlStringRGB(ApostleColors.Get(apostle.Id));
                string name = Fit(Name(apostle.Id), 11);
                string status = apostle.Id == match.WinnerId ? "  winner" : apostle.IsEliminated ? "  out" : string.Empty;
                if (apostle.Id == viewer)
                    status += "  (you)";
                text.Append("<color=#").Append(color).Append('>').Append(name.PadRight(12)).Append("</color>")
                    .Append(apostle.Vessel.ToString().PadLeft(7))
                    .Append(apostle.Souls.ToString().PadLeft(7))
                    .Append(apostle.Corruption.ToString().PadLeft(12))
                    .Append(status).Append('\n');
            }
            text.Append("</mspace>");
            return text.ToString();
        }

        private void PlayAgain()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0)
            {
                SceneManager.LoadScene(scene.buildIndex);
                return;
            }
#if UNITY_EDITOR
            // A scene that is not in the Build Settings can still be reloaded in the Editor.
            EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Debug.LogError("MatchOverlay: add this scene to File > Build Settings so Play Again can reload it.");
#endif
        }

        // The main menu is the first scene in the Build Settings.
        private void MainMenu()
        {
            OnlineSession.LeaveIfActive();
            SceneManager.LoadScene(0);
        }

        private void ViewBoard()
        {
            _endScreen.SetActive(false);
            _showResultButton.SetActive(true);
        }

        private void ShowResultAgain()
        {
            _endScreen.SetActive(true);
            _showResultButton.SetActive(false);
        }

        private static string Name(int id) => PlayerNames.Get(id);

        // cuts a long name so the result table stays lined up.
        private static string Fit(string name, int max) => name.Length <= max ? name : name.Substring(0, max - 1) + ".";

        private static string SwapText(CardSwap swap, int viewer)
        {
            bool known = swap.CardsKnown && (viewer == swap.PlayerId || viewer == swap.TargetId);
            if (viewer == swap.PlayerId)
                return known ? $"Wheel of Fortune: gave {CardName(swap.Given)}, got {CardName(swap.Received)}" : $"Wheel of Fortune: you swapped a card with {Name(swap.TargetId)}";
            if (viewer == swap.TargetId)
                return known ? $"{Name(swap.PlayerId)} took {CardName(swap.Received)}, gave you {CardName(swap.Given)}" : $"{Name(swap.PlayerId)} swapped a card with you";
            return $"{Name(swap.PlayerId)} swapped a card with {Name(swap.TargetId)}";
        }

        private static string CardName(ArcanaId card) => CardCatalog.Get(card).Name;
        private static string Possessive(int id, int viewer) => id == viewer ? "Your" : Name(id) + "'s";

        // ---- Building the UI (once, at runtime) ----

        private void Build(RectTransform root)
        {
            // Banner: top-center, below the portraits. Never catches the mouse.
            RectTransform banner = CreateRect("Banner", root);
            banner.anchorMin = banner.anchorMax = new Vector2(0.5f, 1f);
            banner.pivot = new Vector2(0.5f, 1f);
            banner.anchoredPosition = new Vector2(0f, -150f);
            banner.sizeDelta = new Vector2(1100f, 76f);
            _bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            _bannerGroup.alpha = 0f;
            _bannerGroup.blocksRaycasts = false;

            // The band (which opens sideways), with a thin gold line along its top and bottom edges.
            _bannerBand = CreateRect("Band", banner);
            Stretch(_bannerBand);
            AddImage(_bannerBand, new Color(0.03f, 0.02f, 0.05f, 0.9f), false);
            for (int lineIndex = 0; lineIndex < 2; lineIndex++)
            {
                RectTransform line = CreateRect(lineIndex == 0 ? "TopLine" : "BottomLine", _bannerBand);
                line.anchorMin = new Vector2(0f, lineIndex == 0 ? 1f : 0f);
                line.anchorMax = new Vector2(1f, lineIndex == 0 ? 1f : 0f);
                line.pivot = new Vector2(0.5f, lineIndex == 0 ? 1f : 0f);
                line.sizeDelta = new Vector2(0f, 2f);
                line.anchoredPosition = Vector2.zero;
                AddImage(line, new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0.7f), false);
            }

            _bannerText = CreateText("Text", banner, 32f, FontStyles.Bold | FontStyles.UpperCase);
            Stretch(_bannerText.rectTransform);
            _bannerText.color = GoldColor;

            // The starting tile guide: the same dark band, with a line of help under the title.
            RectTransform guide = CreateRect("SpawnGuide", root);
            guide.anchorMin = guide.anchorMax = new Vector2(0.5f, 1f);
            guide.pivot = new Vector2(0.5f, 1f);
            // Near the top edge: the portraits only appear once the match starts, so the space is free.
            guide.anchoredPosition = new Vector2(0f, -20f);
            guide.sizeDelta = new Vector2(1100f, 96f);
            AddImage(guide, new Color(0.03f, 0.02f, 0.05f, 0.9f), false);
            for (int lineIndex = 0; lineIndex < 2; lineIndex++)
            {
                RectTransform line = CreateRect(lineIndex == 0 ? "TopLine" : "BottomLine", guide);
                line.anchorMin = new Vector2(0f, lineIndex == 0 ? 1f : 0f);
                line.anchorMax = new Vector2(1f, lineIndex == 0 ? 1f : 0f);
                line.pivot = new Vector2(0.5f, lineIndex == 0 ? 1f : 0f);
                line.sizeDelta = new Vector2(0f, 2f);
                line.anchoredPosition = Vector2.zero;
                AddImage(line, new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0.7f), false);
            }
            _guideGroup = guide.gameObject.AddComponent<CanvasGroup>();
            _guideGroup.alpha = 0f;
            _guideGroup.blocksRaycasts = false;
            _guideTitle = CreateText("Title", guide, 32f, FontStyles.Bold | FontStyles.UpperCase);
            _guideTitle.rectTransform.anchorMin = new Vector2(0f, 0.42f);
            _guideTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            _guideTitle.rectTransform.offsetMin = Vector2.zero;
            _guideTitle.rectTransform.offsetMax = Vector2.zero;
            _guideTitle.color = GoldColor;
            _guideTitle.characterSpacing = RestSpacing;
            _guideText = CreateText("Help", guide, 20f, FontStyles.Normal);
            _guideText.rectTransform.anchorMin = new Vector2(0f, 0f);
            _guideText.rectTransform.anchorMax = new Vector2(1f, 0.45f);
            _guideText.rectTransform.offsetMin = Vector2.zero;
            _guideText.rectTransform.offsetMax = Vector2.zero;
            _guideText.color = new Color(1f, 1f, 1f, 0.75f);
            guide.gameObject.SetActive(false);

            // End screen: dims everything and blocks clicks.
            RectTransform end = CreateRect("EndScreen", root);
            Stretch(end);
            AddImage(end, DimColor, true);
            _endScreen = end.gameObject;

            RectTransform panel = CreateRect("Panel", end);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(760f, 560f);
            AddImage(panel, PanelColor, true);
            Outline edge = panel.gameObject.AddComponent<Outline>();
            edge.effectColor = GoldColor;
            edge.effectDistance = new Vector2(3f, -3f);

            _titleText = CreateText("Title", panel, 60f, FontStyles.Bold);
            Row(_titleText.rectTransform, 30f, 80f);
            _subtitleText = CreateText("Subtitle", panel, 24f, FontStyles.Normal);
            Row(_subtitleText.rectTransform, 112f, 40f);
            _tableText = CreateText("Summary", panel, 22f, FontStyles.Normal);
            Row(_tableText.rectTransform, 170f, 280f);
            _tableText.alignment = TextAlignmentOptions.Top;

            _playAgainButton = CreateButton("PlayAgainButton", panel, "Play Again", new Vector2(-245f, 50f), PlayAgain);
            CreateButton("ViewBoardButton", panel, "View Board", new Vector2(0f, 50f), ViewBoard);
            _mainMenuButton = CreateButton("MainMenuButton", panel, "Main Menu", new Vector2(245f, 50f), MainMenu);
            _endScreen.SetActive(false);

            // After View Board: a small button to bring the result back.
            RectTransform again = CreateRect("ShowResult", root);
            again.anchorMin = again.anchorMax = new Vector2(0.5f, 1f);
            again.pivot = new Vector2(0.5f, 1f);
            again.anchoredPosition = new Vector2(0f, -150f);
            again.sizeDelta = new Vector2(260f, 52f);
            _showResultButton = again.gameObject;
            AddImage(again, ButtonColor, true);
            again.gameObject.AddComponent<Button>().onClick.AddListener(ShowResultAgain);
            TMP_Text label = CreateText("Label", again, 22f, FontStyles.Bold);
            Stretch(label.rectTransform);
            label.text = "Show Result";
            _showResultButton.SetActive(false);

            BuildQuit(root);
        }

        private void BuildQuit(RectTransform root)
        {
            // The small Quit button, top-right, left of the turn info box.
            RectTransform quit = CreateRect("QuitButton", root);
            quit.anchorMin = quit.anchorMax = new Vector2(1f, 1f);
            quit.pivot = new Vector2(1f, 1f);
            quit.anchoredPosition = new Vector2(-20f, -QuitButtonTop);
            quit.sizeDelta = new Vector2(100f, 56f);
            AddImage(quit, ButtonColor, true);
            Button quitButton = quit.gameObject.AddComponent<Button>();
            quitButton.onClick.AddListener(AskToQuit);
            TMP_Text quitLabel = CreateText("Label", quit, 24f, FontStyles.Bold);
            Stretch(quitLabel.rectTransform);
            quitLabel.text = "Quit";
            GothicTheme.StyleButton(quitButton);
            _quitButton = quit.gameObject;

            // "Leave the match?": dims everything and blocks clicks. Tapping outside the box means Stay.
            RectTransform confirm = CreateRect("QuitConfirm", root);
            Stretch(confirm);
            AddImage(confirm, DimColor, true);
            Button outside = confirm.gameObject.AddComponent<Button>();
            outside.transition = Selectable.Transition.None;
            outside.onClick.AddListener(CancelQuit);
            _quitConfirm = confirm.gameObject;

            RectTransform box = CreateRect("Box", confirm);
            box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
            box.sizeDelta = new Vector2(640f, 300f);
            AddImage(box, PanelColor, true); // catches taps, so they don't reach the dim behind it
            Outline edge = box.gameObject.AddComponent<Outline>();
            edge.effectColor = GoldColor;
            edge.effectDistance = new Vector2(3f, -3f);

            TMP_Text title = CreateText("Title", box, 40f, FontStyles.Bold);
            Row(title.rectTransform, 24f, 56f);
            title.text = "Leave the match?";
            title.color = GoldColor;
            _quitText = CreateText("Text", box, 24f, FontStyles.Normal);
            Row(_quitText.rectTransform, 90f, 80f);

            // The version, small in the corner, so testers can say which APK they are playing.
            TMP_Text version = CreateText("Version", box, 18f, FontStyles.Normal);
            version.rectTransform.anchorMin = version.rectTransform.anchorMax = version.rectTransform.pivot = new Vector2(1f, 1f);
            version.rectTransform.anchoredPosition = new Vector2(-12f, -8f);
            version.rectTransform.sizeDelta = new Vector2(200f, 26f);
            version.alignment = TextAlignmentOptions.TopRight;
            version.color = new Color(1f, 1f, 1f, 0.45f);
            version.text = "v" + Application.version;

            CreateButton("StayButton", box, "Stay", new Vector2(-125f, 0f), CancelQuit);
            CreateButton("LeaveButton", box, "Leave", new Vector2(125f, 0f), MainMenu);
            _quitConfirm.SetActive(false);
        }

        private GameObject CreateButton(string name, RectTransform parent, string label, Vector2 position,
            UnityEngine.Events.UnityAction onClick)
        {
            RectTransform rect = CreateRect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(position.x, 30f);
            rect.sizeDelta = new Vector2(220f, 60f);
            AddImage(rect, ButtonColor, true);
            rect.gameObject.AddComponent<Button>().onClick.AddListener(onClick);
            TMP_Text text = CreateText("Label", rect, 26f, FontStyles.Bold);
            Stretch(text.rectTransform);
            text.text = label;
            return rect.gameObject;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // A full-width row a given distance below the panel's top.
        private static void Row(RectTransform rect, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(30f, -top - height);
            rect.offsetMax = new Vector2(-30f, -top);
        }

        private static Image AddImage(RectTransform rect, Color color, bool catchesClicks)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = catchesClicks;
            return image;
        }

        private static TMP_Text CreateText(string name, RectTransform parent, float size, FontStyles style)
        {
            RectTransform rect = CreateRect(name, parent);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }
    }
}