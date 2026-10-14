using System.Collections;
using NUnit.Framework;
using RPG.CameraSystem;
using UnityEngine;
using UnityEngine.TestTools;

namespace RPG.Tests
{
    public class CameraFollowTests
    {
        private GameObject cameraObject;
        private GameObject target;
        private CameraFollow follow;

        [SetUp]
        public void SetUp()
        {
            target = new GameObject("Test Target");

            cameraObject = new GameObject("Test Camera");
            cameraObject.AddComponent<Camera>().orthographic = true;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            follow = cameraObject.AddComponent<CameraFollow>();
            follow.Follow(target.transform, snap: true);
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(cameraObject);
            Object.Destroy(target);
        }

        [UnityTest]
        public IEnumerator Camera_CatchesUpWithMovedTarget_AndKeepsDepth()
        {
            Vector3 startOffset = cameraObject.transform.position - target.transform.position;

            target.transform.position = new Vector3(4f, -2f, 0f);
            yield return new WaitForSeconds(1f);

            Vector3 offset = cameraObject.transform.position - target.transform.position;
            Assert.Less(Vector2.Distance(offset, startOffset), 0.05f, "Camera should settle at the same offset.");
            Assert.AreEqual(-10f, cameraObject.transform.position.z, 0.001f, "Camera depth must not change.");
        }

        [UnityTest]
        public IEnumerator Follow_WithSnap_JumpsImmediately()
        {
            var other = new GameObject("Other Target");
            other.transform.position = new Vector3(-6f, 3f, 0f);

            follow.Follow(other.transform, snap: true);
            yield return null;

            Assert.Less(Vector2.Distance(cameraObject.transform.position, other.transform.position), 0.5f);
            Object.Destroy(other);
        }
    }
}
