using System;
using UnityEngine;

namespace RPG.Characters
{
    /// <summary>
    /// Sprite frames for one animation (for example Idle or Walk) in all eight directions.
    /// </summary>
    [Serializable]
    public class DirectionalClip
    {
        public string id = CharacterAnimationSet.IdleClip;
        [Min(0.1f)] public float framesPerSecond = 8f;
        public bool loop = true;

        public Sprite[] east = Array.Empty<Sprite>();
        public Sprite[] northEast = Array.Empty<Sprite>();
        public Sprite[] north = Array.Empty<Sprite>();
        [Tooltip("Leave empty to mirror North East (when mirroring is enabled on the set).")]
        public Sprite[] northWest = Array.Empty<Sprite>();
        [Tooltip("Leave empty to mirror East (when mirroring is enabled on the set).")]
        public Sprite[] west = Array.Empty<Sprite>();
        [Tooltip("Leave empty to mirror South East (when mirroring is enabled on the set).")]
        public Sprite[] southWest = Array.Empty<Sprite>();
        public Sprite[] south = Array.Empty<Sprite>();
        public Sprite[] southEast = Array.Empty<Sprite>();

        public Sprite[] GetFrames(Direction8 direction)
        {
            switch (direction)
            {
                case Direction8.East: return east;
                case Direction8.NorthEast: return northEast;
                case Direction8.North: return north;
                case Direction8.NorthWest: return northWest;
                case Direction8.West: return west;
                case Direction8.SouthWest: return southWest;
                case Direction8.South: return south;
                default: return southEast;
            }
        }
    }

    /// <summary>
    /// All animations of one character type. Shared by every instance of that character.
    /// </summary>
    [CreateAssetMenu(menuName = "RPG/Characters/Animation Set", fileName = "AnimationSet")]
    public class CharacterAnimationSet : ScriptableObject
    {
        public const string IdleClip = "Idle";
        public const string WalkClip = "Walk";

        [Tooltip("West-facing directions without frames use the matching east-facing frames flipped horizontally. Halves the art needed.")]
        [SerializeField] private bool mirrorWestFromEast = true;
        [SerializeField] private DirectionalClip[] clips = Array.Empty<DirectionalClip>();

        public bool MirrorWestFromEast
        {
            get => mirrorWestFromEast;
            set => mirrorWestFromEast = value;
        }

        public DirectionalClip[] Clips
        {
            get => clips;
            set => clips = value ?? Array.Empty<DirectionalClip>();
        }

        public DirectionalClip FindClip(string clipId)
        {
            foreach (DirectionalClip clip in clips)
                if (clip != null && clip.id == clipId)
                    return clip;
            return null;
        }

        /// <summary>
        /// Resolves the frames to show for a clip and direction, applying mirroring.
        /// Falls back to the south-facing frames so a character is never invisible.
        /// </summary>
        public bool TryGetFrames(string clipId, Direction8 direction, out DirectionalClip clip, out Sprite[] frames, out bool flipX)
        {
            flipX = false;
            frames = null;
            clip = FindClip(clipId);
            if (clip == null)
                return false;

            frames = clip.GetFrames(direction);
            if (IsEmpty(frames) && mirrorWestFromEast && Direction8Util.IsWestSide(direction))
            {
                frames = clip.GetFrames(Direction8Util.MirrorHorizontally(direction));
                flipX = true;
            }

            if (IsEmpty(frames))
            {
                frames = clip.south;
                flipX = false;
            }

            return !IsEmpty(frames);
        }

        private static bool IsEmpty(Sprite[] frames) => frames == null || frames.Length == 0;
    }
}
