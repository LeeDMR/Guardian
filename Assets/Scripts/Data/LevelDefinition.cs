using System;
using UnityEngine;

namespace Guardian.Data
{
    [CreateAssetMenu(
        fileName = "LevelDefinition",
        menuName = "Guardian/Level Definition",
        order = 1)]
    public class LevelDefinition : ScriptableObject
    {
        [Header("General")]
        public string levelId = "Level_1";
        public int playerLives = 3;

        [Tooltip("\r\nHow many demons do you need to kill to win?")]
        public int demonsToKill = 2;

        [Header("Cards on board (From left to right)")]
        public LevelCardEntry[] cards;
    }

    [Serializable]
    public class LevelCardEntry
    {
        [Tooltip("What type of card is in this slot?")]
        public CardDefinition cardDefinition;

        [TextArea(2, 4)]
        [Tooltip("The final hint text that the player will see")]
        public string statementText;

        [Tooltip("Is this card a demon in the layout?")]
        public bool isDemon;
    }
}
