using System.Collections.Generic;
using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Montures : catalogue, achat à l'écurie, appel et renvoi. En selle, le héros va beaucoup plus vite ; attaquer
    /// ou lancer une compétence le fait descendre (la monture disparaît dans un nuage de poussière). Pas de monture dans
    /// les donjons, les grottes ni les brèches.</summary>
    public static class Mounts
    {
        public class Def { public string id, name, prefab, desc; public long price; public int level; public float speed; public Color color; }

        public static readonly Def[] All =
        {
            new Def { id = "cheval", name = "Cheval de guerre", prefab = "ChevalDeGuerre", price = 2500, level = 5, speed = 1.60f, color = new Color(0.85f, 0.62f, 0.38f),
                      desc = "Robuste cheval bai harnaché de cuir et de laiton. Endurant, docile, il ne craint ni le fer ni le feu." },
            new Def { id = "panthere", name = "Panthère d'ombre", prefab = "PanthereDOmbre", price = 9000, level = 15, speed = 1.72f, color = new Color(0.55f, 0.85f, 0.45f),
                      desc = "Fauve noir aux yeux verts, silencieux comme la nuit. Bondit sans bruit à travers les sous-bois." },
            new Def { id = "tigre", name = "Tigre des cendres", prefab = "TigreDesCendres", price = 15000, level = 22, speed = 1.80f, color = new Color(1f, 0.6f, 0.2f),
                      desc = "Grand tigre rayé des steppes brûlées. Puissant et rapide, il intimide les bêtes sauvages." },
            new Def { id = "griffon", name = "Griffon royal", prefab = "GriffonRoyal", price = 40000, level = 35, speed = 1.95f, color = new Color(1f, 0.85f, 0.4f),
                      desc = "Corps de lion, tête et ailes d'aigle. Monture des seigneurs : la plus rapide du royaume." },
        };

        public static Def Get(string id) { foreach (var d in All) if (d.id == id) return d; return null; }
        public static bool Owned(Wallet w, string id) => w.mounts != null && w.mounts.Contains(id);
    }

    /// <summary>Le cavalier : posé sur le héros. Gère la monture affichée, la posture en selle et la vitesse.</summary>
    public class Rider : MonoBehaviour
    {
        public static Rider Current;
        PlayerController pc; GameObject mount; Animator mountAnim; Transform saddle, modelRoot, hips; Vector3 modelBase; Mounts.Def def;
        float baseWalk, baseRun, lockedUntil;
        public bool Riding => mount;
        public string MountName => def != null ? def.name : "";

        void Awake() { Current = this; pc = GetComponent<PlayerController>(); }
        void OnDestroy() { if (Current == this) Current = null; }

        public static bool Allowed() => Terrain.activeTerrain && !Rift.Active;

        public void Toggle()
        {
            if (Riding) { Dismount(true); return; }
            var w = pc.wallet;
            if (string.IsNullOrEmpty(w.mount) || !Mounts.Owned(w, w.mount)) { pc.Notify?.Invoke("Aucune monture : rendez-vous à l'écurie"); return; }
            if (!Allowed()) { pc.Notify?.Invoke("Impossible d'appeler une monture ici"); return; }
            if (Time.time < lockedUntil) { pc.Notify?.Invoke("Monture en approche…"); return; }
            Mount(Mounts.Get(w.mount));
        }

        public void Mount(Mounts.Def d)
        {
            if (d == null || Riding || pc.Health.Dead) return;
            var pf = Resources.Load<GameObject>("Prefabs/Mounts/" + d.prefab);
            if (!pf) { pc.Notify?.Invoke("Monture introuvable"); return; }
            def = d;
            mount = Instantiate(pf, transform, false); mount.name = "Monture"; mount.transform.localPosition = Vector3.zero; mount.transform.localRotation = Quaternion.identity;
            foreach (var c in mount.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (var mb in mount.GetComponentsInChildren<MonoBehaviour>()) if (!(mb is Rider)) mb.enabled = false;
            mountAnim = mount.GetComponentInChildren<Animator>(); if (mountAnim) mountAnim.applyRootMotion = false;
            foreach (var t in mount.GetComponentsInChildren<Transform>()) if (t.name == "Socket_Saddle") { saddle = t; break; }
            var a = pc.Anim;
            modelRoot = a ? a.transform : null; if (modelRoot == transform) modelRoot = null;
            hips = a && a.isHuman ? a.GetBoneTransform(HumanBodyBones.Hips) : null;
            if (modelRoot) modelBase = modelRoot.localPosition;
            if (a) { a.SetBool("Monte", true); a.CrossFadeInFixedTime("Sk_Monte", 0.2f, 0, 0f); }
            baseWalk = pc.walkSpeed; baseRun = pc.runSpeed; pc.walkSpeed = baseWalk * d.speed; pc.runSpeed = baseRun * d.speed;
            FX.Dust(transform.position, 1.2f); FX.Burst(transform.position + Vector3.up, d.color, 24, 3f, 0.12f, 0.6f, -0.4f);
            Sound.Play("sfx_invocation", transform.position, 0.6f);
            pc.Notify?.Invoke(d.name);
        }

        public void Dismount(bool voluntary)
        {
            if (!Riding) return;
            FX.Dust(transform.position, 1.2f);
            Destroy(mount); mount = null; mountAnim = null; saddle = null;
            if (modelRoot) modelRoot.localPosition = modelBase;
            var a = pc.Anim; if (a) a.SetBool("Monte", false);
            pc.walkSpeed = baseWalk; pc.runSpeed = baseRun;
            if (!voluntary) lockedUntil = Time.time + 1.5f;
        }

        void Update()
        {
            if (!Riding) return;
            // on descend pour combattre, en mourant, ou en entrant dans un lieu interdit
            if (pc.Busy || pc.Health.Dead || pc.Swimming || !Allowed()) { Dismount(false); return; }
            if (mountAnim) mountAnim.SetFloat(CharacterRig.SpeedId, pc.Speed / Mathf.Max(0.1f, def.speed), 0.1f, Time.deltaTime);
        }

        void LateUpdate()
        {
            if (!Riding || !saddle) return;
            // le bassin du cavalier se pose sur la selle (après l'animation, sans dérive)
            if (modelRoot && hips) modelRoot.position += saddle.position - hips.position;
            else if (modelRoot) modelRoot.position = saddle.position;
        }
    }
}

