using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HybridCLR;
using HybridCLR.Commands;
using HybridCLR.Installer;
using HybridCLR.Settings;
using Korvalune.Boot;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Korvalune.EditorTools
{
    /// <summary>Mise à jour du code depuis le jeu (HybridCLR, libre et gratuit, licence MIT) : la bibliothèque du jeu
    /// (Korvalune.Runtime) sort de l'APK et devient un fichier téléchargé au démarrage (Korvalune.Boot.Bootstrap).
    /// Prépare, pour la construction n° build : l'installation de HybridCLR (il2cpp modifié, une fois, gardée dans Library),
    /// la génération (bibliothèque, ponts d'appel, link.xml, métadonnées complémentaires), puis range le code
    /// dans StreamingAssets/code (version livrée avec l'APK) et dans build/Contenu (publié dans « contenu-N »).</summary>
    public static class HotUpdate
    {
        const string GameAssembly = "Korvalune.Runtime";
        const string Embedded = "Assets/StreamingAssets/code";
        const string Publish = "build/Contenu";

        public static void Disable()
        {
            HybridCLRSettings.instance.enable = false; HybridCLRSettings.Save();
            if (Directory.Exists(Embedded)) { FileUtil.DeleteFileOrDirectory(Embedded); FileUtil.DeleteFileOrDirectory(Embedded + ".meta"); AssetDatabase.Refresh(); }
        }

        public static bool Prepare(int build, StringBuilder rep)
        {
            var s = HybridCLRSettings.instance;
            s.enable = true;
            s.hotUpdateAssemblyDefinitions = new AssemblyDefinitionAsset[0];
            s.hotUpdateAssemblies = new[] { GameAssembly };
            s.hybridclrRepoURL = "https://github.com/focus-creative-games/hybridclr";
            s.il2cppPlusRepoURL = "https://github.com/focus-creative-games/il2cpp_plus";
            HybridCLRSettings.Save();
            // le code n'est plus dans l'APK : on garde tout le moteur (voir Boot/link.xml) pour les futures versions du code
            PlayerSettings.stripEngineCode = false;
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.Android, ManagedStrippingLevel.Minimal);

            var inst = new InstallerController();
            if (!inst.HasInstalledHybridCLR() || inst.InstalledLibil2cppVersion != inst.PackageVersion)
            {
                rep.AppendLine($"- HybridCLR : installation {inst.PackageVersion} (il2cpp {inst.Il2cppPlusLocalVersion})");
                inst.InstallDefaultHybridCLR();
                inst = new InstallerController();
                if (!inst.HasInstalledHybridCLR()) throw new Exception("installation de HybridCLR impossible");
            }
            var t0 = DateTime.Now;
            PrebuildCommand.GenerateAll();
            AssetDatabase.Refresh();
            rep.AppendLine($"- HybridCLR : génération en {(DateTime.Now - t0).TotalMinutes:0.0} min");

            string hotDir = SettingsUtil.GetHotUpdateDllsOutputDirByTarget(BuildTarget.Android);
            string aotDir = SettingsUtil.GetAssembliesPostIl2CppStripDir(BuildTarget.Android);
            var hot = Path.Combine(hotDir, GameAssembly + ".dll");
            if (!File.Exists(hot)) throw new Exception("bibliothèque du jeu non compilée : " + hot);
            // métadonnées complémentaires : bibliothèques du moteur dont le code du jeu utilise des génériques
            var aotNames = new System.Collections.Generic.List<string>();
            var refs = Path.Combine(Application.dataPath, "HybridCLR/Generate/AOTGenericReferences.cs");
            if (File.Exists(refs))
            {
                var txt = File.ReadAllText(refs); int i = txt.IndexOf("PatchedAOTAssemblyList", StringComparison.Ordinal);
                if (i >= 0) { int j = txt.IndexOf("};", i, StringComparison.Ordinal); foreach (Match m in Regex.Matches(txt.Substring(i, j - i), "\"([^\"]+\\.dll)\"")) aotNames.Add(m.Groups[1].Value); }
            }
            foreach (var n in new[] { "mscorlib.dll", "System.dll", "System.Core.dll" }) if (!aotNames.Contains(n)) aotNames.Add(n);

            int apkMin = 0; var minFile = ".github/apk-min.txt";
            if (File.Exists(minFile)) int.TryParse(File.ReadAllText(minFile).Trim(), out apkMin);
            var man = new CodeManifest { version = build, apkMin = apkMin };
            if (Directory.Exists(Embedded)) FileUtil.DeleteFileOrDirectory(Embedded);
            Directory.CreateDirectory(Embedded); Directory.CreateDirectory(Publish);
            CodeFile Put(string src, string name)
            {
                var b = File.ReadAllBytes(src);
                File.WriteAllBytes(Path.Combine(Embedded, name), b); File.WriteAllBytes(Path.Combine(Publish, name), b);
                using (var sha = SHA256.Create()) return new CodeFile { name = name, size = b.Length, hash = BitConverter.ToString(sha.ComputeHash(b)).Replace("-", "").ToLowerInvariant() };
            }
            man.hot = new[] { Put(hot, GameAssembly + ".dll.bytes") };
            man.aot = aotNames.Select(n => Path.Combine(aotDir, n)).Where(File.Exists).Select(f => Put(f, Path.GetFileName(f) + ".bytes")).ToArray();
            var json = JsonUtility.ToJson(man, true);
            File.WriteAllText(Path.Combine(Embedded, "code.json"), json); File.WriteAllText(Path.Combine(Publish, "code.json"), json);
            AssetDatabase.Refresh();
            rep.AppendLine($"- Code mis à jour depuis le jeu : {GameAssembly} {man.hot[0].size / 1024} Ko + {man.aot.Length} métadonnées ({man.aot.Sum(a => a.size) / 1048576.0:0.0} Mo), APK minimal {apkMin}");
            return true;
        }
    }
}
