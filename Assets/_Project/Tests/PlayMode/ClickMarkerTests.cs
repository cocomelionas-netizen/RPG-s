using System.Collections;
using NUnit.Framework;
using RPG.Controls;
using RPG.Player;
using RPG.World;
using UnityEngine;
using UnityEngine.TestTools;

namespace RPG.Tests
{
    public class ClickMarkerTests
    {
        private GameObject cameraObject;
        private GameObject player;
        private GameObject markerObject;
        private PlayerMovement movement;
        private ClickMarker marker;

        [SetUp]
        public void SetUp()
        {
            cameraObject = new GameObject("Test Camera") { tag = "MainCamera" };
            cameraObject.AddComponent<Camera>().orthographic = true;

            player = new GameObject("Test Player");
            player.AddComponent<Rigidbody2D>().gravityScale = 0f;
            player.AddComponent<PlayerInputReader>();
            movement = player.AddComponent<PlayerMovement>();

            markerObject = new GameObject("Test Marker");
            markerObject.AddComponent<SpriteRenderer>();
            marker = markerObject.AddComponent<ClickMarker>();
            marker.Bind(movement);
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(markerObject);
            Object.Destroy(player);
            Object.Destroy(cameraObject);
        }

        [UnityTest]
        public IEnumerator Marker_HiddenWithoutDestination()
        {
            yield return null;
            Assert.IsFalse(marker.IsVisible);
        }

        [UnityTest]
        public IEnumerator Marker_ShowsAtDestination_ThenHidesOnArrival()
        {
            var target = new Vector2(0.8f, 0.4f);
            movement.SetDestination(target);
            yield return null;

            Assert.IsTrue(marker.IsVisible);
            Assert.Less(Vector2.Distance(markerObject.transform.position, target), 0.001f);

            float end = Time.time + 3f;
            while (movement.Destination.HasValue && Time.time < end)
                yield return null;
            yield return null;

            Assert.IsFalse(marker.IsVisible);
        }
    }
}
