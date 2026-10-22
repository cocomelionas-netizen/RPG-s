using System.Collections.Generic;
using RPG.Items;
using RPG.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>
    /// An item's tooltip laid out like the source game's: the name in its quality's colour; its quality on the left and
    /// slot or weapon on the right (red when the player's class cannot use it); its own weapon or armor value; the affix's
    /// stats; "Magical gem sockets" with each socket's gem or "Empty Slot", two to a row; what using it does; empowerment;
    /// "Requires level N" (red when too low); "Sell Price: Ng"; and a hint of what a click does. A dark, thinly framed box
    /// as tall as its lines, drawn over every window. Used by the bag, the character sheet and the loot window.
    /// Gear from the bag or the loot can be compared with what is worn: the worn piece (both rings) shows beside it under
    /// "Equipped", and the tooltip lists what would change if it were worn instead (green better, red worse).
    /// </summary>
    public class ItemTooltip
    {
        private const float Width = 300f;
        private const float Padding = 12f;
        private const int FontSize = 15;
        private const int TitleSize = 18;
        private const float SocketIcon = 18f;
        private const float Gap = 6f;

        private static readonly Color Background = new Color(0.07f, 0.09f, 0.12f, 0.94f);
        private static readonly Color Edge = new Color(0.36f, 0.42f, 0.5f, 0.9f);
        private static readonly Color Plain = new Color(0.95f, 0.95f, 0.93f);
        private static readonly Color Red = new Color(1f, 0.3f, 0.25f);
        private static readonly Color Gold = new Color(1f, 0.82f, 0.25f);
        private static readonly Color Grey = new Color(0.62f, 0.64f, 0.66f);
        private static readonly Color UseColor = new Color(0.55f, 0.95f, 0.55f);
        private static readonly Color Empower = new Color(0.78f, 0.63f, 1f);
        private static readonly Color Better = new Color(0.4f, 0.95f, 0.4f);

        private readonly RectTransform panel;
        private readonly Transform parent;
        private readonly bool comparison;
        private ItemTooltip[] compared;
        private int comparedShown;
        private readonly List<GameObject> rows = new List<GameObject>();
        private float height;

        /// <summary>One line: a left text, an optional right text, an optional small icon before each.</summary>
        private struct Line
        {
            public string Left;
            public Color LeftColor;
            public string Right;
            public Color RightColor;
            public int Size;
            public Sprite LeftIcon;
            public Sprite RightIcon;
            public bool Icons;
        }

        public ItemTooltip(Transform parent) : this(parent, false)
        {
        }

        private ItemTooltip(Transform parent, bool comparison)
        {
            this.parent = parent;
            this.comparison = comparison;
            Image box = UIFactory.CreateImage("Item Tooltip", parent, Background);
            box.raycastTarget = false;
            box.gameObject.AddComponent<Outline>().effectColor = Edge;
            UIFactory.MakeTopmost(box.gameObject);
            panel = box.rectTransform;
            panel.anchorMin = panel.anchorMax = Vector2.zero;
            panel.pivot = new Vector2(0f, 1f);
            box.gameObject.SetActive(false);
        }

        public bool Visible => panel.gameObject.activeSelf;

        public void Hide()
        {
            if (panel.gameObject.activeSelf) panel.gameObject.SetActive(false);
            HideCompared(0);
        }

        /// <summary>
        /// Shows an item of the player's (or one they may take or buy) with a hint of what a click does; with
        /// <paramref name="compare"/>, gear also shows the worn piece beside it and what wearing this one would change.
        /// </summary>
        public void Show(Inventory inventory, ItemStack stack, string hint, bool compare = false)
        {
            ItemDefinition item = inventory != null ? inventory.Definition(stack) : null;
            if (item == null)
            {
                Hide();
                return;
            }
            List<Line> lines = Lines(inventory, stack, item);
            if (comparison) lines.Insert(0, new Line { Left = "Equipped", LeftColor = Grey, Size = 13 });
            int shownWorn = 0;
            if (compare && item.IsEquippable)
            {
                ItemStack first = ItemStack.Empty;
                foreach (int index in WornIndexes(item.slot))
                {
                    ItemStack worn = inventory.GetEquippedAt(index);
                    if (worn.IsEmpty) continue;
                    if (first.IsEmpty) first = worn;
                    if (compared == null) compared = new[] { new ItemTooltip(parent, true), new ItemTooltip(parent, true) };
                    compared[shownWorn++].Show(inventory, worn, null);
                }
                lines.AddRange(Differences(inventory, stack, first));
            }
            HideCompared(shownWorn);
            comparedShown = shownWorn;
            if (!string.IsNullOrEmpty(hint)) lines.Add(new Line { Left = hint, LeftColor = Grey, Size = 13 });
            Build(lines);
            panel.gameObject.SetActive(true);
        }

        private void HideCompared(int from)
        {
            if (compared == null) return;
            for (int i = from; i < compared.Length; i++) compared[i].Hide();
        }

        /// <summary>The equipment indexes gear of a slot would go to (both rings for a ring).</summary>
        private static IEnumerable<int> WornIndexes(ItemSlot slot)
        {
            int index = Inventory.IndexOf(slot);
            if (index < 0) yield break;
            yield return index;
            if (slot == ItemSlot.Ring && Inventory.SlotAt(index + 1) == ItemSlot.Ring) yield return index + 1;
        }

        /// <summary>What wearing <paramref name="stack"/> instead of <paramref name="worn"/> would change.</summary>
        private static IEnumerable<Line> Differences(Inventory inventory, ItemStack stack, ItemStack worn)
        {
            Inventory.GearBonus mine = inventory.BonusOf(stack);
            Inventory.GearBonus theirs = inventory.BonusOf(worn);
            var changes = new List<Line>();
            void Change(int difference, string name)
            {
                if (difference == 0) return;
                changes.Add(Text(difference > 0 ? $"+{difference} {name}" : $"-{-difference} {name}", difference > 0 ? Better : Red));
            }
            Change(mine.Damage - theirs.Damage, "Weapon Value");
            Change(mine.Armor - theirs.Armor, "Armor Value");
            Change(mine.Health - theirs.Health, "Health");
            Change(mine.Mana - theirs.Mana, "Mana");
            for (int i = 0; i < AttributeSet.Count; i++)
            {
                var attribute = (RPG.Progression.Attribute)i;
                Change(mine.Attributes[attribute] - theirs.Attributes[attribute], ItemDefinition.AttributeName(attribute));
            }
            yield return Text(worn.IsEmpty ? "If worn (the slot is empty):" : "If worn instead:", Gold);
            if (changes.Count == 0) yield return Text("No change", Grey);
            foreach (Line change in changes) yield return change;
        }

        /// <summary>Puts the tooltip by the pointer (canvas units, kept on the screen); <paramref name="pivot"/> picks the side.</summary>
        public void Follow(Canvas canvas, Vector2 pivot, Vector2 offset)
        {
            if (!Visible) return;
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            Vector2 pointer = UnityEngine.InputSystem.Mouse.current != null ? UnityEngine.InputSystem.Mouse.current.position.ReadValue() : Vector2.zero;
            panel.pivot = pivot;
            panel.anchoredPosition = UIFactory.KeepOnScreen(panel, pointer / scale + offset, scale);
            if (comparedShown == 0 || compared == null) return;

            // The worn pieces beside it, on the side away from the pointer (the other side when that is off the screen).
            Vector2 size = panel.sizeDelta;
            Vector2 min = panel.anchoredPosition - Vector2.Scale(panel.pivot, size);
            float top = min.y + size.y;
            float screenWidth = Screen.width / Mathf.Max(0.0001f, scale);
            float wornWidth = comparedShown * (Width + Gap);
            bool left = pivot.x >= 0.5f ? min.x - wornWidth >= 0f : min.x + size.x + wornWidth > screenWidth;
            float x = left ? min.x - Gap : min.x + size.x + Gap;
            for (int i = 0; i < comparedShown; i++)
            {
                compared[i].PlaceAt(new Vector2(x, top), left, scale);
                x += left ? -(Width + Gap) : Width + Gap;
            }
        }

        private void PlaceAt(Vector2 topCorner, bool leftOfIt, float scale)
        {
            panel.pivot = new Vector2(leftOfIt ? 1f : 0f, 1f);
            panel.anchoredPosition = UIFactory.KeepOnScreen(panel, topCorner, scale);
        }

        private static List<Line> Lines(Inventory inventory, ItemStack stack, ItemDefinition item)
        {
            var lines = new List<Line>();
            var progress = inventory.GetComponent<CharacterProgress>();
            AffixDefinition affix = inventory.Affix(stack);
            ItemRarity quality = inventory.Quality(stack);
            Color qualityColor = ItemDefinition.RarityColor(quality);
            bool classOk = progress == null || item.CanBeUsedByClass(progress.ClassIndex);
            int level = progress != null ? progress.Level : int.MaxValue;

            lines.Add(new Line { Left = inventory.DisplayName(stack), LeftColor = qualityColor, Size = TitleSize });
            if (item.IsEquippable)
            {
                Color kindColor = classOk ? Plain : Red;
                lines.Add(new Line { Left = quality.ToString(), LeftColor = kindColor, Right = KindOf(item), RightColor = kindColor });
                if (item.damageBonus > 0) lines.Add(Text($"{item.damageBonus} Weapon Value", Plain));
                if (item.armor > 0) lines.Add(Text($"{item.armor} Armor Value", Plain));

                int damage = affix != null ? affix.damageBonus : 0;
                int armor = affix != null ? affix.armor : 0;
                int health = item.healthBonus + (affix != null ? affix.healthBonus : 0);
                int mana = item.manaBonus + (affix != null ? affix.manaBonus : 0);
                AttributeSet attributes = item.attributes + (affix != null ? affix.attributes : default);
                if (damage > 0) lines.Add(Text($"+{damage} Weapon Value", Plain));
                if (armor > 0) lines.Add(Text($"+{armor} Armor Value", Plain));
                if (health > 0) lines.Add(Text($"+{health} Health", Plain));
                if (mana > 0) lines.Add(Text($"+{mana} Mana", Plain));
                if (!attributes.IsZero)
                    foreach (string stat in attributes.Describe("\n").Split('\n'))
                        lines.Add(Text(stat, Plain));
            }
            else
            {
                lines.Add(Text(quality.ToString(), Plain));
            }

            if (stack.HasSocket)
            {
                lines.Add(Text("Magical gem sockets", Gold));
                var sockets = new List<(string name, Sprite icon, Color color)>();
                for (int i = 0; i < ItemStack.MaxSockets; i++)
                {
                    if (stack.SocketAt(i) == 0) continue;
                    ItemDefinition gem = inventory.GemAt(stack, i);
                    sockets.Add(gem == null
                        ? ("Empty Slot", UIFactory.UiArt("SlotFrame"), Grey)
                        : ($"{gem.displayName} ({gem.attributes.Describe(", ")})", gem.icon, ItemDefinition.RarityColor(gem.rarity)));
                }
                // Gems' names are long: one to a row; empty slots two to a row.
                for (int i = 0; i < sockets.Count; i++)
                {
                    var line = new Line { Left = sockets[i].name, LeftColor = sockets[i].color, LeftIcon = sockets[i].icon, Icons = true };
                    if (sockets[i].name == "Empty Slot" && i + 1 < sockets.Count && sockets[i + 1].name == "Empty Slot")
                    {
                        line.Right = sockets[i + 1].name;
                        line.RightColor = sockets[i + 1].color;
                        line.RightIcon = sockets[i + 1].icon;
                        i++;
                    }
                    lines.Add(line);
                }
            }

            foreach (string use in item.UseText().Split('\n'))
            {
                string trimmed = use.Trim();
                if (trimmed.Length == 0) continue;
                bool requirement = trimmed.StartsWith("Requires level");
                Color color = trimmed.StartsWith("Use:") ? UseColor
                    : requirement ? (item.itemLevel > level ? Red : Plain)
                    : Grey;
                lines.Add(Text(trimmed, color));
            }

            if (item.IsEquippable && (stack.EmpowerSteps > 0 || stack.EmpowerSpent > 0))
            {
                int points = ItemDefinition.EmpowerPoints(quality);
                lines.Add(Text($"Empowered +{stack.EmpowerSteps} (item level {item.itemLevel + stack.EmpowerSteps})", Empower));
                lines.Add(Text($"{Mathf.Max(0, points - stack.EmpowerSpent)} of {points} empowerment points left", Empower));
            }

            string className = VendorWindow.ClassName(item, progress);
            if (!string.IsNullOrEmpty(className)) lines.Add(Text($"{className} only", classOk ? Grey : Red));
            if (item.IsEquippable) lines.Add(Text($"Requires level {item.itemLevel}", item.itemLevel > level ? Red : Plain));
            if (item.price > 0) lines.Add(Text($"Sell Price: {inventory.SellPrice(stack)}g", Plain));
            return lines;
        }

        private static Line Text(string text, Color color) => new Line { Left = text, LeftColor = color };

        /// <summary>The slot or weapon the source game names on the right: "Legs", "Wand", "Necklace"...</summary>
        private static string KindOf(ItemDefinition item)
        {
            if (item.weaponType != WeaponType.None) return item.weaponType.ToString();
            switch (item.slot)
            {
                case ItemSlot.Neck: return "Necklace";
                case ItemSlot.Waist: return "Belt";
                case ItemSlot.Shield: return "Offhand";
                default: return item.slot.ToString();
            }
        }

        private void Build(List<Line> lines)
        {
            foreach (GameObject row in rows) Object.Destroy(row);
            rows.Clear();
            float y = Padding;
            float inner = Width - Padding * 2f;
            foreach (Line line in lines)
            {
                RectTransform row = UIFactory.CreateRect("Line", panel);
                row.anchorMin = row.anchorMax = row.pivot = new Vector2(0f, 1f);
                float rowHeight = 0f;
                bool split = !string.IsNullOrEmpty(line.Right);
                float columnWidth = split ? inner / 2f : inner;

                rowHeight = Mathf.Max(rowHeight, Cell(row, line.Left, line.LeftColor, line.Size, line.LeftIcon, line.Icons, 0f, columnWidth,
                    split && !line.Icons ? TextAnchor.UpperLeft : TextAnchor.UpperLeft));
                if (split)
                    rowHeight = Mathf.Max(rowHeight, Cell(row, line.Right, line.RightColor, line.Size, line.RightIcon, line.Icons, columnWidth,
                        columnWidth, line.Icons ? TextAnchor.UpperLeft : TextAnchor.UpperRight));
                row.anchoredPosition = new Vector2(Padding, -y);
                row.sizeDelta = new Vector2(inner, rowHeight);
                y += rowHeight + (line.Size == TitleSize ? 4f : 1f);
                rows.Add(row.gameObject);
            }
            height = y + Padding - 1f;
            panel.sizeDelta = new Vector2(Width, height);
        }

        /// <summary>One text (with a small icon before it) at <paramref name="x"/>; returns its height.</summary>
        private static float Cell(RectTransform row, string content, Color color, int size, Sprite icon, bool iconSpace, float x, float width,
            TextAnchor anchor)
        {
            float textX = x;
            if (iconSpace)
            {
                Image picture = UIFactory.CreateImage("Icon", row, Color.white);
                picture.raycastTarget = false;
                picture.preserveAspect = true;
                picture.sprite = icon;
                picture.enabled = icon != null;
                UIFactory.Place(picture.rectTransform, new Vector2(0f, 1f), new Vector2(x, 0f), new Vector2(SocketIcon, SocketIcon));
                textX += SocketIcon + 6f;
            }
            Text text = UIFactory.CreateText("Text", row, size > 0 ? size : FontSize, anchor);
            text.supportRichText = false;
            // Crisp like the source game's: no black outline on the dark box.
            text.GetComponent<Outline>().enabled = false;
            text.color = color;
            text.text = content;
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = text.rectTransform.pivot = new Vector2(0f, 1f);
            text.rectTransform.anchoredPosition = new Vector2(textX, 0f);
            text.rectTransform.sizeDelta = new Vector2(width - (textX - x), 40f);
            float textHeight = Mathf.Ceil(text.preferredHeight);
            text.rectTransform.sizeDelta = new Vector2(width - (textX - x), textHeight);
            return Mathf.Max(textHeight, iconSpace ? SocketIcon : 0f);
        }
    }
}
