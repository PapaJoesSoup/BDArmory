using System;
using System.Collections.Generic;
using UnityEngine;

namespace BahaTurret
{
	[Obsolete("Use ModuleWeapon and/or ModuleTurret instead")]
	public class BahaTurret : PartModule
	{
		[KSPField(isPersistant = true)]
		public bool turretEnabled = false;  //user initiated

		[KSPField(isPersistant = false)]
		public bool autoReturnTurret = true;
		
		public bool deployed = false;  //actual status
		
		//autoFire vars
		public bool autoFire = false;
		public Vessel autoFireTarget = null;
		public bool guardMode = false;
		public float autoFireLength = 1;
		public float autoFireTimer = 0;
		
		public AnimationState[] deployStates;
		private AnimationState[] fireStates;
		private float fireAnimSpeed = 1;
		
		
		private Transform pitchTransform;
		private Transform yawTransform;
		public Transform referenceTransform;
		private Vector3 yawAxis;
		private Vector3 pitchAxis;
		private float timeCheck = 0;
		
		
		//Gun specs
		[KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Min Pitch"),
        	UI_FloatRange(minValue = 1f, maxValue = 0f, stepIncrement = 1f, scene = UI_Scene.All)]
		public float minPitch = -5;
		[KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Max Pitch"),
        	UI_FloatRange(minValue = 0f, maxValue = 60f, stepIncrement = 1f, scene = UI_Scene.All)]
		public float maxPitch = 80;
		[KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Yaw Range"),
        	UI_FloatRange(minValue = 1f, maxValue = 60f, stepIncrement = 1f, scene = UI_Scene.All)]
		public float yawRange = -1;
		[KSPField(isPersistant = true)]
		public float minPitchLimit = 400;
		[KSPField(isPersistant = true)]
		public float maxPitchLimit = 400;
		[KSPField(isPersistant = true)]
		public float yawRangeLimit = 400;
		
		[KSPField(isPersistant = false)]
		public float rotationSpeed = 2;
		
		[KSPField(isPersistant = false)]
		public float maxTargetingRange = 2000;
		[KSPField(isPersistant = false)]
		public float roundsPerMinute = 850;
		[KSPField(isPersistant = false)]
		public float accuracy = 4;
		[KSPField(isPersistant = false)]
		public float maxEffectiveDistance = 2500;
		
		[KSPField(isPersistant = false)]
		public float bulletMass = 5.40133e-5f;
		[KSPField(isPersistant = false)]
		public float bulletVelocity = 860;
		[KSPField(isPersistant = false)]
		public string ammoName = "50CalAmmo";
		[KSPField(isPersistant = false)]
		public float shellScale = 0.66f;
		[KSPField(isPersistant = false)]
		public bool hasRecoil = true;
		
		[KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Fire Limits"), 
			UI_Toggle(disabledText = "None", enabledText = "In range")]
		public bool onlyFireInRange = true;
		
		[KSPField(isPersistant = false)]
		public float requestResourceAmount = 1;
		[KSPField(isPersistant = false)]
		public bool bulletDrop = true;
		
		[KSPField(isPersistant = false)]
		public string weaponType = "ballistic";
		
		[KSPField(isPersistant = false)]
		public float laserDamage = 10000;
		[KSPField(isPersistant = false)]
		public bool autoLockCapable = false;
		
		[KSPField(isPersistant = false)]
		public string projectileColor = "255, 130, 0, 255";
		Color projectileColorC;
		[KSPField(isPersistant = false)]
		public bool fadeColor = false;
		[KSPField(isPersistant = false)]
		public string startColor = "255, 160, 0, 200";
		Color startColorC;
		
		[KSPField(isPersistant = false)]
		public float cannonShellRadius = 30;
		[KSPField(isPersistant = false)]
		public float cannonShellPower = 8;
		
		
		[KSPField(isPersistant = false)]
		public float maxHeat = 3600;
		[KSPField(isPersistant = false)]
		public float heatPerShot = 75;
		[KSPField(isPersistant = false)]
		public float heatLoss = 250;
		
		[KSPField(isPersistant = false)]
		public float tracerStartWidth = 0.25f;
		[KSPField(isPersistant = false)]
		public float tracerEndWidth = 0.2f;
		//
		
		
		public float heat = 0;
		public bool isOverheated = false;
		
		[KSPField(isPersistant = false)]
		public string bulletTexturePath = "BDArmory/Textures/bullet";
		
		[KSPField(isPersistant = false)]
		public string explModelPath = "BDArmory/Models/explosion/explosion";
		
		[KSPField(isPersistant = false)]
		public string explSoundPath = "BDArmory/Sounds/explode1";
		
		[KSPField(isPersistant = false)]
		public string fireSoundPath = "BDArmory/Parts/50CalTurret/sounds/shot";
		[KSPField(isPersistant = false)]
		public string overheatSoundPath = "BDArmory/Parts/50CalTurret/sounds/turretOverheat";
		[KSPField(isPersistant = false)]
		public string chargeSoundPath = "BDArmory/Parts/laserTest/sounds/charge";
		[KSPField(isPersistant = false)]
		public string deployAnimName = "";
		[KSPField(isPersistant = false)]
		public string fireAnimName = "fireAnim";
		[KSPField(isPersistant = false)]
		public bool hasFireAnimation = true;
		[KSPField(isPersistant = false)]
		public bool spinDownAnimation = false;
		private bool spinningDown = false;
		
		[KSPField(isPersistant = false)]
		public bool oneShotSound = true;
		[KSPField(isPersistant = false)]
		public float soundRepeatTime = 1;

		
		
		[KSPField(isPersistant = false)]
		public string yawTransformName = "aimRotate";
		[KSPField(isPersistant = false)]
		public string pitchTransformName = "aimPitch";
		[KSPField(isPersistant = false)]
		public float tracerLength = 0;

		[KSPField(isPersistant = false)]
		public bool showReloadMeter = false;

		[KSPField(isPersistant = false)]
		public string reloadAudioPath = string.Empty;
		AudioClip reloadAudioClip;
		[KSPField(isPersistant = false)]
		public string reloadCompletePath = string.Empty;
		AudioClip reloadCompleteAudioClip;

