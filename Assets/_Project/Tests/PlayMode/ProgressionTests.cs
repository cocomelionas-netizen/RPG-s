#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using RPG.Combat;
using RPG.Monsters;
using RPG.Networking;
using RPG.Progression;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace RPG.Tests
{
    /// <summary>
    /// Levels, experience, classes and abilities on a real host. Needs the prefabs and class assets
    /// from RPG > Build Test Scene (Warrior: Heavy Strike, Whirlwind, Second Wind; Ranger is class 1).
    /// </summary>
    public class ProgressionTests
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Characters/Player.prefab";
        private const string DummyPrefabPath = "Assets/_Project/Prefabs/World/TrainingDummy.prefab";
        private const string GoblinPrefabPath = "Assets/_Project/Prefabs/Monsters/Goblin.prefab";

        private GameObject cameraObject;
        private GameObject networkObject;
        private NetworkManager network;
        private GameObject dummyPrefab;
        private GameObject goblinPrefab;

        private NetworkObject player;
        private Health playerHealth;
        private CharacterProgress progress;
        private AbilityCaster caster;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            cameraObject = new GameObject("Test Camera") { tag = "MainCamera" };
            cameraObject.AddComponent<Camera>().orthographic = true;

            dummyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DummyPrefabPath);
            goblinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GoblinPrefabPath);
            Assert.IsNotNull(goblinPrefab, "Run RPG > Build Test Scene first.");

            networkObject = new GameObject("Test NetworkManager");
            network = networkObject.AddComponent<NetworkManager>();
            var transport = networkObject.AddComponent<UnityTransport>();
            network.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                PlayerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath),
                ConnectionApproval = true,
            };
            network.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = dummyPrefab });
            network.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = goblinPrefab });
            var bootstrap = networkObject.AddComponent<NetworkBootstrap>();

            yield return null;
            if (network.IsListening)
                yield return ShutdownNetwork();

            Assert.IsTrue(bootstrap.StartHost((ushort)Random.Range(20000, 40000)));
            float end = Time.realtimeSinceStartup + 5f;
            while ((network.LocalClient == null || network.LocalClient.PlayerObject == null) && Time.realtimeSinceStartup < end)
                yield return null;

            player = network.LocalClient.PlayerObject;
            Assert.IsNotNull(player);
            playerHealth = player.GetComponent<Health>();
            progress = player.GetComponent<CharacterProgress>();
            caster = player.GetComponent<AbilityCaster>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return ShutdownNetwork();
            Object.Destroy(networkObject);
            Object.Destroy(cameraObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Player_StartsAsLevelOneWarrior()
        {
            yield return null;
            Assert.AreEqual(1, progress.Level);
            Assert.AreEqual("Warrior", progress.ClassName);
            Assert.AreEqual(progress.Class.MaxHealthAt(1), playerHealth.Max);
            Assert.AreEqual(playerHealth.Max, playerHealth.Current);
            Assert.AreEqual(progress.Class.attackRange, player.GetComponent<AutoAttack>().Range);
        }

        [UnityTest]
        public IEnumerator Experience_LevelUpRaisesMaxHealthAndHeals()
        {
            playerHealth.ApplyDamage(50, false);
            int levelOneMax = playerHealth.Max;

            progress.ServerAddExperience(LevelCurve.ExperienceToNext(1));
            yield return null;

            Assert.AreEqual(2, progress.Level);
            Assert.AreEqual(0, progress.Experience);
            Assert.Greater(playerHealth.Max, levelOneMax);
            Assert.AreEqual(playerHealth.Max, playerHealth.Current, "Levelling up heals fully.");
        }

        [UnityTest]
        public IEnumerator ChangingClass_ChangesStatsAndKeepsLevel()
        {
            progress.ServerAddExperience(LevelCurve.ExperienceToNext(1));
            yield return null;

            Assert.IsTrue(progress.ServerSetClass(1));
            yield return null;

            Assert.AreEqual("Ranger", progress.ClassName);
            Assert.AreEqual(2, progress.Level);
            Assert.AreEqual(progress.Class.MaxHealthAt(2), playerHealth.Max);
            Assert.Greater(player.GetComponent<AutoAttack>().Range, 3f, "Rangers attack from afar.");
        }

        [UnityTest]
        public IEnumerator KillingMonster_GivesExperienceToWhoFoughtIt()
        {
            GameObject goblin = Spawn(goblinPrefab, new Vector2(5f, 0f));
            var ai = goblin.GetComponent<MonsterAI>();
            ai.AggroRadius = 0f;
            yield return null;

            goblin.GetComponent<Health>().ApplyDamage(1000, false, playerHealth);
            yield return null;

            Assert.AreEqual(LevelCurve.KillExperience(ai.ExperienceReward, ai.Level, 1), progress.Experience);
        }

        [UnityTest]
        public IEnumerator Ability_HitsTargetInRange_ThenIsOnCooldown()
        {
            Health dummy = Spawn(dummyPrefab, new Vector2(0.5f, 0f)).GetComponent<Health>();
            yield return null;

            Assert.IsTrue(caster.ServerTryCast(0, dummy, out CastFailure failure), failure.ToString());
            Assert.Less(dummy.Current, dummy.Max);

            int afterFirst = dummy.Current;
            Assert.IsFalse(caster.ServerTryCast(0, dummy, out failure));
            Assert.AreEqual(CastFailure.NotReady, failure);
            Assert.AreEqual(afterFirst, dummy.Current);
            Assert.Greater(caster.CooldownRemaining(0), 1f);
        }

        [UnityTest]
        public IEnumerator Ability_OutOfRangeOrNoTarget_Fails()
        {
            Health dummy = Spawn(dummyPrefab, new Vector2(5f, 0f)).GetComponent<Health>();
            yield return null;

            Assert.IsFalse(caster.ServerTryCast(0, dummy, out CastFailure failure));
            Assert.AreEqual(CastFailure.OutOfRange, failure);
            Assert.IsFalse(caster.ServerTryCast(0, null, out failure));
            Assert.AreEqual(CastFailure.NoTarget, failure);
            Assert.AreEqual(dummy.Max, dummy.Current);
        }

        [UnityTest]
        public IEnumerator Ability_AreaAroundSelf_HitsOnlyNearbyEnemies()
        {
            Health left = Spawn(dummyPrefab, new Vector2(-0.6f, 0f)).GetComponent<Health>();
            Health right = Spawn(dummyPrefab, new Vector2(0.6f, 0f)).GetComponent<Health>();
            Health far = Spawn(dummyPrefab, new Vector2(4f, 0f)).GetComponent<Health>();
            yield return null;

            Assert.IsTrue(caster.ServerTryCast(1, null, out CastFailure failure), failure.ToString());
            Assert.Less(left.Current, left.Max);
            Assert.Less(right.Current, right.Max);
            Assert.AreEqual(far.Max, far.Current);
        }

        [UnityTest]
        public IEnumerator Ability_SelfHeal_RestoresHealth()
        {
            playerHealth.ApplyDamage(60, false);
            int hurt = playerHealth.Current;
            yield return null;

            Assert.IsTrue(caster.ServerTryCast(2, null, out CastFailure failure), failure.ToString());
            Assert.Greater(playerHealth.Current, hurt);
        }

        private GameObject Spawn(GameObject prefab, Vector2 offset)
        {
            Vector3 position = player.transform.position + (Vector3)offset;
            GameObject instance = Object.Instantiate(prefab, position, Quaternion.identity);
            instance.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
            return instance;
        }

        private IEnumerator ShutdownNetwork()
        {
            if (network == null) yield break;
            if (network.IsListening) network.Shutdown();
            float end = Time.realtimeSinceStartup + 5f;
            while ((network.IsListening || network.ShutdownInProgress) && Time.realtimeSinceStartup < end)
                yield return null;
        }
    }
}
#endif
