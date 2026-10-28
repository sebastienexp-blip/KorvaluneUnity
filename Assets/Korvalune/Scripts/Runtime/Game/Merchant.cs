using UnityEngine;

namespace Korvalune.Game
{
    public enum MerchantKind { Forgeron, Armurier, Marchand, Joaillier, Mystique, Alchimiste, Curiosites, Coffre, Reliquaire }

    /// <summary>Point de commerce d'une échoppe : nom flottant au-dessus du marchand, halo doré à l'approche ; toucher le
    /// marchand (ou s'en approcher puis toucher) ouvre la fenêtre de commerce correspondante.</summary>
    public class Merchant : MonoBehaviour
    {
        public MerchantKind kind;
        public string displayName;
        public string title;
        public float talkRadius = 3.2f;
        public static System.Action<Merchant> Opened;
        TextMesh nameTag; Transform tagT; Light halo; Transform player; float heat;

        public static string DefaultTitle(MerchantKind k) => k switch
        {
            MerchantKind.Forgeron => "Forgeron", MerchantKind.Armurier => "Armurier", MerchantKind.Marchand => "Marchand",
            MerchantKind.Joaillier => "Joaillier", MerchantKind.Mystique => "Mystique", MerchantKind.Alchimiste => "Alchimiste",
            MerchantKind.Curiosites => "Marchande de curiosités", MerchantKind.Coffre => "Coffre", _ => "Reliquaire Céleste"
        };

        void Start()
        {
            if (!Application.isPlaying) return;
            if (string.IsNullOrEmpty(title)) title = DefaultTitle(kind);
            // le marchand en personne, derrière son étal
            if (kind != MerchantKind.Coffre && kind != MerchantKind.Reliquaire && transform.Find("PNJ") == null)
            {
                var pf = Resources.Load<GameObject>("Prefabs/Characters/PNJ_" + kind);
                if (pf)
                {
                    var npc = Instantiate(pf, transform); npc.name = "PNJ"; npc.transform.localPosition = Vector3.zero; npc.transform.localRotation = Quaternion.identity;
                    foreach (var c in npc.GetComponentsInChildren<Collider>()) if (!c.isTrigger) Destroy(c);
                }
            }
            var go = new GameObject("Nom"); go.transform.SetParent(transform, false); go.transform.localPosition = Vector3.up * 2.45f; tagT = go.transform;
            nameTag = go.AddComponent<TextMesh>(); nameTag.text = string.IsNullOrEmpty(displayName) ? title : $"{displayName}\n<size=40>{title}</size>";
            nameTag.richText = true; nameTag.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); nameTag.GetComponent<MeshRenderer>().sharedMaterial = nameTag.font.material;
            nameTag.fontSize = 56; nameTag.characterSize = 0.045f; nameTag.anchor = TextAnchor.LowerCenter; nameTag.alignment = TextAlignment.Center; nameTag.fontStyle = FontStyle.Bold;
            halo = new GameObject("Halo").AddComponent<Light>(); halo.transform.SetParent(transform, false); halo.transform.localPosition = new Vector3(0, 2.2f, -0.8f);
            halo.type = LightType.Point; halo.range = 4f; halo.intensity = 0; halo.color = new Color(1f, 0.85f, 0.55f); halo.shadows = LightShadows.None;
            var col = gameObject.AddComponent<CapsuleCollider>(); col.isTrigger = true; col.center = Vector3.up * 1f; col.height = 2.2f; col.radius = 0.8f;
        }

        void Update()
        {
            if (!Application.isPlaying) return;
            if (!player) { var p = FindFirstObjectByType<PlayerController>(); if (!p) return; player = p.transform; }
            float d = Vector3.Distance(player.position, transform.position);
            heat = Mathf.MoveTowards(heat, d < 12f ? 1 : 0, Time.deltaTime * 2);
            var cam = Camera.main;
            if (tagT && cam) tagT.rotation = Quaternion.LookRotation(tagT.position - cam.transform.position);
            if (nameTag) nameTag.color = new Color(1f, 0.86f, 0.55f, heat);
            if (halo) halo.intensity = heat * (d < talkRadius + 1 ? 1.6f : 0.6f);
            if (Tapped()) { var pc = player.GetComponent<PlayerController>(); if (d <= talkRadius) Open(); else pc.WalkTo(transform.position + transform.forward * -1.4f, Open); }
        }

        public void Open() { Opened?.Invoke(this); }

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
