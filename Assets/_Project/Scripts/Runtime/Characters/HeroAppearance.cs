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
    /// Without a catalog the hero keeps its single sprite.
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

        private readonly NetworkVariable<bool> female = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private CharacterAnimator characterAnimator;
        private SpriteRenderer body;
        private Inventory inventory;
        private SpriteRenderer[] layers;
        private readonly CharacterAnimationSet[] looks = new CharacterAnimationSet[Slots.Length];
        private string attackClip = SwingClip;
        private Direction8? orderedFor;

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
                for (int k = 0; k < order.Length; k++)
                    layers[(int)order[k]].sortingOrder = k;
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
