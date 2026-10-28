using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Coffre au trésor (fond des terriers secondaires) : lueur dorée, on le touche pour l'ouvrir ; il répand de
    /// l'or et 2 à 4 objets de meilleure qualité. Un coffre ouvert reste ouvert pour la partie en cours.</summary>
    public class TreasureChest : MonoBehaviour
    {
        public float luck = 2.5f;
        bool opened; Light glow; Transform lid;

        void Start()
        {
            if (!Application.isPlaying) return;
            glow = new GameObject("Lueur").AddComponent<Light>(); glow.transform.SetParent(transform, false); glow.transform.localPosition = new Vector3(0, 1.2f, -0.6f);
            glow.type = LightType.Point; glow.color = new Color(1f, 0.8f, 0.4f); glow.range = 5; glow.intensity = 1.6f; glow.shadows = LightShadows.None;
            var col = gameObject.AddComponent<BoxCollider>(); col.isTrigger = true; col.center = new Vector3(0, 0.6f, 0); col.size = new Vector3(2.2f, 1.6f, 1.6f);
        }

        void Update()
        {
            if (opened || !Application.isPlaying) return;
            if (glow) glow.intensity = 1.4f + 0.5f * Mathf.Sin(Time.time * 3);
            var p = FindFirstObjectByType<PlayerController>(); if (!p) return;
            if (Tapped())
            {
                if (Vector3.Distance(p.transform.position, transform.position) < 2.6f) Open(p);
                else p.WalkTo(transform.position - transform.forward * 1.4f, () => Open(p));
            }
        }

        public void Open(PlayerController p)
        {
            if (opened) return; opened = true;
            Sound.Play("sfx_coffre", transform.position);
            var r = new System.Random();
            long g = Economy.GoldDrop(r, p.level, 1.5f) * 3; LootDrop.SpawnGold(g, LootDrop.Near(transform.position, transform.forward * 1.2f));
            int n = 2 + r.Next(3);
            for (int i = 0; i < n; i++)
            {
                var it = Loot.Roll(r, p.level, luck);
                var off = Quaternion.Euler(0, -60 + 120f * i / Mathf.Max(1, n - 1), 0) * (-transform.forward) * 1.6f;
                LootDrop.Spawn(it, LootDrop.Near(transform.position, off));
            }
            FX.Burst(transform.position + Vector3.up * 0.8f, new Color(1f, 0.82f, 0.3f), 50, 4f, 0.15f, 0.9f, 0.5f);
            if (glow) glow.intensity = 0.4f;
        }

        bool Tapped()
        {
            Vector2 pos; bool down;
            if (Input.touchCount > 0) { var t = Input.GetTouch(0); down = t.phase == TouchPhase.Began; pos = t.position; if (down && UnityEngine.EventSystems.EventSystem.current && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(t.fingerId)) return false; }
            else { down = Input.GetMouseButtonDown(0); pos = Input.mousePosition; if (down && UnityEngine.EventSystems.EventSystem.current && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return false; }
            if (!down || !Camera.main) return false;
            foreach (var h in Physics.RaycastAll(Camera.main.ScreenPointToRay(pos), 200f, ~0, QueryTriggerInteraction.Collide)) if (h.collider && h.collider.gameObject == gameObject) return true;
            return false;
        }
    }
}
