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
    /// MatchHud creates it at runtime on its Canvas, so it needs no setup. Display only.
    /// </summary>
    public sealed class MatchOverlay : MonoBehaviour
    {
        private const float BannerTime = 2.8f;
        private const float FadeTime = 0.25f;
        private const float EndScreenDelay = 1.2f;   // lets the last animations finish first

        private static readonly Color PanelColor = new Color(0.07f, 0.06f, 0.10f, 0.92f);
        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.65f);
        private static readonly Color GoldColor = new Color(1f, 0.85f, 0.4f);
        private static readonly Color ButtonColor = new Color(0.22f, 0.18f, 0.30f);

        private MatchController _controller;

        // Banners
        private CanvasGroup _bannerGroup;
        private TMP_Text _bannerText;
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
        private bool _endShown;

        // What was last seen, to notice changes.
        private readonly bool[] _wasEliminated = new bool[GameConstants.MaxPlayers + 1];
        private int _lastAwakenedId;
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
                _endShown = false;
                _endScreenTimer = -1f;
                _endScreen.SetActive(false);
                _showResultButton.SetActive(false);
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

            if (match.IsOver && !_endShown && _endScreenTimer < 0f)
                _endScreenTimer = EndScreenDelay;
        }

        private void Update()
        {
            UpdateBanner(Time.unscaledDeltaTime);

            if (_endScreenTimer >= 0f)
            {
                _endScreenTimer -= Time.unscaledDeltaTime;
                if (_endScreenTimer < 0f)
                    ShowEndScreen();
            }
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
                _bannerTimer = BannerTime;
            }

            _bannerTimer -= delta;
            float shown = BannerTime - _bannerTimer;
            _bannerGroup.alpha = Mathf.Clamp01(Mathf.Min(shown, _bannerTimer) / FadeTime);
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
                string name = apostle.Id == viewer ? $"A{apostle.Id} (you)" : $"A{apostle.Id}";
                string status = apostle.Id == match.WinnerId ? "  winner" : apostle.IsEliminated ? "  out" : string.Empty;
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

        private static string Name(int id) => $"Apostle {id}";

        private static string Possessive(int id, int viewer) => id == viewer ? "Your" : $"Apostle {id}'s";

        // ---- Building the UI (once, at runtime) ----

        private void Build(RectTransform root)
        {
            // Banner: top-center, below the portraits. Never catches the mouse.
            RectTransform banner = CreateRect("Banner", root);
            banner.anchorMin = banner.anchorMax = new Vector2(0.5f, 1f);
            banner.pivot = new Vector2(0.5f, 1f);
            banner.anchoredPosition = new Vector2(0f, -150f);
            banner.sizeDelta = new Vector2(900f, 64f);
            AddImage(banner, PanelColor, false);
            _bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            _bannerGroup.alpha = 0f;
            _bannerGroup.blocksRaycasts = false;
            _bannerText = CreateText("Text", banner, 30f, FontStyles.Bold);
            Stretch(_bannerText.rectTransform);
            _bannerText.color = GoldColor;

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