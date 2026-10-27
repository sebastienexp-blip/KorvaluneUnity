using System;
using System.Collections.Generic;
using UnityEngine;

namespace Korvalune
{
    /// <summary>
    /// Apparence d'un personnage (sur la racine d'un prefab de classe) : peau, yeux, coiffure + variante + teinte, sourcils,
    /// cils et curseurs de morphologie. Les curseurs pilotent les blendshapes « id_plus » / « id_moins » de TOUS les
    /// SkinnedMeshRenderer du personnage (corps, armure, vêtements, cheveux…), qui ont été générés ensemble.
    /// Les pièces rapportées (cheveux, sourcils, cils) sont rattachées au squelette du personnage par nom d'os.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterAppearance : MonoBehaviour
    {
        static void Kill(UnityEngine.Object o) { if (o == null) return; if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

        [Serializable] public class MorphValue { public string id; [Range(-1.5f, 1.5f)] public float value; }

        public bool female;
        public List<MorphValue> morphs = new List<MorphValue>();
        public Color hairTint = Color.white;

        readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        readonly Dictionary<string, GameObject> attached = new Dictionary<string, GameObject>();
        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        static readonly int BumpMap = Shader.PropertyToID("_BumpMap");
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        void Awake() { IndexBones(); }

        void IndexBones()
        {
            bones.Clear();
            foreach (var t in GetComponentsInChildren<Transform>(true)) if (!bones.ContainsKey(t.name)) bones[t.name] = t;
        }

        /// <summary>Matériaux propres à ce personnage : instances en jeu ; hors jeu (éditeur, captures), copies explicites pour ne pas
        /// modifier les matériaux du projet ni déclencher l'erreur « renderer.material during edit mode ».</summary>
        static readonly HashSet<Renderer> copied = new HashSet<Renderer>();
        static Material[] Mats(Renderer r)
        {
            if (Application.isPlaying) return r.materials;
            if (!copied.Contains(r)) { var ms = r.sharedMaterials; for (int i = 0; i < ms.Length; i++) if (ms[i]) ms[i] = new Material(ms[i]) { name = ms[i].name }; r.sharedMaterials = ms; copied.Add(r); }
            return r.sharedMaterials;
        }

        IEnumerable<Material> MaterialsEndingWith(string suffix)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                foreach (var m in Mats(r))
                    if (m != null && m.name.Replace(" (Instance)", "").EndsWith(suffix)) yield return m;
        }

        // ------------------------------------------------------------------ peau, yeux
        public void SetSkin(CharacterCatalog.Skin s)
        {
            if (s == null) return;
            foreach (var m in MaterialsEndingWith("_Skin"))
            {
                m.SetTexture(BaseMap, s.baseMap);
                if (s.normal) { m.SetTexture(BumpMap, s.normal); m.EnableKeyword("_NORMALMAP"); }
            }
        }

        public void SetEyes(CharacterCatalog.Eye e)
        {
            if (e == null) return;
            foreach (var m in MaterialsEndingWith("_Eye")) m.SetTexture(BaseMap, e.baseMap);
        }

        // ------------------------------------------------------------------ pièces rapportées (cheveux, sourcils, cils)
        /// <summary>slot : "Cheveux", "Sourcils" ou "Cils". Remplace la pièce d'origine du prefab (masquée) par celle du catalogue.</summary>
        public GameObject Attach(string slot, CharacterCatalog.Part part, int variant, Color tint)
        {
            if (attached.TryGetValue(slot, out var old) && old) Kill(old);
            attached.Remove(slot);
            HideBuiltIn(slot);
            if (part == null) return null;
            var src = part.For(female);
            if (!src) return null;
            if (bones.Count == 0) IndexBones();
            var go = Instantiate(src, transform);
            go.name = slot + "_" + part.id;
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var nb = new Transform[smr.bones.Length];
                for (int i = 0; i < nb.Length; i++)
                    nb[i] = smr.bones[i] != null && bones.TryGetValue(smr.bones[i].name, out var t) ? t : smr.bones[i];
                smr.bones = nb;
                if (smr.rootBone != null && bones.TryGetValue(smr.rootBone.name, out var rb)) smr.rootBone = rb;
                if (part.variants.Count > 0)
                {
                    var v = part.variants[Mathf.Clamp(variant, 0, part.variants.Count - 1)];
                    foreach (var m in Mats(smr))
                    {
                        if (v.baseMap) m.SetTexture(BaseMap, v.baseMap);
                        if (v.normal) { m.SetTexture(BumpMap, v.normal); m.EnableKeyword("_NORMALMAP"); }
                        m.SetColor(BaseColor, tint);
                    }
                }
            }
            // le squelette dupliqué du modèle rapporté ne sert plus (la peau suit désormais les os du personnage)
            foreach (var a in go.GetComponentsInChildren<Animator>(true)) Kill(a);
            var dup = new List<GameObject>(); foreach (Transform c in go.transform) if (c.GetComponent<SkinnedMeshRenderer>() == null && c.name == "Hips") dup.Add(c.gameObject);
            foreach (var d in dup) Kill(d);
            attached[slot] = go;
            ApplyMorphs();
            return go;
        }

