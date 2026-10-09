namespace SnowBoarder
{
    public class LevelInfo
    {
        public string SceneName;
        public string DisplayName;
        public string Difficulty;
        public float TargetTime; // time trial "gold" time in seconds
    }

    public static class LevelCatalog
    {
        public const string MainMenuScene = "MainMenu";

        public static readonly LevelInfo[] Levels =
        {
            new LevelInfo { SceneName = "Level1", DisplayName = "Bunny Hill",    Difficulty = "Easy",   TargetTime = 45f },
            new LevelInfo { SceneName = "Level2", DisplayName = "Pine Ridge",    Difficulty = "Medium", TargetTime = 60f },
            new LevelInfo { SceneName = "Level3", DisplayName = "Blizzard Peak", Difficulty = "Hard",   TargetTime = 75f },
        };
    }
}
