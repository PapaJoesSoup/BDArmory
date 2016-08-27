using System;
using UnityEngine;

namespace BahaTurret
{
	public class BDExplosivePart : PartModule
	{
		
		[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiName = "Blast Radius" ),
            UI_Label(affectSymCounterparts = UI_Scene.All, controlEnabled = true, scene = UI_Scene.All)]
		public float blastRadius = 50;


        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiName = "Blast Power"),
        UI_Label(affectSymCounterparts = UI_Scene.All, controlEnabled = true, scene = UI_Scene.All)]
        public float blastPower = 25;

		[KSPField]
		public float blastHeat = -1;

		[KSPAction("Detonate")]
		public void DetonateAG(KSPActionParam param)
		{
			Detonate ();
		}
	
        private double previousMass = -1;
		/*
		[KSPField(isPersistant = true, guiActiveEditor = true, guiName = "Proxy Detonate")]
		public bool proximityDetonation = false;
		*/

		//public GameObject target = null;
		
	//	bool hasFired = false;
		bool hasDetonated = false;
		
		public override void OnStart (PartModule.StartState state)
		{
			part.OnJustAboutToBeDestroyed += new Callback(Detonate);
			part.force_activate();
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
            
            if (this.part.Resources["HighExplosive"].amount == this.previousMass) return;
           
	        var explosiveMass = this.part.Resources["HighExplosive"].amount;
            //=LOG10(m+1)*(10+(m^1.6/(14*m+1)))
            this.blastPower = (float) Math.Round(Math.Log10(1 + explosiveMass) * (10 + Math.Pow(explosiveMass, 1.6)/(14 * explosiveMass + +1)), 0);
           
            this.blastRadius = 2 * this.blastPower;

	        this.previousMass = this.part.Resources["HighExplosive"].amount;
	    }
		
		public void Detonate()
		{
			if(!hasDetonated)
			{
				hasDetonated = true;
				if(part!=null) part.temperature = part.maxTemp + 100;
				Vector3 position = transform.position+part.rb.velocity*Time.fixedDeltaTime;
				ExplosionFX.CreateExplosion(position, blastRadius, blastPower, blastHeat, vessel, FlightGlobals.getUpAxis(), "BDArmory/Models/explosion/explosionLarge", "BDArmory/Sounds/explode1");
			}
		}
	}
}

