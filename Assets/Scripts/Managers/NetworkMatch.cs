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
                NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            }
            else
            {
                controller.CommandSubmitted += SendCommand;
                controller.SpawnSubmitted += SendSpawn;
                ClientReadyRpc(PlayerNames.LocalName);
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

        // ---- Host to players ----

        private void SendSnapshots()
        {
            if (controller.CurrentMatch == null)
                return;
            _sequence++;
            foreach (KeyValuePair<ulong, int> pair in _seatByClient)
                SendSnapshot(pair.Key, pair.Value);
        }

        private void SendSnapshot(ulong clientId, int seat)
        {
            byte[] bytes = controller.CurrentMatch.CaptureSnapshot(seat, _sequence).ToBytes();
            SnapshotRpc(bytes, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void AssignSeatRpc(int seat, int seats, RpcParams rpcParams)
        {
            if (seat == 0)
                controller.ShowOnlineMessage("This match has already started (or is full).");
            else
                controller.StartOnlineClient(seat, seats);
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void SnapshotRpc(byte[] bytes, RpcParams rpcParams)
        {
            controller.ReceiveSnapshot(bytes);
            
        }

        // the host and the bots pick on the host, so it sends every pick to everyone.
        private void SendDraft()
        {
            int[] data = controller.GetDraftData();
            if (data != null)
                DraftRpc(data);
        }

        [Rpc(SendTo.NotServer)]
        private void DraftRpc(int[] data)
        {
            controller.ReceiveDraft(data);
        }

        // ---- Players to host ----

        [Rpc(SendTo.Server)]
        private void ClientReadyRpc(string playerName, RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            _nameByClient[sender] = PlayerNames.Clean(playerName);
            if (_started)
            {
                AssignSeatRpc(0, 0, RpcTarget.Single(sender, RpcTargetUse.Temp));
                return;
            }
            _ready.Add(sender);
        }

        [Rpc(SendTo.NotServer)]
        private void NamesRpc(string packed)
        {
            PlayerNames.Unpack(packed);
        }

        private void SendCommand(MatchCommand command)
        {
            command.Encode(_commandBytes, 0);
            SubmitCommandRpc(_commandBytes);
        }

        [Rpc(SendTo.Server)]
        private void SubmitCommandRpc(byte[] bytes, RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            if (!_seatByClient.TryGetValue(sender, out int seat))
                return;

            CommandResult result = controller.ApplyRemoteCommand(seat, bytes);
            if (result != CommandResult.Applied)
            {
                // Nothing changed. A fresh snapshot puts that player's screen back in step with the host.
                Debug.Log($"NetworkMatch: Apostle {seat}'s action was not applied ({result}).");
                _sequence++;
                SendSnapshot(sender, seat);
            }
        }

        private void SendSpawn(HexCoord tile)
        {
            PickSpawnRpc(tile.Q, tile.R);
        }

        // the seat comes from the connection, never from the message.
        [Rpc(SendTo.Server)]
        private void PickSpawnRpc(int q, int r, RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            if (!_seatByClient.TryGetValue(sender, out int seat))
                return;
            if (!controller.ApplyRemoteSpawn(seat, new HexCoord(q, r)))
                Debug.Log($"NetworkMatch: APostle {seat}'s starting tile was refused.");
        }

        private void OnClientDisconnected(ulong clientId)
        {
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