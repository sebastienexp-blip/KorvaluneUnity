using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Korvalune.EditorTools
{
    /// <summary>Construction de l'APK Android (GitHub Actions) : scènes Sélection → Créateur → Vallée, IL2CPP ARM64 (téléphones)
    /// + x86_64 (émulateur de test), paysage, qualité choisie à l'exécution (GraphicsQuality). Rapport : ci-out/android/build.md.</summary>
    public static class AndroidBuild
    {
        const string Out = "build/Android/Korvalune.apk";

        public static void Build()
        {
            var rep = new StringBuilder("# Construction de l'APK Android\n\n");
            int code = 1;
            try
            {
                Directory.CreateDirectory("ci-out/android");
                ProjectSetup.EnsureUrp(); ProjectSetup.EnsureMobile();
                // scènes : reconstruites si l'intégration ne les a pas encore produites
                if (!File.Exists(CatalogBuilder.ScenePath)) { var cat = CatalogBuilder.Build(); if (cat) CatalogBuilder.BuildScene(cat); }
                if (!File.Exists(WorldBuilder.ScenePath)) WorldBuilder.Build();
                if (!File.Exists(SelectionBuilder.ScenePath)) SelectionBuilder.Build();
                if (!File.Exists(DungeonBuilder.ScenePath)) DungeonBuilder.Build();
                if (!File.Exists(EclipseBuilder.ScenePath)) EclipseBuilder.Build();
                if (!File.Exists(BrecheBuilder.ScenePath)) BrecheBuilder.Build();
                var scenes = new[] { LauncherBuilder.ScenePath, SelectionBuilder.ScenePath, CatalogBuilder.ScenePath, WorldBuilder.ScenePath, DungeonBuilder.ScenePath, CaveBuilder.ScenePath, EclipseBuilder.ScenePath, BrecheBuilder.ScenePath }.Where(File.Exists).ToArray();
                rep.AppendLine("- Scènes : " + string.Join(", ", scenes.Select(Path.GetFileNameWithoutExtension)));
                if (scenes.Length < 3) throw new Exception("scène manquante");
                EditorBuildSettings.scenes = scenes.Select(s => new EditorBuildSettingsScene(s, true)).ToArray();

                var t = NamedBuildTarget.Android;
                PlayerSettings.companyName = "Korvalune Studio"; PlayerSettings.productName = "Korvalune";
                PlayerSettings.SetApplicationIdentifier(t, "com.korvalune.game");
                PlayerSettings.SetScriptingBackend(t, ScriptingImplementation.IL2CPP);
                PlayerSettings.SetManagedStrippingLevel(t, ManagedStrippingLevel.Low);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.X86_64;
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
                PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
                PlayerSettings.allowedAutorotateToPortrait = false; PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
                PlayerSettings.allowedAutorotateToLandscapeLeft = true; PlayerSettings.allowedAutorotateToLandscapeRight = true;
                PlayerSettings.Android.forceInternetPermission = false;
                var run = Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER");
                if (string.IsNullOrEmpty(run) && File.Exists("ci-build-number.txt")) run = File.ReadAllText("ci-build-number.txt").Trim();   // le conteneur Unity ne reçoit pas les variables GitHub
                int build = int.TryParse(run, out var n) ? n : 1;
                PlayerSettings.bundleVersion = "0.1." + build; PlayerSettings.Android.bundleVersionCode = build;
                EditorUserBuildSettings.buildAppBundle = false;
                // signature fixe (même clé à chaque version) : Android accepte la mise à jour par-dessus l'ancienne version,
                // sans désinstaller — les personnages et réglages sont conservés
                var ks = Path.GetFullPath(".github/keystore/korvalune.keystore"); var ksPass = ".github/keystore/pass.txt";
                if (File.Exists(ks) && File.Exists(ksPass))
                {
                    var pw = File.ReadAllText(ksPass).Trim();
                    PlayerSettings.Android.useCustomKeystore = true; PlayerSettings.Android.keystoreName = ks; PlayerSettings.Android.keystorePass = pw;
                    PlayerSettings.Android.keyaliasName = "korvalune"; PlayerSettings.Android.keyaliasPass = pw;
                    rep.AppendLine("- Signature : clé Korvalune (mises à jour sans désinstaller)");
                }
                else rep.AppendLine("- Signature : clé de débogage (⚠ chaque version demande de désinstaller)");
                PlayerSettings.Android.forceInternetPermission = true;          // vérification et téléchargement des mises à jour
                // icône de l'application (rendue depuis le jeu) et nom affiché
                PlayerSettings.productName = "Korvalune";
                var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(LauncherBuilder.ArtDir + "/Icone.png");
                if (icon)
                {
                    PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);      // icône par défaut (toutes tailles)
                    rep.AppendLine("- Icône : " + LauncherBuilder.ArtDir + "/Icone.png");
                }
                else rep.AppendLine("- Icône : absente (rendue par la CI Unity)");
                EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
                AssetDatabase.SaveAssets();

                // contenu téléchargeable : modèles, textures et scènes en paquets ; l'APK ne garde que le lanceur
                string manifest = null;
                try
                {
                    manifest = BuildContent(scenes, build, rep);
                    if (manifest != null) { scenes = new[] { LauncherBuilder.ScenePath }; EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(LauncherBuilder.ScenePath, true) }; }
                }
                catch (Exception ex) { manifest = null; rep.AppendLine("- ⚠ Paquets de contenu non construits (APK tout-en-un) : " + ex.Message.Split('\n')[0]); }

                Directory.CreateDirectory(Path.GetDirectoryName(Out));
                var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = scenes, locationPathName = Out, target = BuildTarget.Android, targetGroup = BuildTargetGroup.Android, options = BuildOptions.None, assetBundleManifestPath = manifest });
                var s = r.summary;
                rep.AppendLine($"- Résultat : **{s.result}** en {s.totalTime.TotalMinutes:0.0} min, {s.totalErrors} erreur(s), {s.totalWarnings} avertissement(s)");
                if (File.Exists(Out)) rep.AppendLine($"- APK : {new FileInfo(Out).Length / 1048576.0:0.0} Mo, version {PlayerSettings.bundleVersion}");
                foreach (var st in r.steps) foreach (var m in st.messages.Where(m => m.type == LogType.Error || m.type == LogType.Exception).Take(20))
                    rep.AppendLine($"- ❌ {st.name} : {m.content.Split('\n')[0]}");
                // poids du contenu : par type d'objet et les 30 plus gros fichiers (pour garder l'APK léger)
                try
                {
                    var all = r.packedAssets.SelectMany(p => p.contents).ToList();
                    rep.AppendLine($"\n## Contenu empaqueté : {all.Sum(c => (long)c.packedSize) / 1048576.0:0} Mo");
                    foreach (var g in all.GroupBy(c => c.type?.Name ?? "?").OrderByDescending(g => g.Sum(c => (long)c.packedSize)).Take(12))
                        rep.AppendLine($"- {g.Key} : {g.Sum(c => (long)c.packedSize) / 1048576.0:0.0} Mo ({g.Count()})");
                    rep.AppendLine("\n### Plus gros fichiers sources");
                    foreach (var g in all.GroupBy(c => c.sourceAssetPath).OrderByDescending(g => g.Sum(c => (long)c.packedSize)).Take(30))
                        rep.AppendLine($"- {g.Sum(c => (long)c.packedSize) / 1048576.0:0.0} Mo — {g.Key}");
                }
                catch (Exception ex) { rep.AppendLine("- (rapport de contenu indisponible : " + ex.Message + ")"); }
                code = s.result == BuildResult.Succeeded && File.Exists(Out) ? 0 : 1;
            }
            catch (Exception ex) { rep.AppendLine("- ❌ " + ex); }
            File.WriteAllText("ci-out/android/build.md", rep.ToString());
            Debug.Log(rep.ToString());
            EditorApplication.Exit(code);
        }

        const string ContentOut = "build/Contenu";

        /// <summary>Paquets de contenu (AssetBundles LZ4) : un par modèle de héros, de monstre, de monture, d'arme…, un par
        /// dossier de préfabriqués, un pour les scènes. Catalogue « catalogue.json » (nom, adresse, taille, empreinte) publié
        /// avec les paquets dans la release « contenu-N ». Renvoie le manifeste (le compilateur garde le code qu'ils utilisent).</summary>
        static string BuildContent(string[] scenes, int build, StringBuilder rep)
        {
            string root = ModelPipeline.Root, prefabs = root + "/Resources/Prefabs", dst = root + "/Contenu";
            if (AssetDatabase.IsValidFolder(prefabs))
            {
                if (!AssetDatabase.IsValidFolder(dst)) AssetDatabase.CreateFolder(root, "Contenu");
                var err = AssetDatabase.MoveAsset(prefabs, dst + "/Prefabs");             // hors de Resources : plus dans l'APK
                if (!string.IsNullOrEmpty(err)) throw new Exception("déplacement des préfabriqués : " + err);
                AssetDatabase.Refresh();
            }
            var builds = new System.Collections.Generic.List<AssetBundleBuild>();
            string[] skip = { ".meta", ".cs", ".forgehash", ".md", ".py", ".txt" };
            string N(string f) => f.Replace('\\', '/');
            string[] Files(string folder, bool deep) => Directory.Exists(folder)
                ? Directory.GetFiles(folder, "*", deep ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).Select(N).Where(f => !skip.Any(f.EndsWith)).ToArray() : new string[0];
            string Name(string s2) => new string(s2.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
            void Add(string name, string[] files) { if (files.Length > 0) builds.Add(new AssetBundleBuild { assetBundleName = Name(name), assetNames = files }); }
            void Split(string folder, string prefix)
            {
                Add(prefix, Files(folder, false));
                foreach (var d in Directory.Exists(folder) ? Directory.GetDirectories(folder) : new string[0]) Add(prefix + "_" + Path.GetFileName(d), Files(d, true));
            }
            foreach (var top in Directory.GetDirectories(root + "/Art")) Split(N(top), "art_" + Path.GetFileName(top));
            Split(dst + "/Prefabs", "prefabs");
            var sc = scenes.Where(x => x != LauncherBuilder.ScenePath).ToArray();
            if (sc.Length == 0) throw new Exception("aucune scène");
            builds.Add(new AssetBundleBuild { assetBundleName = "scenes", assetNames = sc });
            if (Directory.Exists(ContentOut)) Directory.Delete(ContentOut, true);
            Directory.CreateDirectory(ContentOut);
            var man = BuildPipeline.BuildAssetBundles(ContentOut, builds.ToArray(), BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.Android);
            if (man == null) throw new Exception("échec de la construction des paquets");
            var baseUrl = $"https://github.com/sebastienexp-blip/KorvaluneUnity/releases/download/contenu-{build}/";
            var cat = new Korvalune.Game.Launcher.Catalog { version = "0.1." + build };
            long total = 0;
            foreach (var b in man.GetAllAssetBundles().OrderBy(x => x))
            {
                var f = Path.Combine(ContentOut, b); long size = new FileInfo(f).Length; total += size;
                cat.packs.Add(new Korvalune.Game.Launcher.Pack { name = b, url = baseUrl + b, size = size, hash = man.GetAssetBundleHash(b).ToString() });
            }
            File.WriteAllText(Path.Combine(ContentOut, "catalogue.json"), JsonUtility.ToJson(cat, true));
            rep.AppendLine($"- Contenu téléchargeable : {cat.packs.Count} paquets, {total / 1048576.0:0} Mo (release contenu-{build})");
            foreach (var p in cat.packs.OrderByDescending(x => x.size).Take(12)) rep.AppendLine($"    - {p.name} : {p.size / 1048576.0:0.0} Mo");
            return Path.Combine(ContentOut, "Contenu.manifest");
        }
    }
}
