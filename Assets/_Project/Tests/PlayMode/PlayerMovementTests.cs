using System.Collections;
using NUnit.Framework;
using RPG.Controls;
using RPG.Player;
using UnityEngine;
using UnityEngine.TestTools;

namespace RPG.Tests
{
    public class PlayerMovementTests
    {
        private GameObject cameraObject;
        private GameObject player;
        private PlayerMovement movement;

        [SetUp]
        public void SetUp()
        {
            // PlayerMovement uses Camera.main for clicks, so a main camera must exist before it wakes up.
            cameraObject = new GameObject("Test Camera") { tag = "MainCamera" };
            cameraObject.AddComponent<Camera>().orthographic = true;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            player = new GameObject("Test Player");
            var body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            player.AddComponent<CircleCollider2D>().radius = 0.12f;
            player.AddComponent<PlayerInputReader>();
            movement = player.AddComponent<PlayerMovement>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(player);
            Object.Destroy(cameraObject);
        }

        [UnityTest]
        public IEnumerator SetDestination_MovesPlayerToTarget()
        {
            var target = new Vector2(1.5f, 0.75f);
            movement.SetDestination(target);

            yield return WaitUntilOrTimeout(() => !movement.Destination.HasValue, 3f);

            Assert.IsFalse(movement.Destination.HasValue, "Destination should be cleared on arrival.");
            Assert.Less(Vector2.Distance(player.transform.position, target), 0.1f);
            Assert.IsFalse(movement.IsMoving);
        }

        [UnityTest]
        public IEnumerator SetDestination_UpdatesFacingDirection()
        {
            movement.SetDestination(new Vector2(-2f, 0f));

            yield return new WaitForSeconds(0.2f);

            Assert.Less(movement.FacingDirection.x, -0.9f);
        }

        [UnityTest]
        public IEnumerator Obstacle_BlocksPlayer_AndCancelsDestination()
        {
            var wall = new GameObject("Test Wall");
            wall.transform.position = new Vector3(1f, 0f, 0f);
            wall.AddComponent<BoxCollider2D>().size = new Vector2(0.2f, 3f);

            movement.SetDestination(new Vector2(3f, 0f));

            yield return WaitUntilOrTimeout(() => !movement.Destination.HasValue, 3f);

            Assert.IsFalse(movement.Destination.HasValue, "Click-to-move should give up when blocked.");
            Assert.Less(player.transform.position.x, 1f, "Player must not pass through the wall.");
            Object.Destroy(wall);
        }

        [UnityTest]
        public IEnumerator SetDestination_SurvivesFrameHitches()
        {
            // 0.3 s frames: about 15 physics steps run before each Update,
            // which used to look like being blocked and cancelled the click.
            Time.captureDeltaTime = 0.3f;
            try
            {
                Vector2 start = player.transform.position;
                movement.SetDestination(start + new Vector2(-4f, 0f));

                for (int i = 0; i < 3; i++)
                    yield return null;

                Assert.Less(player.transform.position.x, start.x - 1f, "Player should be walking despite slow frames.");
                Assert.Less(movement.FacingDirection.x, -0.9f, "Facing should follow the walk even without an Update in between.");
            }
            finally
            {
                Time.captureDeltaTime = 0f;
            }
        }

        [UnityTest]
        public IEnumerator Stop_ClearsDestination()
        {
            movement.SetDestination(new Vector2(5f, 0f));
            yield return new WaitForSeconds(0.1f);

            movement.Stop();
            Vector3 stoppedAt = player.transform.position;
            yield return new WaitForSeconds(0.2f);

            Assert.IsFalse(movement.Destination.HasValue);
            Assert.Less(Vector3.Distance(player.transform.position, stoppedAt), 0.01f);
        }

        private static IEnumerator WaitUntilOrTimeout(System.Func<bool> condition, float timeout)
        {
            float end = Time.time + timeout;
            while (!condition() && Time.time < end)
                yield return null;
        }
    }
}