		[KSPField(isPersistant = false)]
		public bool moveChildren = false;

		//Used for scaling laser damage down based on distance.
		[KSPField(isPersistant = false)]
		public float tanAngle = 0.0001f;
		//Angle of divergeance/2. Theoretical minimum value calculated using θ = (1.22 L/RL)/2, 
		//where L is laser's wavelength and RL is the radius of the mirror (=gun).
		
		AudioClip fireSound;
		AudioClip overheatSound;
		AudioClip chargeSound;
		AudioSource audioSource;
		AudioSource audioSource2;
		
		private bool turretZeroed = true;  //for knowing if the turret is ready to retract
		private bool inTurretRange = false; //for cutting off fire when target out of FOV
		private float targetDistance = 0;
		private Vector3 targetPosition;
		private Vessel targetVessel;
		private Vector3 targetPrevVel;
		private Part hitPart;
		//private GameObject bullet;
		//private GameObject shell;
		
		private int numberOfGuns = 0;
		
		private float muzzleFlashVelocity = 4;
		
		private VInfoBox heatGauge = null;

		private VInfoBox reloadBar = null;
		
		private bool wasFiring = false;

		public float maxAutoFireAngle = 2;
		
		//aimer textures
		Vector3 pointingAtPosition;
		Vector3 bulletPrediction;
		Texture2D grayCircle;
		Texture2D greenCircle;
		Vector3 fixedLeadOffset = Vector3.zero;
		float targetLeadDistance = 0;
		
		//debug linerenderer
		LineRenderer lineRenderer;
		
		
		//gapless particles
		List<BDAGaplessParticleEmitter> gaplessEmitters = new List<BDAGaplessParticleEmitter>();

		[KSPField(isPersistant = false)]
		public bool airDetonation = false;

		[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Default Detonation Range"),
		 UI_FloatRange(minValue = 500, maxValue = 3500f, stepIncrement = 1f, scene = UI_Scene.All)]
		public float defaultDetonationRange = 3500;

		float detonationRange = 2000;

		
		
		[KSPAction("Toggle Turret")]
		public void AGToggle(KSPActionParam param)
		{
			toggle ();
		}
		
		[KSPEvent(guiActive = true, guiName = "Toggle Turret", active = true)]
		public void toggle()
		{
			turretEnabled = !turretEnabled;	
			guardMode = false;
			if(turretEnabled)
			{
				turretZeroed = false;
			}
			else
			{
				Screen.showCursor = true;
			}
		}
		
		
		
		
		
		
		public override void OnStart (PartModule.StartState state)
		{
			
			SetupTweakables();
      // Git Issue #39  StageIcon object is missing due to changes in KSP 1.1.  added create to replace missing icon.
      if (part.stackIcon.StageIcon == null) part.stackIcon.CreateIcon();

      foreach (var pe in part.FindModelComponents<KSPParticleEmitter>())
			{
				if(pe.useWorldSpace)	
				{
					BDAGaplessParticleEmitter gpe = pe.gameObject.AddComponent<BDAGaplessParticleEmitter>();	
					gpe.part = part;
					gaplessEmitters.Add(gpe);
				}
			}
			
			projectileColorC = Misc.ParseColor255(projectileColor);
			startColorC = Misc.ParseColor255(startColor);
			
			//debug linerenderer
			lineRenderer = gameObject.GetComponent<LineRenderer>();
			if(!lineRenderer)
			{
				lineRenderer = gameObject.AddComponent<LineRenderer>();
			}
			lineRenderer.SetVertexCount(2);
			lineRenderer.SetPosition(0, transform.position);
			lineRenderer.SetPosition(1, transform.position);
			lineRenderer.SetWidth(0.2f, 0.2f);
			
			grayCircle = GameDatabase.Instance.GetTexture("BDArmory/Textures/grayCircle", false);
			greenCircle = GameDatabase.Instance.GetTexture("BDArmory/Textures/greenCircle", false);
			
			targetPosition = Vector3.zero;
			pointingAtPosition = Vector3.zero;
			bulletPrediction = Vector3.zero;
			
			
			if(deployAnimName!="")
			{
				deployStates = SetUpAnimation(deployAnimName, this.part);
			}
			if(hasFireAnimation)
			{
				fireStates = SetUpAnimation (fireAnimName, this.part);
				foreach(AnimationState anim in fireStates)
				{
					anim.enabled = false;	
				}
			}
			
			foreach(Transform mtf in part.FindModelTransforms("muzzleTransform"))
			{
				KSPParticleEmitter pEmitter = mtf.gameObject.GetComponent<KSPParticleEmitter>();
				muzzleFlashVelocity = pEmitter.worldVelocity.z;	
			}
			
			
			
			
			pitchTransform = part.FindModelTransform(pitchTransformName);
			yawTransform = part.FindModelTransform(yawTransformName);
			referenceTransform = yawTransform.parent;
			yawAxis = new Vector3(0,0,1);
			pitchAxis = new Vector3(0,-1,0);
			hitPart = null;
			
			fireSound = GameDatabase.Instance.GetAudioClip(fireSoundPath);
			overheatSound = GameDatabase.Instance.GetAudioClip(overheatSoundPath);
			audioSource = gameObject.AddComponent<AudioSource>();
			audioSource.bypassListenerEffects = true;
			audioSource.minDistance = .3f;
			audioSource.maxDistance = 1000;
			audioSource.priority = 10;
			
			audioSource2 = gameObject.AddComponent<AudioSource>();
			audioSource2.bypassListenerEffects = true;
			audioSource2.minDistance = .3f;
			audioSource2.maxDistance = 1000;
			audioSource2.dopplerLevel = 0;
			audioSource2.priority = 10;

			if(reloadAudioPath != string.Empty)
			{
				reloadAudioClip = (AudioClip) GameDatabase.Instance.GetAudioClip(reloadAudioPath);
			}
			if(reloadCompletePath != string.Empty)
			{
				reloadCompleteAudioClip = (AudioClip) GameDatabase.Instance.GetAudioClip(reloadCompletePath);
			}

			if(weaponType == "laser")
			{
				chargeSound = GameDatabase.Instance.GetAudioClip(chargeSoundPath);
				if(HighLogic.LoadedSceneIsFlight)
				{
					audioSource.clip = fireSound;
				}
				foreach(Transform tf in part.FindModelTransforms("fireTransform"))
				{
					LineRenderer lr = tf.gameObject.AddComponent<LineRenderer>();
					Color laserColor = Misc.ParseColor255(projectileColor);
					laserColor.a = laserColor.a/2;
					lr.material = new Material(Shader.Find ("KSP/Particles/Alpha Blended"));
					lr.material.SetColor("_TintColor", laserColor);
					lr.material.mainTexture = GameDatabase.Instance.GetTexture("BDArmory/Textures/laser", false);
					lr.castShadows = false;
					lr.receiveShadows = false;
					lr.SetWidth(tracerStartWidth, tracerEndWidth);
					lr.SetVertexCount(2);
					lr.SetPosition(0, tf.position);
					lr.SetPosition(1, tf.position);
				}
				
			}
			
			//if(moveChildren) AttachChildren();
			
			part.force_activate();
			
		}
		
