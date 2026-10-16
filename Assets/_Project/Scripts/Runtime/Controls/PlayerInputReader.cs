using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RPG.Controls
{
    /// <summary>
    /// Reads raw player input (WASD / arrows, mouse click, pointer, scroll)
    /// and exposes it as simple values. Gameplay scripts never read devices directly,
    /// so input can later be replaced by network commands or rebinding.
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        private InputAction moveAction;
        private InputAction moveClickAction;
        private InputAction pointAction;
        private InputAction zoomAction;
        private InputAction targetNextAction;
        private InputAction attackAction;
        private InputAction cancelAction;
        private InputAction inventoryAction;
        private InputAction characterAction;
        private InputAction mapAction;
        private InputAction spellbookAction;
        private readonly InputAction[] abilityActions = new InputAction[AbilityKeyCount];

        /// <summary>The action bar's keys, one per slot, as shown on the bar.</summary>
        public static readonly string[] AbilityKeys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=" };

        // Their Input System key names.
        private static readonly string[] AbilityKeyPaths = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "minus", "equals" };

        /// <summary>Number of action bar keys (the source game's bar has 12 slots).</summary>
        public const int AbilityKeyCount = 12;

        /// <summary>Keyboard movement in screen space, each axis in [-1, 1].</summary>
        public Vector2 MoveInput => moveAction.ReadValue<Vector2>();

        /// <summary>True while the move mouse button is held.</summary>
        public bool MoveClickHeld => moveClickAction.IsPressed();

        /// <summary>True on the frame the move mouse button went down.</summary>
        public bool MoveClickPressed => moveClickAction.WasPressedThisFrame();

        /// <summary>Tab: select the next target.</summary>
        public bool TargetNextPressed => targetNextAction.WasPressedThisFrame();

        /// <summary>F: attack the selected target.</summary>
        public bool AttackPressed => attackAction.WasPressedThisFrame();

        /// <summary>I or B: open or close the inventory.</summary>
        public bool InventoryPressed => inventoryAction.WasPressedThisFrame();

        /// <summary>C: open or close the character sheet.</summary>
        public bool CharacterPressed => characterAction.WasPressedThisFrame();

        /// <summary>P: open or close the spellbook.</summary>
        public bool SpellbookPressed => spellbookAction.WasPressedThisFrame();

        /// <summary>M: open or close the world map.</summary>
        public bool MapPressed => mapAction.WasPressedThisFrame();

        /// <summary>Esc: clear the target / cancel.</summary>
        public bool CancelPressed => cancelAction.WasPressedThisFrame();

        /// <summary>True on the frame an action bar key went down; slot 0 is key 1, then 2 ... 9, 0, - and =.</summary>
        public bool AbilityPressed(int slot)
        {
            return slot >= 0 && slot < abilityActions.Length && abilityActions[slot].WasPressedThisFrame();
        }

        /// <summary>Pointer position in screen pixels.</summary>
        public Vector2 PointerPosition => pointAction.ReadValue<Vector2>();

        /// <summary>Scroll this frame: positive = zoom in, negative = zoom out, 0 = none.</summary>
        public float ZoomDelta
        {
            get
            {
                float scroll = zoomAction.ReadValue<float>();
                return Mathf.Approximately(scroll, 0f) ? 0f : Mathf.Sign(scroll);
            }
        }

        /// <summary>True when the pointer is over a UI element, so world clicks should be ignored.</summary>
        public bool IsPointerOverUI => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        private void Awake()
        {
            moveAction = new InputAction("Move", InputActionType.Value);
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");

            moveClickAction = new InputAction("MoveClick", InputActionType.Button, "<Mouse>/leftButton");
            pointAction = new InputAction("Point", InputActionType.Value, "<Pointer>/position");
            zoomAction = new InputAction("Zoom", InputActionType.Value, "<Mouse>/scroll/y");
            targetNextAction = new InputAction("TargetNext", InputActionType.Button, "<Keyboard>/tab");
            attackAction = new InputAction("Attack", InputActionType.Button, "<Keyboard>/f");
            cancelAction = new InputAction("Cancel", InputActionType.Button, "<Keyboard>/escape");
            inventoryAction = new InputAction("Inventory", InputActionType.Button, "<Keyboard>/i");
            inventoryAction.AddBinding("<Keyboard>/b");
            characterAction = new InputAction("Character", InputActionType.Button, "<Keyboard>/c");
            mapAction = new InputAction("Map", InputActionType.Button, "<Keyboard>/m");
            spellbookAction = new InputAction("Spellbook", InputActionType.Button, "<Keyboard>/p");
            for (int i = 0; i < abilityActions.Length; i++)
                abilityActions[i] = new InputAction($"Ability{i + 1}", InputActionType.Button, $"<Keyboard>/{AbilityKeyPaths[i]}");
        }

        private void OnEnable()
        {
            moveAction.Enable();
            moveClickAction.Enable();
            pointAction.Enable();
            zoomAction.Enable();
            targetNextAction.Enable();
            attackAction.Enable();
            cancelAction.Enable();
            inventoryAction.Enable();
            characterAction.Enable();
            mapAction.Enable();
            spellbookAction.Enable();
            foreach (InputAction action in abilityActions) action.Enable();
        }

        private void OnDisable()
        {
            moveAction.Disable();
            moveClickAction.Disable();
            pointAction.Disable();
            zoomAction.Disable();
            targetNextAction.Disable();
            attackAction.Disable();
            cancelAction.Disable();
            inventoryAction.Disable();
            characterAction.Disable();
            mapAction.Disable();
            spellbookAction.Disable();
            foreach (InputAction action in abilityActions) action.Disable();
        }

        private void OnDestroy()
        {
            moveAction.Dispose();
            moveClickAction.Dispose();
            pointAction.Dispose();
            zoomAction.Dispose();
            targetNextAction.Dispose();
            attackAction.Dispose();
            cancelAction.Dispose();
            inventoryAction.Dispose();
            characterAction.Dispose();
            mapAction.Dispose();
            spellbookAction.Dispose();
            foreach (InputAction action in abilityActions) action.Dispose();
        }
    }
}
