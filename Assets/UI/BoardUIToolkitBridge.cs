using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

namespace Guardian.Game
{
    public class BoardUIToolkitBridge : MonoBehaviour
    {
        [SerializeField] private UIDocument doc;
        [SerializeField] private BoardManager board;

        private Label livesValue;
        private Label demonsValue;
        private Button killButton;

        private VisualElement detailsPanel;
        private VisualElement portrait;
        private Label cardTitle;
        private Label cardDesc;
        private Button abilityButton;

        private CardView current;

        private void Awake()
        {
            if (!doc) doc = GetComponent<UIDocument>();
            if (!board) board = FindAnyObjectByType<BoardManager>();

            var root = doc.rootVisualElement;

            livesValue = root.Q<Label>("LivesValue");
            demonsValue = root.Q<Label>("DemonsValue");
            killButton = root.Q<Button>("KillButton");

            detailsPanel = root.Q<VisualElement>("DetailsPanel");
            portrait = root.Q<VisualElement>("Portrait");
            cardTitle = root.Q<Label>("CardTitle");
            cardDesc = root.Q<Label>("CardDesc");
            abilityButton = root.Q<Button>("AbilityButton");

            // UI events
            killButton.clicked += () => board.OnKillButtonPressed();
            abilityButton.clicked += () =>
            {
                if (current != null) board.UseAbility(current);
                RefreshSelected(); // обновим кнопку/тексты после использования
            };

            SetSelected(null);
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
                detailsPanel.style.display = DisplayStyle.None;
                return;
            }

            detailsPanel.style.display = DisplayStyle.Flex;
            RefreshSelected();
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

        // --- Hit tests (для BoardManager.Update, чтобы отмена режимов учитывала UI Toolkit) ---

        public bool IsPointerOverKillButton(Vector2 screenPos)
            => ContainsScreenPoint(killButton, screenPos);

        public bool IsPointerOverDetailsPanel(Vector2 screenPos)
            => ContainsScreenPoint(detailsPanel, screenPos);

        private bool ContainsScreenPoint(VisualElement ve, Vector2 screenPos)
        {
            if (ve == null || ve.panel == null) return false;

            // корректно конвертируем screen -> panel координаты
            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(ve.panel, screenPos);
            return ve.worldBound.Contains(panelPos);
        }
    }
}