		void Update()
		{
			if(HighLogic.LoadedSceneIsEditor && ((maxPitch - minPitch < 0.1f) || yawRange < 0.1f) && onlyFireInRange)	
			{
				onlyFireInRange = false;
				Misc.RefreshAssociatedWindows(part);
			}
		}
		
		
		public override void OnFixedUpdate()
		{
			
			if(!vessel.IsControllable)
			{
				turretEnabled = false;
			}
			
			//laser
			if(weaponType == "laser")
			{
				foreach(Transform tf in part.FindModelTransforms("fireTransform"))
				{
					LineRenderer lr = tf.gameObject.GetComponent<LineRenderer>();
					lr.SetPosition(0, tf.position);
					lr.SetPosition(1, tf.position);
				}
			}

			if(showReloadMeter)
			{
				if(Time.time-timeCheck < (60/roundsPerMinute) && Time.time-timeCheck > 0.1f)
				{
					if(reloadBar == null)
					{
						reloadBar = InitReloadBar(part);
						if(reloadAudioClip)
						{
							audioSource.PlayOneShot(reloadAudioClip);
						}
					}
					reloadBar.SetValue(Time.time-timeCheck, 0, 60/roundsPerMinute);
				}
				else if(reloadBar != null)
				{
					part.stackIcon.ClearInfoBoxes();
					reloadBar = null;
					if(reloadCompleteAudioClip)
					{
						audioSource.PlayOneShot(reloadCompleteAudioClip);
					}

				}
			}
			else
			{
				//heat
				if(heat > maxHeat/3)
				{
					if(heatGauge == null)
					{
						heatGauge = InitHeatGauge(part);
					}
					heatGauge.SetValue(heat, maxHeat/3, maxHeat);
				}
				else if(heatGauge != null && heat < maxHeat/4)
				{
					part.stackIcon.ClearInfoBoxes();
					heatGauge = null;
				}
			}
			
			heat = Mathf.Clamp(heat - heatLoss * TimeWarp.fixedDeltaTime, 0, Mathf.Infinity);
			if(heat>maxHeat && !isOverheated)
			{
				isOverheated = true;
				autoFire = false;
				autoFireTarget = null;
				audioSource.Stop ();
				wasFiring = false;
				audioSource2.volume = Mathf.Sqrt (GameSettings.SHIP_VOLUME);
				audioSource2.PlayOneShot(overheatSound);
			}
			if(heat < maxHeat/3 && isOverheated) //reset on cooldown
			{
				isOverheated = false;
				heat = 0;
			}
			//
			
			
			
			//finding number of guns for fire volume reduction  
			//TODO: make central counter so this check doesn't happen every frame
			numberOfGuns = 0;
			foreach(BahaTurret bt in vessel.FindPartModulesImplementing<BahaTurret>())
			{
				if(bt.deployed)
				{
					numberOfGuns++;	
				}
			}	
			if(numberOfGuns<1)
			{
				numberOfGuns = 1;	
			}
			//

			if(!turretEnabled && !turretZeroed)
			{
				deployed = false;
				ReturnTurret();
			}
			
			//animation handling
			if(deployAnimName!="")
			{
				foreach(AnimationState anim in deployStates)
				{
					//animation clamping
					if(anim.normalizedTime>1)
					{
						anim.speed = 0;
						anim.normalizedTime = 1;
					}
					if(anim.normalizedTime < 0)
					{
						anim.speed = 0;
						anim.normalizedTime = 0;
					}
					
					//deploying
					if(turretEnabled)
					{
						if(anim.normalizedTime<1 && anim.speed<1)
						{
							anim.speed = 1;	
							deployed = false;
						}
						if(anim.normalizedTime == 1)
						{
							deployed = true;
							anim.enabled = false;
						}
					}
					
					//retracting
					if(!turretEnabled)
					{
						if(turretZeroed)
						{
							anim.enabled = true;
							if(anim.normalizedTime > 0 && anim.speed > -1)
							{
								anim.speed = -1;	
							}
						}
					}
				}
			}
			else
			{
				deployed = turretEnabled;	
			}
			//aim+shooting
			if(deployed && (TimeWarp.WarpMode!=TimeWarp.Modes.HIGH || TimeWarp.CurrentRate == 1))
			{
				Aim ();	
				CheckTarget ();

				bool userFiring = (Input.GetKey(BDArmorySettings.FIRE_KEY) && (vessel.isActiveVessel || BDArmorySettings.REMOTE_SHOOTING) && !MapView.MapIsEnabled && !guardMode);
				
				if((userFiring || autoFire) && inTurretRange)
				{
					if(weaponType == "ballistic" || weaponType == "cannon") Fire ();
					else if(weaponType == "laser")
					{
						if(!FireLaser ())
						{
							audioSource.Stop ();	
						}
					}
				}
				else
				{
					if(spinDownAnimation) spinningDown = true;
					if(weaponType == "laser") audioSource.Stop ();
					if(!oneShotSound && wasFiring)
						{
							audioSource.Stop ();
							wasFiring = false;
							audioSource2.volume = Mathf.Sqrt (GameSettings.SHIP_VOLUME);
							audioSource2.PlayOneShot(overheatSound);	
						}
					
				}
				
				if(spinningDown && spinDownAnimation)
				{
					foreach(AnimationState anim in fireStates)
					{
						if(anim.normalizedTime>1) anim.normalizedTime = 0;
						anim.speed = fireAnimSpeed;
						fireAnimSpeed = Mathf.Lerp(fireAnimSpeed, 0, 0.04f);
					}
				}
				
					

			}
			else
			{
				audioSource.Stop ();
				autoFire = false;
				autoFireTarget = null;
			}
			
			if(autoFire && Time.time-autoFireTimer > autoFireLength)
			{
				autoFire = false;
				autoFireTarget = null;
			}
			
			if(!oneShotSound &&  !Input.GetKey(BDArmorySettings.FIRE_KEY) && !autoFire && wasFiring)
			{
				wasFiring = false;
				audioSource.Stop ();
				audioSource2.volume = Mathf.Sqrt(GameSettings.SHIP_VOLUME);
				audioSource2.PlayOneShot(overheatSound);
			}
			
			
	
			
		}
		
		
		private void Aim()
		{
			inTurretRange = true;
			
			Vector3 target;
			Vector3 targetYawOffset;
			Vector3 targetPitchOffset;
			
			targetVessel = null;
			if(vessel.targetObject!=null)
			{
                targetVessel = vessel.targetObject.GetVessel();
			}
			
			//auto target tracking
			if(guardMode)
			{
				if(autoFireTarget)
				{
					target = autoFireTarget.CoM;	

					targetVessel = autoFireTarget;
					
					target += targetVessel.rb_velocity * Time.fixedDeltaTime;
				}
				else
				{
					target = Vector3.zero;
					autoFire = false;
					return;
				}
			}
			else
			{
				if(autoLockCapable && targetVessel != null)
				{
					target = targetVessel.CoM + targetVessel.rb_velocity * Time.fixedDeltaTime;
				}
				else
				{
					//mouse aiming
					Vector3 mouseAim = new Vector3(Input.mousePosition.x/Screen.width, Input.mousePosition.y/Screen.height, 0);
					Ray ray = FlightCamera.fetch.mainCamera.ViewportPointToRay(mouseAim);
					RaycastHit hit;
					if(Physics.Raycast(ray, out hit, maxTargetingRange, 557057))
					{
						target = hit.point;
						try{
							Part p = Part.FromGO(hit.rigidbody.gameObject);
							if(p.vessel == this.vessel)
							{
								target = ray.direction * maxTargetingRange + FlightCamera.fetch.mainCamera.transform.position;		
							}
							
						}catch(NullReferenceException){}
						
					}
					else
					{
						target = (ray.direction * maxTargetingRange) + FlightCamera.fetch.mainCamera.transform.position;	
						if(targetVessel!=null && targetVessel.loaded)
						{
							target = ray.direction * Vector3.Distance(targetVessel.transform.position, FlightCamera.fetch.mainCamera.transform.position) + FlightCamera.fetch.mainCamera.transform.position;	
						}
					}
				}
			}
			//

			//airDetonation
			detonationRange = defaultDetonationRange;



			targetDistance = Vector3.Distance(target, transform.position);
			//target leading
			if((BDArmorySettings.AIM_ASSIST || guardMode) && weaponType != "laser")
			{
				float gAccel = (float) FlightGlobals.getGeeForceAtPosition(target).magnitude;
				float time = targetDistance/(bulletVelocity);
				Vector3 originalTarget = target;
				
				if(targetVessel!=null && targetVessel.loaded)
				{
					//Vector3 acceleration = (targetVessel.rigidbody.velocity - targetPrevVel)/Time.fixedDeltaTime;
					Vector3 acceleration = targetVessel.acceleration;
					float time2 = VectorUtils.CalculateLeadTime(target-transform.position, targetVessel.rb.velocity-rb.velocity, bulletVelocity);
					if(time2 > 0) time = time2;
					target += (targetVessel.rb.velocity-rb.velocity) * time; //target vessel relative velocity compensation
					target += (0.5f * acceleration * time * time); //target acceleration
					targetPrevVel = targetVessel.rb.velocity;
					
				}
				else if(vessel.altitude < 5000)
				{
					float time2 = VectorUtils.CalculateLeadTime(target-transform.position, Vector3.zero-rb.velocity, bulletVelocity);
					if(time2 > 0) time = time2;
					target += (-rb.velocity*(time+Time.fixedDeltaTime));  //this vessel velocity compensation against stationary
				}
				if(bulletDrop && FlightGlobals.RefFrameIsRotating) target += (0.5f*gAccel*time*time * FlightGlobals.getUpAxis());  //gravity compensation
				
				targetLeadDistance = Vector3.Distance(target, transform.position);
				fixedLeadOffset = originalTarget-pointingAtPosition;
				
				if(yawRange == 0)
				{
					fixedLeadOffset = originalTarget-target; //for aiming fixed guns to moving target	
				}

				//airdetonation
				if(targetVessel!=null)
				{
					detonationRange = Mathf.Clamp(Vector3.Distance(transform.position, target), 500, 3500) - 50f;
				}
				
			}

			detonationRange *= UnityEngine.Random.Range(0.95f, 1.05f);
			
			targetYawOffset = yawTransform.position - target;
			targetYawOffset = Quaternion.Inverse(yawTransform.rotation) * targetYawOffset; //sets offset relative to the turret's rotation
			targetYawOffset = Quaternion.AngleAxis(90, yawAxis) * targetYawOffset; //fix difference in coordinate system.
			
			targetPitchOffset = pitchTransform.position - target;
			targetPitchOffset = Quaternion.Inverse(pitchTransform.rotation) * targetPitchOffset;
			targetPitchOffset = Quaternion.AngleAxis(180, pitchAxis) * targetPitchOffset;
			
			
			float rotationSpeedYaw = Mathf.Clamp (Mathf.Abs (10*targetYawOffset.x/Mathf.Clamp (targetDistance,10, maxTargetingRange)), 0, rotationSpeed);
			float rotationSpeedPitch = Mathf.Clamp (Mathf.Abs (10*targetPitchOffset.z/Mathf.Clamp (targetDistance,10, maxTargetingRange)), 0, rotationSpeed);
			
			//lerp-less rotation TEST
			//(todo) 
			
			targetPosition = target;
			
			if(TimeWarp.WarpMode!=TimeWarp.Modes.HIGH || TimeWarp.CurrentRate == 1)
			{
				//yaw movement
				if(yawRange >= 0 && yawRange < 360)
				{
					
					float minYaw = -(yawRange/2);
					float maxYaw = yawRange/2;
					if(targetYawOffset.x > 0 && yawTransform.localRotation.Roll () < maxYaw*Mathf.Deg2Rad)
					{
						yawTransform.localRotation *= Quaternion.AngleAxis(rotationSpeedYaw, yawAxis);
						/*
						if(moveChildren)
						{
							
						}
						*/
					}
					else if(targetYawOffset.x < 0 && yawTransform.localRotation.Roll () > minYaw*Mathf.Deg2Rad)
					{
						yawTransform.localRotation *= Quaternion.AngleAxis(-rotationSpeedYaw, yawAxis);
						/*
						if(moveChildren)
						{
							
						}
						*/
						 
					}
					else
					{
						if(onlyFireInRange)
						{
							inTurretRange = false;	//don't fire because turret can't reach target.
						}
					}
					
				}
				else{
					if(targetYawOffset.x > 0)
					{
						yawTransform.localRotation *= Quaternion.AngleAxis(rotationSpeedYaw, yawAxis);
					}
					if(targetYawOffset.x < 0)
					{
						yawTransform.localRotation *= Quaternion.AngleAxis(-rotationSpeedYaw, yawAxis);
					}
				}
				
				
			
			
				//pitch movement
				if(targetPitchOffset.z > 0 && pitchTransform.localRotation.Yaw ()>-maxPitch*Mathf.Deg2Rad)
				{
					pitchTransform.localRotation *= Quaternion.AngleAxis(rotationSpeedPitch, pitchAxis);
				}
				else if(targetPitchOffset.z < 0 && pitchTransform.localRotation.Yaw () < - minPitch*Mathf.Deg2Rad)
				{
					pitchTransform.localRotation *= Quaternion.AngleAxis(-rotationSpeedPitch, pitchAxis);
				}
				else
				{
					if(onlyFireInRange)
					{
						inTurretRange = false;	//don't fire because turret can't reach target.
					}
				}
			}
			
			if(autoFireTarget != null && guardMode)
			{
				Transform fireTransform = part.FindModelTransform("fireTransform");
				Vector3 targetDirection = targetPosition-fireTransform.position;
				Vector3 aimDirection = fireTransform.forward;
				if(Vector3.Angle(aimDirection, targetDirection) < maxAutoFireAngle)
				{
					autoFire = true;
				}
				else
				{
					autoFire = false;
				}
			}
			else
			{	
				autoFire = false;
			}
		}
		
