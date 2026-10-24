using System;
using Unity.Netcode;
using UnityEngine;

namespace RPG.World
{
    /// <summary>The game's sounds (<see cref="GameAudio.Play"/>).</summary>
    public enum Sfx
    {
        Swing, Shot, Cast, FireCast, Hit, HitHeavy, MonsterHurt, MonsterDie, PlayerDie, LevelUp, Coins, Pickup, Gem, Fail,
        Error, Click, Book, Craft, Footstep,
    }

    /// <summary>
    /// Music, ambience and sounds, on every peer for its own player (only for show). The music and the ambience follow the
    /// zone the local player is in, as the source game sets them (its zone_template music and ambience, as moods:
    /// StartAreaData.ZoneSounds): town, fields, forest, dark places and dungeons, crossfading on the way; at night the
    /// ambience of the fields and woods turns to the night's. Sounds are played where they happen, quieter the farther
    /// from the camera. Muted with the HUD's sound button, remembered on this computer.
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        public const string MusicTown = "Town", MusicFields = "Fields", MusicForest = "Forest", MusicDark = "Dark", MusicDungeon = "Dungeon";
        public const string AmbienceForest = "Forest", AmbienceNight = "Night", AmbienceCave = "Cave", AmbienceWind = "Wind";

        private const float MusicVolume = 0.32f, AmbienceVolume = 0.3f, SoundVolume = 0.7f, FadeSeconds = 2.5f;
        private const float Hearing = 14f, StepInterval = 0.34f;
        private const string MutedKey = "rpg.sound.muted";

        [Serializable]
        public struct Named
        {
            public string name;
            public AudioClip clip;
        }

        [Serializable]
        public struct Sound
        {
            public Sfx kind;
            public AudioClip[] clips;
            [Range(0f, 1f)] public float volume;
        }

        [SerializeField] private Named[] music = Array.Empty<Named>();
        [SerializeField] private Named[] ambience = Array.Empty<Named>();
        [SerializeField] private Sound[] sounds = Array.Empty<Sound>();
        [SerializeField] private string[] zones = Array.Empty<string>();
        [SerializeField] private string[] zoneMusic = Array.Empty<string>();
        [SerializeField] private string[] zoneAmbience = Array.Empty<string>();
        [Tooltip("Zones that are underground (dungeons): dungeon music and cave ambience whatever the source says.")]
        [SerializeField] private string[] underground = Array.Empty<string>();

        private static GameAudio instance;
        private static bool? muted;

        private AudioSource[] musicSources;
        private int musicPlaying;
        private AudioSource ambienceSource;
        private AudioSource[] soundSources;
        private int nextSoundSource;
        private readonly float[] lastPlayed = new float[Enum.GetValues(typeof(Sfx)).Length];
        private string lastZone;
        private string wantedMusic, wantedAmbience;
        private float stepTimer;
        private Vector3 lastPlayerPosition;

        /// <summary>No sound at all (remembered on this computer).</summary>
        public static bool Muted
        {
            get
            {
                if (muted == null)
                {
                    try { muted = PlayerPrefs.GetInt(MutedKey, 0) == 1; }
                    catch (Exception) { muted = false; }
                }
                return muted.Value;
            }
            set
            {
                muted = value;
                try { PlayerPrefs.SetInt(MutedKey, value ? 1 : 0); }
                catch (Exception) { }
                AudioListener.volume = value ? 0f : 1f;
            }
        }

        public void Configure(Named[] musicClips, Named[] ambienceClips, Sound[] soundClips,
            (string zone, string music, string ambience)[] zoneSounds, string[] undergroundZones)
        {
            music = musicClips ?? Array.Empty<Named>();
            ambience = ambienceClips ?? Array.Empty<Named>();
            sounds = soundClips ?? Array.Empty<Sound>();
            zones = new string[zoneSounds.Length];
            zoneMusic = new string[zoneSounds.Length];
            zoneAmbience = new string[zoneSounds.Length];
            for (int i = 0; i < zoneSounds.Length; i++)
            {
                zones[i] = zoneSounds[i].zone;
                zoneMusic[i] = zoneSounds[i].music;
                zoneAmbience[i] = zoneSounds[i].ambience;
            }
            underground = undergroundZones ?? Array.Empty<string>();
        }

