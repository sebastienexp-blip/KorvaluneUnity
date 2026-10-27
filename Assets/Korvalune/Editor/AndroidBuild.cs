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
                var scenes = new[] { SelectionBuilder.ScenePath, CatalogBuilder.ScenePath, WorldBuilder.ScenePath, DungeonBuilder.ScenePath }.Where(File.Exists).ToArray();
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
                int build = int.TryParse(run, out var n) ? n : 1;
                PlayerSettings.bundleVersion = "0.1." + build; PlayerSettings.Android.bundleVersionCode = build;
                EditorUserBuildSettings.buildAppBundle = false;
                EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
                AssetDatabase.SaveAssets();

                Directory.CreateDirectory(Path.GetDirectoryName(Out));
                var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = scenes, locationPathName = Out, target = BuildTarget.Android, targetGroup = BuildTargetGroup.Android, options = BuildOptions.None });
                var s = r.summary;
                rep.AppendLine($"- Résultat : **{s.result}** en {s.totalTime.TotalMinutes:0.0} min, {s.totalErrors} erreur(s), {s.totalWarnings} avertissement(s)");
                if (File.Exists(Out)) rep.AppendLine($"- APK : {new FileInfo(Out).Length / 1048576.0:0.0} Mo, version {PlayerSettings.bundleVersion}");
                foreach (var st in r.steps) foreach (var m in st.messages.Where(m => m.type == LogType.Error || m.type == LogType.Exception).Take(20))
                    rep.AppendLine($"- ❌ {st.name} : {m.content.Split('\n')[0]}");
                code = s.result == BuildResult.Succeeded && File.Exists(Out) ? 0 : 1;
            }
            catch (Exception ex) { rep.AppendLine("- ❌ " + ex); }
            File.WriteAllText("ci-out/android/build.md", rep.ToString());
            Debug.Log(rep.ToString());
            EditorApplication.Exit(code);
        }
    }
}
