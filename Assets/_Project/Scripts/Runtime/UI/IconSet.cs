using UnityEngine;

namespace RPG.UI
{
    /// <summary>
    /// The icons cut from the icon atlas by the scene builder (kept inside this asset, in Resources as "Icons"). Items and
    /// spells point at their own; the HUD loads this for the ones that are no asset (the Attack entry).
    /// </summary>
    public class IconSet : ScriptableObject
    {
        public Sprite attack;
        /// <summary>Every icon in the set.</summary>
        public Sprite[] all = System.Array.Empty<Sprite>();

        private static IconSet loaded;
        private static bool tried;

        /// <summary>The project's icons, or null when there are none (tests, a scene built without them).</summary>
        public static IconSet Instance
        {
            get
            {
                if (!tried)
                {
                    tried = true;
                    loaded = Resources.Load<IconSet>("Icons");
                }
                return loaded;
            }
        }

        /// <summary>The Attack entry's icon (crossed swords), or null.</summary>
        public static Sprite Attack => Instance != null ? Instance.attack : null;
    }
}
