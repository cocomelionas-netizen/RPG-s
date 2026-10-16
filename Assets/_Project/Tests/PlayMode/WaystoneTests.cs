#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using RPG.Items;
using RPG.Networking;
using RPG.World;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace RPG.Tests
{
    /// <summary>Discovering waystones, travelling between them and named zones, on a real host.</summary>
    public class WaystoneTests
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Characters/Player.prefab";

        private GameObject cameraObject;
        private GameObject networkObject;
        private GameObject nearObject;
        private GameObject farObject;
        private NetworkManager network;
        private NetworkObject player;
        private WaystoneTraveller traveller;
        private Inventory inventory;
        private Waystone near;
        private Waystone far;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            cameraObject = new GameObject("Test Camera") { tag = "MainCamera" };
            cameraObject.AddComponent<Camera>().orthographic = true;

            networkObject = new GameObject("Test NetworkManager");
            network = networkObject.AddComponent<NetworkManager>();
            var transport = networkObject.AddComponent<UnityTransport>();
            network.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                PlayerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath),
                ConnectionApproval = true,
            };
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
            traveller = player.GetComponent<WaystoneTraveller>();
            Assert.IsNotNull(traveller, "Run RPG > Build Test Scene first.");
            inventory = player.GetComponent<Inventory>();

            nearObject = new GameObject("Near Waystone");
            nearObject.transform.position = player.transform.position + new Vector3(0.5f, 0f, 0f);
            near = nearObject.AddComponent<Waystone>();
            near.Configure(30, "Near Stone");

            farObject = new GameObject("Far Waystone");
            farObject.transform.position = player.transform.position + new Vector3(25f, 0f, 0f);
            far = farObject.AddComponent<Waystone>();
            far.Configure(31, "Far Stone");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(nearObject);
            Object.Destroy(farObject);
            yield return ShutdownNetwork();
            Object.Destroy(networkObject);
            Object.Destroy(cameraObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StandingNearAWaystone_DiscoversIt()
        {
            float end = Time.realtimeSinceStartup + 2f;
            while (!traveller.IsDiscovered(near.Id) && Time.realtimeSinceStartup < end)
                yield return null;

            Assert.IsTrue(traveller.IsDiscovered(near.Id));
            Assert.IsFalse(traveller.IsDiscovered(far.Id));
        }

        [UnityTest]
        public IEnumerator Travel_NeedsTheDestinationDiscovered()
        {
            inventory.ServerAddGold(100, false);
            yield return null;
            Assert.IsFalse(traveller.ServerTravel(far.Id));
        }

        [UnityTest]
        public IEnumerator Travel_IsFree_AndMovesThePlayer()
        {
            traveller.ServerDiscover(far.Id);
            inventory.ServerSetGold(0);
            yield return null;

            Assert.AreEqual(0, traveller.TravelCost, "The source game's waypoints are free");
            Assert.IsTrue(traveller.ServerTravel(far.Id));
            yield return null;
            yield return null;

            Assert.AreEqual(0, inventory.Gold);
            Assert.Less(Vector2.Distance(player.transform.position, far.ArrivalPoint), 0.5f);
        }

        [UnityTest]
        public IEnumerator Travel_WithACost_TakesGold_OrFailsWithoutIt()
        {
            traveller.SetTravelCost(10);
            traveller.ServerDiscover(far.Id);
            inventory.ServerSetGold(5);
            yield return null;
            Assert.IsFalse(traveller.ServerTravel(far.Id));

            inventory.ServerSetGold(100);
            yield return null;
            Assert.IsTrue(traveller.ServerTravel(far.Id));
            Assert.AreEqual(90, inventory.Gold);
        }

        [Test]
        public void Zone_SmallestContainingZoneWins()
        {
            var big = new GameObject("Big Zone");
            var small = new GameObject("Small Zone");
            try
            {
                big.transform.position = new Vector3(500f, 500f, 0f);
                small.transform.position = new Vector3(502f, 500f, 0f);
                big.AddComponent<Zone>().Configure("Valley", 10f);
                small.AddComponent<Zone>().Configure("Town", 2f);

                Assert.AreEqual("Town", Zone.NameAt(new Vector2(502.5f, 500f)));
                Assert.AreEqual("Valley", Zone.NameAt(new Vector2(495f, 500f)));
            }
            finally
            {
                Object.DestroyImmediate(big);
                Object.DestroyImmediate(small);
            }
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
