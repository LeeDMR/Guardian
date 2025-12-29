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

        public int Index => index;

        CardState state = CardState.Closed;
        BoardManager board;

        bool killSelectable;
        bool abilitySelectable;
        bool abilityTargetSelected;
        bool abilityUsed;
        Vector3 baseScale;

        // "маска" (то, что видит игрок при раскрытии)
        public CardDefinition VisibleDefinition => entry.cardDefinition;

        // "истина" (если есть trueDefinition)
        public CardDefinition TruthDefinition => entry.trueDefinition != null ? entry.trueDefinition : entry.cardDefinition;

        public bool IsOpen => state != CardState.Closed;
        public bool IsDead => state == CardState.DeadDemon || state == CardState.DeadVillager;
        // Kill mode should be able to kill BOTH revealed and unrevealed cards.
        // (If a card is unrevealed, we'll reveal it upon death.)
        public bool CanBeKilled => !IsDead;
        public bool IsKillSelectable => killSelectable;
        public bool IsAbilitySelectable => abilitySelectable;
        public bool CanUseAbility => state == CardState.OpenAlive && !abilityUsed && VisibleDefinition != null && VisibleDefinition.hasAbility;

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
            abilitySelectable = false;
            abilityTargetSelected = false;
            state = CardState.Closed;

            if (numberText) numberText.text = (index + 1).ToString();

            SetClosedVisual();
            SetKillSelectable(false);
            SetAbilitySelectable(false);
        }

        public void Reveal()
        {
            if (state != CardState.Closed) return;
            state = CardState.OpenAlive;
            SetOpenVisual();
        }

        /// <summary>
        /// Помечает способность использованной и возвращает интро-реплику (если задана).
        /// Текст самой "подсказки" должен формировать BoardManager (там есть логика правды/лжи и выбор целей).
        /// </summary>
        public bool TryConsumeAbility(out string introDialogue)
        {
            introDialogue = null;
            if (!CanUseAbility) return false;

            abilityUsed = true;
            introDialogue = VisibleDefinition.abilityIntroDialogue;
            return true;
        }

        public void ForceDisableAbility()
        {
            abilityUsed = true;

            // на всякий случай сбросим подсветки/режимы способности
            abilitySelectable = false;
            abilityTargetSelected = false;
        }

        public void Kill()
        {
            if (IsDead) return;

            // If the card is unrevealed, reveal it first so the player sees what died.
            if (state == CardState.Closed)
            {
                Reveal();
            }

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

            if (killSelectable)
            {
                // режимы не должны пересекаться
                abilitySelectable = false;
                abilityTargetSelected = false;
            }

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

        public void SetAbilitySelectable(bool value)
        {
            abilitySelectable = value;
            if (!abilitySelectable)
                abilityTargetSelected = false;

            if (!abilitySelectable)
            {
                // если не в kill-mode тоже убираем подсветку
                if (!killSelectable)
                {
                    SetHighlight(false, 0f);
                    transform.localScale = baseScale;
                }
            }
            else
            {
                SetHighlight(true, highlightAlpha);
            }
        }

        public void SetAbilityTargetSelected(bool selected)
        {
            if (!abilitySelectable) return;
            abilityTargetSelected = selected;
            SetHighlight(true, abilityTargetSelected ? hoverAlpha : highlightAlpha);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!killSelectable && !abilitySelectable) return;
            SetHighlight(true, hoverAlpha);
            transform.localScale = baseScale * hoverScale;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!killSelectable && !abilitySelectable) return;

            float baseAlpha = highlightAlpha;
            if (abilitySelectable && abilityTargetSelected) baseAlpha = hoverAlpha;

            SetHighlight(true, baseAlpha);
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
