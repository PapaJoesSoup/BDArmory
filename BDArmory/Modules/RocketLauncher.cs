using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using BDArmory.Core;
using BDArmory.Core.Extension;
using BDArmory.Core.Utils;
using BDArmory.FX;
using BDArmory.Misc;
using BDArmory.UI;
using KSP.UI.Screens;
using UniLinq;
using UnityEngine;

namespace BDArmory.Modules
{
	public class RocketLauncher : ModuleWeapon
	{
    //the entirety of RocketLauncher (save for FireRocket(), MakeRocketArray(), UpdateRocketscales(), and RocketResource()) is duplicate code
    //to ModuleWeapon. RocketLauncher left as empty childclass for legacy compatibility
	}


	public class Rocket : MonoBehaviour
	{
		public Transform spawnTransform;
		public Vessel sourceVessel;
		public float mass;
		public float thrust;
		public float thrustTime;
		public float blastRadius;
		public float blastForce;
		public float blastHeat;
		public bool proximityDetonation; // adds proxy detonation option for, say, AA rockets
		public float detonationRange;
		public float engageRangeMax;
		public string explModelPath;
		public string explSoundPath;

		public float randomThrustDeviation = 0.05f;

		public Rigidbody parentRB;

		float startTime;
		public AudioSource audioSource;

		Vector3 prevPosition;
		Vector3 currPosition;
		Vector3 startPosition;

		float stayTime = 0.04f;
		float lifeTime = 10;

		//bool isThrusting = true;

		Rigidbody rb;


		KSPParticleEmitter[] pEmitters;

		float randThrustSeed;

		void Start()
		{
			BDArmorySetup.numberOfParticleEmitters++;

			rb = gameObject.AddComponent<Rigidbody>();
			pEmitters = gameObject.GetComponentsInChildren<KSPParticleEmitter>();

			IEnumerator<KSPParticleEmitter> pe = pEmitters.AsEnumerable().GetEnumerator();
			while (pe.MoveNext())
			{
				if (pe.Current == null) continue;
				if (FlightGlobals.getStaticPressure(transform.position) == 0 && pe.Current.useWorldSpace)
				{
					pe.Current.emit = false;
				}
				else if (pe.Current.useWorldSpace)
				{
					BDAGaplessParticleEmitter gpe = pe.Current.gameObject.AddComponent<BDAGaplessParticleEmitter>();
					gpe.rb = rb;
					gpe.emit = true;
				}
				else
				{
					EffectBehaviour.AddParticleEmitter(pe.Current);
				}
			}
			pe.Dispose();

			prevPosition = transform.position;
			currPosition = transform.position;
			startPosition = transform.position;
			startTime = Time.time;

			rb.mass = mass;
			rb.isKinematic = true;
			//rigidbody.velocity = startVelocity;
			if (!FlightGlobals.RefFrameIsRotating) rb.useGravity = false;

			rb.useGravity = false;

			randThrustSeed = UnityEngine.Random.Range(0f, 100f);

			SetupAudio();
		}

		void FixedUpdate()
		{
			//floating origin and velocity offloading corrections
			if (!FloatingOrigin.Offset.IsZero() || !Krakensbane.GetFrameVelocity().IsZero())
			{
				transform.position -= FloatingOrigin.OffsetNonKrakensbane;
				prevPosition -= FloatingOrigin.OffsetNonKrakensbane;
			}
			float distanceFromStart = Vector3.Distance(transform.position, startPosition);

			if (Time.time - startTime < stayTime && transform.parent != null)
			{
				transform.rotation = transform.parent.rotation;
				transform.position = spawnTransform.position;
				//+(transform.parent.rigidbody.velocity*Time.fixedDeltaTime);
			}
			else
			{
				if (transform.parent != null && parentRB)
				{
					transform.parent = null;
					rb.isKinematic = false;
					rb.velocity = parentRB.velocity + Krakensbane.GetFrameVelocityV3f();
				}
			}

			if (rb && !rb.isKinematic)
			{
				//physics
				if (FlightGlobals.RefFrameIsRotating)
				{
					rb.velocity += FlightGlobals.getGeeForceAtPosition(transform.position) * Time.fixedDeltaTime;
				}

				//guidance and attitude stabilisation scales to atmospheric density.
				float atmosMultiplier =
					Mathf.Clamp01(2.5f *
								  (float)
								  FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(transform.position),
									  FlightGlobals.getExternalTemperature(), FlightGlobals.currentMainBody));

				//model transform. always points prograde
				transform.rotation = Quaternion.RotateTowards(transform.rotation,
					Quaternion.LookRotation(rb.velocity + Krakensbane.GetFrameVelocity(), transform.up),
					atmosMultiplier * (0.5f * (Time.time - startTime)) * 50 * Time.fixedDeltaTime);


				if (Time.time - startTime < thrustTime && Time.time - startTime > stayTime)
				{
					float random = randomThrustDeviation * (1 - (Mathf.PerlinNoise(4 * Time.time, randThrustSeed) * 2));
					float random2 = randomThrustDeviation * (1 - (Mathf.PerlinNoise(randThrustSeed, 4 * Time.time) * 2));
					rb.AddRelativeForce(new Vector3(random, random2, thrust));
				}
			}


