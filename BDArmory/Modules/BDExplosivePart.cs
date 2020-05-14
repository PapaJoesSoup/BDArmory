using BDArmory.Core;
using BDArmory.Core.Extension;
using BDArmory.Core.Utils;
using BDArmory.FX;
using UnityEngine;

namespace BDArmory.Modules
{
    public class BDExplosivePart : PartModule
    {
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiName = "#LOC_BDArmory_TNTMass"),//TNT mass equivalent
        UI_Label(affectSymCounterparts = UI_Scene.All, controlEnabled = true, scene = UI_Scene.All)]
        public float tntMass = 1;

        [KSPField(isPersistant = false, guiActive = true, guiActiveEditor = false, guiName = "#LOC_BDArmory_BlastRadius"),//Blast Radius
         UI_Label(affectSymCounterparts = UI_Scene.All, controlEnabled = true, scene = UI_Scene.All)]
        public float blastRadius = 10;

        [KSPField]
        public string explModelPath = "BDArmory/Models/explosion/explosion";

        [KSPField]
        public string explSoundPath = "BDArmory/Sounds/explode1";

        [KSPAction("Arm")]
        public void ArmAG(KSPActionParam param)
        {
            Armed = true;
        }

        [KSPAction("Detonate")]
        public void DetonateAG(KSPActionParam param)
        {
            Detonate();
        }

        [KSPEvent(guiActive = true, guiActiveEditor = false, guiName = "#LOC_BDArmory_Detonate", active = true)]//Detonate
        public void DetonateEvent()
        {
            Detonate();
        }

        public bool Armed { get; set; } = true;
        public bool Shaped { get; set; } = false;

        private double previousMass = -1;

        bool hasDetonated;

        public override void OnStart(StartState state)
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                part.explosionPotential = 1.0f;
                part.OnJustAboutToBeDestroyed += DetonateIfPossible;
                part.force_activate();
            }

            if (BDArmorySettings.ADVANCED_EDIT)
            {
                //Fields["tntMass"].guiActiveEditor = true;

                //((UI_FloatRange)Fields["tntMass"].uiControlEditor).minValue = 0f;
                //((UI_FloatRange)Fields["tntMass"].uiControlEditor).maxValue = 3000f;
                //((UI_FloatRange)Fields["tntMass"].uiControlEditor).stepIncrement = 5f;
            }

            CalculateBlast();
        }

        public void Update()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                OnUpdateEditor();
            }

            if (hasDetonated)
            {
                this.part.explode();
            }
        }

        private void OnUpdateEditor()
        {
            CalculateBlast();
        }

        private void CalculateBlast()
        {
            if (part.Resources.Contains("HighExplosive"))
            {
                if (part.Resources["HighExplosive"].amount == previousMass) return;

                tntMass = (float)(part.Resources["HighExplosive"].amount * part.Resources["HighExplosive"].info.density * 1000) * 1.5f;
                part.explosionPotential = tntMass / 10f;
                previousMass = part.Resources["HighExplosive"].amount;
            }

            blastRadius = BlastPhysicsUtils.CalculateBlastRange(tntMass);
        }

        public void DetonateIfPossible()
        {
            if (!hasDetonated && Armed)
            {
                Vector3 direction = default(Vector3);

                if (Shaped)
                {
                    direction = (part.transform.position + part.rb.velocity * Time.deltaTime).normalized;
                }
                ExplosionFx.CreateExplosion(part.transform.position, tntMass,
                    explModelPath, explSoundPath, true, 0, part, direction);
                hasDetonated = true;
            }
        }

        private void Detonate()
        {
            if (!hasDetonated && Armed)
            {
                ExplosionFx.CreateExplosion(part.transform.position, tntMass,
                    explModelPath, explSoundPath, true, 0, part);

            }
        }

        public float GetBlastRadius()
        {
            CalculateBlast();
            return blastRadius;
        }
    }
}
