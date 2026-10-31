using System.Collections.Generic;
using System.Text;
using SoulBind.Core;
using SoulBind.Online;
using SoulBind.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Kit = SoulBind.UI.UiKit;

namespace SoulBind.UI
{
    // The Lobby from the design: the room code with Copy, a card for each seat (players who joined,
    // then open seats that bots will take), and the Match panel with Start Match and Leave room.
    public sealed partial class MainMenuView
    {
        // Seconds between re-reading who is in the room (names and count).
        private const float LobbyRefreshSeconds = 0.5f;

        private sealed class SeatCard
        {
            public GameObject Root;
            public Image Fill;
            public Image Border;
            public Image Ring;
            public Image RingBorder;
            public TMP_Text Initial;
            public TMP_Text Name;
            public TMP_Text Role;
            public Image TagFill;
            public TMP_Text Tag;
        }

        private GameObject _lobbyScreen;
        private TMP_Text _lobbyCode;
        private TMP_Text _copyLabel;
        private TMP_Text _seatsTaken;
        private TMP_Text _seatsHint;
        private TMP_Text _lobbyApostles;
        private GameObject _lobbyStepper;
        private TMP_Text _lobbyBots;
        private TMP_Text _lobbyHost;
        private TMP_Text _lobbyHint;
        private TMP_Text _lobbyStatus;
        private GameObject _startButton;
        private readonly SeatCard[] _seatCards = new SeatCard[GameConstants.MaxPlayers];
        private readonly List<OnlineSession.RoomPlayer> _roomPlayers = new List<OnlineSession.RoomPlayer>(6);
        private readonly StringBuilder _lobbyKey = new StringBuilder(128);
        private string _shownLobby = string.Empty;
        private float _nextLobbyRead;

        // ---------- Actions ----------

        private void StartOnline()
        {
            OnlineSession session = OnlineSession.Instance;
            if (session == null || !session.IsHost)
                return;
            string error = session.StartMatch(gameSceneName, _players);
            _lobbyStatus.text = error ?? "Starting...";
        }

        private void LeaveRoom()
        {
            OnlineSession.LeaveIfActive();
            ShowOnlineStatus(string.Empty);
            ShowScreen(Screen.Online);
        }

        private void ChangeSeats(int delta)
        {
            OnlineSession session = OnlineSession.Instance;
            int min = Mathf.Max(2, session != null ? session.PlayerCount : 1);
            _players = Mathf.Clamp(_players + delta, min, GameConstants.MaxPlayers);
            ShowPlayers();
            if (session != null && session.IsHost)
                session.SetSeatCount(_players);
            _nextLobbyRead = 0f;
        }

        private void CopyCode()
        {
            OnlineSession session = OnlineSession.Instance;
            if (session == null)
                return;
            GUIUtility.systemCopyBuffer = session.Code;
            _copyLabel.text = "Copied";
        }

        // ---------- Showing the room ----------

        // Called every frame in the room. Reads the room twice a second and only redraws on a change.
        private void ShowLobby(OnlineSession session, bool force)
        {
            if (session == null || !session.InSession)
                return;
            if (!force && Time.unscaledTime < _nextLobbyRead)
                return;
            _nextLobbyRead = Time.unscaledTime + LobbyRefreshSeconds;

            session.GetRoomPlayers(_roomPlayers);
            int joined = Mathf.Max(session.PlayerCount, _roomPlayers.Count);
            if (session.IsHost && _players < joined)
            {
                _players = Mathf.Min(joined, GameConstants.MaxPlayers);
                ShowPlayers();
                session.SetSeatCount(_players);
            }
            // Players who joined learn the seat count from the host's room settings.
            int seats = session.IsHost ? _players : Mathf.Max(joined, session.SeatCount);

            // Skip the redraw when nothing that is shown has changed.
            _lobbyKey.Clear();
            _lobbyKey.Append(session.Code).Append('|').Append(seats).Append('|').Append(joined);
            for (int i = 0; i < _roomPlayers.Count; i++)
                _lobbyKey.Append('|').Append(_roomPlayers[i].Name).Append(_roomPlayers[i].IsHost ? '*' : '-');
            if (!force && _lobbyKey.Length == _shownLobby.Length && KeyMatches())
                return;
            _shownLobby = _lobbyKey.ToString();

            DrawLobby(session, seats, joined);
        }

