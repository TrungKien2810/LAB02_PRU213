using UnityEngine;
using UnityEngine.UI;

namespace SnowBoarder
{
    /// <summary>
    /// In-game HUD: score, speed, lives, combo/multiplier, timer, power-ups,
    /// trick popups, countdown, plus the pause / game over / level complete panels.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Header("HUD")]
        [SerializeField] Text scoreText;
        [SerializeField] Text speedText;
        [SerializeField] Image speedFill;
        [SerializeField] Image[] hearts;
        [SerializeField] GameObject livesGroup;
        [SerializeField] Text multiplierText;
        [SerializeField] Image comboFill;
        [SerializeField] GameObject comboGroup;
        [SerializeField] Text timeText;
        [SerializeField] Text snowflakeText;
        [SerializeField] Text powerUpText;
        [SerializeField] Text levelText;
        [SerializeField] Text popupText;
        [SerializeField] Text countdownText;
        [SerializeField] Text weatherText;
        [SerializeField] WeatherController weather;

        [Header("Panels")]
        [SerializeField] GameObject pausePanel;
        [SerializeField] Button resumeButton, pauseRestartButton, pauseMenuButton;
        [SerializeField] GameObject gameOverPanel;
        [SerializeField] Text gameOverText;
        [SerializeField] Button gameOverRestartButton, gameOverMenuButton;
        [SerializeField] GameObject completePanel;
        [SerializeField] Text completeTitle;
        [SerializeField] Text completeText;
        [SerializeField] Text starsText;
        [SerializeField] Image[] starImages;
        [SerializeField] Sprite starFull, starEmpty;
        [SerializeField] Button nextButton, completeRestartButton, completeMenuButton;

        GameManager gm;
        float popupTimer, popupDuration;
        Color popupColor;
        float countdownTimer;
        int shownScore;

        void Start()
        {
            gm = GameManager.Instance;
            pausePanel.SetActive(false);
            gameOverPanel.SetActive(false);
            completePanel.SetActive(false);
            popupText.text = "";

            Wire(resumeButton, () => gm.Resume());
            Wire(pauseRestartButton, () => gm.Restart());
            Wire(pauseMenuButton, () => gm.MainMenu());
            Wire(gameOverRestartButton, () => gm.Restart());
            Wire(gameOverMenuButton, () => gm.MainMenu());
            Wire(nextButton, () => gm.NextLevel());
            Wire(completeRestartButton, () => gm.Restart());
            Wire(completeMenuButton, () => gm.MainMenu());

            AudioManager.MuteChanged += ShowMutePopup;

            var level = LevelCatalog.Levels[gm.LevelIndex];
            string mode = gm.Mode == GameMode.TimeTrial ? "TIME TRIAL" : "ADVENTURE";
            levelText.text = $"{level.DisplayName.ToUpper()}  ·  {mode}  ·  {CharacterData.Current.Name}";
            livesGroup.SetActive(gm.Mode == GameMode.Adventure);
        }

        static void Wire(Button b, UnityEngine.Events.UnityAction action)
        {
            if (!b) return;
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(() => AudioManager.PlaySfx(Sfx.Click));
            b.onClick.AddListener(action);
        }

        void OnDestroy() => AudioManager.MuteChanged -= ShowMutePopup;

        void ShowMutePopup(bool muted) =>
            ShowPopup(muted ? "SOUND OFF  (M)" : "SOUND ON  (M)", new Color(0.7f, 0.9f, 1f), 1f);

