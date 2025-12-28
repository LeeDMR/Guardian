using System.Collections.Generic;
using Guardian.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace Guardian.Game
{
    public class BoardManager : MonoBehaviour
    {
        [Header("Level")]
        public LevelDefinition levelDefinition;

        [Header("Board")]
        public CardView cardViewPrefab;
        public Transform cardsParent;

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

        private bool isKillMode;

        // для cancel kill-mode по клику мимо карты
        private readonly List<RaycastResult> raycastResults = new();
        private PointerEventData pointerData;

        private SpeechBubbleView bubble;

        private void Start()
        {
            if (detailsPanel) detailsPanel.Init(this);
            InitLevel();
        }

        void InitLevel()
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
            }

            UpdateTopUI();
        }

        void Update()
        {
            if (!isKillMode) return;

            // ждём именно клик/тап, а не просто движение мыши
            if (Pointer.current == null) return;
            if (!Pointer.current.press.wasPressedThisFrame) return;

            if (IsPointerOverCard()) return;
            if (IsPointerOverKillButton()) return;

            CancelKillMode();
        }

        public void OnKillButtonPressed()
        {
            if (!isKillMode) EnterKillMode();
            else CancelKillMode();
        }

        void EnterKillMode()
        {
            isKillMode = true;
            if (killButtonImage) killButtonImage.color = new Color(0.6f, 0.6f, 0.6f, killButtonImage.color.a);
            if (killButtonLabel) killButtonLabel.text = "Отмена";

            foreach (var c in cardViews)
                c.SetKillSelectable(c.CanBeKilled);
        }

        public void CancelKillMode()
        {
            isKillMode = false;
            if (killButtonImage) killButtonImage.color = Color.white;
            if (killButtonLabel) killButtonLabel.text = "Kill";

            foreach (var c in cardViews)
                c.SetKillSelectable(false);
        }

        public void OnCardClicked(CardView card)
        {
            // KILL MODE: убиваем только подсвеченные
            if (isKillMode)
            {
                if (card.IsKillSelectable)
                {
                    bool wasDemon = card.entry.isDemon;
                    card.Kill();

                    if (wasDemon) demonsRemaining--;
                    else lives--;

                    UpdateTopUI();
                    CancelKillMode();
                }
                return;
            }

            // обычный режим: открыть и выбрать
            if (!card.IsOpen)
            {
                card.Reveal();
                ShowBubble(card, card.VisibleDefinition.revealDialogue);
            }

            SelectCard(card);
        }

        void SelectCard(CardView card)
        {
            selected = card;
            if (detailsPanel) detailsPanel.Show(card);
        }

        public void UseAbility(CardView card)
        {
            // защита: способность только на выбранной открытой карте
            if (card == null || !card.IsOpen) return;

            string msg = card.UseAbility();
            if (!string.IsNullOrWhiteSpace(msg))
                ShowBubble(card, msg);
        }

        void UpdateTopUI()
        {
            if (livesText) livesText.text = $"Lives: {lives}";
            if (demonsText) demonsText.text = $"Demons: {demonsRemaining}";
        }

        void ShowBubble(CardView card, string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            if (canvas == null) canvas = FindObjectOfType<Canvas>();

            if (bubble == null)
                bubble = Instantiate(speechBubblePrefab, canvas.transform);

            PositionBubbleNearCard(card.GetComponent<RectTransform>(), bubble.rect);
            bubble.Show(message, 2.2f);
        }

        void PositionBubbleNearCard(RectTransform cardRect, RectTransform bubbleRect)
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

        bool IsPointerOverCard()
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

        bool IsPointerOverKillButton()
        {
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
    }
}
