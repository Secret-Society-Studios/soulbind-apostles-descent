using SoulBind.Managers;
using SoulBind.Online;
using SoulBind.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Kit = SoulBind.UI.UiKit;

namespace SoulBind.UI
{
    /// <summary>
    /// The start screens, built at runtime to match the SoulBind UI Screens design canvas:
    /// Main (this file), Play Online (MainMenuView.Online.cs) and the Lobby (MainMenuView.Lobby.cs).
    /// Each screen is laid out on a 1280 x 720 design frame that scales to the screen, using the
    /// design's own numbers. The menu scene needs no hand setup (SoulBind > Create Main Menu Scene).
    /// </summary>
    public sealed partial class MainMenuView : MonoBehaviour
    {
        [Tooltip("The match scene Play loads. It must be in File > Build Settings.")]
        [SerializeField] private string gameSceneName = "SampleScene";
        [SerializeField, Range(2, 6)] private int defaultPlayers = 4;

        private enum Screen { Main, Singleplayer, Online, Lobby, Collections, Settings }

        private Screen _screen;
        private int _players;
        private bool _busy;
        private RectTransform _root;
        private GameObject _mainScreen;
        private TMP_Text _mainPlayers;
        private TMP_Text _botsLine;
        private TMP_Text _mainStatus;

        // Profile (top-left on the main screen)
        private TMP_InputField _nameField;
        private GameObject _profilePopup;
        private TMP_Text _profileName;
        private TMP_Text _profileInitial;

        private void Start()
        {
            _players = Mathf.Clamp(defaultPlayers, 2, 6);
            _root = (RectTransform)transform;
            Build();

            OnlineSession session = OnlineSession.Instance;
            if (session != null && session.InSession && session.MatchStarted)
                session.Leave(); // back from an online match
            if (OnlineSession.LastMessage != null)
            {
                _mainStatus.text = OnlineSession.LastMessage;
                OnlineSession.LastMessage = null;
            }
            ShowPlayers();
            ShowScreen(Screen.Main);
        }

        private void Update()
        {
            OnlineSession session = OnlineSession.Instance;
            bool inRoom = session != null && session.InSession;
            if (inRoom && _screen != Screen.Lobby)
                ShowScreen(Screen.Lobby);
            else if (!inRoom && _screen == Screen.Lobby)
            {
                // The room ended: the host left, or the connection dropped.
                if (OnlineSession.LastMessage != null)
                {
                    ShowOnlineStatus(OnlineSession.LastMessage);
                    OnlineSession.LastMessage = null;
                }
                ShowScreen(Screen.Online);
            }

            if (_screen == Screen.Lobby)
                ShowLobby(session, false);
            else if (_screen == Screen.Online)
                UpdateRoomList();
        }

        // Shows one menu screen and hides the rest.
        private void ShowScreen(Screen screen)
        {
            _screen = screen;
            _mainScreen.SetActive(screen == Screen.Main);
            _singleScreen.SetActive(screen == Screen.Singleplayer);
            _collectionsScreen.SetActive(screen == Screen.Collections);
            _settingsScreen.SetActive(screen == Screen.Settings);
            _onlineScreen.SetActive(screen == Screen.Online);
            _lobbyScreen.SetActive(screen == Screen.Lobby);
            if (screen == Screen.Online)
                OnOpenOnline();
            else if (screen == Screen.Lobby)
            {
                _lobbyStatus.text = string.Empty;
                ShowLobby(OnlineSession.Instance, true);
            }
            if (screen == Screen.Main)
                ShowProfileName();
        }

        // ---------- Main screen actions ----------

        // Changes the player count for a local match, from 2 to 6.
        private void ChangePlayers(int delta)
        {
            _players = Mathf.Clamp(_players + delta, 2, 6);
            ShowPlayers();
        }

        // Shows the player count and who the bots play.
        private void ShowPlayers()
        {
            string count = _players.ToString();
            _mainPlayers.text = count;
            _botsLine.text = $"You play Apostle 1 · bots play the other {_players - 1}";
        }

        // Starts a local match where bots play the other seats.
        private void PlayBots() => Play(true);

        // Pass & Play: everyone takes turns on this device; the view follows whoever's turn it is.
        private void PassAndPlay() => Play(false);

        // Saves the match settings and loads the match scene.
        private void Play(bool bots)
        {
            MatchSettings.Set(_players, bots, true);
            SceneManager.LoadScene(gameSceneName);
        }

        // The tutorial: one guided match against a bot, whatever the Apostles count says.
        private void PlayTutorial()
        {
            MatchSettings.SetTutorial();
            SceneManager.LoadScene(gameSceneName);
        }

