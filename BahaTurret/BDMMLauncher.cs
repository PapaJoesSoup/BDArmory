using System;
using System.Collections.Generic;
using UnityEngine;


namespace BahaTurret
{
	public class BDMMLauncher : PartModule
	{
		
		public override void OnStart (PartModule.StartState state)
		{
			part.force_activate();
		}
		
		[KSPEvent(name = "Fire", guiActive = true, active = true)]
		public void Fire()
		{
			
			
			GameObject target = null;
			if(vessel.targetObject!=null) target = vessel.targetObject.GetVessel().gameObject;
			
			part.decouple(0);
			
			
			
			foreach(var bdmm in vessel.FindPartModulesImplementing<BDModularGuidance>())
			{
				bdmm.HasFired = true;
				//bdmm.target = target;
			}
			foreach(var bde in vessel.FindPartModulesImplementing<BDExplosivePart>())
			{
				//bde.target = target;
			}
		}
		
	}
}