        private bool KeyMatches()
        {
            for (int i = 0; i < _lobbyKey.Length; i++)
            {
                if (_lobbyKey[i] != _shownLobby[i])
                    return false;
            }
            return true;
        }

        private void DrawLobby(OnlineSession session, int seats, int joined)
        {
            bool host = session.IsHost;
            int bots = Mathf.Max(0, seats - joined);
            _lobbyCode.text = session.Code;
            _copyLabel.text = "Copy";
            _seatsTaken.text = $"{joined} of {seats} seats taken";
            _seatsHint.text = host
                ? "Open seats are played by bots when the match starts"
                : "Waiting for the host to start the match";

            for (int i = 0; i < _seatCards.Length; i++)
                ShowSeat(_seatCards[i], i, seats);

            _lobbyApostles.text = seats.ToString();
            _lobbyStepper.SetActive(host);
            // Between the host's ‹ › buttons, or at the right edge like the other rows.
            _lobbyApostles.alignment = host ? TextAlignmentOptions.Center : TextAlignmentOptions.Right;
            if (host)
                Kit.At(_lobbyApostles.rectTransform, 230f, 66f, 44f, 30f);
            else
                Kit.At(_lobbyApostles.rectTransform, 154f, 70f, 150f, 22f);
            _lobbyBots.text = bots == 0 ? "None" : $"{bots} (open seats)";
            _lobbyHost.text = HostName();
            _startButton.SetActive(host);
            _lobbyHint.text = host
                ? "Share the code. Players who join take the next open seat, in the colour shown."
                : "The host picks the number of Apostles and starts the match.";
        }

        // One seat card: a player who joined (in join order), or an open seat a bot will take.
        private void ShowSeat(SeatCard card, int index, int seats)
        {
            bool shown = index < seats;
            card.Root.SetActive(shown);
            if (!shown)
                return;

            Color colour = ApostleColors.Get(index + 1);
            bool open = index >= _roomPlayers.Count;
            card.Fill.color = open ? Color.clear : Kit.Panel;
            card.Border.color = open ? Kit.Line : Kit.BronzeLine;
            card.Ring.color = open ? Color.clear : colour;
            card.RingBorder.color = colour;

            if (open)
            {
                card.Initial.text = string.Empty;
                card.Name.text = "Open seat";
                card.Role.text = $"Apostle {index + 1} · waiting for a player";
                ShowTag(card, "BOT", Kit.OpenFill, Kit.Muted);
                return;
            }

            OnlineSession.RoomPlayer player = _roomPlayers[index];
            string name = player.Name.Length > 0 ? player.Name : $"Apostle {index + 1}";
            card.Initial.text = name.Substring(0, 1).ToUpperInvariant();
            card.Name.text = name;
            string role = $"Apostle {index + 1}";
            if (player.IsHost)
                role += " · Host";
            if (player.IsYou)
                role += " · You";
            card.Role.text = role;
            if (player.IsHost)
                ShowTag(card, "HOST", Kit.Gold, Kit.PlayText);
            else
                ShowTag(card, "READY", Kit.ReadyFill, Kit.ReadyText);
        }

        private static void ShowTag(SeatCard card, string text, Color fill, Color ink)
        {
            card.Tag.text = text;
            card.Tag.color = ink;
            card.TagFill.color = fill;
        }

