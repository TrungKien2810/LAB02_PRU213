using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace SnowBoarder
{
    public enum Sfx { Crash, Finish, Pickup, PowerUp, Jump, Land, Trick, Hit, Smash, Boost, Pause, Click, Checkpoint, GameOver, Hover }

    /// <summary>
    /// Plays sound effects and the background music. Crash/Finish use the lab's audio files,
    /// the rest are synthesized at startup so the project does not need extra audio assets.
    /// Also drives a wind/carving loop whose volume follows the player's speed.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        /// <summary>Raised when the player toggles mute (M), so the UI can give feedback.</summary>
        public static event System.Action<bool> MuteChanged;

        [SerializeField] AudioClip crashClip;
        [SerializeField] AudioClip finishClip;

        // A small pool of voices: each shot keeps its own pitch, so a hover tick that is
        // still ringing is not re-tuned by the click that follows it.
        const int SfxVoices = 6;

        AudioSource[] sfxVoices;
        int nextVoice;
        AudioSource windSource;
        AudioSource musicSource;
        AudioClip pickup, powerUp, jump, land, trick, hit, smash, boost, pause, click, hover, checkpoint, gameOver, crashFallback, finishFallback;
        float windTarget;
        float musicTarget, musicFade = 0f;

        void Awake()
        {
            Instance = this;
            GameSettings.ApplyVolume();

            sfxVoices = new AudioSource[SfxVoices];
            for (int i = 0; i < SfxVoices; i++)
            {
                sfxVoices[i] = gameObject.AddComponent<AudioSource>();
                sfxVoices[i].playOnAwake = false;
            }

            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.volume = 0f;
            musicSource.spatialBlend = 0f;

            windSource = gameObject.AddComponent<AudioSource>();
            windSource.clip = SfxSynth.Noise("Wind", 2f, 0.35f);
            windSource.loop = true;
            windSource.volume = 0f;
            windSource.Play();

            pickup = SfxSynth.Arpeggio("Pickup", new[] { 988f, 1319f }, 0.07f, 0.35f);
            powerUp = SfxSynth.Arpeggio("PowerUp", new[] { 523f, 659f, 784f, 1047f }, 0.07f, 0.35f);
            jump = SfxSynth.Sweep("Jump", 300f, 700f, 0.15f, 0.3f);
            land = SfxSynth.NoiseBurst("Land", 0.12f, 0.5f);
            trick = SfxSynth.Arpeggio("Trick", new[] { 784f, 988f, 1175f, 1568f }, 0.06f, 0.35f);
            hit = SfxSynth.Sweep("Hit", 220f, 90f, 0.18f, 0.5f);
            click = SfxSynth.Arpeggio("Click", new[] { 880f }, 0.04f, 0.25f);
            // 1976 Hz (B6) sits clear of the click's 880 / 1760 Hz partials, and the tick is
            // ~2.5x shorter, so hovering and clicking are two clearly different sounds
            hover = SfxSynth.Tick("Hover", 0.075f, 1976f, 0.6f);
            checkpoint = SfxSynth.Arpeggio("Checkpoint", new[] { 659f, 880f }, 0.1f, 0.35f);
            gameOver = SfxSynth.Arpeggio("GameOver", new[] { 392f, 330f, 262f }, 0.18f, 0.35f);
            smash = SfxSynth.NoiseBurst("Smash", 0.3f, 0.7f);
            boost = SfxSynth.Sweep("Boost", 260f, 1300f, 0.35f, 0.35f);
            pause = SfxSynth.Arpeggio("Pause", new[] { 660f, 440f }, 0.06f, 0.3f);

            // fallbacks so the game still has a crash / finish cue if the lab clips are unassigned
            crashFallback = SfxSynth.NoiseBurst("CrashSynth", 0.45f, 0.8f);
            finishFallback = SfxSynth.Arpeggio("FinishSynth", new[] { 784f, 988f, 1319f, 1568f }, 0.12f, 0.4f);
        }

        void Start() => StartCoroutine(PrepareMusicRoutine());

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            windSource.volume = Mathf.MoveTowards(windSource.volume, windTarget * GameSettings.SfxVolume, dt);
            windSource.pitch = 0.8f + windTarget * 0.6f;

            // music fades in when it is ready and out when the track is being rebuilt
            musicFade = Mathf.MoveTowards(musicFade, musicTarget, dt / 1.2f);
            musicSource.volume = musicFade * GameSettings.MusicVolume;

            var kb = Keyboard.current;
            if (kb != null && kb.mKey.wasPressedThisFrame)
            {
                GameSettings.ToggleMute();
                MuteChanged?.Invoke(GameSettings.Muted);
            }
        }

        /// <summary>0..1 intensity for the wind/carving loop.</summary>
        public void SetWind(float intensity) => windTarget = Mathf.Clamp01(intensity) * 0.6f;

        // ---------------- music ----------------
        /// <summary>
        /// Composes the loop for this scene (menu = calm theme, levels = slope theme) and
        /// renders it in small chunks so the procedural synthesis never stalls a frame.
        /// </summary>
        IEnumerator PrepareMusicRoutine()
        {
            bool menu = SceneManager.GetActiveScene().name == LevelCatalog.MainMenuScene;
            int level = 0;
            for (int i = 0; i < LevelCatalog.Levels.Length; i++)
                if (LevelCatalog.Levels[i].SceneName == SceneManager.GetActiveScene().name) level = i;

            var track = menu ? MusicSynth.MenuTheme() : MusicSynth.SlopeTheme(level);
            var clip = AudioClip.Create(menu ? "Menu Theme" : "Slope Theme", track.Length, 1, MusicSynth.Rate, false);

            var watch = System.Diagnostics.Stopwatch.StartNew();
            int pos = 0, block = MusicSynth.Rate / 20; // 50 ms of audio per render step
            while (pos < track.Length)
            {
                watch.Restart();
                while (pos < track.Length && watch.ElapsedMilliseconds < 6)
                {
                    track.Render(pos, Mathf.Min(pos + block, track.Length));
                    pos += block;
                }
                yield return null;
            }

            track.Normalize(0.85f);
            clip.SetData(track.Buffer, 0);

            musicSource.clip = clip;
            musicSource.Play();
            musicTarget = 1f;
        }

        public void Play(Sfx sfx, float volume = 1f)
        {
            AudioClip clip = ClipFor(sfx);
            if (clip == null) return;
            if (sfxVoices == null) return; // Instance is published before the voices exist

            float v = Mathf.Clamp01(volume) * GameSettings.SfxVolume;
            if (v <= 0.0001f) return;

            var voice = sfxVoices[nextVoice];
            nextVoice = (nextVoice + 1) % sfxVoices.Length;
            voice.pitch = PitchFor(sfx);
            voice.PlayOneShot(clip, v);
        }

        AudioClip ClipFor(Sfx sfx) => sfx switch
        {
            Sfx.Crash => crashClip ? crashClip : crashFallback,
            Sfx.Finish => finishClip ? finishClip : finishFallback,
            Sfx.Pickup => pickup,
            Sfx.PowerUp => powerUp,
            Sfx.Jump => jump,
            Sfx.Land => land,
            Sfx.Trick => trick,
            Sfx.Hit => hit,
            Sfx.Smash => smash,
            Sfx.Boost => boost,
            Sfx.Pause => pause,
            Sfx.Click => click,
            Sfx.Hover => hover,
            Sfx.Checkpoint => checkpoint,
            Sfx.GameOver => gameOver,
            _ => null
        };

        /// <summary>
        /// A few percent of random detune on the cues that repeat the most, so sweeping the
        /// pointer over a button row or picking up a run of snowflakes never turns into a
        /// machine gun. The one-shot crash / finish clips keep their original pitch.
        /// </summary>
        static float PitchFor(Sfx sfx) => sfx switch
        {
            Sfx.Hover => Random.Range(0.94f, 1.06f),
            Sfx.Click => Random.Range(0.96f, 1.04f),
            Sfx.Pickup => Random.Range(0.96f, 1.04f),
            _ => 1f
        };

        public static void PlaySfx(Sfx sfx, float volume = 1f)
        {
            if (Instance != null) Instance.Play(sfx, volume);
        }
    }
}
