using UnityEngine;

namespace Korvalune.Game
{
    /// <summary>Plateforme de jeu : sur ordinateur (Windows…), commandes façon Diablo 4 — déplacement et attaque à la souris,
    /// barre de 8 compétences au clavier ; sur téléphone, joystick tactile et 5 boutons de compétences.
    /// Dans l'éditeur, le mode ordinateur s'active avec la préférence « korvalune.pc » = 1.</summary>
    public static class Platform
    {
        public static bool Desktop
        {
            get
            {
                if (Application.isMobilePlatform) return false;
                if (Application.isEditor) return PlayerPrefs.GetInt("korvalune.pc", 0) == 1;
                return true;
            }
        }
        /// <summary>Nombre d'emplacements de la barre de compétences.</summary>
        public static int SkillSlots => Desktop ? 8 : 5;
    }
}
