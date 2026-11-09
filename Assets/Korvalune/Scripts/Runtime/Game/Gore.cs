using System.Collections.Generic;
using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Gore : giclées de sang à chaque coup, flaques et éclaboussures au sol, et à la mort décapitation ou
    /// démembrement. Les monstres sont modélisés par segments (un morceau par os) : le morceau tranché (tête, bras, jambe…)
    /// est réellement détaché, part en tournoyant, rebondit et reste au sol ; le moignon saigne en jet.
    /// Couleur selon la créature : sang rouge, ichor vert des araignées, sang noir-rouge des démons, poussière d'os des
    /// squelettes (qui perdent tout de même leurs os). Réglage Options › Jeu › Gore : Complet / Sang seulement / Désactivé.</summary>
    public static class Gore
    {
        public static bool Blood => Settings.Gore < 2;
        public static bool Dismember => Settings.Gore == 0;

        static readonly Dictionary<Health, (float amount, bool crit, float time)> lastHit = new Dictionary<Health, (float, bool, float)>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Hook()
        {
            Health.AnyDamaged -= OnDamaged; Health.AnyDamaged += OnDamaged;
            Health.AnyDied -= OnDied; Health.AnyDied += OnDied;
        }

        enum Fluid { Red, Dark, Green, Bone }
        static Fluid FluidOf(Health h)
        {
            var ai = h ? h.GetComponent<EnemyAI>() : null; var id = ai != null && ai.type != null ? (ai.type.id ?? "") : "";
            if (id.Contains("squelette") || id.Contains("liche")) return Fluid.Bone;
            if (id.Contains("araignee")) return Fluid.Green;
            if (id.Contains("demon")) return Fluid.Dark;
            return Fluid.Red;
        }
        static Color ColorOf(Fluid f) => f switch
        {
            Fluid.Green => new Color(0.42f, 0.55f, 0.08f, 1f),
            Fluid.Dark => new Color(0.28f, 0.02f, 0.02f, 1f),
            Fluid.Bone => new Color(0.82f, 0.78f, 0.68f, 0.9f),
            _ => new Color(0.5f, 0.02f, 0.03f, 1f),
        };

        static Vector3 FromHero(Vector3 at)
        {
            var p = Object.FindFirstObjectByType<PlayerController>(); if (!p) return Random.onUnitSphere;
            var d = at - p.transform.position; d.y = 0; return d.sqrMagnitude > 0.01f ? d.normalized : Vector3.forward;
        }

        static void OnDamaged(Health h, float amount, bool crit)
        {
            if (!h || h.faction == Faction.Player) return;
            lastHit[h] = (amount, crit, Time.time);
            if (!Blood) return;
            var f = FluidOf(h); var c = ColorOf(f);
            var at = h.transform.position + Vector3.up * (0.9f + Random.Range(-0.2f, 0.3f));
            var dir = FromHero(h.transform.position);
            Spray(at, dir, c, crit ? 26 : 12, crit ? 5.5f : 3.5f, f == Fluid.Bone ? 0.07f : 0.09f);
            if (f != Fluid.Bone && Random.value < (crit ? 0.9f : 0.35f)) Splat(h.transform.position + dir * Random.Range(0.4f, 1.4f) + Random.insideUnitSphere * 0.3f, c, Random.Range(0.5f, crit ? 1.3f : 0.9f));
        }

        static int budgetFrame = -1, deathsThisFrame, seversThisFrame;
        static void OnDied(Health h)
        {
            if (!h || h.faction == Faction.Player || !h.GetComponent<EnemyAI>()) return;
            lastHit.TryGetValue(h, out var hit); lastHit.Remove(h);
            // budget par frame : une attaque de zone qui tue beaucoup de monstres d'un coup ne doit pas créer
            // tous les effets et découpes en même temps (saccade). Les morts au-delà gardent loot et XP.
            if (Time.frameCount != budgetFrame) { budgetFrame = Time.frameCount; deathsThisFrame = 0; seversThisFrame = 0; }
            if (++deathsThisFrame > 4) return;
            if (!Blood) return;
            var f = FluidOf(h); var c = ColorOf(f); var pos = h.transform.position; var dir = FromHero(pos);
            Spray(pos + Vector3.up * 1f, dir, c, 34, 5f, 0.1f);
            if (f != Fluid.Bone) { Splat(pos + dir * 0.6f, c, Random.Range(1.4f, 2.2f)); Splat(pos + dir * 1.6f + Random.insideUnitSphere * 0.4f, c, Random.Range(0.6f, 1.1f)); }
            else FX.Dust(pos, 0.8f);
            if (!Dismember || seversThisFrame >= 1) return;
            // un coup critique ou un gros coup final : la créature perd la tête ou un membre (parfois plusieurs)
            float overkill = hit.amount / Mathf.Max(1f, h.maxHp);
            float chance = 0.35f + (hit.crit ? 0.4f : 0f) + Mathf.Clamp01(overkill) * 0.4f;
            if (Random.value > chance) return;
            var parts = Parts(h.transform);
            if (parts.Count == 0) return;
            int cuts = hit.crit && Random.value < 0.4f ? 2 : 1;
            if (overkill > 0.8f) cuts = 3;
            seversThisFrame++;
            Sound.Play(f == Fluid.Bone ? "sfx_os" : "sfx_tranche", pos, 1f, 0.1f, 0.1f);
            for (int i = 0; i < cuts && parts.Count > 0; i++)
            {
                // la tête en priorité (décapitation), sinon un membre au hasard
                int k = i == 0 && Random.value < 0.55f ? parts.FindIndex(p => p.head) : -1; if (k < 0) k = Random.Range(0, parts.Count);
                Sever(h, parts[k].bone, dir, c, f); parts.RemoveAt(k);
            }
        }

        struct Part { public Transform bone; public bool head; }
        static readonly string[] Cuttable = { "Head", "UpperArm", "LowerArm", "UpperLeg", "LowerLeg", "Thigh", "Femur", "Tibia", "Tail2" };
        static List<Part> Parts(Transform root)
        {
            var l = new List<Part>();
            foreach (var t in root.GetComponentsInChildren<Transform>())
            {
                var n = t.name; if (n.EndsWith("_Mesh") || n.StartsWith("Socket_")) continue;
                bool head = n == "Head";
                bool ok = head; if (!ok) foreach (var s in Cuttable) if (n.Contains(s)) { ok = true; break; }
                if (!ok) continue;
                // une seule coupe par chaîne : on garde l'os le plus haut (le bras entier plutôt que l'avant-bras seul) une fois sur deux
                if (!head && n.Contains("Lower") && Random.value < 0.5f) continue;
                if (n.Contains("Tibia") && Random.value < 0.6f) continue;
                l.Add(new Part { bone = t, head = head });
            }
            return l;
        }

        /// <summary>Détache le segment (l'os et tout ce qui en dépend : tête + mâchoire, bras + main…). Les maillages déformés
        /// par le squelette sont découpés à la volée : les triangles portés par ces os passent dans un débris (pose figée à
        /// l'instant du coup) et disparaissent du corps ; les objets rigides accrochés aux os suivent le débris. Le moignon saigne.</summary>
        static void Sever(Health h, Transform bone, Vector3 dir, Color c, Fluid f)
        {
            var sub = new HashSet<Transform>(bone.GetComponentsInChildren<Transform>());
            var chunk = new GameObject("Débris " + bone.name); chunk.transform.SetPositionAndRotation(bone.position, bone.rotation);
            int n = 0;
            foreach (var r in h.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer) continue;
                if (r is SkinnedMeshRenderer smr) { if (Split(smr, sub, chunk.transform)) n++; continue; }
                if (!sub.Contains(r.transform) && !r.transform.IsChildOf(bone)) continue;
                var mf = r.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue;
                var piece = new GameObject(r.name); piece.transform.SetParent(chunk.transform, false);
                piece.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation);
                piece.transform.localScale = Div(r.transform.lossyScale, chunk.transform.lossyScale);
                piece.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var pr = piece.AddComponent<MeshRenderer>(); pr.sharedMaterials = r.sharedMaterials;
                r.enabled = false; n++;
            }
            if (n == 0) { Object.Destroy(chunk); return; }
            var fly = chunk.AddComponent<GoreChunk>();
            fly.vel = dir * Random.Range(3f, 5.5f) + Vector3.up * Random.Range(4f, 6.5f) + Random.insideUnitSphere;
            fly.spin = Random.insideUnitSphere * 720f; fly.color = c; fly.bloody = f != Fluid.Bone;
            if (f != Fluid.Bone) Fountain(bone, c);
        }

        /// <summary>Découpe d'un maillage skinné : sommet attribué à son os dominant. Retourne vrai si un morceau est parti.</summary>
        static bool Split(SkinnedMeshRenderer smr, HashSet<Transform> sub, Transform chunk)
        {
            var src = smr.sharedMesh; if (!src || !src.isReadable) return false;
            var bones = smr.bones; if (bones == null || bones.Length == 0) return false;
            var cut = new bool[bones.Length]; bool any = false;
            for (int i = 0; i < bones.Length; i++) if (bones[i] && sub.Contains(bones[i])) { cut[i] = true; any = true; }
            if (!any) return false;
            var bw = src.boneWeights; if (bw.Length != src.vertexCount) return false;
            var off = new bool[bw.Length]; int count = 0;
            for (int v = 0; v < bw.Length; v++) if (bw[v].boneIndex0 < cut.Length && cut[bw[v].boneIndex0]) { off[v] = true; count++; }
            if (count == 0) return false;
            // morceau : pose actuelle figée, uniquement les triangles entièrement portés par les os tranchés
            var baked = new Mesh(); smr.BakeMesh(baked, true);
            var keepBody = new List<int>[src.subMeshCount]; var piece = new List<int>[src.subMeshCount]; bool gotPiece = false;
            for (int s = 0; s < src.subMeshCount; s++)
            {
                var t = src.GetTriangles(s); keepBody[s] = new List<int>(t.Length); piece[s] = new List<int>();
                for (int i = 0; i + 2 < t.Length; i += 3)
                {
                    int o = (off[t[i]] ? 1 : 0) + (off[t[i + 1]] ? 1 : 0) + (off[t[i + 2]] ? 1 : 0);
                    if (o >= 2) { piece[s].Add(t[i]); piece[s].Add(t[i + 1]); piece[s].Add(t[i + 2]); gotPiece = true; }
                    else { keepBody[s].Add(t[i]); keepBody[s].Add(t[i + 1]); keepBody[s].Add(t[i + 2]); }
                }
            }
            if (!gotPiece) { Object.Destroy(baked); return false; }
            baked.subMeshCount = src.subMeshCount;
            for (int s = 0; s < src.subMeshCount; s++) baked.SetTriangles(piece[s], s, false);
            baked.RecalculateBounds();
            var go = new GameObject(smr.name); go.transform.SetParent(chunk, true); go.transform.SetPositionAndRotation(smr.transform.position, smr.transform.rotation);
            go.AddComponent<MeshFilter>().sharedMesh = baked; var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterials = smr.sharedMaterials;
            go.AddComponent<GoreMeshes>().meshes.Add(baked);
            // corps : copie du maillage sans ces triangles (le moignon reste ouvert, caché par le jet de sang)
            var body = Object.Instantiate(src); body.name = src.name + " (tranché)";
            for (int s = 0; s < src.subMeshCount; s++) body.SetTriangles(keepBody[s], s, false);
            smr.sharedMesh = body;
            var jan = smr.GetComponent<GoreMeshes>(); if (!jan) jan = smr.gameObject.AddComponent<GoreMeshes>(); jan.meshes.Add(body);
            return true;
        }

        static Vector3 Div(Vector3 a, Vector3 b) => new Vector3(a.x / Mathf.Max(1e-4f, b.x), a.y / Mathf.Max(1e-4f, b.y), a.z / Mathf.Max(1e-4f, b.z));

        // ---------- particules ----------
        static Material bloodMat;
        static Material BloodMat()
        {
            if (bloodMat || !FX.alpha) return bloodMat;
            bloodMat = new Material(FX.alpha); bloodMat.SetTexture("_BaseMap", Droplet()); bloodMat.mainTexture = Droplet(); return bloodMat;
        }

        public static void Spray(Vector3 at, Vector3 dir, Color c, int count, float speed, float size)
        {
            var mat = BloodMat(); if (!mat) return;
            var go = new GameObject("Sang"); go.transform.position = at; go.transform.rotation = Quaternion.LookRotation(dir + Vector3.up * 0.35f);
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = mat; r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.04f; r.lengthScale = 1.2f;
            var m = ps.main; m.playOnAwake = false; m.loop = false; m.duration = 0.1f; m.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.35f, speed); m.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size * 1.4f);
            m.startColor = new ParticleSystem.MinMaxGradient(c, c * 0.7f); m.gravityModifier = 1.6f; m.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 0;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 32; sh.radius = 0.08f;
            ps.Emit(count);
            Object.Destroy(go, 2f);
        }

        /// <summary>Jet de sang du moignon (le long de l'os, pendant un peu plus d'une seconde, qui faiblit).</summary>
        static void Fountain(Transform stump, Color c)
        {
            var mat = BloodMat(); if (!mat || !stump || !stump.parent) return;
            var go = new GameObject("Jet"); go.transform.SetParent(stump.parent, false); go.transform.position = stump.position;
            var up = (stump.position - stump.parent.position); go.transform.rotation = Quaternion.LookRotation(up.sqrMagnitude > 1e-4f ? up.normalized : Vector3.up);
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = mat; r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.05f;
            var m = ps.main; m.loop = false; m.duration = 1.4f; m.startLifetime = 0.6f; m.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.2f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f); m.startColor = c; m.gravityModifier = 1.5f; m.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = new ParticleSystem.MinMaxCurve(70f, AnimationCurve.Linear(0, 1, 1, 0.1f));
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 14; sh.radius = 0.03f;
            ps.Play();
            Object.Destroy(go, 2.4f);
        }

        // ---------- flaques au sol ----------
        static readonly Queue<GameObject> splats = new Queue<GameObject>();
        static readonly Dictionary<Color, Material> splatMats = new Dictionary<Color, Material>();
        static Texture2D splatTex, dropTex;

        public static void Splat(Vector3 pos, Color c, float size)
        {
            if (!FX.alpha) return;
            if (!splatMats.TryGetValue(c, out var m) || !m)
            {
                m = new Material(FX.alpha); m.SetTexture("_BaseMap", SplatTex()); m.mainTexture = SplatTex(); m.SetColor("_BaseColor", new Color(c.r, c.g, c.b, 0.92f)); m.color = m.GetColor("_BaseColor");
                m.renderQueue = 2990; splatMats[c] = m;
            }
            var g = GameBootstrap.Ground(pos);
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); q.name = "Flaque"; Object.Destroy(q.GetComponent<Collider>());
            q.transform.position = g + Vector3.up * (0.03f + Random.Range(0f, 0.01f)); q.transform.rotation = Quaternion.Euler(90, Random.Range(0f, 360f), 0);
            q.transform.localScale = new Vector3(size, size * Random.Range(0.75f, 1.1f), 1);
            var r = q.GetComponent<Renderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            q.AddComponent<GoreFade>().life = 40f;
            splats.Enqueue(q); while (splats.Count > 70) { var o = splats.Dequeue(); if (o) Object.Destroy(o); }
        }

        /// <summary>Tache de sang procédurale : flaque centrale irrégulière, gouttes et traînées projetées.</summary>
        static Texture2D SplatTex()
        {
            if (splatTex) return splatTex;
            const int S = 128; var px = new Color32[S * S]; var rnd = new System.Random(11);
            var blobs = new List<Vector3> { new Vector3(64, 64, 26) };
            for (int i = 0; i < 9; i++) { double a = rnd.NextDouble() * 6.283, d = 14 + rnd.NextDouble() * 18; blobs.Add(new Vector3(64 + (float)(System.Math.Cos(a) * d), 64 + (float)(System.Math.Sin(a) * d), 8 + (float)rnd.NextDouble() * 10)); }
            for (int i = 0; i < 22; i++) { double a = rnd.NextDouble() * 6.283, d = 34 + rnd.NextDouble() * 26; blobs.Add(new Vector3(64 + (float)(System.Math.Cos(a) * d), 64 + (float)(System.Math.Sin(a) * d), 1.5f + (float)rnd.NextDouble() * 3.5f)); }
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
            {
                float v = 0; foreach (var b in blobs) { float dx = x - b.x, dy = y - b.y; v += b.z * b.z / (dx * dx + dy * dy + 1); }
                float a = Mathf.Clamp01((v - 0.9f) * 2.2f); float edge = Mathf.Clamp01(1 - Mathf.Abs(v - 1.4f));
                byte shade = (byte)(255 - 60 * edge);                                             // bord plus sombre, centre plus brillant
                px[y * S + x] = new Color32(shade, shade, shade, (byte)(a * 255));
            }
            splatTex = new Texture2D(S, S, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "Flaque" }; splatTex.SetPixels32(px); splatTex.Apply(true, true);
            return splatTex;
        }

        static Texture2D Droplet()
        {
            if (dropTex) return dropTex;
            const int S = 32; var px = new Color32[S * S];
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
            {
                float dx = (x - 15.5f) / 15.5f, dy = (y - 15.5f) / 15.5f; float r = Mathf.Sqrt(dx * dx + dy * dy);
                px[y * S + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01((1 - r) * 3f) * 255));
            }
            dropTex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Goutte" }; dropTex.SetPixels32(px); dropTex.Apply(false, true);
            return dropTex;
        }
    }

    /// <summary>Morceau tranché : vol balistique en tournoyant, traînée de sang, rebonds sur le sol, puis repos ; s'enfonce
    /// dans le sol au bout d'un moment.</summary>
    public class GoreChunk : MonoBehaviour
    {
        public Vector3 vel, spin; public Color color; public bool bloody;
        float born, nextDrip; bool resting; int bounces;
        void Start() { born = Time.time; }
        void Update()
        {
            float dt = Time.deltaTime, age = Time.time - born;
            if (!resting)
            {
                vel += Physics.gravity * dt; transform.position += vel * dt; transform.Rotate(spin * dt, Space.World);
                var g = GameBootstrap.Ground(transform.position);
                if (transform.position.y < g.y + 0.08f)
                {
                    transform.position = new Vector3(transform.position.x, g.y + 0.08f, transform.position.z);
                    if (bloody && Gore.Blood) Gore.Splat(transform.position, color, Random.Range(0.5f, 0.9f));
                    if (++bounces >= 3 || vel.magnitude < 1.5f) { resting = true; transform.rotation = Quaternion.Euler(Random.Range(-20f, 20f) + 90f * Random.Range(0, 2), Random.Range(0f, 360f), 0); }
                    vel = new Vector3(vel.x * 0.45f, -vel.y * 0.35f, vel.z * 0.45f); spin *= 0.5f;
                }
                if (bloody && Time.time > nextDrip) { nextDrip = Time.time + 0.05f; Gore.Spray(transform.position, -vel.normalized, color, 2, 0.6f, 0.06f); }
            }
            if (age > 14f) transform.position += Vector3.down * dt * 0.15f;
            if (age > 17f) Destroy(gameObject);
        }
    }

    /// <summary>Libère les maillages créés par la découpe quand le corps ou le débris disparaît.</summary>
    public class GoreMeshes : MonoBehaviour
    {
        public readonly List<Mesh> meshes = new List<Mesh>();
        void OnDestroy() { foreach (var m in meshes) if (m) Destroy(m); }
    }

    /// <summary>Flaque qui s'efface doucement en fin de vie.</summary>
    public class GoreFade : MonoBehaviour
    {
        public float life = 40f; float born;
        void Start() { born = Time.time; }
        void Update()
        {
            float left = life - (Time.time - born);
            if (left < 3f) transform.localScale *= 1f - Time.deltaTime * 0.35f;
            if (left <= 0) Destroy(gameObject);
        }
    }
}
