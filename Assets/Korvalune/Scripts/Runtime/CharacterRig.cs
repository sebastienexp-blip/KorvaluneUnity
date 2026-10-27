using System.Collections.Generic;
using UnityEngine;

namespace Korvalune
{
    /// <summary>
    /// Points d'attache d'un personnage importé depuis ModelForge (nœuds « Socket_* » du FBX)
    /// et pilotage de son Animator (paramètres communs à tous les personnages humanoïdes).
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterRig : MonoBehaviour
    {
        public static readonly int SpeedId = Animator.StringToHash("Speed");
        public static readonly int BlockId = Animator.StringToHash("Block");
        public static readonly int AttackId = Animator.StringToHash("Attack");

        /// <summary>Hors jeu (captures, aperçus d'éditeur) : pose le personnage dans sa posture de repos au lieu de la pose en T.</summary>
        public static void EditorIdlePose(GameObject go)
        {
            if (Application.isPlaying || !go) return;
            var an = go.GetComponentInChildren<Animator>();
            if (!an || !an.runtimeAnimatorController) return;
            foreach (var c in an.runtimeAnimatorController.animationClips) if (c && c.name == "Idle") { c.SampleAnimation(an.gameObject, 0.3f); return; }
        }
        public static readonly int HitId = Animator.StringToHash("Hit");
        public static readonly int DodgeId = Animator.StringToHash("Dodge");
        public static readonly int DeadId = Animator.StringToHash("Dead");

        [SerializeField] Animator animator;
        [SerializeField] List<Transform> sockets = new List<Transform>();

        public Animator Animator => animator;

        void Reset() { Collect(); }

        /// <summary>Recherche l'Animator et tous les nœuds dont le nom commence par « Socket_ ».</summary>
        public void Collect()
        {
            animator = GetComponentInChildren<Animator>();
            sockets.Clear();
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("Socket_")) sockets.Add(t);
        }

        public Transform Socket(string name)
        {
            foreach (var s in sockets) if (s != null && s.name == name) return s;
            return null;
        }

        /// <summary>Pose un objet (arme, bouclier) sur un point d'attache, transformation locale nulle.</summary>
        public GameObject Attach(string socket, GameObject prefab)
        {
            var s = Socket(socket);
            if (s == null || prefab == null) return null;
            var go = Instantiate(prefab, s, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go;
        }

        /// <summary>Événement d'animation (impact d'arme, pas…) posé par ModelForge. Le combat s'y abonne.</summary>
        public event System.Action<string> ForgeEvent;
        public void OnForgeEvent(string tag) { ForgeEvent?.Invoke(tag); }

        public void SetSpeed(float metersPerSecond) { if (animator) animator.SetFloat(SpeedId, metersPerSecond); }
        public void SetBlocking(bool on) { if (animator) animator.SetBool(BlockId, on); }
        public void Attack() { if (animator) animator.SetTrigger(AttackId); }
        public void Hit() { if (animator) animator.SetTrigger(HitId); }
        public void Dodge() { if (animator) animator.SetTrigger(DodgeId); }
        public void Die(bool dead = true) { if (animator) animator.SetBool(DeadId, dead); }
    }
}
