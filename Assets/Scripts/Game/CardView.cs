using Guardian.Data;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

namespace Guardian.Game
{
    public enum CardState { Closed, OpenAlive, DeadVillager, DeadDemon }

    public class CardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI")]
        public Button button;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI roleText;
        public TextMeshProUGUI statementText;

        [Header("Kill Mode Visuals")]
        public Image highlightOverlay;          // <-- привяжи HighlightOverlay сюда
        public float highlightAlpha = 0.20f;    // обычная подсветка
        public float hoverAlpha = 0.35f;        // подсветка при наведении
        public float hoverScale = 1.03f;        // лёгкий zoom при наведении

        [Header("Runtime data")]
        [HideInInspector] public int index;
        [HideInInspector] public LevelCardEntry entry;

        private CardState state = CardState.Closed;
        private BoardManager boardManager;

        private bool killSelectable = false;
        private Vector3 baseScale;

        // Демон: истина (если используешь trueDefinition из 1-го варианта)
        private CardDefinition TruthDef => entry.trueDefinition != null ? entry.trueDefinition : entry.cardDefinition;

        public bool IsDemon => entry != null && entry.isDemon;
        public bool IsOpen => state != CardState.Closed;
        public bool CanBeKilled => state == CardState.OpenAlive; // убиваем только открытые живые

        public bool IsKillSelectable => killSelectable;

        private void Awake()
        {
            baseScale = transform.localScale;
            SetHighlight(false, 0f);
        }

        public void Init(int index, LevelCardEntry entry, BoardManager boardManager)
        {
            this.index = index;
            this.entry = entry;
            this.boardManager = boardManager;

            state = CardState.Closed;

            if (button == null)
                button = GetComponent<Button>();

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);

            SetClosedVisual();
            SetKillSelectable(false);
        }

        private void OnClick()
        {
            boardManager?.OnCardClicked(this);
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

            if (IsDemon)
            {
                state = CardState.DeadDemon;
                SetDeadDemonVisual();
            }
            else
            {
                state = CardState.DeadVillager;
                SetDeadVillagerVisual();
            }

            // после смерти карта больше не выбирается
            SetKillSelectable(false);
        }

        public void SetKillSelectable(bool value)
        {
            killSelectable = value;

            if (!killSelectable)
            {
                SetHighlight(false, 0f);
                transform.localScale = baseScale;
            }
            else
            {
                SetHighlight(true, highlightAlpha);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!killSelectable) return;
            SetHighlight(true, hoverAlpha);
            transform.localScale = baseScale * hoverScale;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!killSelectable) return;
            SetHighlight(true, highlightAlpha);
            transform.localScale = baseScale;
        }

        private void SetHighlight(bool enabled, float alpha)
        {
            if (highlightOverlay == null) return;
            highlightOverlay.enabled = enabled;
            var c = highlightOverlay.color;
            c.a = alpha;
            highlightOverlay.color = c;
        }

        private void SetClosedVisual()
        {
            nameText.text = "???";
            roleText.text = "";
            statementText.text = "";
        }

        private void SetOpenAliveVisual()
        {
            // показываем МАСКУ (как ты хотел)
            var visible = entry.cardDefinition;
            nameText.text = visible.displayName;
            roleText.text = visible.roleType.ToString();
            statementText.text = entry.statementText;
        }

        private void SetDeadVillagerVisual()
        {
            nameText.text = entry.cardDefinition.displayName;
            roleText.text = "Невинный погиб";
        }

        private void SetDeadDemonVisual()
        {
            // показываем ИСТИНУ демона
            var truth = TruthDef;
            nameText.text = "Демон уничтожен";
            roleText.text = truth.displayName;
        }
    }
}
