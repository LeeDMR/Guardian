using System.Collections.Generic;
using Guardian.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Guardian.Game
{
    public class BoardManager : MonoBehaviour
    {
        [Header("Level")]
        public LevelDefinition levelDefinition;

        [Header("Board")]
        public CardView cardViewPrefab;
        public Transform cardsParent;

        [Header("UI Toolkit Overlay (optional)")]
        public BoardUIToolkitBridge uiBridge;

        [Header("Top UI")]
        public TextMeshProUGUI livesText;
        public TextMeshProUGUI demonsText;

        [Header("Kill Mode UI")]
        public Button killButton;
        public Image killButtonImage;
        public TextMeshProUGUI killButtonLabel;

        [Header("Details Panel")]
        public CardDetailsPanel detailsPanel;

        [Header("Speech Bubble")]
        public Canvas canvas;
        public SpeechBubbleView speechBubblePrefab;

        private CardView[] cardViews;
        private CardView selected;

        private int lives;
        private int demonsRemaining;

        public CardView SelectedCard => selected;
        public bool IsKillMode => mode == InteractionMode.Kill;
        public bool IsAbilityTargeting => mode == InteractionMode.AbilityTargeting;
        private enum InteractionMode
        {
            Normal,
            Kill,
            AbilityTargeting
        }

        private InteractionMode mode = InteractionMode.Normal;

        private enum AbilityKind
        {
            None,

            // пример "мгновенной" способности
            SenseClosedDemonCount,

            // выбрать 1 карту -> "демон/житель"
            InspectSingle,

            // выбрать 2 карты -> "ровно 1 демон"
            PairExactlyOne,

            // выбрать N карт -> "сколько демонов"
            CountInSelection,
        }

        private class AbilityRequest
        {
            public CardView source;
            public AbilityKind kind;
            public int requiredTargets;
            public readonly List<CardView> targets = new();
        }

        private AbilityRequest abilityRequest;

        // для cancel режимов по клику мимо
        private readonly List<RaycastResult> raycastResults = new();
        private PointerEventData pointerData;

        private SpeechBubbleView bubble;

        private void Start()
        {
            if (detailsPanel) detailsPanel.Init(this);
            InitLevel();
            UpdateTopUI();
        }

        /// <summary>
        /// (UI Toolkit main menu) Start / restart the board with a new level definition.
        /// Safe to call multiple times at runtime.
        /// </summary>
        public void StartLevel(LevelDefinition newLevel)
        {
            if (newLevel == null) return;

            levelDefinition = newLevel;

            // Reset modes/UI
            mode = InteractionMode.Normal;
            abilityRequest = null;
            selected = null;
            if (detailsPanel) detailsPanel.Hide();

            // Clear existing UGUI CardView instances (they are used only as data/state holders in UITK mode).
            if (cardsParent != null)
            {
                for (int i = cardsParent.childCount - 1; i >= 0; i--)
                    Destroy(cardsParent.GetChild(i).gameObject);
            }

            // Rebuild
            InitLevel();
            UpdateTopUI();

            // Hide any leftover UGUI bubble (UITK bubble is managed inside the bridge)
            if (bubble != null)
                bubble.gameObject.SetActive(false);
        }

        private void InitLevel()
        {
            lives = levelDefinition.playerLives;
            demonsRemaining = levelDefinition.demonsToKill;

            cardViews = new CardView[levelDefinition.cards.Length];

            for (int i = 0; i < levelDefinition.cards.Length; i++)
            {
                var entry = levelDefinition.cards[i];
                var cv = Instantiate(cardViewPrefab, cardsParent);
                cv.Init(i, entry, this);
                cardViews[i] = cv;

                // When UI Toolkit cards are used, we keep CardView objects only as data/state holders.
                // Disable their GameObjects so they don't intercept pointer events (UGUI) and don't
                // visually overlap with UI Toolkit.
                if (uiBridge != null)
                    cv.gameObject.SetActive(false);
            }
            uiBridge?.BuildCards(cardViews);
            uiBridge?.RefreshAllCards();
        }

        private void Update()
        {
            // Мы работаем через New Input System.
            if (Pointer.current == null) return;
            if (!Pointer.current.press.wasPressedThisFrame) return;

            // Важно: клики по UI Toolkit не должны сбрасывать выбор/режимы в BoardManager.
            // Иначе при клике по кнопке "Use ability" сначала сработает DeselectCard(),
            // и до обработчика UITK дойдёт уже current == null.
            if (uiBridge != null && uiBridge.IsPointerOverAnyUI(Pointer.current.position.ReadValue()))
                return;

            // 1) Kill mode: клик мимо карт/кнопки -> отмена
            if (mode == InteractionMode.Kill)
            {
                if (IsPointerOverCard()) return;
                if (IsPointerOverKillButton()) return;
                CancelKillMode();
                return;
            }

            // 2) Ability targeting: клик мимо карт/панели -> отмена выбора целей
            if (mode == InteractionMode.AbilityTargeting)
            {
                if (IsPointerOverCard()) return;
                if (IsPointerOverDetailsPanel()) return;
                CancelAbilityTargeting();
                return;
            }

            // 3) Normal: клик мимо всего -> снять выделение и спрятать панель
            if (!IsPointerOverCard() && !IsPointerOverDetailsPanel() && !IsPointerOverKillButton())
                DeselectCard();
        }

        public void OnKillButtonPressed()
        {
            // если был выбор целей способности — отменяем его
            if (mode == InteractionMode.AbilityTargeting)
                CancelAbilityTargeting();

            if (mode != InteractionMode.Kill) EnterKillMode();
            else CancelKillMode();
        }

        private void EnterKillMode()
        {
            // Hide details panel: in kill mode we don't want selection UI to interfere.
            DeselectCard();

            mode = InteractionMode.Kill;

            if (killButtonImage) killButtonImage.color = new Color(0.6f, 0.6f, 0.6f, killButtonImage.color.a);
            if (killButtonLabel) killButtonLabel.text = "Отмена";

            foreach (var c in cardViews)
                c.SetKillSelectable(c.CanBeKilled);
            uiBridge?.SetKillMode(true);
            uiBridge?.RefreshAllCards();
        }

        public void CancelKillMode()
        {
            mode = InteractionMode.Normal;

            if (killButtonImage) killButtonImage.color = Color.white;
            if (killButtonLabel) killButtonLabel.text = "Kill";

            foreach (var c in cardViews)
                c.SetKillSelectable(false);
            uiBridge?.SetKillMode(false);
            uiBridge?.RefreshAllCards();
        }

        public void OnCardClicked(CardView card)
        {
            if (card == null) return;

            // 1) KILL MODE: убиваем и закрытые, и раскрытые карты
            if (mode == InteractionMode.Kill)
            {
                // Не полагаемся на IsKillSelectable (UI может не успеть/не подсветить).
                if (card.CanBeKilled)
                {
                    bool wasDemon = card.entry.isDemon;
                    card.Kill();
                    uiBridge?.RefreshAllCards();

                    if (wasDemon) demonsRemaining--;
                    else lives--;

                    UpdateTopUI();
                    CancelKillMode();
                }
                return;
            }

            // 2) ABILITY TARGETING: выбираем цели (не раскрываем карты)
            if (mode == InteractionMode.AbilityTargeting)
            {
                HandleAbilityTargetClicked(card);
                return;
            }

            // 3) NORMAL:
            //   - клик по закрытой: раскрыть и показать реплику (НО НЕ показывать панель справа)
            //   - клик по открытой: выбрать / снять выбор
            if (!card.IsOpen)
            {
                card.Reveal();
                uiBridge?.RefreshAllCards();
                ShowBubble(card, card.VisibleDefinition.revealDialogue);
                return;
            }

            if (selected == card) DeselectCard();
            else SelectCard(card);
        }

        private void SelectCard(CardView card)
        {
            selected = card;
            if (detailsPanel) detailsPanel.Show(card);
            uiBridge?.SetSelected(selected);
            uiBridge?.RefreshAllCards();
        }

        private void DeselectCard()
        {
            selected = null;
            if (detailsPanel) detailsPanel.Hide();
            uiBridge?.SetSelected(null);
            uiBridge?.RefreshAllCards();
        }

        /// <summary>
        /// Нажатие на кнопку "Использовать способность" (вызывается из CardDetailsPanel).
        /// </summary>
        public void UseAbility(CardView source)
        {
            if (source == null) return;
            if (!source.IsOpen) return;
            if (source != selected) return; // способность только у выбранной карты

            // если мы уже в выборе целей — повторное нажатие будет "Отмена"
            if (mode == InteractionMode.AbilityTargeting)
            {
                CancelAbilityTargeting();
                return;
            }

            // если был kill-mode — сбрасываем, чтобы режимы не пересекались
            if (mode == InteractionMode.Kill)
                CancelKillMode();

            var (kind, requiredTargets) = GetAbilityConfig(source);
            if (kind == AbilityKind.None)
            {
                ShowBubble(source, "У этой карты нет способности.");
                return;
            }

            if (requiredTargets <= 0)
            {
                ExecuteImmediateAbility(source, kind);
                return;
            }

            BeginAbilityTargeting(source, kind, requiredTargets);

            uiBridge?.RefreshSelected();
            uiBridge?.RefreshAllCards();
        }

        private (AbilityKind kind, int requiredTargets) GetAbilityConfig(CardView card)
        {
            if (card?.VisibleDefinition == null) return (AbilityKind.None, 0);
            if (!card.VisibleDefinition.hasAbility) return (AbilityKind.None, 0);

            // На старте — маппинг по роли. Потом можно вынести в CardDefinition (abilityKind/targetsRequired).
            switch (card.VisibleDefinition.roleType)
            {
                case RoleType.Priest:
                    return (AbilityKind.PairExactlyOne, 2);
                case RoleType.Detective:
                    return (AbilityKind.InspectSingle, 1);
                case RoleType.Elder:
                    return (AbilityKind.SenseClosedDemonCount, 0);
                default:
                    return (AbilityKind.None, 0);
            }
        }

        private void ExecuteImmediateAbility(CardView source, AbilityKind kind)
        {
            if (!source.TryConsumeAbility(out string intro)) return;

            string abilityText = BuildAbilityText(source, kind, null);
            if (string.IsNullOrWhiteSpace(abilityText)) return;

            // Показываем только результат способности.
            // Интро-реплика часто воспринимается как "текст при раскрытии" и в итоге мешает.
            string msg = string.IsNullOrWhiteSpace(abilityText) ? intro : abilityText;
            ShowBubble(source, msg);
        }

        private void BeginAbilityTargeting(CardView source, AbilityKind kind, int requiredTargets)
        {
            if (!source.CanUseAbility) return;

            // посчитаем доступные цели
            int eligibleCount = 0;
            foreach (var c in cardViews)
            {
                if (c == null) continue;
                if (c == source) continue;
                if (c.IsDead) continue;
                eligibleCount++;
            }

            if (eligibleCount < requiredTargets)
            {
                ShowBubble(source, "Недостаточно целей для способности.");
                return;
            }

            mode = InteractionMode.AbilityTargeting;
            abilityRequest = new AbilityRequest
            {
                source = source,
                kind = kind,
                requiredTargets = requiredTargets
            };

            foreach (var c in cardViews)
            {
                if (c == null) continue;

                bool eligible = c != source && !c.IsDead;
                c.SetAbilitySelectable(eligible);
                c.SetAbilityTargetSelected(false);
            }

            ShowBubble(source, $"Выберите {requiredTargets} карт(у/ы)…");
        }

        private void HandleAbilityTargetClicked(CardView target)
        {
            if (abilityRequest == null || abilityRequest.source == null)
            {
                CancelAbilityTargeting();
                return;
            }

            if (!target.IsAbilitySelectable) return;

            // toggle
            if (abilityRequest.targets.Contains(target))
            {
                abilityRequest.targets.Remove(target);
                target.SetAbilityTargetSelected(false);
            }
            else
            {
                if (abilityRequest.targets.Count >= abilityRequest.requiredTargets) return;
                abilityRequest.targets.Add(target);
                target.SetAbilityTargetSelected(true);
            }

            if (abilityRequest.targets.Count >= abilityRequest.requiredTargets)
                ResolveAbilityTargeting();
            uiBridge?.RefreshAllCards();
        }

        private void ResolveAbilityTargeting()
        {
            var req = abilityRequest;
            if (req == null || req.source == null)
            {
                CancelAbilityTargeting();
                return;
            }

            // только теперь "тратим" способность
            if (!req.source.TryConsumeAbility(out string intro))
            {
                CancelAbilityTargeting();
                return;
            }

            string abilityText = BuildAbilityText(req.source, req.kind, req.targets);
            // Показываем только результат способности (без реплики раскрытия/интро).
            string msg = string.IsNullOrWhiteSpace(abilityText) ? intro : abilityText;
            ShowBubble(req.source, msg);

            CancelAbilityTargeting();
        }

        private void CancelAbilityTargeting()
        {
            mode = InteractionMode.Normal;
            abilityRequest = null;

            foreach (var c in cardViews)
            {
                if (c == null) continue;
                c.SetAbilityTargetSelected(false);
                c.SetAbilitySelectable(false);
            }
            uiBridge?.RefreshAllCards();
        }

        private string BuildAbilityText(CardView speaker, AbilityKind kind, List<CardView> targets)
        {
            bool isLiar = speaker.entry.isDemon; // демоны всегда лгут

            switch (kind)
            {
                case AbilityKind.SenseClosedDemonCount:
                    {
                        int closedCount = 0;
                        int demonsInClosed = 0;
                        foreach (var c in cardViews)
                        {
                            if (c == null) continue;
                            if (c.IsDead) continue;
                            if (c.IsOpen) continue;

                            closedCount++;
                            if (c.entry.isDemon) demonsInClosed++;
                        }

                        int said = isLiar ? GetWrongCount(demonsInClosed, closedCount) : demonsInClosed;
                        return $"Среди закрытых карт демонов: {said}.";
                    }

                case AbilityKind.InspectSingle:
                    {
                        if (targets == null || targets.Count < 1) return null;
                        var t = targets[0];
                        bool truthIsDemon = t.entry.isDemon;
                        bool saidIsDemon = isLiar ? !truthIsDemon : truthIsDemon;
                        int n = t.index + 1;
                        return saidIsDemon ? $"Карта #{n} — демон." : $"Карта #{n} — житель.";
                    }

                case AbilityKind.PairExactlyOne:
                    {
                        if (targets == null || targets.Count < 2) return null;
                        var a = targets[0];
                        var b = targets[1];

                        int demons = 0;
                        if (a.entry.isDemon) demons++;
                        if (b.entry.isDemon) demons++;

                        // истина: есть хотя бы один демон
                        bool truthHasDemon = demons >= 1;

                        // демоны лгут => инвертируем
                        bool saidHasDemon = isLiar ? !truthHasDemon : truthHasDemon;

                        int na = a.index + 1;
                        int nb = b.index + 1;

                        return saidHasDemon
                            ? $"Среди карт #{na} и #{nb} есть демон."
                            : $"Среди карт #{na} и #{nb} нет демонов.";
                    }

                case AbilityKind.CountInSelection:
                    {
                        if (targets == null || targets.Count < 1) return null;
                        int demons = 0;
                        foreach (var t in targets)
                            if (t != null && t.entry.isDemon) demons++;

                        int said = isLiar ? GetWrongCount(demons, targets.Count) : demons;
                        return $"Среди выбранных карт демонов: {said}.";
                    }
            }

            return null;
        }

        private static int GetWrongCount(int truth, int max)
        {
            // вернём любое значение 0..max, но НЕ truth
            if (max <= 0) return 0;
            if (max == 1) return truth == 0 ? 1 : 0;

            int wrong = Random.Range(0, max + 1);
            if (wrong == truth)
                wrong = (wrong + 1) % (max + 1);
            return wrong;
        }

        private static string ComposeAbilityMessage(string intro, string abilityText)
        {
            if (string.IsNullOrWhiteSpace(intro)) return abilityText;
            if (string.IsNullOrWhiteSpace(abilityText)) return intro;
            return $"{intro}\n{abilityText}";
        }

        private void UpdateTopUI()
        {
            if (livesText) livesText.text = $"{lives}";
            if (demonsText) demonsText.text = $"{demonsRemaining}";
            uiBridge?.SetStats(lives, demonsRemaining);
        }

        private void ShowBubble(CardView card, string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            // Prefer UI Toolkit speech bubble (it is anchored to UI Toolkit cards).
            if (uiBridge != null && card != null)
            {
                uiBridge.ShowSpeechNearCard(card, message);
                return;
            }

            if (canvas == null) canvas = Object.FindAnyObjectByType<Canvas>();

            if (bubble == null)
                bubble = Instantiate(speechBubblePrefab, canvas.transform);

            var cardRect = card != null ? card.GetComponent<RectTransform>() : null;
            if (cardRect != null)
                PositionBubbleNearCard(cardRect, bubble.rect);
            bubble.Show(message, 2.2f);
        }

        public void ShowBubbleAtScreenPoint(Vector2 screenPoint, bool placeLeft, string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            if (canvas == null)
                canvas = FindAnyObjectByType<Canvas>();

            if (bubble == null)
                bubble = Instantiate(speechBubblePrefab, canvas.transform);

            var canvasRect = canvas.GetComponent<RectTransform>();
            var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, cam, out Vector2 localPoint);

            var bubbleRect = bubble.rect;
            bubbleRect.pivot = placeLeft ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
            bubbleRect.anchorMin = bubbleRect.anchorMax = new Vector2(0.5f, 0.5f);

            Vector2 offset = placeLeft ? new Vector2(-18f, -10f) : new Vector2(18f, -10f);
            bubbleRect.anchoredPosition = localPoint + offset;

            bubble.Show(message, 2.2f);
        }

        private void PositionBubbleNearCard(RectTransform cardRect, RectTransform bubbleRect)
        {
            var canvasRect = canvas.GetComponent<RectTransform>();
            var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

            // возьмём верхний правый угол карты
            Vector3[] corners = new Vector3[4];
            cardRect.GetWorldCorners(corners);
            Vector3 worldTopRight = corners[2];
            Vector3 worldTopLeft = corners[1];

            // куда ставить: если карта справа — ставим пузырь слева
            bool placeLeft = worldTopRight.x > (Screen.width * 0.65f);

            Vector3 anchorWorld = placeLeft ? worldTopLeft : worldTopRight;

            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(cam, anchorWorld);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, cam, out Vector2 localPoint);

            // pivot и смещение
            bubbleRect.pivot = placeLeft ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
            bubbleRect.anchorMin = bubbleRect.anchorMax = new Vector2(0.5f, 0.5f);

            Vector2 offset = placeLeft ? new Vector2(-18f, -10f) : new Vector2(18f, -10f);
            bubbleRect.anchoredPosition = localPoint + offset;
        }

        private bool IsPointerOverCard()
        {
            if (EventSystem.current == null) return false;
            if (Pointer.current == null) return false;

            pointerData ??= new PointerEventData(EventSystem.current);
            pointerData.position = Pointer.current.position.ReadValue();

            raycastResults.Clear();
            EventSystem.current.RaycastAll(pointerData, raycastResults);

            foreach (var r in raycastResults)
                if (r.gameObject && r.gameObject.GetComponentInParent<CardView>() != null)
                    return true;

            return false;
        }

        private bool IsPointerOverKillButton()
        {
            if (uiBridge != null && Pointer.current != null)
            {
                if (uiBridge.IsPointerOverKillButton(Pointer.current.position.ReadValue()))
                    return true;
            }
            if (EventSystem.current == null || killButton == null) return false;
            if (Pointer.current == null) return false;

            pointerData ??= new PointerEventData(EventSystem.current);
            pointerData.position = Pointer.current.position.ReadValue();

            raycastResults.Clear();
            EventSystem.current.RaycastAll(pointerData, raycastResults);

            foreach (var r in raycastResults)
            {
                if (!r.gameObject) continue;
                if (r.gameObject == killButton.gameObject) return true;
                if (r.gameObject.transform.IsChildOf(killButton.transform)) return true;
            }
            return false;
        }

        private bool IsPointerOverDetailsPanel()
        {
            if (uiBridge != null && Pointer.current != null)
            {
                if (uiBridge.IsPointerOverDetailsPanel(Pointer.current.position.ReadValue()))
                    return true;
            }
            if (EventSystem.current == null || detailsPanel == null) return false;
            if (Pointer.current == null) return false;

            pointerData ??= new PointerEventData(EventSystem.current);
            pointerData.position = Pointer.current.position.ReadValue();

            raycastResults.Clear();
            EventSystem.current.RaycastAll(pointerData, raycastResults);

            foreach (var r in raycastResults)
            {
                if (!r.gameObject) continue;
                if (r.gameObject.transform.IsChildOf(detailsPanel.transform)) return true;
            }
            return false;
        }
    }
}
