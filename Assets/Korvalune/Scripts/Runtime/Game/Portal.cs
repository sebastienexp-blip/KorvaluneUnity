using UnityEngine;
using UnityEngine.SceneManagement;

namespace Korvalune.Game
{
    /// <summary>Passage entre deux scènes (entrée de la crypte au cimetière, sortie vers la vallée) : le héros qui s'en approche
    /// est sauvegardé puis transporté ; il réapparaît au point d'arrivée. Le portail ne s'arme qu'une fois le héros éloigné
    /// (pas de retour immédiat quand on apparaît dessus).</summary>
    public class Portal : MonoBehaviour
    {
        public string targetScene;
        public Vector3 arrival;
        public string label = "Entrer";
        public float radius = 1.8f;
        bool armed, used;

        void Start()
        {
            if (!Application.isPlaying) return;
            Vfx.Runes(transform.position, Element.Neant, radius, 99999f);
            var l = new GameObject("Lueur").AddComponent<Light>(); l.transform.SetParent(transform, false); l.transform.localPosition = Vector3.up * 1.2f;
            l.type = LightType.Point; l.color = new Color(0.6f, 0.45f, 1f); l.range = 7; l.intensity = 2.5f; l.gameObject.AddComponent<Flicker>().amount = 0.12f;
        }

        void Update()
        {
            if (used) return;
            var p = FindFirstObjectByType<PlayerController>(); if (!p) return;
            float d = Vector3.Distance(p.transform.position, transform.position);
            if (!armed) { armed = d > radius + 2.5f; return; }
            if (d < radius)
            {
                used = true;
                FindFirstObjectByType<GameBootstrap>()?.Persist();
                GameBootstrap.nextSpawn = arrival;
                SceneManager.LoadScene(targetScene);
            }
        }
    }
}
