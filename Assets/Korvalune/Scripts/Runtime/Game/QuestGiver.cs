using UnityEngine;
using UnityEngine.SceneManagement;

namespace Korvalune.Game
{
    /// <summary>PNJ donneur de la quête principale d'une région. Il se tient près de l'arrivée, signalé par un « ! » doré
    /// tant que le chapitre n'est pas entamé. On lui parle (bouton « Parler ») : il ouvre le chapitre par son dialogue
    /// (la cinématique d'ouverture). Il refuse si le chapitre précédent n'est pas terminé. Plus de démarrage automatique
    /// à la téléportation.</summary>
    public class QuestGiver : MonoBehaviour
    {
        Quest quest; GameObject marker;

        public static void Spawn(Quest q)
        {
            var ch = q.chapter; if (ch == null || !q.player) return;
            // première quête : son donneur attend juste devant l'enceinte du château
            var at = ch.id == "ch01" ? new Vector3(6f, 0f, -43f) : q.player.position + q.player.right * 5f;
            at = GameBootstrap.Ground(at);
            var go = new GameObject("PNJ donneur " + ch.id);
            go.transform.position = at;
            var giver = go.AddComponent<QuestGiver>();
            giver.quest = q;
            giver.Build(ch);
            Usable.Add(go, "Parler", 3.2f, () => giver.Talk());
        }

        static Material Mat(Color c, Color emit)
        {
            var m = LootDrop.litBase ? new Material(LootDrop.litBase) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            if (emit.maxColorComponent > 0) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emit); }
            return m;
        }

        static GameObject Prim(Transform parent, PrimitiveType t, Vector3 lp, Vector3 sc, Material m)
        {
            var g = GameObject.CreatePrimitive(t);
            Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false); g.transform.localPosition = lp; g.transform.localScale = sc;
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }

        // silhouette debout : manteau de la couleur du chapitre, tête, nom et « ! » au-dessus
        void Build(Story.Def ch)
        {
            var root = transform;
            // un vrai personnage (modèle des PNJ de la ville) ; à défaut, la silhouette de secours
            string[] kinds = { "Marchand", "Armurier", "Forgeron", "Alchimiste", "Mystique", "Joaillier", "Curiosites" };
            var pf = Res.Load<GameObject>("Prefabs/Characters/PNJ_" + kinds[(Story.ChapterNumber(ch) - 1 + kinds.Length * 10) % kinds.Length]);
            if (pf)
            {
                var npc = Instantiate(pf, root); npc.name = "PNJ"; npc.transform.localPosition = Vector3.zero; npc.transform.localRotation = Quaternion.identity;
                foreach (var c in npc.GetComponentsInChildren<Collider>()) if (!c.isTrigger) Destroy(c);
            }
            else
            {
                var cloak = Mat(Color.HSVToRGB(Mathf.Repeat(0.08f * Story.ChapterNumber(ch), 1f), 0.5f, 0.45f), Color.black);
                var skin = Mat(new Color(0.85f, 0.7f, 0.6f), Color.black);
                Prim(root, PrimitiveType.Capsule, new Vector3(0, 0.9f, 0), new Vector3(0.7f, 0.9f, 0.7f), cloak);
                Prim(root, PrimitiveType.Sphere, new Vector3(0, 1.95f, 0), Vector3.one * 0.42f, skin);
                Prim(root, PrimitiveType.Cube, new Vector3(0, 2.25f, 0), new Vector3(0.46f, 0.1f, 0.46f), cloak);   // capuche
            }
            var name = new GameObject("Nom"); name.transform.SetParent(root, false); name.transform.localPosition = new Vector3(0, 2.7f, 0);
            var tm = name.AddComponent<TextMesh>();
            tm.text = QuestLog.RegionName(SceneManager.GetActiveScene().name) + "\nGardien";
            tm.characterSize = 0.12f; tm.anchor = TextAnchor.LowerCenter; tm.alignment = TextAlignment.Center;
            tm.color = new Color(1f, 0.85f, 0.45f);
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
            var m = new GameObject("Marqueur"); m.transform.SetParent(root, false); m.transform.localPosition = new Vector3(0, 3.2f, 0);
            var mt = m.AddComponent<TextMesh>();
            mt.text = "!"; mt.characterSize = 0.2f; mt.fontSize = 64; mt.anchor = TextAnchor.LowerCenter; mt.alignment = TextAlignment.Center;
            mt.color = new Color(1f, 0.82f, 0.2f);
            mt.font = tm.font; m.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
            marker = m;
        }

        void Update()
        {
            // le « ! » disparaît dès que le chapitre est entamé
            if (marker && quest && QuestLog.Seen("intro_" + quest.chapter.id)) marker.SetActive(false);
            if (marker) marker.transform.Rotate(0, 60f * Time.deltaTime, 0, Space.World);
        }

        void Talk()
        {
            var ch = quest.chapter;
            if (!QuestLog.Available(ch))
            {
                quest.Notify?.Invoke("<i>« Revenez quand vous aurez terminé le chapitre précédent. »</i>");
                return;
            }
            if (QuestLog.Seen("intro_" + ch.id))
            {
                quest.Notify?.Invoke("<i>« Bonne chance, aventurier. »</i>");
                return;
            }
            QuestLog.MarkSeen("intro_" + ch.id);
            Cinematic.ChapterIntro(quest);
        }
    }
}
