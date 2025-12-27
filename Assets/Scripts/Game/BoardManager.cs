using Guardian.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Guardian.Game
{
    public class BoardManager : MonoBehaviour
    {
        [Header("Level data")]
        public LevelDefinition levelDefinition;

        [Header("Prefabs & Layout")]
        public CardView cardViewPrefab;
        public Transform cardsParent;

        [Header("UI")]
        public TextMeshProUGUI livesText;
        public TextMeshProUGUI demonsText;

        [Header("Kill Mode UI")]
        public Button killButton;
        public Image killButtonImage;
        public TextMeshProUGUI killButtonLabel;
        public GameObject killCancelOverlay;

        private CardView[] cardViews;
        private int currentLives;
        private int demonsRemaining;

        private bool isKillMode = false;

        private Color killNormalColor;
        private string killNormalText;

        private void Awake()
        {
            if (killButtonImage != null) killNormalColor = killButtonImage.color;
            if (killButtonLabel != null) killNormalText = killButtonLabel.text;

            if (killCancelOverlay != null)
                killCancelOverlay.SetActive(false);
        }

        private void Start()
        {
            InitLevel();
        }

        private void InitLevel()
        {
            currentLives = levelDefinition.playerLives;
            demonsRemaining = levelDefinition.demonsToKill;

            cardViews = new CardView[levelDefinition.cards.Length];

            for (int i = 0; i < levelDefinition.cards.Length; i++)
            {
                var entry = levelDefinition.cards[i];
                var cardObj = Instantiate(cardViewPrefab, cardsParent);
                var cardView = cardObj.GetComponent<CardView>();
                cardView.Init(i, entry, this);
                cardViews[i] = cardView;
            }

            UpdateUI();
            CancelKillMode(); // на всякий
        }

        private void UpdateUI()
        {
            if (livesText != null) livesText.text = $"{currentLives}";
            if (demonsText != null) demonsText.text = $"{demonsRemaining}";
        }

        // КНОПКА KILL теперь включает/выключает режим
        public void OnKillButtonPressed()
        {
            if (!isKillMode) EnterKillMode();
            else CancelKillMode();
        }

        private void EnterKillMode()
        {
            isKillMode = true;

            // включаем overlay (клик по пустоте отменит режим)
            if (killCancelOverlay != null)
                killCancelOverlay.SetActive(true);

            // внешний вид кнопки: активная/серая
            if (killButtonImage != null)
                killButtonImage.color = new Color(0.6f, 0.6f, 0.6f, killButtonImage.color.a);

            if (killButtonLabel != null)
                killButtonLabel.text = "Отмена";

            // подсветить убиваемые карты
            RefreshKillHighlights();
        }

        public void CancelKillMode()
        {
            if (!isKillMode) return;
            isKillMode = false;

            if (killCancelOverlay != null)
                killCancelOverlay.SetActive(false);

            if (killButtonImage != null)
                killButtonImage.color = killNormalColor;

            if (killButtonLabel != null)
                killButtonLabel.text = killNormalText;

            ClearKillHighlights();
        }

        private void RefreshKillHighlights()
        {
            foreach (var c in cardViews)
            {
                // подсвечиваем только открытые и живые
                c.SetKillSelectable(c.CanBeKilled);
            }
        }

        private void ClearKillHighlights()
        {
            foreach (var c in cardViews)
                c.SetKillSelectable(false);
        }

        // Клик по карте
        public void OnCardClicked(CardView cardView)
        {
            if (isKillMode)
            {
                // в KillMode убиваем только подсвеченные
                if (cardView.IsKillSelectable)
                {
                    bool wasDemon = cardView.IsDemon;
                    cardView.Kill();

                    if (wasDemon) demonsRemaining--;
                    else currentLives--;

                    UpdateUI();

                    if (demonsRemaining <= 0) Debug.Log("ПОБЕДА!");
                    if (currentLives <= 0) Debug.Log("ПОРАЖЕНИЕ!");

                    CancelKillMode();
                }
                return;
            }

            // обычный режим: открыть карту
            if (!cardView.IsOpen)
                cardView.Reveal();
        }
    }
}
