using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace RPG.Networking
{
    /// <summary>
    /// Starts the game as server, host or client and decides where players spawn.
    /// Reads the command line on start (see <see cref="NetworkLaunchOptions"/>); a batch mode
    /// build with no mode given starts as a dedicated server.
    /// Connection approval is the place where login checks will go (Stage 8).
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

        public NetworkManager Network => network;

        /// <summary>Message from the last failed or closed connection, shown by the connection panel.</summary>
        public string LastDisconnectReason { get; private set; }

        private void Awake()
        {
            network = GetComponent<NetworkManager>();
            transport = GetComponent<UnityTransport>();

            // Servers and clients must keep running while their window is in the background.
            Application.runInBackground = true;

            network.NetworkConfig.ConnectionApproval = true;
            network.ConnectionApprovalCallback = ApproveConnection;
            network.OnClientConnectedCallback += OnClientConnected;
            network.OnClientDisconnectCallback += OnClientDisconnected;
        }

        private void OnDestroy()
        {
            if (network == null) return;
            network.ConnectionApprovalCallback = null;
            network.OnClientConnectedCallback -= OnClientConnected;
            network.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        private void Start()
        {
            NetworkLaunchOptions options = NetworkLaunchOptions.FromCommandLine();
            NetworkLaunchMode mode = options.Mode;
            if (mode == NetworkLaunchMode.None && Application.isBatchMode)
                mode = NetworkLaunchMode.Server;

            switch (mode)
            {
                case NetworkLaunchMode.Server: StartServer(options.Port); break;
                case NetworkLaunchMode.Host: StartHost(options.Port); break;
                case NetworkLaunchMode.Client: StartClient(options.Address, options.Port); break;
            }
        }

        public bool StartServer(ushort port)
        {
            SetConnectionData(NetworkLaunchOptions.DefaultAddress, port, "0.0.0.0");
            Application.targetFrameRate = serverFrameRate;
            bool started = network.StartServer();
            Debug.Log(started ? $"[Server] Listening on port {port}." : $"[Server] Failed to start on port {port}.");
            return started;
        }

        public bool StartHost(ushort port)
        {
            SetConnectionData(NetworkLaunchOptions.DefaultAddress, port, "0.0.0.0");
            bool started = network.StartHost();
            Debug.Log(started ? $"[Host] Hosting on port {port}." : $"[Host] Failed to host on port {port}.");
            return started;
        }

        public bool StartClient(string address, ushort port)
        {
            LastDisconnectReason = null;
            SetConnectionData(address, port, null);
            bool started = network.StartClient();
            Debug.Log(started ? $"[Client] Connecting to {address}:{port}..." : "[Client] Failed to start.");
            return started;
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
            response.Approved = true;
            response.CreatePlayerObject = true;
            response.Position = SpawnPosition();
            response.Rotation = Quaternion.identity;
            response.Pending = false;
        }

        private Vector3 SpawnPosition()
        {
            Vector3 center = spawnPoint != null ? spawnPoint.position : Vector3.zero;
            Vector2 offset = Random.insideUnitCircle * spawnRadius;
            // Squash vertically to stay on the isometric ground diamond.
            return center + new Vector3(offset.x, offset.y * 0.5f, 0f);
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
                Debug.Log($"[Server] Client {clientId} disconnected.");
                return;
            }

            LastDisconnectReason = string.IsNullOrEmpty(network.DisconnectReason) ? "Disconnected from server." : network.DisconnectReason;
            Debug.Log($"[Client] {LastDisconnectReason}");
        }
    }
}
