using UnityEngine;

namespace Guardian.Data
{

    [CreateAssetMenu(fileName = "CardDefinition", menuName = "Guardian/Card Definition", order = 0)]
    public class CardDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id;
        public string displayName;

        [Header("Type & Role")]
        public CardType cardType;
        public RoleType roleType;

        [Header("Visual")]
        public Sprite portrait;

        [Header("UI / Ability")]
        public bool hasAbility = true;
        [TextArea(2, 6)] public string description;            // описание роли/способности для правой панели
        public string abilityButtonText = "Использовать способность";

        [Header("Dialogue")]
        [TextArea(1, 3)] public string revealDialogue;         // что говорит при раскрытии
        [TextArea(1, 3)] public string abilityIntroDialogue;   // что говорит перед подсказкой (опционально)
    }
}
