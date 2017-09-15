using System.Collections.Generic;
using System.Linq;
using BDArmory.Core.Extension;
using BDArmory.Misc;
using BDArmory.Parts;
using BDArmory.UI;
using UnityEngine;

namespace BDArmory.FX
{
    public class ExplosionFX : MonoBehaviour
    {
        KSPParticleEmitter[] pEmitters;
        Light lightFX;
        float startTime;
        public AudioClip exSound;
        public AudioSource audioSource;
        float maxTime;

        public float range;

        void Start()
        {
            startTime = Time.time;
            pEmitters = gameObject.GetComponentsInChildren<KSPParticleEmitter>();
            List<KSPParticleEmitter>.Enumerator pe = pEmitters.ToList().GetEnumerator();
            while (pe.MoveNext())
            {
                if (pe.Current == null) continue;
               EffectBehaviour.AddParticleEmitter(pe.Current);
                
                pe.Current.emit = true;

                if (pe.Current.maxEnergy > maxTime)
                {
                    maxTime = pe.Current.maxEnergy;
                }
            }
            pe.Dispose();

            lightFX = gameObject.AddComponent<Light>();
            lightFX.color = Misc.Misc.ParseColor255("255,238,184,255");
            lightFX.intensity = 8;
            lightFX.range = range*3f;
            lightFX.shadows = LightShadows.None;


            audioSource.volume = BDArmorySettings.BDARMORY_WEAPONS_VOLUME;

            audioSource.PlayOneShot(exSound);
        }

        void FixedUpdate()
        {
            lightFX.intensity -= 12*Time.fixedDeltaTime;
            if (Time.time - startTime > 0.2f)
            {
                List<KSPParticleEmitter>.Enumerator pe = pEmitters.ToList().GetEnumerator();
                while (pe.MoveNext())
                {
                    if (pe.Current == null) continue;
                    pe.Current.emit = false;
                }
                pe.Dispose();
            }

            if (Time.time - startTime > maxTime)
            {
                Destroy(gameObject);
            }
        }

        public static void CreateExplosion(Vector3 position, float radius, float power, float heat, Vessel sourceVessel,
            Vector3 direction, string explModelPath, string soundPath)
        {
            GameObject go;
            AudioClip soundClip;

            go = GameDatabase.Instance.GetModel(explModelPath);
            soundClip = GameDatabase.Instance.GetAudioClip(soundPath);


            Quaternion rotation = Quaternion.LookRotation(VectorUtils.GetUpDirection(position));
            GameObject newExplosion = (GameObject) Instantiate(go, position, rotation);
            newExplosion.SetActive(true);
            ExplosionFX eFx = newExplosion.AddComponent<ExplosionFX>();
            eFx.exSound = soundClip;
            eFx.audioSource = newExplosion.AddComponent<AudioSource>();
            eFx.audioSource.minDistance = 200;
            eFx.audioSource.maxDistance = 5500;
            eFx.audioSource.spatialBlend = 1;
            eFx.range = radius;

            if (power <= 5)
            {
                eFx.audioSource.minDistance = 4f;
                eFx.audioSource.maxDistance = 3000;
                eFx.audioSource.priority = 9999;
            }
            IEnumerator<KSPParticleEmitter> pe = newExplosion.GetComponentsInChildren<KSPParticleEmitter>().Cast<KSPParticleEmitter>()
                .GetEnumerator();
            while (pe.MoveNext())
            {
                if (pe.Current == null) continue;
                pe.Current.emit = true;
                
            }
            pe.Dispose();

            DoExplosionDamage(position, power, heat, radius, sourceVessel);
        }

        public static float ExplosionHeatMultiplier = 4200;
        public static float ExplosionImpulseMultiplier = 0.1f; //adjust as necessary to increase/decrease the force of HE weapons

