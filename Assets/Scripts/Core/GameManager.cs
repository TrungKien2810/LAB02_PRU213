using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace SnowBoarder
{
    public enum GameState { Countdown, Playing, Paused, Crashed, Finished, GameOver }

    /// <summary>
    /// Owns the run: score, combo/multiplier, lives, checkpoints, time trial clock,
    /// pause, finish and game over.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Level")]
        [SerializeField] int levelIndex;
        [SerializeField] PlayerController player;
        [SerializeField] HUDController hud;
        [SerializeField] Transform startPoint;
        [SerializeField] TerrainGenerator terrain;   // found automatically when left empty
        [SerializeField] float fallMargin = 6f;      // metres below the snow that mean "fell through"

        [Header("Rules")]
        [SerializeField] int startLives = 3;
        [SerializeField] float comboWindow = 4f;        // seconds to chain the next move
        [SerializeField] float multiplierStep = 0.5f;    // +0.5x per chained move
        [SerializeField] float maxMultiplier = 5f;
        [SerializeField] float speedScoreRate = 1.5f;    // points per (m/s * s)
        [SerializeField] float timeTrialCrashPenalty = 3f;

        public GameState State { get; private set; } = GameState.Countdown;
        public GameMode Mode { get; private set; }
        public int LevelIndex => levelIndex;
        public int Score { get; private set; }
        public int Lives { get; private set; }
        public int Combo { get; private set; }
        public float Multiplier => Mathf.Min(1f + Combo * multiplierStep, maxMultiplier);
        public float ComboTimeLeft { get; private set; }
        public float ComboWindow => comboWindow;
        public float RunTime { get; private set; }
        public int Snowflakes { get; private set; }
        public int TotalSnowflakes { get; private set; }
        public int TricksLanded { get; private set; }
        public int BestCombo { get; private set; }
        public PlayerController Player => player;

        /// <summary>True while a crash still costs something and is followed by a respawn.</summary>
        public bool CanCrash => State == GameState.Playing;

        Vector3 checkpoint;
        float speedScoreAccum;
        float timePenalty;

        void Awake()
        {
            Instance = this;
            Mode = GameSettings.Mode;
            Lives = Mode == GameMode.TimeTrial ? 0 : startLives;
            Time.timeScale = 1f;
            checkpoint = startPoint ? startPoint.position : player.transform.position;
            TotalSnowflakes = FindObjectsByType<Collectible>().Length;
            // Looked up rather than required in the inspector so levels built before this
            // field existed still get the safety net.
            if (!terrain) terrain = FindFirstObjectByType<TerrainGenerator>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
        }

        void Start()
        {
            player.Landed += OnPlayerLanded;
            StartCoroutine(CountdownRoutine());
        }

        IEnumerator CountdownRoutine()
        {
            State = GameState.Countdown;
            player.Freeze(true);
            for (int i = 3; i > 0; i--)
            {
                hud.ShowCountdown(i.ToString());
                AudioManager.PlaySfx(Sfx.Click);
                yield return new WaitForSeconds(0.7f);
            }
            hud.ShowCountdown("GO!");
            AudioManager.PlaySfx(Sfx.Checkpoint);
            player.Freeze(false);
            player.Body.linearVelocity = Vector2.right * 5f;
            State = GameState.Playing;
            yield return new WaitForSeconds(0.6f);
            hud.ShowCountdown("");
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame))
            {
                if (State == GameState.Playing) Pause();
                else if (State == GameState.Paused) Resume();
            }
            if (kb != null && kb.rKey.wasPressedThisFrame && State != GameState.Paused) Restart();

            if (State != GameState.Playing) return;

            if (FellThroughTerrain()) { RecoverFromFall(); return; }

            RunTime += Time.deltaTime;

            // speed points: faster riding = more points
            if (player.IsTouchingGround)
            {
                speedScoreAccum += player.Speed * speedScoreRate * Time.deltaTime;
                if (speedScoreAccum >= 1f)
                {
                    int pts = Mathf.FloorToInt(speedScoreAccum);
                    speedScoreAccum -= pts;
                    Score += pts;
                }
            }

            // combo decays only while riding on the ground
            if (Combo > 0 && player.IsTouchingGround)
            {
                ComboTimeLeft -= Time.deltaTime;
                if (ComboTimeLeft <= 0f) EndCombo();
            }
        }

        public float DisplayTime => RunTime + timePenalty;

        // ---------------- scoring ----------------
        /// <summary>Add points with the current multiplier applied.</summary>
        public int AddScore(int basePoints)
        {
            int pts = Mathf.RoundToInt(basePoints * Multiplier);
            Score += pts;
            return pts;
        }

        /// <summary>A successful move extends the combo chain and raises the multiplier.</summary>
        void ChainMove()
        {
            Combo++;
            BestCombo = Mathf.Max(BestCombo, Combo);
            ComboTimeLeft = comboWindow;
        }

        void EndCombo()
        {
            if (Combo >= 3) hud.ShowPopup($"Combo x{Combo} ended", new Color(0.8f, 0.9f, 1f), 0.8f);
            Combo = 0;
            ComboTimeLeft = 0f;
        }

        void BreakCombo()
        {
            if (Combo > 0) hud.ShowPopup("COMBO LOST", new Color(1f, 0.4f, 0.4f), 0.9f);
            Combo = 0;
            ComboTimeLeft = 0f;
        }

        void OnPlayerLanded(int flips, float airTime, float grabTime, bool clean)
        {
            if (State != GameState.Playing) return;

            if (!clean)
            {
                if (Mathf.Abs(flips) > 0 || airTime > 1f) hud.ShowPopup("Sloppy landing!", new Color(1f, 0.6f, 0.3f), 0.9f);
                BreakCombo();
                return;
            }

            int basePoints = 0;
            var parts = new System.Collections.Generic.List<string>();
            int absFlips = Mathf.Abs(flips);
            if (absFlips > 0)
            {
                string name = flips > 0 ? "BACKFLIP" : "FRONTFLIP";
                parts.Add(absFlips > 1 ? $"{absFlips}x {name}" : name);
                basePoints += 500 * absFlips * absFlips; // doubles/triples are worth a lot more
            }
            if (grabTime > 0.3f)
            {
                parts.Add(grabTime > 1f ? "LONG GRAB" : "GRAB");
                basePoints += Mathf.RoundToInt(200 + grabTime * 300);
            }
            if (airTime > 1.2f)
            {
                parts.Add(airTime > 2.2f ? "HUGE AIR" : "BIG AIR");
                basePoints += Mathf.RoundToInt(airTime * 150);
            }

            if (basePoints == 0) return;

            ChainMove();
            TricksLanded++;
            int pts = AddScore(basePoints);
            string label = string.Join(" + ", parts);
            hud.ShowPopup($"{label}\n+{pts}", new Color(1f, 0.85f, 0.2f), 1.3f);
            AudioManager.PlaySfx(Sfx.Trick);
        }

        public void CollectSnowflake(int points)
        {
            Snowflakes++;
            // snowflakes keep an active combo alive but only tricks/smashes raise the multiplier
            if (Combo > 0) ComboTimeLeft = comboWindow;
            AddScore(points);
            AudioManager.PlaySfx(Sfx.Pickup, 0.7f);
        }

        public void CollectPowerUp(PowerUpType type, float duration)
        {
            AudioManager.PlaySfx(Sfx.PowerUp);
            AddScore(100);
            switch (type)
            {
                case PowerUpType.ExtraLife:
                    if (Mode == GameMode.Adventure) { Lives++; hud.ShowPopup("+1 LIFE", new Color(1f, 0.5f, 0.6f), 1f); }
                    else { timePenalty -= 2f; hud.ShowPopup("-2s", new Color(0.4f, 1f, 0.6f), 1f); }
                    break;
                case PowerUpType.TimeBonus:
                    timePenalty -= 3f;
                    hud.ShowPopup("TIME -3s", new Color(0.4f, 1f, 0.6f), 1f);
                    break;
                case PowerUpType.SpeedBoost:
                    player.ApplyPowerUp(type, duration);
                    hud.ShowPopup("SPEED BOOST!", new Color(1f, 0.6f, 0.2f), 1f);
                    break;
                case PowerUpType.Invincibility:
                    player.ApplyPowerUp(type, duration);
                    hud.ShowPopup("INVINCIBLE!", new Color(0.4f, 0.7f, 1f), 1f);
                    break;
                case PowerUpType.Magnet:
                    player.ApplyPowerUp(type, duration);
                    hud.ShowPopup("SNOWFLAKE MAGNET!", new Color(0.8f, 0.5f, 1f), 1f);
                    break;
            }
        }

        public void OnObstacleSmashed(int points)
        {
            ChainMove();
            int pts = AddScore(points);
            hud.ShowPopup($"SMASH! +{pts}", new Color(0.5f, 0.8f, 1f), 0.8f);
        }

        public void OnObstacleBumped()
        {
            BreakCombo();
        }

        public void SetCheckpoint(Vector3 position)
        {
            checkpoint = position;
            hud.ShowPopup("CHECKPOINT", new Color(1f, 0.85f, 0.3f), 1f);
            AudioManager.PlaySfx(Sfx.Checkpoint);
        }

        // ---------------- crash / respawn ----------------
        /// <summary>
        /// The snow is a single zero-thickness EdgeCollider2D and the visible snow body is only a
        /// mesh, so there is nothing solid underneath to land on. Anything that ends up below the
        /// surface line would fall for the rest of the session: the camera follows it out of the
        /// world, no crash is ever reported and no timer ends the run. This is the last-resort
        /// check that turns that softlock into a normal respawn.
        /// </summary>
        bool FellThroughTerrain()
        {
            if (!terrain || !player || player.IsCrashed || player.IsFrozen) return false;
            if (terrain.Points == null || terrain.Points.Length < 2) return false; // no surface to compare against
            Vector3 p = player.transform.position;
            return p.y < terrain.HeightAt(p.x) - Mathf.Max(2f, fallMargin);
        }

        void RecoverFromFall()
        {
            BreakCombo();
            CameraFollow.Instance?.Shake(0.6f, 0.5f);
            AudioManager.PlaySfx(Sfx.Crash, 0.8f);

            if (Mode == GameMode.Adventure)
            {
                Lives--;
                hud.ShowPopup(Lives > 0 ? "OFF THE TRACK!" : "OUT OF LIVES!", new Color(1f, 0.4f, 0.4f), 1.2f);
            }
            else
            {
                timePenalty += timeTrialCrashPenalty;
                hud.ShowPopup($"OFF THE TRACK! +{timeTrialCrashPenalty:0}s", new Color(1f, 0.4f, 0.4f), 1.2f);
            }

            player.Respawn(checkpoint); // straight back to solid ground, no 1.5 s of extra falling

            if (Mode == GameMode.Adventure && Lives <= 0)
            {
                // Out of lives: hold the boarder at the checkpoint while the results come up.
                State = GameState.Crashed; // also stops Update from re-entering this routine
                player.Freeze(true);
                StartCoroutine(GameOverRoutine());
            }
        }

        public void OnPlayerCrashed()
        {
            if (State != GameState.Playing) return;
            State = GameState.Crashed;
            BreakCombo();
            CameraFollow.Instance?.Shake(0.6f, 0.5f);

            if (Mode == GameMode.Adventure)
            {
                Lives--;
                if (Lives <= 0) { StartCoroutine(GameOverRoutine()); return; }
                hud.ShowPopup(Lives == 1 ? "OUCH! Last life!" : $"OUCH! {Lives} lives left", new Color(1f, 0.4f, 0.4f), 1.2f);
            }
            else
            {
                timePenalty += timeTrialCrashPenalty;
                hud.ShowPopup($"CRASH +{timeTrialCrashPenalty:0}s", new Color(1f, 0.4f, 0.4f), 1.2f);
            }
            StartCoroutine(RespawnRoutine());
        }

        IEnumerator RespawnRoutine()
        {
            yield return new WaitForSeconds(1.5f);
            player.Respawn(checkpoint);
            State = GameState.Playing;
        }

        IEnumerator GameOverRoutine()
        {
            yield return new WaitForSeconds(1.5f);
            State = GameState.GameOver;
            player.Freeze(true);
            AudioManager.PlaySfx(Sfx.GameOver);
            bool newBest = GameSettings.TrySetBestScore(levelIndex, Score);
            hud.ShowGameOver(Score, newBest);
        }

        // ---------------- finish ----------------
        public void OnFinishReached()
        {
            if (State != GameState.Playing) return;
            State = GameState.Finished;
            StartCoroutine(FinishRoutine());
        }

        IEnumerator FinishRoutine()
        {
            AudioManager.PlaySfx(Sfx.Finish);
            hud.ShowPopup("FINISH!", Color.white, 1.5f);

            // finish bonuses
            int livesBonus = Mode == GameMode.Adventure ? Lives * 1000 : 0;
            float target = LevelCatalog.Levels[levelIndex].TargetTime;
            int timeBonus = Mathf.Max(0, Mathf.RoundToInt((target - DisplayTime) * 50f));
            int flakeBonus = TotalSnowflakes > 0 && Snowflakes == TotalSnowflakes ? 2000 : 0;
            Score += livesBonus + timeBonus + flakeBonus;

            yield return new WaitForSeconds(1.5f);
            player.Freeze(true);

            bool newBestScore = GameSettings.TrySetBestScore(levelIndex, Score);
            bool newBestTime = Mode == GameMode.TimeTrial && GameSettings.TrySetBestTime(levelIndex, DisplayTime);
            if (levelIndex + 1 < LevelCatalog.Levels.Length) GameSettings.UnlockLevel(levelIndex + 1);

            int stars = 1;
            if (DisplayTime <= target) stars++;
            if (Mode == GameMode.TimeTrial ? DisplayTime <= target * 0.85f : Score >= 15000 + levelIndex * 8000) stars++;

            hud.ShowLevelComplete(new LevelResult
            {
                Score = Score,
                Time = DisplayTime,
                Snowflakes = Snowflakes,
                TotalSnowflakes = TotalSnowflakes,
                Tricks = TricksLanded,
                BestCombo = BestCombo,
                LivesBonus = livesBonus,
                TimeBonus = timeBonus,
                FlakeBonus = flakeBonus,
                Stars = stars,
                NewBestScore = newBestScore,
                NewBestTime = newBestTime,
                HasNextLevel = levelIndex + 1 < LevelCatalog.Levels.Length,
                Mode = Mode,
            });
        }

        // ---------------- flow ----------------
        public void Pause()
        {
            State = GameState.Paused;
            Time.timeScale = 0f;
            AudioManager.PlaySfx(Sfx.Pause);
            hud.ShowPause(true);
        }

        public void Resume()
        {
            State = GameState.Playing;
            Time.timeScale = 1f;
            AudioManager.PlaySfx(Sfx.Click);
            hud.ShowPause(false);
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void NextLevel()
        {
            Time.timeScale = 1f;
            int next = Mathf.Min(levelIndex + 1, LevelCatalog.Levels.Length - 1);
            GameSettings.SelectedLevel = next;
            SceneManager.LoadScene(LevelCatalog.Levels[next].SceneName);
        }

        public void MainMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(LevelCatalog.MainMenuScene);
        }
    }

    public struct LevelResult
    {
        public int Score;
        public float Time;
        public int Snowflakes, TotalSnowflakes, Tricks, BestCombo;
        public int LivesBonus, TimeBonus, FlakeBonus;
        public int Stars;
        public bool NewBestScore, NewBestTime, HasNextLevel;
        public GameMode Mode;
    }
}
