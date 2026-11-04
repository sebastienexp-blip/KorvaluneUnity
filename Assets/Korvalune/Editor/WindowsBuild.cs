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
    /// <summary>Construction de la version Windows (GitHub Actions, workflow « windows.yml ») : Windows 64 bits, Mono,
    /// toutes les scènes et tout le contenu inclus (pas de paquets à télécharger : c'est le lanceur Windows qui installe et
    /// met à jour le jeu entier), plein écran fenêtré à la résolution de l'écran, qualité Ultra choisie à l'exécution
    /// (GraphicsQuality). Sortie : build/Windows/Korvalune.exe ; rapport : ci-out/windows/build.md.</summary>
    public static class WindowsBuild
    {
        const string Out = "build/Windows/Korvalune.exe";

        public static void Build()
        {
            var rep = new StringBuilder("# Construction de la version Windows\n\n");
            int code = 1;
            try
            {
                Directory.CreateDirectory("ci-out/windows");
                ProjectSetup.EnsureUrp(); ProjectSetup.EnsureMobile();
                // scènes : reconstruites si l'intégration ne les a pas encore produites (mêmes scènes que l'APK)
                if (!File.Exists(LauncherBuilder.ScenePath)) LauncherBuilder.Build();
                if (!File.Exists(CatalogBuilder.ScenePath)) { var cat = CatalogBuilder.Build(); if (cat) CatalogBuilder.BuildScene(cat); }
                if (!File.Exists(WorldBuilder.ScenePath)) WorldBuilder.Build();
                if (!File.Exists(SelectionBuilder.ScenePath)) SelectionBuilder.Build();
                if (!File.Exists(DungeonBuilder.ScenePath)) DungeonBuilder.Build();
                if (!File.Exists(EclipseBuilder.ScenePath)) EclipseBuilder.Build();
                if (!File.Exists(BrecheBuilder.ScenePath)) BrecheBuilder.Build();
                foreach (var seg in SegmentBuilder.All) if (!File.Exists(seg.ScenePath)) SegmentBuilder.Build(seg);
                var scenes = new[] { LauncherBuilder.ScenePath, SelectionBuilder.ScenePath, CatalogBuilder.ScenePath, WorldBuilder.ScenePath, DungeonBuilder.ScenePath, CaveBuilder.ScenePath, EclipseBuilder.ScenePath, BrecheBuilder.ScenePath }.Concat(SegmentBuilder.All.Select(d => d.ScenePath)).Where(File.Exists).ToArray();
                rep.AppendLine("- Scènes : " + string.Join(", ", scenes.Select(Path.GetFileNameWithoutExtension)));
                if (scenes.Length < 3) throw new Exception("scène manquante");
                EditorBuildSettings.scenes = scenes.Select(s => new EditorBuildSettingsScene(s, true)).ToArray();

                var t = NamedBuildTarget.Standalone;
                PlayerSettings.companyName = "Korvalune Studio"; PlayerSettings.productName = "Korvalune";
                PlayerSettings.SetApplicationIdentifier(t, "com.korvalune.game");
                PlayerSettings.SetScriptingBackend(t, ScriptingImplementation.Mono2x);    // IL2CPP Windows impossible depuis Linux
                PlayerSettings.SetManagedStrippingLevel(t, ManagedStrippingLevel.Disabled);
                PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;          // plein écran fenêtré (Alt+Entrée pour une fenêtre)
                PlayerSettings.defaultIsNativeResolution = true;
                PlayerSettings.resizableWindow = true;
                PlayerSettings.runInBackground = true;
                PlayerSettings.visibleInBackground = true;
                PlayerSettings.usePlayerLog = true;
                PlayerSettings.allowFullscreenSwitch = true;
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, true);   // DirectX 11/12
                var run = Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER");
                if (string.IsNullOrEmpty(run) && File.Exists("ci-build-number.txt")) run = File.ReadAllText("ci-build-number.txt").Trim();
                int build = int.TryParse(run, out var n) ? n : 1;
                PlayerSettings.bundleVersion = "0.1." + build;
                var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(LauncherBuilder.ArtDir + "/Icone.png");
                if (icon)
                {
                    PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
                    rep.AppendLine("- Icône : " + LauncherBuilder.ArtDir + "/Icone.png");
                }
                AssetDatabase.SaveAssets();

                Directory.CreateDirectory(Path.GetDirectoryName(Out));
                var r = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes, locationPathName = Out, target = BuildTarget.StandaloneWindows64,
                    targetGroup = BuildTargetGroup.Standalone, options = BuildOptions.CompressWithLz4HC
                });
                var s = r.summary;
                rep.AppendLine($"- Résultat : **{s.result}** en {s.totalTime.TotalMinutes:0.0} min, {s.totalErrors} erreur(s), {s.totalWarnings} avertissement(s)");
                if (File.Exists(Out))
                {
                    long total = new DirectoryInfo(Path.GetDirectoryName(Out)).GetFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
                    rep.AppendLine($"- Jeu : {total / 1048576.0:0} Mo, version {PlayerSettings.bundleVersion}");
                }
                foreach (var st in r.steps) foreach (var m in st.messages.Where(m => m.type == LogType.Error || m.type == LogType.Exception).Take(20))
                    rep.AppendLine($"- ❌ {st.name} : {m.content.Split('\n')[0]}");
                try
                {
                    var all = r.packedAssets.SelectMany(p => p.contents).ToList();
                    rep.AppendLine($"\n## Contenu empaqueté : {all.Sum(c => (long)c.packedSize) / 1048576.0:0} Mo");
                    foreach (var g in all.GroupBy(c => c.type?.Name ?? "?").OrderByDescending(g => g.Sum(c => (long)c.packedSize)).Take(12))
                        rep.AppendLine($"- {g.Key} : {g.Sum(c => (long)c.packedSize) / 1048576.0:0.0} Mo ({g.Count()})");
                }
                catch (Exception ex) { rep.AppendLine("- (rapport de contenu indisponible : " + ex.Message + ")"); }
                code = s.result == BuildResult.Succeeded && File.Exists(Out) ? 0 : 1;
            }
            catch (Exception ex) { rep.AppendLine("- ❌ " + ex); }
            File.WriteAllText("ci-out/windows/build.md", rep.ToString());
            Debug.Log(rep.ToString());
            EditorApplication.Exit(code);
        }
    }
}
