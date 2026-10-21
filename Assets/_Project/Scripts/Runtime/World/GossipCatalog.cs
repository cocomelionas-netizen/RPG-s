using System;
using System.Collections.Generic;
using System.Text;
using RPG.Characters;
using RPG.Combat;
using RPG.Progression;
using UnityEngine;

namespace RPG.World
{
    /// <summary>
    /// Every gossip menu of the source game (what NPCs say, the replies and the scripts they run). A menu's text is the
    /// first of its texts whose conditions the player meets; its replies are those whose conditions are met and whose
    /// scripts this project can run.
    /// </summary>
    [CreateAssetMenu(menuName = "RPG/World/Gossip Catalog", fileName = "GossipCatalog")]
    public class GossipCatalog : ScriptableObject
    {
        public GossipText[] texts = Array.Empty<GossipText>();
        public GossipOption[] options = Array.Empty<GossipOption>();
        public GossipScriptStep[] scripts = Array.Empty<GossipScriptStep>();

        private Dictionary<int, HashSet<int>> reachable;

        /// <summary>The text of a menu for the player; null when none is met (or the menu has none).</summary>
        public GossipText TextFor(int menu, GameObject player)
        {
            foreach (GossipText text in texts)
                if (text.menu == menu && GossipCondition.AllMet(text.conditions, player)) return text;
            return null;
        }

        /// <summary>The replies of a menu the player may pick.</summary>
        public void OptionsFor(int menu, GameObject player, List<GossipOption> result)
        {
            result.Clear();
            foreach (GossipOption option in options)
                if (option.menu == menu && Usable(option) && GossipCondition.AllMet(option.conditions, player)) result.Add(option);
        }

        public GossipOption Option(int entry)
        {
            foreach (GossipOption option in options)
                if (option.entry == entry) return option;
            return null;
        }

        /// <summary>The steps of a script, in order.</summary>
        public void StepsOf(int script, List<GossipScriptStep> result)
        {
            result.Clear();
            if (script <= 0) return;
            foreach (GossipScriptStep step in scripts)
                if (step.script == script) result.Add(step);
            result.Sort((a, b) => a.delay.CompareTo(b.delay));
        }

        /// <summary>Whether this project can do what the reply does (its script, or opening a transmute window: not from gossip).</summary>
        public bool Usable(GossipOption option)
        {
            if (option == null || option.nextMenu == GossipOption.Transmute) return false;
            if (option.script <= 0) return true;
            bool any = false;
            foreach (GossipScriptStep step in scripts)
            {
                if (step.script != option.script) continue;
                if (!step.Supported) return false;
                any = true;
            }
            return any;
        }

        /// <summary>Whether a reply belongs to a menu reached from <paramref name="startMenu"/> (server checks of a pick).</summary>
        public bool Reaches(int startMenu, GossipOption option)
        {
            if (option == null || startMenu <= 0) return false;
            reachable ??= new Dictionary<int, HashSet<int>>();
            if (!reachable.TryGetValue(startMenu, out HashSet<int> menus))
            {
                menus = new HashSet<int> { startMenu };
                var queue = new Queue<int>();
                queue.Enqueue(startMenu);
                while (queue.Count > 0)
                {
                    int menu = queue.Dequeue();
                    foreach (GossipOption next in options)
                        if (next.menu == menu && next.nextMenu > 0 && menus.Add(next.nextMenu)) queue.Enqueue(next.nextMenu);
                }
                reachable[startMenu] = menus;
            }
            return menus.Contains(option.menu);
        }

        /// <summary>
        /// The source game's placeholders filled in: $PLAYER$ and $N (the player's name), $NPC$, $class$ and $CLASS$ (the
        /// player's class), $G=his:her$ (the first for a man, the second for a woman).
        /// </summary>
        public static string Format(string text, string playerName, string npcName, string className, bool female)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var result = new StringBuilder(text.Length + 16);
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '$')
                {
                    if (Matches(text, i, "$PLAYER$")) { result.Append(playerName); i += 7; continue; }
                    if (Matches(text, i, "$NPC$")) { result.Append(npcName); i += 4; continue; }
                    if (Matches(text, i, "$CLASS$")) { result.Append(className); i += 6; continue; }
                    if (Matches(text, i, "$class$")) { result.Append(className.ToLowerInvariant()); i += 6; continue; }
                    if (Matches(text, i, "$G="))
                    {
                        int colon = text.IndexOf(':', i);
                        int end = colon >= 0 ? text.IndexOf('$', colon) : -1;
                        if (end > 0)
                        {
                            result.Append(female ? text.Substring(colon + 1, end - colon - 1) : text.Substring(i + 3, colon - i - 3));
                            i = end;
                            continue;
                        }
                    }
                    if (Matches(text, i, "$N"))
                    {
                        result.Append(playerName);
                        i += 1;
                        continue;
                    }
                }
                result.Append(text[i]);
            }
            return result.ToString();
        }

        /// <summary>The placeholders filled in for a player (its name, class and gender).</summary>
        public static string Format(string text, GameObject player, string npcName)
        {
            var health = player != null ? player.GetComponent<Health>() : null;
            var progress = player != null ? player.GetComponent<CharacterProgress>() : null;
            var hero = player != null ? player.GetComponent<HeroAppearance>() : null;
            return Format(text, health != null ? health.DisplayName : "stranger", npcName,
                progress != null && !string.IsNullOrEmpty(progress.ClassName) ? progress.ClassName : "adventurer",
                hero != null && hero.IsFemale);
        }

        private static bool Matches(string text, int at, string token) => string.CompareOrdinal(text, at, token, 0, token.Length) == 0;
    }
}
