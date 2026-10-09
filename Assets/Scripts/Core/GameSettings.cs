using UnityEngine;

namespace SnowBoarder
{
    public enum GameMode { Adventure = 0, TimeTrial = 1 }

    /// <summary>
    /// Persistent player settings and progress (stored in PlayerPrefs).
    /// </summary>
    public static class GameSettings
    {
        const string KeyMaster = "sb_master_volume";
        const string KeySfx = "sb_sfx_volume";
        const string KeyMusic = "sb_music_volume";
        const string KeyWeather = "sb_weather";
        const string KeyShake = "sb_shake";
        const string KeyCharacter = "sb_character";
        const string KeyBoard = "sb_board";
        const string KeyMode = "sb_mode";
        const string KeyLevel = "sb_level";

        static bool muted;

        public static float MasterVolume
        {
            get => PlayerPrefs.GetFloat(KeyMaster, 0.8f);
            set { PlayerPrefs.SetFloat(KeyMaster, value); ApplyVolume(); }
        }

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(KeySfx, 1f);
            set => PlayerPrefs.SetFloat(KeySfx, value);
        }

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(KeyMusic, 0.6f);
            set => PlayerPrefs.SetFloat(KeyMusic, value);
        }

        /// <summary>
        /// Mute toggle (M key). Session only on purpose: a mute that was left on
        /// can never make the game silent on the next launch.
        /// </summary>
        public static bool Muted
        {
            get => muted;
            set { muted = value; ApplyVolume(); }
        }

        public static void ToggleMute() => Muted = !Muted;

        /// <summary>Pushes the master volume to the audio listener, or silence while muted.</summary>
        public static void ApplyVolume() => AudioListener.volume = Muted ? 0f : MasterVolume;

        public static bool WeatherEffects
        {
            get => PlayerPrefs.GetInt(KeyWeather, 1) == 1;
            set => PlayerPrefs.SetInt(KeyWeather, value ? 1 : 0);
        }

        public static bool ScreenShake
        {
            get => PlayerPrefs.GetInt(KeyShake, 1) == 1;
            set => PlayerPrefs.SetInt(KeyShake, value ? 1 : 0);
        }

        public static int CharacterIndex
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(KeyCharacter, 0), 0, CharacterData.All.Length - 1);
            set => PlayerPrefs.SetInt(KeyCharacter, value);
        }

        public static int BoardIndex
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(KeyBoard, 0), 0, CharacterData.BoardColors.Length - 1);
            set => PlayerPrefs.SetInt(KeyBoard, value);
        }

        public static GameMode Mode
        {
            get => (GameMode)PlayerPrefs.GetInt(KeyMode, 0);
            set => PlayerPrefs.SetInt(KeyMode, (int)value);
        }

        public static int SelectedLevel
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(KeyLevel, 0), 0, LevelCatalog.Levels.Length - 1);
            set => PlayerPrefs.SetInt(KeyLevel, value);
        }

        public static int GetBestScore(int level) => PlayerPrefs.GetInt("sb_best_score_" + level, 0);

        public static bool TrySetBestScore(int level, int score)
        {
            if (score <= GetBestScore(level)) return false;
            PlayerPrefs.SetInt("sb_best_score_" + level, score);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>Best time trial time in seconds, or 0 if none recorded.</summary>
        public static float GetBestTime(int level) => PlayerPrefs.GetFloat("sb_best_time_" + level, 0f);

        public static bool TrySetBestTime(int level, float time)
        {
            float best = GetBestTime(level);
            if (best > 0f && time >= best) return false;
            PlayerPrefs.SetFloat("sb_best_time_" + level, time);
            PlayerPrefs.Save();
            return true;
        }

        public static bool IsLevelUnlocked(int level) => level == 0 || PlayerPrefs.GetInt("sb_unlocked_" + level, 0) == 1;

        public static void UnlockLevel(int level)
        {
            PlayerPrefs.SetInt("sb_unlocked_" + level, 1);
            PlayerPrefs.Save();
        }

        public static void Save() => PlayerPrefs.Save();
    }
}