        void Update()
        {
            if (!gm) return;
            var p = gm.Player;

            // score counts up smoothly
            shownScore = (int)Mathf.MoveTowards(shownScore, gm.Score, Mathf.Max(20f, Mathf.Abs(gm.Score - shownScore) * 8f) * UiDelta);
            scoreText.text = shownScore.ToString("N0");

            float kmh = p.SpeedKmh;
            speedText.text = $"{kmh:0} km/h";
            speedFill.fillAmount = Mathf.Clamp01(p.Speed / (p.Stats.MaxSpeed * 1.25f));
            speedFill.color = p.IsBoosting ? new Color(1f, 0.55f, 0.15f) : Color.Lerp(new Color(0.4f, 0.85f, 1f), new Color(1f, 0.35f, 0.35f), speedFill.fillAmount);

            for (int i = 0; i < hearts.Length; i++)
            {
                hearts[i].enabled = i < gm.Lives;
            }

            bool combo = gm.Combo > 0;
            comboGroup.SetActive(combo);
            if (combo)
            {
                multiplierText.text = $"x{gm.Multiplier:0.0}  COMBO {gm.Combo}";
                comboFill.fillAmount = gm.ComboTimeLeft / gm.ComboWindow;
            }

            float t = gm.DisplayTime;
            string time = $"{(int)(t / 60)}:{t % 60f:00.00}";
            if (gm.Mode == GameMode.TimeTrial)
            {
                float best = GameSettings.GetBestTime(gm.LevelIndex);
                float target = LevelCatalog.Levels[gm.LevelIndex].TargetTime;
                time += best > 0 ? $"\n<size=20>BEST {best:0.00}s · GOLD {target:0}s</size>" : $"\n<size=20>GOLD {target:0}s</size>";
            }
            timeText.text = time;
            snowflakeText.text = $"{gm.Snowflakes}/{gm.TotalSnowflakes}";

            // active power-ups
            string pu = "";
            if (p.BoostTimeLeft > 0) pu += $"<color=#FFA040>BOOST {p.BoostTimeLeft:0.0}s</color>\n";
            if (p.InvincibleTimeLeft > 0) pu += $"<color=#66B3FF>SHIELD {p.InvincibleTimeLeft:0.0}s</color>\n";
            if (p.MagnetTimeLeft > 0) pu += $"<color=#C080FF>MAGNET {p.MagnetTimeLeft:0.0}s</color>\n";
            if (!p.IsTouchingGround && p.AirTime > 0.4f && gm.State == GameState.Playing) pu += $"AIR {p.AirTime:0.0}s\n";
            powerUpText.text = pu;

            if (weatherText)
            {
                weatherText.text = weather && weather.enabled && weather.Gust > 0.6f ? "WHITEOUT! visibility low" : "";
            }

            // popup animation
            if (popupTimer > 0f)
            {
                popupTimer = Mathf.Max(0f, popupTimer - UiDelta);
                float k = 1f - popupTimer / popupDuration;
                float scale = k < 0.15f ? Mathf.Lerp(0.5f, 1.15f, k / 0.15f) : Mathf.Lerp(1.15f, 1f, (k - 0.15f) * 4f);
                popupText.transform.localScale = Vector3.one * scale;
                var c = popupColor;
                c.a = popupTimer < 0.3f ? popupTimer / 0.3f : 1f;
                popupText.color = c;
                if (popupTimer <= 0f) popupText.text = "";
            }

            if (countdownTimer > 0f)
            {
                countdownTimer = Mathf.Max(0f, countdownTimer - UiDelta);
                countdownText.transform.localScale = Vector3.one * (1f + countdownTimer * 0.8f);
            }
        }

        // unscaled (works while paused) but clamped so editor hitches don't skip animations
        static float UiDelta => Mathf.Min(Time.unscaledDeltaTime, 0.1f);

        public void ShowPopup(string text, Color color, float duration)
        {
            popupText.text = text;
            popupColor = color;
            popupTimer = popupDuration = duration;
        }

        public void ShowCountdown(string text)
        {
            countdownText.text = text;
            countdownTimer = 0.5f;
        }

        public void ShowPause(bool show)
        {
            pausePanel.SetActive(show);
            if (show) resumeButton.Select();
        }

        public void ShowGameOver(int score, bool newBest)
        {
            gameOverPanel.SetActive(true);
            gameOverText.text = $"Score: {score:N0}" + (newBest ? "\n<color=#FFD54A>NEW BEST!</color>" : $"\nBest: {GameSettings.GetBestScore(gm.LevelIndex):N0}");
            gameOverRestartButton.Select();
        }

        public void ShowLevelComplete(LevelResult r)
        {
            completePanel.SetActive(true);
            completeTitle.text = r.Mode == GameMode.TimeTrial ? "TIME TRIAL COMPLETE" : "MOUNTAIN CLEARED!";
            if (starImages != null && starImages.Length > 0)
            {
                starsText.text = "";
                for (int i = 0; i < starImages.Length; i++) starImages[i].sprite = i < r.Stars ? starFull : starEmpty;
            }
            else starsText.text = new string('★', r.Stars) + "<color=#FFFFFF40>" + new string('★', 3 - r.Stars) + "</color>";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Time: {r.Time:0.00}s" + (r.NewBestTime ? "  <color=#FFD54A>NEW RECORD!</color>" : ""));
            sb.AppendLine($"Snowflakes: {r.Snowflakes}/{r.TotalSnowflakes}");
            sb.AppendLine($"Tricks landed: {r.Tricks}   Best combo: {r.BestCombo}");
            if (r.LivesBonus > 0) sb.AppendLine($"Lives bonus: +{r.LivesBonus:N0}");
            if (r.TimeBonus > 0) sb.AppendLine($"Time bonus: +{r.TimeBonus:N0}");
            if (r.FlakeBonus > 0) sb.AppendLine($"All snowflakes bonus: +{r.FlakeBonus:N0}");
            sb.AppendLine();
            sb.Append($"<size=40><b>SCORE {r.Score:N0}</b></size>" + (r.NewBestScore ? "\n<color=#FFD54A>NEW HIGH SCORE!</color>" : ""));
            completeText.text = sb.ToString();

            nextButton.gameObject.SetActive(r.HasNextLevel);
            (r.HasNextLevel ? nextButton : completeRestartButton).Select();
        }
    }
}