		//returns turret to default position when turned off
		private void ReturnTurret()
		{
			if(!autoReturnTurret)
			{
				turretZeroed = true;
				return;
			}

			//Debug.Log ("Returning turret");
			float returnSpeed = Mathf.Clamp (rotationSpeed, 0.1f, 6f);
			bool yawReturned = false;
			bool pitchReturned = false;
			turretZeroed = false;
			float yaw = yawTransform.localRotation.Roll() * Mathf.Rad2Deg;
			float pitch = pitchTransform.localRotation.Yaw () * Mathf.Rad2Deg;
			
			
			if(yaw > 1 || yaw < -1)
			{
				if(yaw > 0)
				{
					yawTransform.localRotation *= Quaternion.AngleAxis(-Mathf.Clamp(Mathf.Abs (yaw)/2, 0.01f, returnSpeed), yawAxis);
				}
				if(yaw < 0)
				{
					yawTransform.localRotation *= Quaternion.AngleAxis(Mathf.Clamp(Mathf.Abs (yaw)/2, 0.01f, returnSpeed), yawAxis);
				}
			}
			else
			{
				yawReturned = true;
			}
			
			if(pitch > 1 || pitch < -1)
			{
				if(pitch > 0)
				{
					pitchTransform.localRotation *= Quaternion.AngleAxis(Mathf.Clamp(Mathf.Abs (pitch)/2, 0.01f, returnSpeed), pitchAxis);
				}
				if(pitch < 0)
				{
					pitchTransform.localRotation *= Quaternion.AngleAxis(-Mathf.Clamp(Mathf.Abs (pitch)/2, 0.01f, returnSpeed), pitchAxis);
				}
			}
			else
			{
				pitchReturned = true;
			}
			if(yawReturned && pitchReturned)
			{
				yawTransform.localRotation = Quaternion.Euler(0,0,0);
				pitchTransform.localRotation = Quaternion.Euler(0,0,0);
				turretZeroed = true;
			}
			
		}
		
		
		private void Fire()
		{
			float timeGap = (60/roundsPerMinute) * TimeWarp.CurrentRate;
			
			if(Time.time-timeCheck > timeGap && !isOverheated)
			{
				bool effectsShot = false;
				Transform[] fireTransforms = part.FindModelTransforms("fireTransform");
				foreach(Transform tf in fireTransforms)
				{
					if(!CheckMouseIsOnGui() && WMgrAuthorized() && (BDArmorySettings.INFINITE_AMMO || part.RequestResource(ammoName, requestResourceAmount)>0))
					{
						spinningDown = false;
						
						float randomY = UnityEngine.Random.Range(-20f, 20f)/accuracy;
						float randomZ = UnityEngine.Random.Range(-20f, 20f)/accuracy;
						
						//recoil
						if(hasRecoil)
						{
							gameObject.rigidbody.AddForce (pitchTransform.rotation*new Vector3(-bulletVelocity, -randomY, -randomZ).normalized*(bulletVelocity*bulletMass), ForceMode.Impulse);
						}
						
						if(!effectsShot)
						{
							//sound
							if(oneShotSound)
							{
								audioSource.dopplerLevel = 0;
								audioSource.bypassListenerEffects = true;
								audioSource.volume = 1*(Mathf.Sqrt(GameSettings.SHIP_VOLUME))/Mathf.Sqrt(numberOfGuns);
								audioSource.PlayOneShot(fireSound);
							}
							else
							{
								wasFiring = true;
								if(!audioSource.isPlaying)
								{
									audioSource.clip = fireSound;
									audioSource.dopplerLevel = 0;
									audioSource.bypassListenerEffects = true;
									audioSource.volume = 1*(Mathf.Sqrt(GameSettings.SHIP_VOLUME))/Mathf.Sqrt(numberOfGuns);
									audioSource.loop = false;
									audioSource.Play();	
								}
								else
								{
									if (audioSource.time >= fireSound.length)
									{
										audioSource.time = soundRepeatTime;	
									}
								}
							}
							
							//animation
							if(hasFireAnimation)
							{
								foreach(AnimationState anim in fireStates)
								{
									fireAnimSpeed = Mathf.Clamp ((roundsPerMinute*anim.length)/60, 1, 5);
									anim.enabled = true;
									anim.normalizedTime = 0;
									anim.speed = fireAnimSpeed;
								}
							}
							
							//muzzle flash
							foreach(Transform mtf in part.FindModelTransforms("muzzleTransform"))
							{
								KSPParticleEmitter pEmitter = mtf.gameObject.GetComponent<KSPParticleEmitter>();
								if(!pEmitter.useWorldSpace)
								{
									pEmitter.worldVelocity = mtf.rotation * new Vector3(0,0,muzzleFlashVelocity);
									pEmitter.EmitParticle();
								}
							}
							foreach(var gpe in gaplessEmitters)
							{
								gpe.EmitParticles();	
							}
							
							//shell ejection
							if(BDArmorySettings.EJECT_SHELLS)
							{
								foreach(Transform sTf in part.FindModelTransforms("shellEject"))
								{
									GameObject ejectedShell = (GameObject) Instantiate(GameDatabase.Instance.GetModel("BDArmory/Models/shell/model"), sTf.position + rb.velocity*(Time.fixedDeltaTime), sTf.rotation);
									ejectedShell.SetActive(true);
									ejectedShell.transform.localScale = Vector3.one * shellScale;
									ShellCasing shellComponent = ejectedShell.AddComponent<ShellCasing>();
									shellComponent.initialV = rb.velocity;
									
								}
							}
							effectsShot = true;
						}
						
						
						//firing bullet
						Transform fireTransform = part.FindModelTransform("fireTransform");
						Vector3 aimDirection = fireTransform.forward;
						
						
						//GameObject firedBullet = GameObject.Instantiate(bullet, tf.position, tf.rotation) as GameObject;
						GameObject firedBullet = new GameObject("bullet");
						firedBullet.transform.position = tf.position;
						firedBullet.transform.rotation = tf.rotation;
						firedBullet.AddComponent<Rigidbody>();
						firedBullet.rigidbody.mass = bulletMass;


						Vector3 firedVelocity = fireTransform.rotation * new Vector3(randomZ,randomY,bulletVelocity).normalized * bulletVelocity;
						
						if(targetVessel!=null && targetVessel.loaded && (autoLockCapable || guardMode))
						{
							Vector3 targetDirection = targetPosition-fireTransform.position;
							
							if(Vector3.Angle(aimDirection, targetDirection) < 3f)
							{
								firedVelocity = Quaternion.LookRotation(targetDirection) * new Vector3(randomZ,randomY,bulletVelocity).normalized * bulletVelocity;
							}
						}
						firedBullet.transform.position -= firedVelocity * Time.fixedDeltaTime;
						firedBullet.transform.position += rb.velocity * Time.fixedDeltaTime;
						firedBullet.rigidbody.AddForce(this.rb.velocity + firedVelocity, ForceMode.VelocityChange);
						if(weaponType != "laser")
						{
							BahaTurretBullet bulletScript = firedBullet.AddComponent<BahaTurretBullet>();
							bulletScript.initialSpeed = bulletVelocity;
							bulletScript.sourceVessel = this.vessel;
							bulletScript.bulletTexturePath = bulletTexturePath;
							bulletScript.projectileColor = projectileColorC;
							bulletScript.startColor = startColorC;
							bulletScript.fadeColor = fadeColor;
							bulletScript.tracerStartWidth = tracerStartWidth;
							bulletScript.tracerEndWidth = tracerEndWidth;
							bulletScript.tracerLength = tracerLength;
							bulletScript.bulletDrop = bulletDrop;

							if(weaponType == "cannon")
							{
								bulletScript.bulletType = BahaTurretBullet.BulletTypes.Explosive;
								bulletScript.explModelPath = explModelPath;
								bulletScript.explSoundPath = explSoundPath;
								bulletScript.blastPower = cannonShellPower;
								bulletScript.radius = cannonShellRadius;
								bulletScript.airDetonation = airDetonation;
								bulletScript.detonationRange = detonationRange;
							}
						}

						//heat
						heat += heatPerShot;
					}
					else
					{
						spinningDown = true;
						if(!oneShotSound && wasFiring)
						{
							audioSource.Stop ();
							wasFiring = false;
							audioSource2.volume = Mathf.Sqrt (GameSettings.SHIP_VOLUME);
							audioSource2.PlayOneShot(overheatSound);	
						}
					}
				}
				
				
						
					
				
				
				timeCheck = Time.time;
			}
			else
			{
				spinningDown = true;	
			}
		}
		
		
		private bool FireLaser()
		{
			float maxDistance = BDArmorySettings.PHYSICS_RANGE;
			if(BDArmorySettings.PHYSICS_RANGE == 0) maxDistance = 2500;

			float chargeAmount = requestResourceAmount * TimeWarp.fixedDeltaTime;
			if(!CheckMouseIsOnGui() && WMgrAuthorized() && inTurretRange && !isOverheated && (part.RequestResource(ammoName, chargeAmount)>=chargeAmount || BDArmorySettings.INFINITE_AMMO))
			{
				if(!audioSource.isPlaying)
				{
					audioSource.PlayOneShot (chargeSound);
					audioSource.Play();
					audioSource.loop = true;
					
				}
				foreach(Transform tf in part.FindModelTransforms("fireTransform"))
				{
					
					LineRenderer lr = tf.gameObject.GetComponent<LineRenderer>();
					lr.SetPosition(0, tf.position + (rb.velocity*Time.fixedDeltaTime));

					Vector3 rayDirection = tf.forward;
					
					Vector3 targetDirection = Vector3.zero;  //autoTrack enhancer
					Vector3 targetDirectionLR = tf.forward;
					Vector3 physStepFix = Vector3.zero;
					if(targetVessel!=null && targetVessel.loaded)
					{
						targetDirection = (targetVessel.CoM+(targetVessel.rb.velocity*Time.fixedDeltaTime)) - tf.position;
						physStepFix = targetVessel.rb.velocity*Time.fixedDeltaTime;
						if(autoLockCapable && Vector3.Angle(rayDirection, targetDirection) < 3)
						{
							rayDirection = targetDirection;
							targetDirectionLR = (targetVessel.CoM+(2*targetVessel.rb.velocity*Time.fixedDeltaTime)) - tf.position;
						}
					}
					
					Ray ray = new Ray(tf.position, rayDirection);
					RaycastHit hit;
					if(Physics.Raycast(ray, out hit, maxDistance, 557057))
					{
						lr.SetPosition(1, hit.point + (physStepFix));
						if(Time.time-timeCheck > 60/1200 && BDArmorySettings.BULLET_HITS)
						{
							BulletHitFX.CreateBulletHit(hit.point, hit.normal, false);	
						}
						try
						{
							Part p = Part.FromGO(hit.rigidbody.gameObject);
							if(p.vessel!=this.vessel)
							{
								float distance = hit.distance;
								p.temperature += laserDamage/(float)(Math.PI*Math.Pow(tanAngle*distance,2))*TimeWarp.fixedDeltaTime; //distance modifier: 1/(PI*Pow(Dist*tan(angle),

								if(BDArmorySettings.INSTAKILL) p.temperature += p.maxTemp;
								
							}
						}
						catch(NullReferenceException){}
					
					}
					else
					{
						lr.SetPosition(1, (targetDirectionLR*maxDistance)+tf.position);
					}
				}
				heat += heatPerShot * TimeWarp.CurrentRate;
				
				
				return true;
			}
			else
			{
				return false;
			}
		}
		
		
		//checks if you're about to shoot yourself (prevents it)
		private void CheckTarget()
		{
			foreach(var fireTr in part.FindModelTransforms("fireTransform"))
			{
				
				Ray ray = new Ray(fireTr.position, fireTr.forward);
				RaycastHit hit;
				if(Physics.Raycast(ray, out hit, maxTargetingRange, 557057))
				{
					pointingAtPosition = hit.point;
					try{
						Part p = Part.FromGO(hit.rigidbody.gameObject);
						hitPart = p;
						if(p.vessel == this.vessel)
						{
							inTurretRange = false;
						}
					}
					catch(NullReferenceException)
					{}
				}
				else
				{
					hitPart = null;
					pointingAtPosition = fireTr.position + (ray.direction * (maxTargetingRange));
				}


				if(targetVessel!=null && targetVessel.loaded) pointingAtPosition = fireTr.transform.position + (ray.direction * targetLeadDistance);
			}
			
			
			//trajectory simulation
			if(BDArmorySettings.AIM_ASSIST && BDArmorySettings.DRAW_AIMERS)
			{
				Transform fireTransform = part.FindModelTransform("fireTransform");
				if(weaponType != "laser")
				{
					float simDeltaTime = 0.15f;
					

					Vector3 simVelocity = rb.velocity+(bulletVelocity*fireTransform.forward);
					Vector3 simCurrPos = fireTransform.position + (rb.velocity*Time.fixedDeltaTime);
					Vector3 simPrevPos = fireTransform.position + (rb.velocity*Time.fixedDeltaTime);
					Vector3 simStartPos = fireTransform.position + (rb.velocity*Time.fixedDeltaTime);
					bool simulating = true;
					
					List<Vector3> pointPositions = new List<Vector3>();
					pointPositions.Add(simCurrPos);
					
					while(simulating)
					{
						
						RaycastHit hit;
						if(bulletDrop) simVelocity += FlightGlobals.getGeeForceAtPosition(simCurrPos) * simDeltaTime;
						simCurrPos += simVelocity * simDeltaTime;
						pointPositions.Add(simCurrPos);
						if(Physics.Raycast(simPrevPos,simCurrPos-simPrevPos, out hit, Vector3.Distance(simPrevPos,simCurrPos), 557057))
						{
							Vessel hitVessel = null;
							try
							{
								hitVessel = Part.FromGO(hit.rigidbody.gameObject).vessel;	
							}
							catch(NullReferenceException){}
							
							if(hitVessel==null || (hitVessel!=null && hitVessel != vessel))
							{
								bulletPrediction = hit.point;
								simulating = false;
							}
							
						}
						
						
						simPrevPos = simCurrPos;
						
						if(targetVessel!=null && targetVessel.loaded && !targetVessel.Landed && Vector3.Distance(simStartPos,simCurrPos) > targetLeadDistance)
						{
							bulletPrediction = simStartPos + (simCurrPos-simStartPos).normalized*targetLeadDistance;
							simulating = false;
						}
						
						if(Vector3.Distance(simStartPos,simCurrPos) > maxTargetingRange)
						{
							bulletPrediction = simStartPos + (simCurrPos-simStartPos).normalized*maxTargetingRange/4;
							simulating = false;
						}
					}
					
					
					if(BDArmorySettings.DRAW_DEBUG_LINES && BDArmorySettings.DRAW_AIMERS)
					{
						Vector3[] pointsArray = pointPositions.ToArray();
						if(gameObject.GetComponent<LineRenderer>()==null)
						{
							LineRenderer lr = gameObject.AddComponent<LineRenderer>();
							lr.SetWidth(.1f, .1f);
							lr.SetVertexCount(pointsArray.Length);
							for(int i = 0; i<pointsArray.Length; i++)
							{
								lr.SetPosition(i, pointsArray[i]);	
							}
						}
						else
						{
							LineRenderer lr = gameObject.GetComponent<LineRenderer>();
							lr.enabled = true;
							lr.SetVertexCount(pointsArray.Length);
							for(int i = 0; i<pointsArray.Length; i++)
							{
								lr.SetPosition(i, pointsArray[i]);	
							}	
						}
					}
					else
					{
						/*
						if(gameObject.GetComponent<LineRenderer>()!=null)
						{
							gameObject.GetComponent<LineRenderer>().enabled = false;	
						}

					*/
					}
					
				}
				else
				{
					Ray ray = new Ray(fireTransform.position, fireTransform.forward);
					RaycastHit rayHit;
					if(Physics.Raycast(ray, out rayHit,  maxTargetingRange, 557057))
					{
						bulletPrediction = rayHit.point;
					}
					else
					{
						bulletPrediction = ray.GetPoint(maxTargetingRange);
					}

					pointingAtPosition = ray.GetPoint(maxTargetingRange);
				}
			}
			
		}
		
