using System;
using System.Net;
using System.Threading;
using System.Windows.Forms;

namespace Korvalune.Lanceur
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            using (var m = new Mutex(true, "Korvalune.Lanceur.Unique", out bool premier))
            {
                if (!premier) { FenetreLanceur.ActiverExistante(); return; }      // déjà ouvert : on le ramène devant
                try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)12288; }   // TLS 1.2 et 1.3
                catch { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Depot.InstallerLanceur();
                bool miseAJour = Array.Exists(args, a => a == "--mise-a-jour");
                Application.Run(new FenetreLanceur(miseAJour));
            }
        }
    }
}
