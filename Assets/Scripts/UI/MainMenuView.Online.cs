using System.Collections.Generic;
using SoulBind.Online;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Kit = SoulBind.UI.UiKit;

namespace SoulBind.UI
{
    // The Play Online screen from the design: Host a Room on the left (Apostles, Public or Private,
    // Create Room) and Join a Room on the right (a code box, Join, and the open public rooms).
    public sealed partial class MainMenuView
    {
        // Open rooms listed at once. More than this show as "and N more".
        private const int MaxRoomRows = 3;
        // Seconds between room list refreshes while the screen is open (Lobby has rate limits).
        private const float RoomRefreshSeconds = 6f;

        private sealed class RoomRow
        {
            public GameObject Root;
            public TMP_Text Name;
            public TMP_Text Players;
        }

        private GameObject _onlineScreen;
        private TMP_Text _playingAs;
        private TMP_Text _hostPlayers;
        private TMP_Text[] _privacy;
        private TMP_Text _privacyHint;
        private TMP_InputField _codeField;
        private TMP_Text _onlineStatus;
        private TMP_Text _roomListInfo;
        private readonly RoomRow[] _roomRows = new RoomRow[MaxRoomRows];
        private readonly List<OnlineSession.RoomInfo> _rooms = new List<OnlineSession.RoomInfo>(16);
        private bool _privateRoom;
        private bool _refreshing;
        private float _nextRoomRefresh;

        // ---------- Actions ----------

        private void OnOpenOnline()
        {
            ShowProfileName();
            ShowPrivacy();
            _nextRoomRefresh = 0f; // refresh now
        }

        private void PickPrivacy(bool isPrivate)
        {
            _privateRoom = isPrivate;
            ShowPrivacy();
        }

        private void ShowPrivacy()
        {
            ShowSegment(_privacy, _privateRoom);
            _privacyHint.text = _privateRoom
                ? "Only players with the code can join. Bots take any empty seats."
                : "Listed under Open rooms for anyone to join. Bots take any empty seats.";
        }

        // Shows a message on the Join card, and on the main screen in case it is open.
        private void ShowOnlineStatus(string text)
        {
            _onlineStatus.text = string.IsNullOrEmpty(text) ? "Not connected yet" : text;
            _mainStatus.text = text ?? string.Empty;
        }

        private async void HostOnline()
        {
            if (_busy)
                return;
            _busy = true;
            ShowOnlineStatus("Creating a room...");
            string error = await OnlineSession.GetOrCreate().HostAsync(_privateRoom);
            _busy = false;
            if (this == null)
                return; // the menu closed meanwhile
            ShowOnlineStatus(error);
            if (error == null)
                OnlineSession.Instance.SetSeatCount(_players);
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
            ShowOnlineStatus(error);
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
            ShowOnlineStatus(error);
            if (error != null)
                _nextRoomRefresh = 0f; // the room may be full or gone now
        }

        // ---------- Room list ----------

        // Called every frame on this screen; asks the server only every few seconds.
        private void UpdateRoomList()
        {
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
                bool used = error == null && i < _rooms.Count;
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
                _roomListInfo.text = "No open rooms right now. Host one, or join with a code.";
            else if (_rooms.Count > MaxRoomRows)
                _roomListInfo.text = $"and {_rooms.Count - MaxRoomRows} more...";
            else
                _roomListInfo.text = string.Empty;
            // The note sits under the rows when there are some, in their place when there are none.
            float y = 312f + Mathf.Min(_rooms.Count, MaxRoomRows) * 46f;
            Kit.At(_roomListInfo.rectTransform, 32f, error == null ? y : 312f, 504f, 36f);
        }

        // ---------- Layout ----------

        private void BuildOnline()
        {
            RectTransform frame = Kit.CreateFrame(_root, "OnlineScreen");
            _onlineScreen = frame.gameObject;
            AddFrameLines(frame, false);
            BuildBackButton(frame, () => ShowScreen(Screen.Main));

            TMP_Text title = Kit.Label(frame, "Title", "Play Online", 40f, Kit.Cinzel, Kit.Title,
                TextAlignmentOptions.Left);
            Kit.At(title.rectTransform, 128f, 44f, 500f, 52f);
            title.characterSpacing = 6f;
            _playingAs = Kit.Label(frame, "PlayingAs", "", 14f, Kit.Inter, Kit.Muted, TextAlignmentOptions.Right);
            Kit.At(_playingAs.rectTransform, 724f, 44f, 500f, 52f);

            BuildHostCard(frame);
            BuildJoinCard(frame);
        }

        private RectTransform BuildCard(RectTransform frame, string name, float x, string heading, string sub)
        {
            RectTransform card = Kit.Box(frame, name, Kit.Panel, Kit.GoldLine, 14f);
            Kit.At(card, x, 140f, 568f, 470f);
            TMP_Text title = Kit.Label(card, "Heading", heading, 26f, Kit.Cinzel, Kit.Text,
                TextAlignmentOptions.Left);
            Kit.At(title.rectTransform, 32f, 28f, 504f, 36f);
            TMP_Text hint = Kit.Label(card, "Sub", sub, 14f, Kit.Inter, Kit.Muted, TextAlignmentOptions.Left);
            Kit.At(hint.rectTransform, 32f, 66f, 504f, 20f);
            return card;
        }

