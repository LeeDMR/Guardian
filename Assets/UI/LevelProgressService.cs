using UnityEngine;

namespace Guardian.UI
{
    /// <summary>
    /// Stores level completion state in PlayerPrefs.
    /// One key per level: LVL_Completed_{levelId} = 1
    /// </summary>
    public static class LevelProgressService
    {
        private const string Prefix = "LVL_Completed_";

        private static string Key(string levelId)
        {
            if (string.IsNullOrWhiteSpace(levelId)) levelId = "Level";
            // Avoid accidental whitespace in keys.
            levelId = levelId.Trim();
            return Prefix + levelId;
        }

        public static bool IsCompleted(string levelId)
        {
            return PlayerPrefs.GetInt(Key(levelId), 0) == 1;
        }

        public static void MarkCompleted(string levelId)
        {
            PlayerPrefs.SetInt(Key(levelId), 1);
            PlayerPrefs.Save();
        }

        public static void ClearCompleted(string levelId)
        {
            PlayerPrefs.DeleteKey(Key(levelId));
            PlayerPrefs.Save();
        }
    }
}
