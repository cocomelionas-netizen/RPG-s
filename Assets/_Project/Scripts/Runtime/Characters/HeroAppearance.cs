using System;
using System.Collections.Generic;
using RPG.Items;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

namespace RPG.Characters
{
    /// <summary>
    /// Draws a hero from the gear it wears: one sprite per layer (head, chest, legs, feet, hands, weapon, shield), each the
    /// look of the worn item (<see cref="ItemDefinition.appearance"/>) or the bare body, for the hero's gender, in Flare's
    /// layer order for the direction it faces. The layers play the frame the <see cref="CharacterAnimator"/> plays (it runs
    /// the chest layer's clips); an attack plays the weapon's swing, shot or cast. Gender is the server's, seen by all.
    /// An empowered weapon glows along its edges, like an enchanted weapon in Lineage-style games: white from +4, blue from
    /// +7, orange from +10 to +12, brighter every step and shimmering (<see cref="GlowOf"/>). Without a catalog the hero keeps its single sprite.
    /// </summary>
    [RequireComponent(typeof(CharacterAnimator))]
    public class HeroAppearance : NetworkBehaviour
    {
        public const string SwingClip = "Swing";
        public const string ShootClip = "Shoot";
        public const string CastClip = "Cast";
        private const string AttackClip = "Attack";
        private static readonly AvatarSlot[] Slots = (AvatarSlot[])Enum.GetValues(typeof(AvatarSlot));

        [SerializeField] private AvatarCatalog catalog;
        [Tooltip("Additive material for the glow of an empowered weapon (RPG/SpriteGlow); none: no glow.")]
        [SerializeField] private Material glowMaterial;

        /// <summary>Copies of the weapon around it that make its glow.</summary>
        private static readonly Vector2[] GlowOffsets =
        {
            new Vector2(1f, 0f), new Vector2(-1f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f),
            new Vector2(0.7f, 0.7f), new Vector2(-0.7f, 0.7f), new Vector2(0.7f, -0.7f), new Vector2(-0.7f, -0.7f),
        };
        // Behind the weapon: a bright, nearly white edge right around it, its colour farther out and a faint halo, so the
        // weapon itself stays seen with a glowing outline; over it only a light sheen of the colour.
        private static readonly float[] GlowRings = { 0.009f, 0.02f, 0.042f };
        private static readonly float[] GlowRingStrength = { 0.45f, 0.28f, 0.14f };
        private const float SheenStrength = 0.3f;
        private static readonly float[] GlowRingWhiteness = { 0.6f, 0.1f, 0f };

        private readonly NetworkVariable<bool> female = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private CharacterAnimator characterAnimator;
        private SpriteRenderer body;
        private Inventory inventory;
        private SpriteRenderer[] layers;
        private readonly CharacterAnimationSet[] looks = new CharacterAnimationSet[Slots.Length];
        private string attackClip = SwingClip;
        private Direction8? orderedFor;
        private SpriteRenderer[] glow;
        private Color glowColor;

        public Material GlowMaterial
        {
            get => glowMaterial;
            set => glowMaterial = value;
        }

        public bool IsFemale => female.Value;

        /// <summary>Whether the hero is drawn in layers (a catalog is set).</summary>
        public bool HasLayers => catalog != null;

        public AvatarCatalog Catalog
        {
            get => catalog;
            set => catalog = value;
        }

        private void Awake()
        {
            characterAnimator = GetComponent<CharacterAnimator>();
            body = GetComponent<SpriteRenderer>();
            inventory = GetComponent<Inventory>();
            if (catalog == null) return;

            // The layers sort together as one sprite at the hero's feet; the health bar stays outside, over the world.
            var root = new GameObject("Avatar");
            root.transform.SetParent(transform, false);
            var group = root.AddComponent<SortingGroup>();
            if (body != null)
            {
                group.sortingLayerID = body.sortingLayerID;
                group.sortingOrder = body.sortingOrder;
                body.enabled = false;
            }
            layers = new SpriteRenderer[Slots.Length];
            for (int i = 0; i < Slots.Length; i++)
            {
                var layer = new GameObject(Slots[i].ToString()).AddComponent<SpriteRenderer>();
                layer.transform.SetParent(root.transform, false);
                layers[i] = layer;
            }
            if (glowMaterial != null)
            {
                // The rings' copies, then the sheen (the last one, on the weapon itself).
                glow = new SpriteRenderer[GlowOffsets.Length * GlowRings.Length + 1];
                for (int i = 0; i < glow.Length; i++)
                {
                    var copy = new GameObject("WeaponGlow").AddComponent<SpriteRenderer>();
                    copy.transform.SetParent(root.transform, false);
                    copy.transform.localPosition = i < glow.Length - 1
                        ? (Vector3)(GlowOffsets[i % GlowOffsets.Length] * GlowRings[i / GlowOffsets.Length])
                        : Vector3.zero;
                    copy.sharedMaterial = glowMaterial;
                    copy.enabled = false;
                    glow[i] = copy;
                }
            }
            characterAnimator.ClipAlias = clip => clip == AttackClip ? attackClip : clip;
        }

