using UnityEngine;
using UnityEngine.EventSystems;

namespace Guardian.Game
{
    public class KillCancelOverlay : MonoBehaviour, IPointerClickHandler
    {
        public BoardManager boardManager;

        public void OnPointerClick(PointerEventData eventData)
        {
            boardManager?.CancelKillMode();
        }
    }
}
