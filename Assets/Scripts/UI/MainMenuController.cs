using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SnowBoarder
{
    /// <summary>
    /// Main menu: Start (character / board / mode / mountain selection), Options, How to play, Quit.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] GameObject mainPanel;
        [SerializeField] GameObject playPanel;
        [SerializeField] GameObject optionsPanel;
        [SerializeField] GameObject helpPanel;

        [Header("Main")]
        [SerializeField] Button startButton, optionsButton, helpButton, quitButton;

        [Header("Play setup")]
        [SerializeField] Text characterText;
        [SerializeField] Button characterPrev, characterNext;
        [SerializeField] SpriteRenderer previewTop, previewBottom;
        [SerializeField] Text boardText;
        [SerializeField] Button boardPrev, boardNext;
        [SerializeField] Text modeText;
        [SerializeField] Button modeButton;
        [SerializeField] Button[] levelButtons;
        [SerializeField] Text[] levelLabels;
        [SerializeField] Button goButton, playBackButton;

        [Header("Options")]
        [SerializeField] Slider masterSlider, sfxSlider, musicSlider;
        [SerializeField] Toggle weatherToggle, shakeToggle;
        [SerializeField] Button resetButton, optionsBackButton;

        [Header("Help")]
        [SerializeField] Button helpBackButton;

        void Start()
        {
            Time.timeScale = 1f;
            GameSettings.ApplyVolume();
            UiButtonSfx.AttachAll(transform); // hover cue for every button, slider and toggle

            Wire(startButton, () => Show(playPanel));
            Wire(optionsButton, () => Show(optionsPanel));
            Wire(helpButton, () => Show(helpPanel));
            Wire(quitButton, Quit);

            Wire(characterPrev, () => { GameSettings.CharacterIndex = Wrap(GameSettings.CharacterIndex - 1, CharacterData.All.Length); RefreshPlay(); });
            Wire(characterNext, () => { GameSettings.CharacterIndex = Wrap(GameSettings.CharacterIndex + 1, CharacterData.All.Length); RefreshPlay(); });
            Wire(boardPrev, () => { GameSettings.BoardIndex = Wrap(GameSettings.BoardIndex - 1, CharacterData.BoardColors.Length); RefreshPlay(); });
            Wire(boardNext, () => { GameSettings.BoardIndex = Wrap(GameSettings.BoardIndex + 1, CharacterData.BoardColors.Length); RefreshPlay(); });
            Wire(modeButton, () =>
            {
                GameSettings.Mode = GameSettings.Mode == GameMode.Adventure ? GameMode.TimeTrial : GameMode.Adventure;
                RefreshPlay();
            });
            for (int i = 0; i < levelButtons.Length; i++)
            {
                int idx = i;
                Wire(levelButtons[i], () => { GameSettings.SelectedLevel = idx; RefreshPlay(); });
            }
            Wire(goButton, StartGame);
            Wire(playBackButton, () => Show(mainPanel));

            masterSlider.value = GameSettings.MasterVolume;
            sfxSlider.value = GameSettings.SfxVolume;
            weatherToggle.isOn = GameSettings.WeatherEffects;
            shakeToggle.isOn = GameSettings.ScreenShake;
            masterSlider.onValueChanged.AddListener(v => GameSettings.MasterVolume = v);
            sfxSlider.onValueChanged.AddListener(v => GameSettings.SfxVolume = v);
            weatherToggle.onValueChanged.AddListener(v => GameSettings.WeatherEffects = v);
            shakeToggle.onValueChanged.AddListener(v => GameSettings.ScreenShake = v);
            if (musicSlider)
            {
                musicSlider.value = GameSettings.MusicVolume;
                musicSlider.onValueChanged.AddListener(v => GameSettings.MusicVolume = v);
            }
            Wire(resetButton, () =>
            {
                for (int i = 0; i < LevelCatalog.Levels.Length; i++)
                {
                    PlayerPrefs.DeleteKey("sb_best_score_" + i);
                    PlayerPrefs.DeleteKey("sb_best_time_" + i);
                    PlayerPrefs.DeleteKey("sb_unlocked_" + i);
                }
                GameSettings.SelectedLevel = 0;
                GameSettings.Save();
                RefreshPlay();
            });
            Wire(optionsBackButton, () => { GameSettings.Save(); Show(mainPanel); });
            Wire(helpBackButton, () => Show(mainPanel));

            Show(mainPanel);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && !mainPanel.activeSelf) Show(mainPanel);

            // little idle animation for the preview boarder
            if (previewTop)
            {
                var root = previewTop.transform.parent;
                root.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 1.5f) * 6f);
            }
        }

        static int Wrap(int v, int n) => (v % n + n) % n;

        static void Wire(Button b, UnityEngine.Events.UnityAction action)
        {
            if (!b) return;
            b.onClick.AddListener(() => AudioManager.PlaySfx(Sfx.Click));
            b.onClick.AddListener(action);
        }

        void Show(GameObject panel)
        {
            mainPanel.SetActive(panel == mainPanel);
            playPanel.SetActive(panel == playPanel);
            optionsPanel.SetActive(panel == optionsPanel);
            helpPanel.SetActive(panel == helpPanel);
            if (previewTop) previewTop.transform.parent.gameObject.SetActive(panel == playPanel);

            if (panel == playPanel) { RefreshPlay(); goButton.Select(); }
            else if (panel == optionsPanel) masterSlider.Select();
            else if (panel == helpPanel) helpBackButton.Select();
            else startButton.Select();
        }

        void RefreshPlay()
        {
            var c = CharacterData.Current;
            characterText.text = $"<b>{c.Name}</b>\n<size=18>{c.Description}</size>\n<size=16>" +
                                 $"Speed {Bar(c.MaxSpeed, 16.5f, 22)}  Spin {Bar(c.SpinSpeed, 270, 430)}  Jump {Bar(c.JumpForce, 8.5f, 11)}</size>";
            boardText.text = $"Board: <b>{CharacterData.BoardNames[GameSettings.BoardIndex]}</b>";
            if (previewTop) previewTop.color = c.JacketTint;
            if (previewBottom) previewBottom.color = CharacterData.BoardColors[GameSettings.BoardIndex];

            modeText.text = GameSettings.Mode == GameMode.Adventure
                ? "Mode: <b>ADVENTURE</b>\n<size=16>3 lives · score with tricks & combos</size>"
                : "Mode: <b>TIME TRIAL</b>\n<size=16>Unlimited lives · crash = +3s · beat the clock</size>";

            // make sure the selected level is unlocked
            if (!GameSettings.IsLevelUnlocked(GameSettings.SelectedLevel)) GameSettings.SelectedLevel = 0;

            for (int i = 0; i < levelButtons.Length; i++)
            {
                var info = LevelCatalog.Levels[i];
                bool unlocked = GameSettings.IsLevelUnlocked(i);
                bool selected = GameSettings.SelectedLevel == i;
                levelButtons[i].interactable = unlocked;
                string best = GameSettings.Mode == GameMode.TimeTrial
                    ? (GameSettings.GetBestTime(i) > 0 ? $"Best {GameSettings.GetBestTime(i):0.00}s" : "No time yet")
                    : (GameSettings.GetBestScore(i) > 0 ? $"Best {GameSettings.GetBestScore(i):N0}" : "No score yet");
                levelLabels[i].text = unlocked
                    ? $"<b>{info.DisplayName}</b>\n<size=16>{info.Difficulty} · {best}</size>"
                    : $"<b>LOCKED</b>\n<size=16>Finish previous mountain</size>";
                var colors = levelButtons[i].colors;
                colors.normalColor = selected ? new Color(1f, 0.82f, 0.3f) : Color.white;
                colors.selectedColor = selected ? new Color(1f, 0.85f, 0.4f) : new Color(0.9f, 0.95f, 1f);
                levelButtons[i].colors = colors;
            }
        }

        static string Bar(float v, float min, float max)
        {
            int n = 1 + Mathf.RoundToInt(Mathf.InverseLerp(min, max, v) * 4f);
            return new string('■', n) + "<color=#FFFFFF30>" + new string('■', 5 - n) + "</color>";
        }

        void StartGame()
        {
            GameSettings.Save();
            SceneManager.LoadScene(LevelCatalog.Levels[GameSettings.SelectedLevel].SceneName);
        }

        void Quit()
        {
            GameSettings.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
