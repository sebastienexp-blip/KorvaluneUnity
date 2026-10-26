using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Korvalune.EditorTools
{
    /// <summary>
    /// Intègre les modèles ModelForge : réglages d'import des textures et du FBX (Humanoid / Generic, clips, boucles,
    /// mouvement racine), matériaux URP Lit, contrôleur d'animation, prefab avec points d'attache et collisions.
    /// Tout est piloté par les fichiers « *.forge.json » : relancer le traitement est sans effet de bord (idempotent).
    /// </summary>
    public static class ModelPipeline
    {
        public const string Root = "Assets/Korvalune";

        public class Entry { public string manifestPath, dir, modelPath; public ForgeManifest m; }

        public static List<Entry> FindAll()
        {
            var list = new List<Entry>();
            foreach (var guid in AssetDatabase.FindAssets("t:TextAsset", new[] { Root + "/Art" }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (!p.EndsWith(".forge.json")) continue;
                var m = JsonUtility.FromJson<ForgeManifest>(File.ReadAllText(p));
                var dir = Path.GetDirectoryName(p).Replace('\\', '/');
                list.Add(new Entry { manifestPath = p, dir = dir, modelPath = dir + "/" + m.model, m = m });
            }
            return list.OrderBy(e => e.m.priority).ThenBy(e => e.manifestPath).ToList();
        }

        [MenuItem("Korvalune/Modèles/Réimporter et reconstruire tous les modèles ModelForge")]
        public static void ProcessAllMenu() { ProjectSetup.EnsureUrp(); foreach (var e in FindAll()) Process(e); AssetDatabase.SaveAssets(); }

        public static void Process(Entry e)
        {
            var m = e.m;
            // 1. textures
            foreach (var mat in m.materials)
            {
                ConfigureTexture(Rel(e, mat.baseMap), TextureImporterType.Default, true, mat);
                ConfigureTexture(Rel(e, mat.normalMap), TextureImporterType.NormalMap, false, mat);
                ConfigureTexture(Rel(e, mat.maskMap), TextureImporterType.Default, false, mat);
                ConfigureTexture(Rel(e, mat.occlusionMap), TextureImporterType.Default, false, mat);
                ConfigureTexture(Rel(e, mat.emissionMap), TextureImporterType.Default, true, mat);
            }
            // 2. matériaux URP
            var mats = new Dictionary<string, Material>();
            foreach (var fm in m.materials) mats[fm.name] = BuildMaterial(e, fm);
            AssetDatabase.SaveAssets();
            // 3. import du modèle
            ConfigureModel(e, mats);
            // 4. contrôleur + prefab
            AnimatorController ctrl = null;
            if (!string.IsNullOrEmpty(m.controller)) ctrl = BuildController(e);
            if (!string.IsNullOrEmpty(m.prefab)) BuildPrefab(e, ctrl);
        }

        // ---- traitement incrémental : un modèle déjà intégré (mêmes FBX, manifeste, textures et même pipeline) n'est pas retraité.
        // Évite de réimporter à chaque passage les ~140 pièces du créateur (l'éditeur finissait tué faute de mémoire).
        static string pipelineHash;
        static string HashPath(Entry e) => e.manifestPath.Replace(".forge.json", ".forgehash");

        public static string Fingerprint(Entry e)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                if (pipelineHash == null)
                {
                    var src = Directory.GetFiles(Root + "/Editor", "ModelPipeline.cs", SearchOption.AllDirectories).FirstOrDefault();
                    pipelineHash = src != null ? System.BitConverter.ToString(md5.ComputeHash(File.ReadAllBytes(src))) : "?";
                }
                var sb = new System.Text.StringBuilder(pipelineHash);
                void Add(string path) { if (path != null && File.Exists(path)) { var fi = new FileInfo(path); sb.Append('|').Append(fi.Length).Append(':').Append(System.BitConverter.ToString(md5.ComputeHash(File.ReadAllBytes(path)))); } }
                Add(e.manifestPath); Add(e.modelPath);
                foreach (var mat in e.m.materials) { Add(Rel(e, mat.baseMap)); Add(Rel(e, mat.normalMap)); Add(Rel(e, mat.maskMap)); Add(Rel(e, mat.occlusionMap)); Add(Rel(e, mat.emissionMap)); }
                return System.BitConverter.ToString(md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(sb.ToString())));
            }
        }

        /// <summary>Vrai si le modèle a déjà été intégré à l'identique (empreinte enregistrée et prefab présent).</summary>
        public static bool UpToDate(Entry e)
        {
            var hp = HashPath(e);
            if (!File.Exists(hp)) return false;
            if (!string.IsNullOrEmpty(e.m.prefab) && !File.Exists(e.m.prefab)) return false;
            return File.ReadAllText(hp).Trim() == Fingerprint(e);
        }

        public static void MarkDone(Entry e) { try { File.WriteAllText(HashPath(e), Fingerprint(e)); } catch (System.Exception ex) { Debug.LogWarning("[ModelForge] empreinte : " + ex.Message); } }

        static string Rel(Entry e, string p) => string.IsNullOrEmpty(p) ? null : NormalizePath(e.dir + "/" + p);

        static void ConfigureTexture(string path, TextureImporterType type, bool srgb, ForgeMaterial fm)
        {
            if (path == null) return;
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) { Debug.LogError($"[ModelForge] Texture introuvable : {path}"); return; }
            ti.textureType = type;
            ti.sRGBTexture = type == TextureImporterType.NormalMap ? false : srgb;
            ti.mipmapEnabled = true;
            ti.anisoLevel = 4;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.maxTextureSize = fm.maxSize;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            if (fm.alphaClip && srgb && type == TextureImporterType.Default)
            {
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.alphaIsTransparency = true;
                ti.mipMapsPreserveCoverage = true;          // les mèches ne disparaissent pas au loin
                ti.alphaTestReferenceValue = fm.cutoff;
            }
            var android = ti.GetPlatformTextureSettings("Android");
            android.overridden = true; android.maxTextureSize = fm.mobileMaxSize; android.format = TextureImporterFormat.ASTC_6x6;
            ti.SetPlatformTextureSettings(android);
            ti.SaveAndReimport();
        }

        static Material BuildMaterial(Entry e, ForgeMaterial fm)
        {
            var dir = e.dir + "/Materials";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(e.dir, "Materials");
            var path = dir + "/" + fm.name + ".mat";
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader) { name = fm.name }; AssetDatabase.CreateAsset(mat, path); }
            else if (mat.shader != shader) mat.shader = shader;

            var tiling = new Vector2(fm.tiling[0], fm.tiling[1]);
            mat.SetColor("_BaseColor", new Color(fm.color[0], fm.color[1], fm.color[2], fm.color.Length > 3 ? fm.color[3] : 1f));
            SetTex(mat, "_BaseMap", Rel(e, fm.baseMap), tiling);
            mat.mainTextureScale = tiling;

            var normal = SetTex(mat, "_BumpMap", Rel(e, fm.normalMap), tiling);
            mat.SetFloat("_BumpScale", fm.normalScale);
            Keyword(mat, "_NORMALMAP", normal);

            var mask = SetTex(mat, "_MetallicGlossMap", Rel(e, fm.maskMap), tiling);
            Keyword(mat, "_METALLICSPECGLOSSMAP", mask);
            mat.SetFloat("_Metallic", fm.metallic);
            mat.SetFloat("_Smoothness", fm.smoothness);              // avec une carte : multiplicateur du lissé (canal A)
            mat.SetFloat("_SmoothnessTextureChannel", 0f);

            var occ = SetTex(mat, "_OcclusionMap", Rel(e, fm.occlusionMap), tiling);
            mat.SetFloat("_OcclusionStrength", fm.occlusionStrength);
            Keyword(mat, "_OCCLUSIONMAP", occ);

            var emiTex = SetTex(mat, "_EmissionMap", Rel(e, fm.emissionMap), tiling);
            var emi = new Color(fm.emission[0], fm.emission[1], fm.emission[2]);
            bool emissive = emiTex || emi.maxColorComponent > 0.001f;
            mat.SetColor("_EmissionColor", emi);
            Keyword(mat, "_EMISSION", emissive);
            mat.globalIlluminationFlags = emissive ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;

            mat.SetFloat("_Cull", fm.doubleSided ? 0f : 2f);
            mat.SetFloat("_AlphaClip", fm.alphaClip ? 1f : 0f);
            mat.SetFloat("_Cutoff", fm.cutoff);
            Keyword(mat, "_ALPHATEST_ON", fm.alphaClip);
            mat.renderQueue = fm.alphaClip ? (int)UnityEngine.Rendering.RenderQueue.AlphaTest : -1;
            mat.doubleSidedGI = fm.doubleSided;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static bool SetTex(Material mat, string prop, string path, Vector2 tiling)
        {
            if (!mat.HasProperty(prop)) return false;
            var tex = path == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            mat.SetTexture(prop, tex);
            mat.SetTextureScale(prop, tiling);
            return tex != null;
        }

        static void Keyword(Material m, string k, bool on) { if (on) m.EnableKeyword(k); else m.DisableKeyword(k); }

        static void ConfigureModel(Entry e, Dictionary<string, Material> mats)
        {
            var mi = AssetImporter.GetAtPath(e.modelPath) as ModelImporter;
            if (mi == null) { Debug.LogError($"[ModelForge] Modèle introuvable ou non FBX : {e.modelPath}"); return; }
            var m = e.m;
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.importCameras = false; mi.importLights = false;
            mi.importBlendShapes = true; mi.importBlendShapeNormals = ModelImporterNormals.Calculate;   // curseurs de morphologie
            mi.importVisibility = false;
            mi.isReadable = false;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.CalculateMikk;
            mi.optimizeGameObjects = false;                       // garde les points d'attache visibles
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
            foreach (var kv in mats)
                mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);

            switch (m.rig)
            {
                case "Human": mi.animationType = ModelImporterAnimationType.Human; mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; break;
                case "Generic":
                    mi.animationType = ModelImporterAnimationType.Generic; mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    if (!string.IsNullOrEmpty(m.avatarSource)) mi.motionNodeName = m.avatarSource;
                    break;
                default: mi.animationType = ModelImporterAnimationType.None; break;
            }
            mi.importAnimation = m.clips.Length > 0;
            mi.SaveAndReimport();                                 // nécessaire pour obtenir les clips par défaut

            if (m.clips.Length > 0)
            {
                var defs = mi.defaultClipAnimations;
                var outClips = new List<ModelImporterClipAnimation>();
                foreach (var d in defs)
                {
                    var fc = m.clips.FirstOrDefault(c => c.name == d.takeName || c.name == d.name);
                    if (fc == null) continue;
                    d.name = fc.name;
                    d.loopTime = fc.loop; d.loopPose = fc.loop;
                    d.lockRootRotation = true; d.keepOriginalOrientation = true;
                    d.lockRootHeightY = true; d.keepOriginalPositionY = true;
                    d.lockRootPositionXZ = !fc.rootMotion; d.keepOriginalPositionXZ = true;
                    d.events = (fc.events ?? new float[0]).Select((t, k) => new AnimationEvent
                    { time = Mathf.Clamp01(t), functionName = "OnForgeEvent", stringParameter = fc.name + ":" + k }).ToArray();
                    outClips.Add(d);
                }
                mi.clipAnimations = outClips.ToArray();
                mi.SaveAndReimport();
            }
        }

        public static AnimationClip[] Clips(string modelPath) =>
            AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();

        static AnimatorController BuildController(Entry e)
        {
            var dir = e.dir + "/Animation";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(e.dir, "Animation");
            var path = dir + "/" + Path.GetFileNameWithoutExtension(e.m.model) + ".controller";
            AssetDatabase.DeleteAsset(path);                      // reconstruit à chaque fois : aucun état orphelin
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            var clips = Clips(e.modelPath).ToDictionary(c => c.name, c => c);
            AnimationClip C(string n) => clips.TryGetValue(n, out var c) ? c : null;

            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Block", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Dodge", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            var sm = ctrl.layers[0].stateMachine;

            // locomotion : arbre de mélange 1D sur la vitesse (m/s)
            var loco = ctrl.CreateBlendTreeInController("Locomotion", out var tree, 0);
            tree.blendType = BlendTreeType.Simple1D; tree.blendParameter = "Speed"; tree.useAutomaticThresholds = false;
            if (C("Idle")) tree.AddChild(C("Idle"), 0f);
            if (C("Walk")) tree.AddChild(C("Walk"), 1.6f);
            if (C("Run")) tree.AddChild(C("Run"), 4.5f);
            sm.defaultState = loco;

            AnimatorState block = null;
            if (C("Block"))
            {
                block = sm.AddState("Block"); block.motion = C("Block");
                var tin = loco.AddTransition(block); tin.hasExitTime = false; tin.duration = 0.12f; tin.AddCondition(AnimatorConditionMode.If, 0, "Block");
                var tout = block.AddTransition(loco); tout.hasExitTime = false; tout.duration = 0.15f; tout.AddCondition(AnimatorConditionMode.IfNot, 0, "Block");
            }
            void OneShot(string clip, string trigger, float fadeIn, bool canInterrupt)
            {
                if (!C(clip)) return;
                var s = sm.AddState(clip); s.motion = C(clip);
                var t = sm.AddAnyStateTransition(s); t.hasExitTime = false; t.duration = fadeIn; t.canTransitionToSelf = canInterrupt;
                t.AddCondition(AnimatorConditionMode.If, 0, trigger); t.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
                var back = s.AddTransition(loco); back.hasExitTime = true; back.exitTime = 0.88f; back.duration = 0.15f;
            }
            OneShot("Attack", "Attack", 0.08f, true);
            // animations de compétences propres à chaque héros : lancées par le jeu (Animator.CrossFadeInFixedTime), retour automatique
            foreach (var kv in clips.Where(k => k.Key.StartsWith("Sk_")).OrderBy(k => k.Key))
            {
                var s = sm.AddState(kv.Key); s.motion = kv.Value;
                var back = s.AddTransition(loco); back.hasExitTime = true; back.exitTime = 0.92f; back.duration = 0.15f;
            }
            OneShot("Hit", "Hit", 0.05f, true);
            OneShot("Dodge", "Dodge", 0.06f, false);
            if (C("Death"))
            {
                var d = sm.AddState("Death"); d.motion = C("Death");
                var t = sm.AddAnyStateTransition(d); t.hasExitTime = false; t.duration = 0.1f; t.canTransitionToSelf = false;
                t.AddCondition(AnimatorConditionMode.If, 0, "Dead");
                var up = d.AddTransition(loco); up.hasExitTime = false; up.duration = 0.3f; up.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            }
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            return ctrl;
        }

        static void BuildPrefab(Entry e, AnimatorController ctrl)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(e.modelPath);
            if (model == null) { Debug.LogError($"[ModelForge] Impossible de charger {e.modelPath}"); return; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            go.name = Path.GetFileNameWithoutExtension(e.m.prefab);
            try
            {
                var anim = go.GetComponent<Animator>();
                if (anim != null)
                {
                    anim.runtimeAnimatorController = ctrl;
                    anim.applyRootMotion = false;
                    anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                }
                foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>()) { smr.updateWhenOffscreen = false; smr.quality = SkinQuality.Bone4; }

                foreach (var a in e.m.attachments)
                {
                    var socket = go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == a.socket);
                    var apath = NormalizePath(e.dir + "/" + a.model);
                    var prefabPath = FindPrefabFor(apath);
                    var src = prefabPath != null ? AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) : AssetDatabase.LoadAssetAtPath<GameObject>(apath);
                    if (socket == null || src == null) { Debug.LogError($"[ModelForge] Attache impossible : {a.socket} ← {apath}"); continue; }
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(src, socket);
                    inst.transform.localPosition = Vector3.zero; inst.transform.localRotation = Quaternion.identity; inst.transform.localScale = Vector3.one;
                }

                var c = e.m.collider;
                if (c != null && c.type == "capsule")
                {
                    var cc = go.AddComponent<CapsuleCollider>();
                    cc.center = V3(c.center); cc.height = c.height; cc.radius = c.radius; cc.direction = 1; cc.isTrigger = c.trigger;
                }
                else if (c != null && c.type == "mesh")
                {
                    // décor (château, rochers) : collision exacte sur chaque maillage
                    foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                    { var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; }
                    go.isStatic = true;
                    foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
                }
                else if (c != null && c.type == "box")
                {
                    var bc = go.AddComponent<BoxCollider>();
                    bc.center = V3(c.center); bc.size = V3(c.size); bc.isTrigger = c.trigger;
                }
                if (e.m.kind != "prop" && e.m.kind != "part")
                {
                    var rig = go.AddComponent<CharacterRig>();
                    rig.Collect();
                    // pièces d'équipement séparées (SkinnedMeshRenderer autres que « *_Mesh ») : composant d'équipement
                    bool hasSlots = false;
                    foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (!smr.name.EndsWith("_Mesh")) hasSlots = true;
                    if (hasSlots)
                    {
                        var eq = go.AddComponent<CharacterEquipment>();
                        eq.Collect();
                        if (e.m.slots != null) foreach (var sl in e.m.slots) eq.SetHides(sl.name, sl.hides);
                        eq.Apply();
                    }
                }
                var dir = Path.GetDirectoryName(e.m.prefab).Replace('\\', '/');
                EnsureFolder(dir);
                PrefabUtility.SaveAsPrefabAsset(go, e.m.prefab);
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>Prefab déjà construit pour un modèle (manifeste dont le champ model correspond).</summary>
        static string FindPrefabFor(string modelPath)
        {
            foreach (var e in FindAll())
                if (NormalizePath(e.modelPath) == modelPath && !string.IsNullOrEmpty(e.m.prefab) && File.Exists(e.m.prefab)) return e.m.prefab;
            return null;
        }

        public static string NormalizePath(string p)
        {
            var parts = new List<string>();
            foreach (var s in p.Replace('\\', '/').Split('/'))
            {
                if (s == "..") { if (parts.Count > 0) parts.RemoveAt(parts.Count - 1); }
                else if (s != "." && s.Length > 0) parts.Add(s);
            }
            return string.Join("/", parts);
        }

        public static void EnsureFolder(string dir)
        {
            if (AssetDatabase.IsValidFolder(dir)) return;
            var parent = Path.GetDirectoryName(dir).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(dir));
        }

        static Vector3 V3(float[] a) => a == null || a.Length < 3 ? Vector3.zero : new Vector3(a[0], a[1], a[2]);
    }
}