        private void BuildHostCard(RectTransform frame)
        {
            RectTransform card = BuildCard(frame, "HostCard", 56f, "Host a Room",
                "Get a code to share. Bots take any empty seats.");

            TMP_Text apostles = Kit.Label(card, "ApostlesLabel", "Apostles", 15f, Kit.InterSemiBold, Kit.Soft,
                TextAlignmentOptions.Left);
            Kit.At(apostles.rectTransform, 32f, 118f, 200f, 44f);
            _hostPlayers = BuildStepper(card, 392f, 118f, ChangePlayers);

            TMP_Text type = Kit.Label(card, "TypeLabel", "Room", 15f, Kit.InterSemiBold, Kit.Soft,
                TextAlignmentOptions.Left);
            Kit.At(type.rectTransform, 32f, 186f, 200f, 24f);
            _privacy = BuildSegmented(card, 32f, 218f, 504f, "Public", "Private", PickPrivacy);
            _privacyHint = Kit.Label(card, "TypeHint", "", 13f, Kit.Inter, Kit.Muted, TextAlignmentOptions.Left);
            Kit.At(_privacyHint.rectTransform, 32f, 280f, 504f, 40f);

            TMP_Text create = Kit.Button(card, "Create", "Create Room", Kit.ButtonStyle.Primary, HostOnline, 21f);
            Kit.At((RectTransform)create.transform.parent, 32f, 378f, 504f, 60f);
        }

        private void BuildJoinCard(RectTransform frame)
        {
            RectTransform card = BuildCard(frame, "JoinCard", 656f, "Join a Room",
                "Type the code the host shared, or pick an open room.");

            TMP_Text codeLabel = Kit.Label(card, "CodeLabel", "Room code", 15f, Kit.InterSemiBold, Kit.Soft,
                TextAlignmentOptions.Left);
            Kit.At(codeLabel.rectTransform, 32f, 104f, 300f, 24f);
            _codeField = CreateInputField(card, "Code", "ABC123", 30f, Kit.Cinzel, 20f);
            Kit.At((RectTransform)_codeField.transform, 32f, 134f, 330f, 64f);
            _codeField.onSubmit.AddListener(_ => JoinOnline());
            TMP_Text join = Kit.Button(card, "Join", "Join", Kit.ButtonStyle.Outline, JoinOnline, 21f);
            Kit.At((RectTransform)join.transform.parent, 374f, 134f, 162f, 64f);

            // The connection line: what is happening, or what went wrong.
            RectTransform statusRow = Kit.Box(card, "StatusRow", Kit.Inset, Kit.Line, 10f);
            Kit.At(statusRow, 32f, 212f, 504f, 44f);
            _onlineStatus = Kit.Label(statusRow, "Status", "Not connected yet", 14f, Kit.Inter, Kit.Soft,
                TextAlignmentOptions.Left);
            Kit.Stretch(_onlineStatus.rectTransform, 0f);
            _onlineStatus.rectTransform.offsetMin = new Vector2(16f, 0f);
            _onlineStatus.rectTransform.offsetMax = new Vector2(-16f, 0f);

            TMP_Text listLabel = Kit.Label(card, "RoomsLabel", "Open rooms", 15f, Kit.InterSemiBold, Kit.Soft,
                TextAlignmentOptions.Left);
            Kit.At(listLabel.rectTransform, 32f, 274f, 300f, 28f);
            TMP_Text refresh = Kit.Button(card, "Refresh", "Refresh", Kit.ButtonStyle.Ghost,
                () => _nextRoomRefresh = 0f, 13f, 8f);
            Kit.At((RectTransform)refresh.transform.parent, 436f, 272f, 100f, 32f);

            for (int i = 0; i < MaxRoomRows; i++)
                _roomRows[i] = CreateRoomRow(card, i, 312f + i * 46f);
            _roomListInfo = Kit.Label(card, "RoomsInfo", "", 13f, Kit.Inter, Kit.Muted);
            _roomListInfo.fontStyle = FontStyles.Italic;
            Kit.At(_roomListInfo.rectTransform, 32f, 312f, 504f, 36f);
        }

        // One open room: its name, how full it is, and Join. The whole row is the button.
        private RoomRow CreateRoomRow(RectTransform card, int index, float y)
        {
            TMP_Text join = Kit.Button(card, "Room" + index, "Join", Kit.ButtonStyle.Secondary,
                () => JoinListedRoom(index), 14f, 8f);
            var rect = (RectTransform)join.transform.parent;
            Kit.At(rect, 32f, y, 504f, 40f);
            join.alignment = TextAlignmentOptions.Right;
            join.rectTransform.offsetMax = new Vector2(-16f, 0f);

            TMP_Text name = Kit.Label(rect, "Name", "", 15f, Kit.InterSemiBold, Kit.Text,
                TextAlignmentOptions.Left);
            Kit.At(name.rectTransform, 16f, 0f, 320f, 40f);
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.overflowMode = TextOverflowModes.Ellipsis;
            TMP_Text players = Kit.Label(rect, "Players", "", 14f, Kit.Inter, Kit.Muted);
            Kit.At(players.rectTransform, 340f, 0f, 70f, 40f);

            rect.gameObject.SetActive(false);
            return new RoomRow { Root = rect.gameObject, Name = name, Players = players };
        }
    }
}
