using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using BDArmory.Core;
using BDArmory.UI;
using BDArmory.Core.Extension;
using BDArmory.Core.Utils;
using BDArmory.FX;
using BDArmory.Guidances;
using BDArmory.Misc;
using BDArmory.Radar;
using UniLinq;
using UnityEngine;
using BDArmory.Targeting;
using BDArmory.Parts;

namespace BDArmory.Modules
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
        float controlAuthority;
        float finalMaxTorque;
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

        private bool terminalGuidanceActive;
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Terminal Guidance: "), UI_Toggle(disabledText = "false", enabledText = "true")]
        public bool terminalGuidanceShouldActivate = true;

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
        public Vector3 simpleCoD = new Vector3(0, 0, -1);

        [KSPField]
        public float agmDescentRatio = 1.45f;

        float currentThrust;

        public bool deployed;
        //public float deployedTime;

        AnimationState[] deployStates;

        bool hasPlayedFlyby;

        float debugTurnRate;

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

        [KSPField]
        public bool vacuumSteerable = true;


        public GPSTargetInfo designatedGPSInfo;

        float[] rcsFiredTimes;
        KSPParticleEmitter[] rcsTransforms;

        #endregion


        [KSPAction("Fire Missile")]
        public void AGFire(KSPActionParam param)
        {
            if (BDArmorySetup.Instance.ActiveWeaponManager != null && BDArmorySetup.Instance.ActiveWeaponManager.vessel == vessel) BDArmorySetup.Instance.ActiveWeaponManager.SendTargetDataToMissile(this);
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
            if (BDArmorySetup.Instance.ActiveWeaponManager != null) BDArmorySetup.Instance.ActiveWeaponManager.UpdateList();
        }

        [KSPEvent(guiActive = true, guiName = "Fire Missile", active = true)]
        public void GuiFire()
        {
            if (BDArmorySetup.Instance.ActiveWeaponManager != null && BDArmorySetup.Instance.ActiveWeaponManager.vessel == vessel) BDArmorySetup.Instance.ActiveWeaponManager.SendTargetDataToMissile(this);
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
            if (BDArmorySetup.Instance.ActiveWeaponManager != null) BDArmorySetup.Instance.ActiveWeaponManager.UpdateList();
        }

        [KSPEvent(guiActive = true, guiActiveEditor = false, active = true, guiName = "Jettison")]
        public override void Jettison()
        {
            if (missileTurret) return;

            part.decouple(0);
            if (BDArmorySetup.Instance.ActiveWeaponManager != null) BDArmorySetup.Instance.ActiveWeaponManager.UpdateList();
        }

        [KSPAction("Jettison")]
        public void AGJettsion(KSPActionParam param)
        {
            Jettison();
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

		    List<KSPParticleEmitter>.Enumerator pEemitter = part.FindModelComponents<KSPParticleEmitter>().GetEnumerator();
            while (pEemitter.MoveNext())
            {
                if (pEemitter.Current == null) continue;
			    EffectBehaviour.AddParticleEmitter(pEemitter.Current);
                pEemitter.Current.emit = false;
            }
            pEemitter.Dispose();

			if(HighLogic.LoadedSceneIsFlight)
			{
			    //TODO: Backward compatibility wordaround
			    if (part.FindModuleImplementing<BDExplosivePart>() == null)
			    {
			        FromBlastPowerToTNTMass();
			    }
			    else
			    {
			        //New Explosive module
                    DisablingExplosives(part);
			    }

                MissileReferenceTransform = part.FindModelTransform("missileTransform");
				if(!MissileReferenceTransform)
				{
					MissileReferenceTransform = part.partTransform;
				}

				if(!string.IsNullOrEmpty(exhaustPrefabPath))
				{
                    IEnumerator<Transform> t = part.FindModelTransforms("exhaustTransform").AsEnumerable().GetEnumerator();

                    while (t.MoveNext())
                    {
                        if (t.Current == null) continue;
						GameObject exhaustPrefab = (GameObject)Instantiate(GameDatabase.Instance.GetModel(exhaustPrefabPath));
						exhaustPrefab.SetActive(true);
                        IEnumerator<KSPParticleEmitter> emitter = exhaustPrefab.GetComponentsInChildren<KSPParticleEmitter>().AsEnumerable().GetEnumerator();
                        while (emitter.MoveNext())
						{
						    if (emitter.Current == null) continue;
							emitter.Current.emit = false;
                        }
                        emitter.Dispose();
						exhaustPrefab.transform.parent = t.Current;
						exhaustPrefab.transform.localPosition = Vector3.zero;
						exhaustPrefab.transform.localRotation = Quaternion.identity;
					}
                    t.Dispose();
				}

				if(!string.IsNullOrEmpty(boostExhaustPrefabPath) && !string.IsNullOrEmpty(boostExhaustTransformName))
				{
                    IEnumerator<Transform> t = part.FindModelTransforms(boostExhaustTransformName).AsEnumerable().GetEnumerator();

                    while (t.MoveNext())
                    {
                        if (t.Current == null) continue;
						GameObject exhaustPrefab = (GameObject)Instantiate(GameDatabase.Instance.GetModel(boostExhaustPrefabPath));
						exhaustPrefab.SetActive(true);
                        IEnumerator<KSPParticleEmitter> emitter = exhaustPrefab.GetComponentsInChildren<KSPParticleEmitter>().AsEnumerable().GetEnumerator();
                        while (emitter.MoveNext())
						{
						    if (emitter.Current == null) continue;
							emitter.Current.emit = false;
                        }
                        emitter.Dispose();
						exhaustPrefab.transform.parent = t.Current;
						exhaustPrefab.transform.localPosition = Vector3.zero;
						exhaustPrefab.transform.localRotation = Quaternion.identity;
					}
                    t.Dispose();
				}


				boosters = new List<GameObject>();
				if(!string.IsNullOrEmpty(boostTransformName))
				{
                    IEnumerator<Transform> t = part.FindModelTransforms(boostTransformName).AsEnumerable().GetEnumerator();
                    while (t.MoveNext())
                    {
                        if (t.Current == null) continue;
						boosters.Add(t.Current.gameObject);
                        IEnumerator<KSPParticleEmitter> be = t.Current.GetComponentsInChildren<KSPParticleEmitter>().AsEnumerable().GetEnumerator();
                        while (be.MoveNext())
						{
						    if (be.Current == null) continue;
							if(be.Current.useWorldSpace)
							{
							    if (be.Current.GetComponent<BDAGaplessParticleEmitter>()) continue;
							    BDAGaplessParticleEmitter ge = be.Current.gameObject.AddComponent<BDAGaplessParticleEmitter>();
							    ge.part = part;
							    boostGaplessEmitters.Add(ge);
							}
							else
							{
								if(!boostEmitters.Contains(be.Current))
								{
									boostEmitters.Add(be.Current);
								}
							    EffectBehaviour.AddParticleEmitter(be.Current);
							}
						}
                        be.Dispose();
                    }
                    t.Dispose();
				}

                IEnumerator<KSPParticleEmitter> pEmitter = part.partTransform.Find("model").GetComponentsInChildren<KSPParticleEmitter>().AsEnumerable().GetEnumerator();
                while (pEmitter.MoveNext())
                {
                    if (pEmitter.Current == null) continue;
					if(pEmitter.Current.GetComponent<BDAGaplessParticleEmitter>() || boostEmitters.Contains(pEmitter.Current))
					{
						continue;
					}

					if(pEmitter.Current.useWorldSpace)
					{
						BDAGaplessParticleEmitter gaplessEmitter = pEmitter.Current.gameObject.AddComponent<BDAGaplessParticleEmitter>();	
						gaplessEmitter.part = part;
						gaplessEmitters.Add (gaplessEmitter);
					}
					else
					{
						if(pEmitter.Current.transform.name != boostTransformName)
						{
							pEmitters.Add(pEmitter.Current);	
						}
						else
						{
							boostEmitters.Add(pEmitter.Current);
						}
					    EffectBehaviour.AddParticleEmitter(pEmitter.Current);
                    }
				}
                pEmitter.Dispose();
					
				cmTimer = Time.time;
				
				part.force_activate();


			    List<KSPParticleEmitter>.Enumerator pe = pEmitters.GetEnumerator();
				while(pe.MoveNext())
				{
				    if (pe.Current == null) continue;
					if(hasRCS)
					{
						if(pe.Current.gameObject.name == "rcsUp") upRCS = pe.Current;
						else if(pe.Current.gameObject.name == "rcsDown") downRCS = pe.Current;
						else if(pe.Current.gameObject.name == "rcsLeft") leftRCS = pe.Current;
						else if(pe.Current.gameObject.name == "rcsRight") rightRCS = pe.Current;
						else if(pe.Current.gameObject.name == "rcsForward") forwardRCS = pe.Current;
					}
					
					if(!pe.Current.gameObject.name.Contains("rcs") && !pe.Current.useWorldSpace)
					{
					    pe.Current.sizeGrow = 99999;
					}
				  
                }
                pe.Dispose();

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
			    CruiseAltitudeRange();
                Fields["CruiseAltitude"].guiActive = false;
			    Fields["CruiseAltitude"].guiActiveEditor = false;
                Fields["CruiseSpeed"].guiActive = false;
                Fields["CruiseSpeed"].guiActiveEditor = false;
                Events["CruiseAltitudeRange"].guiActive = false;
			    Events["CruiseAltitudeRange"].guiActiveEditor = false;
			    Fields["CruisePredictionTime"].guiActiveEditor = false;

            }

            if (GuidanceMode != GuidanceModes.AGM)
            {
                Fields["maxAltitude"].guiActive = false;
                Fields["maxAltitude"].guiActiveEditor = false;
            }
		    if (GuidanceMode != GuidanceModes.AGMBallistic)
		    {
		        Fields["BallisticOverShootFactor"].guiActive = false;
		        Fields["BallisticOverShootFactor"].guiActiveEditor = false;
		    }

            if (part.partInfo.title.Contains("Bomb"))
			{
				Fields["dropTime"].guiActive = false;
				Fields["dropTime"].guiActiveEditor = false;
			}

            if (TargetingModeTerminal != TargetingModes.None)
            {
                Fields["terminalGuidanceShouldActivate"].guiName += terminalGuidanceType;
            }
            else
            {
                Fields["terminalGuidanceShouldActivate"].guiActive = false;
                Fields["terminalGuidanceShouldActivate"].guiActiveEditor = false;
            }

            if (deployAnimationName != "")
			{
				deployStates = Misc.Misc.SetUpAnimation(deployAnimationName, part);
			}
			else
			{
				deployedDrag = simpleDrag;	
			}

		    SetInitialDetonationDistance();
		    this._cruiseGuidance = new CruiseGuidance(this);



            // fill activeRadarLockTrackCurve with default values if not set by part config:
            if ((TargetingMode == TargetingModes.Radar || TargetingModeTerminal == TargetingModes.Radar) && activeRadarRange > 0 && activeRadarLockTrackCurve.minTime == float.MaxValue)
            {
                activeRadarLockTrackCurve.Add(0f, 0f);
                activeRadarLockTrackCurve.Add(activeRadarRange, RadarUtils.MISSILE_DEFAULT_LOCKABLE_RCS);           // TODO: tune & balance constants!
                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                    Debug.Log("[BDArmory]: OnStart missile "+shortName+": setting default locktrackcurve with maxrange/minrcs: "+activeRadarLockTrackCurve.maxTime+"/"+ RadarUtils.MISSILE_DEFAULT_LOCKABLE_RCS);
            }
        }

        /// <summary>
        /// This method will convert the blastPower to a tnt mass equivalent
        /// </summary>
        private void FromBlastPowerToTNTMass()
        {
            blastPower = BlastPhysicsUtils.CalculateExplosiveMass(blastRadius);
        }     

        void OnCollisionEnter(Collision col)
		{
            base.CollisionEnter(col);      
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
			BDArmorySetup.OnVolumeChange += UpdateVolume;
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

        void Update()
        {
            CheckDetonationState();
        }

        void OnDestroy()
		{
			BDArmorySetup.OnVolumeChange -= UpdateVolume;
            GameEvents.onPartDie.Remove(PartDie);
        }

	    public override float GetBlastRadius()
        { 
	        if (part.FindModuleImplementing<BDExplosivePart>() != null )
	        {

                return part.FindModuleImplementing<BDExplosivePart>().GetBlastRadius();

	        }
	        else
	        {
	            return blastRadius;
	        }     
        }

	    public override void FireMissile()
		{
		    if (HasFired) return;

            SetupExplosive(this.part);
            HasFired = true;

            Debug.Log("[BDArmory]: Missile Fired! " + vessel.vesselName);

            GameEvents.onPartDie.Add(PartDie);
		    BDATargetManager.FiredMissiles.Add(this);

		    if(GetComponentInChildren<KSPParticleEmitter>())
		    {
		        BDArmorySetup.numberOfParticleEmitters++;
		    }

		    List<MissileFire>.Enumerator wpm = vessel.FindPartModulesImplementing<MissileFire>().GetEnumerator();
		    while(wpm.MoveNext())
		    {
		        if (wpm.Current == null) continue;
		        Team = wpm.Current.team;	
		        break;
		    }
		    wpm.Dispose();
				
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
		            List<MissileFire>.Enumerator mf = legacyTargetVessel.FindPartModulesImplementing<MissileFire>().GetEnumerator();
		            while(mf.MoveNext())
		            {
		                if (mf.Current == null) continue;
		                TargetMf = mf.Current;
		                break;
		            }
                    mf.Dispose();

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
		    DetonationDistanceState = DetonationDistanceStates.NotSafe;
            MissileState = MissileStates.Drop;
		    part.crashTolerance = 9999; //to combat stresses of launch, missle generate a lot of G Force

            StartCoroutine(MissileRoutine());
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
            debugString.Length = 0;

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
				float mCamRelVSqr = (float)(FlightGlobals.ActiveVessel.Velocity() - vessel.Velocity()).sqrMagnitude;
				if(!hasPlayedFlyby 
				   && FlightGlobals.ActiveVessel != vessel 
				   && FlightGlobals.ActiveVessel != SourceVessel 
				   && mCamDistanceSqr < 400*400 && mCamRelVSqr > 300*300  
				   && mCamRelVSqr < 800*800 
				   && Vector3.Angle(vessel.Velocity(), FlightGlobals.ActiveVessel.transform.position-transform.position)<60)
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
				//RaycastCollisions();
            
                //Timed detonation
                if (isTimed && TimeIndex > detonationTime)
				{					
                    Detonate();
				}
			}
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
                bool noProgress = MissileState == MissileStates.PostThrust && (Vector3.Dot(vessel.Velocity() - TargetVelocity, TargetPosition - vessel.transform.position) < 0);
                if (Vector3.Dot(TargetPosition - transform.position, transform.forward) < 0 || noProgress)                
                {
                    Debug.Log("[BDArmory]: Missile has missed!");

                    if (vessel.altitude >= maxAltitude && maxAltitude != 0f)
                        Debug.Log("[BDArmory]: CheckMiss trigged by MaxAltitude");

                    HasMissed = true;
                    guidanceActive = false;

                    TargetMf = null;

                    MissileLauncher launcher = this as MissileLauncher;
                    if (launcher != null)
                    {
                        if (launcher.hasRCS) launcher.KillRCS();
                    }

                    if (sqrDist < Mathf.Pow(GetBlastRadius() * 0.5f, 2)) part.Destroy();

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

			    if (vacuumSteerable)
			    {
			        controlAuthority = 1;
			    }

				debugString.Append($"controlAuthority: {controlAuthority}");
                debugString.Append(Environment.NewLine);

				if(guidanceActive)// && timeIndex - dropTime > 0.5f)
				{
					WarnTarget();

					if(legacyTargetVessel && legacyTargetVessel.loaded)
					{
						Vector3 targetCoMPos = legacyTargetVessel.CoM;
						TargetPosition = targetCoMPos+legacyTargetVessel.Velocity() * Time.fixedDeltaTime;
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
					    var clampedTurnRate = Mathf.Clamp(maxTurnRateDPS - ((TimeIndex - dropTime - boostTime - cruiseTime) * 0.45f),
					        1, maxTurnRateDPS);
					    turnRateDPS = clampedTurnRate;

                        if (!vacuumSteerable)
                        {
                            turnRateDPS *= atmosMultiplier;
                        }
	
                        if (hasRCS) 
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
                        part.transform.rotation = Quaternion.RotateTowards(part.transform.rotation, Quaternion.LookRotation(TargetPosition - part.transform.position, part.transform.up), turnRateDPS * Time.fixedDeltaTime);
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
						aeroTorque = MissileGuidance.DoAeroForces(this, transform.position + (20*vessel.Velocity()), liftArea, .25f, aeroTorque, maxTorque, maxAoA);
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
            float distanceSqr = (TargetPosition - transform.position).sqrMagnitude;

            if ((TargetingModeTerminal != TargetingModes.None) && (distanceSqr < terminalGuidanceDistance*terminalGuidanceDistance) && !terminalGuidanceActive && terminalGuidanceShouldActivate)
            {
                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                    Debug.Log("[BDArmory][Terminal Guidance]: missile "+this.name+" updating targeting mode: " + terminalGuidanceType);

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
                        TargetSignatureData[] scannedTargets = new TargetSignatureData[5];
                        TargetSignatureData.ResetTSDArray(ref scannedTargets);
                        Ray ray = new Ray(transform.position, TargetPosition - GetForwardTransform());

                        //RadarUtils.UpdateRadarLock(ray, maxOffBoresight, activeRadarMinThresh, ref scannedTargets, 0.4f, true, RadarWarningReceiver.RWRThreatTypes.MissileLock, true);
                        RadarUtils.RadarUpdateMissileLock(ray, maxOffBoresight, ref scannedTargets, 0.4f, this);
                        float sqrThresh = Mathf.Pow(terminalGuidanceDistance * 1.5f, 2);

                        //float smallestAngle = maxOffBoresight;
                        TargetSignatureData lockedTarget = TargetSignatureData.noTarget;

                        for (int i = 0; i < scannedTargets.Length; i++)
                        {
                            if (scannedTargets[i].exists && (scannedTargets[i].predictedPosition - TargetPosition).sqrMagnitude < sqrThresh)
                            {
                                //re-check engagement envelope, only lock appropriate targets
                                if (CheckTargetEngagementEnvelope(scannedTargets[i].targetInfo))
                                {
                                     lockedTarget = scannedTargets[i];
                                     ActiveRadar = true;
                                }
                            }
                        }

                        if (lockedTarget.exists)
                        {
                            radarTarget = lockedTarget;
                            TargetAcquired = true;
                            TargetPosition = radarTarget.predictedPositionWithChaffFactor;
                            TargetVelocity = radarTarget.velocity;
                            TargetAcceleration = radarTarget.acceleration;
                            targetGPSCoords = VectorUtils.WorldPositionToGeoCoords(TargetPosition, vessel.mainBody);

                            if (weaponClass == WeaponClasses.SLW)
                                RadarWarningReceiver.PingRWR(new Ray(transform.position, radarTarget.predictedPosition - transform.position), 45, RadarWarningReceiver.RWRThreatTypes.Torpedo, 2f);
                            else
                                RadarWarningReceiver.PingRWR(new Ray(transform.position, radarTarget.predictedPosition - transform.position), 45, RadarWarningReceiver.RWRThreatTypes.MissileLaunch, 2f);
                                 
                                Debug.Log("[BDArmory][Terminal Guidance]: Pitbull! Radar missileBase has gone active.  Radar sig strength: " + radarTarget.signalStrength.ToString("0.0") + " - target: "+radarTarget.vessel.name);
                        }
                        else
                        {
                            TargetAcquired = true;                            
                            TargetPosition = VectorUtils.GetWorldSurfacePostion(UpdateGPSTarget(), vessel.mainBody); //putting back the GPS target if no radar target found
                            TargetVelocity = Vector3.zero;
                            TargetAcceleration = Vector3.zero;
                            targetGPSCoords = VectorUtils.WorldPositionToGeoCoords(TargetPosition, vessel.mainBody);                            
                            Debug.Log("[BDArmory][Terminal Guidance]: Missile radar could not acquire a target lock - Defaulting to GPS Target");                            
                            
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
            if (MissileState == MissileStates.PostThrust) return;

			if(currentThrust * Throttle > 0)
			{
			    debugString.Append("Missile thrust=" + currentThrust * Throttle);
			    debugString.Append(Environment.NewLine);

                part.rb.AddRelativeForce(currentThrust * Throttle * Vector3.forward);
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
			    IEnumerator<AnimationState> anim = deployStates.AsEnumerable().GetEnumerator();
                while (anim.MoveNext())
                {
                    if (anim.Current == null) continue;
					anim.Current.speed = 1;
				}
                anim.Dispose();
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
				if(!BDArmorySetup.GameIsPaused)
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
                List<KSPParticleEmitter>.Enumerator emitter = boostEmitters.GetEnumerator();
                while (emitter.MoveNext())
                {
                    if (emitter.Current == null) continue;
					if(!hasRCS)
					{
						emitter.Current.sizeGrow = Mathf.Lerp(emitter.Current.sizeGrow, 0, 20*Time.deltaTime);
					}
				}
                emitter.Dispose();

			    List<BDAGaplessParticleEmitter>.Enumerator gpe = boostGaplessEmitters.GetEnumerator();
				while(gpe.MoveNext())
				{
				    if (gpe.Current == null) continue;
					if(!vessel.InVacuum() && Throttle > 0)
					{
						gpe.Current.emit = true;                        
                        gpe.Current.pEmitter.worldVelocity = 2 * ParticleTurbulence.flareTurbulence;
					}
					else
					{
						gpe.Current.emit = false;
                    }	
				}
                gpe.Dispose();

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

		    IEnumerator<Light> light = gameObject.GetComponentsInChildren<Light>().AsEnumerable().GetEnumerator();
            while (light.MoveNext())
			{
			    if (light.Current == null) continue;
				light.Current.intensity = 1.5f;	
			}
            light.Dispose();

			if(!spoolEngine)
			{
				currentThrust = thrust;	
			}

			if(string.IsNullOrEmpty(boostTransformName))
			{
                boostEmitters = pEmitters;                
                boostGaplessEmitters = gaplessEmitters;
			}

		    List<KSPParticleEmitter>.Enumerator emitter = boostEmitters.GetEnumerator();
			while(emitter.MoveNext())
			{
			    if (emitter.Current == null) continue;
                emitter.Current.emit = true;
            }
            emitter.Dispose();

			if(hasRCS)
			{
                forwardRCS.emit = true;
			}

		    if (!(thrust > 0)) return;
		    sfAudioSource.PlayOneShot(GameDatabase.Instance.GetAudioClip("BDArmory/Sounds/launch"));
		    RadarWarningReceiver.WarnMissileLaunch(transform.position, transform.forward);
		}

		void EndBoost()
		{
		    List<KSPParticleEmitter>.Enumerator emitter = boostEmitters.GetEnumerator();
		    while (emitter.MoveNext())
		    {
		        if (emitter.Current == null) continue;
				emitter.Current.emit = false;

            }
            emitter.Dispose();

		    List<BDAGaplessParticleEmitter>.Enumerator gEmitter = boostGaplessEmitters.GetEnumerator();
			while(gEmitter.MoveNext())
			{
			    if (gEmitter.Current == null) continue;
			    gEmitter.Current.emit = false;
            }
            gEmitter.Dispose();

			if(decoupleBoosters)
			{
				part.mass -= boosterMass;
			    List<GameObject>.Enumerator booster = boosters.GetEnumerator();
				while(booster.MoveNext())
				{
					if(booster.Current == null) continue;
					booster.Current.AddComponent<DecoupledBooster>().DecoupleBooster(part.rb.velocity, boosterDecoupleSpeed);
				}
                booster.Dispose();
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
				if(!BDArmorySetup.GameIsPaused)
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
			    audioSource.volume = Throttle;
                
                //particleFx
                List<KSPParticleEmitter>.Enumerator emitter = pEmitters.GetEnumerator();
			    while (emitter.MoveNext())
			    {
			        if (emitter.Current == null) continue;
			        if (!hasRCS)
			        {
			            emitter.Current.sizeGrow = Mathf.Lerp(emitter.Current.sizeGrow, 0, 20 * Time.deltaTime);
			        }

			        emitter.Current.maxSize = Mathf.Clamp01(Throttle/ Mathf.Clamp((float) vessel.atmDensity, 0.2f, 1f));
			        emitter.Current.emit = true;
                }
			    emitter.Dispose();

			    List<BDAGaplessParticleEmitter>.Enumerator gpe = gaplessEmitters.GetEnumerator();
				while(gpe.MoveNext())
				{
				    if (gpe.Current == null) continue;
                        gpe.Current.pEmitter.maxSize = Mathf.Clamp01(Throttle / Mathf.Clamp((float)vessel.atmDensity, 0.2f, 1f));
                        gpe.Current.emit = true;                       
					    gpe.Current.pEmitter.worldVelocity = 2 * ParticleTurbulence.flareTurbulence;
				}
                gpe.Dispose();

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

			currentThrust = spoolEngine ? 0 : cruiseThrust;

            List<KSPParticleEmitter>.Enumerator pEmitter = pEmitters.GetEnumerator();
            while (pEmitter.MoveNext())
            {
                if (pEmitter.Current == null) continue;
                EffectBehaviour.AddParticleEmitter(pEmitter.Current);
                pEmitter.Current.emit = true;
			}
            pEmitter.Dispose();

            List<BDAGaplessParticleEmitter>.Enumerator gEmitter = gaplessEmitters.GetEnumerator();
            while (gEmitter.MoveNext())
            {
                if (gEmitter.Current == null) continue;
                EffectBehaviour.AddParticleEmitter(gEmitter.Current.pEmitter);
                gEmitter.Current.emit = true;
			}
            gEmitter.Dispose();

		    if (!hasRCS) return;
		    forwardRCS.emit = false;
		    audioSource.Stop();
		}

		void EndCruise()
		{
			MissileState = MissileStates.PostThrust;

            IEnumerator<Light> light = gameObject.GetComponentsInChildren<Light>().AsEnumerable().GetEnumerator();
            while (light.MoveNext())
            {
                if (light.Current == null) continue;
				light.Current.intensity = 0;	
			}
            light.Dispose();

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
			    List<KSPParticleEmitter>.Enumerator pe = pEmitters.GetEnumerator();
				while(pe.MoveNext())
				{
					if(pe.Current == null) continue;
				    pe.Current.maxEmission = Mathf.FloorToInt(pe.Current.maxEmission * 0.8f);
				    pe.Current.minEmission = Mathf.FloorToInt(pe.Current.minEmission * 0.8f);
				}
                pe.Dispose();

			    List<BDAGaplessParticleEmitter>.Enumerator gpe = gaplessEmitters.GetEnumerator();
				while(gpe.MoveNext())
				{
					if(gpe.Current == null) continue;
				    gpe.Current.pEmitter.maxSize = Mathf.MoveTowards(gpe.Current.pEmitter.maxSize, 0, 0.005f);
				    gpe.Current.pEmitter.minSize = Mathf.MoveTowards(gpe.Current.pEmitter.minSize, 0, 0.008f);
				    gpe.Current.pEmitter.worldVelocity = ParticleTurbulence.Turbulence;
				}
                gpe.Dispose();
				yield return new WaitForFixedUpdate();
			}

		    List<KSPParticleEmitter>.Enumerator pe2 = pEmitters.GetEnumerator();
		    while (pe2.MoveNext())
		    {
		        if (pe2.Current == null) continue;
				pe2.Current.emit = false;
            }
            pe2.Dispose();

		    List<BDAGaplessParticleEmitter>.Enumerator gpe2 = gaplessEmitters.GetEnumerator();
		    while (gpe2.MoveNext())
		    {
		        if (gpe2.Current == null) continue;
				gpe2.Current.emit = false;
            }
            gpe2.Dispose();
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
			Ray laserBeam = new Ray(targetingPod.cameraParentTransform.position + (targetingPod.vessel.Velocity() * Time.fixedDeltaTime), targetingPod.targetPointPosition - targetingPod.cameraParentTransform.position);
			Vector3 target = MissileGuidance.GetBeamRideTarget(laserBeam, part.transform.position, vessel.Velocity(), beamCorrectionFactor, beamCorrectionDamping, (TimeIndex > 0.25f ? previousBeam : laserBeam));
			previousBeam = laserBeam;
			DrawDebugLine(part.transform.position, target);
			DoAero(target);
		}

		void CruiseGuidance()
		{
			Vector3 cruiseTarget = Vector3.zero;

		    cruiseTarget = this._cruiseGuidance.CalculateCruiseGuidance(TargetPosition);

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
                
				rotationTransform.localRotation = Quaternion.Lerp(rotationTransform.localRotation, Quaternion.LookRotation(Vector3.forward, lookUpDirection), 0.04f);
				Quaternion finalRotation = rotationTransform.rotation;
				transform.rotation = originalRotation;
				rotationTransform.rotation = finalRotation;

				vesselReferenceTransform.rotation = Quaternion.LookRotation(-rotationTransform.up, rotationTransform.forward);
			}		
			DoAero(cruiseTarget);
			CheckMiss();
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
				if(proxyDetonate && ((TargetPosition+(TargetVelocity*Time.fixedDeltaTime))-(transform.position)).sqrMagnitude < Mathf.Pow(GetBlastRadius()*0.5f,2))
				{
					part.Destroy();
                }
			}
			else
			{
				aamTarget = transform.position + (20*vessel.Velocity().normalized);
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
                if (proxyDetonate && ((TargetPosition + (TargetVelocity * Time.fixedDeltaTime)) - (transform.position)).sqrMagnitude < Mathf.Pow(GetBlastRadius() * 0.5f, 2))
                {
                    part.Destroy();
                }
            }
            else
            {
                SLWTarget = transform.position + (20 * vessel.Velocity().normalized);
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
            DoAero(CalculateAGMBallisticGuidance(this,TargetPosition));
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
					TargetPosition = legacyTargetVessel.CoM + (legacyTargetVessel.Velocity() * Time.fixedDeltaTime);
					targetGPSCoords = VectorUtils.WorldPositionToGeoCoords(TargetPosition, vessel.mainBody);
					TargetVelocity = legacyTargetVessel.Velocity();
					TargetAcceleration = legacyTargetVessel.acceleration;
					lastLaserPoint = TargetPosition;
					lockFailTimer = 0;
				}
			}
		}
	
		public override void Detonate()
		{
		    if (HasExploded || !HasFired) return;

            Debug.Log("[BDArmory]: Detonate Triggered");

            BDArmorySetup.numberOfParticleEmitters--;	
		    HasExploded = true;
				
		    if(legacyTargetVessel!=null)
		    {
		        List<MissileFire>.Enumerator wpm = legacyTargetVessel.FindPartModulesImplementing<MissileFire>().GetEnumerator();
		        while(wpm.MoveNext())
		        {
		            if (wpm.Current == null) continue;
		            wpm.Current.missileIsIncoming = false;
		        }
		        wpm.Dispose();
		    }
				
		    if(SourceVessel==null) SourceVessel = vessel;

		    if (part.FindModuleImplementing<BDExplosivePart>() != null)
		    {
		        part.FindModuleImplementing<BDExplosivePart>().DetonateIfPossible();
		    }
		    else //TODO: Remove this backguard compatibility
		    {
		        Vector3 position = transform.position;//+rigidbody.velocity*Time.fixedDeltaTime;

		        ExplosionFx.CreateExplosion(position, blastPower, explModelPath, explSoundPath, true, 0, part);
            }
		    

            List<BDAGaplessParticleEmitter>.Enumerator e = gaplessEmitters.GetEnumerator();
            while (e.MoveNext())
            {
                if (e.Current == null) continue;
                e.Current.gameObject.AddComponent<BDAParticleSelfDestruct>();
                e.Current.transform.parent = null;
		        if(e.Current.GetComponent<Light>())
		        {
		            e.Current.GetComponent<Light>().enabled = false;
		        }
		    }
            e.Dispose();

            if (part != null)
            {
                part.Destroy();
                part.explode();
            }
        }

	    public override Vector3 GetForwardTransform()
	    {
	        return MissileReferenceTransform.forward;
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

        public static bool CheckIfMissile(Part p)
        {
            return p.GetComponent<MissileLauncher>();
        }
		
		void WarnTarget()
		{
			if(legacyTargetVessel == null) return;
		    if (legacyTargetVessel == null) return;
		    List<MissileFire>.Enumerator wpm = legacyTargetVessel.FindPartModulesImplementing<MissileFire>().GetEnumerator();
		    while(wpm.MoveNext())
		    {
		        if (wpm.Current == null) continue;
		        wpm.Current.MissileWarning(Vector3.Distance(transform.position, legacyTargetVessel.transform.position), this);
		        break;
		    }
            wpm.Dispose();
		}

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
		    if (HighLogic.LoadedSceneIsFlight)
		    {
                try
                {
                    drawLabels();
                }
                catch (Exception)
                { }
                
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
			//float simSpeedSquared = (float)vessel.Velocity.sqrMagnitude;
			float simSpeedSquared = (part.rb.GetPointVelocity(part.transform.TransformPoint(simpleCoD))+(Vector3)Krakensbane.GetFrameVelocity()).sqrMagnitude;
			Vector3 currPos = transform.position;
			float drag = deployed ? deployedDrag : simpleDrag;
			float dragMagnitude = (0.008f * part.rb.mass) * drag * 0.5f * simSpeedSquared * (float)FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(currPos), FlightGlobals.getExternalTemperature(), FlightGlobals.currentMainBody);
			Vector3 dragForce = dragMagnitude * vessel.Velocity().normalized;
			part.rb.AddForceAtPosition(-dragForce, transform.TransformPoint(simpleCoD));

			Vector3 torqueAxis = -Vector3.Cross(vessel.Velocity(), part.transform.forward).normalized;
			float AoA = Vector3.Angle(part.transform.forward, vessel.Velocity());
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

        private string GetBrevityCode()
        {
            //torpedo: determine subtype
            if (missileType.ToLower() == "torpedo")
            {
                if ((TargetingMode == TargetingModes.Radar) && (activeRadarRange > 0))
                    return "Active Sonar";

                if ((TargetingMode == TargetingModes.Radar) && (activeRadarRange <= 0))
                    return "Passive Sonar";

                if ((TargetingMode == TargetingModes.Laser) || (TargetingMode == TargetingModes.Gps))
                    return "Optical/wireguided";

                if ((TargetingMode == TargetingModes.Heat))
                    return "Heat guided";

                if ((TargetingMode == TargetingModes.None))
                    return "Unguided";
            }

            if (missileType.ToLower() == "bomb")
            {
                if ((TargetingMode == TargetingModes.Laser) || (TargetingMode == TargetingModes.Gps))
                    return "JDAM";

                if ((TargetingMode == TargetingModes.None))
                    return "Unguided";
            }

            //else: missiles:

            if (TargetingMode == TargetingModes.Radar)
            {
                //radar: determine subtype
                if (activeRadarRange <= 0)
                    return "SARH";
                if (activeRadarRange > 0 && activeRadarRange < maxStaticLaunchRange)
                    return "Mixed SARH/F&F";
                if (activeRadarRange >= maxStaticLaunchRange)
                    return "Fire&Forget";
            }

            if (TargetingMode == TargetingModes.AntiRad)
                return "Fire&Forget";

            if (TargetingMode == TargetingModes.Heat)
                return "Fire&Forget";

            if (TargetingMode == TargetingModes.Laser)
                return "SALH";

            if (TargetingMode == TargetingModes.Gps)
            {
                return TargetingModeTerminal != TargetingModes.None ? "GPS/Terminal" : "GPS";
            }

            // default:
            return "Unguided";
        }

        // RMB info in editor
        public override string GetInfo()
        {
            ParseModes();

            StringBuilder output = new StringBuilder();
            output.AppendLine($"{missileType.ToUpper()} - {GetBrevityCode()}");
            output.Append(Environment.NewLine);
            output.AppendLine($"Targeting Type: {targetingType.ToLower()}");
            output.AppendLine($"Guidance Mode: {homingType.ToLower()}");
            if (missileRadarCrossSection != RadarUtils.RCS_MISSILES)
            {
                output.AppendLine($"Detectable cross section: {missileRadarCrossSection} m^2");
            }
            output.AppendLine($"Min Range: {minStaticLaunchRange} m");
            output.AppendLine($"Max Range: {maxStaticLaunchRange} m");

            if (TargetingMode == TargetingModes.Radar)
            {
                if (activeRadarRange > 0)
                {
                    output.AppendLine($"Active Radar Range: {activeRadarRange} m");
                    if (activeRadarLockTrackCurve.maxTime > 0)
                        output.AppendLine($"- Lock/Track: {activeRadarLockTrackCurve.Evaluate(activeRadarLockTrackCurve.maxTime)} m^2 @ {activeRadarLockTrackCurve.maxTime} km");
                    else
                        output.AppendLine($"- Lock/Track: {RadarUtils.MISSILE_DEFAULT_LOCKABLE_RCS} m^2 @ {activeRadarRange/1000} km");
                    output.AppendLine($"- LOAL: {radarLOAL}");
                }
                output.AppendLine($"Max Offborsight: {maxOffBoresight}");
                output.AppendLine($"Locked FOV: {lockedSensorFOV}");
            }

            if (TargetingMode == TargetingModes.Heat)
            {
                output.AppendLine($"All Aspect: {allAspect}");
                output.AppendLine($"Min Heat threshold: {heatThreshold}");
                output.AppendLine($"Max Offborsight: {maxOffBoresight}");
                output.AppendLine($"Locked FOV: {lockedSensorFOV}");
            }

            if (TargetingMode == TargetingModes.Gps)
            {
                output.AppendLine($"Terminal Maneuvering: {terminalManeuvering}");
                if (terminalGuidanceType != "")
                {
                    output.AppendLine($"Terminal guidance: {terminalGuidanceType} @ distance: {terminalGuidanceDistance} m");

                    if (TargetingModeTerminal == TargetingModes.Radar)
                    {
                        output.AppendLine($"Active Radar Range: {activeRadarRange} m");
                        if (activeRadarLockTrackCurve.maxTime > 0)
                            output.AppendLine($"- Lock/Track: {activeRadarLockTrackCurve.Evaluate(activeRadarLockTrackCurve.maxTime)} m^2 @ {activeRadarLockTrackCurve.maxTime} km");
                        else
                            output.AppendLine($"- Lock/Track: {RadarUtils.MISSILE_DEFAULT_LOCKABLE_RCS} m^2 @ {activeRadarRange / 1000} km");
                        output.AppendLine($"- LOAL: {radarLOAL}");
                        output.AppendLine($"Max Offborsight: {maxOffBoresight}");
                        output.AppendLine($"Locked FOV: {lockedSensorFOV}");
                    }

                    if (TargetingModeTerminal == TargetingModes.Heat)
                    {
                        output.AppendLine($"All Aspect: {allAspect}");
                        output.AppendLine($"Min Heat threshold: {heatThreshold}");
                        output.AppendLine($"Max Offborsight: {maxOffBoresight}");
                        output.AppendLine($"Locked FOV: {lockedSensorFOV}");
                    }

                }
            }

            IEnumerator<PartModule> partModules = part.Modules.GetEnumerator();
            output.AppendLine($"Warhead:");
            while (partModules.MoveNext())
            {
                if (partModules.Current == null) continue;
                if (partModules.Current.moduleName != "BDExplosivePart") continue;
                float tntMass = ((BDExplosivePart) partModules.Current).tntMass;
                output.AppendLine($"- Blast radius: {Math.Round(BlastPhysicsUtils.CalculateBlastRange(tntMass), 2)} m");
                output.AppendLine($"- tnt Mass: {tntMass} kg");
                break;
            }
            partModules.Dispose();

            return output.ToString();
        }

    }
}

