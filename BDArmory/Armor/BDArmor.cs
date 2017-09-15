using System.Collections.Generic;
using BDArmory.FX;
using UnityEngine;

namespace BDArmory.Armor
{
    public class BDArmor : PartModule
    {
        static BDArmor instance;
        public static BDArmor Instance => instance;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Thickness (mm)"),
            UI_FloatRange(minValue = 10f, maxValue = 500f, stepIncrement = 10f, scene = UI_Scene.All)]
        public float EquivalentThickness = 0f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "outerArmorThickness"),
            UI_FloatRange(minValue = 10f, maxValue = 500f, stepIncrement = 10f, scene = UI_Scene.All)]
        public float outerArmorThickness = 100f;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "blastRadius")]
        public float blastRadius = 1;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "blastPower")]
        public float blastPower = 1;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "blastHeat")]
        public float blastHeat = 1;

        [KSPField]
        public string explModelPath = "BDArmory/Models/explosion/explosionLarge";

        [KSPField]
        public string explSoundPath = "BDArmory/Sounds/explode1";

        [KSPField]
        public string explodeMode = "Never";

        public ArmorUtils.ExplodeMode _explodeMode { get; private set; } = ArmorUtils.ExplodeMode.Never;

        public  float getEquivalentThickness()
        {
            return EquivalentThickness;
        }

        public float getOuterArmorThickness()
        {
            return outerArmorThickness;
        }

        public override void OnStart(StartState state)
        {
            base.OnAwake();
            part.force_activate();
            instance = this;

            if (UI.BDArmorySettings.ADVANCED_EDIT)
            {
                Fields["EquivalentThickness"].guiActiveEditor = true;
                Fields["outerArmorThickness"].guiActiveEditor = true;
            }

            switch (explodeMode)
            {
                case "Always":
                    _explodeMode = ArmorUtils.ExplodeMode.Always;
                    break;
                case "Dynamic":
                    _explodeMode = ArmorUtils.ExplodeMode.Dynamic;
                    break;
                case "Never":
                    _explodeMode = ArmorUtils.ExplodeMode.Never;
                    break;
            }

        }

        public void CreateExplosion(Part part)
        {
            float explodeScale = 0;
            IEnumerator<PartResource> resources = part.Resources.GetEnumerator();
            while (resources.MoveNext())
            {
                if (resources.Current == null) continue;
                switch (resources.Current.resourceName)
                {
                    case "LiquidFuel":
                        explodeScale += (float)resources.Current.amount;
                        break;
                    case "Oxidizer":
                        explodeScale += (float)resources.Current.amount;
                        break;
                }
            }
            resources.Dispose();
            explodeScale /= 100;
            part.explode();
            ExplosionFX.CreateExplosion(part.partTransform.position, explodeScale * blastRadius, explodeScale * blastPower * 2,
                explodeScale * blastHeat, part.vessel, FlightGlobals.upAxis, explModelPath, explSoundPath);
        }

        public static bool GetArmor(Collider collider, Part hitPart)
        {
            Transform transform = null;

            if (!hitPart) return false;

            if (hitPart.FindModelTransform("ArmorRootTransform"))

                transform = hitPart.FindModelTransform("ArmorRootTransform");

            if (transform == null)
            {
                Debug.Log("[BDArmory]: Armor Transform not found!");
                return false;
            }

            if (collider.transform == transform || collider.transform.IsChildOf(transform))
            {
                return true;
            }
            return false;
        }
    }
}