        // After Awake: the inventory's lists exist by then.
        private void Start() => Refresh();

        private void OnEnable()
        {
            if (inventory != null) inventory.Changed += Refresh;
        }

        private void OnDisable()
        {
            if (inventory != null) inventory.Changed -= Refresh;
        }

        public override void OnNetworkSpawn()
        {
            female.OnValueChanged += OnGenderChanged;
            Refresh();
        }

        public override void OnNetworkDespawn()
        {
            female.OnValueChanged -= OnGenderChanged;
        }

        /// <summary>Server: the hero's gender (saved with the character).</summary>
        public void ServerSetFemale(bool value)
        {
            if (IsServer) female.Value = value;
        }

        private void OnGenderChanged(bool previous, bool current) => Refresh();

        /// <summary>Picks every layer's look from the worn gear and the gender.</summary>
        private void Refresh()
        {
            if (catalog == null || layers == null) return;
            bool isFemale = female.Value;
            for (int i = 0; i < Slots.Length; i++)
            {
                AvatarSlot slot = Slots[i];
                ItemDefinition item = Worn(slot);
                looks[i] = LookOf(catalog, isFemale, slot, item != null ? item.appearance : null);
            }
            attackClip = AttackClipOf(Worn(AvatarSlot.MainHand));
            glowColor = inventory != null && Worn(AvatarSlot.MainHand) != null
                ? GlowOf(inventory.GetEquipped(ItemSlot.Weapon).EmpowerSteps)
                : Color.clear;
            CharacterAnimationSet chest = looks[(int)AvatarSlot.Chest];
            if (chest != null) characterAnimator.AnimationSet = chest;
        }

        private ItemDefinition Worn(AvatarSlot slot)
        {
            if (inventory == null || inventory.Catalog == null) return null;
            return inventory.Catalog.Get(inventory.GetEquipped(ItemSlotOf(slot)).ItemId);
        }

        /// <summary>The equipment slot a layer shows.</summary>
        public static ItemSlot ItemSlotOf(AvatarSlot slot)
        {
            switch (slot)
            {
                case AvatarSlot.Head: return ItemSlot.Head;
                case AvatarSlot.Chest: return ItemSlot.Chest;
                case AvatarSlot.Legs: return ItemSlot.Legs;
                case AvatarSlot.Feet: return ItemSlot.Feet;
                case AvatarSlot.Hands: return ItemSlot.Hands;
                case AvatarSlot.MainHand: return ItemSlot.Weapon;
                default: return ItemSlot.Shield;
            }
        }

        /// <summary>A layer's look: the worn item's <paramref name="appearance"/>, or the bare look when it has none.</summary>
        public static CharacterAnimationSet LookOf(AvatarCatalog catalog, bool female, AvatarSlot slot, string appearance)
        {
            if (catalog == null) return null;
            string look = !string.IsNullOrEmpty(appearance) ? appearance : AvatarCatalog.BareLook(slot);
            return catalog.Find(female, slot, look) ?? catalog.Find(female, slot, AvatarCatalog.BareLook(slot));
        }