		//overheat gauge
		public VInfoBox InitHeatGauge(Part p)  //thanks DYJ
        {
            VInfoBox v = p.stackIcon.DisplayInfo();

            v.SetMsgBgColor(XKCDColors.DarkRed);
            v.SetMsgTextColor(XKCDColors.Orange);
            v.SetMessage("Overheat");
            v.SetProgressBarBgColor(XKCDColors.DarkRed);
            v.SetProgressBarColor(XKCDColors.Orange);

            return v;
        }

		public VInfoBox InitReloadBar(Part p)
		{
			VInfoBox v = p.stackIcon.DisplayInfo();

			v.SetMsgBgColor(XKCDColors.DarkGrey);
			v.SetMsgTextColor(XKCDColors.White);
			v.SetMessage("Reloading");
			v.SetProgressBarBgColor(XKCDColors.DarkGrey);
			v.SetProgressBarColor(XKCDColors.Silver);

			return v;
		}
		
		
		//Animation Setup
		public static AnimationState[] SetUpAnimation(string animationName, Part part)  //Thanks Majiir!
        {
            var states = new List<AnimationState>();
            foreach (var animation in part.FindModelAnimators(animationName))
            {
                var animationState = animation[animationName];
                animationState.speed = 0;
                animationState.enabled = true;
                animationState.wrapMode = WrapMode.ClampForever;
                animation.Blend(animationName);
                states.Add(animationState);
            }
            return states.ToArray();
        }
		
		
		void OnGUI()
		{
			if(deployed && inTurretRange && vessel.isActiveVessel && BDArmorySettings.DRAW_AIMERS && !guardMode & !MapView.MapIsEnabled)
			{
				float size = 30;
				
				Vector3 aimPosition;
				if(BDArmorySettings.AIM_ASSIST && vessel.altitude < 5000)
				{
					aimPosition = FlightCamera.fetch.mainCamera.WorldToViewportPoint(bulletPrediction);
				}
				else
				{
					aimPosition = FlightCamera.fetch.mainCamera.WorldToViewportPoint(pointingAtPosition);
				}

				if(targetVessel!=null && targetVessel.loaded && !targetVessel.Landed)
				{
					aimPosition = FlightCamera.fetch.mainCamera.WorldToViewportPoint(pointingAtPosition+fixedLeadOffset);
				}

				Texture2D texture;
				if(Vector3.Angle(pointingAtPosition-transform.position, targetPosition-transform.position) < 0.3f)
				{
					texture = greenCircle;
				}
				else
				{
					texture = grayCircle;
				}
				Rect drawRect = new Rect(aimPosition.x*Screen.width-(0.5f*size), (1-aimPosition.y)*Screen.height-(0.5f*size), size, size);
				
				float cameraAngle = Vector3.Angle(FlightCamera.fetch.GetCameraTransform().forward, bulletPrediction-FlightCamera.fetch.mainCamera.transform.position);
				if(cameraAngle<90) GUI.DrawTexture(drawRect, texture);
				
				
				
			}
			
			
		}
		

		

		
		bool CheckMouseIsOnGui()
		{
			return Misc.CheckMouseIsOnGui();
		}
		
