using SoulBind.Managers;
using SoulBind.Online;
using SoulBind.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;


namespace SoulBind.UI
{
    /// <summary>
    /// The start screen: the title, how many Apostles (2-6), who plays the other seats (bots, or
    /// friends taking turns on this device), Play and Quit. It builds its own layout at runtime on the
    /// Canvas it sits on, so the menu scene needs no hand setup (SoulBind > Create Main Menu Scene).
    ///
    /// Online: Host Online creates a room and shows its code; friends type the code and Join. The
    /// room screen shows who has joined; the host presses Start, and bots take the empty seats.
    /// </summary>
    public sealed class MainMenuView : MonoBehaviour
    {
        [Tooltip("The match scene Play loads. It must be in File > Build Settings.")]
        [SerializeField] private string gameSceneName = "SampleScene";
        [SerializeField, Range(2, 6)] private int defaultPlayers = 4;

        private static readonly Color BackgroundColor = new Color(0.08f, 0.06f, 0.12f);
        private static readonly Color PanelColor = new Color(0.13f, 0.10f, 0.19f, 0.95f);
        private static readonly Color ButtonColor = new Color(0.24f, 0.19f, 0.34f);
        private static readonly Color PlayColor = new Color(0.62f, 0.45f, 0.16f);
        private static readonly Color GoldColor = new Color(1f, 0.85f, 0.4f);

        private int _players;
        private bool _bots = true;
        private TMP_Text _playersText;
        private TMP_Text _opponentsText;
        private TMP_Text _opponentsHint;

        // Online
        private GameObject _choicesScreen;
        private GameObject _roomScreen;
        private TMP_InputField _codeField;
        private TMP_InputField _nameField;
        private GameObject _profilePopup;
        private TMP_Text _profileName;
        private TMP_Text _profileInitial;
        private TMP_Text _onlineStatus;
        private TMP_Text _roomCode;
        private TMP_Text _roomPlayers;
        private TMP_Text _roomSeats;
        private TMP_Text _roomHint;
        private TMP_Text _roomStatus;
        private GameObject _roomStart;
        private GameObject _roomSeatButtons;
        private bool _busy;
        private int _shownPlayers = -1;
        private int _shownSeats = -1;

        private void Start()
        {
            _players = Mathf.Clamp(defaultPlayers, 2, 6);
            Build((RectTransform)transform);
            ShowChoices();

            OnlineSession session = OnlineSession.Instance;
            if (session != null && session.InSession && session.MatchStarted)
                session.Leave(); // back from an online match
            if (OnlineSession.LastMessage != null)
            {
                _onlineStatus.text = OnlineSession.LastMessage;
                OnlineSession.LastMessage = null;
            }
            ShowScreen();
        }

        private void Update()
        {
            OnlineSession session = OnlineSession.Instance;
            bool inRoom = session != null && session.InSession;
            if (inRoom != _roomScreen.activeSelf)
            {
                if (!inRoom && OnlineSession.LastMessage != null)
                {
                    _onlineStatus.text = OnlineSession.LastMessage;
                    OnlineSession.LastMessage = null;
                }
                ShowScreen();
            }
            if (inRoom)
                ShowRoom(session, false);
        }

        private void ChangePlayers(int delta)
        {
            _players = Mathf.Clamp(_players + delta, 2, 6);
            ShowChoices();
        }

        private void ToggleOpponents()
        {
            _bots = !_bots;
            ShowChoices();
        }

        private void ShowChoices()
        {
            _playersText.text = _players.ToString();
            _opponentsText.text = _bots ? "Bots" : "Friends on this device";
            _opponentsHint.text = _bots
                ? $"You play Apostle 1; bots play the other {_players - 1}."
                : "Everyone takes turns on this screen; the view follows whoever's turn it is.";
        }


        private void Play()
        {
            MatchSettings.Set(_players, _bots, true);
            SceneManager.LoadScene(gameSceneName);
        }

        // ---- Online ----

        private async void HostOnline()
        {
            if (_busy)
                return;
            _busy = true;
            _onlineStatus.text = "Creating a room...";
            string error = await OnlineSession.GetOrCreate().HostAsync();
            _busy = false;
            if (this == null)
                return; // the menu closed meanwhile
            _onlineStatus.text = error ?? string.Empty;
            ShowScreen();
        }

        private async void JoinOnline()
        {
            if (_busy)
                return;
            _busy = true;
            _onlineStatus.text = "Joining...";
            string error = await OnlineSession.GetOrCreate().JoinAsync(_codeField.text);
            _busy = false;
            if (this == null)
                return;
            _onlineStatus.text = error ?? string.Empty;
            ShowScreen();
        }

