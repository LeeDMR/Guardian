using System.Collections.Generic;
using System.Linq;
using UnityEditor.UIElements;
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

        private VisualElement cardsGrid;
        private readonly List<VisualElement> cardRoots = new();
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

        private void Awake()
        {
            if (!doc) doc = GetComponent<UIDocument>();
            if (!board) board = FindAnyObjectByType<BoardManager>();

            root = doc.rootVisualElement;
            cardsGrid = root.Q<VisualElement>("CardsGrid");


            livesValue = root.Q<Label>("LivesValue");
            demonsValue = root.Q<Label>("DemonsValue");
            killButton = root.Q<Button>("KillButton");

            detailsPanel = root.Q<VisualElement>("DetailsPanel");
            portrait = root.Q<VisualElement>("Portrait");
            cardTitle = root.Q<Label>("CardTitle");
            cardDesc = root.Q<Label>("CardDesc");
            abilityButton = root.Q<Button>("AbilityButton");
            detailsPanel = root.Q<VisualElement>("DetailsPanel");

            // UI events
            killButton.clicked += () => board.OnKillButtonPressed();
            abilityButton.clicked += () =>
            {
                if (current != null) board.UseAbility(current);
                RefreshSelected(); // обновим кнопку/тексты после использования
            };

            SetSelected(null);
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
                var btn = ve.Q<Button>("CardTile");

                btn.clicked += () => board.OnCardClicked(cards[idx]);

                cardsGrid.Add(ve);
                cardRoots.Add(btn);

                RefreshCard(idx);
            }
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
            if (card.IsOpen && card.VisibleDefinition.portrait != null)
                portrait.style.backgroundImage = new StyleBackground(card.VisibleDefinition.portrait.texture);
            else
                portrait.style.backgroundImage = StyleKeyword.None;
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
                detailsPanel.style.display = DisplayStyle.None;
                return;
            }

            detailsPanel.style.display = DisplayStyle.Flex;
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

            if (TryGetCardBubbleAnchor(card.Index, out var screenPoint, out var placeLeft))
                board.ShowBubbleAtScreenPoint(screenPoint, placeLeft, message);
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
            var topRightPanel = new Vector2(wb.xMax, wb.yMin);
            var topLeftPanel = new Vector2(wb.xMin, wb.yMin);

            // Convert panel -> screen without PanelToScreenPoint
            var topRightScreen = PanelToScreenApprox(panel, topRightPanel);
            var topLeftScreen = PanelToScreenApprox(panel, topLeftPanel);

            placeLeft = topRightScreen.x > Screen.width * 0.65f;
            screenPoint = placeLeft ? topLeftScreen : topRightScreen;

            return true;
        }

        static Vector2 PanelToScreenApprox(IPanel panel, Vector2 panelPos)
        {
            // Candidate A: assume panel coords == screen coords
            Vector2 s1 = panelPos;

            // Candidate B: flipped Y
            Vector2 s2 = new Vector2(panelPos.x, Screen.height - panelPos.y);

            // Pick the candidate that maps back closest to the original panelPos
            Vector2 p1 = RuntimePanelUtils.ScreenToPanel(panel, s1);
            Vector2 p2 = RuntimePanelUtils.ScreenToPanel(panel, s2);

            float d1 = (p1 - panelPos).sqrMagnitude;
            float d2 = (p2 - panelPos).sqrMagnitude;

            return d1 <= d2 ? s1 : s2;
        }

    }
}
