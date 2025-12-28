using Guardian.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Guardian.Game
{
    public enum CardState { Closed, OpenAlive, DeadVillager, DeadDemon }

    public class CardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI")]
        public Button button;
        public Image portraitImage;
        public TextMeshProUGUI numberText;
        public TextMeshProUGUI nameText;

        [Header("Kill visuals")]
        public Image highlightOverlay;
        public float highlightAlpha = 0.20f;
        public float hoverAlpha = 0.35f;
        public float hoverScale = 1.03f;

        [Header("Visual Sprites")]
        public Sprite backSprite; // рубашка (назначь в префабе)

        [HideInInspector] public int index;
        [HideInInspector] public LevelCardEntry entry;

        CardState state = CardState.Closed;
        BoardManager board;

        bool killSelectable;
        bool abilityUsed;
        Vector3 baseScale;

        // “маска” (что видит игрок при раскрытии)
        public CardDefinition VisibleDefinition => entry.cardDefinition;

        // “истина” (если есть trueDefinition)
        public CardDefinition TruthDefinition => entry.trueDefinition != null ? entry.trueDefinition : entry.cardDefinition;

        public bool IsOpen => state != CardState.Closed;
        public bool CanBeKilled => state == CardState.OpenAlive;
        public bool IsKillSelectable => killSelectable;
        public bool CanUseAbility => state == CardState.OpenAlive && !abilityUsed;

        private void Awake()
        {
            baseScale = transform.localScale;

            if (highlightOverlay == null)
            {
                var t = transform.Find("HighlightOverlay");
                if (t) highlightOverlay = t.GetComponent<Image>();
            }

            SetHighlight(false, 0f);
        }

        public void Init(int idx, LevelCardEntry e, BoardManager bm)
        {
            index = idx;
            entry = e;
            board = bm;

            if (button == null) button = GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => board.OnCardClicked(this));

            abilityUsed = false;
            state = CardState.Closed;

            if (numberText) numberText.text = (index + 1).ToString();

            SetClosedVisual();
            SetKillSelectable(false);
        }

        public void Reveal()
        {
            if (state != CardState.Closed) return;
            state = CardState.OpenAlive;
            SetOpenVisual();
        }

        public string UseAbility()
        {
            if (!CanUseAbility) return null;
            abilityUsed = true;

            // что скажет карта: интро + statementText (из уровня)
            string intro = VisibleDefinition.abilityIntroDialogue;
            string clue = entry.statementText;

            if (!string.IsNullOrWhiteSpace(intro))
                return $"{intro}\n{clue}";
            return clue;
        }

        public void Kill()
        {
            if (state != CardState.OpenAlive) return;

            if (entry.isDemon)
            {
                state = CardState.DeadDemon;
                // можно показать истинный портрет/имя демона:
                if (portraitImage) portraitImage.sprite = TruthDefinition.portrait != null ? TruthDefinition.portrait : portraitImage.sprite;
                if (nameText) nameText.text = "Демон";
            }
            else
            {
                state = CardState.DeadVillager;
                if (nameText) nameText.text = "Погиб";
            }

            SetKillSelectable(false);
        }

        void SetClosedVisual()
        {
            if (portraitImage) portraitImage.sprite = backSprite;
            if (nameText) nameText.text = "???";
        }

        void SetOpenVisual()
        {
            if (portraitImage) portraitImage.sprite = VisibleDefinition.portrait != null ? VisibleDefinition.portrait : backSprite;
            if (nameText) nameText.text = VisibleDefinition.displayName;
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

        void SetHighlight(bool enabled, float alpha)
        {
            if (highlightOverlay == null) return;
            highlightOverlay.enabled = enabled;
            var c = highlightOverlay.color; c.a = alpha;
            highlightOverlay.color = c;
        }
    }
}