        // Quits the game, or stops Play mode in the editor.
        private void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---------- Building ----------

        // Builds the background and every menu screen once.
        private void Build()
        {
            RectTransform background = Kit.Stretch(Kit.CreateRect("Background", _root));
            Image fill = background.gameObject.AddComponent<Image>();
            fill.color = Kit.Background;

            BuildMain();
            BuildSingleplayer();
            BuildCollections();
            BuildSettings();
            BuildOnline();
            BuildLobby();
            BuildProfilePopup();
        }

        // The two thin frame lines just inside the edge of every screen.
        private static void AddFrameLines(RectTransform frame, bool inner)
        {
            RectTransform outer = Kit.Box(frame, "FrameLine", Color.clear, Kit.BronzeLine, 6f);
            Kit.Stretch(outer, 18f);
            if (!inner)
                return;
            RectTransform line = Kit.Box(frame, "FrameLineInner", Color.clear, Kit.Hex(0x2C2236), 4f);
            Kit.Stretch(line, 24f);
        }

        // Builds the main screen: profile card, play buttons and the rest.
        private void BuildMain()
        {
            RectTransform frame = Kit.CreateFrame(_root, "MainScreen");
            _mainScreen = frame.gameObject;
            AddFrameLines(frame, true);
            BuildProfileCard(frame);

            // Emblem: two rings, then the title and subtitle.
            RectTransform ring = Kit.Circle(frame, "Emblem", 56f, Color.clear, Kit.Gold, 2f);
            Kit.At(ring, 612f, 52f, 56f, 56f);
            RectTransform inner = Kit.Circle(ring, "Inner", 32f, Color.clear, Kit.GoldLine, 2f);
            Kit.At(inner, 12f, 12f, 32f, 32f);

            TMP_Text title = Kit.Label(frame, "Title", "SoulBind", 84f, Kit.DisplayHeavy, Kit.Title);
            Kit.At(title.rectTransform, 0f, 112f, 1280f, 92f);
            title.characterSpacing = 4f;
            TMP_Text subtitle = Kit.Label(frame, "Subtitle", "APOSTLE'S DESCENT", 22f, Kit.Display, Kit.Soft);
            Kit.At(subtitle.rectTransform, 0f, 206f, 1280f, 32f);
            subtitle.characterSpacing = 33f;

            // The column, top to bottom. Each opens its own screen, except Tutorial and Quit.
            RectTransform column = Kit.At(Kit.CreateRect("Column", frame), 430f, 268f, 420f, 350f);
            AddMenuButton(column, 0, "Singleplayer", Kit.ButtonStyle.Primary, () => ShowScreen(Screen.Singleplayer));
            AddMenuButton(column, 1, "Multiplayer", Kit.ButtonStyle.Outline, () => ShowScreen(Screen.Online));
            // A practice match against one bot, with a coach, before playing for real.
            AddMenuButton(column, 2, "Tutorial", Kit.ButtonStyle.Secondary, PlayTutorial);
            AddMenuButton(column, 3, "Collections", Kit.ButtonStyle.Secondary, () => ShowScreen(Screen.Collections));
            AddMenuButton(column, 4, "Settings", Kit.ButtonStyle.Secondary, () => ShowScreen(Screen.Settings));
            AddMenuButton(column, 5, "Quit", Kit.ButtonStyle.Ghost, Quit);

            _mainStatus = Kit.Label(frame, "Status", "", 14f, Kit.Inter, Kit.Gold);
            Kit.At(_mainStatus.rectTransform, 330f, 630f, 620f, 24f);

            TMP_Text version = Kit.Label(frame, "Version", "v" + Application.version, 13f, Kit.Inter, Kit.Muted,
                TextAlignmentOptions.Right);
            Kit.At(version.rectTransform, 724f, 662f, 500f, 20f);
        }

        /// <summary>‹ count ›, the left button at x, y. Returns the count's text.</summary>
        private static TMP_Text BuildStepper(RectTransform parent, float x, float y, System.Action<int> change)
        {
            TMP_Text fewer = Kit.Button(parent, "Fewer", "‹", Kit.ButtonStyle.Secondary, () => change(-1), 26f, 8f);
            Kit.At((RectTransform)fewer.transform.parent, x, y, 44f, 44f);
            TMP_Text count = Kit.Label(parent, "Count", "4", 28f, Kit.Display, Kit.Text);
            Kit.At(count.rectTransform, x + 50f, y, 44f, 44f);
            TMP_Text more = Kit.Button(parent, "More", "›", Kit.ButtonStyle.Secondary, () => change(1), 26f, 8f);
            Kit.At((RectTransform)more.transform.parent, x + 100f, y, 44f, 44f);
            return count;
        }

