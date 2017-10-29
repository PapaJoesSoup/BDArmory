using System;
using BDArmory.Core.Extension;
using BDArmory.FX;
using BDArmory.UI;
using UnityEngine;

/*
namespace BDArmory.Misc
{
	/// <summary>
	/// DEPRECATED
	/// </summary>
	[Obsolete("All bullets should be pooled. Use PooledBullet")]
	public class BahaTurretBullet : MonoBehaviour
	{
		public enum BulletTypes{Standard, Explosive}

		public BulletTypes bulletType;
		public float startTime;
		public Vessel sourceVessel;
		public Color lightColor = Misc.ParseColor255("255, 235, 145, 255");
		public Color projectileColor;
		
		public string bulletTexturePath;
		
		public bool fadeColor;
		public Color startColor;
		Color currentColor;
		
		public bool bulletDrop = true;
		
		public float tracerStartWidth = 1;
		public float tracerEndWidth = 1;
		public float tracerLength = 0;
		
		public float initialSpeed;
		
		
		public Vector3 prevPosition;
		public Vector3 currPosition;

		//explosive parameters
		public float radius = 30;
		public float blastPower = 8;
		
		public string explModelPath;
		public string explSoundPath;
		//

		Vector3 startPosition;
		public bool airDetonation = false;
		public float detonationRange = 3500;

		LineRenderer bulletTrail;
	//	VectorLine bulletVectorLine;
		//Material lineMat;
		
		
		Vector3 sourceOriginalV;
		bool hasBounced;
		
		float maxDistance;

		//bool isUnderwater = false;

		Rigidbody rb;
		
		void Start()
		{
			startPosition = transform.position;

			float maxLimit = Mathf.Clamp(BDArmorySettings.MAX_BULLET_RANGE, 0, 8000);
			maxDistance = Mathf.Clamp(BDArmorySettings.PHYSICS_RANGE, 2500, maxLimit);
			projectileColor.a = projectileColor.a/2;
			startColor.a = startColor.a/2;
			
			currentColor = projectileColor;
			if(fadeColor)	
			{
				currentColor = startColor;
			}
			startTime = Time.time;
			prevPosition = gameObject.transform.position;

            sourceOriginalV = sourceVessel.Velocity();
			
			Light light = gameObject.AddComponent<Light>();
			light.type = LightType.Point;
			light.color = lightColor;
			light.range = 8;
			light.intensity = 1;
			
			
			bulletTrail = gameObject.AddComponent<LineRenderer>();
			bulletTrail.SetVertexCount(2);
			bulletTrail.SetPosition(0, transform.position);
			bulletTrail.SetPosition(1, transform.position);
			float width = tracerStartWidth * Vector3.Distance(transform.position, FlightCamera.fetch.mainCamera.transform.position)/50;
			bulletTrail.SetWidth(width, width);
			bulletTrail.material = new Material(Shader.Find("KSP/Particles/Alpha Blended"));
			bulletTrail.material.mainTexture = GameDatabase.Instance.GetTexture(bulletTexturePath, false);
			bulletTrail.material.SetColor("_TintColor", currentColor);
			
			
			rb = GetComponent<Rigidbody> ();
			rb.useGravity = false;
			
		
			
		}
		
		void FixedUpdate()
		{
			float distanceFromStart = Vector3.Distance(transform.position, startPosition);

			if(bulletDrop && FlightGlobals.RefFrameIsRotating)
			{
				rb.velocity += FlightGlobals.getGeeForceAtPosition(transform.position) * TimeWarp.fixedDeltaTime;
			}
			
			
			if(tracerLength == 0)
			{
				bulletTrail.SetPosition(0, transform.position+(rb.velocity * TimeWarp.fixedDeltaTime)-(FlightGlobals.ActiveVessel.Velocity()*TimeWarp.fixedDeltaTime));
			}
			else
			{
				bulletTrail.SetPosition(0, transform.position + ((rb.velocity-sourceOriginalV).normalized * tracerLength));	
			}
			if(fadeColor)
			{
				FadeColor();
				bulletTrail.material.SetColor("_TintColor", currentColor);
			}
			
			float fov = FlightCamera.fetch.mainCamera.fieldOfView;
			float width1 = (fov/60) * tracerStartWidth * Mathf.Clamp(Vector3.Distance(transform.position, FlightCamera.fetch.mainCamera.transform.position),0,3000)/50;
			float width2 = (fov/60) * tracerEndWidth * Mathf.Clamp(Vector3.Distance(transform.position, FlightCamera.fetch.mainCamera.transform.position),0,3000)/50;
			
			bulletTrail.SetWidth(width1, width2);
			
			bulletTrail.SetPosition(1, transform.position);
			
			
			
			currPosition = gameObject.transform.position;
			
			if(distanceFromStart > maxDistance)
			{
				Destroy(gameObject);
				return;
			}
			
			if(Time.time - startTime > 0.01f)
			{
			    Light light = gameObject.GetComponent<Light>();
                light.intensity = 0;	
				float dist = initialSpeed*TimeWarp.fixedDeltaTime;
				
				Ray ray = new Ray(prevPosition, currPosition-prevPosition);
				RaycastHit hit;
                //KerbalEVA hitEVA = null;
                //if (Physics.Raycast(ray, out hit, dist, 2228224))
                //{
                //    bool penetrated = true;
                //    try
                //    {
                //        hitEVA = hit.collider.gameObject.GetComponentUpwards<KerbalEVA>();
                //        if (hitEVA != null)
                //            Debug.Log("[BDArmory]:Hit on kerbal confirmed!");
                //    }
                //    catch (NullReferenceException)
                //    {
                //        Debug.Log("[BDArmory]:Whoops ran amok of the exception handler");
                //    }

                //    if (hitEVA != null)
                //    {
                //        float hitAngle = Vector3.Angle(rb.velocity, -hit.normal);
                //        if (hitEVA.part != null) //see if it will ricochet of the part
                //        {
                //            penetrated = !RicochetOnPart(hitEVA.part, hitAngle);
                //        }

                //        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                //        /////////////////////////////////////////////////[panzer1b] HEAT BASED DAMAGE CODE START//////////////////////////////////////////////////////////////
                //        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////



                //        float heatDamage = (rb.mass / hitEVA.part.crashTolerance) * rb.velocity.magnitude * 50 * BDArmorySettings.DMG_MULTIPLIER;   //how much heat damage will be applied based on bullet mass, velocity, and part's impact tolerance

                //        //how much heat damage will be applied based on bullet mass, velocity, and part's impact tolerance and mass
                //        if (!penetrated)
                //        {
                //            heatDamage = heatDamage / 8;
                //        }
                //        if (BDArmorySettings.INSTAKILL)
                //        //instakill support, will be removed once mod becomes officially MP
                //        {
                //            heatDamage = (float)hitEVA.part.maxTemp + 100;
                //            //make heat damage equal to the part's max temperture, effectively instakilling any part it hits
                //        }
                //        if (BDArmorySettings.DRAW_DEBUG_LABELS)
                //            Debug.Log("[BDArmory]: Hit! damage applied: " + heatDamage); //debugging stuff

                //        if (hitEVA.part.vessel != sourceVessel)
                //        {
                //            hitEVA.part.AddDamage(heatDamage);
                //        }

                //        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                //        /////////////////////////////////////////////////[panzer1b] HEAT BASED DAMAGE CODE END////////////////////////////////////////////////////////////////
                //        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

                //        if (hitEVA.part.vessel != sourceVessel)
                //        {
                //            if (!penetrated && !hasBounced)
                //            {
                //                //ricochet
                //                hasBounced = true;
                //                if (BDArmorySettings.BULLET_HITS)
                //                {
                //                    BulletHitFX.CreateBulletHit(hit.point, hit.normal, true);
                //                }

                //                transform.position = hit.point;
                //                rb.velocity = Vector3.Reflect(rb.velocity, hit.normal);
                //                rb.velocity = hitAngle / 150 * rb.velocity * 0.75f;

                //                Vector3 randomDirection = UnityEngine.Random.rotation * Vector3.one;

                //                rb.velocity = Vector3.RotateTowards(rb.velocity, randomDirection, UnityEngine.Random.Range(0f, 5f) * Mathf.Deg2Rad, 0);
                //            }
                //            else
                //            {
                //                if (BDArmorySettings.BULLET_HITS)
                //                {
                //                    BulletHitFX.CreateBulletHit(hit.point, hit.normal, false);
                //                }

                //                if (bulletType == BulletTypes.Explosive)
                //                {
                //                    ExplosionFX.CreateExplosion(hit.point, radius, blastPower, -1, sourceVessel, rb.velocity.normalized, explModelPath, explSoundPath);
                //                }

                //                Destroy(gameObject); //destroy bullet on collision
                //            }
                //        }
                //    }
                //}

                if (Physics.Raycast(ray, out hit, dist, 688129))
				{
					bool penetrated = true;

					
					//hitting a vessel Part
					
					//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                    /////////////////////////////////////////////////[panzer1b] HEAT BASED DAMAGE CODE START//////////////////////////////////////////////////////////////
                    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

					Part hitPart =  null;   //determine when bullet collides with a target
					try{
                        KerbalEVA eva = hit.collider.gameObject.GetComponentUpwards<KerbalEVA>();
						hitPart = eva ? eva.part : hit.collider.gameObject.GetComponentInParent<Part>();
					}catch(NullReferenceException){}

					float hitAngle = Vector3.Angle(rb.velocity, -hit.normal);
					if(hitPart!=null) //see if it will ricochet of the part
					{
						penetrated = !RicochetOnPart(hitPart, hitAngle);
					}
					else //see if it will ricochet off scenery
					{
						float reflectRandom = UnityEngine.Random.Range(-100f, 90f);
						if(reflectRandom > 90-hitAngle)
						{
							penetrated = false;
						}
					}


					if(hitPart!=null && !hitPart.partInfo.name.Contains("Strut"))   //when a part is hit, execute damage code (ignores struts to keep those from being abused as armor)(no, because they caused weird bugs :) -BahamutoD)
					{
						float heatDamage = (rb.mass/hitPart.crashTolerance) * rb.velocity.magnitude * 50 * BDArmorySettings.DMG_MULTIPLIER;   //how much heat damage will be applied based on bullet mass, velocity, and part's impact tolerance
						if(!penetrated)
						{
							heatDamage = heatDamage/8;
						}
						if(BDArmorySettings.INSTAKILL)  //instakill support, will be removed once mod becomes officially MP
						{
                            heatDamage = (float)hitPart.maxTemp + 100; //make heat damage equal to the part's max temperture, effectively instakilling any part it hits
						}
                        if (BDArmorySettings.DRAW_DEBUG_LINES) Debug.Log("[BDArmory]: Hit! damage applied: " + heatDamage); //debugging stuff

                        if (hitPart.mass <= 0.01)   //if part mass is below 0.01, instakill it and do minor collateral (anti-exploit and to keep people from abusing near massless or massless crap as armor)
                        {
                            if (hitPart.vessel != sourceVessel)
                            {
                                //make heat damage equal to the part's max temperture, and add 500 extra heat damage which should do minor collateral to teh surrounding parts
                                hitPart.AddDamage(hitPart.maxTemp + 500);
                            }
                        }
                        else    //apply damage normally if no special case present
                        {
                            if (hitPart.vessel != sourceVessel) hitPart.AddDamage(heatDamage);  //apply heat damage to the hit part.
                        }

					}

                    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                    /////////////////////////////////////////////////[panzer1b] HEAT BASED DAMAGE CODE END////////////////////////////////////////////////////////////////
                    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
					

					//hitting a Building
					DestructibleBuilding hitBuilding = null;
					try{
						hitBuilding = hit.collider.gameObject.GetComponentUpwards<DestructibleBuilding>();
					}
					catch(NullReferenceException){}
					if(hitBuilding!=null && hitBuilding.IsIntact)
					{
						float damageToBuilding = rb.mass * initialSpeed * BDArmorySettings.DMG_MULTIPLIER/120;
						if(!penetrated)
						{
							damageToBuilding = damageToBuilding/8;
						}
						hitBuilding.AddDamage(damageToBuilding);
						if(hitBuilding.Damage > hitBuilding.impactMomentumThreshold)
						{
							hitBuilding.Demolish();
						}
						if(BDArmorySettings.DRAW_DEBUG_LINES) Debug.Log("[BDArmory]: bullet hit destructible building! Damage: " + (damageToBuilding).ToString("0.00")+ ", total Damage: "+hitBuilding.Damage);
					}
					
					if(hitPart == null || (hitPart!=null && hitPart.vessel!=sourceVessel))
					{
						if(!penetrated && !hasBounced)
						{
							//ricochet
							hasBounced = true;
							if(BDArmorySettings.BULLET_HITS)
							{
								BulletHitFX.CreateBulletHit(hit.point, hit.normal, true);
							}	

							transform.position = hit.point;
							rb.velocity = Vector3.Reflect(rb.velocity, hit.normal);
							rb.velocity = hitAngle/150 * rb.velocity * 0.75f;
							
							Vector3 randomDirection = UnityEngine.Random.rotation * Vector3.one;
							
							rb.velocity = Vector3.RotateTowards(rb.velocity, randomDirection, UnityEngine.Random.Range(0f,5f)*Mathf.Deg2Rad, 0);
						}
						else
						{
							if(BDArmorySettings.BULLET_HITS)
							{
								BulletHitFX.CreateBulletHit(hit.point, hit.normal, false);
							}

							if(bulletType == BulletTypes.Explosive)
							{
								ExplosionFX.CreateExplosion(hit.point, radius, blastPower,-1, sourceVessel, rb.velocity.normalized, explModelPath, explSoundPath);
							}

							Destroy(gameObject); //destroy bullet on collision
						}
					}
				}
			}

			if(airDetonation && distanceFromStart > detonationRange)
			{
				//detonate
				ExplosionFX.CreateExplosion(transform.position, radius, blastPower,-1, sourceVessel, rb.velocity.normalized, explModelPath, explSoundPath);
				Destroy(gameObject); //destroy bullet on collision
			}


			prevPosition = currPosition;
		}
		
		void FadeColor()
		{
			Vector4 currentColorV = new Vector4(currentColor.r, currentColor.g, currentColor.b, currentColor.a);
			Vector4 endColorV = new Vector4(projectileColor.r, projectileColor.g, projectileColor.b, projectileColor.a);
			//float delta = (Vector4.Distance(currentColorV, endColorV)/0.15f) * TimeWarp.fixedDeltaTime;
			float delta = TimeWarp.fixedDeltaTime;
			Vector4 finalColorV = Vector4.MoveTowards(currentColor, endColorV, delta);
			currentColor = new Color(finalColorV.x, finalColorV.y, finalColorV.z, finalColorV.w);
		}

		bool RicochetOnPart(Part p, float angleFromNormal)
		{
			float hitTolerance = p.crashTolerance;
			float chance = ((angleFromNormal/90) * (hitTolerance/100)) * 100;
			float random = UnityEngine.Random.Range(0f,100f);
			//Debug.Log ("Ricochet chance: "+chance);
			if(random < chance)
			{
				return true;
			}
			else
			{
				return false;
			}
		}
	}
}

*/
