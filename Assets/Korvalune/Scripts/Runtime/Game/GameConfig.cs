using System.Collections.Generic;
using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Références de la tranche jouable (remplies par l'éditeur : WorldBuilder) : héros, ennemis, matériaux d'effets.</summary>
    [CreateAssetMenu(menuName = "Korvalune/Configuration du jeu")]
    public class GameConfig : ScriptableObject
    {
        [System.Serializable] public class EnemyType
        {
            public string id, label;
            public GameObject prefab;
            public float hp = 60, damage = 8, speed = 3.2f, attackRange = 1.8f, attackCooldown = 1.6f, aggro = 13f, xp = 20, scale = 1f, armor = 0f;
            public float hitDelay = 0.45f;
        }
        public CharacterCatalog catalog;
        public GameObject defaultHero;
        public List<GameObject> heroes = new List<GameObject>();
        public List<EnemyType> enemies = new List<EnemyType>();
        public Material fxAdditive, fxAlpha, ringMat;
        public Font font;
        public EnemyType Enemy(string id) => enemies.Find(e => e.id == id);
    }
}
