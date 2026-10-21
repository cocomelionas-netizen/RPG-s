using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPG.World
{
    /// <summary>
    /// A friendly NPC of the source game that is not a vendor (guards, priests, townsfolk), standing where the source game
    /// stands it (the same on every peer). Players click it to talk: its gossip menu opens (<see cref="GossipCatalog"/>).
    /// Told apart over the network by its index in <see cref="All"/>, sorted by name and then place so it is the same
    /// everywhere.
    /// </summary>
    public class Townsperson : MonoBehaviour
    {
        [SerializeField] private int entry;
        [SerializeField] private string displayName = "Townsperson";
        [Tooltip("Shown under the name, for example \"Guard Sergeant\".")]
        [SerializeField] private string title = "";
        [Tooltip("The source game's gossip menu opened by a click (0: it has nothing to say).")]
        [SerializeField] private int gossipMenu;
        [SerializeField] private GossipCatalog gossip;
        [Tooltip("How close a player must be to talk (the server allows a little more).")]
        [SerializeField, Min(0.5f)] private float talkRange = 1.6f;

        private static readonly List<Townsperson> People = new List<Townsperson>();

        /// <summary>The source game's NPC entry (npc_template).</summary>
        public int Entry => entry;
        public string DisplayName => displayName;
        public string Title => title;
        public int GossipMenu => gossipMenu;
        public GossipCatalog Gossip => gossip;
        public float TalkRange => talkRange;

        /// <summary>Whether a click opens a gossip window.</summary>
        public bool CanTalk => gossip != null && gossipMenu > 0;

        /// <summary>Every townsperson in the scene, sorted by name and then place.</summary>
        public static ListView<Townsperson> All => new ListView<Townsperson>(People);

        public int Index => People.IndexOf(this);

        public static Townsperson Get(int index) => index >= 0 && index < People.Count ? People[index] : null;

        /// <summary>Who it is and what it says (used by the scene builder and the tests).</summary>
        public void Configure(int npcEntry, string npcName, string npcTitle, int menu, GossipCatalog catalog)
        {
            entry = npcEntry;
            displayName = npcName;
            title = npcTitle ?? "";
            gossipMenu = menu;
            gossip = catalog;
        }

        /// <summary>The nearest townsperson named <paramref name="npcName"/> to a point; null when there is none.</summary>
        public static Townsperson Find(string npcName, Vector2 near)
        {
            Townsperson best = null;
            float bestDistance = float.MaxValue;
            foreach (Townsperson person in People)
            {
                if (person.displayName != npcName) continue;
                float distance = Vector2.Distance(near, person.transform.position);
                if (distance < bestDistance)
                {
                    best = person;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private void OnEnable()
        {
            People.Add(this);
            People.Sort(Compare);
        }

        private void OnDisable() => People.Remove(this);

        private static int Compare(Townsperson a, Townsperson b)
        {
            int byName = string.Compare(a.displayName, b.displayName, StringComparison.Ordinal);
            if (byName != 0) return byName;
            Vector3 pa = a.transform.position, pb = b.transform.position;
            return pa.x != pb.x ? pa.x.CompareTo(pb.x) : pa.y.CompareTo(pb.y);
        }
    }
}
