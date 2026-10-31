using System.Collections.Generic;
using System.Text;
using SoulBind.Online;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SoulBind.UI
{
    // The Play Online window: the list of open public rooms, a box for a friend's code,
    // and Create Lobby (public, or private for code only).
    public sealed partial class MainMenuView
    {
        // Rows shown at once. More open rooms than this show as "and N more".
        private const int MaxRoomRows = 6;
        // Seconds between room list refreshes while the window is open (Lobby has rate limits).
        private const float RoomRefreshSeconds = 6f;
        // Seconds between re-reading the names in the room screen.
        private const float NamesRefreshSeconds = 1f;

        private sealed class RoomRow
        {
            public GameObject Root;
            public TMP_Text Name;
            public TMP_Text Players;
        }

        private GameObject _onlinePopup;
        private TMP_InputField _codeField;
        private TMP_Text _privacyLabel;
        private TMP_Text _popupStatus;
        private TMP_Text _roomListInfo;
        private readonly RoomRow[] _roomRows = new RoomRow[MaxRoomRows];
        private readonly List<OnlineSession.RoomInfo> _rooms = new List<OnlineSession.RoomInfo>(16);
        private readonly List<OnlineSession.RoomPlayer> _roomPlayerList = new List<OnlineSession.RoomPlayer>(6);
        private readonly StringBuilder _namesText = new StringBuilder(96);
        private bool _privateRoom;
        private bool _refreshing;
        private float _nextRoomRefresh;
        private float _nextNames;
        private string _names = string.Empty;

        // ---------- Opening and closing ----------

        private void OpenOnline()
        {
            _popupStatus.text = string.Empty;
            _onlinePopup.transform.SetAsLastSibling();
            _onlinePopup.SetActive(true);
            _nextRoomRefresh = 0f; // refresh now
        }

        private void CloseOnline()
        {
            if (_onlinePopup != null)
                _onlinePopup.SetActive(false);
        }

        private void TogglePrivacy()
        {
            _privateRoom = !_privateRoom;
            ShowPrivacy();
        }

        private void ShowPrivacy()
        {
            _privacyLabel.text = _privateRoom ? "Private: code only" : "Public: listed";
        }

        // Shows a message in the window and behind it, so it is seen either way.
        private void ShowOnlineStatus(string text)
        {
            _popupStatus.text = text;
            _onlineStatus.text = text;
        }

        // ---------- Host and join ----------

        private async void HostOnline()
        {
            if (_busy)
                return;
            _busy = true;
            ShowOnlineStatus("Creating a lobby...");
            string error = await OnlineSession.GetOrCreate().HostAsync(_privateRoom);
            _busy = false;
            if (this == null)
                return; // the menu closed meanwhile
            ShowOnlineStatus(error ?? string.Empty);
            ShowScreen();
        }

        private async void JoinOnline()
        {
            if (_busy)
                return;
            _busy = true;
            ShowOnlineStatus("Joining...");
            string error = await OnlineSession.GetOrCreate().JoinAsync(_codeField.text);
            _busy = false;
            if (this == null)
                return;
            ShowOnlineStatus(error ?? string.Empty);
            ShowScreen();
        }

        private async void JoinListedRoom(int row)
        {
            if (_busy || row >= _rooms.Count)
                return;
            _busy = true;
            ShowOnlineStatus($"Joining {_rooms[row].Name}...");
            string error = await OnlineSession.GetOrCreate().JoinByIdAsync(_rooms[row].Id);
            _busy = false;
            if (this == null)
                return;
            ShowOnlineStatus(error ?? string.Empty);
            if (error != null)
                _nextRoomRefresh = 0f; // the room may be full or gone now
            ShowScreen();
        }

        // ---------- Room list ----------

        // Called every frame outside a room; asks the server only every few seconds.
        private void UpdateRoomList()
        {
            if (_onlinePopup == null || !_onlinePopup.activeSelf)
                return;
            if (_refreshing || _busy || Time.unscaledTime < _nextRoomRefresh)
                return;
            RefreshRooms();
        }

        private async void RefreshRooms()
        {
            _refreshing = true;
            _nextRoomRefresh = Time.unscaledTime + RoomRefreshSeconds;
            if (_rooms.Count == 0)
                _roomListInfo.text = "Looking for rooms...";
            string error = await OnlineSession.FindRoomsAsync(_rooms);
            _refreshing = false;
            if (this == null)
                return;
            ShowRooms(error);
        }

        private void ShowRooms(string error)
        {
            for (int i = 0; i < _roomRows.Length; i++)
            {
                RoomRow row = _roomRows[i];
                bool used = i < _rooms.Count;
                row.Root.SetActive(used);
                if (!used)
                    continue;
                OnlineSession.RoomInfo room = _rooms[i];
                row.Name.text = room.Name;
                row.Players.text = $"{room.Players}/{room.MaxPlayers}";
            }

            if (error != null)
                _roomListInfo.text = error;
            else if (_rooms.Count == 0)
                _roomListInfo.text = "No open rooms right now. Create a lobby, or join with a code.";
            else if (_rooms.Count > MaxRoomRows)
                _roomListInfo.text = $"and {_rooms.Count - MaxRoomRows} more...";
            else
                _roomListInfo.text = string.Empty;
        }

        // ---------- Names in the room screen ----------

        // "Yves (host), Ana, You". Read again at most once a second, so the menu doesn't
        // build a new string every frame.
        private string RoomNames(OnlineSession session)
        {
            if (Time.unscaledTime < _nextNames)
                return _names;
            _nextNames = Time.unscaledTime + NamesRefreshSeconds;

            session.GetRoomPlayers(_roomPlayerList);
            _namesText.Clear();
            for (int i = 0; i < _roomPlayerList.Count; i++)
            {
                OnlineSession.RoomPlayer player = _roomPlayerList[i];
                if (i > 0)
                    _namesText.Append(", ");
                string name = player.Name.Length > 0 ? player.Name : $"Player {i + 1}";
                _namesText.Append(player.IsYou ? "You" : name);
                if (player.IsHost)
                    _namesText.Append(" (host)");
            }
            _names = _namesText.ToString();
            return _names;
        }

        // ---------- Layout ----------

        private void BuildOnlinePopup(RectTransform root)
        {
            // A dark cover; tapping outside the window closes it.
            RectTransform cover = CreateRect("OnlinePopup", root);
            Stretch(cover);
            AddImage(cover, new Color(0f, 0f, 0f, 0.7f)).raycastTarget = true;
            Button outside = cover.gameObject.AddComponent<Button>();
            outside.transition = Selectable.Transition.None;
            outside.onClick.AddListener(CloseOnline);
            _onlinePopup = cover.gameObject;

            RectTransform panel = CreateRect("Panel", cover);
            Place(panel, Vector2.zero, new Vector2(1100f, 800f));
            AddImage(panel, PanelColor).raycastTarget = true; // taps here don't close it
            Outline edge = panel.gameObject.AddComponent<Outline>();
            edge.effectColor = GoldColor;
            edge.effectDistance = new Vector2(3f, -3f);

            TMP_Text title = CreateText("Title", panel, "Play Online", 48f, FontStyles.Bold);
            Place(title.rectTransform, new Vector2(0f, 345f), new Vector2(800f, 70f));
            title.color = GoldColor;
            CreateButton("Close", panel, "X", new Vector2(500f, 345f), new Vector2(70f, 70f),
                ButtonColor, CloseOnline);

            // Open rooms, with Refresh.
            TMP_Text listLabel = CreateText("RoomsLabel", panel, "Open rooms", 32f, FontStyles.Bold);
            Place(listLabel.rectTransform, new Vector2(-300f, 270f), new Vector2(400f, 50f));
            listLabel.alignment = TextAlignmentOptions.Left;
            CreateButton("Refresh", panel, "Refresh", new Vector2(400f, 270f), new Vector2(200f, 56f),
                ButtonColor, () => _nextRoomRefresh = 0f);

            for (int i = 0; i < MaxRoomRows; i++)
                _roomRows[i] = CreateRoomRow(panel, i, new Vector2(0f, 200f - i * 76f));
            _roomListInfo = CreateText("RoomsInfo", panel, "", 24f, FontStyles.Italic);
            Place(_roomListInfo.rectTransform, new Vector2(0f, 200f - MaxRoomRows * 76f + 20f),
                new Vector2(1000f, 40f));
            _roomListInfo.color = new Color(1f, 1f, 1f, 0.7f);

            // Join with a code, or create a lobby.
            _codeField = CreateInputField("Code", panel, "Room code", new Vector2(-300f, -262f),
                new Vector2(400f, 68f));
            _codeField.onSubmit.AddListener(_ => JoinOnline());
            CreateButton("Join", panel, "Join", new Vector2(0f, -262f), new Vector2(180f, 68f),
                ButtonColor, JoinOnline);
            CreateButton("Create", panel, "Create Lobby", new Vector2(330f, -262f), new Vector2(320f, 68f),
                PlayColor, HostOnline);
            _privacyLabel = CreateButton("Privacy", panel, "", new Vector2(330f, -335f),
                new Vector2(320f, 50f), ButtonColor, TogglePrivacy);
            _privacyLabel.fontSize = 24f;
            ShowPrivacy();

            _popupStatus = CreateText("Status", panel, "", 24f, FontStyles.Normal);
            Place(_popupStatus.rectTransform, new Vector2(-200f, -335f), new Vector2(640f, 50f));
            _popupStatus.color = GoldColor;

            _onlinePopup.SetActive(false);
        }

        // One room: its name, how full it is, and Join. The whole row is the button.
        private RoomRow CreateRoomRow(RectTransform panel, int index, Vector2 position)
        {
            RectTransform rect = CreateRect("Room" + index, panel);
            Place(rect, position, new Vector2(1000f, 66f));
            Image image = AddImage(rect, ButtonColor);
            image.raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => JoinListedRoom(index));

            TMP_Text name = CreateText("Name", rect, "", 30f, FontStyles.Bold);
            TopLeft(name.rectTransform, 24f, 8f, 620f, 50f);
            name.alignment = TextAlignmentOptions.Left;
            name.overflowMode = TextOverflowModes.Ellipsis;

            TMP_Text players = CreateText("Players", rect, "", 28f, FontStyles.Normal);
            TopLeft(players.rectTransform, 660f, 8f, 140f, 50f);

            TMP_Text join = CreateText("Join", rect, "Join", 28f, FontStyles.Bold);
            TopLeft(join.rectTransform, 820f, 8f, 160f, 50f);
            join.color = GoldColor;

            rect.gameObject.SetActive(false);
            return new RoomRow { Root = rect.gameObject, Name = name, Players = players };
        }
    }
}
