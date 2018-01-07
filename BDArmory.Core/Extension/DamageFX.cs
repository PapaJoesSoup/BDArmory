using UnityEngine;

namespace BDArmory.Core.Extension
{
    public class DamageFX : MonoBehaviour
    {
        public static bool engineDamaged = false;

        public void Start()
        {
            
        }

        public void FixedUpdate()
        {
            if (engineDamaged)
            {
                float probability = Utils.BDAMath.RangedProbability(new[] { 50f, 25f, 20f, 2f });
                if (probability >= 3)
                {
                    ModuleEngines engine = gameObject.GetComponent<ModuleEngines>();
                    engine.flameout = true;
                    engine.heatProduction *= 1.05f;
                    engine.maxThrust *= 0.825f;
                }
            }
        }


        public static void SetEngineDamage(Part part)
        {
            ModuleEngines engine;
            engine = part.GetComponent<ModuleEngines>();
            engine.flameout = true;
            engine.heatProduction *= 1.0125f;
            engine.maxThrust *= 0.825f;            
        }

        public static void SetWingDamage(Part part)
        {
            ModuleLiftingSurface wing;
            wing = part.GetComponent<ModuleLiftingSurface>();
            wing.deflectionLiftCoeff *= 0.825f;

        }
    }
}
