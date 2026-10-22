using UnityEngine;

namespace Korvalune
{
    /// <summary>Scène vitrine : fait jouer tour à tour les animations d'un personnage (repos, marche, course, attaque, blocage, coup reçu, esquive, mort).</summary>
    public class ShowcaseCycler : MonoBehaviour
    {
        public CharacterRig rig;
        public float stepSeconds = 3f;
        public float turnSpeed = 12f;
        float t; int step;

        void Update()
        {
            if (rig == null) return;
            transform.Rotate(0f, turnSpeed * Time.deltaTime, 0f);
            t += Time.deltaTime;
            if (t < stepSeconds) return;
            t = 0f; step = (step + 1) % 9;
            rig.SetBlocking(false); rig.Die(false); rig.SetSpeed(0f);
            switch (step)
            {
                case 1: rig.SetSpeed(1.6f); break;
                case 2: rig.SetSpeed(4.5f); break;
                case 3: rig.Attack(); break;
                case 4: rig.SetBlocking(true); break;
                case 5: rig.Hit(); break;
                case 6: rig.Dodge(); break;
                case 7: rig.Die(true); break;
            }
        }
    }
}