        /// <summary>
        /// The glow of a weapon empowered <paramref name="steps"/> times (alpha: how strong, 0 none): none below +4, white at
        /// +4 to +6, blue at +7 to +9 (deeper each step), orange at +10 to +12, brighter each step.
        /// </summary>
        public static Color GlowOf(int steps)
        {
            switch (Mathf.Clamp(steps, 0, ItemDefinition.MaxEmpowerSteps))
            {
                case 4: return new Color(1f, 1f, 1f, 0.35f);
                case 5: return new Color(1f, 1f, 1f, 0.5f);
                case 6: return new Color(1f, 1f, 1f, 0.65f);
                case 7: return new Color(0.65f, 0.8f, 1f, 0.6f);
                case 8: return new Color(0.4f, 0.55f, 1f, 0.75f);
                case 9: return new Color(0.25f, 0.4f, 1f, 0.9f);
                case 10: return new Color(1f, 0.62f, 0.22f, 0.75f);
                case 11: return new Color(1f, 0.52f, 0.12f, 0.88f);
                case 12: return new Color(1f, 0.45f, 0.06f, 1f);
                default: return Color.clear;
            }
        }

        /// <summary>The attack a weapon plays: bows shoot, staves and wands cast, the rest (and bare hands) swing.</summary>
        public static string AttackClipOf(ItemDefinition weapon)
        {
            if (weapon == null) return SwingClip;
            switch (weapon.weaponType)
            {
                case WeaponType.Ranged: return ShootClip;
                case WeaponType.Staff:
                case WeaponType.Wand: return CastClip;
                default: return SwingClip;
            }
        }

        private void LateUpdate()
        {
            if (layers == null) return;
            Direction8 direction = characterAnimator.Direction;
            if (orderedFor != direction)
            {
                AvatarSlot[] order = catalog.OrderFor(direction);
                // Every other order, so the weapon's glow fits right under the weapon and its sheen right over it.
                for (int k = 0; k < order.Length; k++)
                    layers[(int)order[k]].sortingOrder = k * 2;
                orderedFor = direction;
            }

            string clip = characterAnimator.CurrentClip;
            int frame = characterAnimator.CurrentFrame;
            Color color = body != null ? body.color : Color.white;
            for (int i = 0; i < layers.Length; i++)
            {
                SpriteRenderer layer = layers[i];
                CharacterAnimationSet look = looks[i];
                if (look == null || clip == null || !look.TryGetFrames(clip, direction, out _, out Sprite[] frames, out bool flipX))
                {
                    layer.sprite = null;
                    continue;
                }
                layer.sprite = frames[Mathf.Min(frame, frames.Length - 1)];
                layer.flipX = flipX;
                layer.color = color;
            }
            UpdateGlow();
        }

        private void UpdateGlow()
        {
            if (glow == null) return;
            SpriteRenderer weapon = layers[(int)AvatarSlot.MainHand];
            bool on = glowColor.a > 0f && weapon.sprite != null && (body == null || body.color.a > 0.5f);
            // A slow pulse, and a shimmer that runs along the blade (each copy flickers on its own).
            float pulse = 0.8f + 0.2f * Mathf.Sin(Time.time * (2f + glowColor.a * 3f));
            for (int i = 0; i < glow.Length; i++)
            {
                SpriteRenderer copy = glow[i];
                bool sheen = i == glow.Length - 1;
                int ring = sheen ? 0 : i / GlowOffsets.Length;
                float shimmer = 0.7f + 0.3f * Mathf.PerlinNoise(Time.time * 6f, i * 1.7f);
                Color tint = sheen ? glowColor : Color.Lerp(glowColor, Color.white, GlowRingWhiteness[ring]);
                Color color = new Color(tint.r, tint.g, tint.b,
                    glowColor.a * pulse * shimmer * (sheen ? SheenStrength : GlowRingStrength[ring]));
                copy.enabled = on;
                if (!on) continue;
                copy.sprite = weapon.sprite;
                copy.flipX = weapon.flipX;
                copy.sortingOrder = sheen ? weapon.sortingOrder + 1 : weapon.sortingOrder - 1;
                copy.color = color;
            }
        }

        /// <summary>The layers of the hero standing and facing the viewer, first drawn first (for its portrait).</summary>
        public void PortraitLayers(List<Sprite> sprites)
        {
            sprites.Clear();
            if (catalog == null) return;
            foreach (AvatarSlot slot in catalog.OrderFor(Direction8.South))
            {
                CharacterAnimationSet look = looks[(int)slot];
                if (look != null && look.TryGetFrames(CharacterAnimationSet.IdleClip, Direction8.South, out _, out Sprite[] frames, out _))
                    sprites.Add(frames[0]);
            }
        }
    }
}
