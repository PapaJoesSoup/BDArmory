using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace BahaTurret
{

	
	public class MissileLauncher : PartModule, IBDWeapon
	{
		public enum MissileStates{Idle, Drop, Boost, Cruise, PostThrust}

		public Transform missileReferenceTransform;

		public enum GuidanceModes{None,AAMLead,AAMPure,AGM,AGMBallistic,Cruise,STS,Bomb,RCS, BeamRiding}
		public GuidanceModes guidanceMode;
		[KSPField]
		public string homingType = "AAM";

		[KSPField]
		public string targetingType = "none";
		public enum TargetingModes{None,Radar,Heat,Laser,GPS,AntiRad}
		public TargetingModes targetingMode;
		public bool team;
		
		public float timeFired = -1;
		public float timeIndex = 0;

		public MissileTurret missileTurret = null;
		public BDRotaryRail rotaryRail = null;

		[KSPField]
		public string exhaustPrefabPath;

		[KSPField]
		public string boostExhaustPrefabPath;

		[KSPField]
		public string boostExhaustTransformName;

		//aero
		[KSPField]
		public bool aero = false;
		[KSPField]
		public float liftArea = 0.015f;
		[KSPField]
		public float steerMult = 0.5f;
		[KSPField]
		public float torqueRampUp = 30f;
		Vector3 aeroTorque = Vector3.zero;
		float controlAuthority = 0;
		float finalMaxTorque = 0;
		[KSPField]
		public float aeroSteerDamping = 0;

		[KSPField]
		public float maxTorque = 90;
		//

		[KSPField]
		public float thrust = 30;
		[KSPField]
		public float cruiseThrust = 3;
		[KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Drop Time"),
        	UI_FloatRange(minValue = 0f, maxValue = 2f, stepIncrement = 0.1f, scene = UI_Scene.Editor)]
		public float dropTime = 0.4f;
		[KSPField]
		public float boostTime = 2.2f;
		[KSPField]
		public float cruiseTime = 45;
		[KSPField]
		public float cruiseDelay = 0;
		[KSPField]
		public bool guidanceActive = true;
		[KSPField]
		public float maxOffBoresight = 45;
		
		[KSPField]
		public float maxAoA = 35;
		
		[KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Decouple Speed"),
        	UI_FloatRange(minValue = 0f, maxValue = 10f, stepIncrement = 0.5f, scene = UI_Scene.Editor)]
		public float decoupleSpeed = 0;
		
		[KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Direction: "), 
			UI_Toggle(disabledText = "Lateral", enabledText = "Forward")]
		public bool decoupleForward = false;
		


		[KSPField]
		public float optimumAirspeed = 220;
		
		[KSPField]
		public float blastRadius = 150;
		[KSPField]
		public float blastPower = 25;
		[KSPField]
		public float blastHeat = -1;
		[KSPField]
		public float maxTurnRateDPS = 20;
		[KSPField]
		public bool proxyDetonate = true;
		
		[KSPField]
		public string audioClipPath = string.Empty;

		AudioClip thrustAudio;

		[KSPField]
		public string boostClipPath = string.Empty;

		AudioClip boostAudio;
		
		[KSPField]
		public bool isSeismicCharge = false;
		
		[KSPField]
		public float rndAngVel = 0;
		
		[KSPField]
		public bool isTimed = false;
		
		[KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Detonation Time"),
        	UI_FloatRange(minValue = 2f, maxValue = 30f, stepIncrement = 0.5f, scene = UI_Scene.Editor)]
		public float detonationTime = 2;

		[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Cruise Altitude"),
		 UI_FloatRange(minValue = 30, maxValue = 2500f, stepIncrement = 5f, scene = UI_Scene.All)]
		public float cruiseAltitude = 500;

		[KSPField]
		public string rotationTransformName = string.Empty;
		Transform rotationTransform;

		[KSPField]
		public bool terminalManeuvering = false;

		[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "In Cargo Bay: "), 
			UI_Toggle(disabledText = "False", enabledText = "True", affectSymCounterparts = UI_Scene.All)]
		public bool inCargoBay = false;
		
		
		[KSPField]
		public string explModelPath = "BDArmory/Models/explosion/explosion";
		
		public string explSoundPath = "BDArmory/Sounds/explode1";
			
		
		[KSPField]
		public bool spoolEngine = false;
		
		[KSPField]
		public bool hasRCS = false;
		[KSPField]
		public float rcsThrust = 1;
		float rcsRVelThreshold = 0.13f;
		KSPParticleEmitter upRCS;
		KSPParticleEmitter downRCS;
		KSPParticleEmitter leftRCS;
		KSPParticleEmitter rightRCS;
		KSPParticleEmitter forwardRCS;
		float rcsAudioMinInterval = 0.2f;

		public Vessel sourceVessel = null;
		bool checkMiss = false;
		bool hasExploded = false;
		private AudioSource audioSource;
		public AudioSource sfAudioSource;
		List<KSPParticleEmitter> pEmitters;
		List<BDAGaplessParticleEmitter> gaplessEmitters;
		
		public MissileFire targetMf = null;
		public bool hasFired = false;


		LineRenderer LR;
		float cmTimer;
		
		//deploy animation
		[KSPField]
		public string deployAnimationName = "";
		
		[KSPField]
		public float deployedDrag = 0.02f;
		
		[KSPField]
		public float deployTime = 0.2f;

		[KSPField]
		public bool useSimpleDrag = false;
		[KSPField]
		public float simpleDrag = 0.02f;
		[KSPField]
		public float simpleStableTorque = 5;

		[KSPField]
		public Vector3 simpleCoD = new Vector3(0,0,-1);

		[KSPField]
		public float agmDescentRatio = 1.45f;
		
		float currentThrust = 0;
		
		public bool deployed;
		//public float deployedTime;
		
		AnimationState[] deployStates;
		
		bool hasPlayedFlyby = false;
	
		float debugTurnRate = 0;
		string debugString = "";


		List<GameObject> boosters;
		[KSPField]
		public bool decoupleBoosters = false;
		[KSPField]
		public float boosterDecoupleSpeed = 5;
		[KSPField]
		public float boosterMass = 0;

		public float timeToImpact;
		public bool targetAcquired = false;
		public Vector3 targetPosition = Vector3.zero;
		Vector3 targetVelocity = Vector3.zero;
		Vector3 targetAcceleration = Vector3.zero;

		public Vessel legacyTargetVessel;


		Transform vesselReferenceTransform;

		[KSPField]
		public string boostTransformName = string.Empty;
		List<KSPParticleEmitter> boostEmitters;
		List<BDAGaplessParticleEmitter> boostGaplessEmitters;

		public MissileStates MissileState = MissileStates.Idle;

		[KSPField]
		public float lockedSensorFOV = 2.5f;


		//laser stuff
		public ModuleTargetingCamera lockedCamera = null;
		Vector3 lastLaserPoint;
		Vector3 laserStartPosition;
		Vector3 startDirection;


		//heat stuff
		public TargetSignatureData heatTarget;
		[KSPField]
		public float heatThreshold = 200;
		[KSPField]
		public bool allAspect = false;



		float lockFailTimer = -1;
		public bool hasMissed = false;

		//radar stuff
		//public ModuleRadar radar;
		public VesselRadarData vrd;
		public TargetSignatureData radarTarget;
		[KSPField]
		public float activeRadarRange = 6000;
		public bool activeRadar = false;
		float lastRWRPing = 0;
		[KSPField]
		public float activeRadarMinThresh = 140;
		[KSPField]
		public bool radarLOAL = false;
		bool radarLOALSearching = false;

		//GPS stuff
		public Vector3d targetGPSCoords;


		//torpedo
		[KSPField]
		public bool torpedo = false;
		[KSPField]
		public float waterImpactTolerance = 25;


		//weapon interface
		[KSPField]
		public string missileType = "missile";
		private WeaponClasses weaponClass;
		public WeaponClasses GetWeaponClass()
		{
			return weaponClass;
		}
		void ParseWeaponClass()
		{
			missileType = missileType.ToLower();
			if(missileType == "bomb")
			{
				weaponClass = WeaponClasses.Bomb;
			}
			else
			{
				weaponClass = WeaponClasses.Missile;
			}
		}
		[KSPField]
		public string shortName = string.Empty;
		public string GetShortName()
		{
			return shortName;
		}
		public Part GetPart()
		{
			return part;
		}
		string tModeString;
		public string GetSubLabel()
		{
			if(tModeString == "None")
			{
				return string.Empty;
			}
			else
			{
				return tModeString;
			}
		}

		//firing paramters
		[KSPField]
		public float minLaunchSpeed = 0; //only used by AI
		[KSPField]
		public float minStaticLaunchRange = 10;
		[KSPField]
		public float maxStaticLaunchRange = 3000;


		//ballistic options
		[KSPField]
		public bool indirect = false;

		public override void OnStart(PartModule.StartState state)
		{
			ParseWeaponClass();

			if(shortName == string.Empty)
			{
				shortName = part.partInfo.title;
			}

			gaplessEmitters = new List<BDAGaplessParticleEmitter>();
			pEmitters = new List<KSPParticleEmitter>();
			boostEmitters = new List<KSPParticleEmitter>();
			boostGaplessEmitters = new List<BDAGaplessParticleEmitter>();

			if(isTimed)
			{
				Fields["detonationTime"].guiActive = true;
				Fields["detonationTime"].guiActiveEditor = true;
			}
			else
			{
				Fields["detonationTime"].guiActive = false;
				Fields["detonationTime"].guiActiveEditor = false;
			}

			ParseModes();

			foreach(var emitter in part.FindModelComponents<KSPParticleEmitter>())
			{
				emitter.emit = false;
			}

			if(HighLogic.LoadedSceneIsFlight)
			{
				missileReferenceTransform = part.FindModelTransform("missileTransform");
				if(!missileReferenceTransform)
				{
					missileReferenceTransform = part.partTransform;
				}

				if(!string.IsNullOrEmpty(exhaustPrefabPath))
				{
					foreach(var t in part.FindModelTransforms("exhaustTransform"))
					{
						GameObject exhaustPrefab = (GameObject)Instantiate(GameDatabase.Instance.GetModel(exhaustPrefabPath));
						exhaustPrefab.SetActive(true);
						foreach(var emitter in exhaustPrefab.GetComponentsInChildren<KSPParticleEmitter>())
						{
							emitter.emit = false;
						}
						exhaustPrefab.transform.parent = t;
						exhaustPrefab.transform.localPosition = Vector3.zero;
						exhaustPrefab.transform.localRotation = Quaternion.identity;
					}
				}

				if(!string.IsNullOrEmpty(boostExhaustPrefabPath) && !string.IsNullOrEmpty(boostExhaustTransformName))
				{
					foreach(var t in part.FindModelTransforms(boostExhaustTransformName))
					{
						GameObject exhaustPrefab = (GameObject)Instantiate(GameDatabase.Instance.GetModel(boostExhaustPrefabPath));
						exhaustPrefab.SetActive(true);
						foreach(var emitter in exhaustPrefab.GetComponentsInChildren<KSPParticleEmitter>())
						{
							emitter.emit = false;
						}
						exhaustPrefab.transform.parent = t;
						exhaustPrefab.transform.localPosition = Vector3.zero;
						exhaustPrefab.transform.localRotation = Quaternion.identity;
					}
				}


				boosters = new List<GameObject>();
				if(!string.IsNullOrEmpty(boostTransformName))
				{
					foreach(var t in part.FindModelTransforms(boostTransformName))
					{
						boosters.Add(t.gameObject);

						foreach(var be in t.GetComponentsInChildren<KSPParticleEmitter>())
						{
							if(be.useWorldSpace)
							{
								if(!be.GetComponent<BDAGaplessParticleEmitter>())
								{
									BDAGaplessParticleEmitter ge = be.gameObject.AddComponent<BDAGaplessParticleEmitter>();
									ge.part = part;
									boostGaplessEmitters.Add(ge);
								}
							}
							else
							{
								if(!boostEmitters.Contains(be))
								{
									boostEmitters.Add(be);
								}
							}
						}
					}
				}

				foreach(var emitter in part.partTransform.FindChild("model").GetComponentsInChildren<KSPParticleEmitter>())
				{
					if(emitter.GetComponent<BDAGaplessParticleEmitter>() || boostEmitters.Contains(emitter))
					{
						continue;
					}

					if(emitter.useWorldSpace)
					{
						BDAGaplessParticleEmitter gaplessEmitter = emitter.gameObject.AddComponent<BDAGaplessParticleEmitter>();	
						gaplessEmitter.part = part;
						gaplessEmitters.Add (gaplessEmitter);
					}
					else
					{
						if(emitter.transform.name != boostTransformName)
						{
							pEmitters.Add(emitter);	
						}
						else
						{
							boostEmitters.Add(emitter);
						}
					}
				}
					
				cmTimer = Time.time;
				
				part.force_activate();

			
				
				foreach(var pe in pEmitters)	
				{
					if(hasRCS)
					{
						if(pe.gameObject.name == "rcsUp") upRCS = pe;
						else if(pe.gameObject.name == "rcsDown") downRCS = pe;
						else if(pe.gameObject.name == "rcsLeft") leftRCS = pe;
						else if(pe.gameObject.name == "rcsRight") rightRCS = pe;
						else if(pe.gameObject.name == "rcsForward") forwardRCS = pe;
					}
					
					if(!pe.gameObject.name.Contains("rcs") && !pe.useWorldSpace)
					{
						pe.sizeGrow = 99999;
					}
				}

				if(rotationTransformName!=string.Empty)
				{
					rotationTransform = part.FindModelTransform(rotationTransformName);
				}






				
				if(hasRCS)
				{
					SetupRCS();
					KillRCS();
				}

				SetupAudio();



			}

			if(guidanceMode != GuidanceModes.Cruise)
			{
				Fields["cruiseAltitude"].guiActive = false;
				Fields["cruiseAltitude"].guiActiveEditor = false;
			}
			
			if(part.partInfo.title.Contains("Bomb"))
			{
				Fields["dropTime"].guiActive = false;
				Fields["dropTime"].guiActiveEditor = false;
			}
			
			if(deployAnimationName != "")
			{
				deployStates = Misc.SetUpAnimation(deployAnimationName, part);
			}
			else
			{
				deployedDrag = simpleDrag;	
			}
		}

		/*
		void OnCollisionEnter(Collision col)
		{
			if(!hasExploded && hasFired && Time.time - timeFired > 1)
			{
				Detonate();
			}
		}
		*/

		void SetupAudio()
		{
			audioSource = gameObject.AddComponent<AudioSource>();
			audioSource.minDistance = 1;
			audioSource.maxDistance = 1000;
			audioSource.loop = true;
			audioSource.pitch = 1f;
			audioSource.priority = 255;
			audioSource.spatialBlend = 1;

			if(audioClipPath!=string.Empty)
			{
				audioSource.clip = GameDatabase.Instance.GetAudioClip(audioClipPath);
			}

			sfAudioSource = gameObject.AddComponent<AudioSource>();
			sfAudioSource.minDistance = 1;
			sfAudioSource.maxDistance = 2000;
			sfAudioSource.dopplerLevel = 0;
			sfAudioSource.priority = 230;
			sfAudioSource.spatialBlend = 1;



			if(audioClipPath != string.Empty)
			{
				thrustAudio = GameDatabase.Instance.GetAudioClip(audioClipPath);
			}

			if(boostClipPath != string.Empty)
			{
				boostAudio = GameDatabase.Instance.GetAudioClip(boostClipPath);
			}

			UpdateVolume();
			BDArmorySettings.OnVolumeChange += UpdateVolume;
		}

		void UpdateVolume()
		{
			if(audioSource)
			{
				audioSource.volume = BDArmorySettings.BDARMORY_WEAPONS_VOLUME;
			}
			if(sfAudioSource)
			{
				sfAudioSource.volume = BDArmorySettings.BDARMORY_WEAPONS_VOLUME;
			}
		}

		void OnDestroy()
		{
			BDArmorySettings.OnVolumeChange -= UpdateVolume;
		}
		
		[KSPAction("Fire Missile")]
		public void AGFire(KSPActionParam param)
		{
			if(BDArmorySettings.Instance.ActiveWeaponManager != null && BDArmorySettings.Instance.ActiveWeaponManager.vessel == vessel) BDArmorySettings.Instance.ActiveWeaponManager.SendTargetDataToMissile(this);
			if(missileTurret)
			{
				missileTurret.FireMissile(this);
			}
			else if(rotaryRail)
			{
				rotaryRail.FireMissile(this);
			}
			else
			{
				FireMissile();	
			}
			if(BDArmorySettings.Instance.ActiveWeaponManager!=null) BDArmorySettings.Instance.ActiveWeaponManager.UpdateList();
		}
		
		[KSPEvent(guiActive = true, guiName = "Fire Missile", active = true)]
		public void GuiFire()
		{
			if(BDArmorySettings.Instance.ActiveWeaponManager != null && BDArmorySettings.Instance.ActiveWeaponManager.vessel == vessel) BDArmorySettings.Instance.ActiveWeaponManager.SendTargetDataToMissile(this);
			if(missileTurret)
			{
				missileTurret.FireMissile(this);
			}
			else if(rotaryRail)
			{
				rotaryRail.FireMissile(this);
			}
			else
			{
				FireMissile();	
			}
			if(BDArmorySettings.Instance.ActiveWeaponManager!=null) BDArmorySettings.Instance.ActiveWeaponManager.UpdateList();
		}

		[KSPEvent(guiActive = true, guiActiveEditor = false, active = true, guiName = "Jettison")]
		public void Jettison()
		{
			if(missileTurret) return;

			part.decouple(0);
			if(BDArmorySettings.Instance.ActiveWeaponManager!=null) BDArmorySettings.Instance.ActiveWeaponManager.UpdateList();
		}
		
		
		public void FireMissile()
		{
			if(!hasFired)
			{
				

				hasFired = true;

				GameEvents.onPartDie.Add(PartDie);

				BDATargetManager.FiredMissiles.Add(this);

				if(GetComponentInChildren<KSPParticleEmitter>())
				{
					BDArmorySettings.numberOfParticleEmitters++;
				}
				
				foreach(var wpm in vessel.FindPartModulesImplementing<MissileFire>())
				{
					team = wpm.team;	
					break;
				}
				
				sfAudioSource.PlayOneShot(GameDatabase.Instance.GetAudioClip("BDArmory/Sounds/deployClick"));
				
				sourceVessel = vessel;



				//TARGETING
				targetPosition = transform.position + (transform.forward * 5000); //set initial target position so if no target update, missile will count a miss if it nears this point or is flying post-thrust
				startDirection = transform.forward;
				if(BDArmorySettings.ALLOW_LEGACY_TARGETING)
				{
					if(vessel.targetObject!=null && vessel.targetObject.GetVessel()!=null)
					{
						legacyTargetVessel = vessel.targetObject.GetVessel();

						foreach(var mf in legacyTargetVessel.FindPartModulesImplementing<MissileFire>())
						{
							targetMf = mf;
							break;
						}

						if(targetingMode == TargetingModes.Heat)
						{
							heatTarget = new TargetSignatureData(legacyTargetVessel, 9999);
						}
					}
				}
				if(targetingMode == TargetingModes.Laser)
				{
					laserStartPosition = transform.position;
					if(lockedCamera)
					{
						targetAcquired = true;
						targetPosition = lastLaserPoint = lockedCamera.groundTargetPosition;
						targetingPod = lockedCamera;
					}
				}
				else if(targetingMode == TargetingModes.AntiRad && targetAcquired)
				{
					RadarWarningReceiver.OnRadarPing += ReceiveRadarPing;
				}

				part.decouple(0);
				part.force_activate();
				part.Unpack();
				vessel.situation = Vessel.Situations.FLYING;
				part.rb.isKinematic = false;
				BDArmorySettings.Instance.ApplyNewVesselRanges(vessel);
				part.bodyLiftMultiplier = 0;
				part.dragModel = Part.DragModel.NONE;

				//add target info to vessel
				TargetInfo info = vessel.gameObject.AddComponent<TargetInfo>();
				info.team = BDATargetManager.BoolToTeam(team);
				info.isMissile = true;
				info.missileModule = this;

				StartCoroutine(DecoupleRoutine());
				

				

				vessel.vesselName = GetShortName();
				vessel.vesselType = VesselType.Probe;

				
				timeFired = Time.time;


				//setting ref transform for navball
				GameObject refObject = new GameObject();
				refObject.transform.rotation = Quaternion.LookRotation(-transform.up, transform.forward);
				refObject.transform.parent = transform;
				part.SetReferenceTransform(refObject.transform);
				vessel.SetReferenceTransform(part);
				vesselReferenceTransform = refObject.transform;

				MissileState = MissileStates.Drop;

				part.crashTolerance = 9999;


				StartCoroutine(MissileRoutine());
				
			}
		}

		IEnumerator DecoupleRoutine()
		{
			yield return new WaitForFixedUpdate();

			if(rndAngVel > 0)
			{
				part.rb.angularVelocity += UnityEngine.Random.insideUnitSphere.normalized * rndAngVel;	
			}


			if(decoupleForward)
			{
				part.rb.velocity += decoupleSpeed * part.transform.forward;
			}
			else
			{
				part.rb.velocity += decoupleSpeed * -part.transform.up;
			}

		}

		/// <summary>
		/// Fires the missile on target vessel.  Used by AI currently.
		/// </summary>
		/// <param name="v">V.</param>
		public void FireMissileOnTarget(Vessel v)
		{
			if(!hasFired)
			{
				legacyTargetVessel = v;

				FireMissile();
			}
		}
		
		void OnDisable()
		{
			if(targetingMode == TargetingModes.AntiRad)
			{
				RadarWarningReceiver.OnRadarPing -= ReceiveRadarPing;
			}
		}
		
		
		public override void OnFixedUpdate()
		{
			debugString = "";
			if(hasFired && !hasExploded && part!=null)
			{
				part.rb.isKinematic = false;
				AntiSpin();

				//simpleDrag
				if(useSimpleDrag)
				{
					SimpleDrag();
				}

				//flybyaudio
				float mCamDistanceSqr = (FlightCamera.fetch.mainCamera.transform.position-transform.position).sqrMagnitude;
				float mCamRelVSqr = (float)(FlightGlobals.ActiveVessel.srf_velocity-vessel.srf_velocity).sqrMagnitude;
				if(!hasPlayedFlyby 
				   && FlightGlobals.ActiveVessel != vessel 
				   && FlightGlobals.ActiveVessel != sourceVessel 
				   && mCamDistanceSqr < 400*400 && mCamRelVSqr > 300*300  
				   && mCamRelVSqr < 800*800 
					&& Vector3.Angle(vessel.srf_velocity, FlightGlobals.ActiveVessel.transform.position-transform.position)<60)
				{
					sfAudioSource.PlayOneShot (GameDatabase.Instance.GetAudioClip("BDArmory/Sounds/missileFlyby"));	
					hasPlayedFlyby = true;
				}
				
				if(vessel.isActiveVessel)
				{
					audioSource.dopplerLevel = 0;
				}
				else
				{
					audioSource.dopplerLevel = 1f;
				}

				
				//Missile State
				timeIndex = Time.time - timeFired;

				if(timeIndex > 0.5f)
				{
					if(torpedo)
					{
						if(vessel.altitude > 0)
						{
							part.crashTolerance = waterImpactTolerance;
						}
						else
						{
							part.crashTolerance = 1;
						}
					}
					else
					{
						part.crashTolerance = 1;
					}
				}
					
				
				UpdateThrustForces();

				UpdateGuidance();

				RaycastCollisions();

				//Timed detonation
				if(isTimed && timeIndex > detonationTime)
				{
					part.temperature = part.maxTemp+100;
				}
			}
		}

		Vector3 previousPos;
		void RaycastCollisions()
		{
			if(weaponClass == WeaponClasses.Bomb) return;

			if(timeIndex > 1f && vessel.srfSpeed > part.crashTolerance)
			{
				/*
				RaycastHit[] hits = Physics.RaycastAll(new Ray(previousPos, part.transform.position - previousPos), (part.transform.position - previousPos).magnitude, 557057);
				for(int i = 0; i < hits.Length; i++)
				{
					if(hits[i].collider.gameObject.layer == 0 && hits[i].collider.attachedRigidbody && hits[i].collider.attachedRigidbody == part.rb)
					{
						continue;
					}
					else
					{
						Debug.Log(part.partInfo.title + " raycast detonated on " + (hits[i].collider.attachedRigidbody ? hits[i].collider.attachedRigidbody.gameObject.name : hits[i].collider.gameObject.name));
						part.temperature = part.maxTemp + 100;
					}
				}
				*/

				RaycastHit lineHit;
				if(Physics.Linecast(part.transform.position, previousPos, out lineHit, 557057))
				{
					if(lineHit.collider.GetComponentInParent<Part>() != part)
					{
						Debug.Log(part.partInfo.title + " linecast hit on " + (lineHit.collider.attachedRigidbody ? lineHit.collider.attachedRigidbody.gameObject.name : lineHit.collider.gameObject.name));
						part.temperature = part.maxTemp + 100;
					}
				}
			}

			previousPos = part.transform.position;
		}

		void UpdateGuidance()
		{
			if(guidanceActive)
			{
				if(BDArmorySettings.ALLOW_LEGACY_TARGETING && legacyTargetVessel)
				{
					UpdateLegacyTarget();
				}

				if(targetingMode == TargetingModes.Heat)
				{
					UpdateHeatTarget();
				}
				else if(targetingMode == TargetingModes.Radar)
				{
					UpdateRadarTarget();
				}
				else if(targetingMode == TargetingModes.Laser)
				{
					UpdateLaserTarget();
				}
				else if(targetingMode == TargetingModes.GPS)
				{
					UpdateGPSTarget();
				}
				else if(targetingMode == TargetingModes.AntiRad)
				{
					UpdateAntiRadiationTarget();
				}

			}

			if(MissileState != MissileStates.Idle && MissileState != MissileStates.Drop) //guidance
			{
				//guidance and attitude stabilisation scales to atmospheric density. //use part.atmDensity
				float atmosMultiplier = Mathf.Clamp01 (2.5f*(float)FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(transform.position), FlightGlobals.getExternalTemperature(), FlightGlobals.currentMainBody)); 

				if(vessel.srfSpeed < optimumAirspeed)
				{
					float optimumSpeedFactor = (float)vessel.srfSpeed / (2 * optimumAirspeed);
					controlAuthority = Mathf.Clamp01(atmosMultiplier * (-Mathf.Abs(2 * optimumSpeedFactor - 1) + 1));
				}
				else
				{
					controlAuthority = Mathf.Clamp01(atmosMultiplier);
				}
				debugString += "\ncontrolAuthority: "+controlAuthority;

				if(guidanceActive)// && timeIndex - dropTime > 0.5f)
				{
					WarnTarget();
					Vector3 targetPosition = Vector3.zero;

					if(legacyTargetVessel && legacyTargetVessel.loaded)
					{
						Vector3 targetCoMPos = legacyTargetVessel.CoM;
						targetPosition = targetCoMPos+legacyTargetVessel.rb_velocity*Time.fixedDeltaTime;
					}

					//increaseTurnRate after launch
					float turnRateDPS = Mathf.Clamp(((timeIndex-dropTime)/boostTime)*maxTurnRateDPS * 25f, 0, maxTurnRateDPS);
					if(!hasRCS)
					{
						turnRateDPS *= controlAuthority;
					}

					//decrease turn rate after thrust cuts out
					if(timeIndex > dropTime+boostTime+cruiseTime)
					{
						turnRateDPS = atmosMultiplier * Mathf.Clamp(maxTurnRateDPS - ((timeIndex-dropTime-boostTime-cruiseTime)*0.45f), 1, maxTurnRateDPS);	
						if(hasRCS) 
						{
							turnRateDPS = 0;
						}
					}

					if(hasRCS)
					{
						if(turnRateDPS > 0)
						{
							DoRCS();
						}
						else
						{
							KillRCS();
						}
					}
					debugTurnRate = turnRateDPS;

					finalMaxTorque = Mathf.Clamp((timeIndex-dropTime)*torqueRampUp, 0, maxTorque); //ramp up torque

					if(guidanceMode == GuidanceModes.AAMLead)
					{
						AAMGuidance();
					}
					else if(guidanceMode == GuidanceModes.AGM)
					{
						AGMGuidance();
					}
					else if(guidanceMode == GuidanceModes.AGMBallistic)
					{
						AGMBallisticGuidance();
					}
					else if(guidanceMode == GuidanceModes.BeamRiding)
					{
						BeamRideGuidance();
					}
					else if(guidanceMode == GuidanceModes.RCS)
					{
						part.transform.rotation = Quaternion.RotateTowards(part.transform.rotation, Quaternion.LookRotation(targetPosition-part.transform.position, part.transform.up), turnRateDPS*Time.fixedDeltaTime);
					}
					else if(guidanceMode == GuidanceModes.Cruise)
					{
						CruiseGuidance();
					}


				}
				else
				{
					CheckMiss();
					targetMf = null;
					if(aero)
					{
						aeroTorque = MissileGuidance.DoAeroForces(this, transform.position + (20*vessel.srf_velocity), liftArea, .25f, aeroTorque, maxTorque, maxAoA);
					}
				}

				if(aero && aeroSteerDamping > 0)
				{
					part.rb.AddRelativeTorque(-aeroSteerDamping * part.transform.InverseTransformVector(part.rb.angularVelocity));
				}

				if(hasRCS && !guidanceActive)
				{
					KillRCS();	
				}
			}
		}

		void UpdateThrustForces()
		{
			if(currentThrust > 0)
			{
				part.rb.AddRelativeForce(currentThrust * Vector3.forward);
			}
		}

		IEnumerator MissileRoutine()
		{
			MissileState = MissileStates.Drop;
			StartCoroutine(AnimRoutine());
			yield return new WaitForSeconds(dropTime);
			yield return StartCoroutine(BoostRoutine());
			yield return new WaitForSeconds(cruiseDelay);
			yield return StartCoroutine(CruiseRoutine());
		}

		IEnumerator AnimRoutine()
		{
			yield return new WaitForSeconds(deployTime);
 
			if(!string.IsNullOrEmpty(deployAnimationName))
			{
				deployed = true;
			
				foreach(var anim in deployStates)
				{
					anim.speed = 1;
				}
			}
		}

		IEnumerator BoostRoutine()
		{
			StartBoost();
			float boostStartTime = Time.time;
			while(Time.time-boostStartTime < boostTime)
			{
				//light, sound & particle fx
				//sound
				if(!BDArmorySettings.GameIsPaused)
				{
					if(!audioSource.isPlaying)
					{
						audioSource.Play();	
					}
				}
				else if(audioSource.isPlaying)
				{
					audioSource.Stop();
				}

				//particleFx
				foreach(var emitter in boostEmitters)
				{
					if(!hasRCS)
					{
						emitter.sizeGrow = Mathf.Lerp(emitter.sizeGrow, 0, 20*Time.deltaTime);
					}
				}
				foreach(var gpe in boostGaplessEmitters)
				{
					if(vessel.atmDensity > 0)
					{
						gpe.emit = true;
						//gpe.pEmitter.worldVelocity = ParticleTurbulence.Turbulence;
						gpe.pEmitter.worldVelocity = 2*ParticleTurbulence.flareTurbulence;
					}
					else
					{
						gpe.emit = false;
					}	
				}

				//thrust
				if(spoolEngine) 
				{
					currentThrust = Mathf.MoveTowards(currentThrust, thrust, thrust/10);
				}

				yield return null;
			}
			EndBoost();
		}
		//boost
		void StartBoost()
		{
			MissileState = MissileStates.Boost;

			if(boostAudio)
			{
				audioSource.clip = boostAudio;
			}
			else if(thrustAudio)
			{
				audioSource.clip = thrustAudio;
			}

			foreach(Light light in gameObject.GetComponentsInChildren<Light>())
			{
				light.intensity = 1.5f;	
			}


			if(!spoolEngine)
			{
				currentThrust = thrust;	
			}

			if(string.IsNullOrEmpty(boostTransformName))
			{
				boostEmitters = pEmitters;
				boostGaplessEmitters = gaplessEmitters;
			}
			foreach(var emitter in boostEmitters)
			{
				emitter.emit = true;
			}

			if(hasRCS)
			{
				forwardRCS.emit = true;
			}

			if(thrust > 0)
			{
				sfAudioSource.PlayOneShot(GameDatabase.Instance.GetAudioClip("BDArmory/Sounds/launch"));
				RadarWarningReceiver.WarnMissileLaunch(transform.position, transform.forward);
			}

		}
		void EndBoost()
		{
			foreach(var emitter in boostEmitters)
			{
				if(!emitter) continue;
				emitter.emit = false;
			}

			foreach(var emitter in boostGaplessEmitters)
			{
				if(!emitter) continue;
				emitter.emit = false;
			}

			if(decoupleBoosters)
			{
				part.mass -= boosterMass;
				foreach(var booster in boosters)
				{
					if(!booster) continue;
					(booster.AddComponent<DecoupledBooster>()).DecoupleBooster(part.rb.velocity, boosterDecoupleSpeed);
				}
			}

			if(cruiseDelay > 0)
			{
				currentThrust = 0;
			}
		}

		IEnumerator CruiseRoutine()
		{
			StartCruise();
			float cruiseStartTime = Time.time;
			while(Time.time - cruiseStartTime < cruiseTime)
			{
				if(!BDArmorySettings.GameIsPaused)
				{
					if(!audioSource.isPlaying || audioSource.clip != thrustAudio)
					{
						audioSource.clip = thrustAudio;
						audioSource.Play();	
					}
				}
				else if(audioSource.isPlaying)
				{
					audioSource.Stop();
				}

				//particleFx
				foreach(var emitter in pEmitters)
				{
					if(!hasRCS)
					{
						emitter.sizeGrow = Mathf.Lerp(emitter.sizeGrow, 0, 20*Time.deltaTime);
					}
				}
				foreach(var gpe in gaplessEmitters)
				{
					if(vessel.atmDensity > 0)
					{
						gpe.emit = true;
						//gpe.pEmitter.worldVelocity = ParticleTurbulence.Turbulence;
						gpe.pEmitter.worldVelocity = 2*ParticleTurbulence.flareTurbulence;
					}
					else
					{
						gpe.emit = false;
					}	
				}

				if(spoolEngine)
				{
					currentThrust = Mathf.MoveTowards(currentThrust, cruiseThrust, cruiseThrust/10);
				}



				yield return null;
			}
			EndCruise();
		}

		void StartCruise()
		{
			MissileState = MissileStates.Cruise;

			if(thrustAudio)	
			{
				audioSource.clip = thrustAudio;
			}

			if(spoolEngine)
			{
				currentThrust = 0;
			}
			else
			{
				currentThrust = cruiseThrust;	
			}

			foreach(var emitter in pEmitters)
			{
				emitter.emit = true;
			}

			foreach(var emitter in gaplessEmitters)
			{
				emitter.emit = true;
			}

			if(hasRCS)
			{
				forwardRCS.emit = false;
				audioSource.Stop();
			}
		}

		void EndCruise()
		{
			MissileState = MissileStates.PostThrust;

			foreach(Light light in gameObject.GetComponentsInChildren<Light>())
			{
				light.intensity = 0;	
			}

			StartCoroutine(FadeOutAudio());
			StartCoroutine(FadeOutEmitters());
		}

		IEnumerator FadeOutAudio()
		{
			if(thrustAudio && audioSource.isPlaying)
			{
				while(audioSource.volume > 0 || audioSource.pitch > 0)
				{
					audioSource.volume = Mathf.Lerp(audioSource.volume, 0, 5*Time.deltaTime);
					audioSource.pitch = Mathf.Lerp(audioSource.pitch, 0, 5*Time.deltaTime);
					yield return null;
				}
			}
		}

		IEnumerator FadeOutEmitters()
		{
			float fadeoutStartTime = Time.time;
			while(Time.time-fadeoutStartTime < 5)
			{
				foreach(KSPParticleEmitter pe in pEmitters)
				{
					if(!pe) continue;
					pe.maxEmission = Mathf.FloorToInt(pe.maxEmission * 0.8f);
					pe.minEmission = Mathf.FloorToInt(pe.minEmission * 0.8f);
				}
				
				foreach(var gpe in gaplessEmitters)
				{
					if(!gpe) continue;
					gpe.pEmitter.maxSize = Mathf.MoveTowards(gpe.pEmitter.maxSize, 0, 0.005f);
					gpe.pEmitter.minSize = Mathf.MoveTowards(gpe.pEmitter.minSize, 0, 0.008f);
					gpe.pEmitter.worldVelocity = ParticleTurbulence.Turbulence;
				}

				yield return new WaitForFixedUpdate();
			}

			foreach(KSPParticleEmitter pe in pEmitters)
			{
				if(!pe) continue;
				pe.emit = false;
			}

			foreach(var gpe in gaplessEmitters)
			{
				if(!gpe) continue;
				gpe.emit = false;
			}
		}

		[KSPField]
		public float beamCorrectionFactor;
		[KSPField]
		public float beamCorrectionDamping;

		ModuleTargetingCamera targetingPod;
		Ray previousBeam;
		void BeamRideGuidance()
		{
			if(!targetingPod)
			{
				guidanceActive = false;
				return;
			}

			if(RadarUtils.TerrainCheck(targetingPod.cameraParentTransform.position, transform.position))
			{
				guidanceActive = false;
				return;
			}
			Ray laserBeam = new Ray(targetingPod.cameraParentTransform.position + (targetingPod.vessel.rb_velocity * Time.fixedDeltaTime), targetingPod.targetPointPosition - targetingPod.cameraParentTransform.position);
			Vector3 target = MissileGuidance.GetBeamRideTarget(laserBeam, part.transform.position, vessel.srf_velocity, beamCorrectionFactor, beamCorrectionDamping, (timeIndex > 0.25f ? previousBeam : laserBeam));
			previousBeam = laserBeam;
			DrawDebugLine(part.transform.position, target);
			DoAero(target);
		}

		void CruiseGuidance()
		{
			Vector3 cruiseTarget = Vector3.zero;
			float distance = Vector3.Distance(targetPosition, transform.position);

			if(terminalManeuvering && distance < 4500)
			{
				cruiseTarget = MissileGuidance.GetTerminalManeuveringTarget(targetPosition, vessel, cruiseAltitude);
				debugString += "\nTerminal Maneuvers";
			}
			else
			{
				float agmThreshDist = 2500;
				if(distance <agmThreshDist)
				{
					if(!MissileGuidance.GetBallisticGuidanceTarget(targetPosition, vessel, true, out cruiseTarget))
					{
						cruiseTarget = MissileGuidance.GetAirToGroundTarget(targetPosition, vessel, agmDescentRatio);
					}
				
					debugString += "\nDescending On Target";
				}
				else
				{
					cruiseTarget = MissileGuidance.GetCruiseTarget(targetPosition, vessel, cruiseAltitude);
					debugString += "\nCruising";
				}
			}
					
			//float clampedSpeed = Mathf.Clamp((float)vessel.srfSpeed, 1, 1000);
			//float limitAoA = Mathf.Clamp(3500 / clampedSpeed, 5, maxAoA);

			//debugString += "\n limitAoA: "+limitAoA.ToString("0.0");

			Vector3 upDirection = VectorUtils.GetUpDirection(transform.position);

			//axial rotation
			if(rotationTransform)
			{
				Quaternion originalRotation = transform.rotation;
				Quaternion originalRTrotation = rotationTransform.rotation;
				transform.rotation = Quaternion.LookRotation(transform.forward, upDirection);
				rotationTransform.rotation = originalRTrotation;
				Vector3 lookUpDirection = Vector3.ProjectOnPlane(cruiseTarget-transform.position, transform.forward) * 100;
				lookUpDirection = transform.InverseTransformPoint(lookUpDirection + transform.position);

				lookUpDirection = new Vector3(lookUpDirection.x, 0, 0);
				lookUpDirection += 10*Vector3.up;
				//Debug.Log ("lookUpDirection: "+lookUpDirection);


				rotationTransform.localRotation = Quaternion.Lerp(rotationTransform.localRotation, Quaternion.LookRotation(Vector3.forward, lookUpDirection), 0.04f);
				Quaternion finalRotation = rotationTransform.rotation;
				transform.rotation = originalRotation;
				rotationTransform.rotation = finalRotation;

				vesselReferenceTransform.rotation = Quaternion.LookRotation(-rotationTransform.up, rotationTransform.forward);
			}

			//aeroTorque = MissileGuidance.DoAeroForces(this, cruiseTarget, liftArea, controlAuthority * steerMult, aeroTorque, finalMaxTorque, limitAoA); 
			DoAero(cruiseTarget);
			CheckMiss();

			debugString += "\nRadarAlt: " + MissileGuidance.GetRadarAltitude(vessel);
		}

		void AAMGuidance()
		{
			Vector3 aamTarget;
			if(targetAcquired)
			{
				DrawDebugLine(transform.position+(part.rb.velocity*Time.fixedDeltaTime), targetPosition);

				aamTarget = MissileGuidance.GetAirToAirTarget(targetPosition, targetVelocity, targetAcceleration, vessel, out timeToImpact, optimumAirspeed);
				if(Vector3.Angle(aamTarget-transform.position, transform.forward) > maxOffBoresight*0.75f)
				{
					aamTarget = targetPosition;
				}

				//proxy detonation
				if(proxyDetonate && ((targetPosition+(targetVelocity*Time.fixedDeltaTime))-(transform.position)).sqrMagnitude < Mathf.Pow(blastRadius*0.5f,2))
				{
					part.temperature = part.maxTemp + 100;
				}
			}
			else
			{
				aamTarget = transform.position + (20*vessel.srf_velocity.normalized);
			}


			if(Time.time-timeFired > dropTime+0.25f)
			{
				DoAero(aamTarget);
			}

			CheckMiss();
		}

		void AGMGuidance()
		{
			if(targetingMode != TargetingModes.GPS)
			{
				if(targetAcquired)
				{
					//lose lock if seeker reaches gimbal limit
					float targetViewAngle = Vector3.Angle(transform.forward, targetPosition - transform.position);
				
					if(targetViewAngle > maxOffBoresight)
					{
						Debug.Log("AGM Missile guidance failed - target out of view");
						guidanceActive = false;
					}
					CheckMiss();
				}
				else
				{
					if(targetingMode == TargetingModes.Laser)
					{
						//keep going straight until found laser point
						targetPosition = laserStartPosition + (20000 * startDirection);
					}
				}
			}

			Vector3 agmTarget = MissileGuidance.GetAirToGroundTarget(targetPosition, vessel, agmDescentRatio);

			DoAero(agmTarget);
		}

		void DoAero(Vector3 targetPosition)
		{
			aeroTorque = MissileGuidance.DoAeroForces(this, targetPosition, liftArea, controlAuthority * steerMult, aeroTorque, finalMaxTorque, maxAoA);
		}

		void AGMBallisticGuidance()
		{
			Vector3 agmTarget;
			bool validSolution = MissileGuidance.GetBallisticGuidanceTarget(targetPosition, vessel, !indirect, out agmTarget);
			if(!validSolution || Vector3.Angle(targetPosition - transform.position, agmTarget - transform.position) > Mathf.Clamp(maxOffBoresight, 0, 65))
			{
				Vector3 dToTarget = targetPosition - transform.position;
				Vector3 direction = Quaternion.AngleAxis(Mathf.Clamp(maxOffBoresight * 0.9f, 0, 45f), Vector3.Cross(dToTarget, VectorUtils.GetUpDirection(transform.position))) * dToTarget;
				agmTarget = transform.position + direction;
			}

			DoAero(agmTarget);
		}

		void UpdateGPSTarget()
		{
			if(targetAcquired)
			{
				targetPosition = VectorUtils.GetWorldSurfacePostion(targetGPSCoords, vessel.mainBody);
				targetVelocity = Vector3.zero;
				targetAcceleration = Vector3.zero;
			}
			else
			{
				guidanceActive = false;
			}
		}

		void UpdateLaserTarget()
		{
			if(targetAcquired)
			{
				if(lockedCamera && lockedCamera.groundStabilized && !lockedCamera.gimbalLimitReached && lockedCamera.surfaceDetected) //active laser target
				{
					targetPosition = lockedCamera.groundTargetPosition;
					targetVelocity = (targetPosition - lastLaserPoint) / Time.fixedDeltaTime;
					targetAcceleration = Vector3.zero;
					lastLaserPoint = targetPosition;

					if(guidanceMode == GuidanceModes.BeamRiding && timeIndex > 0.25f && Vector3.Dot(part.transform.forward, part.transform.position - lockedCamera.transform.position) < 0)
					{
						targetAcquired = false;
						lockedCamera = null;
					}
				}
				else //lost active laser target, home on last known position
				{
					if(CMSmoke.RaycastSmoke(new Ray(transform.position, lastLaserPoint - transform.position)))
					{
						//Debug.Log("Laser missile affected by smoke countermeasure");
						float angle = VectorUtils.FullRangePerlinNoise(0.75f * Time.time, 10) * BDArmorySettings.SMOKE_DEFLECTION_FACTOR;
						targetPosition = VectorUtils.RotatePointAround(lastLaserPoint, transform.position, VectorUtils.GetUpDirection(transform.position), angle);
						targetVelocity = Vector3.zero;
						targetAcceleration = Vector3.zero;
						lastLaserPoint = targetPosition;
					}
					else
					{
						targetPosition = lastLaserPoint;
					}
				}
			}
			else
			{
				ModuleTargetingCamera foundCam = null;
				bool parentOnly = (guidanceMode == GuidanceModes.BeamRiding);
				foundCam = BDATargetManager.GetLaserTarget(this, parentOnly);
				if(foundCam != null && foundCam.cameraEnabled && foundCam.groundStabilized && CanSeePosition(foundCam.groundTargetPosition))
				{
					Debug.Log("Laser guided missile actively found laser point. Enabling guidance.");
					lockedCamera = foundCam;
					targetAcquired = true;
				}
			}
		}

		void UpdateLegacyTarget()
		{
			if(legacyTargetVessel)
			{
				maxOffBoresight = 90;
				
				if(targetingMode == TargetingModes.Radar)
				{
					activeRadarRange = 20000;
					targetAcquired = true;
					radarTarget = new TargetSignatureData(legacyTargetVessel, 500);
					return;
				}
				else if(targetingMode == TargetingModes.Heat)
				{
					targetAcquired = true;
					heatTarget = new TargetSignatureData(legacyTargetVessel, 500);
					return;
				}

				if(targetingMode != TargetingModes.GPS || targetAcquired)
				{
					targetAcquired = true;
					targetPosition = legacyTargetVessel.CoM + (legacyTargetVessel.rb_velocity * Time.fixedDeltaTime);
					targetGPSCoords = VectorUtils.WorldPositionToGeoCoords(targetPosition, vessel.mainBody);
					targetVelocity = legacyTargetVessel.srf_velocity;
					targetAcceleration = legacyTargetVessel.acceleration;
					lastLaserPoint = targetPosition;
					lockFailTimer = 0;
				}
			}
		}

		void UpdateAntiRadiationTarget()
		{
			if(!targetAcquired)
			{
				guidanceActive = false;
				return;
			}

			if(FlightGlobals.ready)
			{
				if(lockFailTimer < 0)
				{
					lockFailTimer = 0;
				}
				lockFailTimer += Time.fixedDeltaTime;
			}

			if(lockFailTimer > 8)
			{
				guidanceActive = false;
				targetAcquired = false;
			}
			else
			{
				targetPosition = VectorUtils.GetWorldSurfacePostion(targetGPSCoords, vessel.mainBody);
			}
		}

		void ReceiveRadarPing(Vessel v, Vector3 source, RadarWarningReceiver.RWRThreatTypes type, float persistTime)
		{
			if(targetingMode == TargetingModes.AntiRad && targetAcquired && v == vessel)
			{
				if((source - VectorUtils.GetWorldSurfacePostion(targetGPSCoords, vessel.mainBody)).sqrMagnitude < Mathf.Pow(50, 2)
					&& Vector3.Angle(source-transform.position, transform.forward) < maxOffBoresight)
				{
					targetAcquired = true;
					targetPosition = source;
					targetGPSCoords = VectorUtils.WorldPositionToGeoCoords(targetPosition, vessel.mainBody);
					targetVelocity = Vector3.zero;
					targetAcceleration = Vector3.zero;
					lockFailTimer = 0;
				}
			}
		}
		
		int snapshotTicker;
		int locksCount = 0;
		TargetSignatureData[] scannedTargets;
		float radarFailTimer = 0;
		float maxRadarFailTime = 1;
		void UpdateRadarTarget()
		{
			targetAcquired = false;

			float angleToTarget = Vector3.Angle(radarTarget.predictedPosition-transform.position,transform.forward);
			if(radarTarget.exists)
			{
				if(!activeRadar && ((radarTarget.predictedPosition - transform.position).sqrMagnitude > Mathf.Pow(activeRadarRange, 2) || angleToTarget > maxOffBoresight * 0.75f))
				{
					if(vrd)
					{
						TargetSignatureData t = TargetSignatureData.noTarget;
						List<TargetSignatureData> possibleTargets = vrd.GetLockedTargets();
						for(int i = 0; i < possibleTargets.Count; i++)
						{
							if(possibleTargets[i].vessel == radarTarget.vessel)
							{
								t = possibleTargets[i];
							}
						}

				
						if(t.exists)
						{
							targetAcquired = true;
							radarTarget = t;
							targetPosition = radarTarget.predictedPosition;
							targetVelocity = radarTarget.velocity;
							targetAcceleration = radarTarget.acceleration;
							radarFailTimer = 0;
							return;
						}
						else
						{
							if(radarFailTimer > maxRadarFailTime)
							{
								Debug.Log("Semi-Active Radar guidance failed. Parent radar lost target.");
								radarTarget = TargetSignatureData.noTarget;
								legacyTargetVessel = null;
								return;
							}
							else
							{
								if(radarFailTimer == 0)
								{
									Debug.Log("Semi-Active Radar guidance failed - waiting for data");
								}
								radarFailTimer += Time.fixedDeltaTime;
								radarTarget.timeAcquired = Time.time;
								radarTarget.position = radarTarget.predictedPosition;
								targetPosition = radarTarget.predictedPosition;
								targetVelocity = radarTarget.velocity;
								targetAcceleration = Vector3.zero;
								targetAcquired = true;
							}
						}
					}
					else
					{
						Debug.Log("Semi-Active Radar guidance failed. Out of range and no data feed.");
						radarTarget = TargetSignatureData.noTarget;
						legacyTargetVessel = null;
						return;
					}
				}
				else
				{
					vrd = null;

					if(angleToTarget > maxOffBoresight)
					{
						Debug.Log("Radar guidance failed.  Target is out of active seeker gimbal limits.");
						radarTarget = TargetSignatureData.noTarget;
						legacyTargetVessel = null;
						return;
					}
					else
					{
						if(scannedTargets == null) scannedTargets = new TargetSignatureData[5];
						TargetSignatureData.ResetTSDArray(ref scannedTargets);
						Ray ray = new Ray(transform.position, radarTarget.predictedPosition - transform.position);
						bool pingRWR = Time.time - lastRWRPing > 0.4f;
						if(pingRWR) lastRWRPing = Time.time;
						bool radarSnapshot = (snapshotTicker > 20);
						if(radarSnapshot)
						{
							snapshotTicker = 0;
						}
						else
						{
							snapshotTicker++;
						}
						RadarUtils.UpdateRadarLock(ray, lockedSensorFOV, activeRadarMinThresh, ref scannedTargets, 0.4f, pingRWR, RadarWarningReceiver.RWRThreatTypes.MissileLock, radarSnapshot);
						float sqrThresh = radarLOALSearching ? Mathf.Pow(500, 2) : Mathf.Pow(40, 2);

						if(radarLOAL && radarLOALSearching && !radarSnapshot)
						{
							//only scan on snapshot interval
						}
						else
						{
							for(int i = 0; i < scannedTargets.Length; i++)
							{
								if(scannedTargets[i].exists && (scannedTargets[i].predictedPosition - radarTarget.predictedPosition).sqrMagnitude < sqrThresh)
								{
									radarTarget = scannedTargets[i];
									targetAcquired = true;
									radarLOALSearching = false;
									targetPosition = radarTarget.predictedPosition + (radarTarget.velocity * Time.fixedDeltaTime);
									targetVelocity = radarTarget.velocity;
									targetAcceleration = radarTarget.acceleration;
									radarFailTimer = 0;
									if(!activeRadar && Time.time - timeFired > 1)
									{
										if(locksCount == 0)
										{
											RadarWarningReceiver.PingRWR(ray, lockedSensorFOV, RadarWarningReceiver.RWRThreatTypes.MissileLaunch, 2f);
											Debug.Log("Pitbull! Radar missile has gone active.  Radar sig strength: " + radarTarget.signalStrength.ToString("0.0"));

										}
										else if(locksCount > 2)
										{
											guidanceActive = false;
											checkMiss = true;
											if(BDArmorySettings.DRAW_DEBUG_LABELS)
											{
												Debug.Log("Radar missile reached max re-lock attempts.");
											}
										}
										locksCount++;
									}
									activeRadar = true;
									return;
								}
							}
						}

						if(radarLOAL)
						{
							radarLOALSearching = true;
							targetAcquired = true;
							targetPosition = radarTarget.predictedPosition + (radarTarget.velocity * Time.fixedDeltaTime);
							targetVelocity = radarTarget.velocity;
							targetAcceleration = Vector3.zero;
							activeRadar = false;
						}
						else
						{
							radarTarget = TargetSignatureData.noTarget;
						}

					}
				}
			}
			else if(radarLOAL && radarLOALSearching)
			{
				if(scannedTargets == null) scannedTargets = new TargetSignatureData[5];
				TargetSignatureData.ResetTSDArray(ref scannedTargets);
				Ray ray = new Ray(transform.position, transform.forward);
				bool pingRWR = Time.time - lastRWRPing > 0.4f;
				if(pingRWR) lastRWRPing = Time.time;
				bool radarSnapshot = (snapshotTicker > 6);
				if(radarSnapshot)
				{
					snapshotTicker = 0;
				}
				else
				{
					snapshotTicker++;
				}
				RadarUtils.UpdateRadarLock(ray, lockedSensorFOV*3, activeRadarMinThresh*2, ref scannedTargets, 0.4f, pingRWR, RadarWarningReceiver.RWRThreatTypes.MissileLock, radarSnapshot);
				float sqrThresh = Mathf.Pow(300, 2);

				float smallestAngle = 360;
				TargetSignatureData lockedTarget = TargetSignatureData.noTarget;

				for(int i = 0; i < scannedTargets.Length; i++)
				{
					if(scannedTargets[i].exists && (scannedTargets[i].predictedPosition - radarTarget.predictedPosition).sqrMagnitude < sqrThresh)
					{
						float angle = Vector3.Angle(scannedTargets[i].predictedPosition - transform.position, transform.forward);
						if(angle < smallestAngle)
						{
							lockedTarget = scannedTargets[i];
							smallestAngle = angle;
						}


						activeRadar = true;
						return;
					}
				}

				if(lockedTarget.exists)
				{
					radarTarget = lockedTarget;
					targetAcquired = true;
					radarLOALSearching = false;
					targetPosition = radarTarget.predictedPosition + (radarTarget.velocity * Time.fixedDeltaTime);
					targetVelocity = radarTarget.velocity;
					targetAcceleration = radarTarget.acceleration;

					if(!activeRadar && Time.time - timeFired > 1)
					{
						RadarWarningReceiver.PingRWR(new Ray(transform.position, radarTarget.predictedPosition - transform.position), lockedSensorFOV, RadarWarningReceiver.RWRThreatTypes.MissileLaunch, 2f);
						Debug.Log("Pitbull! Radar missile has gone active.  Radar sig strength: " + radarTarget.signalStrength.ToString("0.0"));
					}
					return;
				}
				else
				{
					targetAcquired = true;
					targetPosition = transform.position + (startDirection * 500);
					targetVelocity = Vector3.zero;
					targetAcceleration = Vector3.zero;
					radarLOALSearching = true;
					return;
				}
			}

			if(!radarTarget.exists)
			{
				legacyTargetVessel = null;
			}
		}

		void UpdateHeatTarget()
		{
			targetAcquired = false;

			if(lockFailTimer > 1)
			{
				legacyTargetVessel = null;

				return;
			}
			
			if(heatTarget.exists && lockFailTimer < 0)
			{
				lockFailTimer = 0;
			}
			if(lockFailTimer >= 0)
			{
				Ray lookRay = new Ray(transform.position, heatTarget.position+(heatTarget.velocity*Time.fixedDeltaTime)-transform.position);
				heatTarget = BDATargetManager.GetHeatTarget(lookRay, lockedSensorFOV/2, heatThreshold, allAspect);
				
				if(heatTarget.exists)
				{
					targetAcquired = true;
					targetPosition = heatTarget.position+(2*heatTarget.velocity*Time.fixedDeltaTime);
					targetVelocity = heatTarget.velocity;
					targetAcceleration = heatTarget.acceleration;
					lockFailTimer = 0;
				}
				else
				{
					if(FlightGlobals.ready)
					{
						lockFailTimer += Time.fixedDeltaTime;
					}
				}
			}


		}

		void CheckMiss()
		{
			float sqrDist = ((targetPosition+(targetVelocity*Time.fixedDeltaTime))-(transform.position+(part.rb.velocity*Time.fixedDeltaTime))).sqrMagnitude;
			if(sqrDist < 160000 || (MissileState == MissileStates.PostThrust && (guidanceMode == GuidanceModes.AAMLead || guidanceMode == GuidanceModes.AAMPure)))
			{
				checkMiss = true;	
			}
			
			//kill guidance if missile has missed
			if(!hasMissed && checkMiss)
			{
				bool noProgress = MissileState == MissileStates.PostThrust && (Vector3.Dot(vessel.srf_velocity-targetVelocity, targetPosition - vessel.transform.position) < 0);
				if(Vector3.Dot(targetPosition-transform.position,transform.forward) < 0 || noProgress) 
				{
					Debug.Log ("Missile CheckMiss showed miss");
					hasMissed = true;
					guidanceActive = false;
					targetMf = null;
					if(hasRCS) KillRCS();
					if(sqrDist < Mathf.Pow(blastRadius * 0.5f, 2)) part.temperature = part.maxTemp + 100;

					isTimed = true;
					detonationTime = Time.time - timeFired + 1.5f;
					return;
				}
			}
		}

		void RayDetonator()
		{
			Vector3 lineStart = transform.position;
			Vector3 lineEnd = transform.position + part.rb.velocity;
			RaycastHit rayHit;
			if(Physics.Linecast(lineStart, lineEnd, out rayHit, 557057))
			{
				if(rayHit.collider.attachedRigidbody && rayHit.collider.attachedRigidbody != part.rb)
				{
					part.temperature = part.temperature + 100;
				}
			}
			
		}

	

		void DrawDebugLine(Vector3 start, Vector3 end)
		{
			if(BDArmorySettings.DRAW_DEBUG_LINES)
			{
				if(!gameObject.GetComponent<LineRenderer>())
				{
					LR = gameObject.AddComponent<LineRenderer>();
					LR.material = new Material(Shader.Find("KSP/Emissive/Diffuse"));
					LR.material.SetColor("_EmissiveColor", Color.red);
				}else
				{
					LR = gameObject.GetComponent<LineRenderer>();
				}
				LR.SetVertexCount(2);
				LR.SetPosition(0, start);
				LR.SetPosition(1, end);
			}
		}
		
		
		
		public void Detonate()
		{
			if(isSeismicCharge)
			{
				DetonateSeismicCharge();
			
			}
			else if(!hasExploded && hasFired)
			{
				BDArmorySettings.numberOfParticleEmitters--;
				
				hasExploded = true;

				
				if(legacyTargetVessel!=null)
				{
					foreach(var wpm in legacyTargetVessel.FindPartModulesImplementing<MissileFire>())
					{
						wpm.missileIsIncoming = false;
					}
				}
				
				if(part!=null) part.temperature = part.maxTemp + 100;
				Vector3 position = transform.position;//+rigidbody.velocity*Time.fixedDeltaTime;
				if(sourceVessel==null) sourceVessel = vessel;
				ExplosionFX.CreateExplosion(position, blastRadius, blastPower, blastHeat, sourceVessel, transform.forward, explModelPath, explSoundPath); //TODO: apply separate heat damage

				foreach(var e in gaplessEmitters)
				{
					e.gameObject.AddComponent<BDAParticleSelfDestruct>();
					e.transform.parent = null;
					if(e.GetComponent<Light>())
					{
						e.GetComponent<Light>().enabled = false;
					}
				}
			}
		}


		void PartDie(Part p)
		{
			if(p == part)
			{
				Detonate();
				BDATargetManager.FiredMissiles.Remove(this);
				GameEvents.onPartDie.Remove(PartDie);
			}
		}



		public bool CanSeePosition(Vector3 pos)
		{
			if((pos-transform.position).sqrMagnitude < Mathf.Pow(20,2))
			{
				return false;
			}

			float dist = 10000;
			Ray ray = new Ray(missileReferenceTransform.position, pos-missileReferenceTransform.position);
			ray.origin += 10 * ray.direction;
			RaycastHit rayHit;
			if(Physics.Raycast(ray, out rayHit, dist, 557057))
			{
				if((rayHit.point-pos).sqrMagnitude < 200)
				{
					return true;
				}
				else
				{
					return false;
				}
			}

			return true;
		}
		
		
		
		
		void DetonateSeismicCharge()
		{
			if(!hasExploded && hasFired)
			{
				GameSettings.SHIP_VOLUME = 0;
				GameSettings.MUSIC_VOLUME = 0;
				GameSettings.AMBIENCE_VOLUME = 0;
				
				BDArmorySettings.numberOfParticleEmitters--;
				
				hasExploded = true;

				/*
				if(targetVessel == null)
				{
					if(target!=null && FlightGlobals.ActiveVessel.gameObject == target)
					{
						targetVessel = FlightGlobals.ActiveVessel;
					}
					else if(target!=null && !BDArmorySettings.Flares.Contains(t => t.gameObject == target))
					{
						targetVessel = Part.FromGO(target).vessel;
					}
					
				}
				*/
				if(legacyTargetVessel!=null)
				{
					foreach(var wpm in legacyTargetVessel.FindPartModulesImplementing<MissileFire>())
					{
						wpm.missileIsIncoming = false;
					}
				}
				
				if(part!=null)
				{
					
					part.temperature = part.maxTemp + 100;
				}
				Vector3 position = transform.position+part.rb.velocity*Time.fixedDeltaTime;
				if(sourceVessel==null) sourceVessel = vessel;
				
				SeismicChargeFX.CreateSeismicExplosion(transform.position-(part.rb.velocity.normalized*15), UnityEngine.Random.rotation);
				
			}	
		}
		
		
		
		public static bool CheckIfMissile(Part p)
		{
			if(p.GetComponent<MissileLauncher>())
			{
				return true;
			}
			else return false;
		}
			

		
		void WarnTarget()
		{
			if(legacyTargetVessel == null)
			{
				return;
				/*
				if(FlightGlobals.ActiveVessel.gameObject == target)
				{
					targetVessel = FlightGlobals.ActiveVessel;
				}
				else if(target!=null && !BDArmorySettings.Flares.Contains(target))
				{
					targetVessel = Part.FromGO(target).vessel;
				}
				*/
				
			}
			
			if(legacyTargetVessel!=null)
			{
				foreach(var wpm in legacyTargetVessel.FindPartModulesImplementing<MissileFire>())
				{
					wpm.MissileWarning(Vector3.Distance(transform.position, legacyTargetVessel.transform.position), this);
					break;
				}
			}
		}


		float[] rcsFiredTimes;
		KSPParticleEmitter[] rcsTransforms;
		void SetupRCS()
		{
			rcsFiredTimes = new float[]{0,0,0,0};
			rcsTransforms = new KSPParticleEmitter[]{upRCS, leftRCS, rightRCS, downRCS};
		}



		void DoRCS()
		{
			Vector3 relV = targetVelocity-vessel.obt_velocity;

			for(int i = 0; i < 4; i++)
			{
				//Vector3 relV = legacyTargetVessel.obt_velocity-vessel.obt_velocity;

				//Vector3 relV = vessel.obt_velocity-targetVelocity;
				//Vector3 localRelV = rcsTransforms[i].transform.InverseTransformVector(relV);


				//float giveThrust = Mathf.Clamp(-localRelV.z, 0, rcsThrust);
				float giveThrust = Mathf.Clamp(Vector3.Project(relV, rcsTransforms[i].transform.forward).magnitude * -Mathf.Sign(Vector3.Dot(rcsTransforms[i].transform.forward, relV)), 0, rcsThrust);
				part.rb.AddForce(-giveThrust*rcsTransforms[i].transform.forward);

				if(giveThrust > rcsRVelThreshold)
				{
					rcsAudioMinInterval = UnityEngine.Random.Range(0.15f,0.25f);
					if(Time.time-rcsFiredTimes[i] > rcsAudioMinInterval)
					{
						sfAudioSource.PlayOneShot(GameDatabase.Instance.GetAudioClip("BDArmory/Sounds/popThrust"));
						rcsTransforms[i].emit = true;
						rcsFiredTimes[i] = Time.time;
					}
				}
				else
				{
					rcsTransforms[i].emit = false;
				}

				//turn off emit
				if(Time.time-rcsFiredTimes[i] > rcsAudioMinInterval*0.75f)
				{
					rcsTransforms[i].emit = false;
				}
			}


		}
		
		void KillRCS()
		{
			upRCS.emit = false;
			downRCS.emit = false;
			leftRCS.emit = false;
			rightRCS.emit = false;
		}


		void OnGUI()
		{
			if(hasFired && BDArmorySettings.DRAW_DEBUG_LABELS)	
			{
				GUI.Label(new Rect(200,200,200,200), debugString);	


			}
			if(hasFired  && hasRCS)
			{
				BDGUIUtils.DrawLineBetweenWorldPositions(transform.position, targetPosition, 2, Color.red);
			}
		}


		void AntiSpin()
		{
			part.rb.angularDrag = 0;
			part.angularDrag = 0;
			Vector3 spin = Vector3.Project(part.rb.angularVelocity, part.rb.transform.forward);// * 8 * Time.fixedDeltaTime;
			part.rb.angularVelocity -= spin;
			//rigidbody.maxAngularVelocity = 7;

			if(guidanceActive)
			{
				part.rb.angularVelocity -= 0.6f * part.rb.angularVelocity;
			}
			else
			{
				part.rb.angularVelocity -= 0.02f * part.rb.angularVelocity;
			}
		}
		
		void SimpleDrag()
		{
			part.dragModel = Part.DragModel.NONE;
			//float simSpeedSquared = (float)vessel.srf_velocity.sqrMagnitude;
			float simSpeedSquared = (part.rb.GetPointVelocity(part.transform.TransformPoint(simpleCoD))+(Vector3)Krakensbane.GetFrameVelocity()).sqrMagnitude;
			Vector3 currPos = transform.position;
			float drag = deployed ? deployedDrag : simpleDrag;
			float dragMagnitude = (0.008f * part.rb.mass) * drag * 0.5f * simSpeedSquared * (float)FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(currPos), FlightGlobals.getExternalTemperature(), FlightGlobals.currentMainBody);
			Vector3 dragForce = dragMagnitude * vessel.srf_velocity.normalized;
			part.rb.AddForceAtPosition(-dragForce, transform.TransformPoint(simpleCoD));

			Vector3 torqueAxis = -Vector3.Cross(vessel.srf_velocity, part.transform.forward).normalized;
			float AoA = Vector3.Angle(part.transform.forward, vessel.srf_velocity);
			AoA /= 20;
			part.rb.AddTorque(AoA * simpleStableTorque * dragMagnitude * torqueAxis);
		}

		void ParseModes()
		{
			homingType = homingType.ToLower();
			switch(homingType)
			{
			case "aam":
				guidanceMode = GuidanceModes.AAMLead;
				break;
			case "aamlead":
				guidanceMode = GuidanceModes.AAMLead;
				break;
			case "aampure":
				guidanceMode = GuidanceModes.AAMPure;
				break;
			case "agm":
				guidanceMode = GuidanceModes.AGM;
				break;
			case "agmballistic":
				guidanceMode = GuidanceModes.AGMBallistic;
				break;
			case "cruise":
				guidanceMode = GuidanceModes.Cruise;
				break;
			case "sts":
				guidanceMode = GuidanceModes.STS;
				break;
			case "rcs":
				guidanceMode = GuidanceModes.RCS;
				break;
			case "beamriding":
				guidanceMode = GuidanceModes.BeamRiding;
				break;
			default:
				guidanceMode = GuidanceModes.None;
				break;
			}

			targetingType = targetingType.ToLower();
			switch(targetingType)
			{
			case "radar":
				targetingMode = TargetingModes.Radar;
				break;
			case "heat":
				targetingMode = TargetingModes.Heat;
				break;
			case "laser":
				targetingMode = TargetingModes.Laser;
				break;
			case "gps":
				targetingMode = TargetingModes.GPS;
				maxOffBoresight = 360;
				break;
			case "antirad":
				targetingMode = TargetingModes.AntiRad;
				break;
			default:
				targetingMode = TargetingModes.None;
				break;
			}

			tModeString = Enum.GetName(typeof(TargetingModes), targetingMode);
		}



		// RMB info in editor
		public override string GetInfo()
		{
			var output = new StringBuilder();
			output.Append(Environment.NewLine);
			output.Append(String.Format("Weapon Type: {0}", "Missile"));
			output.Append(Environment.NewLine);
			output.Append(String.Format("Guidance Mode: {0}", homingType.ToString().ToUpper()));
			output.Append(Environment.NewLine);
			output.Append(String.Format("Targetting Mode: {0}", targetingType.ToString().ToUpper()));
			output.Append(Environment.NewLine);
			if (activeRadar) {
				output.Append(String.Format("Active Radar Range: {0}", activeRadarRange));
				output.Append(Environment.NewLine);
			}

			output.Append(String.Format("Min/Max Range: {0}/{1} meters", minStaticLaunchRange, maxStaticLaunchRange));
			output.Append(Environment.NewLine);
			return output.ToString();

		}

		
	}
}

