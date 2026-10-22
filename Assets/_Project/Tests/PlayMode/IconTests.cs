using System.Collections.Generic;
using NUnit.Framework;
using RPG.Items;
using RPG.UI;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.Tests
{
    /// <summary>Item slots show an item's icon (and no name), or its name when it has none.</summary>
    public class IconTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created) Object.DestroyImmediate(o);
            created.Clear();
        }

        private ItemSlotView CreateSlot()
        {
            var go = new GameObject("Slot", typeof(RectTransform));
            created.Add(go);
            ItemSlotView view = go.AddComponent<ItemSlotView>();
            view.Background = go.AddComponent<Image>();
            view.Border = go.AddComponent<Outline>();
            view.Icon = UIFactory.CreateIcon(go.transform, 4f);
            view.Label = UIFactory.CreateText("Name", go.transform, 11, TextAnchor.MiddleCenter);
            return view;
        }

        private ItemDefinition CreateItem(Sprite icon)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            created.Add(item);
            item.displayName = "Bronze Hewer";
            item.icon = icon;
            return item;
        }

        [Test]
        public void Slot_ShowsIconInsteadOfName()
        {
            var texture = new Texture2D(4, 4);
            created.Add(texture);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
            created.Add(sprite);
            ItemSlotView view = CreateSlot();

            view.Show(CreateItem(sprite), "Bronze Hewer", Color.green, Color.black);
            Assert.IsTrue(view.Icon.enabled);
            Assert.AreEqual(sprite, view.Icon.sprite);
            Assert.AreEqual("", view.Label.text);
            Assert.AreEqual(Color.green, view.Border.effectColor);

            view.Show(CreateItem(null), "Bronze Hewer", Color.green, Color.black);
            Assert.IsFalse(view.Icon.enabled);
            Assert.AreEqual("Bronze Hewer", view.Label.text);

            view.Show(null, "", Color.white, Color.black);
            Assert.IsFalse(view.Icon.enabled);
            Assert.AreEqual("", view.Label.text);
        }
    }
}
