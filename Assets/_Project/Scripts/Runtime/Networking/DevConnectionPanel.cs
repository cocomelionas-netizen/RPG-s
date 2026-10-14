using Unity.Netcode;
using UnityEngine;

namespace RPG.Networking
{
    /// <summary>
    /// Developer connection panel (IMGUI) for testing: Offline / Host / Client / Server buttons, address and port.
    /// In a browser only Offline and Client are offered (browsers cannot host).
    /// Collapses to a status line once connected. The real login screen replaces it in Stage 8.
    /// F1 toggles it.
    /// </summary>
    [RequireComponent(typeof(NetworkBootstrap))]
    public class DevConnectionPanel : MonoBehaviour
    {
        private NetworkBootstrap bootstrap;
        private string address = NetworkLaunchOptions.DefaultAddress;
        private string port = NetworkLaunchOptions.DefaultPort.ToString();
        private bool visible = true;

        private void Awake()
        {
            bootstrap = GetComponent<NetworkBootstrap>();
        }

        private void OnGUI()
        {
            if (Application.isBatchMode) return;

            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.F1)
            {
                visible = !visible;
                e.Use();
            }
            if (!visible) return;

            NetworkManager network = bootstrap.Network;
            bool running = network.IsListening || network.ShutdownInProgress;

            GUILayout.BeginArea(new Rect(10f, 10f, 260f, running ? 70f : 195f), GUI.skin.box);
            if (running)
                DrawStatus(network);
            else
                DrawConnectForm();
            GUILayout.EndArea();
        }

        private void DrawConnectForm()
        {
            GUILayout.Label("Connection (F1 hides)");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Address", GUILayout.Width(60f));
            address = GUILayout.TextField(address);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Port", GUILayout.Width(60f));
            port = GUILayout.TextField(port);
            GUILayout.EndHorizontal();

            ushort portNumber = ushort.TryParse(port, out ushort parsed) ? parsed : NetworkLaunchOptions.DefaultPort;
            if (GUILayout.Button("Offline (single player)")) bootstrap.StartOffline();
            GUILayout.BeginHorizontal();
            if (!NetworkBootstrap.IsBrowser && GUILayout.Button("Host")) bootstrap.StartHost(portNumber);
            if (GUILayout.Button("Client")) bootstrap.StartClient(address, portNumber);
            if (!NetworkBootstrap.IsBrowser && GUILayout.Button("Server")) bootstrap.StartServer(portNumber);
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(bootstrap.LastDisconnectReason))
                GUILayout.Label(bootstrap.LastDisconnectReason);
        }

        private void DrawStatus(NetworkManager network)
        {
            string status;
            if (network.ShutdownInProgress) status = "Shutting down...";
            else if (bootstrap.IsOffline) status = "Offline - single player";
            else if (network.IsHost) status = $"Host - players online: {network.ConnectedClientsIds.Count}";
            else if (network.IsServer) status = $"Server - players online: {network.ConnectedClientsIds.Count}";
            else if (network.IsConnectedClient) status = $"Client - connected (id {network.LocalClientId})";
            else status = "Client - connecting...";

            GUILayout.Label(status);
            if (GUILayout.Button("Disconnect"))
                bootstrap.Shutdown();
        }
    }
}