namespace Korvalune.Game
{
    /// <summary>L'écurie de la vallée : enclos de bois, râtelier à foin, abreuvoir, cheval de démonstration et maître
    /// d'écurie (commerce des montures).</summary>
    public static class Stable
    {
        public static void Place(GameBootstrap game, Vector3 pos, float yaw)
        {
            var root = new GameObject("Écurie").transform; root.position = pos; root.rotation = Quaternion.Euler(0, yaw, 0);
            var lit = game && game.config && game.config.litMat ? game.config.litMat : LootDrop.litBase;
            Material Mat(Color c) { var m = lit ? new Material(lit) : new Material(Shader.Find("Universal Render Pipeline/Lit")); m.SetColor("_BaseColor", c); if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f); return m; }
            var wood = Mat(new Color(0.42f, 0.28f, 0.16f)); var dark = Mat(new Color(0.25f, 0.16f, 0.09f)); var water = Mat(new Color(0.12f, 0.22f, 0.28f));
            GameObject Box(string n, Vector3 lp, Vector3 size, Material m, float ry = 0, bool solid = true)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = n; g.transform.SetParent(root, false);
                g.transform.localPosition = lp; g.transform.localRotation = Quaternion.Euler(0, ry, 0); g.transform.localScale = size; g.GetComponent<Renderer>().sharedMaterial = m;
                if (!solid) Object.Destroy(g.GetComponent<Collider>());
                return g;
            }
            // enclos de 8 × 6 m : poteaux et deux lisses, ouvert côté ville
            float W = 8f, D = 6f;
            for (float x = -W / 2; x <= W / 2 + 0.01f; x += 2f)
                foreach (var z in new[] { -D / 2, D / 2 })
                    Box("Poteau", new Vector3(x, 0.6f, z), new Vector3(0.16f, 1.2f, 0.16f), dark);
            for (float z = -D / 2; z <= D / 2 + 0.01f; z += 2f) Box("Poteau", new Vector3(W / 2, 0.6f, z), new Vector3(0.16f, 1.2f, 0.16f), dark);
            foreach (var y in new[] { 0.55f, 1.0f })
            {
                foreach (var z in new[] { -D / 2, D / 2 }) Box("Lisse", new Vector3(0, y, z), new Vector3(W, 0.09f, 0.07f), wood);
                Box("Lisse", new Vector3(W / 2, y, 0), new Vector3(0.07f, 0.09f, D), wood);
            }
            // râtelier, abreuvoir, bottes de foin
            Box("Râtelier", new Vector3(W / 2 - 0.5f, 0.9f, 1.5f), new Vector3(0.6f, 0.9f, 1.6f), dark);
            Box("Abreuvoir", new Vector3(W / 2 - 0.5f, 0.35f, -1.5f), new Vector3(0.7f, 0.5f, 1.8f), wood);
            Box("Eau", new Vector3(W / 2 - 0.5f, 0.58f, -1.5f), new Vector3(0.55f, 0.02f, 1.6f), water, 0, false);
            var hay = Resources.Load<GameObject>("Prefabs/World/Ville/M_Foin");
            if (hay) foreach (var p in new[] { new Vector3(1.5f, 0, 2f), new Vector3(-2.5f, 0, -2.2f) }) { var h = Object.Instantiate(hay, root); h.transform.localPosition = p; }
            // cheval de démonstration, au repos dans l'enclos
            var horse = Resources.Load<GameObject>("Prefabs/Mounts/ChevalDeGuerre");
            if (horse)
            {
                var h = Object.Instantiate(horse, root); h.name = "Cheval de l'écurie"; h.transform.localPosition = new Vector3(1.2f, 0, -0.3f); h.transform.localRotation = Quaternion.Euler(0, 200, 0);
                foreach (var c in h.GetComponentsInChildren<Collider>()) Object.Destroy(c);
                foreach (var mb in h.GetComponentsInChildren<MonoBehaviour>()) mb.enabled = false;
            }
            // maître d'écurie, à l'entrée de l'enclos
            var keeper = new GameObject("Maître d'écurie"); keeper.transform.SetParent(root, false); keeper.transform.localPosition = new Vector3(-W / 2 - 1.2f, 0, 0); keeper.transform.localRotation = Quaternion.Euler(0, -90, 0);
            var mer = keeper.AddComponent<Merchant>(); mer.kind = MerchantKind.Ecurie; mer.displayName = "Aldric";
        }
    }
}
