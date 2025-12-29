using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Guardian.Data;
using Guardian.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Guardian.Game
{
    public class BoardUIToolkitBridge : MonoBehaviour
    {
        [SerializeField] private UIDocument doc;
        [SerializeField] private BoardManager board;
        [SerializeField] private VisualTreeAsset cardTileAsset;
        [SerializeField] private VisualTreeAsset guardianHudAsset;

        [Header("Cards Grid Auto-Fit (UI Toolkit)")]
        [Tooltip("Сколько рядов предпочитаем на доске. Например: 2 для 6 карт (получится 3x2).")]
        [SerializeField, Min(1)] private int preferredRows = 2;

        [Tooltip("Если > 0, то используем фиксированное число колонок (rows посчитаются автоматически).")]
        [SerializeField, Min(0)] private int preferredColumns = 0;

        [Tooltip("Padding в .cards-grid (должен совпадать с CardTile.uss).")]
        [SerializeField] private float gridPadding = 28f;

        [Tooltip("Gap между карточками в .cards-grid (должен совпадать с CardTile.uss).")]
        [SerializeField] private float gridGap = 24f;

        [Tooltip("Базовый размер карточки из USS. Мы масштабируем относительно него.")]
        [SerializeField] private Vector2 referenceCardSize = new Vector2(240f, 340f);

        [Tooltip("Разрешить увеличивать карточки больше базового размера (обычно лучше выключить).")]
        [SerializeField] private bool allowUpscale = false;


        private VisualElement cardsGrid;
        private VisualElement boardArea;
        private readonly List<VisualElement> cardRoots = new();
        // Cached info about what coordinate convention RuntimePanelUtils.ScreenToPanel expects.
        // Some Unity/UITK setups treat screen Y as top-left (0 at top), while UGUI uses bottom-left.
        // We detect it once per panel and convert accordingly when positioning UGUI SpeechBubble.
        private IPanel originPanel;
        private bool originComputed;
        private bool screenToPanelIsBottomLeft;
        private CardView[] cards;

        private Label livesValue;
        private Label demonsValue;
        private Button killButton;

        private VisualElement detailsPanel;
        private VisualElement portrait;
        private Label cardTitle;
        private Label cardDesc;
        private Button abilityButton;

        private CardView current;
        private VisualElement root;

        // --- Main Menu (UI Toolkit) ---
        private VisualElement gameHud;
        private VisualElement menuOverlay;
        private VisualElement levelSelectOverlay;
        private VisualElement settingsOverlay;

        private VisualElement winOverlay;
        private VisualElement loseOverlay;

        private Button winRestartButton;
        private Button winMenuButton;
        private Button loseRestartButton;
        private Button loseMenuButton;

        private Button playButton;
        private Button settingsButton;
        private Button exitButton;
        private Button levelsBackButton;
        private Button settingsBackButton;
        private VisualElement levelsList;
        private SliderInt musicSlider;
        private SliderInt sfxSlider;

        private bool initialized;

        private void Awake()
        {
            // UIDocument clones the VisualTreeAsset in OnEnable().
            if (!doc) doc = GetComponent<UIDocument>();
            if (!board) board = FindAnyObjectByType<BoardManager>();
        }

        private void OnEnable()
        {
            // Avoid NullReference in Awake: visual tree may not be cloned yet.
            StartCoroutine(InitWhenReady());
            SettingsService.ApplyAll();
        }

        private IEnumerator InitWhenReady()
        {
            // Wait a few frames for UITK to finish cloning the tree (especially in Editor).
            for (int i = 0; i < 60; i++)
            {
                if (TryInitUI())
                    yield break;

                yield return null;
            }

            Debug.LogError("[BoardUIToolkitBridge] UI tree did not initialize. Check UIDocument VisualTreeAsset and UXML element names.");
        }

        private bool TryInitUI()
        {
            if (initialized) return true;
            if (!doc) return false;

            root = doc.rootVisualElement;
            if (root == null) return false;

            // UIDocument normally clones the VisualTreeAsset into rootVisualElement in OnEnable().
            // However, in some Editor/domain-reload setups the tree may still be empty here.
            // If it's empty but we have a VisualTreeAsset assigned, clone it ourselves.
            if (root.childCount == 0)
            {
                var vta = doc.visualTreeAsset != null ? doc.visualTreeAsset : guardianHudAsset;
                if (vta != null)
                    vta.CloneTree(root);
            }

            // всё ещё пусто? значит не назначен ни Source Asset, ни guardianHudAsset
            if (root.childCount == 0)
                return false;

            if (root.childCount == 0) return false;
            // Main menu elements
            gameHud = root.Q<VisualElement>("GameHUD");
            menuOverlay = root.Q<VisualElement>("MenuOverlay");
            levelSelectOverlay = root.Q<VisualElement>("LevelSelectOverlay");
            settingsOverlay = root.Q<VisualElement>("SettingsOverlay");
            winOverlay = root.Q<VisualElement>("WinOverlay");
            loseOverlay = root.Q<VisualElement>("LoseOverlay");

            // If the UXML hasn't been cloned into the panel yet, retry next frame.
            if (gameHud == null && menuOverlay == null)
                return false;

            playButton = root.Q<Button>("PlayButton");
            settingsButton = root.Q<Button>("SettingsButton");
            exitButton = root.Q<Button>("ExitButton");
            levelsBackButton = root.Q<Button>("LevelsBackButton");
            settingsBackButton = root.Q<Button>("SettingsBackButton");
            winRestartButton = root.Q<Button>("WinRestartButton");
            winMenuButton = root.Q<Button>("WinMenuButton");
            loseRestartButton = root.Q<Button>("LoseRestartButton");
            loseMenuButton = root.Q<Button>("LoseMenuButton");
            levelsList = root.Q<VisualElement>("LevelsList");
            musicSlider = root.Q<SliderInt>("MusicSlider");
            sfxSlider = root.Q<SliderInt>("SfxSlider");

            cardsGrid = root.Q<VisualElement>("CardsGrid");
            boardArea = root.Q<VisualElement>("BoardArea");

            if (boardArea != null)
                boardArea.RegisterCallback<GeometryChangedEvent>(_ => RefitCardsGrid());

            livesValue = root.Q<Label>("LivesValue");
            demonsValue = root.Q<Label>("DemonsValue");
            killButton = root.Q<Button>("KillButton");

            detailsPanel = root.Q<VisualElement>("DetailsPanel");
            portrait = root.Q<VisualElement>("Portrait");
            cardTitle = root.Q<Label>("CardTitle");
            cardDesc = root.Q<Label>("CardDesc");
            abilityButton = root.Q<Button>("AbilityButton");

            // UI events (guard against missing elements)
            if (killButton != null && board != null)
            {
                killButton.clicked -= OnKillClicked;
                killButton.clicked += OnKillClicked;
            }

            if (abilityButton != null && board != null)
            {
                abilityButton.clicked -= OnAbilityClicked;
                abilityButton.clicked += OnAbilityClicked;
            }

            SetSelected(null);

            // Wire menu events last (so doc/root exists and board ref is valid)
            HookMenuEvents();
            ShowMainMenu();

            initialized = true;
            return true;
        }

        private void OnKillClicked()
        {
            board?.OnKillButtonPressed();
        }

        private void OnAbilityClicked()
        {
            if (current != null) board?.UseAbility(current);
            RefreshSelected();
        }

        private void HookMenuEvents()
        {
            if (playButton != null)
                playButton.clicked += ShowLevelSelect;

            if (settingsButton != null)
                settingsButton.clicked += ShowSettings;

            if (exitButton != null)
                exitButton.clicked += QuitGame;

            if (levelsBackButton != null)
                levelsBackButton.clicked += ShowMainMenu;

            if (settingsBackButton != null)
                settingsBackButton.clicked += ShowMainMenu;

            // Win/Lose overlays
            if (winRestartButton != null)
                winRestartButton.clicked += RestartCurrentLevel;
            if (loseRestartButton != null)
                loseRestartButton.clicked += RestartCurrentLevel;

            if (winMenuButton != null)
                winMenuButton.clicked += () => { HideOutcomeOverlays(); ShowMainMenu(); };
            if (loseMenuButton != null)
                loseMenuButton.clicked += () => { HideOutcomeOverlays(); ShowMainMenu(); };

            // Settings UI: populate from PlayerPrefs and wire callbacks
            PopulateSettingsUI(root);
            WireSettingsCallbacks(root);
            ApplyUiScaleToRoot(root);

            WireSettingsTabs(root);

            BuildLevelsList();
        }

        private void ShowMainMenu()
        {
            HideOutcomeOverlays();
            BuildLevelsList();
            // Hide gameplay HUD while in menu
            if (gameHud != null)
                gameHud.style.display = DisplayStyle.None;

            if (menuOverlay != null)
                menuOverlay.RemoveFromClassList("hidden");

            if (levelSelectOverlay != null)
                levelSelectOverlay.AddToClassList("hidden");

            if (settingsOverlay != null)
                settingsOverlay.AddToClassList("hidden");
        }

        private void ShowLevelSelect()
        {
            HideOutcomeOverlays();
            BuildLevelsList();
            if (menuOverlay != null)
                menuOverlay.AddToClassList("hidden");
            if (settingsOverlay != null)
                settingsOverlay.AddToClassList("hidden");
            if (levelSelectOverlay != null)
                levelSelectOverlay.RemoveFromClassList("hidden");
        }

        private void ShowSettings()
        {
            HideOutcomeOverlays();
            if (menuOverlay != null)
                menuOverlay.AddToClassList("hidden");
            if (levelSelectOverlay != null)
                levelSelectOverlay.AddToClassList("hidden");
            if (settingsOverlay != null)
                settingsOverlay.RemoveFromClassList("hidden");
        }

        private void StartSelectedLevel(LevelDefinition level)
        {
            HideOutcomeOverlays();
            if (level == null || board == null) return;

            board.StartLevel(level);

            // Show gameplay UI
            if (gameHud != null)
                gameHud.style.display = DisplayStyle.Flex;

            if (menuOverlay != null)
                menuOverlay.AddToClassList("hidden");
            if (levelSelectOverlay != null)
                levelSelectOverlay.AddToClassList("hidden");
            if (settingsOverlay != null)
                settingsOverlay.AddToClassList("hidden");
        }



        private void RestartCurrentLevel()
        {
            if (board == null) return;
            HideOutcomeOverlays();
            StartSelectedLevel(board.levelDefinition);
        }

        public void ShowVictory()
        {
            // Keep gameplay HUD visible and show a modal overlay over it
            if (gameHud != null)
                gameHud.style.display = DisplayStyle.Flex;

            if (menuOverlay != null) menuOverlay.AddToClassList("hidden");
            if (levelSelectOverlay != null) levelSelectOverlay.AddToClassList("hidden");
            if (settingsOverlay != null) settingsOverlay.AddToClassList("hidden");

            if (loseOverlay != null) loseOverlay.AddToClassList("hidden");
            if (winOverlay != null) winOverlay.RemoveFromClassList("hidden");
        }

        public void ShowDefeat()
        {
            if (gameHud != null)
                gameHud.style.display = DisplayStyle.Flex;

            if (menuOverlay != null) menuOverlay.AddToClassList("hidden");
            if (levelSelectOverlay != null) levelSelectOverlay.AddToClassList("hidden");
            if (settingsOverlay != null) settingsOverlay.AddToClassList("hidden");

            if (winOverlay != null) winOverlay.AddToClassList("hidden");
            if (loseOverlay != null) loseOverlay.RemoveFromClassList("hidden");
        }

        public void HideOutcomeOverlays()
        {
            if (winOverlay != null) winOverlay.AddToClassList("hidden");
            if (loseOverlay != null) loseOverlay.AddToClassList("hidden");
        }
        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void BuildLevelsList()
        {
            if (levelsList == null) return;

            levelsList.Clear();

            // Levels are loaded from Resources/Levels.
            var levels = Resources.LoadAll<LevelDefinition>("Levels")
                .Where(l => l != null)
                .OrderBy(l => l.name)
                .ToArray();

            if (levels.Length == 0)
            {
                var lbl = new Label("Нет уровней в Resources/Levels");
                lbl.style.color = new StyleColor(new Color(1f, 1f, 1f, 0.7f));
                levelsList.Add(lbl);
                return;
            }

            foreach (var lvl in levels)
            {
                string id = string.IsNullOrWhiteSpace(lvl.levelId) ? lvl.name : lvl.levelId;
                bool completed = LevelProgressService.IsCompleted(id);

                var btn = new Button(() => StartSelectedLevel(lvl))
                {
                    text = completed ? $"{id} (пройден)" : id
                };
                btn.AddToClassList("btn");
                btn.AddToClassList("menu-btn");
                btn.AddToClassList("primary");
                if (completed) btn.AddToClassList("completed");
                levelsList.Add(btn);
            }
        }

        public void BuildCards(CardView[] cardViews)
        {
            cards = cardViews;
            if (cardsGrid == null || cardTileAsset == null) return;

            cardsGrid.Clear();
            cardRoots.Clear();

            for (int i = 0; i < cards.Length; i++)
            {
                int idx = i;
                var ve = cardTileAsset.Instantiate();

                // В CardTile.uxml корневой элемент — Button, но мы оставляем поиск по имени для надёжности
                var btn = ve.Q<Button>("CardTile");
                if (btn == null)
                {
                    // Если структура UXML вдруг изменилась — всё равно добавим корневой VE, чтобы не сломать игру.
                    cardsGrid.Add(ve);
                    cardRoots.Add(ve);
                    continue;
                }

                btn.clicked += () => board.OnCardClicked(cards[idx]);

                cardsGrid.Add(ve);
                cardRoots.Add(btn);

                RefreshCard(idx);
            }

            // После того как карточки добавлены и контейнер получил реальные размеры — подгоним их под доску
            RefitCardsGrid();
        }

        public void RefreshCard(int i)
        {
            if (cards == null) return;
            if (i < 0 || i >= cards.Length) return;
            if (i >= cardRoots.Count) return; // <- критично

            var root = cardRoots[i];
            var cv = cards[i];

            var card = cards[i];
            var btn = cardRoots[i];

            btn.EnableInClassList("open", card.IsOpen);
            btn.EnableInClassList("dead", card.IsDead);

            btn.EnableInClassList("selected", board.SelectedCard == card);
            btn.EnableInClassList("kill", board.IsKillMode && card.IsKillSelectable);

            btn.Q<Label>("Number").text = (card.Index + 1).ToString();

            var title = btn.Q<Label>("Title");
            title.text = card.IsOpen ? card.VisibleDefinition.displayName : "???";

            var portrait = btn.Q<VisualElement>("Portrait");

            Sprite spriteToShow = null;

            if (card.IsOpen)
            {
                // Если демон мёртв — показываем истинный портрет демона
                if (card.IsDead && card.entry.isDemon)
                {
                    var truth = card.TruthDefinition;
                    spriteToShow = (truth != null)
                        ? (truth.portraitTrue != null ? truth.portraitTrue : truth.portrait)
                        : null;
                }
                else
                {
                    // Иначе (обычное раскрытие) — показываем маску (visible)
                    var vis = card.VisibleDefinition;
                    spriteToShow = vis != null ? vis.portrait : null;
                }
            }

            if (portrait != null)
            {
                if (spriteToShow != null)
                {
                    portrait.style.backgroundImage = new StyleBackground(spriteToShow);
                    portrait.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop; // красиво заполняет
                }
                else
                {
                    portrait.style.backgroundImage = StyleKeyword.None;
                }
            }
        }

        public void RefreshAllCards()
        {
            if (cards == null) return;

            int count = Mathf.Min(cards.Length, cardRoots.Count);
            for (int i = 0; i < count; i++)
                RefreshCard(i);
        }



        public void SetStats(int lives, int demons)
        {
            if (livesValue != null) livesValue.text = lives.ToString();
            if (demonsValue != null) demonsValue.text = demons.ToString();
        }

        public void SetKillMode(bool isKillMode)
        {
            if (killButton == null) return;

            if (isKillMode)
            {
                killButton.text = "Отмена";
                killButton.AddToClassList("active");
            }
            else
            {
                killButton.text = "Kill";
                killButton.RemoveFromClassList("active");
            }
        }

        public void SetSelected(CardView card)
        {
            current = card;

            if (detailsPanel == null) return;

            if (current == null || !current.IsOpen || current.IsDead)
            {
                detailsPanel.RemoveFromClassList("shown");
                return;
            }
            RefreshSelected();
            detailsPanel.AddToClassList("shown");
        }

        public void RefreshSelected()
        {
            if (current == null || current.VisibleDefinition == null) return;

            var def = current.VisibleDefinition;

            if (portrait != null)
            {
                // Unity 6 умеет StyleBackground(Sprite)
                portrait.style.backgroundImage = new StyleBackground(def.portrait);
                portrait.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            }

            if (cardTitle != null) cardTitle.text = def.displayName;
            if (cardDesc != null) cardDesc.text = def.description;

            bool canUse = current.CanUseAbility;
            if (abilityButton != null)
            {
                abilityButton.SetEnabled(canUse);
                abilityButton.text = canUse ? def.abilityButtonText : "Способность использована";
            }
        }


        private void RefitCardsGrid()
        {
            if (boardArea == null || cardsGrid == null) return;
            if (cardRoots.Count == 0) return;

            int n = cardRoots.Count;

            int cols;
            int rows;

            if (preferredColumns > 0)
            {
                cols = Mathf.Max(1, preferredColumns);
                rows = Mathf.CeilToInt(n / (float)cols);
            }
            else
            {
                rows = Mathf.Max(1, preferredRows);
                cols = Mathf.CeilToInt(n / (float)rows);
            }

            // Доступная область внутри BoardArea
            float w = boardArea.contentRect.width - gridPadding * 2f - gridGap * (cols - 1);
            float h = boardArea.contentRect.height - gridPadding * 2f - gridGap * (rows - 1);
            if (w <= 0f || h <= 0f) return;

            float cellW = w / cols;
            float cellH = h / rows;

            float scale = Mathf.Min(cellW / referenceCardSize.x, cellH / referenceCardSize.y);
            if (!allowUpscale) scale = Mathf.Min(scale, 1f);
            scale = Mathf.Max(0.1f, scale);

            float cardW = referenceCardSize.x * scale;
            float cardH = referenceCardSize.y * scale;

            for (int i = 0; i < cardRoots.Count; i++)
            {
                var card = cardRoots[i];
                card.style.width = cardW;
                card.style.height = cardH;
            }
        }

        // --- Hit tests (для BoardManager.Update, чтобы отмена режимов учитывала UI Toolkit) ---

        public bool IsPointerOverKillButton(Vector2 screenPos)
            => ContainsScreenPoint(killButton, screenPos);

        public bool IsPointerOverDetailsPanel(Vector2 screenPos)
            => ContainsScreenPoint(detailsPanel, screenPos);

        /// <summary>
        /// True if the pointer is over our UI Toolkit HUD (top bar or details panel).
        /// We keep it explicit to avoid "catching" clicks on fullscreen containers (Root/BoardArea).
        /// </summary>
        public bool IsPointerOverAnyUI(Vector2 screenPos)
        {
            if (root == null || root.panel == null) return false;

            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(root.panel, screenPos);
            var picked = root.panel.Pick(panelPos);
            return picked != null;
        }

        private bool ContainsScreenPoint(VisualElement ve, Vector2 screenPos)
        {
            if (ve == null) return false;
            var panel = doc != null ? doc.rootVisualElement?.panel : ve.panel;
            if (panel == null) return false;

            // 1) Direct bound check (fast)
            if (ContainsBound(panel, ve, screenPos)) return true;

            // 2) Try flipped Y (robust for different screen coordinate conventions)
            Vector2 flipped = new Vector2(screenPos.x, Screen.height - screenPos.y);
            if (ContainsBound(panel, ve, flipped)) return true;

            // 3) Pick fallback (counts clicks on children like the Use Ability button)
            if (IsPickedUnder(panel, ve, screenPos)) return true;
            return IsPickedUnder(panel, ve, flipped);
        }

        private static bool ContainsBound(IPanel panel, VisualElement ve, Vector2 screenPos)
        {
            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel, screenPos);
            return ve.worldBound.Contains(panelPos);
        }

        private static bool IsPickedUnder(IPanel panel, VisualElement root, Vector2 screenPos)
        {
            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(panel, screenPos);
            var picked = panel.Pick(panelPos);
            if (picked == null) return false;
            return IsDescendantOrSelf(picked, root);
        }

        private static bool IsDescendantOrSelf(VisualElement child, VisualElement parent)
        {
            for (var e = child; e != null; e = e.parent)
                if (e == parent) return true;
            return false;
        }
        public void ShowSpeechNearCard(CardView card, string message)
        {
            if (board == null || card == null) return;
            if (string.IsNullOrWhiteSpace(message)) return;

            // IMPORTANT:
            // CardView.Index is a gameplay index and can diverge from the visual order.
            // The safest anchor is to resolve the visual element index from our `cards` array.
            int visualIndex = ResolveVisualIndex(card);
            if (visualIndex < 0) return;

            if (TryGetCardBubbleAnchor(visualIndex, out var screenPoint, out var placeLeft))
            {
                board.ShowBubbleAtScreenPoint(screenPoint, placeLeft, message);
                return;
            }

            // Layout for UI Toolkit elements can update at the end of the frame. If we asked for
            // worldBound too early (rare but possible), we retry on the next UI tick.
            if (root != null)
            {
                root.schedule.Execute(() =>
                {
                    if (TryGetCardBubbleAnchor(visualIndex, out var sp, out var left))
                        board.ShowBubbleAtScreenPoint(sp, left, message);
                }).StartingIn(0);
            }
        }

        private int ResolveVisualIndex(CardView card)
        {
            if (cards != null)
            {
                for (int i = 0; i < cards.Length; i++)
                    if (ReferenceEquals(cards[i], card))
                        return i;
            }

            // Fallback for edge cases
            return card.Index;
        }

        bool TryGetCardBubbleAnchor(int index, out Vector2 screenPoint, out bool placeLeft)
        {
            screenPoint = default;
            placeLeft = false;

            if (index < 0 || index >= cardRoots.Count) return false;

            var ve = cardRoots[index];
            if (ve == null) return false;

            IPanel panel = ve.panel;
            if (panel == null) return false;

            var wb = ve.worldBound; // panel coords
            // If geometry isn't resolved yet, worldBound can be 0-sized. We'll retry next tick.
            if (wb.width < 1f || wb.height < 1f) return false;
            var topRightPanel = new Vector2(wb.xMax, wb.yMin);
            var topLeftPanel = new Vector2(wb.xMin, wb.yMin);

            // Convert panel -> screen (for UITK). Then convert to UGUI screen coords if needed.
            // We use PanelToScreenSafe (numerical inversion of ScreenToPanel) to handle GameView scaling.
            Vector2 topRightScreenForPanel = PanelToScreenSafe(panel, topRightPanel);
            Vector2 topLeftScreenForPanel = PanelToScreenSafe(panel, topLeftPanel);
            Vector2 topRightUGUIScreen = ToUGUIScreen(panel, topRightScreenForPanel);
            Vector2 topLeftUGUIScreen = ToUGUIScreen(panel, topLeftScreenForPanel);

            placeLeft = topRightUGUIScreen.x > Screen.width * 0.65f;
            screenPoint = placeLeft ? topLeftUGUIScreen : topRightUGUIScreen;

            return true;
        }

        private static Vector2 PanelToScreenViaRoot(IPanel panel, Rect rootWorldBound, Vector2 panelPos)
        {
            float rw = Mathf.Max(1f, rootWorldBound.width);
            float rh = Mathf.Max(1f, rootWorldBound.height);

            float nx = (panelPos.x - rootWorldBound.xMin) / rw;
            float ny = (panelPos.y - rootWorldBound.yMin) / rh;

            // Two candidates for Y origin (some panels treat Y up, others Y down).
            Vector2 s1 = new Vector2(nx * Screen.width, ny * Screen.height);
            Vector2 s2 = new Vector2(nx * Screen.width, (1f - ny) * Screen.height);

            Vector2 b1 = RuntimePanelUtils.ScreenToPanel(panel, s1);
            Vector2 b2 = RuntimePanelUtils.ScreenToPanel(panel, s2);

            return (b1 - panelPos).sqrMagnitude <= (b2 - panelPos).sqrMagnitude ? s1 : s2;
        }

        private void EnsureOriginComputed(IPanel panel)
        {
            if (panel == null) return;
            if (originComputed && originPanel == panel) return;

            originPanel = panel;
            // If ScreenToPanel treats (0,0) as bottom-left, then screenY=Screen.height corresponds to top-left,
            // which should map to a SMALLER panel Y (because UITK panel Y grows downward).
            Vector2 p0 = RuntimePanelUtils.ScreenToPanel(panel, Vector2.zero);
            Vector2 pTop = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(0f, Screen.height));
            screenToPanelIsBottomLeft = pTop.y < p0.y;
            originComputed = true;
        }

        private Vector2 ToUGUIScreen(IPanel panel, Vector2 screenPointInScreenToPanelConvention)
        {
            EnsureOriginComputed(panel);
            if (screenToPanelIsBottomLeft) return screenPointInScreenToPanelConvention;
            // ScreenToPanel expects top-left screen coords; convert to UGUI (bottom-left).
            return new Vector2(screenPointInScreenToPanelConvention.x, Screen.height - screenPointInScreenToPanelConvention.y);
        }

        static Vector2 PanelToScreenSafe(IPanel panel, Vector2 panelPos)
        {
            // Unity versions differ: some have RuntimePanelUtils.PanelToScreenPoint,
            // others don't. We use reflection to stay compatible.
            // 1) Try using built-in helper when present (Unity version dependent).
            // We search by name and compatible signature to avoid brittle reflection.
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
            var methods = typeof(RuntimePanelUtils).GetMethods(flags);
            for (int i = 0; i < methods.Length; i++)
            {
                var m = methods[i];
                if (m.Name != "PanelToScreenPoint") continue;

                var ps = m.GetParameters();
                if (ps.Length != 2) continue;
                if (ps[1].ParameterType != typeof(Vector2)) continue;
                if (!ps[0].ParameterType.IsInstanceOfType(panel) && ps[0].ParameterType != typeof(IPanel))
                    continue;

                try
                {
                    object result = ps[0].ParameterType == typeof(IPanel)
                        ? m.Invoke(null, new object[] { panel, panelPos })
                        : m.Invoke(null, new object[] { panel, panelPos });

                    if (result is Vector2 v2)
                    {
                        // Different Unity versions / panels can disagree on Y origin.
                        // Pick the candidate that maps back closest to the requested panelPos.
                        Vector2 v2Flipped = new Vector2(v2.x, Screen.height - v2.y);
                        Vector2 back1 = RuntimePanelUtils.ScreenToPanel(panel, v2);
                        Vector2 back2 = RuntimePanelUtils.ScreenToPanel(panel, v2Flipped);
                        return (back1 - panelPos).sqrMagnitude <= (back2 - panelPos).sqrMagnitude ? v2 : v2Flipped;
                    }
                }
                catch
                {
                    // ignore and fallback
                }
            }

            // 2) Robust fallback: numerically invert ScreenToPanel mapping.
            // This handles GameView scaling, DPI scaling, and different panel implementations.
            // We assume an affine mapping without rotation (true for runtime panels).
            Vector2 s00 = Vector2.zero;
            Vector2 s10 = new Vector2(Screen.width, 0f);
            Vector2 s01 = new Vector2(0f, Screen.height);

            Vector2 p00 = RuntimePanelUtils.ScreenToPanel(panel, s00);
            Vector2 p10 = RuntimePanelUtils.ScreenToPanel(panel, s10);
            Vector2 p01 = RuntimePanelUtils.ScreenToPanel(panel, s01);

            float sx = (p10.x - p00.x) / Mathf.Max(1f, Screen.width);
            float sy = (p01.y - p00.y) / Mathf.Max(1f, Screen.height);

            if (Mathf.Abs(sx) < 1e-5f) sx = 1f;
            if (Mathf.Abs(sy) < 1e-5f) sy = 1f;

            float screenX = (panelPos.x - p00.x) / sx;
            float screenY = (panelPos.y - p00.y) / sy;

            // As with the reflection path, verify Y origin by round-tripping.
            Vector2 c1 = new Vector2(screenX, screenY);
            Vector2 c2 = new Vector2(c1.x, Screen.height - c1.y);
            Vector2 b1 = RuntimePanelUtils.ScreenToPanel(panel, c1);
            Vector2 b2 = RuntimePanelUtils.ScreenToPanel(panel, c2);
            return (b1 - panelPos).sqrMagnitude <= (b2 - panelPos).sqrMagnitude ? c1 : c2;
        }

        private static void SetPct(VisualElement root, string labelName, int value)
        {
            var lbl = root.Q<Label>(labelName);
            if (lbl != null) lbl.text = $"{value}%";
        }
        private void PopulateSettingsUI(VisualElement root)
        {
            // sliders
            var master = root.Q<SliderInt>("MasterSlider");
            var music = root.Q<SliderInt>("MusicSlider");
            var sfx = root.Q<SliderInt>("SfxSlider");
            var uiScale = root.Q<SliderInt>("UIScaleSlider");

            // toggles
            var fullscreen = root.Q<Toggle>("FullscreenToggle");
            var vsync = root.Q<Toggle>("VSyncToggle");

            // dropdowns
            var quality = root.Q<DropdownField>("QualityDropdown");
            var resolution = root.Q<DropdownField>("ResolutionDropdown");
            var fps = root.Q<DropdownField>("FpsDropdown");

            // set values
            if (master != null) master.value = Mathf.RoundToInt(SettingsService.Master * 100f);
            if (music != null) music.value = Mathf.RoundToInt(SettingsService.Music * 100f);
            if (sfx != null) sfx.value = Mathf.RoundToInt(SettingsService.Sfx * 100f);
            if (uiScale != null) uiScale.value = Mathf.RoundToInt(SettingsService.UIScale * 100f);

            if (master != null) SetPct(root, "MasterValue", master.value);
            if (music != null) SetPct(root, "MusicValue", music.value);
            if (sfx != null) SetPct(root, "SfxValue", sfx.value);
            if (uiScale != null) SetPct(root, "UIScaleValue", uiScale.value);

            if (fullscreen != null) fullscreen.value = SettingsService.Fullscreen;
            if (vsync != null) vsync.value = SettingsService.VSync;

            if (quality != null)
            {
                quality.choices = new System.Collections.Generic.List<string>(QualitySettings.names);
                quality.index = Mathf.Clamp(SettingsService.QualityIndex, 0, quality.choices.Count - 1);
            }

            if (fps != null)
            {
                fps.choices = new System.Collections.Generic.List<string> { "Unlimited", "30", "60", "120" };
                fps.index = SettingsService.FpsCap switch { 0 => 0, 30 => 1, 60 => 2, 120 => 3, _ => 2 };
            }

            if (resolution != null)
            {
                var res = Screen.resolutions;
                var list = new System.Collections.Generic.List<string>();
                for (int i = 0; i < res.Length; i++)
                    list.Add($"{res[i].width}x{res[i].height} @{(int)res[i].refreshRateRatio.value}Hz");

                resolution.choices = list;

                int idx = SettingsService.ResolutionIndex;
                if (idx < 0)
                {
                    // найти текущую
                    idx = 0;
                    for (int i = 0; i < res.Length; i++)
                        if (res[i].width == Screen.width && res[i].height == Screen.height)
                        { idx = i; break; }
                }
                resolution.index = Mathf.Clamp(idx, 0, Mathf.Max(0, list.Count - 1));
            }
        }

        private void WireSettingsTabs(VisualElement root)
        {
            var tabAudio = root.Q<Button>("TabAudio");
            var tabGraphics = root.Q<Button>("TabGraphics");
            var tabUI = root.Q<Button>("TabUI");

            var pageAudio = root.Q<VisualElement>("Settings_Audio");
            var pageGraphics = root.Q<VisualElement>("Settings_Graphics");
            var pageUI = root.Q<VisualElement>("Settings_UI");

            // если чего-то нет в UXML — просто выходим без ошибок
            if (tabAudio == null || tabGraphics == null || tabUI == null) return;
            if (pageAudio == null || pageGraphics == null || pageUI == null) return;

            void ShowTab(string tab)
            {
                pageAudio.EnableInClassList("hidden", tab != "audio");
                pageGraphics.EnableInClassList("hidden", tab != "graphics");
                pageUI.EnableInClassList("hidden", tab != "ui");

                tabAudio.EnableInClassList("is-active", tab == "audio");
                tabGraphics.EnableInClassList("is-active", tab == "graphics");
                tabUI.EnableInClassList("is-active", tab == "ui");
            }

            // Важно: чтобы не подписываться по 10 раз — сначала отцепим (безопасно)
            tabAudio.clicked -= () => ShowTab("audio");
            tabGraphics.clicked -= () => ShowTab("graphics");
            tabUI.clicked -= () => ShowTab("ui");

            tabAudio.clicked += () => ShowTab("audio");
            tabGraphics.clicked += () => ShowTab("graphics");
            tabUI.clicked += () => ShowTab("ui");

            // по умолчанию
            ShowTab("audio");
        }

        private void WireSettingsCallbacks(VisualElement root)
        {
            var master = root.Q<SliderInt>("MasterSlider");
            if (master != null)
                master.RegisterValueChangedCallback(e =>
                {
                    SetPct(root, "MasterValue", e.newValue);
                    SettingsService.SetMaster(e.newValue / 100f);
                    SettingsService.ApplyAudio();
                    SettingsService.Save();
                });

            var music = root.Q<SliderInt>("MusicSlider");
            if (music != null)
                music.RegisterValueChangedCallback(e =>
                {
                    SetPct(root, "MusicValue", e.newValue);
                    SettingsService.SetMusic(e.newValue / 100f);
                    SettingsService.Save();
                });

            var sfx = root.Q<SliderInt>("SfxSlider");
            if (sfx != null)
                sfx.RegisterValueChangedCallback(e =>
                {
                    SetPct(root, "SfxValue", e.newValue);
                    SettingsService.SetSfx(e.newValue / 100f);
                    SettingsService.Save();
                });

            var fullscreen = root.Q<Toggle>("FullscreenToggle");
            if (fullscreen != null)
                fullscreen.RegisterValueChangedCallback(e => { SettingsService.SetFullscreen(e.newValue); SettingsService.ApplyGraphics(); SettingsService.Save(); });

            var vsync = root.Q<Toggle>("VSyncToggle");
            if (vsync != null)
                vsync.RegisterValueChangedCallback(e => { SettingsService.SetVSync(e.newValue); SettingsService.ApplyGraphics(); SettingsService.Save(); });

            var quality = root.Q<DropdownField>("QualityDropdown");
            if (quality != null)
                quality.RegisterValueChangedCallback(_ => { SettingsService.SetQualityIndex(quality.index); SettingsService.ApplyGraphics(); SettingsService.Save(); });

            var resolution = root.Q<DropdownField>("ResolutionDropdown");
            if (resolution != null)
                resolution.RegisterValueChangedCallback(_ => { SettingsService.SetResolutionIndex(resolution.index); SettingsService.ApplyGraphics(); SettingsService.Save(); });

            var fps = root.Q<DropdownField>("FpsDropdown");
            if (fps != null)
                fps.RegisterValueChangedCallback(_ =>
                {
                    int cap = fps.index switch { 0 => 0, 1 => 30, 2 => 60, 3 => 120, _ => 60 };
                    SettingsService.SetFpsCap(cap);
                    SettingsService.ApplyGraphics();
                    SettingsService.Save();
                });

            var uiScale = root.Q<SliderInt>("UIScaleSlider");
            if (uiScale != null)
                uiScale.RegisterValueChangedCallback(e =>
                {
                    SetPct(root, "UIScaleValue", e.newValue);
                    SettingsService.SetUIScale(e.newValue / 100f);
                    ApplyUiScaleToRoot(root);
                    SettingsService.Save();
                });

            var reset = root.Q<Button>("SettingsResetButton");
            if (reset != null)
                reset.clicked += () =>
                {
                    SettingsService.ResetToDefaults();
                    SettingsService.ApplyAll();
                    PopulateSettingsUI(root);
                    ApplyUiScaleToRoot(root);
                };
        }

        private void ApplyUiScaleToRoot(VisualElement root)
        {
            // Важно: scale применяется к rootVisualElement (или к отдельному контейнеру UI)
            float s = Guardian.UI.SettingsService.UIScale;
            root.style.scale = new Scale(new Vector3(s, s, 1f));
        }


    }
}
