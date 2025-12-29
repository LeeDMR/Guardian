using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Guardian.Game
{
    public class SpeechBubbleView : MonoBehaviour
    {
        public RectTransform rect;
        public TextMeshProUGUI text;

        [Header("Style")]
        [SerializeField] private Image background;
        [SerializeField] private Color backgroundColor = new Color(0.12f, 0.12f, 0.14f, 0.95f);
        [SerializeField] private Color textColor = new Color(0.92f, 0.92f, 0.95f, 1f);
        [SerializeField] private bool useAutoFontSize = true;
        [SerializeField] private float fontSize = 20f;
        [SerializeField] private float fontSizeMin = 16f;
        [SerializeField] private float fontSizeMax = 22f;

        [Header("Layout")]
        [SerializeField] private Vector2 padding = new Vector2(18f, 14f);
        [SerializeField] private float minWidth = 170f;
        [SerializeField] private float maxWidth = 420f;
        [SerializeField] private float minHeight = 64f;
        [SerializeField] private float maxHeight = 260f;

        [Header("Fx")]
        [SerializeField] private bool addOutline = true;
        [SerializeField] private Color outlineColor = new Color(0f, 0f, 0f, 0.35f);
        [SerializeField] private Vector2 outlineDistance = new Vector2(2f, -2f);

        Coroutine routine;

        private void Awake()
        {
            if (rect == null) rect = GetComponent<RectTransform>();
            if (text == null) text = GetComponentInChildren<TextMeshProUGUI>(true);
            if (background == null) background = GetComponent<Image>();

            // Make the bubble purely visual (not blocking clicks).
            if (background != null)
            {
                background.color = backgroundColor;
                background.raycastTarget = false;
            }
            if (text != null)
            {
                text.color = textColor;
                text.raycastTarget = false;
                text.enableWordWrapping = true;
                text.alignment = TextAlignmentOptions.TopLeft;

                text.enableAutoSizing = useAutoFontSize;
                if (useAutoFontSize)
                {
                    text.fontSizeMin = fontSizeMin;
                    text.fontSizeMax = fontSizeMax;
                    text.fontSize = Mathf.Clamp(fontSize, fontSizeMin, fontSizeMax);
                }
                else
                {
                    text.fontSize = fontSize;
                }
            }

            if (addOutline)
            {
                var outline = GetComponent<Outline>();
                if (outline == null) outline = gameObject.AddComponent<Outline>();
                outline.effectColor = outlineColor;
                outline.effectDistance = outlineDistance;
                outline.useGraphicAlpha = true;
            }

            // Start hidden.
            gameObject.SetActive(false);
        }

        public void Show(string message, float duration)
        {
            if (text) text.text = message;
            FitToContent();
            gameObject.SetActive(true);

            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(AutoHide(duration));
        }

        private void FitToContent()
        {
            if (rect == null || text == null) return;

            // Ensure our text rect has padding.
            var tr = text.rectTransform;
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(padding.x, padding.y);
            tr.offsetMax = new Vector2(-padding.x, -padding.y);

            // 1) Set width based on preferred width (clamped), then force update to get correct height.
            text.ForceMeshUpdate();
            float desiredW = Mathf.Clamp(text.preferredWidth + padding.x * 2f, minWidth, maxWidth);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, desiredW);

            // 2) Now that width is known, recalc height.
            text.ForceMeshUpdate();
            float desiredH = Mathf.Clamp(text.preferredHeight + padding.y * 2f, minHeight, maxHeight);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, desiredH);
        }

        IEnumerator AutoHide(float t)
        {
            yield return new WaitForSeconds(t);
            gameObject.SetActive(false);
        }
    }
}
