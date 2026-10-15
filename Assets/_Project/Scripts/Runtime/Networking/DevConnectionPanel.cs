using RPG.Accounts;
using RPG.Progression;
using Unity.Netcode;
using UnityEngine;

namespace RPG.Networking
{
    /// <summary>
    /// Login and connection panel (IMGUI): character name and password, log in or create a new character
    /// with a class, then play offline, host, join a server or run a server. In a browser only Offline and
    /// Join are offered (browsers cannot host). Collapses to a status line once connected; F1 hides it.
    /// The name and the last mode are remembered on this device; the password never is.
    /// </summary>
    [RequireComponent(typeof(NetworkBootstrap))]
    public class DevConnectionPanel : MonoBehaviour
    {
        private const string NameKey = "rpg.login.name";

        private NetworkBootstrap bootstrap;
        private string address = NetworkLaunchOptions.DefaultAddress;
        private string port = NetworkLaunchOptions.DefaultPort.ToString();
        private string username = "";
        private string password = "";
        private bool createCharacter;
        private int classIndex;
        private bool visible = true;
        private string[] classNames;

        private void Awake()
        {
            bootstrap = GetComponent<NetworkBootstrap>();
            username = PlayerPrefs.GetString(NameKey, "");
        }

        private void Start()
        {
            GameObject playerPrefab = bootstrap.Network.NetworkConfig.PlayerPrefab;
            ClassCatalog catalog = playerPrefab != null && playerPrefab.TryGetComponent(out CharacterProgress progress) ? progress.Catalog : null;
            classNames = new string[catalog != null ? catalog.Count : 0];
            for (int i = 0; i < classNames.Length; i++)
                classNames[i] = catalog.Get(i).displayName;
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

            if (running)
            {
                // Under the minimap (top right), clear of the player's portrait.
                float hudScale = Mathf.Sqrt(Screen.width / 1920f * (Screen.height / 1080f));
                GUILayout.BeginArea(new Rect(Screen.width - 270f, 300f * hudScale, 260f, 70f), GUI.skin.box);
                DrawStatus(network);
            }
            else
            {
                float width = 330f;
                GUILayout.BeginArea(new Rect((Screen.width - width) * 0.5f, Screen.height * 0.18f, width, 330f), GUI.skin.box);
                DrawLoginForm();
            }
            GUILayout.EndArea();
        }

        private void DrawLoginForm()
        {
            GUILayout.Label(createCharacter ? "Create a character" : "Log in");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Name", GUILayout.Width(70f));
            username = GUILayout.TextField(username, AccountService.MaxNameLength);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Password", GUILayout.Width(70f));
            password = GUILayout.PasswordField(password, '*', 64);
            GUILayout.EndHorizontal();

            createCharacter = GUILayout.Toggle(createCharacter, " New character");
            if (createCharacter && classNames != null && classNames.Length > 0)
                classIndex = GUILayout.Toolbar(Mathf.Clamp(classIndex, 0, classNames.Length - 1), classNames);

            GUILayout.Space(6f);
            var login = new LoginRequest { username = username.Trim(), password = password, createCharacter = createCharacter, classIndex = classIndex };

            if (GUILayout.Button("Play offline (single player)", GUILayout.Height(32f)))
                Connect(login, () => bootstrap.StartOffline());

            GUILayout.Space(6f);
            GUILayout.Label("Multiplayer");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Address", GUILayout.Width(70f));
            address = GUILayout.TextField(address);
            GUILayout.Label("Port", GUILayout.Width(32f));
            port = GUILayout.TextField(port, GUILayout.Width(55f));
            GUILayout.EndHorizontal();

            ushort portNumber = ushort.TryParse(port, out ushort parsed) ? parsed : NetworkLaunchOptions.DefaultPort;
            GUILayout.BeginHorizontal();
            if (!NetworkBootstrap.IsBrowser && GUILayout.Button("Host")) Connect(login, () => bootstrap.StartHost(portNumber));
            if (GUILayout.Button("Join")) Connect(login, () => bootstrap.StartClient(address, portNumber));
            if (!NetworkBootstrap.IsBrowser && GUILayout.Button("Server only")) bootstrap.StartServer(portNumber);
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(bootstrap.LastDisconnectReason))
            {
                var style = new GUIStyle(GUI.skin.label) { wordWrap = true };
                style.normal.textColor = new Color(1f, 0.5f, 0.4f);
                GUILayout.Label(bootstrap.LastDisconnectReason, style);
            }
        }

        private void Connect(LoginRequest login, System.Func<bool> connect)
        {
            bootstrap.SetLogin(login);
            if (!connect()) return;

            PlayerPrefs.SetString(NameKey, login.username);
            PlayerPrefs.Save();
            password = "";
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
            if (GUILayout.Button(bootstrap.IsOffline || network.IsConnectedClient ? "Log out" : "Disconnect"))
                bootstrap.Shutdown();
        }
    }
}