        private string HostName()
        {
            for (int i = 0; i < _roomPlayers.Count; i++)
            {
                if (!_roomPlayers[i].IsHost)
                    continue;
                if (_roomPlayers[i].IsYou)
                    return "You";
                return _roomPlayers[i].Name.Length > 0 ? _roomPlayers[i].Name : "Apostle 1";
            }
            return "-";
        }

        // ---------- Layout ----------

        private void BuildLobby()
        {
            RectTransform frame = Kit.CreateFrame(_root, "LobbyScreen");
            _lobbyScreen = frame.gameObject;
            AddFrameLines(frame, false);
            BuildBackButton(frame, LeaveRoom);

            TMP_Text codeLabel = Kit.Label(frame, "CodeLabel", "ROOM CODE", 13f, Kit.Inter, Kit.Muted,
                TextAlignmentOptions.Left);
            Kit.At(codeLabel.rectTransform, 128f, 38f, 300f, 18f);
            codeLabel.characterSpacing = 20f;
            _lobbyCode = Kit.Label(frame, "Code", "", 44f, Kit.DisplayHeavy, Kit.Title, TextAlignmentOptions.Left);
            Kit.At(_lobbyCode.rectTransform, 128f, 56f, 270f, 54f);
            _lobbyCode.characterSpacing = 20f;
            _copyLabel = Kit.Button(frame, "Copy", "Copy", Kit.ButtonStyle.Secondary, CopyCode, 14f, 8f);
            Kit.At((RectTransform)_copyLabel.transform.parent, 408f, 62f, 100f, 44f);

            _seatsTaken = Kit.Label(frame, "SeatsTaken", "", 22f, Kit.Display, Kit.Text, TextAlignmentOptions.Right);
            Kit.At(_seatsTaken.rectTransform, 724f, 46f, 500f, 30f);
            _seatsHint = Kit.Label(frame, "SeatsHint", "", 13f, Kit.Inter, Kit.Muted, TextAlignmentOptions.Right);
            Kit.At(_seatsHint.rectTransform, 724f, 82f, 500f, 20f);

            // Seats: two columns, three rows.
            for (int i = 0; i < _seatCards.Length; i++)
            {
                float x = 56f + (i % 2) * (387f + 16f);
                float y = 150f + (i / 2) * (140f + 16f);
                _seatCards[i] = CreateSeatCard(frame, i, x, y);
            }

            BuildMatchPanel(frame);
            _lobbyScreen.SetActive(false);
        }

        private SeatCard CreateSeatCard(RectTransform frame, int index, float x, float y)
        {
            RectTransform rect = Kit.Box(frame, "Seat" + (index + 1), Kit.Panel, Kit.BronzeLine, 14f, 2f);
            Kit.At(rect, x, y, 387f, 140f);
            var card = new SeatCard
            {
                Root = rect.gameObject,
                Fill = rect.GetComponent<Image>(),
                Border = rect.Find("Border").GetComponent<Image>()
            };

            RectTransform ring = Kit.Circle(rect, "Ring", 64f, Color.white, Color.white, 3f);
            Kit.At(ring, 22f, 38f, 64f, 64f);
            card.Ring = ring.GetComponent<Image>();
            card.RingBorder = ring.Find("Border").GetComponent<Image>();
            card.Initial = Kit.Label(ring, "Initial", "", 26f, Kit.Display, Kit.Background);
            Kit.Stretch(card.Initial.rectTransform);

            card.Name = Kit.Label(rect, "Name", "", 18f, Kit.InterBold, Kit.Text, TextAlignmentOptions.Left);
            Kit.At(card.Name.rectTransform, 104f, 42f, 180f, 26f);
            card.Name.textWrappingMode = TextWrappingModes.NoWrap;
            card.Name.overflowMode = TextOverflowModes.Ellipsis;
            card.Role = Kit.Label(rect, "Role", "", 13f, Kit.Inter, Kit.Muted, TextAlignmentOptions.Left);
            Kit.At(card.Role.rectTransform, 104f, 72f, 190f, 34f);

            RectTransform tag = Kit.Box(rect, "Tag", Kit.OpenFill, Color.clear, 6f);
            Kit.At(tag, 295f, 57f, 70f, 26f);
            card.TagFill = tag.GetComponent<Image>();
            card.TagFill.raycastTarget = false;
            card.Tag = Kit.Label(tag, "Text", "", 12f, Kit.InterBold, Kit.Muted);
            Kit.Stretch(card.Tag.rectTransform);
            card.Tag.characterSpacing = 10f;

            rect.GetComponent<Image>().raycastTarget = false;
            return card;
        }

