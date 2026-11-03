using System.Collections.Generic;
using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Ambiance propre à une région extérieure (posée à la construction de la scène) : dépôt sur le dessus des
    /// pierres et du décor (neige, cendre, poussière de néant), cristaux lumineux construits au lancement (posés ou
    /// flottants), nappe de lave qui s'écoule lentement.</summary>
    public class ZoneLook : MonoBehaviour
    {
        public bool hasTop; public Color top = Color.white; public float topStrength = 0.6f;
        public List<Vector3> crystals = new List<Vector3>();
        public List<float> crystalSizes = new List<float>();
        public Color crystalColor = new Color(0.55f, 0.8f, 1f);
        public bool floatingCrystals;
        public Renderer flow; public Vector2 flowSpeed = new Vector2(0.004f, 0.002f);
        public float hazardLevel = -10000f; public string hazardName = "";
        Material flowMat; float nextBurn; Health hero;

        void Awake() { if (hasTop) Settings.SetWorldLayer(top, topStrength); }

        void Start()
        {
            var lit = LootDrop.litBase;
            for (int i = 0; i < crystals.Count; i++)
            {
                float s = i < crystalSizes.Count ? crystalSizes[i] : 1.5f;
                var cl = RiftLevel.CrystalCluster(transform, crystals[i] - transform.position, s, crystalColor, lit, 97 + i * 13);
                if (floatingCrystals && i % 3 == 0) { var f = cl.gameObject.AddComponent<Floater>(); f.bob = 0.4f; f.period = 4f + i % 5; f.spin = 8f; }
                if (i % 4 == 0)
                {
                    var l = new GameObject("Lueur").AddComponent<Light>(); l.transform.SetParent(cl, false); l.transform.localPosition = Vector3.up * s * 0.6f;
                    l.type = LightType.Point; l.color = crystalColor; l.intensity = 1.6f; l.range = 4f + s * 2f; l.shadows = LightShadows.None;
                }
            }
            if (flow) flowMat = flow.material;
        }

        void Update()
        {
            // lave / néant : brûle le héros qui y met les pieds (12 % de sa vie par seconde)
            if (hazardLevel > -9000f)
            {
                if (!hero) { var pc = FindFirstObjectByType<PlayerController>(); if (pc) hero = pc.GetComponent<Health>(); }
                if (hero && hero.transform.position.y < hazardLevel + 0.15f && Time.time >= nextBurn && hero.hp > 0)
                {
                    nextBurn = Time.time + 0.5f;
                    hero.TakeDamage(hero.maxHp * 0.06f, null);
                    FX.Burst(hero.transform.position + Vector3.up * 0.3f, crystalColor, 10, 2f, 0.15f, 0.5f, 0.6f);
                    FindFirstObjectByType<GameBootstrap>()?.Hud?.Notify($"<color=#ff7a5a>{hazardName}</color>");
                }
            }
            if (!flowMat) return;
            var o = flowSpeed * Time.time;
            if (flowMat.HasProperty("_BaseMap")) flowMat.SetTextureOffset("_BaseMap", o);
            if (flowMat.HasProperty("_EmissionMap")) flowMat.SetTextureOffset("_EmissionMap", o);
        }
    }
}
