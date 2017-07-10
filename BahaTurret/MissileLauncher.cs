using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BahaTurret
{	
	public class MissileLauncher : MissileBase
    {

        #region  Variable Declarations

        [KSPField]
		public string homingType = "AAM";

        [KSPField]
		public string targetingType = "none";
 
        public MissileTurret missileTurret = null;
		public BDRotaryRail rotaryRail = null;

		[KSPField]
		public string exhaustPrefabPath;

		[KSPField]
		public string boostExhaustPrefabPath;

		[KSPField]
		public string boostExhaustTransformName;

        #region Aero

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

        #endregion

        [KSPField]
		public float maxTorque = 90;
	
		[KSPField]
		public float thrust = 30;
		[KSPField]
		public float cruiseThrust = 3;
		
		[KSPField]
		public float boostTime = 2.2f;
		[KSPField]
		public float cruiseTime = 45;
		[KSPField]
		public float cruiseDelay = 0;
	
		[KSPField]
		public float maxAoA = 35;
		
		[KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Direction: "), 
			UI_Toggle(disabledText = "Lateral", enabledText = "Forward")]
		public bool decoupleForward = false;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Decouple Speed"),
                  UI_FloatRange(minValue = 0f, maxValue = 10f, stepIncrement = 0.1f, scene = UI_Scene.Editor)]
        public float decoupleSpeed = 0;

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
		
		[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Cruise Altitude"),
		 UI_FloatRange(minValue = 30, maxValue = 2500f, stepIncrement = 5f, scene = UI_Scene.All)]
		public float cruiseAltitude = 500;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Max Altitude"),
         UI_FloatRange(minValue = 0f, maxValue = 5000f, stepIncrement = 10f, scene = UI_Scene.All)]
        public float maxAltitude = 0f;

        [KSPField]
		public string rotationTransformName = string.Empty;
		Transform rotationTransform;

		[KSPField]
		public bool terminalManeuvering = false;

        [KSPField]
        public string terminalGuidanceType = "";
        [KSPField]
        public float terminalGuidanceDistance = 0.0f;

        private bool terminalGuidanceActive = false;

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

		private AudioSource audioSource;
		public AudioSource sfAudioSource;
		List<KSPParticleEmitter> pEmitters;
		List<BDAGaplessParticleEmitter> gaplessEmitters;
		
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

		List<GameObject> boosters;
		[KSPField]
		public bool decoupleBoosters = false;
		[KSPField]
		public float boosterDecoupleSpeed = 5;
		[KSPField]
		public float boosterMass = 0;

		Transform vesselReferenceTransform;

		[KSPField]
		public string boostTransformName = string.Empty;
		List<KSPParticleEmitter> boostEmitters;
		List<BDAGaplessParticleEmitter> boostGaplessEmitters;
		
		[KSPField]
		public bool torpedo = false;
		[KSPField]
		public float waterImpactTolerance = 25;

        //ballistic options
        [KSPField]
        public bool indirect = false;

        public GPSTargetInfo designatedGPSInfo;

        #endregion

        [KSPAction("Fire Missile")]
        public void AGFire(KSPActionParam param)
        {
            if (BDArmorySettings.Instance.ActiveWeaponManager != null && BDArmorySettings.Instance.ActiveWeaponManager.vessel == vessel) BDArmorySettings.Instance.ActiveWeaponManager.SendTargetDataToMissile(this);
            if (missileTurret)
            {
                missileTurret.FireMissile(this);
            }
            else if (rotaryRail)
            {
                rotaryRail.FireMissile(this);
            }
            else
            {
                FireMissile();
            }
            if (BDArmorySettings.Instance.ActiveWeaponManager != null) BDArmorySettings.Instance.ActiveWeaponManager.UpdateList();
        }

        [KSPEvent(guiActive = true, guiName = "Fire Missile", active = true)]
        public void GuiFire()
        {
            if (BDArmorySettings.Instance.ActiveWeaponManager != null && BDArmorySettings.Instance.ActiveWeaponManager.vessel == vessel) BDArmorySettings.Instance.ActiveWeaponManager.SendTargetDataToMissile(this);
            if (missileTurret)
            {
                missileTurret.FireMissile(this);
            }
            else if (rotaryRail)
            {
                rotaryRail.FireMissile(this);
            }
            else
            {
                FireMissile();
            }
            if (BDArmorySettings.Instance.ActiveWeaponManager != null) BDArmorySettings.Instance.ActiveWeaponManager.UpdateList();
        }

        [KSPEvent(guiActive = true, guiActiveEditor = false, active = true, guiName = "Jettison")]
        public override void Jettison()
        {
            if (missileTurret) return;

            part.decouple(0);
            if (BDArmorySettings.Instance.ActiveWeaponManager != null) BDArmorySettings.Instance.ActiveWeaponManager.UpdateList();
        }

        void ParseWeaponClass()
		{
			missileType = missileType.ToLower();
            if (missileType == "bomb")
            {
                weaponClass = WeaponClasses.Bomb;
            }
            else if (missileType == "torpedo" || missileType == "depthcharge")
            {
                weaponClass = WeaponClasses.SLW;
            }
            else
            {
                weaponClass = WeaponClasses.Missile;
            }
		}
        
		public override void OnStart(StartState state)
		{

            //base.OnStart(state);
            ParseWeaponClass();

			if(shortName == string.Empty)
			{
				shortName = part.partInfo.title;                
            }

			gaplessEmitters = new List<BDAGaplessParticleEmitter>();
			pEmitters = new List<KSPParticleEmitter>();
            boostEmitters = new List<KSPParticleEmitter>();
            boostGaplessEmitters = new List<BDAGaplessParticleEmitter>();

            Fields["maxOffBoresight"].guiActive = false;
            Fields["maxOffBoresight"].guiActiveEditor = false;
		    Fields["maxStaticLaunchRange"].guiActive = false;
            Fields["maxStaticLaunchRange"].guiActiveEditor = false;
            Fields["minStaticLaunchRange"].guiActive = false;
            Fields["minStaticLaunchRange"].guiActiveEditor = false;
     
            if (isTimed)
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
            // extension for feature_engagementenvelope
            InitializeEngagementRange(minStaticLaunchRange, maxStaticLaunchRange);

            foreach (var emitter in part.FindModelComponents<KSPParticleEmitter>())
			{
			    EffectBehaviour.AddParticleEmitter(emitter);
                emitter.emit = false;
            }

			if(HighLogic.LoadedSceneIsFlight)
			{
				MissileReferenceTransform = part.FindModelTransform("missileTransform");
				if(!MissileReferenceTransform)
				{
					MissileReferenceTransform = part.partTransform;
				}

				if(!string.IsNullOrEmpty(exhaustPrefabPath))
				{
					foreach(var t in part.FindModelTransforms("exhaustTransform"))
					{
						GameObject exhaustPrefab = Instantiate(GameDatabase.Instance.GetModel(exhaustPrefabPath));
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
                        try
                        {
                            GameObject exhaustPrefab;
                            exhaustPrefab = Instantiate(GameDatabase.Instance.GetModel(boostExhaustPrefabPath));
                            exhaustPrefab.SetActive(true);

                            foreach (var emitter in exhaustPrefab.GetComponentsInChildren<KSPParticleEmitter>())
                            {
                                emitter.emit = false;
                            }
                            exhaustPrefab.transform.parent = t;
                            exhaustPrefab.transform.localPosition = Vector3.zero;
                            exhaustPrefab.transform.localRotation = Quaternion.identity;
                        }
                        catch (Exception e)
                        {
                            Debug.Log("[BDArmory]: exhaust prefab missing");
                        }                        					
	
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
							    EffectBehaviour.AddParticleEmitter(be);
							}
                           ;
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
					    EffectBehaviour.AddParticleEmitter(emitter);
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

			if(GuidanceMode != GuidanceModes.Cruise)
			{
				Fields["cruiseAltitude"].guiActive = false;
				Fields["cruiseAltitude"].guiActiveEditor = false;
			}

            if (GuidanceMode != GuidanceModes.AGM)
            {
                Fields["maxAltitude"].guiActive = false;
                Fields["maxAltitude"].guiActiveEditor = false;
            }

            if (part.partInfo.title.Contains("Bomb"))
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

		    if (this.DetonationDistance == -1)
		    {
		          SetInitialDetonationDistance();
		    }
		  

		}

        private void SetInitialDetonationDistance()
        {

            if (GuidanceMode == GuidanceModes.AAMLead || GuidanceMode == GuidanceModes.AAMPure)
            {
                DetonationDistance = blastRadius;
            }
            else
            {
                DetonationDistance = blastPower;
            }
        }

        void OnCollisionEnter(Collision col)
		{
            Debug.Log("[BDArmory]: Something Collided");

          if (TimeIndex>1 && this.part.vessel.speed > 10)
            {
                Detonate();                
            }
		}        
        
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

	    public override float GetBlastRadius()
	    {
	        return this.blastRadius;
	    }

	    public override void FireMissile()
		{
			if(!HasFired)
			{                
                GameEvents.onPartDie.Add(PartDie);
                BDATargetManager.FiredMissiles.Add(this);

				if(GetComponentInChildren<KSPParticleEmitter>())
				{
					BDArmorySettings.numberOfParticleEmitters++;
				}
				
				foreach(var wpm in vessel.FindPartModulesImplementing<MissileFire>())
				{
					Team = wpm.team;	
					break;
				}
				
				sfAudioSource.PlayOneShot(GameDatabase.Instance.GetAudioClip("BDArmory/Sounds/deployClick"));
				SourceVessel = vessel;
                
				//TARGETING
				TargetPosition = transform.position + (transform.forward * 5000); //set initial target position so if no target update, missileBase will count a miss if it nears this point or is flying post-thrust
				startDirection = transform.forward;

				if(BDArmorySettings.ALLOW_LEGACY_TARGETING)
				{
					if(vessel.targetObject!=null && vessel.targetObject.GetVessel()!=null)
					{
						legacyTargetVessel = vessel.targetObject.GetVessel();

						foreach(var mf in legacyTargetVessel.FindPartModulesImplementing<MissileFire>())
						{
							TargetMf = mf;
							break;
						}

						if(TargetingMode == TargetingModes.Heat)
						{
							heatTarget = new TargetSignatureData(legacyTargetVessel, 9999);
						}
					}
				}
                SetLaserTargeting();
                SetAntiRadTargeting();

				part.decouple(0);
				part.force_activate();
				part.Unpack();
				vessel.situation = Vessel.Situations.FLYING;
				part.rb.isKinematic = false;
				part.bodyLiftMultiplier = 0;
				part.dragModel = Part.DragModel.NONE;

                //add target info to vessel
			    AddTargetInfoToVessel();
                StartCoroutine(DecoupleRoutine());

				vessel.vesselName = GetShortName();
				vessel.vesselType = VesselType.Probe;
				
				TimeFired = Time.time;

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
                HasFired = true;
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
		/// Fires the missileBase on target vessel.  Used by AI currently.
		/// </summary>
		/// <param name="v">V.</param>
		public void FireMissileOnTarget(Vessel v)
		{
			if(!HasFired)
			{
				legacyTargetVessel = v;
				FireMissile();
			}
		}
		
		void OnDisable()
		{
			if(TargetingMode == TargetingModes.AntiRad)
			{
				RadarWarningReceiver.OnRadarPing -= ReceiveRadarPing;
			}
		}
		
		public override void OnFixedUpdate()
		{
            debugString = "";
			if(HasFired && !HasExploded && part!=null)
			{
                CheckDetonationDistance();

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
				   && FlightGlobals.ActiveVessel != SourceVessel 
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

				if(TimeIndex > 0.5f)
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
                if (isTimed && TimeIndex > detonationTime)
				{
					//part.temperature = part.maxTemp+100; //This is already done in DetonateIfPossible()
                    Detonate();
				}
			}
		}

	    Vector3 previousPos;
		void RaycastCollisions()
		{
			if(weaponClass == WeaponClasses.Bomb) return;

			if(TimeIndex > 1f && vessel.srfSpeed > part.crashTolerance)
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
						Debug.Log("[BDArmory]:" + part.partInfo.title + " linecast hit on " + (lineHit.collider.attachedRigidbody ? lineHit.collider.attachedRigidbody.gameObject.name : lineHit.collider.gameObject.name));
						part.temperature = part.maxTemp + 100;
					}
				}
			}

			previousPos = part.transform.position;
		}

        private void CheckMiss()
        {
            float sqrDist = ((TargetPosition + (TargetVelocity * Time.fixedDeltaTime)) - (transform.position + (part.rb.velocity * Time.fixedDeltaTime))).sqrMagnitude;
            if (sqrDist < 160000 || (MissileState == MissileStates.PostThrust && (GuidanceMode == GuidanceModes.AAMLead || GuidanceMode == GuidanceModes.AAMPure)))
            {
                checkMiss = true;
            }
            if (maxAltitude != 0f)
            {
                if (vessel.altitude >= maxAltitude) checkMiss = true;                
            }

            //kill guidance if missileBase has missed
            if (!HasMissed && checkMiss)
            {
                bool noProgress = MissileState == MissileStates.PostThrust && (Vector3.Dot(vessel.srf_velocity - TargetVelocity, TargetPosition - vessel.transform.position) < 0);
                if (Vector3.Dot(TargetPosition - transform.position, transform.forward) < 0 || noProgress)                
                {
                    Debug.Log("[BDArmory]: Missile has missed!");
                    if (vessel.altitude >= maxAltitude && maxAltitude != 0f) Debug.Log("[BDArmory]: CheckMiss trigged by MaxAltitude");

                    HasMissed = true;
                    guidanceActive = false;

                    TargetMf = null;

                    var launcher = this as MissileLauncher;
                    if (launcher != null)
                    {
                        if (launcher.hasRCS) launcher.KillRCS();
                    }

                    if (sqrDist < Mathf.Pow(GetBlastRadius() * 0.5f, 2)) part.temperature = part.maxTemp + 100;

                    isTimed = true;
                    detonationTime = TimeIndex + 1.5f;
                    return;
                }
            }
        }

        void UpdateGuidance()
		{
			if(guidanceActive)
			{
				if(BDArmorySettings.ALLOW_LEGACY_TARGETING && legacyTargetVessel)
				{
					UpdateLegacyTarget();
				}

				if(TargetingMode == TargetingModes.Heat)
				{
					UpdateHeatTarget();
				}
				else if(TargetingMode == TargetingModes.Radar)
				{
					UpdateRadarTarget();
				}
				else if(TargetingMode == TargetingModes.Laser)
				{
					UpdateLaserTarget();
				}
				else if(TargetingMode == TargetingModes.Gps)
				{
					UpdateGPSTarget();
				}
				else if(TargetingMode == TargetingModes.AntiRad)
				{
					UpdateAntiRadiationTarget();
				}

                UpdateTerminalGuidance();
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
						targetPosition = targetCoMPos+legacyTargetVessel.srf_velocity*Time.fixedDeltaTime;
					}

					//increaseTurnRate after launch
					float turnRateDPS = Mathf.Clamp(((TimeIndex-dropTime)/boostTime)*maxTurnRateDPS * 25f, 0, maxTurnRateDPS);
					if(!hasRCS)
					{
						turnRateDPS *= controlAuthority;
					}

					//decrease turn rate after thrust cuts out
					if(TimeIndex > dropTime+boostTime+cruiseTime)
					{
						turnRateDPS = atmosMultiplier * Mathf.Clamp(maxTurnRateDPS - ((TimeIndex-dropTime-boostTime-cruiseTime)*0.45f), 1, maxTurnRateDPS);	
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

					finalMaxTorque = Mathf.Clamp((TimeIndex-dropTime)*torqueRampUp, 0, maxTorque); //ramp up torque

                    if (GuidanceMode == GuidanceModes.AAMLead)
                    {
                        AAMGuidance();
                    }
                    else if (GuidanceMode == GuidanceModes.AGM)
                    {
                        AGMGuidance();
                    }
                    else if (GuidanceMode == GuidanceModes.AGMBallistic)
                    {
                        AGMBallisticGuidance();
                    }
                    else if (GuidanceMode == GuidanceModes.BeamRiding)
                    {
                        BeamRideGuidance();
                    }
                    else if (GuidanceMode == GuidanceModes.RCS)
                    {
                        part.transform.rotation = Quaternion.RotateTowards(part.transform.rotation, Quaternion.LookRotation(targetPosition - part.transform.position, part.transform.up), turnRateDPS * Time.fixedDeltaTime);
                    }
                    else if (GuidanceMode == GuidanceModes.Cruise)
                    {
                        CruiseGuidance();
                    }
                    else if (GuidanceMode == GuidanceModes.SLW)
                    {
                        SLWGuidance();
                    }
                }
				else
				{
					CheckMiss();
					TargetMf = null;
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

        // feature_engagementenvelope: terminal guidance mode for cruise missiles
        private void UpdateTerminalGuidance()
        {
            // check if guidance mode should be changed for terminal phase
            float distance = Vector3.Distance(TargetPosition, transform.position);

            if ((TargetingModeTerminal != TargetingModes.None) && (distance < terminalGuidanceDistance) && !terminalGuidanceActive)
            {
                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                    Debug.Log("[BDArmory][Terminal Guidance]: missile updating targeting mode: " + terminalGuidanceType);

                TargetingMode = TargetingModeTerminal;
                terminalGuidanceActive = true;
                TargetAcquired = false;

                switch (TargetingModeTerminal)
                {
                    case TargetingModes.Heat:
                        // get ground heat targets
                        heatTarget = BDATargetManager.GetHeatTarget(new Ray(transform.position + (50 * GetForwardTransform()), TargetPosition - GetForwardTransform()), terminalGuidanceDistance, heatThreshold, true, null, true);
                        if (heatTarget.exists)
                        {
                            if (BDArmorySettings.DRAW_DEBUG_LABELS)
                                Debug.Log("[BDArmory][Terminal Guidance]: Heat target acquired! Position: " + heatTarget.position + ", heatscore: " + heatTarget.signalStrength);
                            TargetAcquired = true;
                            TargetPosition = heatTarget.position + (heatTarget.velocity * Time.fixedDeltaTime);
                            TargetVelocity = heatTarget.velocity;
                            TargetAcceleration = heatTarget.acceleration;
                            lockFailTimer = 0;
                            targetGPSCoords = VectorUtils.WorldPositionToGeoCoords(TargetPosition, vessel.mainBody);
                        }
                        else
                        {
                            if (BDArmorySettings.DRAW_DEBUG_LABELS)
                                Debug.Log("[BDArmory][Terminal Guidance]: Missile heatseeker could not acquire a target lock.");
                        }
                        break;

                    case TargetingModes.Radar:
                        // pretend we have an active radar seeker for ground targets:
                        //radarTarget = vesselRadarData.lockedTargetData.targetData;
                        //vrd = vesselRadarData;

                        var scannedTargets = new TargetSignatureData[5];
                        TargetSignatureData.ResetTSDArray(ref scannedTargets);
                        Ray ray = new Ray(transform.position, TargetPosition - GetForwardTransform());

                        RadarUtils.UpdateRadarLock(ray, maxOffBoresight, activeRadarMinThresh, ref scannedTargets, 0.4f, true, RadarWarningReceiver.RWRThreatTypes.MissileLock, true);
                        float sqrThresh = Mathf.Pow(terminalGuidanceDistance * 1.5f, 2);

                        //float smallestAngle = maxOffBoresight;
                        TargetSignatureData lockedTarget = TargetSignatureData.noTarget;

                        for (int i = 0; i < scannedTargets.Length; i++)
                        {
                            if (scannedTargets[i].exists && (scannedTargets[i].predictedPosition - TargetPosition).sqrMagnitude < sqrThresh)
                            {
                                lockedTarget = scannedTargets[i];
                                ActiveRadar = true;
                            }
                        }

                        if (lockedTarget.exists)
                        {
                            radarTarget = lockedTarget;
                            TargetAcquired = true;
                            TargetPosition = radarTarget.predictedPosition;
                            TargetVelocity = radarTarget.velocity;
                            TargetAcceleration = radarTarget.acceleration;
                            targetGPSCoords = VectorUtils.WorldPositionToGeoCoords(TargetPosition, vessel.mainBody);

                            RadarWarningReceiver.PingRWR(new Ray(transform.position, radarTarget.predictedPosition - transform.position), 45, RadarWarningReceiver.RWRThreatTypes.MissileLaunch, 2f);
                            if (BDArmorySettings.DRAW_DEBUG_LABELS)
                                Debug.Log("[BDArmory][Terminal Guidance]: Pitbull! Radar missileBase has gone active.  Radar sig strength: " + radarTarget.signalStrength.ToString("0.0"));
                        }
                        else
                        {
                            TargetAcquired = true;
                            //TargetPosition = transform.position + (startDirection * 500);
                            TargetPosition = VectorUtils.GetWorldSurfacePostion(targetGPSCoords, vessel.mainBody); //putting back the GPS target if no radar target found
                            TargetVelocity = Vector3.zero;
                            TargetAcceleration = Vector3.zero;
                            targetGPSCoords = VectorUtils.WorldPositionToGeoCoords(TargetPosition, vessel.mainBody);                            
                            if (BDArmorySettings.DRAW_DEBUG_LABELS)
                                Debug.Log("[BDArmory][Terminal Guidance]: Missile radar could not acquire a target lock");                            
                            
                        }
                        break;

                    case TargetingModes.Laser:
                        // not very useful, currently unsupported!
                        break;

                    case TargetingModes.Gps:
                        // from gps to gps -> no actions need to be done!
                        break;

                    case TargetingModes.AntiRad:
                        TargetAcquired = true;
                        SetAntiRadTargeting(); //should then already work automatically via OnReceiveRadarPing
                        if (BDArmorySettings.DRAW_DEBUG_LABELS)
                            Debug.Log("[BDArmory][Terminal Guidance]: Antiradiation mode set! Waiting for radar signals...");
                        break;

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
                EffectBehaviour.AddParticleEmitter(emitter);
                emitter.emit = true;
			}

			foreach(var emitter in gaplessEmitters)
			{
                EffectBehaviour.AddParticleEmitter(emitter.pEmitter);
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
			Ray laserBeam = new Ray(targetingPod.cameraParentTransform.position + (targetingPod.vessel.srf_velocity * Time.fixedDeltaTime), targetingPod.targetPointPosition - targetingPod.cameraParentTransform.position);
			Vector3 target = MissileGuidance.GetBeamRideTarget(laserBeam, part.transform.position, vessel.srf_velocity, beamCorrectionFactor, beamCorrectionDamping, (TimeIndex > 0.25f ? previousBeam : laserBeam));
			previousBeam = laserBeam;
			DrawDebugLine(part.transform.position, target);
			DoAero(target);
		}

		void CruiseGuidance()
		{
			Vector3 cruiseTarget = Vector3.zero;
			float distance = Vector3.Distance(TargetPosition, transform.position);

			if(terminalManeuvering && distance < 4500)
			{
				cruiseTarget = MissileGuidance.GetTerminalManeuveringTarget(TargetPosition, vessel, cruiseAltitude);
				debugString += "\nTerminal Maneuvers";
			}
			else
			{
				float agmThreshDist = 2500;
				if(distance <agmThreshDist)
				{
					if(!MissileGuidance.GetBallisticGuidanceTarget(TargetPosition, vessel, true, out cruiseTarget))
					{
						cruiseTarget = MissileGuidance.GetAirToGroundTarget(TargetPosition, vessel, agmDescentRatio);
					}
				
					debugString += "\nDescending On Target";
				}
				else
				{
					cruiseTarget = MissileGuidance.GetCruiseTarget(TargetPosition, vessel, cruiseAltitude);
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
			if(TargetAcquired)
			{
				DrawDebugLine(transform.position+(part.rb.velocity*Time.fixedDeltaTime), TargetPosition);
                float timeToImpact;
                aamTarget = MissileGuidance.GetAirToAirTarget(TargetPosition, TargetVelocity, TargetAcceleration, vessel, out timeToImpact, optimumAirspeed);
                TimeToImpact = timeToImpact;
                if (Vector3.Angle(aamTarget-transform.position, transform.forward) > maxOffBoresight*0.75f)
				{
					aamTarget = TargetPosition;
				}

				//proxy detonation
				if(proxyDetonate && ((TargetPosition+(TargetVelocity*Time.fixedDeltaTime))-(transform.position)).sqrMagnitude < Mathf.Pow(blastRadius*0.5f,2))
				{
					part.temperature = part.maxTemp + 100;
				}
			}
			else
			{
				aamTarget = transform.position + (20*vessel.srf_velocity.normalized);
			}

            if (TimeIndex > dropTime+0.25f)
			{
				DoAero(aamTarget);
			}

			CheckMiss();
		}

		void AGMGuidance()
		{
			if(TargetingMode != TargetingModes.Gps)
			{
				if(TargetAcquired)
				{
					//lose lock if seeker reaches gimbal limit
					float targetViewAngle = Vector3.Angle(transform.forward, TargetPosition - transform.position);
				
					if(targetViewAngle > maxOffBoresight)
					{
						Debug.Log("[BDArmory]: AGM Missile guidance failed - target out of view");
						guidanceActive = false;
					}
					CheckMiss();
				}
				else
				{
					if(TargetingMode == TargetingModes.Laser)
					{
						//keep going straight until found laser point
						TargetPosition = laserStartPosition + (20000 * startDirection);
					}
				}
			}

			Vector3 agmTarget = MissileGuidance.GetAirToGroundTarget(TargetPosition, vessel, agmDescentRatio);
            DoAero(agmTarget);
		}

        void SLWGuidance()
        {
            Vector3 SLWTarget;
            if (TargetAcquired)
            {
                DrawDebugLine(transform.position + (part.rb.velocity * Time.fixedDeltaTime), TargetPosition);
                float timeToImpact;
                SLWTarget = MissileGuidance.GetAirToAirTarget(TargetPosition, TargetVelocity, TargetAcceleration, vessel, out timeToImpact, optimumAirspeed);
                TimeToImpact = timeToImpact;
                if (Vector3.Angle(SLWTarget - transform.position, transform.forward) > maxOffBoresight * 0.75f)
                {
                    SLWTarget = TargetPosition;
                }

                //proxy detonation
                if (proxyDetonate && ((TargetPosition + (TargetVelocity * Time.fixedDeltaTime)) - (transform.position)).sqrMagnitude < Mathf.Pow(blastRadius * 0.5f, 2))
                {
                    part.temperature = part.maxTemp + 100;
                }
            }
            else
            {
                SLWTarget = transform.position + (20 * vessel.srf_velocity.normalized);
            }

            if (TimeIndex > dropTime + 0.25f)
            {
                DoAero(SLWTarget);
            }

            if (SLWTarget.y > 0f) SLWTarget.y = getSWLWOffset;

            CheckMiss();
        }

		void DoAero(Vector3 targetPosition)
		{
			aeroTorque = MissileGuidance.DoAeroForces(this, targetPosition, liftArea, controlAuthority * steerMult, aeroTorque, finalMaxTorque, maxAoA);
		}

		void AGMBallisticGuidance()
		{
			Vector3 agmTarget;
			bool validSolution = MissileGuidance.GetBallisticGuidanceTarget(TargetPosition, vessel, !indirect, out agmTarget);
			if(!validSolution || Vector3.Angle(TargetPosition - transform.position, agmTarget - transform.position) > Mathf.Clamp(maxOffBoresight, 0, 65))
			{
				Vector3 dToTarget = TargetPosition - transform.position;
				Vector3 direction = Quaternion.AngleAxis(Mathf.Clamp(maxOffBoresight * 0.9f, 0, 45f), Vector3.Cross(dToTarget, VectorUtils.GetUpDirection(transform.position))) * dToTarget;
				agmTarget = transform.position + direction;
            }

			DoAero(agmTarget);
		}

		void UpdateLegacyTarget()
		{
			if(legacyTargetVessel)
			{
				maxOffBoresight = 90;
				
				if(TargetingMode == TargetingModes.Radar)
				{
                    //activeRadarRange = 40000;
                    activeRadarRange = BDArmorySettings.MAX_ACTIVE_RADAR_RANGE;

                    TargetAcquired = true;
					radarTarget = new TargetSignatureData(legacyTargetVessel, 500);
					return;
				}
				else if(TargetingMode == TargetingModes.Heat)
				{
					TargetAcquired = true;
					heatTarget = new TargetSignatureData(legacyTargetVessel, 500);
					return;
				}

				if(TargetingMode != TargetingModes.Gps || TargetAcquired)
				{
					TargetAcquired = true;
					TargetPosition = legacyTargetVessel.CoM + (legacyTargetVessel.srf_velocity * Time.fixedDeltaTime);
					targetGPSCoords = VectorUtils.WorldPositionToGeoCoords(TargetPosition, vessel.mainBody);
					TargetVelocity = legacyTargetVessel.srf_velocity;
					TargetAcceleration = legacyTargetVessel.acceleration;
					lastLaserPoint = TargetPosition;
					lockFailTimer = 0;
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

		public override void Detonate()
		{
            /*
            if (isSeismicCharge)
			{
				DetonateSeismicCharge();
			}
            */

            if (!HasExploded && HasFired)
			{
				BDArmorySettings.numberOfParticleEmitters--;	
				HasExploded = true;
				
				if(legacyTargetVessel!=null)
				{
					foreach(var wpm in legacyTargetVessel.FindPartModulesImplementing<MissileFire>())
					{
						wpm.missileIsIncoming = false;
					}
				}
				
				if(part!=null) part.temperature = part.maxTemp + 100;
				Vector3 position = transform.position;//+rigidbody.velocity*Time.fixedDeltaTime;
				if(SourceVessel==null) SourceVessel = vessel;
				ExplosionFX.CreateExplosion(position, blastRadius, blastPower, blastHeat, SourceVessel, transform.forward, explModelPath, explSoundPath); //TODO: apply separate heat damage

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

	    public override Vector3 GetForwardTransform()
	    {
	        return this.MissileReferenceTransform.forward;
	    }

	    protected override void PartDie(Part p)
        {
			if(p == part)
			{
				Detonate();
				BDATargetManager.FiredMissiles.Remove(this);
				GameEvents.onPartDie.Remove(PartDie);
			}
		}


        /*public bool CanSeePosition(Vector3 pos)
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
    */


        //      void DetonateSeismicCharge()
        //{
        //	if(!HasExploded && HasFired)
        //	{
        //		GameSettings.SHIP_VOLUME = 0;
        //		GameSettings.MUSIC_VOLUME = 0;
        //		GameSettings.AMBIENCE_VOLUME = 0;

        //		BDArmorySettings.numberOfParticleEmitters--;

        //		HasExploded = true;

        //		/*
        //		if(targetVessel == null)
        //		{
        //			if(target!=null && FlightGlobals.ActiveVessel.gameObject == target)
        //			{
        //				targetVessel = FlightGlobals.ActiveVessel;
        //			}
        //			else if(target!=null && !BDArmorySettings.Flares.Contains(t => t.gameObject == target))
        //			{
        //				targetVessel = Part.FromGO(target).vessel;
        //			}

        //		}
        //		*/
        //		if(legacyTargetVessel!=null)
        //		{
        //			foreach(var wpm in legacyTargetVessel.FindPartModulesImplementing<MissileFire>())
        //			{
        //				wpm.missileIsIncoming = false;
        //			}
        //		}

        //		if(part!=null)
        //		{

        //			part.temperature = part.maxTemp + 100;
        //		}
        //		Vector3 position = transform.position+part.rb.velocity*Time.fixedDeltaTime;
        //		if(SourceVessel==null) SourceVessel = vessel;

        //		SeismicChargeFX.CreateSeismicExplosion(transform.position-(part.rb.velocity.normalized*15), UnityEngine.Random.rotation);

        //	}	
        //} 


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
			Vector3 relV = TargetVelocity-vessel.obt_velocity;

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

	    public void KillRCS()
		{
			upRCS.emit = false;
            EffectBehaviour.RemoveParticleEmitter(upRCS);
            downRCS.emit = false;
            EffectBehaviour.RemoveParticleEmitter(downRCS);
            leftRCS.emit = false;
            EffectBehaviour.RemoveParticleEmitter(leftRCS);
            rightRCS.emit = false;
            EffectBehaviour.RemoveParticleEmitter(rightRCS);
        }

		void OnGUI()
		{
			if(HasFired && BDArmorySettings.DRAW_DEBUG_LABELS)	
			{
				GUI.Label(new Rect(200,300,200,200), debugString);	
			}
			if(HasFired && hasRCS)
			{
				BDGUIUtils.DrawLineBetweenWorldPositions(transform.position, TargetPosition, 2, Color.red);
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
            switch (homingType)
            {
                case "aam":
                    GuidanceMode = GuidanceModes.AAMLead;
                    break;
                case "aamlead":
                    GuidanceMode = GuidanceModes.AAMLead;
                    break;
                case "aampure":
                    GuidanceMode = GuidanceModes.AAMPure;
                    break;
                case "agm":
                    GuidanceMode = GuidanceModes.AGM;
                    break;
                case "agmballistic":
                    GuidanceMode = GuidanceModes.AGMBallistic;
                    break;
                case "cruise":
                    GuidanceMode = GuidanceModes.Cruise;
                    break;
                case "sts":
                    GuidanceMode = GuidanceModes.STS;
                    break;
                case "rcs":
                    GuidanceMode = GuidanceModes.RCS;
                    break;
                case "beamriding":
                    GuidanceMode = GuidanceModes.BeamRiding;
                    break;
                case "slw":
                    GuidanceMode = GuidanceModes.SLW;
                    break;
                default:
                    GuidanceMode = GuidanceModes.None;
                    break;
            }

            targetingType = targetingType.ToLower();
			switch(targetingType)
			{
			case "radar":
				TargetingMode = TargetingModes.Radar;
				break;
			case "heat":
				TargetingMode = TargetingModes.Heat;
				break;
			case "laser":
				TargetingMode = TargetingModes.Laser;
				break;
			case "gps":
				TargetingMode = TargetingModes.Gps;
				maxOffBoresight = 360;
				break;
			case "antirad":
				TargetingMode = TargetingModes.AntiRad;
				break;
			default:
				TargetingMode = TargetingModes.None;
				break;
			}

            terminalGuidanceType = terminalGuidanceType.ToLower();
            switch (terminalGuidanceType)
            {
                case "radar":
                    TargetingModeTerminal = TargetingModes.Radar;
                    break;
                case "heat":
                    TargetingModeTerminal = TargetingModes.Heat;
                    break;
                case "laser":
                    TargetingModeTerminal = TargetingModes.Laser;
                    break;
                case "gps":
                    TargetingModeTerminal = TargetingModes.Gps;
                    maxOffBoresight = 360;
                    break;
                case "antirad":
                    TargetingModeTerminal = TargetingModes.AntiRad;
                    break;
                default:
                    TargetingModeTerminal = TargetingModes.None;
                    break;
            }
        }


        // RMB info in editor
        public override string GetInfo()
        {
            var output = new StringBuilder();
            output.Append(Environment.NewLine);
            output.Append(String.Format("Weapon Type: {0}", missileType));
            output.Append(Environment.NewLine);
            output.Append(String.Format("Guidance Mode: {0}", homingType.ToString().ToUpper()));
            output.Append(Environment.NewLine);
            output.Append(String.Format("Targetting Mode: {0}", targetingType.ToString().ToUpper()));
            output.Append(Environment.NewLine);

            if (targetingType.ToLower() == "radar")
            {
                output.Append(String.Format("Active Radar Range: {0}", activeRadarRange));
                output.Append(Environment.NewLine);
            }

            if (targetingType.ToLower() == "gps")
            {
                output.Append(String.Format("Terminal Maneuvering: {0}", terminalManeuvering));
                output.Append(Environment.NewLine);
                if (terminalGuidanceType != "")
                {
                    output.Append(String.Format("Terminal guidance: {0}, distance: {1} meters", terminalGuidanceType, terminalGuidanceDistance));
                    output.Append(Environment.NewLine);
                }
            }


            output.Append(String.Format("Min/Max Range: {0}/{1} meters", minStaticLaunchRange, maxStaticLaunchRange));
            output.Append(Environment.NewLine);
            output.Append(String.Format("Blast radius/power/heat: {0}/{1}/{2}", blastRadius, blastPower, blastHeat));
            output.Append(Environment.NewLine);
            return output.ToString();

        }

    }
}

