using UnityEngine;

namespace RPG.Combat
{
    /// <summary>A ring on the ground under the local player's selected target.</summary>
    public class TargetIndicator : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer ring;

        private void Awake()
        {
            if (ring == null) ring = GetComponentInChildren<SpriteRenderer>();
        }

        private void LateUpdate()
        {
            Health target = PlayerTargeting.LocalTarget;
            bool show = target != null && target.IsSpawned && !target.IsDead;
            ring.enabled = show;
            if (show)
                transform.position = new Vector3(target.transform.position.x, target.transform.position.y, transform.position.z);
        }
    }
}