        void HideBuiltIn(string slot)
        {
            var eq = GetComponent<CharacterEquipment>();
            string builtIn = slot == "Cheveux" ? "Cheveux" : null;
            if (eq != null && builtIn != null) { var s = eq.Find(builtIn); if (s != null && s.renderer) s.renderer.enabled = false; }
            // sourcils / cils d'origine : parties du maillage principal, matériaux *_Brow / *_Lash rendus invisibles
            string suffix = slot == "Sourcils" ? "_Brow" : slot == "Cils" ? "_Lash" : null;
            if (suffix != null) foreach (var m in MaterialsEndingWith(suffix)) { var c = m.GetColor(BaseColor); c.a = 0; m.SetColor(BaseColor, c); }
        }

        public void SetHairTint(Color c)
        {
            hairTint = c;
            if (attached.TryGetValue("Cheveux", out var go) && go)
                foreach (var r in go.GetComponentsInChildren<Renderer>(true)) foreach (var m in Mats(r)) m.SetColor(BaseColor, c);
        }

        // ------------------------------------------------------------------ morphologie
        public float GetMorph(string id) { var m = morphs.Find(x => x.id == id); return m != null ? m.value : 0f; }

        public void SetMorph(string id, float value)
        {
            var m = morphs.Find(x => x.id == id);
            if (m == null) morphs.Add(m = new MorphValue { id = id });
            m.value = value;
            ApplyMorph(id, value);
        }

        public void ApplyMorphs() { batch = true; foreach (var m in morphs) ApplyMorph(m.id, m.value); batch = false; Rig?.Flush(); }
        bool batch;
        MorphRig rig;
        MorphRig Rig { get { if (!rig) rig = GetComponentInChildren<MorphRig>(true); return rig && rig.Ready ? rig : null; } }

        void ApplyMorph(string id, float v)
        {
            // morphologie compacte (MorphRig) : le corps se déforme et toutes les pièces le suivent
            var r = Rig;
            if (r != null)
            {
                r.SetWeight(id + "_plus", Mathf.Max(0, v)); r.SetWeight(id + "_moins", Mathf.Max(0, -v));
                if (!batch) r.Flush();
                return;
            }
            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = smr.sharedMesh; if (!mesh) continue;
                int ip = mesh.GetBlendShapeIndex(id + "_plus"), im = mesh.GetBlendShapeIndex(id + "_moins");
                if (ip >= 0) smr.SetBlendShapeWeight(ip, Mathf.Max(0, v) * 100f);
                if (im >= 0) smr.SetBlendShapeWeight(im, Mathf.Max(0, -v) * 100f);
            }
        }

        public void ResetMorphs() { batch = true; foreach (var m in morphs) ApplyMorph(m.id, 0); batch = false; Rig?.Flush(); morphs.Clear(); }
    }
}