        private void StartOnline()
        {
            OnlineSession session = OnlineSession.Instance;
            if (session == null || !session.IsHost)
                return;
            string error = session.StartMatch(gameSceneName, _players);
            _roomStatus.text = error ?? "Starting...";
        }

        private void LeaveRoom()
        {
            OnlineSession.LeaveIfActive();
            _onlineStatus.text = string.Empty;
            ShowScreen();
        }

        private void ShowScreen()
        {
            OnlineSession session = OnlineSession.Instance;
            bool inRoom = session != null && session.InSession;
            _choicesScreen.SetActive(!inRoom);
            _roomScreen.SetActive(inRoom);
            if (inRoom)
            {
                _roomStatus.text = string.Empty;
                ShowRoom(session, true);
            }
        }

        // Called every frame in the room, but only rewrites the text when something changed.
        private void ShowRoom(OnlineSession session, bool force)
        {
            int joined = session.PlayerCount;
            if (session.IsHost && _players < joined)
                _players = Mathf.Min(joined, 6);
            if (!force && joined == _shownPlayers && _players == _shownSeats)
                return;
            _shownPlayers = joined;
            _shownSeats = _players;

            _roomCode.text = session.Code;
            _roomPlayers.text = joined == 1 ? "1 player in the room" : $"{joined} players in the room";
            _roomStart.SetActive(session.IsHost);
            _roomSeatButtons.SetActive(session.IsHost);
            _roomSeats.text = session.IsHost ? $"Apostles: {_players}" : string.Empty;
            int bots = Mathf.Max(0, _players - joined);
            _roomHint.text = session.IsHost
                ? $"Share the code with your friends. {(bots == 0 ? "No bots." : bots == 1 ? "1 bot takes the empty seat." : $"{bots} bots take the empty seats.")}"
                : "Waiting for the host to start the match...";
        }

        private void ChangeSeats(int delta)
        {
            OnlineSession session = OnlineSession.Instance;
            int min = Mathf.Max(2, session != null ? session.PlayerCount : 1);
            _players = Mathf.Clamp(_players + delta, min, 6);
            _playersText.text = _players.ToString();
        }

        private void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---- Layout ----