        private void Awake()
        {
            instance = this;
            musicSources = new[] { Source(true), Source(true) };
            ambienceSource = Source(true);
            soundSources = new AudioSource[10];
            for (int i = 0; i < soundSources.Length; i++) soundSources[i] = Source(false);
            AudioListener.volume = Muted ? 0f : 1f;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private AudioSource Source(bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.loop = loop;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            return source;
        }

        /// <summary>Plays a sound where it happens (null: everywhere, at full volume), quieter the farther from the camera.</summary>
        public static void Play(Sfx kind, Vector3? at = null, float volume = 1f)
        {
            if (instance != null) instance.PlayHere(kind, at, volume);
        }

        private void PlayHere(Sfx kind, Vector3? at, float volume)
        {
            if (Muted) return;
            // The same sound at most twenty times a second (a hit on ten monsters at once is one sound).
            int index = (int)kind;
            if (Time.unscaledTime - lastPlayed[index] < 0.05f) return;
            Sound sound = default;
            bool found = false;
            foreach (Sound candidate in sounds)
                if (candidate.kind == kind && candidate.clips != null && candidate.clips.Length > 0)
                {
                    sound = candidate;
                    found = true;
                    break;
                }
            if (!found) return;

            if (at.HasValue)
            {
                Camera view = Camera.main;
                if (view != null)
                {
                    float distance = Vector2.Distance(view.transform.position, at.Value);
                    if (distance > Hearing) return;
                    volume *= 1f - distance / Hearing;
                }
            }
            lastPlayed[index] = Time.unscaledTime;
            AudioSource source = soundSources[nextSoundSource];
            nextSoundSource = (nextSoundSource + 1) % soundSources.Length;
            source.pitch = UnityEngine.Random.Range(0.94f, 1.06f);
            source.PlayOneShot(sound.clips[UnityEngine.Random.Range(0, sound.clips.Length)],
                Mathf.Clamp01(volume * (sound.volume > 0f ? sound.volume : 1f) * SoundVolume));
        }

        private void Update()
        {
            Transform player = LocalPlayer();
            Vector2 position = player != null ? (Vector2)player.position
                : Camera.main != null ? (Vector2)Camera.main.transform.position : Vector2.zero;
            string zone = Zone.NameAt(position) ?? "";
            if (zone != lastZone)
            {
                lastZone = zone;
                bool isUnderground = Array.IndexOf(underground, zone) >= 0;
                int index = Array.IndexOf(zones, zone);
                if (index < 0) index = Array.IndexOf(zones, "");
                wantedMusic = isUnderground ? MusicDungeon : index >= 0 ? zoneMusic[index] : MusicFields;
                wantedAmbience = isUnderground ? AmbienceCave : index >= 0 ? zoneAmbience[index] : AmbienceForest;
            }
            // Birds by day, the night's quiet at night.
            string ambienceNow = wantedAmbience == AmbienceForest && DayNight.NightAmount > 0.5f ? AmbienceNight : wantedAmbience;

            UpdateMusic(Clip(music, wantedMusic));
            Fade(ambienceSource, Clip(ambience, ambienceNow) ?? Clip(ambience, AmbienceForest), ambienceNow == AmbienceNight ? AmbienceVolume * 0.4f : AmbienceVolume);
            UpdateFootsteps(player);
        }

        private void UpdateMusic(AudioClip wanted)
        {
            AudioSource current = musicSources[musicPlaying];
            if (wanted != null && current.clip != wanted && current.volume <= 0.01f)
            {
                current.clip = wanted;
                current.Play();
            }
            else if (wanted != null && current.clip != wanted)
            {
                // Fade the playing track out on one source and the new one in on the other.
                musicPlaying = 1 - musicPlaying;
                AudioSource next = musicSources[musicPlaying];
                if (next.clip != wanted)
                {
                    next.clip = wanted;
                    next.Play();
                }
            }
            for (int i = 0; i < musicSources.Length; i++)
            {
                AudioSource source = musicSources[i];
                float target = i == musicPlaying && source.clip == wanted ? MusicVolume : 0f;
                source.volume = Mathf.MoveTowards(source.volume, target, MusicVolume * Time.unscaledDeltaTime / FadeSeconds);
                if (source.volume <= 0f && i != musicPlaying && source.isPlaying) source.Stop();
            }
        }

        private static void Fade(AudioSource source, AudioClip wanted, float volume)
        {
            if (source.clip != wanted)
            {
                source.volume = Mathf.MoveTowards(source.volume, 0f, volume * Time.unscaledDeltaTime / FadeSeconds);
                if (source.volume > 0f) return;
                source.clip = wanted;
                if (wanted != null) source.Play();
            }
            source.volume = Mathf.MoveTowards(source.volume, wanted != null ? volume : 0f, volume * Time.unscaledDeltaTime / FadeSeconds);
        }

        private void UpdateFootsteps(Transform player)
        {
            if (player == null) return;
            Vector3 position = player.position;
            float speed = Vector2.Distance(position, lastPlayerPosition) / Mathf.Max(Time.deltaTime, 0.0001f);
            lastPlayerPosition = position;
            if (speed < 0.5f || speed > 20f)
            {
                stepTimer = 0f;
                return;
            }
            stepTimer -= Time.deltaTime;
            if (stepTimer > 0f) return;
            stepTimer = StepInterval;
            PlayHere(Sfx.Footstep, null, 0.35f);
        }

        private static AudioClip Clip(Named[] clips, string name)
        {
            foreach (Named named in clips)
                if (named.name == name) return named.clip;
            return null;
        }

        private static Transform LocalPlayer()
        {
            NetworkManager network = NetworkManager.Singleton;
            if (network == null || !network.IsListening || network.LocalClient == null) return null;
            NetworkObject playerObject = network.LocalClient.PlayerObject;
            return playerObject != null ? playerObject.transform : null;
        }
    }
}