        /// <summary>A round back button at the design's top-left spot.</summary>
        private static void BuildBackButton(RectTransform frame, UnityEngine.Events.UnityAction onClick)
        {
            TMP_Text back = Kit.Button(frame, "Back", "‹", Kit.ButtonStyle.Secondary, onClick, 34f, 26f);
            var rect = (RectTransform)back.transform.parent;
            Kit.At(rect, 56f, 44f, 52f, 52f);
            rect.GetComponent<Image>().color = Kit.Panel;
            back.color = Kit.Gold;
        }

        // ---------- Profile ----------

        // Top-left: your avatar and name. Tap it to change the name.
        private void BuildProfileCard(RectTransform frame)
        {
            RectTransform card = Kit.Box(frame, "Profile", Kit.Panel, Kit.GoldLine, 36f);
            Kit.At(card, 56f, 44f, 280f, 72f);
            card.gameObject.AddComponent<Button>().onClick.AddListener(OpenProfile);

            RectTransform avatar = Kit.Circle(card, "Avatar", 52f, Kit.Gold, Kit.GoldBright, 2f);
            Kit.At(avatar, 10f, 10f, 52f, 52f);
            avatar.GetComponent<Image>().raycastTarget = false;
            _profileInitial = Kit.Label(avatar, "Initial", "?", 24f, Kit.Display, Kit.Background);
            Kit.Stretch(_profileInitial.rectTransform);

            _profileName = Kit.Label(card, "Name", "", 18f, Kit.InterBold, Kit.Text, TextAlignmentOptions.Left);
            Kit.At(_profileName.rectTransform, 76f, 12f, 190f, 26f);
            _profileName.textWrappingMode = TextWrappingModes.NoWrap;
            _profileName.overflowMode = TextOverflowModes.Ellipsis;
            TMP_Text hint = Kit.Label(card, "Hint", "Tap for your profile and stats", 12f, Kit.Inter, Kit.Muted,
                TextAlignmentOptions.Left);
            Kit.At(hint.rectTransform, 76f, 40f, 190f, 18f);
        }

        // The profile page: your name (type to change it) and your stats. Tapping outside cancels.
        private void BuildProfilePopup()
        {
            RectTransform cover = Kit.Stretch(Kit.CreateRect("ProfilePopup", _root));
            Image dim = cover.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.7f);
            Button outside = cover.gameObject.AddComponent<Button>();
            outside.transition = Selectable.Transition.None;
            outside.onClick.AddListener(CloseProfile);
            _profilePopup = cover.gameObject;

            RectTransform frame = Kit.CreateFrame(cover, "Frame");
            RectTransform panel = Kit.Box(frame, "Panel", Kit.Panel, Kit.GoldLine, 14f);
            Kit.At(panel, 280f, 70f, ProfileWidth, 580f);
            // A click on the panel would bubble up to the cover's Button and close the page,
            // so the panel takes its own clicks with a Button that does nothing.
            panel.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;

            TMP_Text title = Kit.Label(panel, "Title", "Profile", 30f, Kit.Display, Kit.Title);
            Kit.At(title.rectTransform, 0f, 22f, ProfileWidth, 40f);

            TMP_Text nameLabel = Kit.Label(panel, "NameLabel", "YOUR NAME", 12f, Kit.InterBold, Kit.Muted,
                TextAlignmentOptions.Left);
            Kit.At(nameLabel.rectTransform, 60f, 72f, 300f, 18f);
            _nameField = CreateInputField(panel, "NameField", "Type your name", 26f, Kit.InterSemiBold, 0f);
            Kit.At((RectTransform)_nameField.transform, 60f, 94f, 600f, 60f);
            _nameField.characterLimit = PlayerNames.MaxLength;
            _nameField.onValidateInput = (string _, int __, char c) => PlayerNames.IsAllowed(c) ? c : '\0';
            _nameField.onSubmit.AddListener(_ => SaveProfile());

            BuildProfileStats(panel);

            TMP_Text cancel = Kit.Button(panel, "Cancel", "Cancel", Kit.ButtonStyle.Ghost, CloseProfile, 16f);
            Kit.At((RectTransform)cancel.transform.parent, 60f, 500f, 290f, 56f);
            TMP_Text save = Kit.Button(panel, "Save", "Save", Kit.ButtonStyle.Primary, SaveProfile, 20f);
            Kit.At((RectTransform)save.transform.parent, 370f, 500f, 290f, 56f);