        private void Build(RectTransform root)
        {
            RectTransform background = CreateRect("Background", root);
            Stretch(background);
            AddImage(background, BackgroundColor);

            TMP_Text title = CreateText("Title", root, "SoulBind", 120f, FontStyles.Bold);
            Place(title.rectTransform, new Vector2(0f, 330f), new Vector2(1400f, 150f));
            title.color = GoldColor;
            TMP_Text subtitle = CreateText("Subtitle", root, "Apostle's Descent", 46f, FontStyles.Italic);
            Place(subtitle.rectTransform, new Vector2(0f, 235f), new Vector2(1400f, 70f));

            // The game's version (Player Settings > Version, raised by SoulBind > Build Android APK),
            // bottom-right, so testers can tell which APK they have.
            TMP_Text version = CreateText("Version", root, "v" + Application.version, 26f, FontStyles.Normal);
            RectTransform versionRect = version.rectTransform;
            versionRect.anchorMin = versionRect.anchorMax = versionRect.pivot = new Vector2(1f, 0f);
            versionRect.anchoredPosition = new Vector2(-24f, 16f);
            versionRect.sizeDelta = new Vector2(320f, 40f);
            version.alignment = TextAlignmentOptions.BottomRight;
            version.color = new Color(1f, 1f, 1f, 0.5f);

            _choicesScreen = CreateRect("Choices", root).gameObject;
            Stretch((RectTransform)_choicesScreen.transform);
            var choices = (RectTransform)_choicesScreen.transform;

            

            RectTransform panel = CreateRect("Panel", choices);
            Place(panel, new Vector2(-400f, -40f), new Vector2(760f, 360f));
            AddImage(panel, PanelColor);
            Outline edge = panel.gameObject.AddComponent<Outline>();
            edge.effectColor = GoldColor;
            edge.effectDistance = new Vector2(3f, -3f);

            // Players: < 4 >
            TMP_Text playersLabel = CreateText("PlayersLabel", panel, "Apostles", 34f, FontStyles.Bold);
            Place(playersLabel.rectTransform, new Vector2(-180f, 110f), new Vector2(300f, 60f));
            CreateButton("Fewer", panel, "<", new Vector2(80f, 110f), new Vector2(70f, 64f), ButtonColor, () => ChangePlayers(-1));
            _playersText = CreateText("Players", panel, "4", 44f, FontStyles.Bold);
            Place(_playersText.rectTransform, new Vector2(170f, 110f), new Vector2(100f, 64f));
            CreateButton("More", panel, ">", new Vector2(260f, 110f), new Vector2(70f, 64f), ButtonColor, () => ChangePlayers(1));

            // Opponents: Bots / Friends on this device
            TMP_Text opponentsLabel = CreateText("OpponentsLabel", panel, "Opponents", 34f, FontStyles.Bold);
            Place(opponentsLabel.rectTransform, new Vector2(-180f, 20f), new Vector2(300f, 60f));
            _opponentsText = CreateButton("Opponents", panel, "Bots", new Vector2(170f, 20f), new Vector2(330f, 64f),
                ButtonColor, ToggleOpponents);
            _opponentsHint = CreateText("OpponentsHint", panel, "", 22f, FontStyles.Normal);
            Place(_opponentsHint.rectTransform, new Vector2(0f, -55f), new Vector2(700f, 50f));
            _opponentsHint.color = new Color(1f, 1f, 1f, 0.7f);

            CreateButton("Play", panel, "Play", new Vector2(0f, -130f), new Vector2(320f, 80f), PlayColor, Play);

            BuildOnlinePanel(choices);
            BuildRoomScreen(root);
            CreateButton("Quit", root, "Quit", new Vector2(0f, -330f), new Vector2(220f, 60f), ButtonColor, Quit);
            BuildProfile(root);
        }

        
        // Top-left: your avatar and name. Tap it to change the name.
        private void BuildProfile(RectTransform root)
        {
            RectTransform card = CreateRect("Profile", root);
            // Anchored to the safe area's left edge, so a camera
            // notch or rounded corner never covers it.
            float left = Screen.width > 0
                ? Screen.safeArea.xMin / Screen.width : 0f;
            card.anchorMin = card.anchorMax = new Vector2(left, 1f);
            card.pivot = new Vector2(0f, 1f);
            card.anchoredPosition = new Vector2(40f, -30f);
            card.sizeDelta = new Vector2(320f, 76f);
            AddImage(card, PanelColor).raycastTarget = true;
            Outline edge = card.gameObject.AddComponent<Outline>();
            edge.effectColor = GoldColor;
            edge.effectDistance = new Vector2(2f, -2f);
            card.gameObject.AddComponent<Button>()
                .onClick.AddListener(OpenProfile);

            // A round avatar with the name's first letter.
            RectTransform avatar = CreateRect("Avatar", card);
            avatar.anchorMin = avatar.anchorMax = new Vector2(0f, 0.5f);
            avatar.pivot = new Vector2(0f, 0.5f);
            avatar.anchoredPosition = new Vector2(10f, 0f);
            avatar.sizeDelta = new Vector2(56f, 56f);
            Image disc = AddImage(avatar, GoldColor);
            disc.sprite = DiscSpriteFactory.Create(1f);
            disc.preserveAspect = true;
            _profileInitial = CreateText("Initial", avatar, "?",
                30f, FontStyles.Bold);
            Stretch(_profileInitial.rectTransform);
            _profileInitial.color = BackgroundColor;

            _profileName = CreateText("Name", card, "", 28f,
                FontStyles.Bold);
            TopLeft(_profileName.rectTransform, 76f, 8f, 236f, 36f);
            _profileName.alignment = TextAlignmentOptions.Left;
            _profileName.overflowMode = TextOverflowModes.Ellipsis;

            TMP_Text hint = CreateText("Hint", card,
                "Tap to change your name", 16f, FontStyles.Italic);
            TopLeft(hint.rectTransform, 76f, 44f, 236f, 24f);
            hint.alignment = TextAlignmentOptions.Left;
            hint.color = new Color(1f, 1f, 1f, 0.6f);

            // The pop-up for typing it. Tapping outside cancels.
            RectTransform popup = CreateRect("ProfilePopup", root);
            Stretch(popup);
            AddImage(popup, new Color(0f, 0f, 0f, 0.7f))
                .raycastTarget = true;
            Button outside = popup.gameObject.AddComponent<Button>();
            outside.transition = Selectable.Transition.None;
            outside.onClick.AddListener(CloseProfile);
            _profilePopup = popup.gameObject;

            RectTransform panel = CreateRect("Panel", popup);
            Place(panel, Vector2.zero, new Vector2(620f, 300f));
            AddImage(panel, PanelColor).raycastTarget = true;
            Outline panelEdge = panel.gameObject.AddComponent<Outline>();
            panelEdge.effectColor = GoldColor;
            panelEdge.effectDistance = new Vector2(3f, -3f);

            TMP_Text title = CreateText("Title", panel, "Your name",
                36f, FontStyles.Bold);
            Place(title.rectTransform, new Vector2(0f, 100f),
                new Vector2(560f, 50f));
            title.color = GoldColor;

            _nameField = CreateInputField("NameField", panel,
                "Type your name", new Vector2(0f, 15f),
                new Vector2(500f, 68f));
            _nameField.characterLimit = PlayerNames.MaxLength;
            _nameField.onValidateInput = (string _, int __, char c) =>
                PlayerNames.IsAllowed(c) ? c : '\0';
            _nameField.onSubmit.AddListener(_ => SaveProfile());

            CreateButton("Cancel", panel, "Cancel",
                new Vector2(-130f, -95f), new Vector2(220f, 64f),
                ButtonColor, CloseProfile);
            CreateButton("Save", panel, "Save",
                new Vector2(130f, -95f), new Vector2(220f, 64f),
                PlayColor, SaveProfile);

            _profilePopup.SetActive(false);
            ShowProfileName();
        }

