using System.Collections;
using TMPro;
using UnityEngine;

namespace Guardian.Game
{
    public class SpeechBubbleView : MonoBehaviour
    {
        public RectTransform rect;
        public TextMeshProUGUI text;

        Coroutine routine;

        public void Show(string message, float duration)
        {
            if (text) text.text = message;
            gameObject.SetActive(true);

            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(AutoHide(duration));
        }

        IEnumerator AutoHide(float t)
        {
            yield return new WaitForSeconds(t);
            gameObject.SetActive(false);
        }
    }
}
