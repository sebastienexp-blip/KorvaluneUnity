using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Korvalune.EditorTools
{
    /// <summary>
    /// Construit le catalogue du créateur de personnage (Data/CharacterCatalog.asset) à partir d'Art/Catalog/catalog.json,
    /// des modèles de pièces (Art/Catalog/Models/{M,F}) et des prefabs de classes (Prefabs/Characters/&lt;Classe&gt;_{M,F}),
    /// puis la scène CharacterCreator.unity.
    /// </summary>
    public static class CatalogBuilder
    {
        public const string CatalogDir = ModelPipeline.Root + "/Art/Catalog";
        public const string AssetPath = ModelPipeline.Root + "/Data/CharacterCatalog.asset";
        public const string ScenePath = ModelPipeline.Root + "/Scenes/CharacterCreator.unity";

        [Serializable] class JVariant { public string id; public string file; public string normal; public string license; public string author; }
        [Serializable] class JEntry
        {
            public string id; public string file; public string normal; public string sex; public string[] tags; public string license; public string author;
            public JVariant[] variants;
        }
        [Serializable] class JCatalog { public JEntry[] skins; public JEntry[] eyes; public JEntry[] hair; public JEntry[] eyebrows; public JEntry[] eyelashes; }

        public static readonly (string id, string label, string desc)[] Classes =
        {
            ("KaelDraven", "Kael Draven — Chevalier déchu", "Armure de plates noire et or, épée et écu."),
            ("ElyraNoctis", "Elyra Noctis — Archimage du Néant", "Mage du Néant, bâton à cristal violet."),
            ("Varek", "Varek — Chasseur des cendres", "Rôdeur encapuchonné, arbalète."),
            ("Morvanna", "Morvanna — Nécromancienne", "Nécromancienne, bâton à crâne et âme verte."),
            ("Tharion", "Tharion — Gardien solaire", "Paladin en plates blanches et or, marteau et pavois."),
        };

        static T Tex<T>(string rel) where T : UnityEngine.Object => string.IsNullOrEmpty(rel) ? null : AssetDatabase.LoadAssetAtPath<T>($"{CatalogDir}/{rel}");

        /// <summary>Réglages d'import des textures du catalogue (normales, transparence des cheveux / cils / sourcils).</summary>
        public static void ConfigureTextures()
        {
            if (!Directory.Exists(CatalogDir)) return;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { CatalogDir }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(p) as TextureImporter; if (ti == null) continue;
                bool changed = false;
                bool normal = p.EndsWith("_Normal.png");
                bool alpha = p.Contains("/Hair/") || p.Contains("/Eyebrows/") || p.Contains("/Eyelashes/");
                if (normal && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; changed = true; }
                if (alpha && !normal && !ti.alphaIsTransparency) { ti.alphaIsTransparency = true; ti.mipMapsPreserveCoverage = true; ti.alphaTestReferenceValue = 0.4f; changed = true; }
                int max = p.Contains("/Skins/") ? 2048 : 1024;
                if (ti.maxTextureSize != max) { ti.maxTextureSize = max; changed = true; }
                if (changed) ti.SaveAndReimport();
            }
        }

        public static CharacterCatalog Build()
        {
            var jpath = $"{CatalogDir}/catalog.json";
            if (!File.Exists(jpath)) return null;
            ConfigureTextures();
            var j = JsonUtility.FromJson<JCatalog>(File.ReadAllText(jpath));
            ModelPipeline.EnsureFolder(Path.GetDirectoryName(AssetPath).Replace('\\', '/'));
            var cat = AssetDatabase.LoadAssetAtPath<CharacterCatalog>(AssetPath);
            if (cat == null) { cat = ScriptableObject.CreateInstance<CharacterCatalog>(); AssetDatabase.CreateAsset(cat, AssetPath); }

            cat.skins = (j.skins ?? new JEntry[0]).Select(e => new CharacterCatalog.Skin
            { id = e.id, baseMap = Tex<Texture2D>(e.file), normal = Tex<Texture2D>(e.normal), sex = e.sex, tags = e.tags ?? new string[0], license = e.license, author = e.author })
                .Where(s => s.baseMap).ToList();
            cat.eyes = (j.eyes ?? new JEntry[0]).Select(e => new CharacterCatalog.Eye { id = e.id, baseMap = Tex<Texture2D>(e.file), license = e.license, author = e.author })
                .Where(s => s.baseMap).ToList();
            cat.hair = Parts("hair", j.hair);
            cat.eyebrows = Parts("eyebrows", j.eyebrows);
            cat.eyelashes = Parts("eyelashes", j.eyelashes);

            cat.classes.Clear();
            foreach (var (id, label, desc) in Classes)
            {
                var m = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelPipeline.Root}/Prefabs/Characters/{id}_M.prefab");
                var f = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelPipeline.Root}/Prefabs/Characters/{id}_F.prefab");
                if (!m && !f) m = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelPipeline.Root}/Prefabs/Characters/{id}.prefab");
                if (m || f) cat.classes.Add(new CharacterCatalog.PlayableClass { id = id, label = label, description = desc, male = m, female = f });
            }
            cat.morphsMale = Morphs("BaseMale"); cat.morphsFemale = Morphs("BaseFemale");
            EditorUtility.SetDirty(cat); AssetDatabase.SaveAssets();
            return cat;
        }

        static List<CharacterCatalog.Part> Parts(string kind, JEntry[] src)
        {
            var list = new List<CharacterCatalog.Part>();
            foreach (var e in src ?? new JEntry[0])
            {
                var p = new CharacterCatalog.Part { id = e.id, sex = e.sex ?? "MF", license = e.license, author = e.author };
                p.male = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelPipeline.Root}/Prefabs/Catalog/{kind}/{e.id}_M.prefab");
                p.female = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelPipeline.Root}/Prefabs/Catalog/{kind}/{e.id}_F.prefab");
                if (e.variants != null)
                    foreach (var v in e.variants) p.variants.Add(new CharacterCatalog.Variant { id = v.id, baseMap = Tex<Texture2D>(v.file), normal = Tex<Texture2D>(v.normal) });
                else if (!string.IsNullOrEmpty(e.file)) p.variants.Add(new CharacterCatalog.Variant { id = "default", baseMap = Tex<Texture2D>(e.file) });
                if (p.male || p.female) list.Add(p);
            }
            return list;
        }

        static List<CharacterCatalog.Morph> Morphs(string baseName)
        {
            var path = Directory.GetFiles(ModelPipeline.Root + "/Art", baseName + ".forge.json", SearchOption.AllDirectories).FirstOrDefault();
            if (path == null) return new List<CharacterCatalog.Morph>();
            var m = JsonUtility.FromJson<ForgeManifest>(File.ReadAllText(path));
            return (m.morphs ?? new ForgeMorph[0]).Select(x => new CharacterCatalog.Morph { id = x.id, group = x.group, label = x.label, face = x.face, bipolar = x.bipolar }).ToList();
        }

        /// <summary>Scène du créateur de personnage : caméra, éclairage de studio, sol, composant CharacterCreator.</summary>
        public static void BuildScene(CharacterCatalog cat)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sun = new GameObject("Soleil").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 2.0f; sun.color = new Color(1f, 0.93f, 0.84f); sun.shadows = LightShadows.Soft; sun.transform.rotation = Quaternion.Euler(35, -30, 0);
            var rim = new GameObject("Contre-jour").AddComponent<Light>();
            rim.type = LightType.Directional; rim.intensity = 1.1f; rim.color = new Color(0.55f, 0.45f, 0.95f); rim.shadows = LightShadows.None; rim.transform.rotation = Quaternion.Euler(15, 160, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.30f, 0.30f, 0.38f); RenderSettings.ambientEquatorColor = new Color(0.16f, 0.15f, 0.18f); RenderSettings.ambientGroundColor = new Color(0.06f, 0.05f, 0.06f);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cylinder); ground.name = "Socle";
            ground.transform.localScale = new Vector3(2.2f, 0.03f, 2.2f); ground.transform.position = new Vector3(0, -0.03f, 0);
            var gp = ModelPipeline.Root + "/Rendering/Showcase_Ground.mat";
            var gm = AssetDatabase.LoadAssetAtPath<Material>(gp); if (gm) ground.GetComponent<Renderer>().sharedMaterial = gm;
            var cam = new GameObject("Caméra").AddComponent<Camera>();
            cam.transform.position = new Vector3(-0.35f, 1.25f, -3.6f); cam.transform.LookAt(new Vector3(-0.35f, 0.95f, 0));
            cam.fieldOfView = 34; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.07f, 0.06f, 0.09f); cam.tag = "MainCamera";
            var stage = new GameObject("Scène").transform;
            var cc = new GameObject("Créateur de personnage").AddComponent<CharacterCreator>();
            cc.catalog = cat; cc.viewCamera = cam; cc.stage = stage;
            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
