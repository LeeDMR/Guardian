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
        [Tooltip("То, что игрок видит при вскрытии (маска/обложка)")]
        public CardDefinition cardDefinition;

        [Tooltip("Истинная сущность (для демона укажи Demon_..., для жителей можно оставить пустым)")]
        public CardDefinition trueDefinition;

        [TextArea(2, 4)]
        [Tooltip("Текст, который карта скажет при использовании способности")]
        public string statementText;

        public bool isDemon;
    }
}
