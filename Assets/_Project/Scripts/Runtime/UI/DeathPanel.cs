using RPG.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>
    /// The source game's "You have died. Respawn?": shown while the local player is dead, its Respawn button brings them
    /// back at the spawn point (<see cref="PlayerRespawn.RequestRespawn"/>); a friend's Deliverance can come first. Builds
    /// its own UI on its RectTransform.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class DeathPanel : MonoBehaviour
    {
        private GameObject panel;
        private Button respawn;
        private float deadSince = -1f;

        private void Awake()
        {
            Image background = UIFactory.CreateImage("Death", transform, new Color(0.08f, 0.04f, 0.04f, 0.92f));
            panel = background.gameObject;
            UIFactory.Stretch(background.rectTransform);
            UIFactory.AddFrame(background);

            Text text = UIFactory.CreateText("Text", background.transform, 22, TextAnchor.UpperCenter);
            UIFactory.Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(12f, 60f);
            text.rectTransform.offsetMax = new Vector2(-12f, -16f);
            text.text = "You have died.\n<size=15>Respawn at the start, or wait for a friend to revive you.</size>";
            text.color = new Color(1f, 0.82f, 0.72f);

            respawn = UIFactory.CreateButton("Respawn", background.transform, new Color(0.45f, 0.14f, 0.1f, 0.95f), "Respawn", 18);
            var rect = (RectTransform)respawn.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 14f);
            rect.sizeDelta = new Vector2(180f, 40f);
            respawn.onClick.AddListener(Respawn);
            panel.SetActive(false);
        }

        private static void Respawn()
        {
            PlayerRespawn player = UIFactory.LocalPlayer<PlayerRespawn>();
            if (player != null) player.RequestRespawn();
        }

        private void Update()
        {
            Health health = UIFactory.LocalPlayer<Health>();
            bool dead = health != null && health.IsSpawned && health.IsDead;
            if (panel.activeSelf != dead)
            {
                panel.SetActive(dead);
                deadSince = dead ? Time.unscaledTime : -1f;
            }
            // Like the server: Respawn works after a moment (a stray click does not skip a revive).
            bool ready = dead && Time.unscaledTime - deadSince >= PlayerRespawn.MinimumDeadSeconds;
            if (dead && respawn.interactable != ready) respawn.interactable = ready;
        }
    }
}