        private void BuildMatchPanel(RectTransform frame)
        {
            RectTransform panel = Kit.Box(frame, "MatchPanel", Kit.Panel, Kit.GoldLine, 14f);
            Kit.At(panel, 894f, 150f, 330f, 486f);

            TMP_Text title = Kit.Label(panel, "Title", "Match", 22f, Kit.Display, Kit.Text, TextAlignmentOptions.Left);
            Kit.At(title.rectTransform, 26f, 24f, 278f, 30f);

            _lobbyApostles = InfoRow(panel, "Apostles", 70f);
            // The host can still change the number of Apostles here.
            RectTransform stepper = Kit.At(Kit.CreateRect("Stepper", panel), 0f, 0f, 330f, 486f);
            _lobbyStepper = stepper.gameObject;
            TMP_Text fewer = Kit.Button(stepper, "Fewer", "‹", Kit.ButtonStyle.Secondary, () => ChangeSeats(-1), 20f, 6f);
            Kit.At((RectTransform)fewer.transform.parent, 200f, 66f, 30f, 30f);
            TMP_Text more = Kit.Button(stepper, "More", "›", Kit.ButtonStyle.Secondary, () => ChangeSeats(1), 20f, 6f);
            Kit.At((RectTransform)more.transform.parent, 274f, 66f, 30f, 30f);

            _lobbyBots = InfoRow(panel, "Bots", 108f);
            _lobbyHost = InfoRow(panel, "Host", 142f);
            TMP_Text version = InfoRow(panel, "Version", 176f);
            version.text = "v" + Application.version;

            Kit.Divider(panel, 26f, 214f, 278f);
            _lobbyHint = Kit.Label(panel, "Hint", "", 13f, Kit.Inter, Kit.Muted, TextAlignmentOptions.TopLeft);
            Kit.At(_lobbyHint.rectTransform, 26f, 228f, 278f, 60f);
            _lobbyStatus = Kit.Label(panel, "Status", "", 13f, Kit.Inter, Kit.Gold, TextAlignmentOptions.Left);
            Kit.At(_lobbyStatus.rectTransform, 26f, 296f, 278f, 36f);

            TMP_Text start = Kit.Button(panel, "Start", "Start Match", Kit.ButtonStyle.Primary, StartOnline, 21f);
            Kit.At((RectTransform)start.transform.parent, 26f, 340f, 278f, 60f);
            _startButton = start.transform.parent.gameObject;
            TMP_Text leave = Kit.Button(panel, "Leave", "Leave room", Kit.ButtonStyle.Ghost, LeaveRoom, 15f);
            Kit.At((RectTransform)leave.transform.parent, 26f, 412f, 278f, 48f);
        }

        // "Label ........ value" across the panel. Returns the value's text.
        private static TMP_Text InfoRow(RectTransform panel, string label, float y)
        {
            TMP_Text name = Kit.Label(panel, label + "Label", label, 15f, Kit.Inter, Kit.Muted,
                TextAlignmentOptions.Left);
            Kit.At(name.rectTransform, 26f, y, 150f, 22f);
            TMP_Text value = Kit.Label(panel, label, "", 15f, Kit.InterSemiBold, Kit.Text,
                TextAlignmentOptions.Right);
            Kit.At(value.rectTransform, 154f, y, 150f, 22f);
            return value;
        }
    }
}
