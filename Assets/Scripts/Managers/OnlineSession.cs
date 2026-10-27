using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SoulBind.Core;
using SoulBind.Managers;
using SoulBind.Utility;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SoulBind.Online
{
    /// <summary>
    /// The online room, from Host or Join in the main menu until a player leaves. Hosting creates a
    /// session with a join code (Unity Lobby) and a Relay connection, so players can join from any
    /// network without opening ports; joining uses the code. The session starts Netcode for
    /// GameObjects by itself: the host as host, everyone else as a client.
    ///
    /// It lives on its own object that survives scene changes, and creates the NetworkManager the
    /// first time it is needed, so no scene needs one placed by hand.
    /// The main menu scene must be the first scene in the Build Settings.
    /// </summary>
    public sealed class OnlineSession : MonoBehaviour
    {
        /// <summary>The room, or null when there is none.</summary>
        public static OnlineSession Instance { get; private set; }

        /// <summary>Why the last room ended unexpectedly (shown once by the menu), or null.</summary>
        public static string LastMessage { get; set; }

        private ISession _session;
        private bool _leaving;
        private bool _listening;

        public bool InSession => _session != null;
        public bool IsHost => _session != null && _session.IsHost;
        public string Code => _session != null ? _session.Code : string.Empty;

        /// <summary>
        /// One player in the room, for the lobby screen. Name is
        /// the name they typed on their device ("" if none).
        /// </summary>
        public readonly struct RoomPlayer
        {
            public readonly string Name;
            public readonly bool IsHost;
            public readonly bool IsYou;

            public RoomPlayer(string name, bool isHost, bool isYou)
            {
                Name = name;
                IsHost = isHost;
                IsYou = isYou;
            }
        }

        

        /// <summary>True once the match scene has loaded for this room.</summary>
        public bool MatchStarted { get; private set; }

        /// <summary>Players in the room, the host included.</summary>
        public int PlayerCount
        {
            get
            {
                if (_session == null)
                    return 0;
                int count = _session.Players != null ? _session.Players.Count : 1;
                NetworkManager network = NetworkManager.Singleton;
                if (IsHost && network != null && network.IsListening)
                    count = Mathf.Max(count, network.ConnectedClientsIds.Count);
                return count;
            }
        }

        public static OnlineSession GetOrCreate()
        {
            if (Instance != null)
                return Instance;
            var go = new GameObject("OnlineSession");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<OnlineSession>();
            return Instance;
        }

        /// <summary>Leaves the room if there is one. Safe to call at any time.</summary>
        public static void LeaveIfActive()
        {
            if (Instance != null && Instance.InSession)
                Instance.Leave();
        }

        // ---- Host / Join ----

        /// <summary>Creates a room. Returns null when it worked, or a message saying what went wrong.</summary>
        public async Task<string> HostAsync()
        {
            try
            {
                await SignInAsync();
                EnsureNetworkManager();
                SessionOptions options = new SessionOptions
                {
                    MaxPlayers = GameConstants.MaxPlayers,
                    IsPrivate = true, // only people with the code can find it
                }.WithRelayNetwork();
                _session = await MultiplayerService.Instance.CreateSessionAsync(options);
                AfterEntering();
                Debug.Log($"OnlineSession: hosting room {_session.Code}.");
                return null;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return "Couldn't create a room: " + e.Message;
            }
        }

        /// <summary>Joins a room by its code. Returns null when it worked, or a message saying what went wrong.</summary>
        public async Task<string> JoinAsync(string code)
        {
            code = code != null ? code.Trim().ToUpperInvariant() : string.Empty;
            if (code.Length == 0)
                return "Type the room code first.";
            try
            {
                await SignInAsync();
                EnsureNetworkManager();
                _session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
                MatchSettings.SetOnline(GameConstants.MaxPlayers); // the host sends the real seat count
                AfterEntering();
                Debug.Log($"OnlineSession: joined room {code}.");
                return null;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return "Couldn't join: " + e.Message;
            }
        }

        /// <summary>Host only: takes everyone in the room to the match scene. seats includes the bots.</summary>
        public string StartMatch(string gameSceneName, int seats)
        {
            NetworkManager network = NetworkManager.Singleton;
            if (!IsHost || network == null || !network.IsListening || network.SceneManager == null)
                return "The connection isn't ready yet. Try again in a moment.";

            MatchSettings.SetOnline(Mathf.Max(seats, PlayerCount));
            SceneEventProgressStatus status = network.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
                return $"Couldn't load '{gameSceneName}' ({status}). Is it in File > Build Settings?";
            return null;
        }

        /// <summary>Leaves the room and stops the connection.</summary>
        public async void Leave()
        {
            ISession session = _session;
            _session = null;
            MatchStarted = false;
            _leaving = true;
            try
            {
                NetworkManager network = NetworkManager.Singleton;
                if (network != null && network.IsListening)
                    network.Shutdown();
                if (session != null)
                    await session.LeaveAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning("OnlineSession: leaving the room: " + e.Message);
            }
            finally
            {
                _leaving = false;
            }
        }

        // ---- Setup ----

        private static async Task SignInAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
                await UnityServices.InitializeAsync();
            while (UnityServices.State == ServicesInitializationState.Initializing)
                await Task.Yield();

            if (AuthenticationService.Instance.IsSignedIn)
                return;
#if UNITY_EDITOR
            // Multiplayer Play Mode runs several players on one computer. A different profile for each
            // gives each one its own anonymous player, or they could not join each other's rooms.
            AuthenticationService.Instance.SwitchProfile("e" + Guid.NewGuid().ToString("N").Substring(0, 12));
#endif
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        // A NetworkManager with a Unity Transport, made once and kept for the whole run.
        private void EnsureNetworkManager()
        {
            if (NetworkManager.Singleton == null)
            {
                var go = new GameObject("NetworkManager");
                go.SetActive(false); // configure before NetworkManager wakes up
                UnityTransport transport = go.AddComponent<UnityTransport>();
                NetworkManager network = go.AddComponent<NetworkManager>();
                network.NetworkConfig = new NetworkConfig
                {
                    NetworkTransport = transport,
                    EnableSceneManagement = true,
                };
                DontDestroyOnLoad(go);
                go.SetActive(true);
            }

            if (!_listening && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientStopped += OnNetworkStopped;
                _listening = true;
            }
        }

        private void AfterEntering()
        {
            MatchStarted = false;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening)
                Debug.LogWarning("OnlineSession: the room exists but Netcode did not start. Check that Relay and Lobby are " + "enabled for this project in the Unity Cloud dashboard.");
        }

        // The menu is scene 0; any other scene is the match.
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_session != null && scene.buildIndex != 0)
                MatchStarted = true;
        }

        // The connection ended without this player leaving: the host left, or the network dropped.
        private void OnNetworkStopped(bool wasHost)
        {
            if (_leaving || _session == null)
                return;

            LastMessage = wasHost ? "The online room was closed." : "Lost the connection to the host.";
            Debug.LogWarning("OnlineSession: " + LastMessage);
            Leave();
            if (SceneManager.GetActiveScene().buildIndex != 0)
                SceneManager.LoadScene(0);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_listening && NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientStopped -= OnNetworkStopped;
            if (Instance == this)
                Instance = null;
        }
    }
}