        private void OpenProfile()
        {
            _nameField.text = PlayerNames.LocalName;
            _profilePopup.transform.SetAsLastSibling();
            _profilePopup.SetActive(true);
        }

        private void SaveProfile()
        {
            PlayerNames.LocalName = _nameField.text;
            CloseProfile();
        }

        private void CloseProfile()
        {
            _profilePopup.SetActive(false);
            ShowProfileName();
        }

        private void ShowProfileName()
        {
            string typed = PlayerNames.LocalName;
            bool named = typed.Length > 0;
            _profileName.text = named ? typed : "Set your name";
            _profileInitial.text = named
                ? typed.Substring(0, 1).ToUpperInvariant()
                : "?";
        }

        // A box placed from the parent's top-left corner.
        private static void TopLeft(RectTransform rect, float x,
            float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        // Host a room, or type a code and join one.
        private void BuildOnlinePanel(RectTransform parent)
        {
            RectTransform panel = CreateRect("OnlinePanel", parent);
            Place(panel, new Vector2(400f, -40f), new Vector2(760f, 360f));
            AddImage(panel, PanelColor);
            Outline edge = panel.gameObject.AddComponent<Outline>();
            edge.effectColor = GoldColor;
            edge.effectDistance = new Vector2(3f, -3f);

            TMP_Text title = CreateText("OnlineTitle", panel, "Online", 34f, FontStyles.Bold);
            Place(title.rectTransform, new Vector2(0f, 130f), new Vector2(700f, 60f));
            CreateButton("Host", panel, "Host Online", new Vector2(0f, 55f), new Vector2(360f, 72f), PlayColor, HostOnline);
            TMP_Text hostHint = CreateText("HostHint", panel, "Uses the Apostles count on the left. Bots take the empty seats.",
                20f, FontStyles.Normal);
            Place(hostHint.rectTransform, new Vector2(0f, 0f), new Vector2(700f, 36f));
            hostHint.color = new Color(1f, 1f, 1f, 0.7f);

            _codeField = CreateInputField("Code", panel, "Room code", new Vector2(-120f, -70f), new Vector2(330f, 68f));
            CreateButton("Join", panel, "Join", new Vector2(175f, -70f), new Vector2(200f, 68f), ButtonColor, JoinOnline);

            _onlineStatus = CreateText("OnlineStatus", panel, "", 22f, FontStyles.Normal);
            Place(_onlineStatus.rectTransform, new Vector2(0f, -140f), new Vector2(720f, 50f));
            _onlineStatus.color = GoldColor;
        }

        // Shown while in a room: the code, who has joined, and (for the host) the seat count and Start.
        private void BuildRoomScreen(RectTransform root)
        {
            RectTransform panel = CreateRect("Room", root);
            Place(panel, new Vector2(0f, -40f), new Vector2(900f, 400f));
            AddImage(panel, PanelColor);
            Outline edge = panel.gameObject.AddComponent<Outline>();
            edge.effectColor = GoldColor;
            edge.effectDistance = new Vector2(3f, -3f);
            _roomScreen = panel.gameObject;

            TMP_Text label = CreateText("CodeLabel", panel, "Room code", 28f, FontStyles.Bold);
            Place(label.rectTransform, new Vector2(0f, 150f), new Vector2(800f, 44f));
            _roomCode = CreateText("RoomCode", panel, "", 72f, FontStyles.Bold);
            Place(_roomCode.rectTransform, new Vector2(0f, 85f), new Vector2(800f, 90f));
            _roomCode.color = GoldColor;
            _roomPlayers = CreateText("RoomPlayers", panel, "", 30f, FontStyles.Normal);
            Place(_roomPlayers.rectTransform, new Vector2(0f, 20f), new Vector2(800f, 44f));

            _roomSeatButtons = CreateRect("Seats", panel).gameObject;
            var seats = (RectTransform)_roomSeatButtons.transform;
            Stretch(seats);
            CreateButton("FewerSeats", seats, "<", new Vector2(-160f, -35f), new Vector2(64f, 56f), ButtonColor, () => ChangeSeats(-1));
            _roomSeats = CreateText("RoomSeats", seats, "", 30f, FontStyles.Bold);
            Place(_roomSeats.rectTransform, new Vector2(0f, -35f), new Vector2(240f, 56f));
            CreateButton("MoreSeats", seats, ">", new Vector2(160f, -35f), new Vector2(64f, 56f), ButtonColor, () => ChangeSeats(1));

            _roomHint = CreateText("RoomHint", panel, "", 22f, FontStyles.Normal);
            Place(_roomHint.rectTransform, new Vector2(0f, -90f), new Vector2(840f, 40f));
            _roomHint.color = new Color(1f, 1f, 1f, 0.7f);

            _roomStart = CreateButton("Start", panel, "Start", new Vector2(-130f, -155f), new Vector2(240f, 70f), PlayColor,
                StartOnline).transform.parent.gameObject;
            CreateButton("Leave", panel, "Leave", new Vector2(130f, -155f), new Vector2(240f, 70f), ButtonColor, LeaveRoom);
            _roomStatus = CreateText("RoomStatus", panel, "", 20f, FontStyles.Normal);
            Place(_roomStatus.rectTransform, new Vector2(0f, -205f), new Vector2(840f, 30f));
            _roomStatus.color = GoldColor;
            _roomScreen.SetActive(false);
        }

        // A text box for the room code. On phones, tapping it opens the keyboard.
        private static TMP_InputField CreateInputField(string name, RectTransform parent, string placeholderText,
            Vector2 position, Vector2 size)
        {
            RectTransform box = CreateRect(name, parent);
            Place(box, position, size);
            box.gameObject.SetActive(false); // set up completely before the field wakes up
            Image image = AddImage(box, new Color(0.06f, 0.05f, 0.09f));
            image.raycastTarget = true;
            Outline edge = box.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(1f, 0.85f, 0.4f, 0.6f);
            edge.effectDistance = new Vector2(2f, -2f);

            RectTransform area = CreateRect("Text Area", box);
            Stretch(area);
            area.offsetMin = new Vector2(14f, 6f);
            area.offsetMax = new Vector2(-14f, -6f);
            area.gameObject.AddComponent<RectMask2D>();

            TMP_Text placeholder = CreateText("Placeholder", area, placeholderText, 30f, FontStyles.Italic);
            Stretch(placeholder.rectTransform);
            placeholder.color = new Color(1f, 1f, 1f, 0.35f);
            TMP_Text text = CreateText("Text", area, "", 34f, FontStyles.Bold);
            Stretch(text.rectTransform);

            TMP_InputField field = box.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = area;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.targetGraphic = image;
            field.characterLimit = 10;
            // Letters and digits only, in capitals (this replaces the field's own validation).
            field.onValidateInput = (string _, int __, char c) => char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '\0';
            box.gameObject.SetActive(true);
            return field;
        }

        // Returns the button's label so it can be changed later.
        private static TMP_Text CreateButton(string name, RectTransform parent, string label, Vector2 position, Vector2 size,
            Color color, UnityEngine.Events.UnityAction onClick)
        {
            RectTransform rect = CreateRect(name, parent);
            Place(rect, position, size);
            Image image = AddImage(rect, color);
            image.raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            TMP_Text text = CreateText("Label", rect, label, Mathf.Min(40f, size.y * 0.5f), FontStyles.Bold);
            Stretch(text.rectTransform);
            return text;
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

        // A fixed-size box, positioned from the middle of the parent.
        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static Image AddImage(RectTransform rect, Color color)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateText(string name, RectTransform parent, string text, float size, FontStyles style)
        {
            RectTransform rect = CreateRect(name, parent);
            TextMeshProUGUI tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}