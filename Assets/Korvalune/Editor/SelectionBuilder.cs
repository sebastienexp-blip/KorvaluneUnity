using System.IO;
using System.Linq;
using Korvalune.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Korvalune.EditorTools
{
    /// <summary>Scène de sélection des personnages (style Diablo 3) : crypte gothique — dallage, piliers, arches, autel
    /// circulaire où se tient le héros, braseros à flammes et lumières vacillantes, brume rasante et braises.</summary>
    public static class SelectionBuilder
    {
        public const string ScenePath = ModelPipeline.Root + "/Scenes/Korvalune_Selection.unity";

        static Material Mat(string name, Color c, float smooth, string texRel = null, float tile = 1)
        {
            var path = $"{ModelPipeline.Root}/Rendering/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth);
            if (texRel != null)
            {
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ModelPipeline.Root}/Art/{texRel}_BaseMap.png");
                var n = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ModelPipeline.Root}/Art/{texRel}_Normal.png");
                if (t) { m.SetTexture("_BaseMap", t); m.SetTextureScale("_BaseMap", new Vector2(tile, tile)); }
                if (n) { m.SetTexture("_BumpMap", n); m.EnableKeyword("_NORMALMAP"); }
            }
            EditorUtility.SetDirty(m); return m;
        }

        static GameObject Prim(PrimitiveType t, string name, Vector3 pos, Vector3 scale, Material m, Transform parent, Quaternion? rot = null)
        {
            var g = GameObject.CreatePrimitive(t); g.name = name; g.transform.SetParent(parent);
            g.transform.position = pos; g.transform.localScale = scale; if (rot.HasValue) g.transform.rotation = rot.Value;
            g.GetComponent<Renderer>().sharedMaterial = m; Object.DestroyImmediate(g.GetComponent<Collider>()); g.isStatic = true;
            return g;
        }

        public static CharacterSelect Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var env = new GameObject("Crypte").transform;
            var stone = Mat("Crypte_Pierre", new Color(0.55f, 0.52f, 0.50f), 0.15f, "World/ChateauKorvalune/Textures/W_Stone", 3);
            var floor = Mat("Crypte_Dallage", new Color(0.42f, 0.40f, 0.39f), 0.25f, "World/Terrain/Roche", 6);
            var dark = Mat("Crypte_Ombre", new Color(0.05f, 0.045f, 0.05f), 0.1f);
            var gold = Mat("Crypte_Or", new Color(0.75f, 0.55f, 0.25f), 0.7f);
            gold.SetFloat("_Metallic", 1f);
            var fire = Mat("Crypte_Flamme", new Color(1f, 0.55f, 0.15f), 0f);
            fire.EnableKeyword("_EMISSION"); fire.SetColor("_EmissionColor", new Color(4f, 1.6f, 0.4f));

            Prim(PrimitiveType.Cylinder, "Sol", new Vector3(0, -0.05f, 0), new Vector3(40, 0.1f, 40), floor, env);
            Prim(PrimitiveType.Cylinder, "Autel", new Vector3(0, 0.12f, 0), new Vector3(3.4f, 0.12f, 3.4f), stone, env);
            Prim(PrimitiveType.Cylinder, "Autel_bord", new Vector3(0, 0.25f, 0), new Vector3(3.0f, 0.02f, 3.0f), gold, env);
            Prim(PrimitiveType.Cylinder, "Autel_haut", new Vector3(0, 0.26f, 0), new Vector3(2.9f, 0.02f, 2.9f), stone, env);
            // piliers en hémicycle derrière le héros, arches
            for (int i = 0; i < 9; i++)
            {
                float a = Mathf.Lerp(-80, 80, i / 8f) * Mathf.Deg2Rad; var p = new Vector3(Mathf.Sin(a) * 9, 0, Mathf.Cos(a) * 9 + 1.5f);
                Prim(PrimitiveType.Cylinder, "Pilier", p + Vector3.up * 5, new Vector3(1.1f, 5, 1.1f), stone, env);
                Prim(PrimitiveType.Cube, "Socle", p + Vector3.up * 0.35f, new Vector3(1.6f, 0.7f, 1.6f), stone, env);
                Prim(PrimitiveType.Cube, "Chapiteau", p + Vector3.up * 10.2f, new Vector3(1.6f, 0.5f, 1.6f), stone, env);
                if (i < 8)
                {
                    float b = Mathf.Lerp(-80, 80, (i + 0.5f) / 8f) * Mathf.Deg2Rad; var q = new Vector3(Mathf.Sin(b) * 9, 10.8f, Mathf.Cos(b) * 9 + 1.5f);
                    Prim(PrimitiveType.Cube, "Arche", q, new Vector3(3.6f, 1.2f, 1.0f), stone, env, Quaternion.Euler(0, b * Mathf.Rad2Deg, 0));
                }
            }
            Prim(PrimitiveType.Cube, "Mur du fond", new Vector3(0, 8, 14), new Vector3(40, 18, 1), dark, env);
            // braseros : coupe, flamme émissive, lumière, braises
            foreach (var p in new[] { new Vector3(-2.8f, 0, 1.2f), new Vector3(2.8f, 0, 1.2f), new Vector3(-6.5f, 0, 5.5f), new Vector3(6.5f, 0, 5.5f) })
            {
                Prim(PrimitiveType.Cylinder, "Pied", p + Vector3.up * 0.6f, new Vector3(0.18f, 0.6f, 0.18f), gold, env);
                Prim(PrimitiveType.Cylinder, "Coupe", p + Vector3.up * 1.25f, new Vector3(0.75f, 0.08f, 0.75f), gold, env);
                Prim(PrimitiveType.Sphere, "Flamme", p + Vector3.up * 1.45f, new Vector3(0.45f, 0.5f, 0.45f), fire, env);
                var l = new GameObject("Feu").AddComponent<Light>(); l.transform.SetParent(env); l.transform.position = p + Vector3.up * 1.9f;
                l.type = LightType.Point; l.color = new Color(1f, 0.6f, 0.28f); l.range = 9; l.intensity = 3.2f; l.shadows = LightShadows.Soft;
                l.gameObject.AddComponent<Flicker>();
                var ps = new GameObject("Braises").AddComponent<ParticleSystem>(); ps.transform.SetParent(env); ps.transform.position = p + Vector3.up * 1.5f;
                var m = ps.main; m.startLifetime = 2.2f; m.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.4f); m.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
                m.startColor = new Color(1f, 0.55f, 0.2f); m.gravityModifier = -0.12f; m.maxParticles = 120;
                var em = ps.emission; em.rateOverTime = 22;
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.25f; sh.rotation = new Vector3(-90, 0, 0);
                var no = ps.noise; no.enabled = true; no.strength = 0.4f;
                ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ModelPipeline.Root + "/Rendering/FX_Additif.mat");
            }
            // lumière lunaire froide par le haut, contre-jour violet
            var moon = new GameObject("Lune").AddComponent<Light>(); moon.type = LightType.Spot; moon.spotAngle = 38; moon.range = 30; moon.intensity = 18; moon.color = new Color(0.62f, 0.70f, 1f);
            moon.transform.position = new Vector3(0, 14, -2); moon.transform.LookAt(Vector3.up); moon.shadows = LightShadows.Soft;
            var rim = new GameObject("Contre-jour").AddComponent<Light>(); rim.type = LightType.Directional; rim.intensity = 0.35f; rim.color = new Color(0.55f, 0.35f, 0.9f); rim.transform.rotation = Quaternion.Euler(15, 180, 0);
            RenderSettings.skybox = null; RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.10f, 0.09f, 0.11f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = 0.045f; RenderSettings.fogColor = new Color(0.07f, 0.06f, 0.08f);

            var cam = new GameObject("Main Camera").AddComponent<Camera>(); cam.tag = "MainCamera";
            cam.transform.position = new Vector3(0, 1.75f, -5.4f); cam.transform.LookAt(new Vector3(0, 1.15f, 0)); cam.fieldOfView = 36;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.03f, 0.025f, 0.035f);
            var data = cam.gameObject.AddComponent<UniversalAdditionalCameraData>(); data.renderPostProcessing = true;
            var stageT = new GameObject("Héros").transform; stageT.position = new Vector3(0, 0.28f, 0);
            var sel = new GameObject("Sélection des personnages").AddComponent<CharacterSelect>();
            sel.catalog = AssetDatabase.LoadAssetAtPath<CharacterCatalog>(ModelPipeline.Root + "/Data/CharacterCatalog.asset");
            sel.viewCamera = cam; sel.stage = stageT;
            var prof = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ModelPipeline.Root + "/Data/Vallee_PostProcess.asset");
            if (prof) { var v = new GameObject("Post-traitement").AddComponent<Volume>(); v.isGlobal = true; v.sharedProfile = prof; }
            EditorSceneManager.SaveScene(scene, ScenePath);
            var list = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            return sel;
        }

        /// <summary>Captures : compte d'essai à trois héros (liste), puis mode création.</summary>
        public static void Captures(CharacterSelect sel, System.Action<string, bool, string> check)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            Directory.CreateDirectory("ci-out");
            SaveSystem.AccountId = "ci_essai";
            var acc = SaveSystem.Load(); acc.characters.Clear(); SaveSystem.Save();
            string[] cls = { "KaelDraven", "ElyraNoctis", "Varek" };
            for (int i = 0; i < 3; i++) SaveSystem.Create(new[] { "Aldric", "Séléné", "Corvin" }[i], cls[i], i == 1, null);
            acc.characters[0].level = 47; acc.characters[1].level = 200; acc.characters[2].level = 12;
            var cam = sel.viewCamera;
            void Shot(string name)
            {
                Canvas.ForceUpdateCanvases();
                var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
                cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
                var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
                RenderTexture.active = null; cam.targetTexture = null;
                File.WriteAllBytes($"ci-out/Selection_{name}.png", tex.EncodeToPNG()); Object.DestroyImmediate(tex); rt.Release();
            }
            try
            {
                void Idle() { foreach (var a in Object.FindObjectsByType<Animator>(FindObjectsSortMode.None)) { var c = a.runtimeAnimatorController ? a.runtimeAnimatorController.animationClips.FirstOrDefault(x => x.name == "Idle") : null; if (c) c.SampleAnimation(a.gameObject, 0.4f); } }
                sel.Init(); sel.Select(1); Idle(); Shot("1_liste");
                sel.StartCreate(true); sel.PickClass(3); Idle(); Shot("2_creation");
                check("Écran de sélection (style Diablo 3)", true, "ci-out/Selection_*.png, 3 héros d'essai, 5 emplacements");
            }
            catch (System.Exception ex) { check("Écran de sélection (style Diablo 3)", false, ex.Message); }
            SaveSystem.AccountId = "local";
        }
    }
}