		public static void DoExplosionRay(Ray ray, float power, float heat, float maxDistance, ref List<Part> ignoreParts, ref List<DestructibleBuilding> ignoreBldgs, Vessel sourceVessel = null)
		{
			RaycastHit rayHit;
			if(Physics.Raycast(ray, out rayHit, maxDistance, 557057))
			{
				float sqrDist = (rayHit.point - ray.origin).sqrMagnitude;
				float sqrMaxDist = maxDistance * maxDistance;
				float distanceFactor = Mathf.Clamp01((sqrMaxDist - sqrDist) / sqrMaxDist);
				//parts
				Part part = rayHit.collider.GetComponentInParent<Part>();
				if(part)
				{
					Vessel missileSource = null;
					if(sourceVessel != null)
					{
                        MissileBase ml = part.FindModuleImplementing<MissileBase>();
						if(ml)
						{
							missileSource = ml.SourceVessel;
						}
					}


                    if (!ignoreParts.Contains(part) && part.physicalSignificance == Part.PhysicalSignificance.FULL &&
                        (!sourceVessel || sourceVessel != missileSource))
                    {
                        ignoreParts.Add(part);
                        Rigidbody rb = part.GetComponent<Rigidbody>();
                        if (rb)
                        {
                            rb.AddForceAtPosition(ray.direction*power*distanceFactor*ExplosionImpulseMultiplier,
                                rayHit.point, ForceMode.Impulse);
                        }
                        if (heat < 0)
                        {
                            heat = power;
                        }
                        float heatDamage = (BDArmorySettings.DMG_MULTIPLIER/100)*ExplosionHeatMultiplier*heat*
                                           distanceFactor/part.crashTolerance;
                        if (heat < 40 && part.crashTolerance >= 80)//penalty for low-mid caliber HE rounds hitting armor panels
                        {
                            heatDamage = heatDamage * heat / 40;//non linear scaling, the weaker the round the more penalty is applied (renders rapid fire low caliber HE borderline worthless against armor plates and structural parts)
                        }
                        part.AddDamage(heatDamage);
                        if (BDArmorySettings.DRAW_DEBUG_LABELS)
                            Debug.Log("[BDArmory]:====== Explosion ray hit part! Damage: " + heatDamage);
                        return;
                    }
                }

                //buildings
                DestructibleBuilding building = rayHit.collider.GetComponentInParent<DestructibleBuilding>();
                if (building && !ignoreBldgs.Contains(building))
                {
                    ignoreBldgs.Add(building);
                    float damageToBuilding = (BDArmorySettings.DMG_MULTIPLIER/100)*ExplosionHeatMultiplier*0.00645f*
                                             power*distanceFactor;
                    if (damageToBuilding > building.impactMomentumThreshold/10) building.AddDamage(damageToBuilding);
                    if (building.Damage > building.impactMomentumThreshold) building.Demolish();
                    if (BDArmorySettings.DRAW_DEBUG_LABELS)
                        Debug.Log("[BDArmory]:== Explosion hit destructible building! Damage: " +
                                  (damageToBuilding).ToString("0.00") + ", total Damage: " + building.Damage);
                }
            }
        }

        public static List<Part> ignoreParts = new List<Part>();
        public static List<DestructibleBuilding> ignoreBuildings = new List<DestructibleBuilding>();

		public static void DoExplosionDamage(Vector3 position, float power, float heat, float maxDistance, Vessel sourceVessel)
		{
			if(BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory]: ======= Doing explosion sphere =========");
			ignoreParts.Clear();
			ignoreBuildings.Clear();

            // Unity does not like linq.  changing to an enumeration to extract needed lists.
            #region Old code (For reference.  remove when satisfied new code works as expected)
            //var vesselsAffected =
            //    BDATargetManager.LoadedVessels.Where(
            //        v => v != null && v.loaded && !v.packed && (v.CoM - position).magnitude < maxDistance * 4);

            //var partsAffected =
            //    vesselsAffected.SelectMany(v => v.parts).Where(p => p != null && p && (p.transform.position - position).magnitude < maxDistance);

            //foreach (var part in partsAffected)
            //{
            //    DoExplosionRay(new Ray(position, part.transform.TransformPoint(part.CoMOffset) - position), power, heat, maxDistance, ref ignoreParts, ref ignoreBuildings, sourceVessel);
            //}

            //foreach (var bldg in BDATargetManager.LoadedBuildings)
            //{
            //    if (bldg == null) continue;
            //    if ((bldg.transform.position - position).magnitude < maxDistance * 1000)
            //    {
            //        DoExplosionRay(new Ray(position, bldg.transform.position - position), power, heat, maxDistance, ref ignoreParts, ref ignoreBuildings);
            //    }
            //}
            #endregion

            // this replaces 2 passes through the vessels list and 2 passes through the parts lists with a single pass, and eliminates boxing and unboxing performed by linq and foreach loops.  Should be faster, with better gc
            List<Vessel>.Enumerator v = BDATargetManager.LoadedVessels.GetEnumerator();
		    while (v.MoveNext())
		    {
		        if (v.Current == null) continue;
                if (!v.Current.loaded || v.Current.packed || (v.Current.CoM - position).magnitude >= maxDistance * 4) continue;
		        List<Part>.Enumerator p = v.Current.parts.GetEnumerator();
		        while (p.MoveNext())
		        {
		            if (p.Current == null) continue;
		            if ((p.Current.transform.position - position).magnitude >= maxDistance) continue;
		            DoExplosionRay(new Ray(position, p.Current.transform.TransformPoint(p.Current.CoMOffset) - position), power, heat, maxDistance, ref ignoreParts, ref ignoreBuildings, sourceVessel);
		        }
                p.Dispose();
		    }
            v.Dispose();

		    List<DestructibleBuilding>.Enumerator bldg = BDATargetManager.LoadedBuildings.GetEnumerator();
			while(bldg.MoveNext())
			{
				if(bldg.Current == null) continue;
				if((bldg.Current.transform.position - position).magnitude < maxDistance * 1000)
				{
					DoExplosionRay(new Ray(position, bldg.Current.transform.position - position), power, heat, maxDistance, ref ignoreParts, ref ignoreBuildings);
				}
			}
            bldg.Dispose();
		}
	}
}

