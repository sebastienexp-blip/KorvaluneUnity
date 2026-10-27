using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Matériaux d'effets (textures Art/VFX, shader URP Particles/Unlit), créés par l'éditeur (VfxBuilder).</summary>
    [CreateAssetMenu(menuName = "Korvalune/Bibliothèque d'effets")]
    public class VfxLibrary : ScriptableObject
    {
        public Material fire, smoke, glow, spark, lightning, ice, runes, shock, slash, debris, beam, soul, glowAlpha;
    }
}
