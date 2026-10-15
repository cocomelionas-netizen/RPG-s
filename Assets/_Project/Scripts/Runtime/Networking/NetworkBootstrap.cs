using RPG.Accounts;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace RPG.Networking
{
    /// <summary>
    /// Starts the game as server, host or client and decides where players spawn.
    /// Reads the command line on start (see <see cref="NetworkLaunchOptions"/>); a batch mode
    /// build with no mode given starts as a dedicated server. Other builds wait for the player to log in.
    /// With an <see cref="AccountService"/> on this object every connection must carry a valid login
    /// (<see cref="SetLogin"/>); without one (tests) everyone is let in as a guest.
    /// </summary>
    [RequireComponent(typeof(NetworkManager), typeof(UnityTransport))]
    public class NetworkBootstrap : MonoBehaviour
    {
        [SerializeField] private Transform spawnPoint;
        [Tooltip("Players spawn at a random point within this radius around the spawn point.")]
        [SerializeField, Min(0f)] private float spawnRadius = 0.75f;
        [Tooltip("Frame rate cap for dedicated servers, which do not need to render.")]
        [SerializeField, Min(10)] private int serverFrameRate = 60;

        private NetworkManager network;
        private UnityTransport transport;
        private OfflineTransport offlineTransport;
        private AccountService accounts;

        public NetworkManager Network => network;

        /// <summary>True while running a single-player session with <see cref="OfflineTransport"/>.</summary>
        public bool IsOffline => network.IsListening && network.NetworkConfig.NetworkTransport == offlineTransport;

        /// <summary>Browsers cannot host or run servers, only play offline or join a server over WebSockets.</summary>
        public static bool IsBrowser => Application.platform == RuntimePlatform.WebGLPlayer;

        /// <summary>Message from the last failed or closed connection, shown by the connection panel.</summary>
        public string LastDisconnectReason { get; private set; }

        private void Awake()
        {
            network = GetComponent<NetworkManager>();
            transport = GetComponent<UnityTransport>();
            offlineTransport = GetComponent<OfflineTransport>();
            accounts = GetComponent<AccountService>();
            if (offlineTransport == null) offlineTransport = gameObject.AddComponent<OfflineTransport>();

            // Servers and clients must keep running while their window is in the background.
            Application.runInBackground = true;

            network.NetworkConfig.ConnectionApproval = true;
            network.ConnectionApprovalCallback = ApproveConnection;
            network.OnClientConnectedCallback += OnClientConnected;
            network.OnClientDisconnectCallback += OnClientDisconnected;
            network.OnServerStopped += OnServerStopped;
        }

        private void OnDestroy()
        {
            if (network == null) return;
            network.ConnectionApprovalCallback = null;
            network.OnClientConnectedCallback -= OnClientConnected;
            network.OnClientDisconnectCallback -= OnClientDisconnected;
            network.OnServerStopped -= OnServerStopped;
        }

        private void Start()
        {
            NetworkLaunchOptions options = NetworkLaunchOptions.FromCommandLine();
            NetworkLaunchMode mode = options.Mode;
            if (mode == NetworkLaunchMode.None && Application.isBatchMode)
                mode = NetworkLaunchMode.Server;

            // Started from the command line (for example the test bot): play as a new throwaway character.
            if (mode != NetworkLaunchMode.None && mode != NetworkLaunchMode.Server && accounts != null)
                SetLogin(new LoginRequest
                {
                    username = "Bot" + Random.Range(10000, 99999),
                    password = System.Guid.NewGuid().ToString("N"),
                    createCharacter = true,
                });

            switch (mode)
            {
                case NetworkLaunchMode.Server: StartServer(options.Port); break;
                case NetworkLaunchMode.Host: StartHost(options.Port); break;
                case NetworkLaunchMode.Client: StartClient(options.Address, options.Port); break;
            }
        }

        /// <summary>Accounts (null when every connection is let in as a guest).</summary>
        public AccountService Accounts => accounts;

        /// <summary>The login sent with the next connection (or used for the local player of a host).</summary>
        public void SetLogin(LoginRequest login)
        {
            network.NetworkConfig.ConnectionData = login != null ? login.ToBytes() : System.Array.Empty<byte>();
        }

        /// <summary>
        /// A host's own player logs in locally: check it before starting, so a wrong password is reported
        /// instead of starting a server without a player.
        /// </summary>
        private bool CheckLocalLogin()
        {
            if (accounts == null) return true;
            string error = accounts.Check(LoginRequest.FromBytes(network.NetworkConfig.ConnectionData));
            if (error == null) return true;
            LastDisconnectReason = error;
            return false;
        }

        /// <summary>Single player: a host with no network, so it works anywhere, including in a browser.</summary>
        public bool StartOffline()
        {
            LastDisconnectReason = null;
            if (!CheckLocalLogin()) return false;
            network.NetworkConfig.NetworkTransport = offlineTransport;
            bool started = network.StartHost();
            Debug.Log(started ? "[Offline] Started single-player session." : "[Offline] Failed to start.");
            return started;
        }

        public bool StartServer(ushort port)
        {
            UseNetworkTransport();
            SetConnectionData(NetworkLaunchOptions.DefaultAddress, port, "0.0.0.0");
            Application.targetFrameRate = serverFrameRate;
            bool started = network.StartServer();
            Debug.Log(started ? $"[Server] Listening on port {port}." : $"[Server] Failed to start on port {port}.");
            return started;
        }

        public bool StartHost(ushort port)
        {
            LastDisconnectReason = null;
            if (!CheckLocalLogin()) return false;
            UseNetworkTransport();
            SetConnectionData(NetworkLaunchOptions.DefaultAddress, port, "0.0.0.0");
            bool started = network.StartHost();
            Debug.Log(started ? $"[Host] Hosting on port {port}." : $"[Host] Failed to host on port {port}.");
            return started;
        }

        public bool StartClient(string address, ushort port)
        {
            LastDisconnectReason = null;
            UseNetworkTransport();
            SetConnectionData(address, port, null);
            bool started = network.StartClient();
            Debug.Log(started ? $"[Client] Connecting to {address}:{port}..." : "[Client] Failed to start.");
            return started;
        }

        private void UseNetworkTransport()
        {
            network.NetworkConfig.NetworkTransport = transport;
            // Browsers can only open WebSocket connections; a server must also listen with WebSockets to accept them.
            transport.UseWebSockets = IsBrowser;
        }

        private void SetConnectionData(string address, ushort port, string listenAddress)
        {
            // Force our values: UnityTransport also reads -ip/-port itself and misparses -ip as a port
            // (NGO 2.13), and we already parse the same arguments in NetworkLaunchOptions.
            transport.SetConnectionData(true, address, port, listenAddress);
        }

        public void Shutdown()
        {
            network.Shutdown();
        }

        private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            if (accounts != null)
            {
                string error = accounts.LogIn(LoginRequest.FromBytes(request.Payload), request.ClientNetworkId);
                if (error != null)
                {
                    response.Approved = false;
                    response.Reason = error;
                    response.Pending = false;
                    Debug.Log($"[Server] Login refused for client {request.ClientNetworkId}: {error}");
                    return;
                }
            }

            response.Approved = true;
            response.CreatePlayerObject = true;
            response.Position = GetSpawnPosition();
            response.Rotation = Quaternion.identity;
            response.Pending = false;
        }

        /// <summary>A spawn position near the spawn point (also used for respawning).</summary>
        public Vector3 GetSpawnPosition()
        {
            Vector3 center = spawnPoint != null ? spawnPoint.position : Vector3.zero;
            Vector2 offset = Random.insideUnitCircle * spawnRadius;
            // Squash vertically to stay on the isometric ground diamond.
            return center + new Vector3(offset.x, offset.y * 0.5f, 0f);
        }

        private void OnServerStopped(bool wasHost)
        {
            if (accounts != null) accounts.LogOutEveryone();
        }

        private void OnClientConnected(ulong clientId)
        {
            if (network.IsServer)
                Debug.Log($"[Server] Client {clientId} connected. Players online: {network.ConnectedClientsIds.Count}.");
            else if (clientId == network.LocalClientId)
                Debug.Log("[Client] Connected.");
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (network.IsServer)
            {
                if (accounts != null) accounts.LogOut(clientId);
                Debug.Log($"[Server] Client {clientId} disconnected.");
                return;
            }

            LastDisconnectReason = string.IsNullOrEmpty(network.DisconnectReason) ? "Disconnected from server." : network.DisconnectReason;
            Debug.Log($"[Client] {LastDisconnectReason}");
        }
    }
}
