using System.Collections.Generic;
using RPG.Accounts;
using RPG.Characters;
using RPG.Items;
using RPG.Progression;
using RPG.UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RPG.Networking
{
    /// <summary>
    /// The login screens, over the painted island: LOG IN (account name and password, where to play: offline, join a
    /// server or host; create an account), CHARACTERS (the account's characters, each drawn in the gear it wears; enter
    /// the world, create or delete one) and NEW CHARACTER (name, class, man or woman, drawn in its starting gear). In a
    /// browser only Offline and Join are offered (browsers cannot host). While playing only a small Log Out button stays.
    /// The account name and where to play are remembered on this device; the password never is.
    /// </summary>
    [RequireComponent(typeof(NetworkBootstrap))]
    public class DevConnectionPanel : MonoBehaviour
    {
        private const string NameKey = "rpg.login.name";
        private const string ModeKey = "rpg.login.mode";
        private const string AddressKey = "rpg.login.address";

        private enum Screen { Login, Characters, Create }

        private enum Mode { Offline, Join, Host }

        private static readonly Color Gold = new Color(1f, 0.84f, 0.45f);
        private static readonly Color Ink = new Color(0.93f, 0.9f, 0.82f);
        private static readonly Color Faint = new Color(0.68f, 0.65f, 0.58f);
        private static readonly Color ErrorColor = new Color(1f, 0.45f, 0.35f);
        private static readonly Color CardColor = new Color(0.1f, 0.09f, 0.08f, 0.92f);
        private static readonly Color ChosenColor = new Color(0.32f, 0.24f, 0.1f, 0.95f);
        private static readonly Color ButtonColor = new Color(0.3f, 0.26f, 0.2f, 0.95f);

        private static readonly string[] ClassBlurbs =
        {
            "Plate, sword and shield. Holy strikes, auras and blessings: stands in front and takes the blows.",
            "Bow and leather. Shots that slow, snare and stun: strikes from afar and slips away.",
            "Staff and robes. Fire and frost from afar, shields of will: fragile, but deadly.",
        };

        private NetworkBootstrap bootstrap;
        private ClassCatalog classes;
        private AvatarCatalog avatars;
        private Inventory gearSource;

        private Screen screen = Screen.Login;
        private Mode mode = Mode.Offline;
        private string account = "";
        private string password = "";
        private CharacterSummary[] characters = System.Array.Empty<CharacterSummary>();
        private int chosen;
        private int newClass;
        private bool newFemale;
        private bool busy;
        private bool wasPlaying;
        private bool wasRunning;
        private string message;

        // The UI.
        private GameObject root;
        private GameObject screens;
        private RawImage backdrop;
        private GameObject loginPanel;
        private GameObject charactersPanel;
        private GameObject createPanel;
        private GameObject confirmPanel;
        private GameObject playingBar;
        private Text playingStatus;
        private InputField accountField;
        private InputField passwordField;
        private InputField addressField;
        private InputField portField;
        private InputField nameField;
        private Text loginMessage;
        private Text charactersMessage;
        private Text createMessage;
        private Text confirmText;
        private Text busyText;
        private readonly List<Button> modeButtons = new List<Button>();
        private GameObject serverRow;
        private RectTransform cardList;
        private readonly List<Card> cards = new List<Card>();
        private HeroPreview bigHero;
        private Text heroName;
        private Text heroLine;
        private Button enterButton;
        private Button deleteButton;
        private Text countText;
        private readonly List<Button> classButtons = new List<Button>();
        private Text classBlurb;
        private Button manButton;
        private Button womanButton;
        private HeroPreview newHero;

        private class Card
        {
            public Button Button;
            public Image Background;
            public HeroPreview Hero;
            public Text Name;
            public Text Line;
        }

        private void Awake()
        {
            bootstrap = GetComponent<NetworkBootstrap>();
            account = PlayerPrefs.GetString(NameKey, "");
            mode = (Mode)Mathf.Clamp(PlayerPrefs.GetInt(ModeKey, 0), 0, 2);
            if (NetworkBootstrap.IsBrowser && mode == Mode.Host) mode = Mode.Offline;
        }

        private void Start()
        {
            GameObject playerPrefab = bootstrap.Network.NetworkConfig.PlayerPrefab;
            if (playerPrefab != null)
            {
                classes = playerPrefab.TryGetComponent(out CharacterProgress progress) ? progress.Catalog : null;
                avatars = playerPrefab.TryGetComponent(out HeroAppearance hero) ? hero.Catalog : null;
                gearSource = playerPrefab.GetComponent<Inventory>();
            }
            if (Application.isBatchMode) return;
            Build();
            ShowScreen(Screen.Login);
        }

        // ---------- Building the screens ----------

        private void Build()
        {
            root = new GameObject("Login Screens", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();
            RectTransform canvasRect = (RectTransform)root.transform;
            RectTransform rootRect = UIFactory.CreateRect("Screens", canvasRect);
            UIFactory.Stretch(rootRect);
            screens = rootRect.gameObject;

            // The painted island behind everything, drifting slowly, darkened.
            backdrop = new GameObject("Backdrop", typeof(RectTransform)).AddComponent<RawImage>();
            backdrop.transform.SetParent(rootRect, false);
            UIFactory.Stretch(backdrop.rectTransform);
            backdrop.texture = Resources.Load<Texture2D>("UI/Map/World");
            backdrop.color = backdrop.texture != null ? new Color(0.62f, 0.6f, 0.56f) : new Color(0.08f, 0.1f, 0.12f);
            Image shade = UIFactory.CreateImage("Shade", backdrop.transform, new Color(0.02f, 0.03f, 0.05f, 0.45f));
            UIFactory.Stretch(shade.rectTransform);

            Text title = Label(rootRect, "HALVAREN", 96, Gold, TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;
            title.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(3f, -4f);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1200f, 120f));
            Text subtitle = Label(rootRect, "an island of old roads and older ruins", 24, Ink, TextAnchor.MiddleCenter);
            subtitle.fontStyle = FontStyle.Italic;
            UIFactory.Place(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -178f), new Vector2(1200f, 34f));

            BuildLogin(rootRect);
            BuildCharacters(rootRect);
            BuildCreate(rootRect);
            BuildConfirm(rootRect);

            busyText = Label(rootRect, "", 22, Gold, TextAnchor.MiddleCenter);
            UIFactory.Place(busyText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(900f, 40f));

            // While playing: a small bar under the minimap with Log Out.
            Image bar = UIFactory.CreateImage("Playing", canvasRect, new Color(0.07f, 0.06f, 0.05f, 0.85f));
            UIFactory.StyleBox(bar);
            playingBar = bar.gameObject;
            UIFactory.Place(bar.rectTransform, new Vector2(1f, 1f), new Vector2(-16f, -300f), new Vector2(250f, 44f));
            playingStatus = Label(bar.rectTransform, "", 14, Faint, TextAnchor.MiddleLeft);
            UIFactory.Place(playingStatus.rectTransform, new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(130f, 40f));
            Button logOut = UIFactory.CreateButton("Log Out", bar.rectTransform, ButtonColor, "Log Out", 15);
            UIFactory.Place((RectTransform)logOut.transform, new Vector2(1f, 0.5f), new Vector2(-6f, 0f), new Vector2(100f, 34f));
            logOut.onClick.AddListener(() => bootstrap.Shutdown());
        }

        private static Text Label(Transform parent, string text, int size, Color color, TextAnchor anchor)
        {
            Text label = UIFactory.CreateText("Text", parent, size, anchor);
            label.text = text;
            label.color = color;
            label.raycastTarget = false;
            return label;
        }

        private static Image Window(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            Image panel = UIFactory.CreateImage(name, parent, new Color(0.08f, 0.07f, 0.06f, 0.96f));
            UIFactory.AddFrame(panel);
            UIFactory.Place(panel.rectTransform, anchor, position, size);
            return panel;
        }

        private static Text Heading(RectTransform panel, string text, float y)
        {
            Text heading = Label(panel, text, 30, Gold, TextAnchor.MiddleCenter);
            heading.fontStyle = FontStyle.Bold;
            UIFactory.Place(heading.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(panel.sizeDelta.x - 40f, 44f));
            return heading;
        }

        /// <summary>A text box: a dark well with a thin frame, its placeholder in grey.</summary>
        private static InputField Field(RectTransform parent, string placeholder, Vector2 anchor, Vector2 position, Vector2 size, bool secret = false)
        {
            Image well = UIFactory.CreateImage(placeholder, parent, new Color(0.03f, 0.03f, 0.03f, 0.95f));
            UIFactory.StyleBox(well);
            UIFactory.Place(well.rectTransform, anchor, position, size);
            Text text = UIFactory.CreateText("Text", well.transform, 22, TextAnchor.MiddleLeft);
            text.supportRichText = false;
            text.color = Ink;
            UIFactory.Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(14f, 2f);
            text.rectTransform.offsetMax = new Vector2(-14f, -2f);
            Text hint = UIFactory.CreateText("Placeholder", well.transform, 20, TextAnchor.MiddleLeft);
            hint.text = placeholder;
            hint.fontStyle = FontStyle.Italic;
            hint.color = new Color(0.5f, 0.48f, 0.44f);
            UIFactory.Stretch(hint.rectTransform);
            hint.rectTransform.offsetMin = new Vector2(14f, 2f);
            hint.rectTransform.offsetMax = new Vector2(-14f, -2f);
            var field = well.gameObject.AddComponent<InputField>();
            field.textComponent = text;
            field.placeholder = hint;
            field.targetGraphic = well;
            field.caretColor = Gold;
            field.caretWidth = 2;
            field.customCaretColor = true;
            field.selectionColor = new Color(0.8f, 0.6f, 0.2f, 0.45f);
            field.characterLimit = AccountService.MaxNameLength;
            if (secret)
            {
                field.contentType = InputField.ContentType.Password;
                field.characterLimit = 64;
            }
            return field;
        }

        private static Button Big(RectTransform parent, string label, Vector2 anchor, Vector2 position, Vector2 size, int fontSize = 22)
        {
            Button button = UIFactory.CreateButton(label, parent, ButtonColor, label, fontSize);
            UIFactory.Place((RectTransform)button.transform, anchor, position, size);
            return button;
        }

        private void BuildLogin(RectTransform rootRect)
        {
            Image panel = Window(rootRect, "Log In", new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(560f, 620f));
            loginPanel = panel.gameObject;
            RectTransform rect = panel.rectTransform;
            Heading(rect, "Log In", -34f);

            accountField = Field(rect, "Account name", new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(460f, 52f));
            accountField.text = account;
            passwordField = Field(rect, "Password", new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(460f, 52f), true);

            Button logIn = Big(rect, "Log In", new Vector2(0.5f, 1f), new Vector2(0f, -222f), new Vector2(460f, 56f), 24);
            logIn.onClick.AddListener(LogIn);
            Button register = Big(rect, "Create Account", new Vector2(0.5f, 1f), new Vector2(0f, -286f), new Vector2(460f, 44f), 18);
            register.onClick.AddListener(Register);

            UIFactory.AddDivider(rect, -352f, 480f);
            Text where = Label(rect, "Where to play", 18, Faint, TextAnchor.MiddleCenter);
            UIFactory.Place(where.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -372f), new Vector2(460f, 26f));
            var modes = new List<(Mode mode, string label)> { (Mode.Offline, "Offline"), (Mode.Join, "Join Server") };
            if (!NetworkBootstrap.IsBrowser) modes.Add((Mode.Host, "Host"));
            float width = (460f - (modes.Count - 1) * 10f) / modes.Count;
            for (int i = 0; i < modes.Count; i++)
            {
                Mode choice = modes[i].mode;
                Button button = UIFactory.CreateButton(modes[i].label, rect, CardColor, modes[i].label, 17);
                UIFactory.StyleBox((Image)button.targetGraphic);
                UIFactory.Place((RectTransform)button.transform, new Vector2(0f, 1f), new Vector2(50f + i * (width + 10f), -404f), new Vector2(width, 42f));
                button.onClick.AddListener(() => ChooseMode(choice));
                modeButtons.Add(button);
            }

            RectTransform server = UIFactory.CreateRect("Server", rect);
            UIFactory.Place(server, new Vector2(0.5f, 1f), new Vector2(0f, -460f), new Vector2(460f, 48f));
            serverRow = server.gameObject;
            addressField = Field(server, "Server address", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(330f, 46f));
            addressField.characterLimit = 64;
            addressField.text = PlayerPrefs.GetString(AddressKey, NetworkLaunchOptions.DefaultAddress);
            portField = Field(server, "Port", new Vector2(1f, 0.5f), Vector2.zero, new Vector2(120f, 46f));
            portField.characterLimit = 5;
            portField.contentType = InputField.ContentType.IntegerNumber;
            portField.text = NetworkLaunchOptions.DefaultPort.ToString();

            loginMessage = Label(rect, "", 17, ErrorColor, TextAnchor.UpperCenter);
            UIFactory.Place(loginMessage.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(480f, 56f));

            if (!NetworkBootstrap.IsBrowser)
            {
                // A dedicated server (no player), under the window.
                Button serverOnly = Big(rect, "Run a Server", new Vector2(1f, 0f), new Vector2(0f, -56f), new Vector2(180f, 40f), 15);
                serverOnly.onClick.AddListener(() => bootstrap.StartServer(Port()));
            }
        }

        private void BuildCharacters(RectTransform rootRect)
        {
            RectTransform screenRect = UIFactory.CreateRect("Characters", rootRect);
            UIFactory.Stretch(screenRect);
            charactersPanel = screenRect.gameObject;

            // The list on the left.
            Image list = Window(screenRect, "List", new Vector2(0f, 0.5f), new Vector2(70f, -60f), new Vector2(470f, 760f));
            RectTransform listRect = list.rectTransform;
            countText = Heading(listRect, "Characters", -34f);
            cardList = UIFactory.CreateRect("Cards", listRect);
            UIFactory.Place(cardList, new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(420f, 600f));
            for (int i = 0; i < AccountService.MaxCharacters; i++)
            {
                int index = i;
                Button button = UIFactory.CreateButton("Card", cardList, CardColor, null, 0);
                var background = (Image)button.targetGraphic;
                UIFactory.StyleBox(background);
                UIFactory.Place((RectTransform)button.transform, new Vector2(0.5f, 1f), new Vector2(0f, -i * 98f), new Vector2(420f, 90f));
                RectTransform heroBox = UIFactory.CreateRect("Hero", button.transform);
                UIFactory.Place(heroBox, new Vector2(0f, 0f), new Vector2(18f, 6f), new Vector2(60f, 78f));
                var card = new Card
                {
                    Button = button,
                    Background = background,
                    Hero = HeroPreview.Create(heroBox, avatars),
                    Name = Label(button.transform, "", 22, Gold, TextAnchor.LowerLeft),
                    Line = Label(button.transform, "", 16, Faint, TextAnchor.UpperLeft),
                };
                card.Name.fontStyle = FontStyle.Bold;
                UIFactory.Place(card.Name.rectTransform, new Vector2(0f, 0.5f), new Vector2(96f, 0f), new Vector2(300f, 34f));
                card.Name.rectTransform.pivot = new Vector2(0f, 0f);
                UIFactory.Place(card.Line.rectTransform, new Vector2(0f, 0.5f), new Vector2(96f, -2f), new Vector2(300f, 30f));
                card.Line.rectTransform.pivot = new Vector2(0f, 1f);
                button.onClick.AddListener(() => ChooseCard(index));
                cards.Add(card);
            }
            Button newCharacter = Big(listRect, "New Character", new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(420f, 50f));
            newCharacter.onClick.AddListener(() => OpenCreate());

            // The chosen character, large, in the middle.
            RectTransform stage = UIFactory.CreateRect("Stage", screenRect);
            UIFactory.Place(stage, new Vector2(0.5f, 0.5f), new Vector2(120f, -40f), new Vector2(520f, 640f));
            Image ground = UIFactory.CreateImage("Ground", stage, new Color(0f, 0f, 0f, 0.45f));
            ground.sprite = UIFactory.CircleSprite;
            ground.raycastTarget = false;
            UIFactory.Place(ground.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(300f, 70f));
            RectTransform heroArea = UIFactory.CreateRect("Hero", stage);
            UIFactory.Place(heroArea, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(360f, 440f));
            bigHero = HeroPreview.Create(heroArea, avatars);
            heroName = Label(stage, "", 40, Gold, TextAnchor.MiddleCenter);
            heroName.fontStyle = FontStyle.Bold;
            heroName.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2f, -3f);
            UIFactory.Place(heroName.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(600f, 54f));
            heroLine = Label(stage, "", 22, Ink, TextAnchor.MiddleCenter);
            UIFactory.Place(heroLine.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(600f, 32f));

            // The actions on the right.
            Image actions = Window(screenRect, "Actions", new Vector2(1f, 0.5f), new Vector2(-70f, -60f), new Vector2(380f, 330f));
            RectTransform actionsRect = actions.rectTransform;
            enterButton = Big(actionsRect, "Enter World", new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(320f, 70f), 28);
            enterButton.onClick.AddListener(EnterWorld);
            deleteButton = Big(actionsRect, "Delete Character", new Vector2(0.5f, 1f), new Vector2(0f, -126f), new Vector2(320f, 46f), 18);
            deleteButton.onClick.AddListener(AskDelete);
            Button back = Big(actionsRect, "Log Out", new Vector2(0.5f, 1f), new Vector2(0f, -184f), new Vector2(320f, 46f), 18);
            back.onClick.AddListener(BackToLogin);
            charactersMessage = Label(actionsRect, "", 16, ErrorColor, TextAnchor.UpperCenter);
            UIFactory.Place(charactersMessage.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(330f, 70f));
        }

        private void BuildCreate(RectTransform rootRect)
        {
            RectTransform screenRect = UIFactory.CreateRect("New Character", rootRect);
            UIFactory.Stretch(screenRect);
            createPanel = screenRect.gameObject;

            // Classes on the left.
            Image left = Window(screenRect, "Class", new Vector2(0f, 0.5f), new Vector2(70f, -60f), new Vector2(470f, 600f));
            RectTransform leftRect = left.rectTransform;
            Heading(leftRect, "Class", -34f);
            int classCount = classes != null ? classes.Count : 0;
            for (int i = 0; i < classCount; i++)
            {
                int index = i;
                string className = classes.Get(i).displayName;
                Button button = UIFactory.CreateButton(className, leftRect, CardColor, className, 24);
                UIFactory.StyleBox((Image)button.targetGraphic);
                UIFactory.Place((RectTransform)button.transform, new Vector2(0.5f, 1f), new Vector2(0f, -84f - i * 78f), new Vector2(410f, 66f));
                button.onClick.AddListener(() => ChooseClass(index));
                classButtons.Add(button);
            }
            classBlurb = Label(leftRect, "", 19, Ink, TextAnchor.UpperLeft);
            UIFactory.Place(classBlurb.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(410f, 220f));

            // The new hero in the middle.
            RectTransform stage = UIFactory.CreateRect("Stage", screenRect);
            UIFactory.Place(stage, new Vector2(0.5f, 0.5f), new Vector2(120f, -40f), new Vector2(520f, 640f));
            Image ground = UIFactory.CreateImage("Ground", stage, new Color(0f, 0f, 0f, 0.45f));
            ground.sprite = UIFactory.CircleSprite;
            ground.raycastTarget = false;
            UIFactory.Place(ground.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(300f, 70f));
            RectTransform heroArea = UIFactory.CreateRect("Hero", stage);
            UIFactory.Place(heroArea, new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(360f, 440f));
            newHero = HeroPreview.Create(heroArea, avatars);

            // Name, man or woman, create on the right.
            Image right = Window(screenRect, "Details", new Vector2(1f, 0.5f), new Vector2(-70f, -60f), new Vector2(380f, 420f));
            RectTransform rightRect = right.rectTransform;
            Heading(rightRect, "New Character", -34f);
            nameField = Field(rightRect, "Character name", new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(320f, 52f));
            manButton = UIFactory.CreateButton("Man", rightRect, CardColor, "Man", 19);
            UIFactory.StyleBox((Image)manButton.targetGraphic);
            UIFactory.Place((RectTransform)manButton.transform, new Vector2(0f, 1f), new Vector2(30f, -156f), new Vector2(155f, 46f));
            manButton.onClick.AddListener(() => ChooseGender(false));
            womanButton = UIFactory.CreateButton("Woman", rightRect, CardColor, "Woman", 19);
            UIFactory.StyleBox((Image)womanButton.targetGraphic);
            UIFactory.Place((RectTransform)womanButton.transform, new Vector2(1f, 1f), new Vector2(-30f, -156f), new Vector2(155f, 46f));
            womanButton.onClick.AddListener(() => ChooseGender(true));
            Button create = Big(rightRect, "Create", new Vector2(0.5f, 1f), new Vector2(0f, -226f), new Vector2(320f, 60f), 26);
            create.onClick.AddListener(CreateCharacter);
            Button back = Big(rightRect, "Back", new Vector2(0.5f, 1f), new Vector2(0f, -298f), new Vector2(320f, 44f), 18);
            back.onClick.AddListener(() => ShowScreen(Screen.Characters));
            createMessage = Label(rightRect, "", 16, ErrorColor, TextAnchor.UpperCenter);
            UIFactory.Place(createMessage.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(330f, 52f));
        }

        private void BuildConfirm(RectTransform rootRect)
        {
            Image dim = UIFactory.CreateImage("Confirm", rootRect, new Color(0f, 0f, 0f, 0.6f));
            UIFactory.Stretch(dim.rectTransform);
            confirmPanel = dim.gameObject;
            Image panel = Window(dim.rectTransform, "Box", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 240f));
            confirmText = Label(panel.rectTransform, "", 22, Ink, TextAnchor.MiddleCenter);
            UIFactory.Place(confirmText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(480f, 100f));
            Button yes = Big(panel.rectTransform, "Delete", new Vector2(0.5f, 0f), new Vector2(-110f, 30f), new Vector2(190f, 50f));
            ((Image)yes.targetGraphic).color = new Color(1f, 0.7f, 0.65f);
            yes.onClick.AddListener(DeleteCharacter);
            Button no = Big(panel.rectTransform, "Cancel", new Vector2(0.5f, 0f), new Vector2(110f, 30f), new Vector2(190f, 50f));
            no.onClick.AddListener(() => confirmPanel.SetActive(false));
            confirmPanel.SetActive(false);
        }

        // ---------- Showing ----------

        private void ShowScreen(Screen next)
        {
            screen = next;
            loginPanel.SetActive(next == Screen.Login);
            charactersPanel.SetActive(next == Screen.Characters);
            createPanel.SetActive(next == Screen.Create);
            confirmPanel.SetActive(false);
            if (next == Screen.Login)
            {
                RefreshModes();
                loginMessage.text = message ?? "";
                Focus(string.IsNullOrEmpty(accountField.text) ? accountField : passwordField);
            }
            else if (next == Screen.Characters)
            {
                RefreshCharacters();
            }
            else
            {
                createMessage.text = "";
                RefreshCreate();
                Focus(nameField);
            }
            message = null;
        }

        private static void Focus(InputField field)
        {
            field.Select();
            field.ActivateInputField();
        }

        private void RefreshModes()
        {
            string[] labels = { "Offline", "Join Server", "Host" };
            foreach (Button button in modeButtons)
            {
                Text label = button.GetComponentInChildren<Text>();
                bool on = label != null && label.text == labels[(int)mode];
                ((Image)button.targetGraphic).color = on ? new Color(1f, 0.85f, 0.5f) : Color.white;
                if (label != null) label.color = on ? Gold : Faint;
            }
            serverRow.SetActive(mode != Mode.Offline);
            addressField.gameObject.SetActive(mode == Mode.Join);
            // As tall as what it shows: the server's address and port only when not playing offline.
            var panel = (RectTransform)loginPanel.transform;
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, mode == Mode.Offline ? 530f : 600f);
        }

        private void RefreshCharacters()
        {
            countText.text = $"Characters  {characters.Length} / {AccountService.MaxCharacters}";
            chosen = Mathf.Clamp(chosen, 0, Mathf.Max(0, characters.Length - 1));
            for (int i = 0; i < cards.Count; i++)
            {
                Card card = cards[i];
                bool used = i < characters.Length;
                card.Button.gameObject.SetActive(used);
                if (!used) continue;
                CharacterSummary character = characters[i];
                card.Name.text = character.name;
                card.Line.text = $"Level {character.level} {ClassName(character.classIndex)}";
                card.Hero.Show(character.female, character.looks);
                bool on = i == chosen;
                card.Background.color = on ? new Color(1f, 0.82f, 0.45f) : Color.white;
                card.Name.color = on ? Gold : Ink;
            }
            bool any = characters.Length > 0;
            bigHero.gameObject.SetActive(any);
            enterButton.interactable = any;
            deleteButton.interactable = any;
            if (any)
            {
                CharacterSummary character = characters[chosen];
                bigHero.Show(character.female, character.looks);
                heroName.text = character.name;
                heroLine.text = $"Level {character.level} {ClassName(character.classIndex)}";
            }
            else
            {
                heroName.text = "No characters yet";
                heroLine.text = "Create one to begin.";
            }
            charactersMessage.text = message ?? "";
        }

        private void RefreshCreate()
        {
            newClass = Mathf.Clamp(newClass, 0, Mathf.Max(0, classButtons.Count - 1));
            for (int i = 0; i < classButtons.Count; i++)
            {
                bool on = i == newClass;
                ((Image)classButtons[i].targetGraphic).color = on ? new Color(1f, 0.82f, 0.45f) : Color.white;
                Text label = classButtons[i].GetComponentInChildren<Text>();
                if (label != null) label.color = on ? Gold : Ink;
            }
            classBlurb.text = newClass < ClassBlurbs.Length ? ClassBlurbs[newClass] : "";
            foreach ((Button button, bool female) in new[] { (manButton, false), (womanButton, true) })
            {
                bool on = female == newFemale;
                ((Image)button.targetGraphic).color = on ? new Color(1f, 0.82f, 0.45f) : Color.white;
                Text label = button.GetComponentInChildren<Text>();
                if (label != null) label.color = on ? Gold : Ink;
            }
            newHero.Show(newFemale, HeroPreview.LooksOf(StartingItem));
        }

        /// <summary>The new character's starting piece for a slot (its class's), so the preview wears it.</summary>
        private ItemDefinition StartingItem(ItemSlot slot)
        {
            if (gearSource == null) return null;
            foreach (Inventory.ClassGear gear in gearSource.StartingGear)
                if (gear.classIndex == newClass && gear.item != null && gear.item.slot == slot) return gear.item;
            return null;
        }

        private string ClassName(int index)
        {
            CharacterClassDefinition definition = classes != null ? classes.Get(index) : null;
            return definition != null ? definition.displayName : "Adventurer";
        }

        // ---------- Every frame ----------

        private void Update()
        {
            if (root == null) return;
            NetworkManager network = bootstrap.Network;
            bool running = network.IsListening || network.ShutdownInProgress;
            bool playing = running && !bootstrap.AwaitingReply;

            if (wasRunning && !running) busy = false;
            wasRunning = running;
            if (playing != wasPlaying)
            {
                wasPlaying = playing;
                busy = false;
                if (!playing)
                {
                    // Back from the world (logged out, or the connection was lost or refused).
                    message = bootstrap.LastDisconnectReason;
                    if (!string.IsNullOrEmpty(password)) RequestList(Screen.Characters);
                    else ShowScreen(Screen.Login);
                }
            }

            if (screens.activeSelf == playing) screens.SetActive(!playing);
            if (playingBar.activeSelf != playing) playingBar.SetActive(playing);
            if (playing)
            {
                UpdatePlayingStatus(network);
                return;
            }
            string waiting = busy || bootstrap.AwaitingReply ? "Please wait..." : "";
            if (busyText.text != waiting) busyText.text = waiting;

            // The island drifts slowly behind the screens.
            float t = Time.unscaledTime * 0.01f;
            backdrop.uvRect = new Rect(0.25f + Mathf.Sin(t) * 0.12f, 0.2f + Mathf.Cos(t * 0.7f) * 0.1f, 0.5f, 0.5f);

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || busy) return;
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                if (confirmPanel.activeSelf) DeleteCharacter();
                else if (screen == Screen.Login) LogIn();
                else if (screen == Screen.Characters) EnterWorld();
                else CreateCharacter();
            }
            else if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (confirmPanel.activeSelf) confirmPanel.SetActive(false);
                else if (screen == Screen.Create) ShowScreen(Screen.Characters);
                else if (screen == Screen.Characters) BackToLogin();
            }
            else if (keyboard.tabKey.wasPressedThisFrame && screen == Screen.Login)
            {
                Focus(accountField.isFocused ? passwordField : accountField);
            }
        }

        private string lastStatus;

        private void UpdatePlayingStatus(NetworkManager network)
        {
            string status;
            if (network.ShutdownInProgress) status = "Leaving...";
            else if (bootstrap.IsOffline) status = "Offline";
            else if (network.IsHost) status = $"Host: {network.ConnectedClientsIds.Count} online";
            else if (network.IsServer) status = $"Server: {network.ConnectedClientsIds.Count} online";
            else if (network.IsConnectedClient) status = "Online";
            else status = "Connecting...";
            if (status == lastStatus) return;
            lastStatus = status;
            playingStatus.text = status;
        }

        // ---------- Actions ----------

        private void ChooseMode(Mode next)
        {
            mode = next;
            RefreshModes();
        }

        private void ChooseCard(int index)
        {
            if (index == chosen && screen == Screen.Characters)
            {
                // A second click on the chosen character enters the world.
                EnterWorld();
                return;
            }
            chosen = index;
            RefreshCharacters();
        }

        private void ChooseClass(int index)
        {
            newClass = index;
            RefreshCreate();
        }

        private void ChooseGender(bool female)
        {
            newFemale = female;
            RefreshCreate();
        }

        private void OpenCreate()
        {
            if (characters.Length >= AccountService.MaxCharacters)
            {
                charactersMessage.text = $"An account holds at most {AccountService.MaxCharacters} characters. Delete one first.";
                return;
            }
            nameField.text = "";
            ShowScreen(Screen.Create);
        }

        private ushort Port() => ushort.TryParse(portField.text, out ushort parsed) ? parsed : NetworkLaunchOptions.DefaultPort;

        private LoginRequest Request(AccountAction action, string character = "") =>
            new LoginRequest { username = account, password = password, action = action, character = character };

        private void LogIn()
        {
            account = accountField.text.Trim();
            password = passwordField.text;
            Remember();
            RequestList(Screen.Characters);
        }

        private void Register()
        {
            account = accountField.text.Trim();
            password = passwordField.text;
            Remember();
            Send(Request(AccountAction.Register), reply =>
            {
                characters = reply.characters ?? System.Array.Empty<CharacterSummary>();
                chosen = 0;
                OpenCreate();
            }, Screen.Login);
        }

        private void RequestList(Screen then)
        {
            Send(Request(AccountAction.List), reply =>
            {
                characters = reply.characters ?? System.Array.Empty<CharacterSummary>();
                if (characters.Length == 0) OpenCreate();
                else ShowScreen(then);
            }, Screen.Login);
        }

        private void CreateCharacter()
        {
            LoginRequest request = Request(AccountAction.CreateCharacter, nameField.text.Trim());
            request.classIndex = newClass;
            request.female = newFemale;
            string wanted = request.character;
            Send(request, reply =>
            {
                characters = reply.characters ?? System.Array.Empty<CharacterSummary>();
                chosen = System.Array.FindIndex(characters, c => AccountService.NormalizeName(c.name) == AccountService.NormalizeName(wanted));
                ShowScreen(Screen.Characters);
            }, Screen.Create);
        }

        private void AskDelete()
        {
            if (chosen < 0 || chosen >= characters.Length) return;
            CharacterSummary character = characters[chosen];
            confirmText.text = $"Delete {character.name}, level {character.level} {ClassName(character.classIndex)}?\nThis cannot be undone.";
            confirmPanel.SetActive(true);
        }

        private void DeleteCharacter()
        {
            confirmPanel.SetActive(false);
            if (chosen < 0 || chosen >= characters.Length) return;
            Send(Request(AccountAction.DeleteCharacter, characters[chosen].name), reply =>
            {
                characters = reply.characters ?? System.Array.Empty<CharacterSummary>();
                chosen = 0;
                ShowScreen(Screen.Characters);
            }, Screen.Characters);
        }

        private void EnterWorld()
        {
            if (busy || chosen < 0 || chosen >= characters.Length) return;
            bootstrap.SetLogin(Request(AccountAction.Play, characters[chosen].name));
            busy = true;
            bool started = mode == Mode.Join ? bootstrap.StartClient(addressField.text.Trim(), Port())
                : mode == Mode.Host ? bootstrap.StartHost(Port())
                : bootstrap.StartOffline();
            if (started) return;
            busy = false;
            message = bootstrap.LastDisconnectReason ?? "Could not start the game.";
            RefreshCharacters();
        }

        private void BackToLogin()
        {
            password = "";
            passwordField.text = "";
            characters = System.Array.Empty<CharacterSummary>();
            ShowScreen(Screen.Login);
        }

        /// <summary>Sends an account action (to this game's accounts, or the server when joining) and shows its answer.</summary>
        private void Send(LoginRequest request, System.Action<AccountReply> done, Screen onError)
        {
            if (busy) return;
            busy = true;
            bootstrap.RequestAccount(request, mode == Mode.Join, addressField.text.Trim(), Port(), reply =>
            {
                busy = false;
                if (reply == null || !string.IsNullOrEmpty(reply.error))
                {
                    message = reply != null ? reply.error : "No answer.";
                    if (onError == Screen.Login) password = "";
                    ShowScreen(onError);
                    return;
                }
                done(reply);
            });
        }

        private void Remember()
        {
            PlayerPrefs.SetString(NameKey, account);
            PlayerPrefs.SetInt(ModeKey, (int)mode);
            PlayerPrefs.SetString(AddressKey, addressField.text.Trim());
            PlayerPrefs.Save();
        }
    }
}
