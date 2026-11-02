using System.Collections.Generic;
using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>L'arme et la main gauche visibles suivent l'équipement porté : une hache s'affiche comme une hache, un arc
    /// dans la main gauche (l'arbalète disparaît), un carquois dans le dos, un orbe, un grimoire ou un phylactère dans la main
    /// gauche… Sans objet porté dans un emplacement, le héros garde son équipement d'origine.</summary>
    public class WeaponVisual : MonoBehaviour
    {
        // type d'objet → (modèle, emplacement)
        static readonly Dictionary<string, (string model, string socket)> Map = new Dictionary<string, (string, string)>
        {
            { "Épée longue", ("Longsword", "Socket_Weapon_R") }, { "Hache de guerre", ("HacheDeGuerre", "Socket_Weapon_R") }, { "Épée bâtarde", ("EpeeBatarde", "Socket_Weapon_R") },
            { "Masse d'armes", ("SunHammer", "Socket_Weapon_R") }, { "Fléau d'armes", ("FleauDArmes", "Socket_Weapon_R") },
            { "Arbalète", ("AshCrossbow", "Socket_Weapon_R") }, { "Arc long", ("ArcLong", "Socket_Shield_L") }, { "Arc de chasse", ("ArcDeChasse", "Socket_Shield_L") },
            { "Sceptre", ("Sceptre", "Socket_Weapon_R") }, { "Faux", ("Faux", "Socket_Weapon_R") }, { "Dague rituelle", ("DagueRituelle", "Socket_Weapon_R") },
            { "Écu", ("HeaterShield", "Socket_Shield_L") }, { "Pavois", ("SunPavise", "Socket_Shield_L") }, { "Orbe", ("Orbe", "Socket_Shield_L") },
            { "Grimoire", ("Grimoire", "Socket_Shield_L") }, { "Phylactère", ("Phylactere", "Socket_Shield_L") }, { "Carquois", ("Carquois", "Socket_Back") },
        };

        Equipment eq;
        readonly Dictionary<string, Transform> sockets = new Dictionary<string, Transform>();
        readonly Dictionary<string, List<GameObject>> defaults = new Dictionary<string, List<GameObject>>();
        readonly List<GameObject> spawned = new List<GameObject>();

        void Start()
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("Socket_") && !sockets.ContainsKey(t.name))
                {
                    sockets[t.name] = t;
                    var l = new List<GameObject>(); foreach (Transform c in t) l.Add(c.gameObject); defaults[t.name] = l;
                }
            eq = GetComponent<Equipment>();
            if (eq) eq.Changed += Apply;
            Apply();
        }

        void OnDestroy() { if (eq) eq.Changed -= Apply; }

        public void Apply()
        {
            foreach (var g in spawned) if (g) Destroy(g); spawned.Clear();
            var used = new HashSet<string>();
            var hideRight = false;
            foreach (var slot in new[] { Slot.Arme, Slot.MainGauche })
            {
                var it = eq ? eq.In(slot) : null; if (it == null || !Map.TryGetValue(it.baseName ?? "", out var m)) continue;
                if (!sockets.TryGetValue(m.socket, out var s)) continue;
                var pf = Resources.Load<GameObject>("Prefabs/Weapons/" + m.model); if (!pf) continue;
                var g = Instantiate(pf, s, false); g.name = m.model; g.transform.localPosition = Vector3.zero; g.transform.localRotation = Quaternion.identity;
                foreach (var c in g.GetComponentsInChildren<Collider>()) Destroy(c);
                spawned.Add(g); used.Add(m.socket);
                if (slot == Slot.Arme && m.socket == "Socket_Shield_L") hideRight = true;          // arc : tenu à gauche, main droite libre
            }
            foreach (var kv in defaults)
            {
                bool show = !used.Contains(kv.Key) && !(hideRight && kv.Key == "Socket_Weapon_R");
                foreach (var d in kv.Value) if (d) d.SetActive(show);
            }
        }
    }
}
