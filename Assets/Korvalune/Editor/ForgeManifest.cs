using System;

namespace Korvalune.EditorTools
{
    /// <summary>Description d'un modèle produit par ModelForge (fichier « Nom.forge.json » à côté du FBX).</summary>
    [Serializable]
    public class ForgeManifest
    {
        public string model;                 // nom du FBX (même dossier)
        public string kind = "prop";         // character | creature | prop
        public string rig = "None";          // Human | Generic | None
        public string prefab;                // chemin du prefab à créer
        public string controller;            // « humanoid-combat », « creature-combat » ou vide
        public string avatarSource;          // Generic : os racine du mouvement
        public string animModel;             // FBX ASCII séparé contenant les animations (même squelette), facultatif
        public ForgeMaterial[] materials = new ForgeMaterial[0];
        public ForgeClip[] clips = new ForgeClip[0];
        public ForgeAttachment[] attachments = new ForgeAttachment[0];
        public ForgeCollider collider;
        public ForgeExpect expect;
        public ForgeSlot[] slots = new ForgeSlot[0];
        public ForgeMorph[] morphs = new ForgeMorph[0];   // curseurs de morphologie (blendshapes id_plus / id_moins)   // pièces d'équipement séparées et ce qu'elles masquent
        public int priority;                 // ordre de traitement (armes avant personnages)
    }

    [Serializable]
    public class ForgeMaterial
    {
        public string name;
        public string baseMap, normalMap, maskMap, occlusionMap, emissionMap;   // maskMap : R = métal, A = lissé (URP « Metallic Map »)
        public float[] color = { 1, 1, 1, 1 };
        public float metallic = 0f, smoothness = 0.5f, normalScale = 1f, occlusionStrength = 1f;
        public float[] emission = { 0, 0, 0 };
        public float[] tiling = { 1, 1 };
        public bool doubleSided;
        public bool alphaClip;               // découpe alpha (cheveux, cils, sourcils) : canal A de la BaseMap
        public float cutoff = 0.5f;
        public int maxSize = 2048, mobileMaxSize = 1024;
    }

    [Serializable]
    public class ForgeMorph
    {
        public string id, group, label;
        public bool face, bipolar;
    }

    [Serializable]
    public class ForgeSlot
    {
        public string name;                    // nom du maillage séparé dans le FBX (emplacement)
        public string[] hides = new string[0]; // emplacements / zones de peau masqués quand celui-ci est équipé
    }

    [Serializable]
    public class ForgeClip
    {
        public string name;
        public bool loop;
        public bool rootMotion;
        public float[] events = new float[0];   // instants normalisés (impact de l'arme, pas…) enregistrés comme événements « OnForgeEvent »
    }

    [Serializable]
    public class ForgeAttachment
    {
        public string socket;
        public string model;     // chemin d'un autre FBX ModelForge, relatif au dossier du manifeste
    }

    [Serializable]
    public class ForgeCollider
    {
        public string type = "capsule";   // capsule | box | none
        public float[] center = { 0, 1, 0 };
        public float height = 1.8f, radius = 0.35f;
        public float[] size = { 1, 1, 1 };
        public bool trigger;
    }

    [Serializable]
    public class ForgeExpect
    {
        public float heightMin, heightMax;
        public int maxTriangles;
        public string[] sockets = new string[0];
        public string facingBone;     // os qui doit se trouver devant (+Z) …
        public string behindBone;     // … par rapport à celui-ci
        public string leftBone;       // os qui doit être à gauche (X < 0 dans Unity)
    }
}
