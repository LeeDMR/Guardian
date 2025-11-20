using Guardian.Data;
using TMPro;
using UnityEngine;

namespace Guardian.Game
{
    public class BoardManager : MonoBehaviour
    {
        [Header("Level data")]
        public LevelDefinition levelDefinition;

        [Header("Prefabs & Layout")]
        public CardView cardViewPrefab;
        public Transform cardsParent; // GridLayoutGroup

        [Header("UI")]
        public TextMeshProUGUI livesText;
        public TextMeshProUGUI demonsText;

        private CardView[] cardViews;
        private int currentLives;
        private int demonsRemaining;

        private CardView selectedCard;

        private void Start()
        {
            InitLevel();
        }

        private void InitLevel()
        {
            currentLives = levelDefinition.playerLives;
            demonsRemaining = levelDefinition.demonsToKill;

            cardViews = new CardView[levelDefinition.cards.Length];

            // Создаём карточки
            for (int i = 0; i < levelDefinition.cards.Length; i++)
            {
                LevelCardEntry entry = levelDefinition.cards[i];
                var cardObj = Instantiate(cardViewPrefab, cardsParent);
                var cardView = cardObj.GetComponent<CardView>();

                cardView.Init(i, entry, this);
                cardViews[i] = cardView;
            }

            UpdateUI();
        }

        private void UpdateUI()
        {
            if (livesText != null)
                livesText.text = $"{currentLives}";

            if (demonsText != null)
                demonsText.text = $"{demonsRemaining}";
        }

        public void OnCardClicked(CardView cardView)
        {
            // Логика клика:
            // 1-й клик по закрытой карте — открыть
            if (!cardView.IsOpen)
            {
                cardView.Reveal();
            }

            // Запоминаем выбранную карту для убийства
            selectedCard = cardView;
            // На будущее можно подсветить выбранную карту
        }

        // Вызывается кнопкой "Убить" в UI
        public void OnKillButtonPressed()
        {
            if (selectedCard == null) return;
            if (!selectedCard.IsOpen) return;

            bool wasDemon = selectedCard.IsDemon;
            selectedCard.Kill();

            if (wasDemon)
            {
                demonsRemaining--;
                CheckWinCondition();
            }
            else
            {
                currentLives--;
                CheckLoseCondition();
            }

            UpdateUI();
        }

        private void CheckWinCondition()
        {
            if (demonsRemaining <= 0)
            {
                Debug.Log("ПОБЕДА!");
                // Здесь можно показать окно Victory
            }
        }

        private void CheckLoseCondition()
        {
            if (currentLives <= 0)
            {
                Debug.Log("ПОРАЖЕНИЕ!");
                // Здесь можно показать окно Defeat
            }
        }
    }
}
