using System;
using BDArmory.Core.Extension;
using BDArmory.FX;
using UnityEngine;

namespace BDArmory.Parts
{
	public class BDExplosivePart : PartModule
	{

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiName = "Blast Radius"),
            UI_Label(affectSymCounterparts = UI_Scene.All, controlEnabled = true, scene = UI_Scene.All)]
        public float blastRadius = 50;


        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiName = "Blast Power"),
        UI_Label(affectSymCounterparts = UI_Scene.All, controlEnabled = true, scene = UI_Scene.All)]
        public float blastPower = 25;

        [KSPField]
		public float blastHeat = -1;

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

        

        [KSPEvent(guiActive = true, guiActiveEditor = false, guiName = "Detonate", active = true)]
	    public void DetonateEvent()
	    {
            Detonate();
        }

	    public bool Armed { get; set; } = true;

        private double previousMass = -1;
		
		bool hasDetonated;
		
		public override void OnStart (StartState state)
		{
		    if (HighLogic.LoadedSceneIsFlight)
		    {
		        part.OnJustAboutToBeDestroyed += DetonateIfPossible;
                part.force_activate();
		    }
		    
		    CalculateBlast();
		}

        public void Update()
        {
            if (HighLogic.LoadedSceneIsEditor)
                OnUpdateEditor();
        }

	    private void OnUpdateEditor()
	    {
            CalculateBlast();
        }

	    private void CalculateBlast()
	    {
	        if (!part.Resources.Contains("HighExplosive")) return;

            if (part.Resources["HighExplosive"].amount == previousMass) return;
           
	        double explosiveMass = part.Resources["HighExplosive"].amount;   

	        blastPower = (float)Math.Round(explosiveMass / 1.5f, 0);
            blastRadius = (float) (15 * Math.Pow(blastPower, (1.0 / 3.0)));

            previousMass = part.Resources["HighExplosive"].amount;
	    }
		
		public void DetonateIfPossible()
		{
			if(!hasDetonated && Armed && part.vessel.speed > 10)
			{   
			   Detonate();
               hasDetonated = true;
			}
		}

	    private void Detonate()
	    {
	        if (part != null)
	        {
                part.SetDamage(part.maxTemp*2);
	        }
	        Vector3 position = part.vessel.CoM;
	        ExplosionFX.CreateExplosion(position, blastRadius, blastPower, blastHeat, vessel, FlightGlobals.getUpAxis(),
	            "BDArmory/Models/explosion/explosionLarge", "BDArmory/Sounds/explode1");
	    }
    }
}

