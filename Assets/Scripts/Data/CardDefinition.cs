using UnityEngine;

namespace Guardian.Data
{
    [CreateAssetMenu(
        fileName = "CardDefinition",
        menuName = "Guardian/Card Definition",
        order = 0)]
    public class CardDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id;              // Внутренний ID (например "card_detective_1")
        public string displayName;     // Имя на карте, типа "Сыщик"

        [Header("Type & Role")]
        public CardType cardType;      // Demon / Villager
        public RoleType roleType;      // Сыщик, Священник и т.п.

        [Header("Visual")]
        public Sprite portrait;        // Портрет жителя/демона (для прототипа можно оставить пустым)

        [TextArea(2, 4)]
        [Header("Statement template (опционально)")]
        public string statementTemplate;

    }
}
