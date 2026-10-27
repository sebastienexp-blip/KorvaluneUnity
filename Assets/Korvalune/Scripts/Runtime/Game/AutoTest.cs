using System.Collections;
using System.IO;
using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Partie de test automatique (émulateur de la CI) : activée par le fichier « autotest.txt » posé dans le dossier
    /// de données de l'application. Le lanceur et la sélection s'enchaînent seuls (héroïne Elyra créée comme par un joueur),
    /// puis le héros marche dans la vallée ; l'état est écrit dans le journal (« [Korvalune][test] ») et les panneaux
    /// s'ouvrent tour à tour pour les captures d'écran.</summary>
    public class AutoTest : MonoBehaviour
    {
        static bool? isOn;
        public static bool Enabled
        {
            get
            {
                if (!isOn.HasValue)
                {
                    try { isOn = File.Exists(Path.Combine(Application.persistentDataPath, "autotest.txt")); } catch { isOn = false; }
#if UNITY_ANDROID && !UNITY_EDITOR
                    // l'émulateur de la CI peut aussi lancer le jeu avec « -e autotest 1 » (plus fiable que le fichier)
                    if (!isOn.Value)
                        try
                        {
                            using var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                            using var act = up.GetStatic<AndroidJavaObject>("currentActivity");
                            using var intent = act.Call<AndroidJavaObject>("getIntent");
                            isOn = !string.IsNullOrEmpty(intent.Call<string>("getStringExtra", "autotest"));
                        }
                        catch { }
#endif
                    Debug.Log("[Korvalune] partie automatique : " + (isOn.Value ? "activée" : "non"));
                }
                return isOn.Value;
            }
        }

        public static void Log(string msg) => Debug.Log("[Korvalune][test] " + msg);

        IEnumerator Start()
        {
            var p = GetComponent<PlayerController>(); var cc = GetComponent<CharacterController>();
            var boot = FindFirstObjectByType<GameBootstrap>();
            yield return new WaitForSeconds(3f);
            Report(p, cc, "arrivée");
            // marche vers la porte du château pendant une dizaine de secondes
            var goal = boot ? boot.castleGate : transform.position + Vector3.forward * 30f;
            p.WalkTo(goal, null);
            for (int i = 0; i < 5; i++) { yield return new WaitForSeconds(2f); Report(p, cc, $"marche {2 * (i + 1)} s"); }
            p.walkTo = null;
            var hud = boot ? boot.Hud : null;
            if (hud)
            {
                yield return new WaitForSeconds(4f); Log("panneau : arbre de compétences"); hud.TestShow("arbre");
                yield return new WaitForSeconds(10f); Log("panneau : inventaire"); hud.TestShow("sac");
                yield return new WaitForSeconds(10f); Log("panneau : menu"); hud.TestShow("menu");
                yield return new WaitForSeconds(10f); Log("panneau : options"); hud.TestShow("options");
                yield return new WaitForSeconds(10f); hud.TestShow("");
            }
            Report(p, cc, "fin");
            Log("TERMINÉ");
        }

        void Report(PlayerController p, CharacterController cc, string when)
        {
            var pos = transform.position; var t = Terrain.activeTerrain;
            float ground = t ? t.SampleHeight(pos) + t.transform.position.y : float.NaN;
            bool ok = cc.isGrounded || (t && pos.y > ground - 1f);
            Log($"{when} : position {pos}, sol {ground:0.00}, au sol {cc.isGrounded} → {(ok ? "OK" : "CHUTE")}");
        }
    }
}
