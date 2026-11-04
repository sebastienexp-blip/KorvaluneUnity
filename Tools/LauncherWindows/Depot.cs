using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Korvalune.Lanceur
{
    /// <summary>Avancement d'une opération longue (téléchargement, vérification, extraction).</summary>
    public struct Avancement
    {
        public string Etape;      // texte affiché au-dessus de la barre
        public long Fait, Total;  // octets
        public double Debit;      // octets par seconde (téléchargement), 0 sinon
    }

    /// <summary>Un morceau du jeu publié (le jeu est découpé en morceaux de moins de 2 Go, limite de GitHub).</summary>
    public class Morceau { public string Nom; public long Taille; public string Empreinte; }

    /// <summary>Ce qui est publié dans la release GitHub « windows-derniere » : version du jeu, morceaux de l'archive,
    /// notes de version, version du lanceur.</summary>
    public class Publication
    {
        public int Version;
        public List<Morceau> Morceaux = new List<Morceau>();
        public string Notes;
        public int Lanceur;
        public long Taille => Morceaux.Sum(m => m.Taille);
    }

    /// <summary>Installation du jeu dans %LOCALAPPDATA%\Korvalune (aucun droit administrateur) : téléchargement avec reprise,
    /// vérification SHA-256 de chaque morceau, extraction, remplacement de l'ancienne version. Les sauvegardes du jeu
    /// (personnages, réglages) sont ailleurs (AppData\LocalLow\Korvalune Studio) : une mise à jour ou une réparation ne
    /// les touche jamais.</summary>
    public static class Depot
    {
        public const string Base = "https://github.com/sebastienexp-blip/KorvaluneUnity/releases/download/windows-derniere/";
        public const string Page = "https://github.com/sebastienexp-blip/KorvaluneUnity/releases/tag/windows-derniere";
        public static readonly CultureInfo Fr = new CultureInfo("fr-FR");

        public static readonly string Racine = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Korvalune");
        public static string DossierJeu => Path.Combine(Racine, "Jeu");
        public static string ExeJeu => Path.Combine(DossierJeu, "Korvalune.exe");
        public static string ExeLanceur => Path.Combine(Racine, "KorvaluneLauncher.exe");
        public static string Telechargements => Path.Combine(Racine, "telechargement");
        public static string NotesLocales => Path.Combine(Racine, "notes.txt");
        public static string Sauvegardes => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "Korvalune Studio", "Korvalune");

        public static bool Installe => File.Exists(ExeJeu);
        public static int VersionLocale
        {
            get { try { return int.Parse(File.ReadAllText(Path.Combine(DossierJeu, "version.txt")).Trim()); } catch { return 0; } }
        }
        public static int VersionLanceur => Assembly.GetExecutingAssembly().GetName().Version.Build;
        public static bool JeuOuvert => Process.GetProcessesByName("Korvalune").Length > 0;

        static readonly HttpClient Http = CreerClient();
        static HttpClient CreerClient()
        {
            var h = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true }) { Timeout = Timeout.InfiniteTimeSpan };
            h.DefaultRequestHeaders.UserAgent.ParseAdd("KorvaluneLauncher/" + VersionLanceur);
            return h;
        }

        static async Task<string> Texte(string nom, CancellationToken ct)
        {
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                cts.CancelAfter(TimeSpan.FromSeconds(15));
                // paramètre anti-cache : GitHub peut servir une ancienne copie quelques minutes après une publication
                using (var r = await Http.GetAsync(Base + nom + "?t=" + DateTime.UtcNow.Ticks, cts.Token).ConfigureAwait(false))
                {
                    if (r.StatusCode == System.Net.HttpStatusCode.NotFound) throw new FileNotFoundException("non publié : " + nom, nom);
                    r.EnsureSuccessStatusCode();
                    var b = await r.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    return Encoding.UTF8.GetString(b).TrimStart('﻿');
                }
            }
        }

        /// <summary>Lit la publication en ligne. Lève une exception si le serveur est injoignable.</summary>
        public static async Task<Publication> Lire(CancellationToken ct)
        {
            var p = new Publication();
            p.Version = int.Parse((await Texte("version.txt", ct)).Trim());
            foreach (var ligne in (await Texte("paquet.txt", ct)).Split('\n'))
            {
                var c = ligne.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (c.Length >= 3) p.Morceaux.Add(new Morceau { Nom = c[0], Taille = long.Parse(c[1]), Empreinte = c[2].ToLowerInvariant() });
            }
            if (p.Morceaux.Count == 0) throw new InvalidDataException("paquet.txt vide");
            try { p.Notes = await Texte("notes.txt", ct); File.WriteAllText(NotesLocales, p.Notes, Encoding.UTF8); } catch { p.Notes = null; }
            try { p.Lanceur = int.Parse((await Texte("lanceur.txt", ct)).Trim()); } catch { p.Lanceur = 0; }
            return p;
        }

        public static string NotesHorsLigne()
        {
            try { return File.Exists(NotesLocales) ? File.ReadAllText(NotesLocales) : null; } catch { return null; }
        }

        // ---------------------------------------------------------------------------------------------- installation
        /// <summary>Télécharge (avec reprise), vérifie, extrait et met en place la version publiée.</summary>
        public static async Task Installer(Publication p, IProgress<Avancement> prog, CancellationToken ct)
        {
            Directory.CreateDirectory(Telechargements);
            // place disque : morceaux + jeu extrait (≈ 1,6 fois l'archive) + marge
            long dejaLa = p.Morceaux.Sum(m => { var f = Path.Combine(Telechargements, m.Nom); return File.Exists(f) ? new FileInfo(f).Length : 0; });
            long besoin = (long)(p.Taille * 2.8) - dejaLa;
            var disque = new DriveInfo(Path.GetPathRoot(Racine));
            if (disque.AvailableFreeSpace < besoin)
                throw new IOException($"Espace disque insuffisant sur {disque.Name} : il faut {Go(besoin)} libres ({Go(disque.AvailableFreeSpace)} disponibles).");

            // 1. téléchargement des morceaux manquants
            long total = p.Taille, base0 = 0;
            foreach (var m in p.Morceaux)
            {
                var dst = Path.Combine(Telechargements, m.Nom);
                var ok = Path.Combine(Telechargements, m.Nom + ".ok");
                if (File.Exists(dst) && File.Exists(ok) && File.ReadAllText(ok).Trim() == m.Empreinte && new FileInfo(dst).Length == m.Taille)
                { base0 += m.Taille; continue; }                                         // déjà téléchargé et vérifié
                await Telecharger(m, dst, base0, total, prog, ct).ConfigureAwait(false);
                prog.Report(new Avancement { Etape = "Vérification de l'intégrité…", Fait = base0 + m.Taille, Total = total });
                var h = await Task.Run(() => Empreinte(dst), ct).ConfigureAwait(false);
                if (h != m.Empreinte)
                {
                    File.Delete(dst);
                    throw new InvalidDataException("Un fichier téléchargé est abîmé (empreinte différente). Il a été supprimé : relancez l'installation.");
                }
                File.WriteAllText(ok, h);
                base0 += m.Taille;
            }

            // 2. extraction dans un dossier neuf, puis échange avec l'ancienne version
            if (JeuOuvert) throw new InvalidOperationException("Korvalune est ouvert : fermez le jeu pour terminer la mise à jour.");
            var neuf = Path.Combine(Racine, "Jeu.nouveau");
            await Task.Run(() =>
            {
                if (Directory.Exists(neuf)) Directory.Delete(neuf, true);
                Directory.CreateDirectory(neuf);
                using (var flux = new FluxMorceaux(p.Morceaux.Select(m => Path.Combine(Telechargements, m.Nom)).ToList()))
                using (var zip = new ZipArchive(flux, ZipArchiveMode.Read))
                {
                    long tot = zip.Entries.Sum(e => e.Length), fait = 0; var buf = new byte[1 << 20];
                    string racineNeuf = Path.GetFullPath(neuf) + Path.DirectorySeparatorChar;
                    foreach (var e in zip.Entries)
                    {
                        ct.ThrowIfCancellationRequested();
                        var chemin = Path.GetFullPath(Path.Combine(neuf, e.FullName));
                        if (!chemin.StartsWith(racineNeuf, StringComparison.OrdinalIgnoreCase)) continue;   // chemin hors du dossier : ignoré
                        if (e.FullName.EndsWith("/") || e.FullName.EndsWith("\\")) { Directory.CreateDirectory(chemin); continue; }
                        Directory.CreateDirectory(Path.GetDirectoryName(chemin));
                        using (var src = e.Open()) using (var dst = File.Create(chemin))
                        {
                            int n;
                            while ((n = src.Read(buf, 0, buf.Length)) > 0)
                            {
                                dst.Write(buf, 0, n); fait += n;
                                prog.Report(new Avancement { Etape = "Installation des fichiers du jeu…", Fait = fait, Total = tot });
                            }
                        }
                    }
                }
                File.WriteAllText(Path.Combine(neuf, "version.txt"), p.Version.ToString());
                if (!File.Exists(Path.Combine(neuf, "Korvalune.exe"))) throw new InvalidDataException("Archive incomplète : Korvalune.exe absent.");
                var ancien = Path.Combine(Racine, "Jeu.ancien");
                if (Directory.Exists(ancien)) Directory.Delete(ancien, true);
                if (Directory.Exists(DossierJeu)) Directory.Move(DossierJeu, ancien);
                Directory.Move(neuf, DossierJeu);
                try { Directory.Delete(ancien, true); } catch { }
                try { Directory.Delete(Telechargements, true); } catch { }
            }, ct).ConfigureAwait(false);
        }

        static async Task Telecharger(Morceau m, string dst, long base0, long total, IProgress<Avancement> prog, CancellationToken ct)
        {
            var part = dst + ".part";
            long deja = File.Exists(part) ? new FileInfo(part).Length : 0;
            if (deja > m.Taille) { File.Delete(part); deja = 0; }
            if (deja < m.Taille)
            {
                var req = new HttpRequestMessage(HttpMethod.Get, Base + m.Nom);
                if (deja > 0) req.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(deja, null);
                using (var r = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
                {
                    r.EnsureSuccessStatusCode();
                    if (deja > 0 && r.StatusCode != System.Net.HttpStatusCode.PartialContent) deja = 0;   // reprise refusée : on repart de zéro
                    using (var src = await r.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var f = new FileStream(part, deja > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16))
                    {
                        var buf = new byte[1 << 18]; int n; long fait = deja;
                        var chrono = Stopwatch.StartNew(); var mesures = new Queue<(double t, long o)>(); mesures.Enqueue((0, fait));
                        double dernier = -1, debit = 0;
                        using (ct.Register(() => { try { src.Dispose(); } catch { } }))   // pause : interrompt la lecture en cours
                        {
                            while ((n = await src.ReadAsync(buf, 0, buf.Length, ct).ConfigureAwait(false)) > 0)
                            {
                                await f.WriteAsync(buf, 0, n, ct).ConfigureAwait(false); fait += n;
                                double t = chrono.Elapsed.TotalSeconds;
                                if (t - dernier >= 0.1)
                                {
                                    dernier = t; mesures.Enqueue((t, fait));
                                    while (mesures.Count > 2 && t - mesures.Peek().t > 3) mesures.Dequeue();   // débit sur les 3 dernières secondes
                                    var a = mesures.Peek(); if (t - a.t > 0.2) debit = (fait - a.o) / (t - a.t);
                                    prog.Report(new Avancement { Etape = "Téléchargement de Korvalune…", Fait = base0 + fait, Total = total, Debit = debit });
                                }
                            }
                        }
                        ct.ThrowIfCancellationRequested();
                    }
                }
            }
            if (new FileInfo(part).Length != m.Taille) throw new IOException("Téléchargement interrompu : relancez pour reprendre là où il s'est arrêté.");
            if (File.Exists(dst)) File.Delete(dst);
            File.Move(part, dst);
        }

        static string Empreinte(string fichier)
        {
            using (var sha = SHA256.Create()) using (var f = new FileStream(fichier, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20))
                return BitConverter.ToString(sha.ComputeHash(f)).Replace("-", "").ToLowerInvariant();
        }

        public static string Go(long o) => o >= 1L << 30 ? (o / (double)(1L << 30)).ToString("0.0", Fr) + " Go" : (o / 1048576.0).ToString("0", Fr) + " Mo";

        /// <summary>Supprime le jeu installé (les personnages et réglages sont conservés).</summary>
        public static void Desinstaller()
        {
            if (JeuOuvert) throw new InvalidOperationException("Fermez Korvalune avant de le désinstaller.");
            if (Directory.Exists(DossierJeu)) Directory.Delete(DossierJeu, true);
            try { if (Directory.Exists(Telechargements)) Directory.Delete(Telechargements, true); } catch { }
        }

        // ---------------------------------------------------------------------------------------------- lanceur
        /// <summary>Premier lancement depuis le fichier téléchargé : le lanceur se copie dans %LOCALAPPDATA%\Korvalune et crée
        /// les raccourcis « Korvalune » sur le bureau et dans le menu Démarrer.</summary>
        public static void InstallerLanceur()
        {
            try
            {
                Directory.CreateDirectory(Racine);
                var ici = Path.GetFullPath(Assembly.GetExecutingAssembly().Location);
                if (!string.Equals(ici, Path.GetFullPath(ExeLanceur), StringComparison.OrdinalIgnoreCase))
                {
                    int installee = 0;
                    try { if (File.Exists(ExeLanceur)) installee = FileVersionInfo.GetVersionInfo(ExeLanceur).FileBuildPart; } catch { }
                    if (VersionLanceur >= installee) File.Copy(ici, ExeLanceur, true);
                }
                var marque = Path.Combine(Racine, "raccourcis.ok");
                if (!File.Exists(marque))
                {
                    Raccourci(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Korvalune.lnk"));
                    Raccourci(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Korvalune.lnk"));
                    File.WriteAllText(marque, DateTime.Now.ToString("s"));
                }
            }
            catch { }
        }

        static void Raccourci(string lnk)
        {
            try
            {
                var t = Type.GetTypeFromProgID("WScript.Shell"); if (t == null) return;
                dynamic sh = Activator.CreateInstance(t);
                dynamic s = sh.CreateShortcut(lnk);
                s.TargetPath = ExeLanceur; s.WorkingDirectory = Racine; s.Description = "Korvalune"; s.IconLocation = ExeLanceur + ",0";
                s.Save();
            }
            catch { }
        }

        /// <summary>Télécharge la nouvelle version du lanceur, puis la met en place après la fermeture de celui-ci (petit
        /// script qui attend, remplace le fichier et relance). Renvoie vrai si le lanceur doit se fermer.</summary>
        public static async Task<bool> MettreAJourLanceur(CancellationToken ct)
        {
            Directory.CreateDirectory(Racine);
            var neuf = Path.Combine(Racine, "KorvaluneLauncher.nouveau.exe");
            using (var r = await Http.GetAsync(Base + "KorvaluneLauncher.exe", ct).ConfigureAwait(false))
            {
                r.EnsureSuccessStatusCode();
                var b = await r.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                if (b.Length < 50000 || b[0] != 'M' || b[1] != 'Z') return false;      // pas un programme Windows : on ignore
                File.WriteAllBytes(neuf, b);
            }
            var cmd = Path.Combine(Racine, "maj-lanceur.cmd");
            File.WriteAllText(cmd, string.Join("\r\n",
                "@echo off", "chcp 65001 >nul", "set n=0",
                ":boucle", "ping -n 2 127.0.0.1 >nul",
                $"move /y \"{neuf}\" \"{ExeLanceur}\" >nul 2>&1 && goto fin",
                "set /a n+=1", "if %n% lss 20 goto boucle",
                ":fin", $"start \"\" \"{ExeLanceur}\"", "del \"%~f0\"", ""), new UTF8Encoding(false));
            Process.Start(new ProcessStartInfo("cmd.exe", "/c \"" + cmd + "\"") { CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden });
            return true;
        }
    }

    /// <summary>Lecture des morceaux téléchargés comme un seul fichier (l'archive zip complète), sans les recoller sur le disque.</summary>
    sealed class FluxMorceaux : Stream
    {
        readonly List<FileStream> parts; readonly long[] debuts; readonly long longueur; long pos;
        public FluxMorceaux(List<string> fichiers)
        {
            parts = fichiers.Select(f => new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16)).ToList();
            debuts = new long[parts.Count]; long s = 0;
            for (int i = 0; i < parts.Count; i++) { debuts[i] = s; s += parts[i].Length; }
            longueur = s;
        }
        public override bool CanRead => true; public override bool CanSeek => true; public override bool CanWrite => false;
        public override long Length => longueur;
        public override long Position { get => pos; set => pos = value; }
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (pos >= longueur) return 0;
            int i = Array.BinarySearch(debuts, pos); if (i < 0) i = ~i - 1;
            while (i < parts.Count - 1 && pos >= debuts[i] + parts[i].Length) i++;
            var p = parts[i]; p.Position = pos - debuts[i];
            int n = p.Read(buffer, offset, (int)Math.Min(count, p.Length - p.Position));
            pos += n; return n;
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            pos = origin == SeekOrigin.Begin ? offset : origin == SeekOrigin.Current ? pos + offset : longueur + offset;
            return pos;
        }
        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) foreach (var p in parts) p.Dispose(); base.Dispose(disposing); }
    }
}
