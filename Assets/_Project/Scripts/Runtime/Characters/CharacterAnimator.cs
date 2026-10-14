using UnityEngine;

namespace RPG.Characters
{
    /// <summary>
    /// Plays 8-direction sprite animations from a <see cref="CharacterAnimationSet"/>.
    /// Picks Idle or Walk from the character's motion and faces its movement direction.
    /// One-shot clips (attacks, casts) can be played on top and return to Idle/Walk when done.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class CharacterAnimator : MonoBehaviour
    {
        [SerializeField] private CharacterAnimationSet animationSet;
        [SerializeField] private string idleClip = CharacterAnimationSet.IdleClip;
        [SerializeField] private string walkClip = CharacterAnimationSet.WalkClip;

        private SpriteRenderer spriteRenderer;
        private ICharacterMotionSource motion;
        private string currentClip;
        private string oneShotClip;
        private float clipTime;

        public Direction8 Direction { get; private set; } = Direction8.South;
        public string CurrentClip => currentClip;

        public CharacterAnimationSet AnimationSet
        {
            get => animationSet;
            set => animationSet = value;
        }

        /// <summary>
        /// What drives the animation. Defaults to the first <see cref="ICharacterMotionSource"/> on this object;
        /// set it when several exist (for example local movement vs. network replication).
        /// </summary>
        public ICharacterMotionSource MotionSource
        {
            get => motion;
            set => motion = value;
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (motion == null) motion = GetComponent<ICharacterMotionSource>();
        }

        /// <summary>Plays a non-looping clip once, then returns to Idle/Walk.</summary>
        public void PlayOneShot(string clipId)
        {
            oneShotClip = clipId;
            currentClip = null;
        }

        private void Update()
        {
            if (animationSet == null) return;

            bool isMoving = false;
            if (motion != null)
            {
                isMoving = motion.IsMoving;
                if (motion.FacingDirection.sqrMagnitude > 0f)
                    Direction = Direction8Util.FromVector(motion.FacingDirection);
            }

            string wantedClip = oneShotClip ?? (isMoving ? walkClip : idleClip);
            if (wantedClip != currentClip)
            {
                currentClip = wantedClip;
                clipTime = 0f;
            }
            else
            {
                clipTime += Time.deltaTime;
            }

            if (!animationSet.TryGetFrames(currentClip, Direction, out DirectionalClip clip, out Sprite[] frames, out bool flipX))
            {
                oneShotClip = null;
                return;
            }

            int frame = Mathf.FloorToInt(clipTime * clip.framesPerSecond);
            if (clip.loop)
            {
                frame %= frames.Length;
            }
            else if (frame >= frames.Length)
            {
                frame = frames.Length - 1;
                if (currentClip == oneShotClip)
                    oneShotClip = null;
            }

            spriteRenderer.sprite = frames[frame];
            spriteRenderer.flipX = flipX;
        }
    }
}
