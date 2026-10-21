using System;
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
}
