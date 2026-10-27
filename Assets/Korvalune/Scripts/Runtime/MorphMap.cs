using System;
using System.Collections.Generic;
using UnityEngine;

namespace Korvalune
{
    /// <summary>Correspondance entre les sommets de chaque pièce d'un personnage (peau, cheveux, vêtements, armure) et les
    /// sommets du corps de base : k plus proches voisins et poids, calculés à l'import (MorphBaker).</summary>
    public class MorphMap : ScriptableObject
    {
        public const int K = 4;
        [Serializable] public class Part
        {
            public string path;                   // chemin du SkinnedMeshRenderer sous la racine du personnage
            public int vertexCount;
            public ushort[] idx;                  // K indices de sommets du corps par sommet de la pièce
            public byte[] w;                      // K poids (0..255) par sommet
            public Vector3 c0, c1, c2;            // repère racine → repère local du maillage (colonnes)
        }
        public Vector3 axis = Vector3.one;        // conversion des axes (table ModelForge → Unity)
        public float scale = 1f;
        public int bodyVertices;
        public List<Part> parts = new List<Part>();
    }
}
