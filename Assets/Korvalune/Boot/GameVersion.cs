using UnityEngine;

namespace Korvalune.Boot
{
    /// <summary>Version du jeu en cours d'exécution. Sur Android, le code du jeu est mis à jour depuis le jeu (sans réinstaller
    /// l'APK) : la version jouée peut donc être plus récente que l'APK installé. Le contenu téléchargeable correspondant est
    /// dans la release GitHub « contenu-{Build} ».</summary>
    public static class GameVersion
    {
        /// <summary>Numéro de construction de l'APK (ou du programme Windows) installé.</summary>
        public static int ApkBuild
        {
            get { var v = Application.version.Split('.'); return v.Length > 0 && int.TryParse(v[v.Length - 1], out var n) ? n : 0; }
        }
        static int build;
        /// <summary>Numéro de la version du jeu chargée (code et contenu).</summary>
        public static int Build { get => build > 0 ? build : ApkBuild; set => build = value; }
        public static string Label => "0.1." + Build;
        /// <summary>Vrai si le code du jeu a été chargé depuis une mise à jour téléchargée (HybridCLR).</summary>
        public static bool HotLoaded;

        public const string Releases = "https://github.com/sebastienexp-blip/KorvaluneUnity/releases/download/";
        public static string ContentBase(int build) => $"{Releases}contenu-{build}/";
    }

    /// <summary>Description du code d'une version (code.json, publié avec le contenu) : la bibliothèque du jeu et les
    /// métadonnées complémentaires dont l'interpréteur a besoin, avec tailles et empreintes SHA-256 ; apkMin = plus vieil APK
    /// capable de faire tourner ce code (sinon il faut installer le nouvel APK).</summary>
    [System.Serializable]
    public class CodeManifest
    {
        public int version, apkMin;
        public CodeFile[] hot = new CodeFile[0], aot = new CodeFile[0];
    }
    [System.Serializable] public class CodeFile { public string name; public long size; public string hash; }
}
