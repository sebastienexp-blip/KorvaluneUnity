using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Colonne de lumière au-dessus d'un objet tombé au sol, à la couleur de sa rareté (dans l'esprit des
    /// butins de Diablo) : faisceau aux bords doux qui fond vers le haut (cœur brillant + halo large, toujours tourné
    /// vers la caméra), volutes d'énergie qui montent en spirale le long du faisceau, flammèches et anneau de lumière
    /// au sol, éclair d'arrivée. Plus l'objet est rare, plus la colonne est haute, large et animée ; les objets
    /// mythiques et ancestraux ont plusieurs faisceaux entrelacés.</summary>
    public class LootBeam : MonoBehaviour
    {
        static Texture2D beamTex, softTex, ringTex;
        Transform[] quads; Material[] mats; float[] baseA; float born; Color col; int rarity;
        ParticleSystem wisps;

        static Texture2D BeamTex()
        {
            if (beamTex) return beamTex;
            // dégradé : très lumineux au centre, bords doux ; fondu en haut, fondu bref en bas
            int W = 64, H = 256; beamTex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Faisceau" };
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
            {
                float v = y / (H - 1f); float vert = Mathf.Pow(1f - v, 1.35f) * Mathf.Clamp01(v / 0.04f);
                for (int x = 0; x < W; x++)
                {
                    float u = Mathf.Abs(x / (W - 1f) * 2f - 1f);
                    float core = Mathf.Exp(-u * u * 18f), soft = Mathf.Pow(Mathf.Clamp01(1f - u), 2.2f);
                    float a = Mathf.Clamp01((core * 0.9f + soft * 0.45f) * vert);
                    byte w = (byte)(255 * Mathf.Clamp01(0.75f + core * 0.25f));
                    px[y * W + x] = new Color32(w, w, w, (byte)(255 * a));
                }
            }
            beamTex.SetPixels32(px); beamTex.Apply(false, true); return beamTex;
        }

        static Texture2D SoftTex()
        {
            if (softTex) return softTex;
            int S = 64; softTex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Halo doux" };
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
            {
                float r = Mathf.Sqrt((x - 31.5f) * (x - 31.5f) + (y - 31.5f) * (y - 31.5f)) / 32f; float a = Mathf.Pow(Mathf.Clamp01(1 - r), 2.4f);
                px[y * S + x] = new Color32(255, 255, 255, (byte)(255 * a));
            }
            softTex.SetPixels32(px); softTex.Apply(false, true); return softTex;
        }

        static Texture2D RingTex()
        {
            if (ringTex) return ringTex;
            int S = 128; ringTex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Anneau" };
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
            {
                float r = Mathf.Sqrt((x - 63.5f) * (x - 63.5f) + (y - 63.5f) * (y - 63.5f)) / 64f;
                float a = Mathf.Exp(-Mathf.Pow((r - 0.72f) / 0.09f, 2)) + 0.35f * Mathf.Pow(Mathf.Clamp01(1 - r), 3f);
                px[y * S + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.Clamp01(a)));
            }
            ringTex.SetPixels32(px); ringTex.Apply(false, true); return ringTex;
        }

        static Material Mat(Texture2D t, Color c)
        {
            var m = new Material(FX.additive);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t); if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", t);
            m.SetColor("_BaseColor", c); if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            return m;
        }

        GameObject Quad(string name, Vector3 pos, Vector3 scale, Material m)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); q.name = name;
            var cl = q.GetComponent<Collider>(); if (cl) { if (Application.isPlaying) Destroy(cl); else DestroyImmediate(cl); }
            q.transform.SetParent(transform, false); q.transform.localPosition = pos; q.transform.localScale = scale;
            var r = q.GetComponent<Renderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            return q;
        }

        public static void Attach(GameObject go, Color c, int rarity)
        {
            if (!FX.additive || rarity < (int)Rarity.Legendaire) return;     // pas d'aura jusqu'à rare
            var lb = go.AddComponent<LootBeam>(); lb.col = c; lb.rarity = rarity; lb.Build();
        }

        void Build()
        {
            born = Time.time;
            int r = rarity;
            float h = r switch { 1 => 1.8f, 2 => 3.4f, 3 => 7.5f, 4 => 8.5f, 5 => 12f, _ => 14f };
            float w = r switch { 1 => 0.35f, 2 => 0.5f, 3 => 0.9f, 4 => 1.0f, 5 => 1.2f, _ => 1.3f };
            int beams = r >= 5 ? 3 : 1;
            var qs = new System.Collections.Generic.List<Transform>(); var ms = new System.Collections.Generic.List<Material>(); var ba = new System.Collections.Generic.List<float>();
            var core = new Color(Mathf.Lerp(col.r, 1, 0.45f), Mathf.Lerp(col.g, 1, 0.45f), Mathf.Lerp(col.b, 1, 0.45f), 1);
            for (int b = 0; b < beams; b++)
            {
                // les faisceaux d'un mythique s'écartent un peu et s'inclinent, comme des rayons qui tombent du ciel
                var off = beams > 1 ? new Vector3(Mathf.Cos(b * 2.1f) * 0.35f, 0, Mathf.Sin(b * 2.1f) * 0.35f) : Vector3.zero;
                float hb = h * (b == 0 ? 1f : 0.82f + 0.08f * b);
                var halo = Mat(BeamTex(), new Color(col.r, col.g, col.b, 0.55f)); var heart = Mat(BeamTex(), new Color(core.r, core.g, core.b, 0.9f));
                qs.Add(Quad("Faisceau halo", off + new Vector3(0, hb / 2, 0), new Vector3(w * 2.6f, hb, 1), halo).transform); ms.Add(halo); ba.Add(0.55f);
                qs.Add(Quad("Faisceau cœur", off + new Vector3(0, hb / 2, 0), new Vector3(w * 0.9f, hb * 0.96f, 1), heart).transform); ms.Add(heart); ba.Add(0.9f);
            }
            // anneau et éclat au sol
            var ringM = Mat(RingTex(), new Color(col.r, col.g, col.b, 0.9f));
            var ring = Quad("Anneau", new Vector3(0, 0.04f, 0), Vector3.one * (1.1f + 0.3f * r), ringM); ring.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var poolM = Mat(SoftTex(), new Color(col.r, col.g, col.b, 0.7f));
            var pool = Quad("Lueur au sol", new Vector3(0, 0.03f, 0), Vector3.one * (1.8f + 0.5f * r), poolM); pool.transform.localRotation = Quaternion.Euler(90, 0, 0);
            quads = qs.ToArray(); mats = ms.ToArray(); baseA = ba.ToArray();
            ringTr = ring.transform; ringMat = ringM; ringBase = ring.transform.localScale.x;

            // volutes : traits lumineux étirés qui montent en spirale autour du faisceau
            wisps = new GameObject("Volutes").AddComponent<ParticleSystem>(); wisps.transform.SetParent(transform, false);
            wisps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = wisps.main; m.loop = true; m.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.8f + 0.2f * r);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.6f + 0.35f * r, 1.4f + 0.6f * r); m.startSize = new ParticleSystem.MinMaxCurve(0.06f + 0.02f * r, 0.14f + 0.04f * r);
            m.startColor = new ParticleSystem.MinMaxGradient(col, core); m.simulationSpace = ParticleSystemSimulationSpace.Local; m.maxParticles = 200;
            var em = wisps.emission; em.rateOverTime = r switch { 1 => 4, 2 => 8, 3 => 22, 4 => 26, 5 => 40, _ => 50 };
            var sh = wisps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.25f + 0.08f * r; sh.rotation = new Vector3(-90, 0, 0); sh.radiusThickness = 0.3f;
            var vel = wisps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
            vel.orbitalY = new ParticleSystem.MinMaxCurve(1.5f + 0.4f * r, 3f + 0.6f * r); vel.radial = new ParticleSystem.MinMaxCurve(-0.15f, 0.05f);
            var col0 = wisps.colorOverLifetime; col0.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(core, 0), new GradientColorKey(col, 0.5f), new GradientColorKey(col, 1) },
                                              new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.15f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0, 1) });
            col0.color = g;
            var sz = wisps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0.3f));
            var pr = wisps.GetComponent<ParticleSystemRenderer>(); pr.sharedMaterial = Mat(SoftTex(), Color.white);
            pr.renderMode = ParticleSystemRenderMode.Stretch; pr.velocityScale = 0.12f; pr.lengthScale = 2.5f + 0.4f * r;
            wisps.Play();

            // flammèches au pied (légendaire et au-delà)
            if (r >= 3)
            {
                var fl = new GameObject("Flammèches").AddComponent<ParticleSystem>(); fl.transform.SetParent(transform, false);
                fl.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var fm = fl.main; fm.loop = true; fm.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f); fm.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2f);
                fm.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f); fm.startColor = new ParticleSystem.MinMaxGradient(col, core); fm.simulationSpace = ParticleSystemSimulationSpace.World;
                var fe = fl.emission; fe.rateOverTime = 14 + 4 * (r - 3);
                var fs = fl.shape; fs.shapeType = ParticleSystemShapeType.Circle; fs.radius = 0.35f; fs.rotation = new Vector3(-90, 0, 0);
                var fz = fl.sizeOverLifetime; fz.enabled = true; fz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 1, 1, 0));
                var fc = fl.colorOverLifetime; fc.enabled = true; var fg = new Gradient();
                fg.SetKeys(new[] { new GradientColorKey(core, 0), new GradientColorKey(col, 1) }, new[] { new GradientAlphaKey(0.9f, 0), new GradientAlphaKey(0, 1) }); fc.color = fg;
                var fr = fl.GetComponent<ParticleSystemRenderer>(); fr.sharedMaterial = Mat(SoftTex(), Color.white); fr.renderMode = ParticleSystemRenderMode.Stretch; fr.velocityScale = 0.25f; fr.lengthScale = 1.2f;
                fl.Play();
            }
            // éclair d'arrivée : la colonne « tombe » du ciel (légendaire et au-delà)
            if (r >= 3) FX.Burst(transform.position + Vector3.up * 0.4f, col, 24 + 6 * r, 3f + 0.4f * r, 0.12f, 0.7f, 0.5f);
        }

        Transform ringTr; Material ringMat; float ringBase;

        void LateUpdate()
        {
            if (quads == null) return;
            float t = Time.time - born;
            var cam = Camera.main;
            // apparition : la colonne descend du ciel et s'allume en 0,35 s
            float grow = Mathf.Clamp01(t / 0.35f);
            for (int i = 0; i < quads.Length; i++)
            {
                var q = quads[i]; if (!q) continue;
                if (cam) { var d = cam.transform.position - q.position; d.y = 0; if (d.sqrMagnitude > 1e-4f) q.rotation = Quaternion.LookRotation(-d.normalized, Vector3.up); }
                // scintillement doux, chaque faisceau à son rythme
                float a = baseA[i] * grow * (0.82f + 0.12f * Mathf.Sin(t * (2.3f + i * 0.7f)) + 0.06f * Mathf.Sin(t * 7.1f + i));
                var c = mats[i].GetColor("_BaseColor"); c.a = a; mats[i].SetColor("_BaseColor", c);
            }
            if (ringTr)
            {
                float k = (t * 0.6f) % 1f;
                ringTr.localScale = Vector3.one * ringBase * (0.85f + 0.3f * k);
                var c = ringMat.GetColor("_BaseColor"); c.a = 0.9f * (1f - k) * grow; ringMat.SetColor("_BaseColor", c);
            }
        }
    }
}
