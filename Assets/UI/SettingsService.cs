using UnityEngine;

namespace Guardian.UI
{
    public static class SettingsService
    {
        // PlayerPrefs keys
        private const string K_Master = "SET_Master";
        private const string K_Music = "SET_Music";
        private const string K_Sfx = "SET_Sfx";

        private const string K_Fullscreen = "SET_Fullscreen";
        private const string K_Resolution = "SET_Resolution"; // index
        private const string K_Quality = "SET_Quality";    // index
        private const string K_VSync = "SET_VSync";      // 0/1
        private const string K_FpsCap = "SET_FpsCap";     // 0=unlimited,30,60,120

        private const string K_UIScale = "SET_UIScale";    // 0.8..1.3

        // Defaults
        public static float Master => PlayerPrefs.GetFloat(K_Master, 1.0f);
        public static float Music => PlayerPrefs.GetFloat(K_Music, 1.0f);
        public static float Sfx => PlayerPrefs.GetFloat(K_Sfx, 1.0f);

        public static bool Fullscreen => PlayerPrefs.GetInt(K_Fullscreen, 1) == 1;
        public static int ResolutionIndex => PlayerPrefs.GetInt(K_Resolution, -1); // -1 = current
        public static int QualityIndex => PlayerPrefs.GetInt(K_Quality, QualitySettings.GetQualityLevel());
        public static bool VSync => PlayerPrefs.GetInt(K_VSync, 1) == 1;

        public static int FpsCap => PlayerPrefs.GetInt(K_FpsCap, 60);
        public static float UIScale => PlayerPrefs.GetFloat(K_UIScale, 1.0f);

        public static void SetMaster(float v) { PlayerPrefs.SetFloat(K_Master, Mathf.Clamp01(v)); }
        public static void SetMusic(float v) { PlayerPrefs.SetFloat(K_Music, Mathf.Clamp01(v)); }
        public static void SetSfx(float v) { PlayerPrefs.SetFloat(K_Sfx, Mathf.Clamp01(v)); }

        public static void SetFullscreen(bool v) { PlayerPrefs.SetInt(K_Fullscreen, v ? 1 : 0); }
        public static void SetResolutionIndex(int idx) { PlayerPrefs.SetInt(K_Resolution, idx); }
        public static void SetQualityIndex(int idx) { PlayerPrefs.SetInt(K_Quality, idx); }
        public static void SetVSync(bool v) { PlayerPrefs.SetInt(K_VSync, v ? 1 : 0); }

        public static void SetFpsCap(int v) { PlayerPrefs.SetInt(K_FpsCap, v); }
        public static void SetUIScale(float v) { PlayerPrefs.SetFloat(K_UIScale, Mathf.Clamp(v, 0.8f, 1.3f)); }

        public static void Save() => PlayerPrefs.Save();

        // Apply to Unity runtime
        public static void ApplyAll()
        {
            ApplyAudio();
            ApplyGraphics();
            ApplyUI();
        }

        // Аудио: без AudioMixer используем AudioListener.volume как master.
        // Music/SFX — обычно через AudioMixer, но если его нет, мы просто сохраняем значения (для будущего).
        public static void ApplyAudio()
        {
            AudioListener.volume = Master; // глобальный master
        }

        public static void ApplyGraphics()
        {
            // Quality
            int q = Mathf.Clamp(QualityIndex, 0, QualitySettings.names.Length - 1);
            QualitySettings.SetQualityLevel(q, true);

            // VSync
            QualitySettings.vSyncCount = VSync ? 1 : 0;

            // FPS cap
            int cap = FpsCap;
            Application.targetFrameRate = (cap <= 0) ? -1 : cap;

            // Fullscreen + Resolution
            Screen.fullScreen = Fullscreen;

            var res = Screen.resolutions;
            int idx = ResolutionIndex;

            if (idx == -1 || res == null || res.Length == 0)
            {
                // оставить текущую
                return;
            }

            idx = Mathf.Clamp(idx, 0, res.Length - 1);
            var r = res[idx];
            Screen.SetResolution(r.width, r.height, Screen.fullScreenMode, r.refreshRateRatio);
        }

        // UI scale: применяем через scale корня UI Toolkit (в Bridge)
        // Здесь оставим просто как хранилище, ApplyUI делается из Bridge.
        public static void ApplyUI()
        {
            // nothing here, will be applied in UI Toolkit bridge (rootVisualElement.scale)
        }

        public static void ResetToDefaults()
        {
            PlayerPrefs.DeleteKey(K_Master);
            PlayerPrefs.DeleteKey(K_Music);
            PlayerPrefs.DeleteKey(K_Sfx);

            PlayerPrefs.DeleteKey(K_Fullscreen);
            PlayerPrefs.DeleteKey(K_Resolution);
            PlayerPrefs.DeleteKey(K_Quality);
            PlayerPrefs.DeleteKey(K_VSync);
            PlayerPrefs.DeleteKey(K_FpsCap);

            PlayerPrefs.DeleteKey(K_UIScale);
            Save();
        }
    }
}
