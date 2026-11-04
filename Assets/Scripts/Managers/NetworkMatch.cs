using System.Collections.Generic;
using SoulBind.Core;
using SoulBind.Managers;
using SoulBind.Utility;
using Unity.Netcode;
using UnityEngine;

namespace SoulBind.Online
{
    /// <summary>
    /// Connects the MatchController to the other players in an online match. It sits in the match scene
    /// on a GameObject with a NetworkObject (SoulBind > Set Up Online adds it).
    ///
    /// The host is the only one with the real Match. When everyone's match scene has loaded, it gives
    /// each player a seat (the host is Apostle 1, the others 2, 3, ... in the order they joined; bots
    /// take the remaining seats) and starts the match. After every change it sends each player a
    /// snapshot of what that player may see: their own hand, but only the size of everyone else's.
    /// Players send their actions to the host, which checks that the sender is the Apostle who acts
    /// now before applying anything.
    ///
    /// Safety on the host: a player's seat always comes from their connection, never from the message;
    /// each player may send only a few messages a second (a flood is dropped, and a long flood is
    /// disconnected); command bytes must be exactly the right size; and a player whose game version
    /// differs is turned away with a message.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkMatch : NetworkBehaviour
    {
        [SerializeField] private MatchController controller;
        [Tooltip("The host starts once every connected player's match scene is ready, or after this many seconds.")]
        [SerializeField] private float maxWaitSeconds = 20f;

        private readonly Dictionary<ulong, int> _seatByClient = new Dictionary<ulong, int>(GameConstants.MaxPlayers);
        private readonly HashSet<ulong> _ready = new HashSet<ulong>();
        private readonly Dictionary<ulong, string> _nameByClient = new Dictionary<ulong, string>(GameConstants.MaxPlayers);
        private readonly List<int> _remoteSeats = new List<int>(GameConstants.MaxPlayers);
        private readonly byte[] _commandBytes = new byte[MatchCommand.EncodedSize];
        private readonly Dictionary<ulong, Budget> _budgets = new Dictionary<ulong, Budget>(GameConstants.MaxPlayers);
        private bool _started;
        private float _waited;
        private int _sequence;

        public override void OnNetworkSpawn()
        {
            if (controller == null)
            {
                Debug.LogError("NetworkMatch: assign the Match Controller in the Inspector (or run SoulBind > Set Up Online).", this);
                return;
            }

            if (IsServer)
            {
                controller.MatchChanged += SendSnapshots;
                controller.DraftChanged += SendDraft;
                controller.ChatShown += SendChatToPlayers;
                NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            }
            else
            {
                controller.CommandSubmitted += SendCommand;
                controller.SpawnSubmitted += SendSpawn;
                controller.ChatSubmitted += SendChat;
                ClientReadyRpc(PlayerNames.LocalName, BuildInfo.ProtocolVersion);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (controller != null)
            {
                controller.MatchChanged -= SendSnapshots;
                controller.CommandSubmitted -= SendCommand;
                controller.DraftChanged -= SendDraft;
                controller.SpawnSubmitted -= SendSpawn;
                controller.ChatShown -= SendChatToPlayers;
                controller.ChatSubmitted -= SendChat;
            }
            if (NetworkManager != null)
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        // Host: starts the match once every connected player is ready (or waiting took too long).
        private void Update()
        {
            if (!IsSpawned || !IsServer || _started || controller == null || !controller.IsWaitingForOnlineStart)
                return;

            _waited += Time.deltaTime;
            int others = NetworkManager.ConnectedClientsIds.Count - 1;
            if (_ready.Count < others && _waited < maxWaitSeconds)
                return;
            StartMatch();
        }

        // Seats everyone who is ready, starts the match on the host and tells each client its seat.
        private void StartMatch()
        {
            _started = true;
            int seats = Mathf.Clamp(Mathf.Max(MatchSettings.PlayerCount, _ready.Count + 1), 2, GameConstants.MaxPlayers);

            // Seats in the order the players connected. The host is seat 1.
            int next = 2;
            _remoteSeats.Clear();
            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
            {
                if (clientId == NetworkManager.ServerClientId || !_ready.Contains(clientId))
                    continue;
                if (next > seats)
                {
                    AssignSeatRpc(0, 0, RpcTarget.Single(clientId, RpcTargetUse.Temp));
                    continue;
                }
                _seatByClient[clientId] = next;
                _remoteSeats.Add(next);
                next++;
            }

            // Names, once the seats exist. Bots keep "Apostle N".
            // Sent before AssignSeatRpc, so the first frame has them.
            PlayerNames.Clear();
            PlayerNames.Set(1, PlayerNames.LocalName);
            foreach (KeyValuePair<ulong, int> seat in _seatByClient)
            {
                if (_nameByClient.TryGetValue(seat.Key, out string playerName))
                    PlayerNames.Set(seat.Value, playerName);
            }
            NamesRpc(PlayerNames.Pack());

            // Seats first, so each player knows who they are before the first snapshot arrives.
            foreach (KeyValuePair<ulong, int> pair in _seatByClient)
                AssignSeatRpc(pair.Value, seats, RpcTarget.Single(pair.Key, RpcTargetUse.Temp));

            controller.StartOnlineHost(seats, _remoteSeats); // its first MatchChanged sends the snapshots
        }

        // ---------- Host to players ----------

        // Sends every player its own view of the match (hidden hands stay hidden).
        private void SendSnapshots()
        {
            if (controller.CurrentMatch == null)
                return;
            _sequence++;
            foreach (KeyValuePair<ulong, int> pair in _seatByClient)
                SendSnapshot(pair.Key, pair.Value);
        }

        // Sends one player its view of the match.
        private void SendSnapshot(ulong clientId, int seat)
        {
            byte[] bytes = controller.CurrentMatch.CaptureSnapshot(seat, _sequence).ToBytes();
            SnapshotRpc(bytes, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        // Host to one client: your seat, or 0 when the match is full or already started.
        [Rpc(SendTo.SpecifiedInParams)]
        private void AssignSeatRpc(int seat, int seats, RpcParams rpcParams)
        {
            if (seat == 0)
                controller.ShowOnlineMessage("This match has already started (or is full).");
            else
                controller.StartOnlineClient(seat, seats);
        }

        // Host to one client: the latest match state.
        [Rpc(SendTo.SpecifiedInParams)]
        private void SnapshotRpc(byte[] bytes, RpcParams rpcParams)
        {
            controller.ReceiveSnapshot(bytes);
        }

        // The host and the bots pick on the host, so it sends every pick to everyone.
        private void SendDraft()
        {
            int[] data = controller.GetDraftData();
            if (data != null)
                DraftRpc(data);
        }

        // Host to everyone: the spawn picks so far.
        [Rpc(SendTo.NotServer)]
        private void DraftRpc(int[] data)
        {
            controller.ReceiveDraft(data);
        }

        // ---------- Players to host ----------

        // Client to host: I am ready, with my name and protocol version.
        [Rpc(SendTo.Server)]
        private void ClientReadyRpc(string playerName, int protocol, RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            if (!Allow(sender))
                return;
            if (protocol != BuildInfo.ProtocolVersion)
            {
                Kick(sender, BuildInfo.MismatchMessage(protocol));
                return;
            }
            _nameByClient[sender] = PlayerNames.Clean(playerName);
            if (_started)
            {
                AssignSeatRpc(0, 0, RpcTarget.Single(sender, RpcTargetUse.Temp));
                return;
            }
            _ready.Add(sender);
        }

        // Host to everyone: every seat's name.
        [Rpc(SendTo.NotServer)]
        private void NamesRpc(string packed)
        {
            PlayerNames.Unpack(packed);
        }

        // Packs a command into the reused buffer and sends it to the host.
        private void SendCommand(MatchCommand command)
        {
            command.Encode(_commandBytes, 0);
            SubmitCommandRpc(_commandBytes);
        }

        // Client to host: a command. The host checks the size and the seat before running it.
        [Rpc(SendTo.Server)]
        private void SubmitCommandRpc(byte[] bytes, RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            if (!_seatByClient.TryGetValue(sender, out int seat) || !Allow(sender))
                return;
            if (bytes == null || bytes.Length != MatchCommand.EncodedSize)
                return; // not something this game sends

            CommandResult result = controller.ApplyRemoteCommand(seat, bytes);
            if (result != CommandResult.Applied)
            {
                // Nothing changed. A fresh snapshot puts that player's screen back in step with the host,
                // at most a few times a second, so refused actions can't make the host flood the network.
                Debug.Log($"NetworkMatch: Apostle {seat}'s action was not applied ({result}).");
                if (CanResync(sender))
                {
                    _sequence++;
                    SendSnapshot(sender, seat);
                }
            }
        }

        // Sends a spawn pick to the host.
        private void SendSpawn(HexCoord tile)
        {
            PickSpawnRpc(tile.Q, tile.R);
        }

        // The seat comes from the connection, never from the message.
        [Rpc(SendTo.Server)]
        private void PickSpawnRpc(int q, int r, RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            if (!_seatByClient.TryGetValue(sender, out int seat) || !Allow(sender))
                return;
            if (!controller.ApplyRemoteSpawn(seat, new HexCoord(q, r)))
                Debug.Log($"NetworkMatch: Apostle {seat}'s starting tile was refused.");
        }

        // ---------- Quick chat ----------

        // Player: only the chat id goes to the host.
        private void SendChat(int chatId)
        {
            SubmitChatRpc((byte)chatId);
        }

        // Host: the seat comes from the connection; the controller checks the id and the spam limit.
        [Rpc(SendTo.Server)]
        private void SubmitChatRpc(byte chatId, RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            if (_seatByClient.TryGetValue(sender, out int seat) && Allow(sender))
                controller.ReceiveRemoteChat(seat, chatId);
        }

        // Host: every message shown here (its own, a bot's or a player's) goes to all players.
        private void SendChatToPlayers(int seat, int chatId)
        {
            ChatRpc((byte)seat, (byte)chatId);
        }

        // Host to everyone: a chat line from a seat.
        [Rpc(SendTo.NotServer)]
        private void ChatRpc(byte seat, byte chatId)
        {
            controller.ShowRemoteChat(seat, chatId);
        }

        // ---------- Flood control (host) ----------

        private const float MessagesPerSecond = 8f; // a tap-walk sends about 3 a second
        private const float Burst = 16f;            // messages allowed at once after a quiet spell
        private const int DropLimit = 64;           // dropped messages before the sender is disconnected
        private const float ResyncGap = 0.25f;      // seconds between fix-up snapshots to one player

        private sealed class Budget
        {
            public float Tokens = Burst;
            public float Last;
            public float LastResync = -1f;
            public int Dropped;
        }

        // True if this player may send another message now. Each message spends a token; tokens come
        // back at MessagesPerSecond. Someone who keeps flooding is disconnected.
        private bool Allow(ulong clientId)
        {
            float now = Time.unscaledTime;
            Budget budget = GetBudget(clientId, now);
            budget.Tokens = Mathf.Min(Burst, budget.Tokens + (now - budget.Last) * MessagesPerSecond);
            budget.Last = now;
            if (budget.Tokens >= 1f)
            {
                budget.Tokens -= 1f;
                return true;
            }
            budget.Dropped++;
            if (budget.Dropped >= DropLimit)
                Kick(clientId, "Disconnected: too many messages.");
            return false;
        }

        // A client may ask for a fresh snapshot at most once per ResyncGap.
        private bool CanResync(ulong clientId)
        {
            float now = Time.unscaledTime;
            Budget budget = GetBudget(clientId, now);
            if (budget.LastResync >= 0f && now - budget.LastResync < ResyncGap)
                return false;
            budget.LastResync = now;
            return true;
        }

        // The client's message budget, created on first use.
        private Budget GetBudget(ulong clientId, float now)
        {
            if (!_budgets.TryGetValue(clientId, out Budget budget))
            {
                budget = new Budget { Last = now };
                _budgets[clientId] = budget;
            }
            return budget;
        }

        // Sends a player away with a reason their game shows on the menu. Never the host itself.
        private void Kick(ulong clientId, string reason)
        {
            if (NetworkManager == null || clientId == NetworkManager.ServerClientId)
                return;
            Debug.LogWarning($"NetworkMatch: disconnecting player {clientId}: {reason}");
            NetworkManager.DisconnectClient(clientId, reason);
        }

        // Forgets a client that left and frees its seat.
        private void OnClientDisconnected(ulong clientId)
        {
            _budgets.Remove(clientId);
            _ready.Remove(clientId);
            _nameByClient.Remove(clientId);
            if (_seatByClient.TryGetValue(clientId, out int seat))
            {
                _seatByClient.Remove(clientId);
                controller.ReleaseSeat(seat);
            }
        }
    }
}
