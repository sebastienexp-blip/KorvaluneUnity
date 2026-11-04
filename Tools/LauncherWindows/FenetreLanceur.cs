using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Korvalune.Lanceur
{
    /// <summary>Fenêtre du lanceur : illustration du jeu, titre, braises qui montent, notes de version défilantes, état de
    /// l'installation avec barre de progression (débit, temps restant), grand bouton Installer / Mettre à jour / Jouer,
    /// liens Réparer, Ouvrir le dossier, Désinstaller. Tout est dessiné à la main, à l'échelle de l'écran (125 %, 150 %…).</summary>
    public class FenetreLanceur : Form
    {
        const int W = 1152, H = 648;                 // taille logique (mise à l'échelle de l'écran : k)
        const string Titre = "Korvalune — Lanceur";
        readonly float k;
        readonly bool miseAJourDemandee; bool miseAJourFaite;
        readonly string capture; readonly bool demo;

        // couleurs
        static readonly Color Or = Color.FromArgb(242, 204, 122), OrSombre = Color.FromArgb(150, 104, 46), Parchemin = Color.FromArgb(232, 222, 202),
                              Gris = Color.FromArgb(178, 168, 156), Violet = Color.FromArgb(170, 130, 255);

        Image fondSrc; Bitmap fond; FontFamily serif;
        Font fStatut, fDetail, fLien, fVersion, fNoteTitre, fNote, fBouton, fFenetre;

        // braises et étincelles
        class Braise { public float X, Y, V, R, Ph, Vie; public bool Violette; }
        readonly List<Braise> braises = new List<Braise>(); readonly Random rnd = new Random();
        readonly System.Windows.Forms.Timer anim; readonly Stopwatch horloge = Stopwatch.StartNew(); double tPrec;

        // boutons dessinés
        class Bouton { public RectangleF R; public string Texte; public Action Clic; public bool Actif = true; public int Style; public bool Visible = true; }
        readonly List<Bouton> boutons = new List<Bouton>(); Bouton survol, appuye;
        Bouton bJouer, bReparer, bDossier, bDesinstaller, bPage, bReduire, bFermer;

        // état
        enum Etat { Recherche, AInstaller, AMettreAJour, Pret, HorsLigne, Telechargement, Erreur }
        Etat etat = Etat.Recherche; Publication pub; string statut = "", detail = ""; float barre = -1; bool lancement, fermetureAutorisee;
        CancellationTokenSource cts;

        // notes de version
        class LigneNote { public string Texte; public Font F; public Color C; public float Retrait, H, Avant; public bool Puce; }
        List<LigneNote> notes = new List<LigneNote>(); float notesDefil, notesHauteur;
        static readonly RectangleF Panneau = new RectangleF(W - 56 - 430, 64, 430, H - 64 - 214);
        static readonly RectangleF ZoneNotes = new RectangleF(Panneau.X + 24, Panneau.Y + 58, Panneau.Width - 48, Panneau.Height - 74);

        public FenetreLanceur(bool miseAJour)
        {
            miseAJourDemandee = miseAJour;
            var args = Environment.GetCommandLineArgs();
            int ic = Array.IndexOf(args, "--capture"); if (ic >= 0 && ic + 1 < args.Length) capture = args[ic + 1];
            demo = Array.IndexOf(args, "--demo") >= 0;

            Text = Titre; FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None; BackColor = Color.Black; KeyPreview = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            float dpi; using (var g = CreateGraphics()) dpi = g.DpiX / 96f;
            var ecran = Screen.PrimaryScreen.WorkingArea;
            k = Math.Min(dpi, Math.Min(ecran.Width * 0.94f / W, ecran.Height * 0.94f / H));
            ClientSize = new Size((int)Math.Round(W * k), (int)Math.Round(H * k));
            try { Icon = new Icon(Ressource("korvalune.ico")); } catch { }
            fondSrc = Image.FromStream(Ressource("fond.jpg"));
            serif = Famille("Palatino Linotype", "Book Antiqua", "Georgia", "Times New Roman");
            var sans = Famille("Segoe UI", "Tahoma", "Arial");
            fStatut = new Font(sans, 18, FontStyle.Bold, GraphicsUnit.Pixel);
            fDetail = new Font(sans, 14, FontStyle.Regular, GraphicsUnit.Pixel);
            fLien = new Font(sans, 13.5f, FontStyle.Regular, GraphicsUnit.Pixel);
            fVersion = new Font(sans, 12, FontStyle.Regular, GraphicsUnit.Pixel);
            fNoteTitre = new Font(serif, 18, FontStyle.Bold, GraphicsUnit.Pixel);
            fNote = new Font(sans, 13.5f, FontStyle.Regular, GraphicsUnit.Pixel);
            fBouton = new Font(serif, 30, FontStyle.Bold, GraphicsUnit.Pixel);
            fFenetre = new Font(sans, 15, FontStyle.Regular, GraphicsUnit.Pixel);

            bJouer = Ajouter(new RectangleF(W - 56 - 300, H - 180, 300, 86), "…", ClicPrincipal, 0);
            float x = 56;
            bReparer = Ajouter(Lien(ref x, "Réparer"), "Réparer", Reparer, 1);
            bDossier = Ajouter(Lien(ref x, "Ouvrir le dossier"), "Ouvrir le dossier", () => Ouvrir("explorer.exe", "\"" + Depot.Racine + "\""), 1);
            bDesinstaller = Ajouter(Lien(ref x, "Désinstaller"), "Désinstaller", Desinstaller, 1);
            bPage = Ajouter(Lien(ref x, "Page de téléchargement"), "Page de téléchargement", () => Ouvrir(Depot.Page, null), 1);
            bReduire = Ajouter(new RectangleF(W - 92, 0, 46, 34), "—", () => WindowState = FormWindowState.Minimized, 2);
            bFermer = Ajouter(new RectangleF(W - 46, 0, 46, 34), "✕", Close, 3);

            for (int i = 0; i < 70; i++) braises.Add(NouvelleBraise(true));
            Notes(Depot.NotesHorsLigne() ?? "## Notes de version\nElles s'afficheront ici dès la connexion au serveur.");
            MajBoutons();

            anim = new System.Windows.Forms.Timer { Interval = 33 };
            anim.Tick += (s, e) => Animer();
            anim.Start();
            Shown += async (s, e) =>
            {
                if (demo) Demo(); else await Verifier();
                if (capture != null) { await Task.Delay(demo ? 1500 : 3000); Capturer(capture); fermetureAutorisee = true; Close(); }
            };
        }

        // ============================================================================================== logique
        async Task Verifier()
        {
            cts?.Cancel(); cts = new CancellationTokenSource();
            etat = Etat.Recherche; statut = "Recherche des mises à jour…"; detail = "Connexion au serveur de Korvalune"; barre = -1; MajBoutons();
            try { pub = await Depot.Lire(cts.Token); }
            catch (Exception ex)
            {
                pub = null;
                if (ex is FileNotFoundException && !Depot.Installe)
                { etat = Etat.HorsLigne; statut = "La version Windows est en cours de préparation"; detail = "Elle sera disponible d'ici peu : réessayez dans quelques minutes."; }
                else if (Depot.Installe) { etat = Etat.Pret; statut = "Hors ligne — la version installée peut être lancée"; detail = $"Version 0.1.{Depot.VersionLocale} · serveur de mises à jour injoignable"; }
                else { etat = Etat.HorsLigne; statut = "Serveur injoignable"; detail = "Vérifiez votre connexion à Internet, puis réessayez."; }
                MajBoutons(); return;
            }
            if (pub.Notes != null) Notes(pub.Notes);
            // lanceur plus récent publié : il se remplace lui-même, puis se relance
            if (pub.Lanceur > Depot.VersionLanceur)
            {
                statut = "Mise à jour du lanceur…"; detail = $"Lanceur 1.0.{pub.Lanceur}"; Invalidate();
                try { if (await Depot.MettreAJourLanceur(cts.Token)) { fermetureAutorisee = true; Close(); return; } } catch { }
            }
            Evaluer();
            // ouvert depuis le jeu (« mise à jour disponible ») : la mise à jour démarre d'elle-même
            if (miseAJourDemandee && !miseAJourFaite && etat == Etat.AMettreAJour) { miseAJourFaite = true; await Installer(false); }
        }

        void Evaluer()
        {
            barre = -1;
            if (pub == null) { etat = Depot.Installe ? Etat.Pret : Etat.HorsLigne; MajBoutons(); return; }
            bool reprise = Directory.Exists(Depot.Telechargements) && Directory.GetFiles(Depot.Telechargements).Length > 0;
            if (!Depot.Installe)
            {
                etat = Etat.AInstaller; statut = "Korvalune n'est pas encore installé";
                detail = $"Version 0.1.{pub.Version} · {Depot.Go(pub.Taille)} à télécharger" + (reprise ? " · le téléchargement reprendra où il s'était arrêté" : "");
            }
            else if (Depot.VersionLocale < pub.Version)
            {
                etat = Etat.AMettreAJour; statut = $"Nouvelle version disponible : 0.1.{pub.Version}";
                detail = $"Version installée : 0.1.{Depot.VersionLocale} · {Depot.Go(pub.Taille)} à télécharger · personnages conservés";
            }
            else { etat = Etat.Pret; statut = "Korvalune est à jour"; detail = $"Version 0.1.{Depot.VersionLocale}"; barre = 1; }
            MajBoutons();
        }

        async Task Installer(bool reparation)
        {
            if (pub == null) { await Verifier(); return; }
            if (Depot.JeuOuvert) { statut = "Korvalune est ouvert"; detail = "Fermez le jeu pour installer la mise à jour."; Invalidate(); return; }
            cts = new CancellationTokenSource(); var jeton = cts;
            etat = Etat.Telechargement; barre = 0; statut = reparation ? "Réparation…" : "Préparation…"; detail = ""; MajBoutons();
            var prog = new Progress<Avancement>(a =>
            {
                if (etat != Etat.Telechargement) return;
                statut = a.Etape; barre = a.Total > 0 ? (float)a.Fait / a.Total : 0;
                detail = $"{Depot.Go(a.Fait)} / {Depot.Go(a.Total)}";
                if (a.Debit > 1) detail += $" · {(a.Debit / 1048576).ToString("0.0", Depot.Fr)} Mo/s · {Duree((a.Total - a.Fait) / a.Debit)}";
            });
            try
            {
                await Depot.Installer(pub, prog, jeton.Token);
                etat = Etat.Pret; barre = 1; statut = reparation ? "Réparation terminée — bon jeu !" : "Installation terminée — bon jeu !"; detail = $"Version 0.1.{pub.Version}";
                SystemSounds();
            }
            catch (Exception ex) when (jeton.IsCancellationRequested)
            {
                _ = ex; etat = Depot.Installe ? Etat.AMettreAJour : Etat.AInstaller;
                statut = "Téléchargement en pause"; detail = "Il reprendra là où il s'est arrêté.";
            }
            catch (Exception ex)
            {
                etat = Etat.Erreur; barre = -1; statut = "L'installation n'a pas pu se terminer"; detail = ex.Message.Split('\n')[0];
            }
            MajBoutons();
        }

        static void SystemSounds() { try { System.Media.SystemSounds.Asterisk.Play(); } catch { } }

        async void ClicPrincipal()
        {
            switch (etat)
            {
                case Etat.Pret: Jouer(); break;
                case Etat.AInstaller: case Etat.AMettreAJour: await Installer(false); break;
                case Etat.Erreur: if (pub != null) await Installer(false); else await Verifier(); break;
                case Etat.HorsLigne: await Verifier(); break;
                case Etat.Telechargement: cts?.Cancel(); break;
            }
        }

        void Jouer()
        {
            if (lancement) return;
            if (!Depot.Installe) { Evaluer(); return; }
            try
            {
                Process.Start(new ProcessStartInfo(Depot.ExeJeu) { WorkingDirectory = Depot.DossierJeu, UseShellExecute = true });
                lancement = true; statut = "Lancement de Korvalune…"; detail = "Bonne aventure !"; MajBoutons();
                var t = new System.Windows.Forms.Timer { Interval = 2500 };
                t.Tick += (s, e) => { t.Stop(); fermetureAutorisee = true; Close(); };
                t.Start();
            }
            catch (Exception ex) { statut = "Impossible de lancer le jeu"; detail = ex.Message; Invalidate(); }
        }

        async void Reparer()
        {
            if (MessageBox.Show(this, "Retélécharger et réinstaller entièrement Korvalune ?\n\nVos personnages et vos réglages sont conservés.",
                                Titre, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            if (pub == null) { await Verifier(); if (pub == null) return; }
            try { if (Directory.Exists(Depot.Telechargements)) Directory.Delete(Depot.Telechargements, true); } catch { }
            await Installer(true);
        }

        void Desinstaller()
        {
            if (MessageBox.Show(this, "Supprimer Korvalune de cet ordinateur ?\n\nVos personnages et vos réglages sont conservés : ils seront retrouvés si vous réinstallez le jeu.",
                                Titre, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
            try { Depot.Desinstaller(); statut = "Korvalune a été désinstallé"; detail = "Le lanceur peut le réinstaller à tout moment."; }
            catch (Exception ex) { statut = "Désinstallation impossible"; detail = ex.Message; }
            if (pub != null) { var s = statut; var d = detail; Evaluer(); statut = s; detail = d; }
            else { etat = Etat.HorsLigne; }
            MajBoutons();
        }

        void Ouvrir(string quoi, string arguments)
        {
            try { Directory.CreateDirectory(Depot.Racine); Process.Start(arguments == null ? new ProcessStartInfo(quoi) { UseShellExecute = true } : new ProcessStartInfo(quoi, arguments)); } catch { }
        }

        static string Duree(double s)
        {
            if (double.IsNaN(s) || double.IsInfinity(s)) return "";
            if (s < 60) return "moins d'une minute";
            if (s < 3600) return $"environ {Math.Ceiling(s / 60)} min";
            return $"environ {(int)(s / 3600)} h {(int)(s % 3600 / 60):00}";
        }

        void MajBoutons()
        {
            bool occupe = etat == Etat.Telechargement || etat == Etat.Recherche || lancement;
            switch (etat)
            {
                case Etat.Recherche: bJouer.Texte = "…"; bJouer.Actif = false; break;
                case Etat.AInstaller: bJouer.Texte = statut.StartsWith("Téléchargement en pause") ? "REPRENDRE" : "INSTALLER"; bJouer.Actif = true; break;
                case Etat.AMettreAJour: bJouer.Texte = statut.StartsWith("Téléchargement en pause") ? "REPRENDRE" : "METTRE À JOUR"; bJouer.Actif = true; break;
                case Etat.Pret: bJouer.Texte = "JOUER"; bJouer.Actif = !lancement; break;
                case Etat.HorsLigne: case Etat.Erreur: bJouer.Texte = "RÉESSAYER"; bJouer.Actif = true; break;
                case Etat.Telechargement: bJouer.Texte = "PAUSE"; bJouer.Actif = true; break;
            }
            bReparer.Actif = !occupe && Depot.Installe;
            bDesinstaller.Actif = !occupe && Depot.Installe;
            Invalidate();
        }

        void Demo()
        {
            pub = new Publication { Version = 152 };
            etat = Etat.Telechargement; statut = "Téléchargement de Korvalune…"; barre = 0.43f;
            detail = "1,1 Go / 2,6 Go · 18,4 Mo/s · environ 2 min"; MajBoutons();
        }

        // ============================================================================================== notes de version
        void Notes(string texte)
        {
            var l = new List<LigneNote>();
            foreach (var brut in texte.Replace("\r", "").Split('\n'))
            {
                var s = brut.TrimEnd(); if (s.Length == 0) continue;
                if (s.StartsWith("#")) l.Add(new LigneNote { Texte = s.TrimStart('#', ' '), F = fNoteTitre, C = Or, Avant = l.Count == 0 ? 0 : 16 });
                else if (s.StartsWith("- ") || s.StartsWith("• ")) l.Add(new LigneNote { Texte = s.Substring(2), F = fNote, C = Parchemin, Retrait = 16, Avant = 5, Puce = true });
                else l.Add(new LigneNote { Texte = s, F = fNote, C = Parchemin, Avant = 5 });
            }
            using (var bmp = new Bitmap(4, 4)) using (var g = Graphics.FromImage(bmp))
            {
                g.TextRenderingHint = TextRenderingHint.AntiAlias;
                float y = 0;
                foreach (var n in l) { n.H = g.MeasureString(n.Texte, n.F, (int)(ZoneNotes.Width - n.Retrait - 10)).Height; y += n.Avant + n.H; }
                notesHauteur = y + 8;
            }
            notes = l; notesDefil = 0; Invalidate();
        }

        // ============================================================================================== animation
        Braise NouvelleBraise(bool partout)
        {
            bool v = rnd.NextDouble() < 0.35;
            return new Braise
            {
                X = (float)(rnd.NextDouble() * W * (v ? 1 : 0.75)), Y = partout ? (float)(rnd.NextDouble() * H) : H + 10,
                V = 14 + (float)rnd.NextDouble() * 40, R = 1.2f + (float)rnd.NextDouble() * 2.4f, Ph = (float)(rnd.NextDouble() * 20), Vie = 0, Violette = v
            };
        }

        void Animer()
        {
            double t = horloge.Elapsed.TotalSeconds; float dt = (float)Math.Min(0.1, t - tPrec); tPrec = t;
            if (WindowState == FormWindowState.Minimized) return;
            for (int i = 0; i < braises.Count; i++)
            {
                var b = braises[i]; b.Y -= b.V * dt; b.X += (float)Math.Sin(t * 0.8 + b.Ph) * 9 * dt; b.Vie += dt;
                if (b.Y < -20) braises[i] = NouvelleBraise(false);
            }
            Invalidate();
        }

        // ============================================================================================== dessin
        void PreparerFond()
        {
            fond?.Dispose();
            fond = new Bitmap(ClientSize.Width, ClientSize.Height, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(fond))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.TextRenderingHint = TextRenderingHint.AntiAlias;
                // illustration en « couverture » (recadrée au format de la fenêtre)
                float ri = (float)fondSrc.Width / fondSrc.Height, rf = (float)W / H;
                var src = ri > rf ? new RectangleF((fondSrc.Width - fondSrc.Height * rf) / 2, 0, fondSrc.Height * rf, fondSrc.Height)
                                  : new RectangleF(0, (fondSrc.Height - fondSrc.Width / rf) / 2, fondSrc.Width, fondSrc.Width / rf);
                g.DrawImage(fondSrc, new RectangleF(0, 0, fond.Width, fond.Height), src, GraphicsUnit.Pixel);
                g.ScaleTransform(k, k);
                // voiles sombres : à gauche (titre), en bas (état, bouton), en haut (barre de fenêtre)
                using (var br = new LinearGradientBrush(new RectangleF(0, 0, 620, H), Color.FromArgb(200, 4, 3, 10), Color.FromArgb(0, 4, 3, 10), 0f)) g.FillRectangle(br, 0, 0, 620, H);
                using (var br = new LinearGradientBrush(new RectangleF(0, H - 290, W, 291), Color.FromArgb(0, 3, 2, 8), Color.FromArgb(242, 3, 2, 8), 90f)) g.FillRectangle(br, 0, H - 290, W, 290);
                using (var br = new LinearGradientBrush(new RectangleF(0, 0, W, 90), Color.FromArgb(150, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), 90f)) g.FillRectangle(br, 0, 0, W, 90);
                // filet doré en bas
                using (var br = new LinearGradientBrush(new RectangleF(0, H - 2, W, 2), Color.FromArgb(0, Or), Color.FromArgb(0, Or), 0f))
                {
                    br.InterpolationColors = new ColorBlend { Colors = new[] { Color.FromArgb(0, Or), Color.FromArgb(150, Or), Color.FromArgb(0, Or) }, Positions = new[] { 0f, 0.5f, 1f } };
                    g.FillRectangle(br, 0, H - 2, W, 2);
                }
                // titre (lettres dorées, contour sombre, halo)
                using (var p = new GraphicsPath())
                {
                    p.AddString("KORVALUNE", serif, (int)FontStyle.Bold, 92, new PointF(46, 58), StringFormat.GenericTypographic);
                    for (int i = 6; i >= 1; i--) using (var halo = new Pen(Color.FromArgb(14, 255, 170, 70), i * 4) { LineJoin = LineJoin.Round }) g.DrawPath(halo, p);
                    using (var ombre = new Matrix()) { ombre.Translate(4, 6); using (var po = (GraphicsPath)p.Clone()) { po.Transform(ombre); using (var b = new SolidBrush(Color.FromArgb(170, 0, 0, 0))) g.FillPath(b, po); } }
                    var r = p.GetBounds();
                    using (var or = new LinearGradientBrush(r, Color.FromArgb(255, 246, 214), Color.FromArgb(196, 132, 52), 90f))
                    {
                        or.InterpolationColors = new ColorBlend { Colors = new[] { Color.FromArgb(255, 248, 222), Color.FromArgb(240, 196, 110), Color.FromArgb(170, 108, 40) }, Positions = new[] { 0f, 0.55f, 1f } };
                        g.FillPath(or, p);
                    }
                    using (var c = new Pen(Color.FromArgb(220, 40, 18, 6), 1.6f) { LineJoin = LineJoin.Round }) g.DrawPath(c, p);
                }
                using (var f = new Font(serif, 25, FontStyle.Italic, GraphicsUnit.Pixel))
                {
                    g.DrawString("Les ombres du Cœur Céleste", f, new SolidBrush(Color.FromArgb(180, 0, 0, 0)), 57, 172);
                    g.DrawString("Les ombres du Cœur Céleste", f, new SolidBrush(Parchemin), 55, 170);
                }
                // petit ornement sous le sous-titre
                using (var pen = new Pen(Color.FromArgb(160, Or), 1.2f)) { g.DrawLine(pen, 58, 214, 250, 214); }
                using (var b = new SolidBrush(Or)) g.FillPolygon(b, new[] { new PointF(258, 214), new PointF(264, 208), new PointF(270, 214), new PointF(264, 220) });
                using (var pen = new Pen(Color.FromArgb(160, Or), 1.2f)) { g.DrawLine(pen, 278, 214, 470, 214); }
                // panneau des notes de version
                using (var p = Arrondi(Panneau, 10))
                {
                    using (var b = new SolidBrush(Color.FromArgb(196, 10, 8, 18))) g.FillPath(b, p);
                    using (var pen = new Pen(Color.FromArgb(150, OrSombre), 1.4f)) g.DrawPath(pen, p);
                }
                using (var p = Arrondi(RectangleF.Inflate(Panneau, -4, -4), 8)) using (var pen = new Pen(Color.FromArgb(40, Or), 1f)) g.DrawPath(pen, p);
                using (var f = new Font(serif, 21, FontStyle.Bold, GraphicsUnit.Pixel)) g.DrawString("Notes de version", f, new SolidBrush(Or), Panneau.X + 22, Panneau.Y + 16);
                using (var br = new LinearGradientBrush(new RectangleF(Panneau.X + 22, Panneau.Y + 48, Panneau.Width - 44, 1.5f), Color.FromArgb(200, Or), Color.FromArgb(0, Or), 0f))
                    g.FillRectangle(br, Panneau.X + 22, Panneau.Y + 48, Panneau.Width - 44, 1.5f);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            if (fond == null || fond.Width != ClientSize.Width || fond.Height != ClientSize.Height) PreparerFond();
            g.CompositingQuality = CompositingQuality.HighSpeed; g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.DrawImageUnscaled(fond, 0, 0);
            g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.ScaleTransform(k, k);
            float t = (float)horloge.Elapsed.TotalSeconds;

            // braises (orangées) et étincelles (violettes, comme la lune de l'illustration)
            foreach (var b in braises)
            {
                float a = Math.Min(1, b.Vie * 1.5f) * Math.Max(0, Math.Min(1, b.Y / (H * 0.85f))) * (0.65f + 0.35f * (float)Math.Sin(t * 5 + b.Ph));
                if (a <= 0.02f) continue;
                var c = b.Violette ? Violet : Color.FromArgb(255, 150, 60);
                using (var halo = new SolidBrush(Color.FromArgb((int)(42 * a), c))) g.FillEllipse(halo, b.X - b.R * 3, b.Y - b.R * 3, b.R * 6, b.R * 6);
                using (var coeur = new SolidBrush(Color.FromArgb((int)(220 * a), b.Violette ? Color.FromArgb(225, 210, 255) : Color.FromArgb(255, 214, 140)))) g.FillEllipse(coeur, b.X - b.R, b.Y - b.R, b.R * 2, b.R * 2);
            }

            DessinerNotes(g);

            // état, barre de progression, détail
            Texte(g, statut, fStatut, Parchemin, 56, H - 182);
            if (barre >= 0)
            {
                var rb = new RectangleF(56, H - 148, W - 56 - 56 - 300 - 40, 12);
                using (var p = Arrondi(rb, 6))
                {
                    using (var b = new SolidBrush(Color.FromArgb(170, 8, 6, 12))) g.FillPath(b, p);
                    if (barre > 0.001f)
                    {
                        var rf = new RectangleF(rb.X, rb.Y, Math.Max(12, rb.Width * Math.Min(1, barre)), rb.Height);
                        using (var pf = Arrondi(rf, 6))
                        {
                            using (var b = new LinearGradientBrush(rf, Color.FromArgb(255, 226, 150), Color.FromArgb(198, 120, 40), 90f)) g.FillPath(b, pf);
                            if (etat == Etat.Telechargement)
                            {
                                // reflet qui glisse sur la partie remplie
                                float x = rf.X + ((t * 260) % (rf.Width + 160)) - 80;
                                var clip = g.Clip; g.SetClip(pf, CombineMode.Intersect);
                                using (var b = new LinearGradientBrush(new RectangleF(x - 60, rf.Y, 120, rf.Height), Color.FromArgb(0, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 0f))
                                {
                                    b.InterpolationColors = new ColorBlend { Colors = new[] { Color.FromArgb(0, 255, 255, 255), Color.FromArgb(120, 255, 250, 230), Color.FromArgb(0, 255, 255, 255) }, Positions = new[] { 0f, 0.5f, 1f } };
                                    g.FillRectangle(b, x - 60, rf.Y, 120, rf.Height);
                                }
                                g.Clip = clip;
                            }
                        }
                    }
                    using (var pen = new Pen(Color.FromArgb(120, OrSombre), 1f)) g.DrawPath(pen, p);
                }
                if (etat == Etat.Telechargement)
                    Texte(g, (barre * 100).ToString("0", Depot.Fr) + " %", fDetail, Or, rb.Right, H - 128, true);
            }
            Texte(g, detail, fDetail, Gris, 56, H - 128);

            // boutons
            foreach (var b in boutons) if (b.Visible) DessinerBouton(g, b, t);
            Texte(g, $"Jeu {(Depot.Installe ? "0.1." + Depot.VersionLocale : "non installé")}   ·   Lanceur 1.0.{Depot.VersionLanceur}", fVersion, Color.FromArgb(140, 130, 120), W - 56, H - 56, true);
        }

        void DessinerNotes(Graphics g)
        {
            var etat0 = g.Save();
            g.SetClip(ZoneNotes);
            float y = ZoneNotes.Y - notesDefil;
            foreach (var n in notes)
            {
                y += n.Avant;
                if (y + n.H >= ZoneNotes.Y - 4 && y <= ZoneNotes.Bottom + 4)
                {
                    if (n.Puce) using (var b = new SolidBrush(Color.FromArgb(220, Or))) { float cy = y + 9; g.FillPolygon(b, new[] { new PointF(ZoneNotes.X + 3, cy), new PointF(ZoneNotes.X + 7, cy - 4), new PointF(ZoneNotes.X + 11, cy), new PointF(ZoneNotes.X + 7, cy + 4) }); }
                    using (var b = new SolidBrush(n.C)) g.DrawString(n.Texte, n.F, b, new RectangleF(ZoneNotes.X + n.Retrait, y, ZoneNotes.Width - n.Retrait - 10, n.H + 2));
                }
                y += n.H;
            }
            g.Restore(etat0);
            // fondu en bas de la zone et ascenseur
            if (notesHauteur > ZoneNotes.Height)
            {
                if (notesDefil < notesHauteur - ZoneNotes.Height - 1)
                    using (var br = new LinearGradientBrush(new RectangleF(ZoneNotes.X, ZoneNotes.Bottom - 30, ZoneNotes.Width, 31), Color.FromArgb(0, 10, 8, 18), Color.FromArgb(230, 10, 8, 18), 90f))
                        g.FillRectangle(br, ZoneNotes.X - 2, ZoneNotes.Bottom - 30, ZoneNotes.Width + 4, 30);
                float hp = ZoneNotes.Height * ZoneNotes.Height / notesHauteur, yp = ZoneNotes.Y + (ZoneNotes.Height - hp) * notesDefil / (notesHauteur - ZoneNotes.Height);
                using (var p = Arrondi(new RectangleF(Panneau.Right - 12, yp, 4, hp), 2)) using (var b = new SolidBrush(Color.FromArgb(110, Or))) g.FillPath(b, p);
            }
        }

        void DessinerBouton(Graphics g, Bouton b, float t)
        {
            bool sur = b == survol && b.Actif, bas = sur && b == appuye;
            if (b.Style == 0)
            {
                var r = b.R; if (bas) r.Offset(0, 2);
                if (b.Actif)
                    for (int i = 4; i >= 1; i--)
                        using (var p = Arrondi(RectangleF.Inflate(r, i * 3, i * 3), 10 + i * 3))
                        using (var pen = new Pen(Color.FromArgb((sur ? 26 : 12) + (int)(6 * Math.Sin(t * 2.4)), 255, 180, 80), 3)) g.DrawPath(pen, p);
                using (var p = Arrondi(r, 10))
                {
                    Color h, m, l;
                    if (!b.Actif) { h = Color.FromArgb(88, 80, 74); m = Color.FromArgb(62, 56, 52); l = Color.FromArgb(44, 40, 38); }
                    else if (etat == Etat.Telechargement) { h = Color.FromArgb(sur ? 110 : 92, 80, 150); m = Color.FromArgb(62, 44, 104); l = Color.FromArgb(34, 22, 62); }
                    else { h = sur ? Color.FromArgb(236, 178, 84) : Color.FromArgb(214, 154, 64); m = sur ? Color.FromArgb(178, 112, 36) : Color.FromArgb(156, 96, 30); l = Color.FromArgb(92, 50, 14); }
                    using (var br = new LinearGradientBrush(r, h, l, 90f))
                    {
                        br.InterpolationColors = new ColorBlend { Colors = new[] { h, m, l }, Positions = new[] { 0f, 0.55f, 1f } };
                        g.FillPath(br, p);
                    }
                    using (var reflet = new LinearGradientBrush(new RectangleF(r.X, r.Y, r.Width, r.Height / 2), Color.FromArgb(70, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f))
                    using (var ph = Arrondi(new RectangleF(r.X + 3, r.Y + 3, r.Width - 6, r.Height / 2 - 3), 8)) g.FillPath(reflet, ph);
                    using (var pen = new Pen(b.Actif ? Color.FromArgb(250, 220, 150) : Color.FromArgb(120, 110, 100), 1.6f)) g.DrawPath(pen, p);
                }
                using (var p = Arrondi(RectangleF.Inflate(r, -4, -4), 7)) using (var pen = new Pen(Color.FromArgb(90, 40, 20, 6), 1f)) g.DrawPath(pen, p);
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                float taille = b.Texte.Length > 10 ? 25 : 30;
                using (var f = new Font(serif, taille, FontStyle.Bold, GraphicsUnit.Pixel))
                {
                    var rt = new RectangleF(r.X, r.Y + 2, r.Width, r.Height);
                    using (var o = new SolidBrush(Color.FromArgb(170, 30, 12, 2))) g.DrawString(b.Texte, f, o, new RectangleF(rt.X + 1.5f, rt.Y + 2, rt.Width, rt.Height), sf);
                    using (var c = new SolidBrush(b.Actif ? Color.FromArgb(255, 246, 224) : Color.FromArgb(160, 150, 140))) g.DrawString(b.Texte, f, c, rt, sf);
                }
            }
            else if (b.Style == 1)
            {
                var c = !b.Actif ? Color.FromArgb(95, 90, 84) : sur ? Or : Color.FromArgb(205, 190, 160);
                Texte(g, b.Texte, fLien, c, b.R.X, b.R.Y + 4);
                if (sur) using (var pen = new Pen(Color.FromArgb(180, Or), 1f)) g.DrawLine(pen, b.R.X, b.R.Bottom - 3, b.R.Right, b.R.Bottom - 3);
            }
            else
            {
                if (sur) using (var br = new SolidBrush(b.Style == 3 ? Color.FromArgb(200, 170, 30, 30) : Color.FromArgb(60, 255, 255, 255))) g.FillRectangle(br, b.R);
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                using (var br = new SolidBrush(Color.FromArgb(230, 230, 220, 205))) g.DrawString(b.Texte, fFenetre, br, b.R, sf);
            }
        }

        static void Texte(Graphics g, string s, Font f, Color c, float x, float y, bool droite = false)
        {
            if (string.IsNullOrEmpty(s)) return;
            var sf = new StringFormat(StringFormatFlags.NoWrap) { Alignment = droite ? StringAlignment.Far : StringAlignment.Near };
            using (var o = new SolidBrush(Color.FromArgb(160, 0, 0, 0))) g.DrawString(s, f, o, x + 1, y + 1.5f, sf);
            using (var b = new SolidBrush(c)) g.DrawString(s, f, b, x, y, sf);
        }

        static GraphicsPath Arrondi(RectangleF r, float rayon)
        {
            var p = new GraphicsPath(); float d = Math.Min(rayon * 2, Math.Min(r.Width, r.Height));
            if (d <= 0.5f) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure(); return p;
        }

        // ============================================================================================== souris, clavier, fenêtre
        Bouton Ajouter(RectangleF r, string texte, Action clic, int style)
        {
            var b = new Bouton { R = r, Texte = texte, Clic = clic, Style = style }; boutons.Add(b); return b;
        }

        RectangleF Lien(ref float x, string texte)
        {
            float w; using (var bmp = new Bitmap(4, 4)) using (var g = Graphics.FromImage(bmp)) w = g.MeasureString(texte, fLien).Width;
            var r = new RectangleF(x, H - 60, w, 24); x += w + 30; return r;
        }

        PointF Logique(Point p) => new PointF(p.X / k, p.Y / k);
        Bouton Sous(PointF p) => boutons.LastOrDefault(b => b.Visible && b.R.Contains(p));

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var b = Sous(Logique(e.Location));
            if (b != survol) { survol = b; Cursor = b != null && b.Actif ? Cursors.Hand : Cursors.Default; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); survol = null; appuye = null; Cursor = Cursors.Default; }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            var p = Logique(e.Location); var b = Sous(p);
            if (b != null) { appuye = b; Invalidate(); return; }
            if (Panneau.Contains(p)) return;
            // glisser la fenêtre depuis n'importe quel endroit libre
            ReleaseCapture(); SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            var b = Sous(Logique(e.Location));
            if (b != null && b == appuye && b.Actif) { appuye = null; Invalidate(); b.Clic(); return; }
            appuye = null; Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (notesHauteur <= ZoneNotes.Height) return;
            notesDefil = Math.Max(0, Math.Min(notesHauteur - ZoneNotes.Height, notesDefil - e.Delta / 120f * 48));
            Invalidate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Enter && bJouer.Actif) bJouer.Clic();
            else if (e.KeyCode == Keys.Escape) WindowState = FormWindowState.Minimized;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!fermetureAutorisee && etat == Etat.Telechargement)
            {
                var r = MessageBox.Show(this, "Un téléchargement est en cours.\n\nLe mettre en pause et quitter ? Il reprendra là où il s'est arrêté au prochain lancement.",
                                        Titre, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r != DialogResult.Yes) { e.Cancel = true; return; }
                cts?.Cancel();
            }
            anim.Stop();
            base.OnFormClosing(e);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.Style |= 0x00020000;          // WS_MINIMIZEBOX : réduire/restaurer depuis la barre des tâches
                cp.ClassStyle |= 0x00020000;     // CS_DROPSHADOW : ombre portée sous la fenêtre
                return cp;
            }
        }

        void Capturer(string fichier)
        {
            try
            {
                using (var bmp = new Bitmap(ClientSize.Width, ClientSize.Height))
                {
                    using (var g = Graphics.FromImage(bmp)) OnPaint(new PaintEventArgs(g, ClientRectangle));
                    bmp.Save(fichier, ImageFormat.Png);
                }
            }
            catch (Exception ex) { File.WriteAllText(fichier + ".erreur.txt", ex.ToString()); }
        }

        public static void ActiverExistante()
        {
            var h = FindWindow(null, Titre);
            if (h != IntPtr.Zero) { ShowWindow(h, 9); SetForegroundWindow(h); }
        }

        static Stream Ressource(string nom) => Assembly.GetExecutingAssembly().GetManifestResourceStream(nom);

        static FontFamily Famille(params string[] noms)
        {
            var installees = new InstalledFontCollection().Families;
            foreach (var n in noms) { var f = installees.FirstOrDefault(x => x.Name.Equals(n, StringComparison.OrdinalIgnoreCase)); if (f != null) return f; }
            return FontFamily.GenericSerif;
        }

        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string classe, string titre);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int n);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    }
}
