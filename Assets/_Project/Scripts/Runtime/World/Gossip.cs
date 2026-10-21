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
    /// What a player's quest log knows, for the gossip conditions that ask about quests. Put on the player by the quest
    /// log; without one every quest counts as never taken.
    /// </summary>
    public interface IQuestFacts
    {
        /// <summary>The quest is in the log (taken, not turned in).</summary>
        bool HasQuest(int quest);

        /// <summary>The quest is in the log with every objective done.</summary>
        bool HasFinishedQuest(int quest);

        /// <summary>The quest was turned in.</summary>
        bool TurnedInQuest(int quest);
    }

    /// <summary>One of the source game's gossip conditions: a fact about the player that must be true (or false).</summary>
    [Serializable]
    public struct GossipCondition
    {
        public const int HasQuest = 1;
        public const int TurnedInQuest = 2;
        public const int FinishedQuest = 3;
        public const int HasAura = 7;
        public const int HasOrDidQuest = 12;
        public const int LevelAbove = 15;

        public int type;
        public int value;
        [Tooltip("Whether the fact must be true (else it must be false).")]
        public bool mustBe;
        [Tooltip("The aura's name (Has Aura).")]
        public string name;

        public GossipCondition(int type, int value, bool mustBe, string name = "")
        {
            this.type = type;
            this.value = value;
            this.mustBe = mustBe;
            this.name = name;
        }

        /// <summary>Whether the player meets it (unknown kinds are met).</summary>
        public bool Met(GameObject player)
        {
            if (player == null) return !mustBe;
            var quests = player.GetComponent<IQuestFacts>();
            bool fact;
            switch (type)
            {
                case HasQuest: fact = quests != null && quests.HasQuest(value); break;
                case TurnedInQuest: fact = quests != null && quests.TurnedInQuest(value); break;
                case FinishedQuest: fact = quests != null && quests.HasFinishedQuest(value); break;
                case HasOrDidQuest: fact = quests != null && (quests.HasQuest(value) || quests.TurnedInQuest(value)); break;
                case HasAura:
                    var effects = player.GetComponent<StatusEffects>();
                    fact = false;
                    if (effects != null)
                        foreach (StatusView view in effects.Views)
                            if (view.Name.ToString() == name) fact = true;
                    break;
                case LevelAbove:
                    var progress = player.GetComponent<CharacterProgress>();
                    fact = progress != null && progress.Level > value;
                    break;
                default: return true;
            }
            return fact == mustBe;
        }

        public static bool AllMet(GossipCondition[] conditions, GameObject player)
        {
            if (conditions == null) return true;
            foreach (GossipCondition condition in conditions)
                if (!condition.Met(player)) return false;
            return true;
        }
    }

    /// <summary>One thing an NPC may say when its gossip menu opens (the first whose conditions are met is said).</summary>
    [Serializable]
    public class GossipText
    {
        public int entry;
        public int menu;
        [TextArea] public string text;
        public GossipCondition[] conditions;

        public GossipText()
        {
        }

        public GossipText(int entry, int menu, string text, params GossipCondition[] conditions)
        {
            this.entry = entry;
            this.menu = menu;
            this.text = text;
            this.conditions = conditions;
        }
    }

    /// <summary>A reply the player can pick in a gossip menu: it opens another menu and/or runs a script.</summary>
    [Serializable]
    public class GossipOption
    {
        /// <summary><see cref="nextMenu"/>: closes the window.</summary>
        public const int Close = -1;
        /// <summary><see cref="nextMenu"/>: opens the NPC's transmute window.</summary>
        public const int Transmute = -2;

        public int entry;
        public int menu;
        public string text;
        [Tooltip("The menu it opens (0 or Close: none).")]
        public int nextMenu;
        [Tooltip("The script it runs (0: none).")]
        public int script;
        public GossipCondition[] conditions;

        public GossipOption()
        {
        }

        public GossipOption(int entry, int menu, string text, int nextMenu, int script, params GossipCondition[] conditions)
        {
            this.entry = entry;
            this.menu = menu;
            this.text = text;
            this.nextMenu = nextMenu;
            this.script = script;
            this.conditions = conditions;
        }
    }

    /// <summary>One step of a source game script (the scripts table).</summary>
    [Serializable]
    public class GossipScriptStep
    {
        public const int CastSpell = 1;
        public const int TalkCredit = 2;
        public const int Say = 3;
        public const int LocateNpc = 10;
        public const int OpenBank = 11;
        public const int Respec = 12;
        public const int QueueArena = 13;

        public int script;
        [Tooltip("Milliseconds after the script starts.")]
        public int delay;
        public int command;
        [Tooltip("The spell, NPC or world text the command names.")]
        public int data;
        [Tooltip("Its name (spell, NPC) or text (world text).")]
        public string name;
        [Tooltip("A spell's aura duration in seconds.")]
        public float seconds;

        public GossipScriptStep()
        {
        }

        public GossipScriptStep(int script, int delay, int command, int data, string name, float seconds)
        {
            this.script = script;
            this.delay = delay;
            this.command = command;
            this.data = data;
            this.name = name;
            this.seconds = seconds;
        }

        /// <summary>Whether this project does the command (no bank, respec or arena yet).</summary>
        public bool Supported => command == CastSpell || command == TalkCredit || command == Say || command == LocateNpc;
    }

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
