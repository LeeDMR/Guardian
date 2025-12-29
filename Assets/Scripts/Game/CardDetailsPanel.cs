using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Guardian.Game
{
    public class CardDetailsPanel : MonoBehaviour
    {
        [Header("UI")]
        public Image portrait;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI descriptionText;
        public Button useAbilityButton;
        public TextMeshProUGUI useAbilityButtonLabel;

        private BoardManager board;
        private CardView current;

        public void Init(BoardManager boardManager)
        {
            board = boardManager;

            // Чтобы кнопка работала даже если забыли привязать OnClick в инспекторе.
            if (useAbilityButton)
            {
                useAbilityButton.onClick.RemoveAllListeners();
                useAbilityButton.onClick.AddListener(OnUseAbilityClicked);
            }
            Hide();
        }

        public void Show(CardView card)
        {
            current = card;
            gameObject.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            current = null;
            gameObject.SetActive(false);
        }

        public void Refresh()
        {
            if (current == null) return;

            var def = current.VisibleDefinition;
            if (portrait) portrait.sprite = def.portrait;
            if (nameText) nameText.text = def.displayName;
            if (descriptionText) descriptionText.text = def.description;

            bool canUse = current.CanUseAbility;
            if (useAbilityButton) useAbilityButton.interactable = canUse;

            if (useAbilityButtonLabel)
                useAbilityButtonLabel.text = canUse ? def.abilityButtonText : "способность использована";
        }

        // Можно вешать на кнопку в инспекторе, но мы также привязываем в Init() автоматически.
        public void OnUseAbilityClicked()
        {
            if (current == null) return;
            board.UseAbility(current);
            Refresh();
        }
    }
}
