using UnityEngine;
using UnityEngine.EventSystems;

namespace Guardian.Game
{
    public class KillCancelOverlay : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private BoardManager boardManager;

        public void OnPointerClick(PointerEventData eventData)
        {
            boardManager?.CancelKillMode();
        }
    }
}
