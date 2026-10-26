using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Korvalune.EditorTools
{
    /// <summary>
    /// Point d'entrée de l'intégration continue (GitHub Actions / GameCI) :
    ///   -executeMethod Korvalune.EditorTools.CI.Run
    /// Importe et intègre tous les modèles ModelForge, construit la scène vitrine, vérifie chaque modèle
    /// (import, échelle, orientation, avatar, clips, matériaux, points d'attache, collisions), tente des captures,
    /// écrit ci-report.md à la racine du projet et quitte avec le code 1 en cas d'échec.
    /// </summary>
    public static class CI
    {
        static readonly List<string> Logs = new List<string>();
        static readonly StringBuilder Rep = new StringBuilder();
        static int failures;

        public static void Run()
        {
            Application.logMessageReceived += OnLog;
            try
            {
                Line($"# Rapport d'intégration Unity — {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
                Line($"- Unity {Application.unityVersion}, plateforme active {EditorUserBuildSettings.activeBuildTarget}, rendu {SystemInfo.graphicsDeviceType}");
                var pipe = ProjectSetup.EnsureUrp();
                Check("Render Pipeline", GraphicsSettings.defaultRenderPipeline == pipe && pipe != null, pipe != null ? $"URP actif ({pipe.name})" : "URP absent");
                var entries = ModelPipeline.FindAll();
                Line($"- {entries.Count} modèle(s) ModelForge trouvé(s)");
                try { File.Delete("ci-out/progress.log"); } catch { }
                // pièces du créateur en dernier ; mémoire libérée régulièrement (les 140 pièces à blendshapes saturaient l'éditeur)
                entries = entries.OrderBy(x => x.m.kind == "part" ? 1 : 0).ToList();
                int n = 0;
                foreach (var e in entries)
                {
                    Progress("traitement " + e.modelPath);
                    if (++n % 8 == 0) { AssetDatabase.SaveAssets(); EditorUtility.UnloadUnusedAssetsImmediate(); GC.Collect(); }
                    try { ModelPipeline.Process(e); }
                    catch (Exception ex) { Check($"Traitement {e.m.model}", false, ex.ToString()); }
                }
                AssetDatabase.SaveAssets();
                int v = 0; foreach (var e in entries.Where(x => x.m.kind != "part")) { Validate(e); if (++v % 4 == 0) { EditorUtility.UnloadUnusedAssetsImmediate(); GC.Collect(); } }
                ValidateParts(entries.Where(x => x.m.kind == "part").ToList());
                BuildShowcase(entries);
                Captures(entries);
                CharacterCreatorCheck();
                WorldCheck();
            }
            catch (Exception ex) { Check("Exécution CI", false, ex.ToString()); }
            finally
            {
                Application.logMessageReceived -= OnLog;
                var errs = Logs.Where(l => l.StartsWith("Error") || l.StartsWith("Exception") || l.StartsWith("Assert")).Distinct().Take(40).ToList();
                var warns = Logs.Where(l => l.StartsWith("Warning")).Distinct().Take(40).ToList();
                Line("\n## Journal Unity (erreurs et avertissements)");
                if (errs.Count == 0 && warns.Count == 0) Line("Aucun.");
                foreach (var l in errs) Line("- ❌ " + Trim(l));
                foreach (var l in warns) Line("- ⚠️ " + Trim(l));
                Line(failures == 0 ? "\n**RÉSULTAT : tout est valide.**" : $"\n**RÉSULTAT : {failures} vérification(s) en échec.**");
                File.WriteAllText("ci-report.md", Rep.ToString());
                Debug.Log(Rep.ToString());
            }
            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        static void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Log) return;
            if (msg.Contains("Rapport d'intégration")) return;
            Progress(type + ": " + msg.Split('\n')[0]);
            Logs.Add(type + ": " + msg.Split('\n')[0]);
        }

        static string Trim(string s) => s.Length > 300 ? s.Substring(0, 300) + "…" : s;
        static void Line(string s) { Rep.AppendLine(s); Progress(s); }
        /// <summary>Journal de progression écrit au fil de l'eau (survit à un plantage natif de l'éditeur).</summary>
        static void Progress(string s) { try { Directory.CreateDirectory("ci-out"); File.AppendAllText("ci-out/progress.log", DateTime.UtcNow.ToString("HH:mm:ss ") + s + "\n"); } catch { } }
        static void Check(string what, bool ok, string detail = "")
        {
            if (!ok) failures++;
            Line($"- {(ok ? "✅" : "❌")} **{what}** {detail}");
        }

        // ------------------------------------------------------------------------------------------ vérifications
        static void Validate(ModelPipeline.Entry e)
        {
            var m = e.m;
            Line($"\n## {m.model} ({m.kind}, rig {m.rig})");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(e.modelPath);
            Check("Import du FBX", model != null, e.modelPath);
            if (model == null) return;
            var fname = Path.GetFileName(e.modelPath);
            var importIssues = Logs.Where(l => l.Contains(fname) && !l.StartsWith("Log")).Distinct().ToList();
            Check("Import sans erreur ni avertissement", importIssues.Count == 0, importIssues.Count == 0 ? "" : string.Join(" | ", importIssues.Take(5).Select(Trim)));

            int tris = 0, verts = 0;
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh) { tris += mf.sharedMesh.triangles.Length / 3; verts += mf.sharedMesh.vertexCount; }
            foreach (var sm in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (sm.sharedMesh) { tris += sm.sharedMesh.triangles.Length / 3; verts += sm.sharedMesh.vertexCount; }
            var ex = m.expect ?? new ForgeExpect();
            Check("Budget de triangles", ex.maxTriangles <= 0 || tris <= ex.maxTriangles, $"{tris} triangles, {verts} sommets (max {ex.maxTriangles})");

            var src = !string.IsNullOrEmpty(m.prefab) ? AssetDatabase.LoadAssetAtPath<GameObject>(m.prefab) : model;
            if (!string.IsNullOrEmpty(m.prefab)) Check("Prefab créé", src != null, m.prefab);
            if (src == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
            try
            {
                var own = go.GetComponentsInChildren<Renderer>(true).Where(r => IsOwnRenderer(r, go, m)).ToArray();
                var b = Bounds(own);
                Check("Échelle (hauteur)", ex.heightMax <= 0 || (b.size.y >= ex.heightMin && b.size.y <= ex.heightMax),
                    $"{b.size.y:0.000} m (attendu {ex.heightMin}–{ex.heightMax}), boîte {b.size.x:0.00}×{b.size.y:0.00}×{b.size.z:0.00}, pieds à y={b.min.y:0.000}");
                Transform Find(string n) => string.IsNullOrEmpty(n) ? null : go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n);
                if (!string.IsNullOrEmpty(ex.leftBone)) { var t = Find(ex.leftBone); Check("Orientation gauche/droite", t != null && t.position.x < 0, t ? $"{ex.leftBone}.x = {t.position.x:0.000}" : "os absent"); }
                if (!string.IsNullOrEmpty(ex.facingBone))
                {
                    var f = Find(ex.facingBone); var bh = Find(ex.behindBone);
                    Check("Orientation avant (+Z)", f && bh && f.position.z > bh.position.z, f && bh ? $"{ex.facingBone}.z={f.position.z:0.000} > {ex.behindBone}.z={bh.position.z:0.000}" : "os absent");
                }
                foreach (var s in ex.sockets ?? new string[0]) Check($"Point d'attache {s}", Find(s) != null);
                foreach (var a in m.attachments) { var s = Find(a.socket); Check($"Équipement posé sur {a.socket}", s && s.childCount > 0, a.model); }

                // matériaux
                var expected = new HashSet<string>(m.materials.Select(x => x.name));
                foreach (var r in own)
                    foreach (var mat in r.sharedMaterials)
                    {
                        bool ok = mat != null && expected.Contains(mat.name) && mat.shader.name == "Universal Render Pipeline/Lit" && mat.GetTexture("_BaseMap") != null;
                        Check($"Matériau {(mat ? mat.name : "(vide)")}", ok, mat ? $"shader {mat.shader.name}, base {(mat.GetTexture("_BaseMap") ? mat.GetTexture("_BaseMap").name : "—")}, normale {(mat.IsKeywordEnabled("_NORMALMAP") ? "oui" : "non")}, métal/lissé {(mat.IsKeywordEnabled("_METALLICSPECGLOSSMAP") ? "oui" : "non")}, AO {(mat.IsKeywordEnabled("_OCCLUSIONMAP") ? "oui" : "non")}" : "");
                    }

                // collisions
                if (m.collider != null && m.collider.type != "none") Check("Collider", go.GetComponent<Collider>() != null, m.collider.type);
                var eq = go.GetComponent<CharacterEquipment>();
                if (eq != null)
                {
                    var names = string.Join(", ", eq.Slots.Select(sl => sl.name + (sl.renderer != null && sl.renderer.enabled ? " (visible)" : " (masqué)")));
                    Check("Équipement détachable", eq.Slots.Count > 0 && eq.Slots.All(sl => sl.renderer != null), $"{eq.Slots.Count} pièce(s) : {names}");
                }

                // rig et animations
                if (m.rig != "None")
                {
                    var anim = go.GetComponent<Animator>();
                    var av = anim ? anim.avatar : null;
                    Check("Avatar", av != null && av.isValid && (m.rig != "Human" || av.isHuman), av ? $"valide={av.isValid}, humain={av.isHuman}, {av.humanDescription.human.Length} os mappés" : "absent");
                    if (av != null && m.rig == "Human")
                    {
                        var mapped = new HashSet<string>(av.humanDescription.human.Select(h => h.humanName));
                        var missing = new List<string>();
                        for (int i = 0; i < HumanTrait.BoneCount; i++) if (HumanTrait.RequiredBone(i) && !mapped.Contains(HumanTrait.BoneName[i])) missing.Add(HumanTrait.BoneName[i]);
                        Check("Os humanoïdes obligatoires", missing.Count == 0, missing.Count == 0 ? "tous mappés" : "manquants : " + string.Join(", ", missing));
                        int fingers = av.humanDescription.human.Count(h => h.humanName.Contains("Proximal") || h.humanName.Contains("Intermediate") || h.humanName.Contains("Distal"));
                        Line($"  - doigts mappés : {fingers}/30");
                    }
                    Check("Contrôleur d'animation", string.IsNullOrEmpty(m.controller) || (anim && anim.runtimeAnimatorController), anim && anim.runtimeAnimatorController ? anim.runtimeAnimatorController.name : "");
                    var clips = ModelPipeline.Clips(e.modelPath).ToDictionary(c => c.name, c => c);
                    foreach (var fc in m.clips)
                    {
                        clips.TryGetValue(fc.name, out var clip);
                        if (clip == null) { Check($"Clip {fc.name}", false, "absent"); continue; }
                        // échantillonnage : la pose doit changer et rester finie
                        var bones = go.GetComponentsInChildren<Transform>(true);
                        var before = bones.Select(t => t.position).ToArray();
                        clip.SampleAnimation(go, clip.length * 0.5f);
                        var after = bones.Select(t => t.position).ToArray();
                        float moved = 0; bool finite = true;
                        for (int i = 0; i < bones.Length; i++) { moved = Mathf.Max(moved, (after[i] - before[i]).magnitude); finite &= !float.IsNaN(after[i].x) && !float.IsInfinity(after[i].y); }
                        Check($"Clip {fc.name}", clip.length > 0 && clip.isLooping == fc.loop && finite && moved > 0.005f,
                            $"{clip.length:0.00} s, boucle={clip.isLooping}, humanoïde={clip.isHumanMotion}, déplacement max {moved:0.000} m, événements {AnimationUtility.GetAnimationEvents(clip).Length}");
                        clip.SampleAnimation(go, 0f);
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        static bool IsOwnRenderer(Renderer r, GameObject root, ForgeManifest m)
        {
            // exclut les équipements posés (ils ont leurs propres vérifications)
            var t = r.transform;
            while (t != null && t != root.transform) { if (t.name.StartsWith("Socket_")) return false; t = t.parent; }
            return true;
        }

        static Bounds Bounds(Renderer[] rs)
        {
            if (rs.Length == 0) return new Bounds();
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
        }

        // ------------------------------------------------------------------------------------------ pièces du créateur
        static void ValidateParts(List<ModelPipeline.Entry> parts)
        {
            if (parts.Count == 0) return;
            Line($"\n## Pièces du créateur de personnage ({parts.Count} modèles : cheveux, sourcils, cils)");
            int ok = 0, shapes = 0; var bad = new List<string>();
            foreach (var e in parts)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(e.modelPath);
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>(e.m.prefab);
                var smr = model ? model.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
                if (model && pf && smr && smr.sharedMesh) { ok++; shapes = Math.Max(shapes, smr.sharedMesh.blendShapeCount); }
                else bad.Add(Path.GetFileName(e.modelPath));
            }
            Check("Pièces importées (modèle + prefab + peau)", bad.Count == 0, $"{ok}/{parts.Count}" + (bad.Count > 0 ? " — échecs : " + string.Join(", ", bad.Take(10)) : "") + $", jusqu'à {shapes} blendshapes");
        }

        static void CharacterCreatorCheck()
        {
            var cat = CatalogBuilder.Build();
            if (cat == null) { Line("\n_Pas de catalogue de personnalisation (Art/Catalog/catalog.json absent)._"); return; }
            Line("\n## Créateur de personnage");
            Check("Catalogue", cat.skins.Count > 0 && cat.eyes.Count > 0, $"{cat.classes.Count} classes, {cat.skins.Count} peaux, {cat.eyes.Count} yeux, {cat.hair.Count} coiffures ({cat.hair.Sum(h => h.variants.Count)} variantes), {cat.eyebrows.Count} sourcils, {cat.eyelashes.Count} cils, {cat.morphsMale.Count}/{cat.morphsFemale.Count} curseurs homme/femme");
            Check("Classes jouables en homme et en femme", cat.classes.Count > 0 && cat.classes.All(c => c.male && c.female), string.Join(", ", cat.classes.Select(c => $"{c.id} [{(c.male ? "H" : "-")}{(c.female ? "F" : "-")}]")));
            CatalogBuilder.BuildScene(cat);
            Check("Scène du créateur", File.Exists(CatalogBuilder.ScenePath), CatalogBuilder.ScenePath);
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            // captures : personnage par défaut puis apparence aléatoire, homme et femme
            var cc = UnityEngine.Object.FindFirstObjectByType<Korvalune.CharacterCreator>();
            var cam = cc.viewCamera;
            void Shot(string name)
            {
                Canvas.ForceUpdateCanvases();
                var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
                var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
                RenderTexture.active = null; cam.targetTexture = null;
                File.WriteAllBytes($"ci-out/Createur_{name}.png", tex.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(tex); rt.Release();
            }
            try
            {
                cc.Init(); Shot("1_defaut");
                cc.ShowTab("Visage"); cc.Randomize(); Shot("2_aleatoire_homme");
                cc.female = true; cc.ShowTab("Cheveux"); cc.Randomize(); Shot("3_aleatoire_femme");
                cc.classIndex = 1; cc.Rebuild(); cc.ShowTab("Équipement"); Shot("4_classe2_femme");
                Check("Captures du créateur", true, "ci-out/Createur_*.png");
            }
            catch (Exception ex) { Check("Captures du créateur", false, ex.Message); }
        }

        static void WorldCheck()
        {
            Line("\n## Vallée de Korvalune (tranche jouable)");
            if (!AssetDatabase.IsValidFolder(ModelPipeline.Root + "/Art/World")) { Line("_Décor absent (Art/World)._"); return; }
            var game = WorldBuilder.Build();
            Check("Scène de la vallée", File.Exists(WorldBuilder.ScenePath), WorldBuilder.ScenePath);
            var t = Terrain.activeTerrain;
            Check("Terrain", t != null && t.terrainData.terrainLayers.Length == 5 && t.terrainData.treeInstanceCount > 500,
                t ? $"{t.terrainData.size}, {t.terrainData.terrainLayers.Count(l => l && l.diffuseTexture)} couches texturées, {t.terrainData.treeInstanceCount} arbres" : "absent");
            Check("Château", GameObject.Find("ChateauKorvalune") != null);
            var cfg = game.config;
            Check("Héros jouable", cfg.defaultHero != null, cfg.defaultHero ? cfg.defaultHero.name + $" ({cfg.heroes.Count} héros)" : "aucun prefab");
            Check("Ennemis configurés", cfg.enemies.All(e => e.prefab), string.Join(", ", cfg.enemies.Select(e => e.id + (e.prefab ? "" : " (absent)"))));
            WorldBuilder.Captures(game, (w, ok, d) => Check(w, ok, d));
            Check("Héros posé sur le sol", game.Player && Mathf.Abs(game.Player.transform.position.y - WorldBuilder.Height(game.Player.transform.position.x, game.Player.transform.position.z)) < 1.5f,
                game.Player ? $"y = {game.Player.transform.position.y:0.0}" : "");
        }

        // ------------------------------------------------------------------------------------------ scène vitrine
        const string ShowcasePath = ModelPipeline.Root + "/Scenes/Showcase.unity";

        static void BuildShowcase(List<ModelPipeline.Entry> entries)
        {
            ModelPipeline.EnsureFolder(ModelPipeline.Root + "/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupStage(out var cam);
            float x = 0;
            foreach (var e in entries.Where(e => e.m.kind != "prop" && e.m.kind != "part" && !string.IsNullOrEmpty(e.m.prefab)))
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(e.m.prefab);
                if (!p) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(p);
                go.transform.position = new Vector3(x, 0, 0); go.transform.rotation = Quaternion.Euler(0, 180, 0);
                var cyc = go.AddComponent<Korvalune.ShowcaseCycler>(); cyc.rig = go.GetComponent<Korvalune.CharacterRig>();
                x += e.m.kind == "creature" ? 4.5f : 2.5f;
            }
            EditorSceneManager.SaveScene(scene, ShowcasePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ShowcasePath, true) };
            Check("Scène vitrine", File.Exists(ShowcasePath), ShowcasePath);
        }

        static void SetupStage(out Camera cam)
        {
            var sun = new GameObject("Soleil").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 2.2f; sun.color = new Color(1f, 0.92f, 0.82f);
            sun.shadows = LightShadows.Soft; sun.transform.rotation = Quaternion.Euler(42, -35, 0);
            var fill = new GameObject("Contre-jour").AddComponent<Light>();
            fill.type = LightType.Directional; fill.intensity = 0.7f; fill.color = new Color(0.55f, 0.65f, 1f); fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(20, 150, 0);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.32f, 0.34f, 0.40f); RenderSettings.ambientEquatorColor = new Color(0.2f, 0.19f, 0.2f); RenderSettings.ambientGroundColor = new Color(0.09f, 0.08f, 0.07f);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane); ground.name = "Sol"; ground.transform.localScale = new Vector3(4, 1, 4);
            var gm = new Material(Shader.Find("Universal Render Pipeline/Lit")); gm.SetColor("_BaseColor", new Color(0.16f, 0.15f, 0.14f)); gm.SetFloat("_Smoothness", 0.2f);
            ModelPipeline.EnsureFolder(ModelPipeline.Root + "/Rendering");
            var gp = ModelPipeline.Root + "/Rendering/Showcase_Ground.mat";
            if (!File.Exists(gp)) AssetDatabase.CreateAsset(gm, gp);
            ground.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(gp) ?? gm;
            cam = new GameObject("Caméra").AddComponent<Camera>();
            cam.transform.position = new Vector3(0, 1.6f, -4.2f); cam.transform.LookAt(new Vector3(0, 1.0f, 0));
            cam.fieldOfView = 32; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.10f, 0.10f, 0.12f);
            cam.gameObject.tag = "MainCamera";
        }

        // ------------------------------------------------------------------------------------------ captures
        static void Captures(List<ModelPipeline.Entry> entries)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { Line("\n_Captures : aucun processeur graphique dans ce conteneur (Unity lancé sans rendu)._"); return; }
            Directory.CreateDirectory("ci-out");
            foreach (var e in entries.Where(x => !string.IsNullOrEmpty(x.m.prefab) && x.m.kind != "part"))
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(e.m.prefab);
                if (!p) continue;
                var clips = ModelPipeline.Clips(e.modelPath);
                var poses = new List<(string, AnimationClip, float)> { ("repos", null, 0) };
                foreach (var c in clips) poses.Add((c.name, c, c.name == "Death" ? c.length : c.length * 0.4f));
                foreach (var (label, clip, time) in poses)
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    SetupStage(out var cam);
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(p);
                    go.transform.rotation = Quaternion.Euler(0, 200, 0);
                    if (clip) clip.SampleAnimation(go, time);
                    var b = Bounds(go.GetComponentsInChildren<Renderer>());
                    float h = Mathf.Max(b.size.y, b.size.x * 0.8f, 0.5f);
                    cam.transform.position = b.center + new Vector3(0, h * 0.12f, -h * 2.1f); cam.transform.LookAt(b.center);
                    var rt = new RenderTexture(768, 960, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                    cam.targetTexture = rt; cam.Render();
                    RenderTexture.active = rt;
                    var tex = new Texture2D(768, 960, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 768, 960), 0, 0); tex.Apply();
                    RenderTexture.active = null; cam.targetTexture = null;
                    File.WriteAllBytes($"ci-out/{Path.GetFileNameWithoutExtension(e.m.model)}_{label}.png", tex.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(tex); rt.Release();
                }
            }
            Line($"\n_Captures écrites dans ci-out/ ({SystemInfo.graphicsDeviceType})._");
        }
    }
}