			if (Time.time - startTime > thrustTime)
			{
				//isThrusting = false;
				IEnumerator<KSPParticleEmitter> pEmitter = pEmitters.AsEnumerable().GetEnumerator();
				while (pEmitter.MoveNext())
				{
					if (pEmitter.Current == null) continue;
					if (pEmitter.Current.useWorldSpace)
					{
						pEmitter.Current.minSize = Mathf.MoveTowards(pEmitter.Current.minSize, 0.1f, 0.05f);
						pEmitter.Current.maxSize = Mathf.MoveTowards(pEmitter.Current.maxSize, 0.2f, 0.05f);
					}
					else
					{
						pEmitter.Current.minSize = Mathf.MoveTowards(pEmitter.Current.minSize, 0, 0.1f);
						pEmitter.Current.maxSize = Mathf.MoveTowards(pEmitter.Current.maxSize, 0, 0.1f);
						if (pEmitter.Current.maxSize == 0)
						{
							pEmitter.Current.emit = false;
						}
					}
				}
				pEmitter.Dispose();
			}

			if (Time.time - startTime > 0.1f + stayTime)
			{
				currPosition = transform.position;
				float dist = (currPosition - prevPosition).magnitude;
				Ray ray = new Ray(prevPosition, currPosition - prevPosition);
				RaycastHit hit;
				KerbalEVA hitEVA = null;
				//if (Physics.Raycast(ray, out hit, dist, 2228224))
				//{
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

				//    if (hitEVA && hitEVA.part.vessel != sourceVessel)
				//    {
				//        Detonate(hit.point);
				//    }
				//}

				if (!hitEVA)
				{
					if (Physics.Raycast(ray, out hit, dist, 9076737))
					{
						Part hitPart = null;
						try
						{
							KerbalEVA eva = hit.collider.gameObject.GetComponentUpwards<KerbalEVA>();
							hitPart = eva ? eva.part : hit.collider.gameObject.GetComponentInParent<Part>();
						}
						catch (NullReferenceException)
						{
						}


						if (hitPart == null || (hitPart != null && hitPart.vessel != sourceVessel))
						{
							Detonate(hit.point);
						}
					}
					else if (FlightGlobals.getAltitudeAtPos(transform.position) < 0)
					{
						Detonate(transform.position);
					}
				}
				if (proximityDetonation && (distanceFromStart >= (detonationRange*10))) //flak rocket functionality
				{
					using (var hitsEnu = Physics.OverlapSphere(transform.position, detonationRange, 557057).AsEnumerable().GetEnumerator())
					{
						while (hitsEnu.MoveNext())
						{
							if (hitsEnu.Current == null) continue;

							try
							{
								Part partHit = hitsEnu.Current.GetComponentInParent<Part>();
								if (partHit?.vessel == sourceVessel) continue;

								Detonate(transform.position);
							}
							catch
							{
								// ignored
							}
						}
					}
				}
			}
			else if (FlightGlobals.getAltitudeAtPos(currPosition) <= 0)
			{
				Detonate(currPosition);
			}
			prevPosition = currPosition;

			if (Time.time - startTime > lifeTime) // life's 10s, quite a long time for faster rockets
			{
				Detonate(transform.position);
			}
			if (distanceFromStart >= 8000)//rockets are performance intensive, lets cull those that have flown too far away
			{
				Detonate(transform.position);
			}
		}

		void Update()
		{
			if (HighLogic.LoadedSceneIsFlight)
			{
				if (BDArmorySetup.GameIsPaused)
				{
					if (audioSource.isPlaying)
					{
						audioSource.Stop();
					}
				}
				else
				{
					if (!audioSource.isPlaying)
					{
						audioSource.Play();
					}
				}
			}
		}
		void Detonate(Vector3 pos)
		{
			BDArmorySetup.numberOfParticleEmitters--;

			ExplosionFx.CreateExplosion(pos, BlastPhysicsUtils.CalculateExplosiveMass(blastRadius),
				explModelPath, explSoundPath, true);

			IEnumerator<KSPParticleEmitter> emitter = pEmitters.AsEnumerable().GetEnumerator();
			while (emitter.MoveNext())
			{
				if (emitter.Current == null) continue;
				if (!emitter.Current.useWorldSpace) continue;
				emitter.Current.gameObject.AddComponent<BDAParticleSelfDestruct>();
				emitter.Current.transform.parent = null;
			}
			emitter.Dispose();
			Destroy(gameObject); //destroy rocket on collision
		}


		void SetupAudio()
		{
			audioSource = gameObject.AddComponent<AudioSource>();
			audioSource.loop = true;
			audioSource.minDistance = 1;
			audioSource.maxDistance = 2000;
			audioSource.dopplerLevel = 0.5f;
			audioSource.volume = 0.9f * BDArmorySettings.BDARMORY_WEAPONS_VOLUME;
			audioSource.pitch = 1f;
			audioSource.priority = 255;
			audioSource.spatialBlend = 1;

			audioSource.clip = GameDatabase.Instance.GetAudioClip("BDArmory/Sounds/rocketLoop");

			UpdateVolume();
			BDArmorySetup.OnVolumeChange += UpdateVolume;
		}

		void UpdateVolume()
		{
			if (audioSource)
			{
				audioSource.volume = BDArmorySettings.BDARMORY_WEAPONS_VOLUME;
			}
		}
	}
}
