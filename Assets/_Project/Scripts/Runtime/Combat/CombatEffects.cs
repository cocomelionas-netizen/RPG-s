using System.Collections.Generic;
using UnityEngine;

namespace RPG.Combat
{
    /// <summary>
    /// Short-lived visual effects for combat, shown locally on each peer: projectiles flying to a target
    /// and rings spreading on the ground (area abilities, heals). Purely cosmetic: hits are decided by the server.
    /// One instance lives in the scene; the static methods do nothing when there is none (tests, servers).
    /// </summary>
    public class CombatEffects : MonoBehaviour
    {
        [SerializeField] private Sprite projectileSprite;
        [Tooltip("A white ground ring; its visible radius is ringSpriteRadius world units at scale 1.")]
        [SerializeField] private Sprite ringSprite;
        [SerializeField, Min(0.01f)] private float ringSpriteRadius = 0.205f;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 12f;
        [SerializeField, Min(0.05f)] private float ringSeconds = 0.4f;
        [Tooltip("Projectiles fly at about chest height above the units' feet.")]
        [SerializeField] private float projectileHeight = 0.4f;

        private const int SortingOrder = 400;

        private static CombatEffects instance;

        private class Effect
        {
            public SpriteRenderer Renderer;
            public bool IsRing;
            public Vector3 From;
            public Vector3 To;
            public float Duration;
            public float Age;
            public float Radius;
            public Color Color;
        }

        private readonly List<Effect> effects = new List<Effect>();

        private void Awake()
        {
            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        /// <summary>Sets the sprites (used by the scene builder).</summary>
        public void Configure(Sprite projectile, Sprite ring, float ringRadiusAtScaleOne)
        {
            projectileSprite = projectile;
            ringSprite = ring;
            ringSpriteRadius = Mathf.Max(0.01f, ringRadiusAtScaleOne);
        }

        /// <summary>A projectile flying from one unit's feet position to another's.</summary>
        public static void Projectile(Vector2 from, Vector2 to, Color color)
        {
            if (instance != null) instance.SpawnProjectile(from, to, color);
        }

        /// <summary>A ring spreading on the ground to <paramref name="radius"/> world units.</summary>
        public static void Ring(Vector2 center, float radius, Color color)
        {
            if (instance != null) instance.SpawnRing(center, radius, color);
        }

        private void SpawnProjectile(Vector2 from, Vector2 to, Color color)
        {
            if (projectileSprite == null) return;
            Effect effect = Take(projectileSprite, color);
            effect.IsRing = false;
            effect.From = new Vector3(from.x, from.y + projectileHeight, 0f);
            effect.To = new Vector3(to.x, to.y + projectileHeight, 0f);
            effect.Duration = Mathf.Max(0.06f, Vector2.Distance(from, to) / projectileSpeed);
            effect.Renderer.transform.localScale = Vector3.one;
            Place(effect);
        }

        private void SpawnRing(Vector2 center, float radius, Color color)
        {
            if (ringSprite == null || radius <= 0f) return;
            Effect effect = Take(ringSprite, color);
            effect.IsRing = true;
            effect.From = effect.To = new Vector3(center.x, center.y, 0f);
            effect.Radius = radius;
            effect.Duration = ringSeconds;
            Place(effect);
        }

        private Effect Take(Sprite sprite, Color color)
        {
            Effect effect = effects.Find(e => !e.Renderer.gameObject.activeSelf);
            if (effect == null)
            {
                var go = new GameObject("Effect");
                go.transform.SetParent(transform, false);
                var spriteRenderer = go.AddComponent<SpriteRenderer>();
                spriteRenderer.sortingOrder = SortingOrder;
                effect = new Effect { Renderer = spriteRenderer };
                effects.Add(effect);
            }

            effect.Renderer.sprite = sprite;
            effect.Renderer.color = color;
            effect.Color = color;
            effect.Age = 0f;
            effect.Renderer.gameObject.SetActive(true);
            return effect;
        }

        private void LateUpdate()
        {
            foreach (Effect effect in effects)
            {
                if (!effect.Renderer.gameObject.activeSelf) continue;

                effect.Age += Time.deltaTime;
                if (effect.Age >= effect.Duration)
                {
                    effect.Renderer.gameObject.SetActive(false);
                    continue;
                }
                Place(effect);
            }
        }

        private void Place(Effect effect)
        {
            float t = Mathf.Clamp01(effect.Age / effect.Duration);
            Transform effectTransform = effect.Renderer.transform;

            if (effect.IsRing)
            {
                // Spreads out from 40 % to the full radius while fading.
                float scale = effect.Radius / ringSpriteRadius * Mathf.Lerp(0.4f, 1f, Mathf.Sqrt(t));
                effectTransform.position = effect.From;
                effectTransform.localScale = new Vector3(scale, scale, 1f);
                Color color = effect.Color;
                color.a *= 1f - t * t;
                effect.Renderer.color = color;
            }
            else
            {
                effectTransform.position = Vector3.Lerp(effect.From, effect.To, t);
            }
        }
    }
}
