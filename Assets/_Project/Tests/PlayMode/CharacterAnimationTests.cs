using System.Collections;
using NUnit.Framework;
using RPG.Characters;
using UnityEngine;
using UnityEngine.TestTools;

namespace RPG.Tests
{
    public class CharacterAnimationTests
    {
        /// <summary>Lets a test drive a CharacterAnimator without real movement.</summary>
        private class FakeMotion : MonoBehaviour, ICharacterMotionSource
        {
            public Vector2 FacingDirection { get; set; } = Vector2.down;
            public bool IsMoving { get; set; }
        }

        private GameObject character;
        private FakeMotion motion;
        private CharacterAnimator animator;
        private SpriteRenderer spriteRenderer;
        private CharacterAnimationSet set;
        private Texture2D texture;
        private Sprite idleSouth, idleEast, walkEast0, walkEast1, attackSouth;

        [SetUp]
        public void SetUp()
        {
            texture = new Texture2D(4, 4);
            idleSouth = MakeSprite("IdleSouth");
            idleEast = MakeSprite("IdleEast");
            walkEast0 = MakeSprite("WalkEast0");
            walkEast1 = MakeSprite("WalkEast1");
            attackSouth = MakeSprite("AttackSouth");

            set = ScriptableObject.CreateInstance<CharacterAnimationSet>();
            set.MirrorWestFromEast = true;
            set.Clips = new[]
            {
                new DirectionalClip { id = CharacterAnimationSet.IdleClip, framesPerSecond = 1f, south = new[] { idleSouth }, east = new[] { idleEast } },
                new DirectionalClip { id = CharacterAnimationSet.WalkClip, framesPerSecond = 1000f, east = new[] { walkEast0, walkEast1 } },
                new DirectionalClip { id = "Attack", framesPerSecond = 10f, loop = false, south = new[] { attackSouth, attackSouth } },
            };

            character = new GameObject("Test Character");
            spriteRenderer = character.AddComponent<SpriteRenderer>();
            motion = character.AddComponent<FakeMotion>();
            animator = character.AddComponent<CharacterAnimator>();
            animator.AnimationSet = set;
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(character);
            Object.Destroy(set);
            Object.Destroy(texture);
        }

        [TestCase(1f, 0f, Direction8.East)]
        [TestCase(1f, 1f, Direction8.NorthEast)]
        [TestCase(0f, 1f, Direction8.North)]
        [TestCase(-1f, 1f, Direction8.NorthWest)]
        [TestCase(-1f, 0f, Direction8.West)]
        [TestCase(-1f, -1f, Direction8.SouthWest)]
        [TestCase(0f, -1f, Direction8.South)]
        [TestCase(1f, -1f, Direction8.SouthEast)]
        [TestCase(0.9f, 0.2f, Direction8.East)]
        [TestCase(0f, 0f, Direction8.South)]
        public void Direction8_FromVector_SnapsToNearest(float x, float y, Direction8 expected)
        {
            Assert.AreEqual(expected, Direction8Util.FromVector(new Vector2(x, y)));
        }

        [UnityTest]
        public IEnumerator Idle_FacingSouth_ShowsIdleSouth()
        {
            yield return null;

            Assert.AreEqual(CharacterAnimationSet.IdleClip, animator.CurrentClip);
            Assert.AreSame(idleSouth, spriteRenderer.sprite);
            Assert.IsFalse(spriteRenderer.flipX);
        }

        [UnityTest]
        public IEnumerator Moving_PlaysWalkClip()
        {
            motion.FacingDirection = Vector2.right;
            motion.IsMoving = true;
            yield return null;
            yield return null;

            Assert.AreEqual(CharacterAnimationSet.WalkClip, animator.CurrentClip);
            Assert.That(spriteRenderer.sprite == walkEast0 || spriteRenderer.sprite == walkEast1);
        }

        [UnityTest]
        public IEnumerator WestWithoutFrames_MirrorsEast()
        {
            motion.FacingDirection = Vector2.left;
            yield return null;

            Assert.AreEqual(Direction8.West, animator.Direction);
            Assert.AreSame(idleEast, spriteRenderer.sprite);
            Assert.IsTrue(spriteRenderer.flipX);
        }

        [UnityTest]
        public IEnumerator MissingDirection_FallsBackToSouth()
        {
            motion.FacingDirection = Vector2.up;
            yield return null;

            Assert.AreSame(idleSouth, spriteRenderer.sprite);
        }

        [UnityTest]
        public IEnumerator OneShot_ReturnsToIdleWhenDone()
        {
            animator.PlayOneShot("Attack");
            yield return null;
            Assert.AreEqual("Attack", animator.CurrentClip);
            Assert.AreSame(attackSouth, spriteRenderer.sprite);

            yield return new WaitForSeconds(0.4f);

            Assert.AreEqual(CharacterAnimationSet.IdleClip, animator.CurrentClip);
        }

        private Sprite MakeSprite(string name)
        {
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0f));
            sprite.name = name;
            return sprite;
        }
    }
}
