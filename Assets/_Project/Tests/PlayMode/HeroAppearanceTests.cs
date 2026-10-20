#if UNITY_EDITOR
using NUnit.Framework;
using RPG.Characters;
using RPG.Items;
using UnityEditor;
using UnityEngine;

namespace RPG.Tests
{
    /// <summary>Heroes drawn from their gear: the looks of the catalog the builder makes and the attack each weapon plays.</summary>
    public class HeroAppearanceTests
    {
        private const string CatalogPath = "Assets/_Project/Data/Characters/AvatarCatalog.asset";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Characters/Player.prefab";

        [Test]
        public void Weapons_PlayTheirOwnAttack()
        {
            ItemDefinition Weapon(WeaponType type)
            {
                var item = ScriptableObject.CreateInstance<ItemDefinition>();
                item.slot = ItemSlot.Weapon;
                item.weaponType = type;
                return item;
            }
            Assert.AreEqual(HeroAppearance.SwingClip, HeroAppearance.AttackClipOf(null), "Bare hands swing.");
            Assert.AreEqual(HeroAppearance.SwingClip, HeroAppearance.AttackClipOf(Weapon(WeaponType.Sword)));
            Assert.AreEqual(HeroAppearance.ShootClip, HeroAppearance.AttackClipOf(Weapon(WeaponType.Ranged)));
            Assert.AreEqual(HeroAppearance.CastClip, HeroAppearance.AttackClipOf(Weapon(WeaponType.Staff)));
            Assert.AreEqual(HeroAppearance.CastClip, HeroAppearance.AttackClipOf(Weapon(WeaponType.Wand)));
        }

        [Test]
        public void Catalog_HasBareBodiesAndArmorForBothGenders()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AvatarCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, "The scene builder makes the avatar catalog.");
            Assert.AreEqual(8, catalog.LayerOrder.Length, "A layer order per direction.");
            foreach (bool female in new[] { false, true })
            {
                foreach (AvatarSlot slot in new[] { AvatarSlot.Head, AvatarSlot.Chest, AvatarSlot.Legs, AvatarSlot.Feet, AvatarSlot.Hands })
                {
                    CharacterAnimationSet bare = catalog.Find(female, slot, AvatarCatalog.BareLook(slot));
                    Assert.IsNotNull(bare, $"{slot} bare ({(female ? "woman" : "man")}).");
                    foreach (string clip in new[] { "Idle", "Walk", HeroAppearance.SwingClip, HeroAppearance.ShootClip, HeroAppearance.CastClip })
                        Assert.IsTrue(bare.TryGetFrames(clip, Direction8.NorthWest, out _, out _, out _), $"{slot} {clip}.");
                }
                Assert.IsNotNull(catalog.Find(female, AvatarSlot.Chest, "plate_cuirass"));
                Assert.IsNotNull(catalog.Find(female, AvatarSlot.MainHand, "longbow"));
                Assert.IsNotNull(catalog.Find(female, AvatarSlot.OffHand, "kite_shield"));
            }
        }

        [Test]
        public void Gear_HasALookOfItsSlot()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<AvatarCatalog>(CatalogPath);
            int checkedItems = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/_Project/Data/Items" }))
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (item == null || string.IsNullOrEmpty(item.appearance)) continue;
                AvatarSlot slot = item.slot == ItemSlot.Weapon ? AvatarSlot.MainHand : item.slot == ItemSlot.Shield ? AvatarSlot.OffHand
                    : item.slot == ItemSlot.Head ? AvatarSlot.Head : item.slot == ItemSlot.Chest ? AvatarSlot.Chest
                    : item.slot == ItemSlot.Legs ? AvatarSlot.Legs : item.slot == ItemSlot.Feet ? AvatarSlot.Feet : AvatarSlot.Hands;
                Assert.IsNotNull(catalog.Find(false, slot, item.appearance), $"{item.displayName}: {item.appearance}.");
                Assert.IsNotNull(catalog.Find(true, slot, item.appearance), $"{item.displayName}: {item.appearance} (woman).");
                checkedItems++;
            }
            Assert.Greater(checkedItems, 100, "Gear gets looks.");
        }

        [Test]
        public void Player_IsDrawnInLayers()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.IsTrue(prefab.TryGetComponent(out HeroAppearance appearance));
            Assert.IsTrue(appearance.HasLayers);
        }
    }
}
#endif
