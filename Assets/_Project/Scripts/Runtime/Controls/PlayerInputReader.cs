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
        private readonly InputAction[] abilityActions = new InputAction[AbilityKeyCount];

        /// <summary>Number of ability keys (1, 2, 3...).</summary>
        public const int AbilityKeyCount = 5;

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

        /// <summary>Esc: clear the target / cancel.</summary>
        public bool CancelPressed => cancelAction.WasPressedThisFrame();

        /// <summary>True on the frame an ability key (1, 2, 3...) went down; slot 0 is key 1.</summary>
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
            for (int i = 0; i < abilityActions.Length; i++)
                abilityActions[i] = new InputAction($"Ability{i + 1}", InputActionType.Button, $"<Keyboard>/{i + 1}");
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
            foreach (InputAction action in abilityActions) action.Dispose();
        }
    }
}
