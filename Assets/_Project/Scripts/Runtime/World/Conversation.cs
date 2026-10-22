using System;
using System.Collections;
using System.Collections.Generic;
using RPG.Combat;
using RPG.UI;
using Unity.Netcode;
using UnityEngine;

namespace RPG.World
{
    /// <summary>
    /// On the player: runs the scripts of the gossip replies it picks (<see cref="GossipCatalog"/>). The server checks
    /// that the player stands at the townsperson, that the reply is one of its menus' and that its conditions are met,
    /// then casts the script's spells on the player (Breda's beer), gives talk credit (for quests) and has the NPC speak.
    /// Pointing the way to an NPC is done by the window itself.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class Conversation : NetworkBehaviour
    {
        private const float RangeTolerance = 0.8f;
        private static readonly Color AuraColor = new Color(0.85f, 0.7f, 0.35f);
        private static readonly Color SayColor = new Color(1f, 0.95f, 0.75f);

        /// <summary>Server: a reply gave talk credit for an NPC (its source entry): quests listen to it.</summary>
        public static event Action<Conversation, int> ServerTalkCredit;

        private Health health;
        private readonly List<GossipScriptStep> steps = new List<GossipScriptStep>();

        private void Awake()
        {
            health = GetComponent<Health>();
        }

        /// <summary>Owner: asks the server to run the script of a picked reply.</summary>
        public void RequestChoose(Townsperson person, GossipOption option)
        {
            if (IsOwner && person != null && option != null && option.script > 0) ChooseRpc(person.Index, option.entry);
        }

        [Rpc(SendTo.Server)]
        private void ChooseRpc(int personIndex, int optionEntry, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId == OwnerClientId) ServerChoose(Townsperson.Get(personIndex), optionEntry);
        }

        /// <summary>Server: runs a reply's script if the player may pick it here. False when refused.</summary>
        public bool ServerChoose(Townsperson person, int optionEntry)
        {
            if (!IsServer || health.IsDead || person == null || !person.CanTalk) return false;
            if (Vector2.Distance(AutoAttack.ServerPosition(health), person.transform.position) > person.TalkRange + RangeTolerance)
                return false;
            GossipCatalog catalog = person.Gossip;
            GossipOption option = catalog.Option(optionEntry);
            if (option == null || !catalog.Reaches(person.GossipMenu, option) || !catalog.Usable(option)
                || !GossipCondition.AllMet(option.conditions, gameObject))
                return false;

            catalog.StepsOf(option.script, steps);
            StartCoroutine(Run(person, steps.ToArray()));
            return true;
        }

        private IEnumerator Run(Townsperson person, GossipScriptStep[] script)
        {
            float started = Time.time;
            foreach (GossipScriptStep step in script)
            {
                float wait = started + step.delay / 1000f - Time.time;
                if (wait > 0f) yield return new WaitForSeconds(wait);
                if (health.IsDead) yield break;
                switch (step.command)
                {
                    case GossipScriptStep.CastSpell:
                        var effects = GetComponent<StatusEffects>();
                        if (effects != null && step.seconds > 0f)
                            effects.ServerApply(step.name, AuraColor, StatusKind.Flavor, 0f, step.seconds);
                        break;
                    case GossipScriptStep.TalkCredit:
                        ServerTalkCredit?.Invoke(this, step.data);
                        break;
                    case GossipScriptStep.Say:
                        if (person != null) SayRpc(person.Index, step.name);
                        break;
                }
            }
        }

        [Rpc(SendTo.Owner)]
        private void SayRpc(int personIndex, string text)
        {
            Townsperson person = Townsperson.Get(personIndex);
            if (person == null) return;
            string line = GossipCatalog.Format(text, gameObject, person.DisplayName);
            CombatTextLayer.ShowText(person.transform.position + Vector3.up * 1.1f, line, SayColor, 0.9f);
            MessageLog.Add($"{person.DisplayName} says: {line}", SayColor);
        }
    }
}