            _profilePopup.SetActive(false);
        }

        // Opens the profile popup with the saved name and stats.
        private void OpenProfile()
        {
            _nameField.text = PlayerNames.LocalName;
            ShowProfileStats();
            _profilePopup.transform.SetAsLastSibling();
            _profilePopup.SetActive(true);
        }

        // Saves the typed name and closes the popup.
        private void SaveProfile()
        {
            PlayerNames.LocalName = _nameField.text;
            CloseProfile();
        }

        // Closes the profile popup and refreshes the name on the main screen.
        private void CloseProfile()
        {
            _profilePopup.SetActive(false);
            ShowProfileName();
        }

        // Shows the saved name, or a prompt to set one.
        private void ShowProfileName()
        {
            string typed = PlayerNames.LocalName;
            bool named = typed.Length > 0;
            _profileName.text = named ? typed : "Set your name";
            _profileInitial.text = named ? typed.Substring(0, 1).ToUpperInvariant() : "?";
            if (_playingAs != null)
                _playingAs.text = $"Playing as <color=#F5EBD8><b>{(named ? typed : "Apostle")}</b></color>";
        }

        // ---------- Shared pieces ----------

        // A text box. On phones, tapping it opens the keyboard.
        private static TMP_InputField CreateInputField(RectTransform parent, string name, string placeholderText,
            float fontSize, TMP_FontAsset font, float spacing)
        {
            RectTransform box = Kit.Box(parent, name, Kit.Field, Kit.GoldLine, 10f, 2f);
            box.gameObject.SetActive(false); // set up completely before the field wakes up
            Image image = box.GetComponent<Image>();
            image.raycastTarget = true;

            RectTransform area = Kit.Stretch(Kit.CreateRect("Text Area", box), 0f);
            area.offsetMin = new Vector2(18f, 4f);
            area.offsetMax = new Vector2(-18f, -4f);
            area.gameObject.AddComponent<RectMask2D>();

            TMP_Text placeholder = Kit.Label(area, "Placeholder", placeholderText, fontSize * 0.8f, Kit.Inter,
                new Color(1f, 1f, 1f, 0.3f));
            Kit.Stretch(placeholder.rectTransform);
            placeholder.fontStyle = FontStyles.Italic;
            TMP_Text text = Kit.Label(area, "Text", "", fontSize, font, Kit.Text);
            Kit.Stretch(text.rectTransform);
            text.characterSpacing = spacing;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;

            TMP_InputField field = box.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = area;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.targetGraphic = image;
            field.characterLimit = 10;
            // Letters and digits only, in capitals (this replaces the field's own validation).
            field.onValidateInput = (string _, int __, char c) =>
                char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '\0';
            box.gameObject.SetActive(true);
            return field;
        }

        /// <summary>
        /// Two choices side by side in a dark track; the picked one is gold.
        /// Returns both labels so the caller can repaint them with ShowSegment.
        /// </summary>
        private static TMP_Text[] BuildSegmented(RectTransform parent, float x, float y, float width,
            string left, string right, System.Action<bool> pick)
        {
            RectTransform track = Kit.Box(parent, "Segmented", Kit.Inset, Kit.Line, 10f);
            Kit.At(track, x, y, width, 52f);
            float half = (width - 8f) / 2f;
            TMP_Text a = Kit.Button(track, "Left", left, Kit.ButtonStyle.Ghost, () => pick(false), 15f, 7f);
            Kit.At((RectTransform)a.transform.parent, 4f, 4f, half, 44f);
            TMP_Text b = Kit.Button(track, "Right", right, Kit.ButtonStyle.Ghost, () => pick(true), 15f, 7f);
            Kit.At((RectTransform)b.transform.parent, 4f + half, 4f, half, 44f);
            return new[] { a, b };
        }

        // Paints a segmented control: the picked half gold with dark text, the other one plain.
        private static void ShowSegment(TMP_Text[] halves, bool rightPicked)
        {
            for (int i = 0; i < halves.Length; i++)
            {
                bool picked = (i == 1) == rightPicked;
                var rect = (RectTransform)halves[i].transform.parent;
                rect.GetComponent<Image>().color = picked ? Kit.Gold : Color.clear;
                Transform border = rect.Find("Border");
                if (border != null)
                    border.gameObject.SetActive(false);
                halves[i].color = picked ? Kit.PlayText : Kit.Soft;
                TMP_FontAsset font = picked ? Kit.InterBold : Kit.InterSemiBold;
                if (font != null)
                    halves[i].font = font;
            }
        }
    }
}