		bool WMgrAuthorized()
		{
			MissileFire manager = BDArmorySettings.Instance.ActiveWeaponManager;
			if(manager != null)
			{
				if(manager.hasSingleFired) return false;
				else return true;
			}
			else
			{
				return true;	
			}
		}
		
		void SetupTweakables()
		{
			var minPitchRange = (UI_FloatRange) Fields["minPitch"].uiControlEditor;
			if(minPitchLimit > 90)
			{
				minPitchLimit = minPitch;
			}
			if(minPitchLimit == 0)
			{
				Fields["minPitch"].guiActiveEditor = false;	
			}
			minPitchRange.minValue = minPitchLimit;
			minPitchRange.maxValue = 0;
			
			var maxPitchRange = (UI_FloatRange) Fields["maxPitch"].uiControlEditor;
			if(maxPitchLimit > 90)
			{
				maxPitchLimit = maxPitch;
			}
			if(maxPitchLimit == 0)
			{
				Fields["maxPitch"].guiActiveEditor = false;	
			}
			maxPitchRange.maxValue = maxPitchLimit;
			maxPitchRange.minValue = 0;
			
			var yawRangeEd = (UI_FloatRange) Fields["yawRange"].uiControlEditor;
			if(yawRangeLimit > 360)
			{
				yawRangeLimit = yawRange;	
			}

			if(yawRangeLimit == 0)
			{
				Fields["yawRange"].guiActiveEditor = false;
				onlyFireInRange = false;
				Fields["onlyFireInRange"].guiActiveEditor = false;
			}
			else if(yawRangeLimit < 0)
			{
				yawRangeEd.minValue = 0;
				yawRangeEd.maxValue = 360;
				
				if(yawRange < 0) yawRange = 360;
			}
			else
			{
				yawRangeEd.minValue = 0;
				yawRangeEd.maxValue = yawRangeLimit;
			}

			//air detonation slider
			Fields["defaultDetonationRange"].guiActive = airDetonation;
			Fields["defaultDetonationRange"].guiActiveEditor = airDetonation;

		}
		
		
		public Vector3 GetLeadOffset()
		{
			return fixedLeadOffset;
		}
	}
	
	
}

