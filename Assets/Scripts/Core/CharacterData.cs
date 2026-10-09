using UnityEngine;

namespace SnowBoarder
{
    /// <summary>
    /// Playable snowboarders. Each one has different stats so the choice matters.
    /// </summary>
    public class CharacterData
    {
        public string Name;
        public string Description;
        public Color JacketTint;
        public float Acceleration;   // push force along the slope
        public float MaxSpeed;       // m/s
        public float SpinSpeed;      // deg/s spin rate in the air
        public float JumpForce;
        public float Grip;           // how quickly the board aligns to the slope

        public static readonly CharacterData[] All =
        {
            new CharacterData
            {
                Name = "Barry", Description = "All-rounder. Easy to control.",
                JacketTint = Color.white,
                Acceleration = 14f, MaxSpeed = 18f, SpinSpeed = 340f, JumpForce = 9f, Grip = 1f
            },
            new CharacterData
            {
                Name = "Speedy Sue", Description = "Very fast, but spins slowly.",
                JacketTint = new Color(1f, 0.55f, 0.75f),
                Acceleration = 18f, MaxSpeed = 22f, SpinSpeed = 270f, JumpForce = 8.5f, Grip = 0.8f
            },
            new CharacterData
            {
                Name = "Flippy Finn", Description = "Trick master. Big air, quick spins.",
                JacketTint = new Color(0.55f, 1f, 0.6f),
                Acceleration = 12f, MaxSpeed = 16.5f, SpinSpeed = 430f, JumpForce = 11f, Grip = 1.2f
            },
        };

        public static readonly Color[] BoardColors =
        {
            Color.white,                       // original cyan board
            new Color(1f, 0.55f, 0.55f),       // red
            new Color(1f, 0.9f, 0.4f),         // gold
            new Color(0.75f, 0.6f, 1f),        // purple
            new Color(0.5f, 0.5f, 0.55f),      // stealth
        };

        public static readonly string[] BoardNames = { "Glacier", "Lava", "Gold", "Galaxy", "Stealth" };

        public static CharacterData Current => All[GameSettings.CharacterIndex];
    }
}
