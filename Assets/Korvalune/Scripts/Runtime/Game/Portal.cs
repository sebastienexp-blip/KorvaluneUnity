using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Korvalune.Game
{
    /// <summary>Entrée de donjon (ou sortie) : à l'approche du héros, l'entrée s'illumine — contour doré pulsant sur la
    /// structure, colonne de lumière, runes au sol, nom flottant au-dessus. On la touche pour y entrer : le héros court
    /// jusqu'au seuil, il est sauvegardé puis transporté dans la scène cible, au point d'arrivée.</summary>
    public class Portal : MonoBehaviour
    {
        public string targetScene;
        public Vector3 arrival;
        public string label = "Entrer";
        public string displayName = "";
        public Transform highlightRoot;           // structure qui s'illumine (escalier, arche…) ; à défaut, ce seul objet
        public float radius = 1.8f, revealDistance = 14f;
        public Color glowColor = new Color(1f, 0.78f, 0.35f);
        bool used, armed;
        readonly List<Material> mats = new List<Material>();
        Light lamp; Transform nameTag; TextMesh nameText; Transform shaft; float heat;

        void Start()
        {
            if (!Application.isPlaying) return;
            Vfx.Runes(transform.position, Element.Neant, radius, 99999f);
            lamp = new GameObject("Lueur").AddComponent<Light>(); lamp.transform.SetParent(transform, false); lamp.transform.localPosition = Vector3.up * 1.6f;
            lamp.type = LightType.Point; lamp.color = glowColor; lamp.range = 9; lamp.intensity = 1.2f; lamp.shadows = LightShadows.None;
            // matériaux de la structure : copies avec émission (illumination au survol)
            var root = highlightRoot ? highlightRoot : transform;
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                var ms = r.materials;
                foreach (var m in ms) if (m && m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; mats.Add(m); }
                r.materials = ms;
            }
            // colonne de lumière douce
            if (LootDrop.beamMat)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Cylinder); b.name = "Colonne"; Destroy(b.GetComponent<Collider>());
                b.transform.SetParent(transform, false); b.transform.localPosition = Vector3.up * 2.5f; b.transform.localScale = new Vector3(radius * 1.4f, 2.5f, radius * 1.4f);
                var bm = new Material(LootDrop.beamMat); bm.SetColor("_BaseColor", new Color(glowColor.r, glowColor.g, glowColor.b, 0)); b.GetComponent<Renderer>().sharedMaterial = bm;
                b.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; shaft = b.transform;
            }
            // nom flottant
            var tag = new GameObject("Nom"); tag.transform.SetParent(transform, false); tag.transform.localPosition = Vector3.up * 3.4f; nameTag = tag.transform;
            nameText = tag.AddComponent<TextMesh>(); nameText.text = string.IsNullOrEmpty(displayName) ? label : displayName;
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); nameText.GetComponent<MeshRenderer>().sharedMaterial = nameText.font.material;
            nameText.fontSize = 64; nameText.characterSize = 0.06f; nameText.anchor = TextAnchor.MiddleCenter; nameText.alignment = TextAlignment.Center; nameText.fontStyle = FontStyle.Bold;
            // zone touchable (structure entière)
            var col = gameObject.AddComponent<SphereCollider>(); col.isTrigger = true; col.radius = Mathf.Max(radius * 1.6f, 2.6f); col.center = Vector3.up * 1f;
        }

        void Update()
        {
            if (used || !Application.isPlaying) return;
            var p = FindFirstObjectByType<PlayerController>(); if (!p) return;
            float d = Vector3.Distance(p.transform.position, transform.position);
            // illumination : s'intensifie à l'approche, pulse lentement
            float want = Mathf.Clamp01(1f - (d - 4f) / (revealDistance - 4f));
            heat = Mathf.MoveTowards(heat, want, Time.deltaTime * 2f);
            float pulse = heat * (0.75f + 0.25f * Mathf.Sin(Time.time * 3.2f));
            foreach (var m in mats) m.SetColor("_EmissionColor", glowColor * (pulse * 0.9f));
            if (lamp) lamp.intensity = 1.2f + 3.5f * pulse;
            if (shaft) shaft.GetComponent<Renderer>().sharedMaterial.SetColor("_BaseColor", new Color(glowColor.r, glowColor.g, glowColor.b, 0.22f * pulse));
            if (nameTag)
            {
                var cam = Camera.main;
                if (cam) nameTag.rotation = Quaternion.LookRotation(nameTag.position - cam.transform.position);
                var c = Color.Lerp(new Color(0.85f, 0.75f, 0.55f), glowColor, pulse); c.a = Mathf.Clamp01(heat * 1.5f); nameText.color = c;
            }
            // toucher l'entrée → le héros y court puis entre
            if (TappedThis()) p.WalkTo(transform.position, Enter);
            // marcher droit dedans suffit aussi
            if (!armed) armed = d > radius + 2.5f;
            else if (d < radius * 0.6f && p.walkTo == null) Enter();
        }

        bool TappedThis()
        {
            Vector2 pos; bool down = false;
            if (Input.touchCount > 0) { var t = Input.GetTouch(0); down = t.phase == TouchPhase.Began; pos = t.position; if (down && EventSystem.current && EventSystem.current.IsPointerOverGameObject(t.fingerId)) return false; }
            else { down = Input.GetMouseButtonDown(0); pos = Input.mousePosition; if (down && EventSystem.current && EventSystem.current.IsPointerOverGameObject()) return false; }
            if (!down || !Camera.main) return false;
            var ray = Camera.main.ScreenPointToRay(pos);
            foreach (var h in Physics.RaycastAll(ray, 200f, ~0, QueryTriggerInteraction.Collide)) if (h.collider && h.collider.gameObject == gameObject) return true;
            return false;
        }

        void Enter()
        {
            if (used) return;
            used = true;
            FX.Burst(transform.position + Vector3.up, glowColor, 40, 4f, 0.15f, 0.8f, -0.4f);
            FindFirstObjectByType<GameBootstrap>()?.Persist();
            Sound.UI("sfx_portail", 1f);
            GameBootstrap.nextSpawn = arrival;
            SceneManager.LoadScene(targetScene);
        }
    }
}
