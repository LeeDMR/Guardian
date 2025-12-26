using Guardian.Data;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Guardian.Game
{
    public enum CardState
    {
        Closed,
        OpenAlive,
        DeadVillager,
        DeadDemon
    }

    public class CardView : MonoBehaviour
    {
        [Header("UI")]
        public Button button;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI roleText;
        public TextMeshProUGUI statementText;

        [Header("Runtime data")]
        public int index;
        public LevelCardEntry entry;

        private CardState state = CardState.Closed;
        private BoardManager boardManager;

        public void Init(int index, LevelCardEntry entry, BoardManager boardManager)
        {
            this.index = index;
            this.entry = entry;
            this.boardManager = boardManager;

            // В начале карта закрыта
            SetClosedVisual();

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);
        }

        private void OnClick()
        {
            boardManager.OnCardClicked(this);
        }

        public void Reveal()
        {
            if (state != CardState.Closed) return;

            state = CardState.OpenAlive;
            SetOpenAliveVisual();
        }

        public void Kill()
        {
            if (state != CardState.OpenAlive) return;

            if (entry.isDemon)
            {
                state = CardState.DeadDemon;
                SetDeadDemonVisual();
            }
            else
            {
                state = CardState.DeadVillager;
                SetDeadVillagerVisual();
            }
        }

        private void SetClosedVisual()
        {
            nameText.text = "???";
            roleText.text = "";
            statementText.text = "";
            // Можно поменять цвет кнопки, чтобы выглядела как рубашка карты
        }

        private void SetOpenAliveVisual()
        {
            nameText.text = entry.cardDefinition.displayName;
            roleText.text = GetMaskedRoleText();
            statementText.text = entry.statementText;
            // Можно поменять цвет/иконку для «живой, открытой» карты
        }

        private void SetDeadVillagerVisual()
        {
            nameText.text = entry.cardDefinition.displayName;
            roleText.text = "Невинный погиб";
            // Можно добавить иконку черепа, другой цвет фона
        }

        private void SetDeadDemonVisual()
        {
            nameText.text = "Демон уничтожен";
            roleText.text = entry.cardDefinition.displayName;
            // Здесь можно показывать «истинный облик», позже — спрайт демона
        }

        public bool IsDemon => entry.isDemon;
        public bool IsAlive => state == CardState.OpenAlive || state == CardState.Closed;
        public bool IsOpen => state != CardState.Closed;

        private string GetMaskedRoleText()
        {
            if (entry.isDemon)
            {
                return CardType.Villager.ToString();
            }

            return entry.cardDefinition.roleType.ToString();
        }
    }
}
