using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Smooth.Slinq.Test;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BahaTurret
{
    public class MissileFire : PartModule
    {

        #region  Declarations

        //weapons
        private const int LIST_CAPACITY = 100;
        private List<IBDWeapon> weaponTypes = new List<IBDWeapon>(LIST_CAPACITY);
        public IBDWeapon[] weaponArray;

        // extension for feature_engagementenvelope: specific lists by weapon engagement type
        private List<IBDWeapon> weaponTypesAir = new List<IBDWeapon>(LIST_CAPACITY);
        private List<IBDWeapon> weaponTypesMissile = new List<IBDWeapon>(LIST_CAPACITY);
        private List<IBDWeapon> weaponTypesGround = new List<IBDWeapon>(LIST_CAPACITY);
        private List<IBDWeapon> weaponTypesSLW = new List<IBDWeapon>(LIST_CAPACITY);

        [KSPField(guiActiveEditor = false, isPersistant = true, guiActive = false)] public int weaponIndex = 0;

        //ScreenMessage armedMessage;
        ScreenMessage selectionMessage;
        string selectionText = "";

        Transform cameraTransform;

        float startTime;
        int missilesAway = 0;

        public bool hasLoadedRippleData = false;
        float rippleTimer;

        public TargetSignatureData heatTarget;
        //[KSPField(isPersistant = true)]
        public float rippleRPM
        {
            get
            {
                if (selectedWeapon != null)
                {
                    return rippleDictionary[selectedWeapon.GetShortName()].rpm;
                }
                else
                {
                    return 0;
                }
            }
            set
            {
                if (selectedWeapon != null)
                {
                    if (rippleDictionary.ContainsKey(selectedWeapon.GetShortName()))
                    {
                        rippleDictionary[selectedWeapon.GetShortName()].rpm = value;
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    return;
                }
            }
        }

        float triggerTimer = 0;
        int rippleGunCount = 0;
        int _gunRippleIndex = 0;
        public float gunRippleRpm = 0;

        public int gunRippleIndex
        {
            get { return _gunRippleIndex; }
            set
            {
                _gunRippleIndex = value;
                if (_gunRippleIndex >= rippleGunCount)
                {
                    _gunRippleIndex = 0;
                }
            }
        }
        
        //ripple stuff
        string rippleData = string.Empty;
        Dictionary<string, RippleOption> rippleDictionary; //weapon name, ripple option
        public bool canRipple = false;

        //public float triggerHoldTime = 0.3f;

        //[KSPField(isPersistant = true)]

        public bool rippleFire
        {
            get
            {
                if (selectedWeapon != null)
                {
                    if (rippleDictionary.ContainsKey(selectedWeapon.GetShortName()))
                    {
                        return rippleDictionary[selectedWeapon.GetShortName()].rippleFire;
                    }
                    else
                    {
                        //rippleDictionary.Add(selectedWeapon.GetShortName(), new RippleOption(false, 650));
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
        }

        public void ToggleRippleFire()
        {
            if (selectedWeapon != null)
            {
                RippleOption ro;
                if (rippleDictionary.ContainsKey(selectedWeapon.GetShortName()))
                {
                    ro = rippleDictionary[selectedWeapon.GetShortName()];
                }
                else
                {
                    ro = new RippleOption(false, 650); //default to true ripple fire for guns, otherwise, false
                    if (selectedWeapon.GetWeaponClass() == WeaponClasses.Gun)
                    {
                        ro.rippleFire = currentGun.useRippleFire;
                    }
                    rippleDictionary.Add(selectedWeapon.GetShortName(), ro);
                }

                ro.rippleFire = !ro.rippleFire;

                if (selectedWeapon.GetWeaponClass() == WeaponClasses.Gun)
                {
                    foreach (ModuleWeapon w in vessel.FindPartModulesImplementing<ModuleWeapon>())
                    {
                        if (w.GetShortName() == selectedWeapon.GetShortName())
                            w.useRippleFire = ro.rippleFire;
                    }
                }
            }
        }

        public void AGToggleRipple(KSPActionParam param)
        {
            ToggleRippleFire();
        }

        void ParseRippleOptions()
        {
            rippleDictionary = new Dictionary<string, RippleOption>();
            Debug.Log("[BDArmory]: Parsing ripple options");
            if (!string.IsNullOrEmpty(rippleData))
            {
                Debug.Log("[BDArmory]: Ripple data: " + rippleData);
                try
                {
                    foreach (string weapon in rippleData.Split(new char[] {';'}))
                    {
                        if (weapon == string.Empty) continue;

                        string[] options = weapon.Split(new char[] {','});
                        string wpnName = options[0];
                        bool rf = bool.Parse(options[1]);
                        float _rpm = float.Parse(options[2]);
                        RippleOption ro = new RippleOption(rf, _rpm);
                        rippleDictionary.Add(wpnName, ro);
                    }
                }
                catch (IndexOutOfRangeException)
                {
                    Debug.Log("[BDArmory]: Ripple data was invalid.");
                    rippleData = string.Empty;
                }
            }
            else
            {
                Debug.Log("[BDArmory]: Ripple data is empty.");
            }

            if (vessel)
            {
                foreach (var rl in vessel.FindPartModulesImplementing<RocketLauncher>())
                {
                    if (!rl) continue;

                    if (!rippleDictionary.ContainsKey(rl.GetShortName()))
                    {
                        rippleDictionary.Add(rl.GetShortName(), new RippleOption(false, 650f));
                    }
                }
            }

            hasLoadedRippleData = true;
        }

        void SaveRippleOptions(ConfigNode node)
        {
            if (rippleDictionary != null)
            {
                rippleData = string.Empty;
                foreach (var wpnName in rippleDictionary.Keys)
                {
                    rippleData += wpnName + "," + rippleDictionary[wpnName].rippleFire.ToString() + "," +
                                  rippleDictionary[wpnName].rpm.ToString() + ";";
                }


                node.SetValue("RippleData", rippleData, true);
            }
            Debug.Log("[BDArmory]: Saved ripple data: " + rippleData);
        }

        public bool hasSingleFired = false;

        //bomb aimer
        Part bombPart = null;
        Vector3 bombAimerPosition = Vector3.zero;
        Texture2D bombAimerTexture = GameDatabase.Instance.GetTexture("BDArmory/Textures/grayCircle", false);
        bool showBombAimer = false;
        
        //targeting
        private List<Vessel> loadedVessels = new List<Vessel>();
        float targetListTimer;
        
        //rocket aimer handling
        RocketLauncher currentRocket = null;
        
        //sounds
        AudioSource audioSource;
        public AudioSource warningAudioSource;
        AudioSource targetingAudioSource;
        AudioClip clickSound;
        AudioClip warningSound;
        AudioClip armOnSound;
        AudioClip armOffSound;
        AudioClip heatGrowlSound;
        bool warningSounding;

        //missile warning
        public bool missileIsIncoming = false;
        public float incomingMissileDistance = float.MaxValue;
        public Vessel incomingMissileVessel;

        //guard mode vars
        float targetScanTimer = 0;
        Vessel guardTarget = null;
        public TargetInfo currentTarget;
        TargetInfo overrideTarget; //used for setting target next guard scan for stuff like assisting teammates
        float overrideTimer = 0;

        public bool TargetOverride
        {
            get { return overrideTimer > 0; }
        }

        //AIPilot
        public BDModulePilotAI pilotAI = null;
        public float timeBombReleased = 0;

        //targeting pods
        public ModuleTargetingCamera mainTGP = null;
        public List<ModuleTargetingCamera> targetingPods = new List<ModuleTargetingCamera>();

        //radar
        public List<ModuleRadar> radars = new List<ModuleRadar>();
        public VesselRadarData vesselRadarData;

        //jammers
        public List<ModuleECMJammer> jammers = new List<ModuleECMJammer>();

        //wingcommander
        public ModuleWingCommander wingCommander;

        //RWR
        private RadarWarningReceiver radarWarn = null;

        public RadarWarningReceiver rwr
        {
            get
            {
                if (!radarWarn || radarWarn.vessel != vessel)
                {
                    return null;
                }
                return radarWarn;
            }
            set { radarWarn = value; }
        }

        //GPS
        public GPSTargetInfo designatedGPSInfo;

        public Vector3d designatedGPSCoords => designatedGPSInfo.gpsCoordinates;

        //Guard view scanning
        float guardViewScanDirection = 1;
        float guardViewScanRate = 200;
        float currentGuardViewAngle = 0;
        private Transform vrt;

        public Transform viewReferenceTransform
        {
            get
            {
                if (vrt == null)
                {
                    vrt = (new GameObject()).transform;
                    vrt.parent = transform;
                    vrt.localPosition = Vector3.zero;
                    vrt.rotation = Quaternion.LookRotation(-transform.forward, -vessel.ReferenceTransform.forward);
                }

                return vrt;
            }
        }

        //weapon slaving
        public bool slavingTurrets = false;
        public Vector3 slavedPosition;
        public Vector3 slavedVelocity;
        public Vector3 slavedAcceleration;

		//current weapon ref
		public MissileBase CurrentMissile;
        public ModuleWeapon currentGun
        {
            get
            {
                if (selectedWeapon != null && selectedWeapon.GetWeaponClass() == WeaponClasses.Gun)
                {
                    return selectedWeapon.GetPart().FindModuleImplementing<ModuleWeapon>();
                }
                else
                {
                    return null;
                }
            }
        }
                
        public bool underFire = false;
        Coroutine ufRoutine = null;

        Vector3 debugGuardViewDirection;
        bool focusingOnTarget = false;
        float focusingOnTargetTimer = 0;
        public Vector3 incomingThreatPosition;
        public Vessel incomingThreatVessel;

        bool guardFiringMissile = false;
        bool disabledRocketAimers = false;
        bool antiRadTargetAcquired = false;
        Vector3 antiRadiationTarget;
        bool laserPointDetected = false;

        ModuleTargetingCamera foundCam;

        #region KSPFields,events,actions

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Firing Interval"),
         UI_FloatRange(minValue = 1f, maxValue = 60f, stepIncrement = 1f, scene = UI_Scene.All)] public float
            targetScanInterval = 3;

        // extension for feature_engagementenvelope: burst length for guns
        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Firing Burst Length"),
         UI_FloatRange(minValue = 0f, maxValue = 60f, stepIncrement = 0.5f, scene = UI_Scene.All)]
        public float fireBurstLength = 0;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Field of View"),
         UI_FloatRange(minValue = 10f, maxValue = 360f, stepIncrement = 10f, scene = UI_Scene.All)] public float
            guardAngle = 360;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Visual Range"),
         UI_FloatRange(minValue = 100f, maxValue = 5000, stepIncrement = 100f, scene = UI_Scene.All)] public float
            guardRange = 5000;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Guns Range"),
         UI_FloatRange(minValue = 0f, maxValue = 10000f, stepIncrement = 10f, scene = UI_Scene.All)] public float
            gunRange = 2000f;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Missiles/Target"),
         UI_FloatRange(minValue = 1f, maxValue = 6f, stepIncrement = 1f, scene = UI_Scene.All)] public float
            maxMissilesOnTarget = 1;


        public void ToggleGuardMode()
        {
            guardMode = !guardMode;

            if (!guardMode)
            {
                //disable turret firing and guard mode
                foreach (var weapon in vessel.FindPartModulesImplementing<ModuleWeapon>())
                {
                    weapon.legacyTargetVessel = null;
                    weapon.autoFire = false;
                    weapon.aiControlled = false;
                }
            }
        }


        [KSPAction("Toggle Guard Mode")]
        public void AGToggleGuardMode(KSPActionParam param)
        {
            ToggleGuardMode();
        }


        [KSPField(isPersistant = true)] public bool guardMode = false;


        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Target Type: "),
         UI_Toggle(disabledText = "Vessels", enabledText = "Missiles")] public bool targetMissiles = false;

        [KSPAction("Toggle Target Type")]
        public void AGToggleTargetType(KSPActionParam param)
        {
            ToggleTargetType();
        }

        public void ToggleTargetType()
        {
            targetMissiles = !targetMissiles;
            audioSource.PlayOneShot(clickSound);
        }

		[KSPAction("Jettison Weapon")]
		public void AGJettisonWeapon(KSPActionParam param)
		{
			if(CurrentMissile)
			{
				foreach(var missile in vessel.FindPartModulesImplementing<MissileBase>())
				{
					if(missile.GetShortName() == CurrentMissile.GetShortName())
					{
						missile.Jettison();
					}
				}
			}
			else if(selectedWeapon!=null && selectedWeapon.GetWeaponClass() == WeaponClasses.Rocket)
			{
				foreach(var rocket in vessel.FindPartModulesImplementing<RocketLauncher>())
				{
					rocket.Jettison();
				}
			}
		}
		
		
		[KSPField(guiActive = true, guiActiveEditor = true, guiName = "Team")]
		public string teamString = "A";
		void UpdateTeamString()
		{
			teamString = Enum.GetName(typeof(BDArmorySettings.BDATeams), BDATargetManager.BoolToTeam(team));
		}
		
		
		[KSPField(isPersistant = true)]
        public bool team = false;


        [KSPAction("Toggle Team")]
        public void AGToggleTeam(KSPActionParam param)
        {
            ToggleTeam();
        }

        public delegate void ToggleTeamDelegate(MissileFire wm, BDArmorySettings.BDATeams team);

        public static event ToggleTeamDelegate OnToggleTeam;

        [KSPEvent(active = true, guiActiveEditor = true, guiActive = false)]
        public void ToggleTeam()
        {
            team = !team;

            if (HighLogic.LoadedSceneIsFlight)
            {
                audioSource.PlayOneShot(clickSound);
                foreach (var wpnMgr in vessel.FindPartModulesImplementing<MissileFire>())
                {
                    wpnMgr.team = team;
                }
                if (vessel.GetComponent<TargetInfo>())
                {
                    vessel.GetComponent<TargetInfo>().RemoveFromDatabases();
                    Destroy(vessel.GetComponent<TargetInfo>());
                }

                if (OnToggleTeam != null)
                {
                    OnToggleTeam(this, BDATargetManager.BoolToTeam(team));
                }
            }

            UpdateTeamString();
            ResetGuardInterval();

        }

        [KSPField(isPersistant = true)]
        public bool isArmed = false;


        [KSPAction("Arm/Disarm")]
        public void AGToggleArm(KSPActionParam param)
        {
            ToggleArm();
        }

        public void ToggleArm()
        {
            isArmed = !isArmed;
            if (isArmed) audioSource.PlayOneShot(armOnSound);
            else audioSource.PlayOneShot(armOffSound);
        }


        [KSPField(isPersistant = false, guiActive = true, guiName = "Weapon")] public string selectedWeaponString =
            "None";

        IBDWeapon sw = null;

        public IBDWeapon selectedWeapon
        {
            get
            {
                if ((sw == null || sw.GetPart().vessel != vessel) && weaponIndex > 0)
                {
                    foreach (IBDWeapon weapon in vessel.FindPartModulesImplementing<IBDWeapon>())
                    {
                        if (weapon.GetShortName() == selectedWeaponString)
                        {
                            sw = weapon;
                            break;
                        }
                    }
                }
                return sw;
            }
            set
            {
                sw = value;
                selectedWeaponString = GetWeaponName(value);
            }
        }

        [KSPAction("Fire Missile")]
        public void AGFire(KSPActionParam param)
        {
            FireMissile();
        }

        [KSPAction("Fire Guns (Hold)")]
        public void AGFireGunsHold(KSPActionParam param)
        {
            if (weaponIndex > 0 &&
                (selectedWeapon.GetWeaponClass() == WeaponClasses.Gun ||
                 selectedWeapon.GetWeaponClass() == WeaponClasses.DefenseLaser))
            {
                foreach (var weap in vessel.FindPartModulesImplementing<ModuleWeapon>())
                {
                    if (weap.weaponState != ModuleWeapon.WeaponStates.Enabled ||
                        weap.GetShortName() != selectedWeapon.GetShortName())
                    {
                        continue;
                    }

                    weap.AGFireHold(param);
                }
            }
        }

        [KSPAction("Fire Guns (Toggle)")]
        public void AGFireGunsToggle(KSPActionParam param)
        {
            if (weaponIndex > 0 &&
                (selectedWeapon.GetWeaponClass() == WeaponClasses.Gun ||
                 selectedWeapon.GetWeaponClass() == WeaponClasses.DefenseLaser))
            {
                foreach (var weap in vessel.FindPartModulesImplementing<ModuleWeapon>())
                {
                    if (weap.weaponState != ModuleWeapon.WeaponStates.Enabled ||
                        weap.GetShortName() != selectedWeapon.GetShortName())
                    {
                        continue;
                    }

                    weap.AGFireToggle(param);
                }
            }
        }

        /*
        [KSPEvent(guiActive = true, guiName = "Fire", active = true)]
        public void GuiFire()
        {
            FireMissile();	
        }
        */
        /*
        [KSPEvent(guiActive = true, guiName = "Next Weapon", active = true)]
        public void GuiCycle()
        {
            CycleWeapon(true);	
        }
        */

        [KSPAction("Next Weapon")]
        public void AGCycle(KSPActionParam param)
        {
            CycleWeapon(true);
        }

        /*
        [KSPEvent(guiActive = true, guiName = "Previous Weapon", active = true)]
        public void GuiCycleBack()
        {
            CycleWeapon(false);	
        }
        */

        [KSPAction("Previous Weapon")]
        public void AGCycleBack(KSPActionParam param)
        {
            CycleWeapon(false);
        }

        [KSPEvent(guiActive = true, guiActiveEditor = false, guiName = "Open GUI", active = true)]
        public void ToggleToolbarGUI()
        {
            BDArmorySettings.toolbarGuiEnabled = !BDArmorySettings.toolbarGuiEnabled;
        }

        #endregion

        #endregion

        #region KSP Events
             
        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);

            if (HighLogic.LoadedSceneIsFlight)
            {
                SaveRippleOptions(node);
            }
        }
               
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (HighLogic.LoadedSceneIsFlight)
            {
                rippleData = string.Empty;
                if (node.HasValue("RippleData"))
                {
                    rippleData = node.GetValue("RippleData");
                }
                ParseRippleOptions();
            }
        }

        public override void OnAwake()
        {
            clickSound = GameDatabase.Instance.GetAudioClip("BDArmory/Sounds/click");
            warningSound = GameDatabase.Instance.GetAudioClip("BDArmory/Sounds/warning");
            armOnSound = GameDatabase.Instance.GetAudioClip("BDArmory/Sounds/armOn");
            armOffSound = GameDatabase.Instance.GetAudioClip("BDArmory/Sounds/armOff");
            heatGrowlSound = GameDatabase.Instance.GetAudioClip("BDArmory/Sounds/heatGrowl");

            //HEAT LOCKING
            heatTarget = TargetSignatureData.noTarget;
        }

        public override void OnStart(PartModule.StartState state)
        {
            UpdateMaxGuardRange();

            startTime = Time.time;

            UpdateTeamString();

            if (HighLogic.LoadedSceneIsFlight)
            {
                part.force_activate();

                selectionMessage = new ScreenMessage("", 2.0f, ScreenMessageStyle.LOWER_CENTER);

                UpdateList();
                if (weaponArray.Length > 0) selectedWeapon = weaponArray[weaponIndex];
                //selectedWeaponString = GetWeaponName(selectedWeapon);

                cameraTransform = part.FindModelTransform("BDARPMCameraTransform");

                part.force_activate();
                rippleTimer = Time.time;
                targetListTimer = Time.time;

                wingCommander = part.FindModuleImplementing<ModuleWingCommander>();


                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.minDistance = 1;
                audioSource.maxDistance = 500;
                audioSource.dopplerLevel = 0;
                audioSource.spatialBlend = 1;

                warningAudioSource = gameObject.AddComponent<AudioSource>();
                warningAudioSource.minDistance = 1;
                warningAudioSource.maxDistance = 500;
                warningAudioSource.dopplerLevel = 0;
                warningAudioSource.spatialBlend = 1;

                targetingAudioSource = gameObject.AddComponent<AudioSource>();
                targetingAudioSource.minDistance = 1;
                targetingAudioSource.maxDistance = 250;
                targetingAudioSource.dopplerLevel = 0;
                targetingAudioSource.loop = true;
                targetingAudioSource.spatialBlend = 1;

                StartCoroutine(MissileWarningResetRoutine());

                if (vessel.isActiveVessel)
                {
                    BDArmorySettings.Instance.ActiveWeaponManager = this;
                }

                UpdateVolume();
                BDArmorySettings.OnVolumeChange += UpdateVolume;
                BDArmorySettings.OnSavedSettings += ClampVisualRange;

                StartCoroutine(StartupListUpdater());
                missilesAway = 0;

                GameEvents.onVesselCreate.Add(OnVesselCreate);
                GameEvents.onPartJointBreak.Add(OnPartJointBreak);
                GameEvents.onPartDie.Add(OnPartDie);

                foreach (var aipilot in vessel.FindPartModulesImplementing<BDModulePilotAI>())
                {
                    pilotAI = aipilot;
                    break;
                }
            }
        }

        void OnPartDie(Part p)
        {
            if (p == part)
            {
                try
                {
                    GameEvents.onPartDie.Remove(OnPartDie);
                    GameEvents.onPartJointBreak.Remove(OnPartJointBreak);
                }
                catch(Exception e)
                {
                    if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory]: Error OnPartDie" + e.Message);
                }                
            }
            RefreshTargetingModules();
            UpdateList();
        }

        void OnVesselCreate(Vessel v)
        {
            RefreshTargetingModules();
        }

        void OnPartJointBreak(PartJoint j, float breakForce)
        {
            if (!part)
            {
                GameEvents.onPartJointBreak.Remove(OnPartJointBreak);
            }

            if ((j.Parent && j.Parent.vessel == vessel) || (j.Child && j.Child.vessel == vessel))
            {
                RefreshTargetingModules();
                UpdateList();
            }
        }

        public override void OnUpdate()
        {
            base.OnUpdate();

            if (!HighLogic.LoadedSceneIsFlight)
            {
                return;
            }


            if (!vessel.packed)
            {
                if (weaponIndex >= weaponArray.Length)
                {
                    hasSingleFired = true;
                    triggerTimer = 0;

                    weaponIndex = Mathf.Clamp(weaponIndex, 0, weaponArray.Length - 1);

                    DisplaySelectedWeaponMessage();
                }
                if (weaponArray.Length > 0) selectedWeapon = weaponArray[weaponIndex];

                //finding next rocket to shoot (for aimer)
                //FindNextRocket();


                //targeting
                if (weaponIndex > 0 &&
                    (selectedWeapon.GetWeaponClass() == WeaponClasses.Missile ||
                    selectedWeapon.GetWeaponClass() == WeaponClasses.SLW ||
                     selectedWeapon.GetWeaponClass() == WeaponClasses.Bomb))
                {
                    SearchForLaserPoint();
                    SearchForHeatTarget();
                    SearchForRadarSource();
                }
            }

            UpdateTargetingAudio();


            if (vessel.isActiveVessel)
            {
                if (!CheckMouseIsOnGui() && isArmed && BDInputUtils.GetKey(BDInputSettingsFields.WEAP_FIRE_KEY))
                {
                    triggerTimer += Time.fixedDeltaTime;
                }
                else
                {
                    triggerTimer = 0;
                    hasSingleFired = false;
                }


                //firing missiles and rockets===
                if (!guardMode &&
                    selectedWeapon != null &&
                    (selectedWeapon.GetWeaponClass() == WeaponClasses.Rocket
                     || selectedWeapon.GetWeaponClass() == WeaponClasses.Missile
                     || selectedWeapon.GetWeaponClass() == WeaponClasses.Bomb
                     || selectedWeapon.GetWeaponClass() == WeaponClasses.SLW
                    ))
                {
                    canRipple = true;
                    if (!MapView.MapIsEnabled && triggerTimer > BDArmorySettings.TRIGGER_HOLD_TIME && !hasSingleFired)
                    {
                        if (rippleFire)
                        {
                            if (Time.time - rippleTimer > 60f / rippleRPM)
                            {
                                FireMissile();
                                rippleTimer = Time.time;
                            }
                        }
                        else
                        {
                            FireMissile();
                            hasSingleFired = true;
                        }
                    }
                }
                else if (!guardMode &&
                         selectedWeapon != null &&
                         (selectedWeapon.GetWeaponClass() == WeaponClasses.Gun && currentGun.roundsPerMinute < 1500))
                {
                    canRipple = true;
                }
                else
                {
                    canRipple = false;
                }
            }
        }            

        public override void OnFixedUpdate()
        {
            if (guardMode && vessel.IsControllable)
            {
                GuardMode();
            }
            else
            {
                targetScanTimer = -100;
            }

            if (vessel.isActiveVessel)
            {
                TargetAcquire();
            }
            BombAimer();
        }

        void OnDestroy()
        {
            BDArmorySettings.OnVolumeChange -= UpdateVolume;
            BDArmorySettings.OnSavedSettings -= ClampVisualRange;
            GameEvents.onVesselCreate.Remove(OnVesselCreate);
            GameEvents.onPartJointBreak.Remove(OnPartJointBreak);
            GameEvents.onPartDie.Remove(OnPartDie);
        }

        void ClampVisualRange()
        {
            if (!BDArmorySettings.ALLOW_LEGACY_TARGETING)
            {
                guardRange = Mathf.Clamp(guardRange, 0, BDArmorySettings.MAX_GUARD_VISUAL_RANGE);
            }

            //UpdateMaxGuardRange();
        }

        void OnGUI()
        {
            if (HighLogic.LoadedSceneIsFlight && vessel == FlightGlobals.ActiveVessel &&
                BDArmorySettings.GAME_UI_ENABLED && !MapView.MapIsEnabled)
            {
                if (BDArmorySettings.DRAW_DEBUG_LINES)
                {
                    if (guardMode && !BDArmorySettings.ALLOW_LEGACY_TARGETING)
                    {
                        BDGUIUtils.DrawLineBetweenWorldPositions(part.transform.position,
                            part.transform.position + (debugGuardViewDirection * 25), 2, Color.yellow);
                    }

                    if (incomingMissileVessel)
                    {
                        BDGUIUtils.DrawLineBetweenWorldPositions(part.transform.position,
                            incomingMissileVessel.transform.position, 5, Color.cyan);
                    }
                }

                if (showBombAimer)
                {
                    MissileBase ml = CurrentMissile;
                    if (ml)
                    {
                        float size = 128;
                        Texture2D texture = BDArmorySettings.Instance.greenCircleTexture;


                        if ((ml is MissileLauncher && ((MissileLauncher)ml).guidanceActive) || ml is BDModularGuidance)
                        {
                            texture = BDArmorySettings.Instance.largeGreenCircleTexture;
                            size = 256;
                        }
                        BDGUIUtils.DrawTextureOnWorldPos(bombAimerPosition, texture, new Vector2(size, size), 0);
                    }
                }



                //MISSILE LOCK HUD
                MissileBase missile = CurrentMissile;
                if (missile)
                {
                    if (missile.TargetingMode == MissileBase.TargetingModes.Laser)
                    {
                        if (laserPointDetected && foundCam)
                        {
                            BDGUIUtils.DrawTextureOnWorldPos(foundCam.groundTargetPosition, BDArmorySettings.Instance.greenCircleTexture, new Vector2(48, 48), 1);
                        }

                        foreach (var cam in BDATargetManager.ActiveLasers)
                        {
                            if (cam && cam.vessel != vessel && cam.surfaceDetected && cam.groundStabilized && !cam.gimbalLimitReached)
                            {
                                BDGUIUtils.DrawTextureOnWorldPos(cam.groundTargetPosition, BDArmorySettings.Instance.greenDiamondTexture, new Vector2(18, 18), 0);
                            }
                        }
                    }
                    else if (missile.TargetingMode == MissileBase.TargetingModes.Heat)
                    {
                        MissileBase ml = CurrentMissile;
                        if (heatTarget.exists)
                        {
                            BDGUIUtils.DrawTextureOnWorldPos(heatTarget.position, BDArmorySettings.Instance.greenCircleTexture, new Vector2(36, 36), 3);
                            float distanceToTarget = Vector3.Distance(heatTarget.position, ml.MissileReferenceTransform.position);
                            BDGUIUtils.DrawTextureOnWorldPos(ml.MissileReferenceTransform.position + (distanceToTarget * ml.GetForwardTransform()), BDArmorySettings.Instance.largeGreenCircleTexture, new Vector2(128, 128), 0);
                            Vector3 fireSolution = MissileGuidance.GetAirToAirFireSolution(ml, heatTarget.position, heatTarget.velocity);
                            Vector3 fsDirection = (fireSolution - ml.MissileReferenceTransform.position).normalized;
                            BDGUIUtils.DrawTextureOnWorldPos(ml.MissileReferenceTransform.position + (distanceToTarget * fsDirection), BDArmorySettings.Instance.greenDotTexture, new Vector2(6, 6), 0);
                        }
                        else
                        {
                            BDGUIUtils.DrawTextureOnWorldPos(ml.MissileReferenceTransform.position + (2000 * ml.GetForwardTransform()), BDArmorySettings.Instance.greenCircleTexture, new Vector2(36, 36), 3);
                            BDGUIUtils.DrawTextureOnWorldPos(ml.MissileReferenceTransform.position + (2000 * ml.GetForwardTransform()), BDArmorySettings.Instance.largeGreenCircleTexture, new Vector2(156, 156), 0);
                        }
                    }
                    else if (missile.TargetingMode == MissileBase.TargetingModes.Radar)
                    {
                        MissileBase ml = CurrentMissile;
                        //if(radar && radar.locked)
                        if (vesselRadarData && vesselRadarData.locked)
                        {
                            float distanceToTarget = Vector3.Distance(vesselRadarData.lockedTargetData.targetData.predictedPosition, ml.MissileReferenceTransform.position);
                            BDGUIUtils.DrawTextureOnWorldPos(ml.MissileReferenceTransform.position + (distanceToTarget * ml.GetForwardTransform()), BDArmorySettings.Instance.dottedLargeGreenCircle, new Vector2(128, 128), 0);
                            //Vector3 fireSolution = MissileGuidance.GetAirToAirFireSolution(CurrentMissile, radar.lockedTarget.predictedPosition, radar.lockedTarget.velocity);
                            Vector3 fireSolution = MissileGuidance.GetAirToAirFireSolution(ml, vesselRadarData.lockedTargetData.targetData.predictedPosition, vesselRadarData.lockedTargetData.targetData.velocity);
                            Vector3 fsDirection = (fireSolution - ml.MissileReferenceTransform.position).normalized;
                            BDGUIUtils.DrawTextureOnWorldPos(ml.MissileReferenceTransform.position + (distanceToTarget * fsDirection), BDArmorySettings.Instance.greenDotTexture, new Vector2(6, 6), 0);

                            if (BDArmorySettings.DRAW_DEBUG_LABELS)
                            {
                                string dynRangeDebug = string.Empty;
                                MissileLaunchParams dlz = MissileLaunchParams.GetDynamicLaunchParams(missile, vesselRadarData.lockedTargetData.targetData.velocity, vesselRadarData.lockedTargetData.targetData.predictedPosition);
                                dynRangeDebug += "MaxDLZ: " + dlz.maxLaunchRange;
                                dynRangeDebug += "\nMinDLZ: " + dlz.minLaunchRange;
                                GUI.Label(new Rect(800, 600, 200, 200), dynRangeDebug);
                            }
                        }
                    }
                    else if (missile.TargetingMode == MissileBase.TargetingModes.AntiRad)
                    {
                        if (rwr && rwr.rwrEnabled)
                        {
                            for (int i = 0; i < rwr.pingsData.Length; i++)
                            {
                                if (rwr.pingsData[i].exists && (rwr.pingsData[i].signalStrength == 0 || rwr.pingsData[i].signalStrength == 5) && Vector3.Dot(rwr.pingWorldPositions[i] - missile.transform.position, missile.GetForwardTransform()) > 0)
                                {
                                    BDGUIUtils.DrawTextureOnWorldPos(rwr.pingWorldPositions[i], BDArmorySettings.Instance.greenDiamondTexture, new Vector2(22, 22), 0);
                                }
                            }
                        }

                        if (antiRadTargetAcquired)
                        {
                            BDGUIUtils.DrawTextureOnWorldPos(antiRadiationTarget,
                                BDArmorySettings.Instance.openGreenSquare, new Vector2(22, 22), 0);
                        }
                    }
                }

                if ((missile && missile.TargetingMode == MissileBase.TargetingModes.Gps) || BDArmorySettings.Instance.showingGPSWindow)
                {
                    if (designatedGPSCoords != Vector3d.zero)
                    {
                        BDGUIUtils.DrawTextureOnWorldPos(VectorUtils.GetWorldSurfacePostion(designatedGPSCoords, vessel.mainBody), BDArmorySettings.Instance.greenSpikedPointCircleTexture, new Vector2(22, 22), 0);
                    }
                }

                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                {
                    GUI.Label(new Rect(500, 600, 100, 100), "Missiles away: " + missilesAway);
                }
            }
        }

        bool CheckMouseIsOnGui()
        {
            return Misc.CheckMouseIsOnGui();
        }

        #endregion

        #region Enumerators

        IEnumerator StartupListUpdater()
        {
            while (vessel.packed || !FlightGlobals.ready)
            {
                yield return null;
                if (vessel.isActiveVessel)
                {
                    BDArmorySettings.Instance.ActiveWeaponManager = this;
                }
            }
            UpdateList();
        }

        IEnumerator MissileWarningResetRoutine()
        {
            while (enabled)
            {
                missileIsIncoming = false;
                yield return new WaitForSeconds(1);
            }
        }

        IEnumerator UnderFireRoutine()
        {
            underFire = true;
            yield return new WaitForSeconds(3);
            underFire = false;
        }

        IEnumerator GuardTurretRoutine()
        {
            if (gameObject.activeInHierarchy && !BDArmorySettings.ALLOW_LEGACY_TARGETING)
            //target is out of visual range, try using sensors
            {
                if (guardTarget.LandedOrSplashed)
                {
                    if (targetingPods.Count > 0)
                    {
                        foreach (var tgp in targetingPods)
                        {
                            if (tgp.enabled &&
                                (!tgp.cameraEnabled || !tgp.groundStabilized ||
                                 (tgp.groundTargetPosition - guardTarget.transform.position).magnitude > 20))
                            {
                                tgp.EnableCamera();
                                yield return StartCoroutine(tgp.PointToPositionRoutine(guardTarget.CoM));
                                if (tgp)
                                {
                                    if (tgp.groundStabilized && guardTarget &&
                                        (tgp.groundTargetPosition - guardTarget.transform.position).magnitude < 20)
                                    {
                                        tgp.slaveTurrets = true;
                                        StartGuardTurretFiring();
                                        yield break;
                                    }
                                    else
                                    {
                                        tgp.DisableCamera();
                                    }
                                }
                            }
                        }
                    }

                    if (!guardTarget || (guardTarget.transform.position - transform.position).magnitude > guardRange)
                    {
                        SetTarget(null); //disengage, sensors unavailable.
                        yield break;
                    }
                }
                else
                {
                    if (!vesselRadarData || !(vesselRadarData.radarCount > 0))
                    {
                        foreach (var rd in radars)
                        {
                            if (rd.canLock)
                            {
                                rd.EnableRadar();
                                break;
                            }
                        }
                    }

                    if (vesselRadarData &&
                        (!vesselRadarData.locked ||
                         (vesselRadarData.lockedTargetData.targetData.predictedPosition - guardTarget.transform.position)
                             .magnitude > 40))
                    {
                        //vesselRadarData.TryLockTarget(guardTarget.transform.position);
                        vesselRadarData.TryLockTarget(guardTarget);
                        yield return new WaitForSeconds(0.5f);
                        if (guardTarget && vesselRadarData && vesselRadarData.locked &&
                            vesselRadarData.lockedTargetData.vessel == guardTarget)
                        {
                            vesselRadarData.SlaveTurrets();
                            StartGuardTurretFiring();
                            yield break;
                        }
                    }

                    if (!guardTarget || (guardTarget.transform.position - transform.position).magnitude > guardRange)
                    {
                        SetTarget(null); //disengage, sensors unavailable.
                        yield break;
                    }
                }
            }


            StartGuardTurretFiring();
            yield break;
        }

        IEnumerator ResetMissileThreatDistanceRoutine()
        {
            yield return new WaitForSeconds(8);
            incomingMissileDistance = float.MaxValue;
        }

        IEnumerator GuardMissileRoutine()
        {
            MissileBase ml = CurrentMissile;

            if (ml && !guardFiringMissile)
            {
                guardFiringMissile = true;


                if (BDArmorySettings.ALLOW_LEGACY_TARGETING)
                {
                    //TODO BDModularGuidance Legacy and turret implementation
                    if (BDArmorySettings.DRAW_DEBUG_LABELS)
                    {
                        Debug.Log("[BDArmory]: Firing on target: " + guardTarget.GetName() + ", (legacy targeting)");
                    }
                    if (ml is MissileLauncher && ((MissileLauncher)ml).missileTurret)
                    {
                        ((MissileLauncher)ml).missileTurret.PrepMissileForFire(((MissileLauncher)ml));
                        ((MissileLauncher)ml).FireMissileOnTarget(guardTarget);
                        ((MissileLauncher)ml).missileTurret.UpdateMissileChildren();
                    }
                    else
                    {
                        ((MissileLauncher)ml).FireMissileOnTarget(guardTarget);
                    }
                    StartCoroutine(MissileAwayRoutine(ml));
                    UpdateList();
                }
                else if (ml.TargetingMode == MissileBase.TargetingModes.Radar && vesselRadarData)
                {
                    float attemptLockTime = Time.time;
                    while ((!vesselRadarData.locked || (vesselRadarData.lockedTargetData.vessel != guardTarget)) && Time.time - attemptLockTime < 2)
                    {
                        if (vesselRadarData.locked)
                        {
                            vesselRadarData.UnlockAllTargets();
                            yield return null;
                        }
                        //vesselRadarData.TryLockTarget(guardTarget.transform.position+(guardTarget.rb_velocity*Time.fixedDeltaTime));
                        vesselRadarData.TryLockTarget(guardTarget);
                        yield return new WaitForSeconds(0.25f);
                    }

                    if (ml && pilotAI && guardTarget && vesselRadarData.locked)
                    {
                        SetCargoBays();
                        float LAstartTime = Time.time;
                        while (guardTarget && Time.time - LAstartTime < 3 && pilotAI &&
                               !pilotAI.GetLaunchAuthorization(guardTarget, this))
                        {
                            yield return new WaitForFixedUpdate();
                        }

                        yield return new WaitForSeconds(0.5f);
                    }


                    //wait for missile turret to point at target
                    //TODO BDModularGuidance: add turret
                    var mlauncher = ml as MissileLauncher;
                    if (mlauncher != null)
                    {
                        if (guardTarget && ml && mlauncher.missileTurret && vesselRadarData.locked)
                        {
                            vesselRadarData.SlaveTurrets();
                            float turretStartTime = Time.time;
                            while (Time.time - turretStartTime < 5)
                            {
                                float angle = Vector3.Angle(mlauncher.missileTurret.finalTransform.forward, mlauncher.missileTurret.slavedTargetPosition - mlauncher.missileTurret.finalTransform.position);
                                if (angle < 1)
                                {
                                    turretStartTime -= 2 * Time.fixedDeltaTime;
                                }
                                yield return new WaitForFixedUpdate();
                            }
                        }
                    }

                    yield return null;

                    if (ml && guardTarget && vesselRadarData.locked && (!pilotAI || pilotAI.GetLaunchAuthorization(guardTarget, this)))
                    {
                        if (BDArmorySettings.DRAW_DEBUG_LABELS)
                        {
                            Debug.Log("Firing on target: " + guardTarget.GetName());
                        }
                        FireCurrentMissile(true);
                        StartCoroutine(MissileAwayRoutine(mlauncher));
                    }
                }
                else if (ml.TargetingMode == MissileBase.TargetingModes.Heat)
                {
                    if (vesselRadarData && vesselRadarData.locked)
                    {
                        vesselRadarData.UnlockAllTargets();
                        vesselRadarData.UnslaveTurrets();
                    }
                    float attemptStartTime = Time.time;
                    float attemptDuration = Mathf.Max(targetScanInterval * 0.75f, 5f);
                    SetCargoBays();


                    MissileLauncher mlauncher;
                    while (ml && Time.time - attemptStartTime < attemptDuration && (!heatTarget.exists || (heatTarget.predictedPosition - guardTarget.transform.position).magnitude > 40))
                    {
                        //TODO BDModularGuidance: add turret
                        //try using missile turret to lock target
                        mlauncher = ml as MissileLauncher;
                        if (mlauncher != null)
                        {
                            if (mlauncher.missileTurret)
                            {
                                mlauncher.missileTurret.slaved = true;
                                mlauncher.missileTurret.slavedTargetPosition = guardTarget.CoM;
                                mlauncher.missileTurret.SlavedAim();
                            }
                        }

                        yield return new WaitForFixedUpdate();
                    }


                    //try uncaged IR lock with radar
                    if (guardTarget && !heatTarget.exists && vesselRadarData && vesselRadarData.radarCount > 0)
                    {
                        if (!vesselRadarData.locked ||
                            (vesselRadarData.lockedTargetData.targetData.predictedPosition -
                             guardTarget.transform.position).magnitude > 40)
                        {
                            //vesselRadarData.TryLockTarget(guardTarget.transform.position);
                            vesselRadarData.TryLockTarget(guardTarget);
                            yield return new WaitForSeconds(Mathf.Min(1, (targetScanInterval * 0.25f)));
                        }
                    }

                    if (guardTarget && ml && heatTarget.exists && pilotAI)
                    {
                        float LAstartTime = Time.time;
                        while (Time.time - LAstartTime < 3 && pilotAI &&
                               !pilotAI.GetLaunchAuthorization(guardTarget, this))
                        {
                            yield return new WaitForFixedUpdate();
                        }

                        yield return new WaitForSeconds(0.5f);
                    }

                    //wait for missile turret to point at target
                    mlauncher = ml as MissileLauncher;
                    if (mlauncher != null)
                    {
                        if (ml && mlauncher.missileTurret && heatTarget.exists)
                        {
                            float turretStartTime = attemptStartTime;
                            while (heatTarget.exists && Time.time - turretStartTime < Mathf.Max(targetScanInterval / 2f, 2))
                            {
                                float angle = Vector3.Angle(mlauncher.missileTurret.finalTransform.forward, mlauncher.missileTurret.slavedTargetPosition - mlauncher.missileTurret.finalTransform.position);
                                mlauncher.missileTurret.slaved = true;
                                mlauncher.missileTurret.slavedTargetPosition = MissileGuidance.GetAirToAirFireSolution(mlauncher, heatTarget.predictedPosition, heatTarget.velocity);
                                mlauncher.missileTurret.SlavedAim();

                                if (angle < 1)
                                {
                                    turretStartTime -= 3 * Time.fixedDeltaTime;
                                }
                                yield return new WaitForFixedUpdate();
                            }
                        }
                    }

                    yield return null;

                    if (guardTarget && ml && heatTarget.exists &&
                        (!pilotAI || pilotAI.GetLaunchAuthorization(guardTarget, this)))
                    {
                        if (BDArmorySettings.DRAW_DEBUG_LABELS)
                        {
                            Debug.Log("[BDArmory]: Firing on target: " + guardTarget.GetName());
                        }

                        FireCurrentMissile(true);
                        StartCoroutine(MissileAwayRoutine(mlauncher));
                    }
                }
                else if (ml.TargetingMode == MissileBase.TargetingModes.Gps)
                {
                    designatedGPSInfo = new GPSTargetInfo(VectorUtils.WorldPositionToGeoCoords(guardTarget.CoM, vessel.mainBody), guardTarget.vesselName.Substring(0, Mathf.Min(12, guardTarget.vesselName.Length)));

                    FireCurrentMissile(true);
                    StartCoroutine(MissileAwayRoutine(ml)); //NEW: try to prevent launching all missile complements at once...

                }
                else if (ml.TargetingMode == MissileBase.TargetingModes.AntiRad)
                {
                    if (rwr)
                    {
                        rwr.EnableRWR();
                    }

                    float attemptStartTime = Time.time;
                    float attemptDuration = targetScanInterval * 0.75f;
                    while (Time.time - attemptStartTime < attemptDuration &&
                           (!antiRadTargetAcquired || (antiRadiationTarget - guardTarget.CoM).magnitude > 20))
                    {
                        yield return new WaitForFixedUpdate();
                    }

                    if (SetCargoBays())
                    {
                        yield return new WaitForSeconds(1f);
                    }

                    if (ml && antiRadTargetAcquired && (antiRadiationTarget - guardTarget.CoM).magnitude < 20)
                    {
                        FireCurrentMissile(true);
                        StartCoroutine(MissileAwayRoutine(ml));
                    }
                }
                else if (ml.TargetingMode == MissileBase.TargetingModes.Laser)
                {
                    if (targetingPods.Count > 0) //if targeting pods are available, slew them onto target and lock.
                    {
                        foreach (var tgp in targetingPods)
                        {
                            tgp.EnableCamera();
                            yield return StartCoroutine(tgp.PointToPositionRoutine(guardTarget.CoM));

                            if (tgp)
                            {
                                if (tgp.groundStabilized && (tgp.groundTargetPosition - guardTarget.transform.position).magnitude < 20)
                                {
                                    break;
                                }
                                tgp.DisableCamera();
                            }
                        }
                    }

                    //search for a laser point that corresponds with target vessel
                    float attemptStartTime = Time.time;
                    float attemptDuration = targetScanInterval * 0.75f;
                    while (Time.time - attemptStartTime < attemptDuration && (!laserPointDetected || (foundCam && (foundCam.groundTargetPosition - guardTarget.CoM).magnitude > 20)))
                    {
                        yield return new WaitForFixedUpdate();
                    }
                    if (SetCargoBays())
                    {
                        yield return new WaitForSeconds(1f);
                    }
                    if (ml && laserPointDetected && foundCam && (foundCam.groundTargetPosition - guardTarget.CoM).magnitude < 20)
                    {
                        FireCurrentMissile(true);
                        StartCoroutine(MissileAwayRoutine(ml));
                    }
                    else
                    {
                        if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory]: Laser Target Error");
                    }

                }


                guardFiringMissile = false;
            }
        }

        IEnumerator GuardBombRoutine()
        {
            guardFiringMissile = true;
            bool hasSetCargoBays = false;
            float bombStartTime = Time.time;
            float bombAttemptDuration = Mathf.Max(targetScanInterval, 12f);
            float radius = CurrentMissile.GetBlastRadius() * Mathf.Min((1 + (maxMissilesOnTarget / 2f)), 1.5f);
            if (CurrentMissile.TargetingMode == MissileBase.TargetingModes.Gps && Vector3.Distance(designatedGPSInfo.worldPos, guardTarget.CoM) > CurrentMissile.GetBlastRadius())
            {
                //check database for target first
                float twoxsqrRad = 4f * radius * radius;
                bool foundTargetInDatabase = false;
                foreach (var gps in BDATargetManager.GPSTargets[BDATargetManager.BoolToTeam(team)])
                {
                    if ((gps.worldPos - guardTarget.CoM).sqrMagnitude < twoxsqrRad)
                    {
                        designatedGPSInfo = gps;
                        foundTargetInDatabase = true;
                        break;
                    }
                }


                //no target in gps database, acquire via targeting pod
                if (!foundTargetInDatabase)
                {
                    ModuleTargetingCamera tgp = null;
                    foreach (var t in targetingPods)
                    {
                        if (t) tgp = t;
                    }

                    if (tgp)
                    {
                        tgp.EnableCamera();
                        yield return StartCoroutine(tgp.PointToPositionRoutine(guardTarget.CoM));

                        if (tgp)
                        {
                            if (guardTarget && tgp.groundStabilized && Vector3.Distance(tgp.groundTargetPosition, guardTarget.transform.position) < CurrentMissile.GetBlastRadius())
                            {
                                radius = 500;
                                designatedGPSInfo = new GPSTargetInfo(tgp.bodyRelativeGTP, "Guard Target");
                                bombStartTime = Time.time;
                            }
                            else//failed to acquire target via tgp, cancel.
                            {
                                tgp.DisableCamera();
                                designatedGPSInfo = new GPSTargetInfo();
                                guardFiringMissile = false;
                                yield break;
                            }
                        }
                        else//no gps target and lost tgp, cancel.
                        {
                            guardFiringMissile = false;
                            yield break;
                        }
                    }
                    else //no gps target and no tgp, cancel.
                    {
                        guardFiringMissile = false;
                        yield break;
                    }
                }
            }

            bool doProxyCheck = true;

            float prevDist = 2 * radius;
            radius = Mathf.Max(radius, 50f);
            while (guardTarget && Time.time - bombStartTime < bombAttemptDuration && weaponIndex > 0 &&
                   weaponArray[weaponIndex].GetWeaponClass() == WeaponClasses.Bomb && missilesAway < maxMissilesOnTarget)
            {
                float targetDist = Vector3.Distance(bombAimerPosition, guardTarget.CoM);

                if (targetDist < (radius * 20f) && !hasSetCargoBays)
                {
                    SetCargoBays();
                    hasSetCargoBays = true;
                }

                if (targetDist > radius)
                {
                    if (targetDist < Mathf.Max(radius * 2, 800f) &&
                        Vector3.Dot(guardTarget.CoM - bombAimerPosition, guardTarget.CoM - transform.position) < 0)
                    {
                        pilotAI.RequestExtend(guardTarget.CoM);
                        break;
                    }
                    yield return null;
                }
                else
                {
                    if (doProxyCheck)
                    {
                        if (targetDist - prevDist > 0)
                        {
                            doProxyCheck = false;
                        }
                        else
                        {
                            prevDist = targetDist;
                        }
                    }

                    if (!doProxyCheck)
                    {
                        FireCurrentMissile(true);
                        timeBombReleased = Time.time;
                        yield return new WaitForSeconds(rippleFire ? 60f / rippleRPM : 0.06f);
                        if (missilesAway >= maxMissilesOnTarget)
                        {
                            yield return new WaitForSeconds(1f);
                            if (pilotAI)
                            {
                                pilotAI.RequestExtend(guardTarget.CoM);
                            }
                        }
                    }
                    else
                    {
                        yield return null;
                    }
                }
            }

            designatedGPSInfo = new GPSTargetInfo();
            guardFiringMissile = false;
        }

        IEnumerator MissileAwayRoutine(MissileBase ml)
        {
            missilesAway++;
            float missileThrustTime = 300;

            var launcher = ml as MissileLauncher;
            if (launcher != null)
            {
                missileThrustTime = launcher.dropTime + launcher.cruiseTime + launcher.boostTime;
            }

            float timeStart = Time.time;
            float timeLimit = Mathf.Max(missileThrustTime + 4, 10);
            while (ml)
            {
                if (ml.guidanceActive && Time.time - timeStart < timeLimit)
                {
                    yield return null;
                }
                else
                {
                    break;
                }

            }
            missilesAway--;
        }

        IEnumerator BombsAwayRoutine(MissileBase ml)
        {
            missilesAway++;
            float timeStart = Time.time;
            float timeLimit = 3;
            while (ml)
            {
                if (Time.time - timeStart < timeLimit)
                {
                    yield return null;
                }
                else
                {
                    break;
                }
            }
            missilesAway--;
        }

        


        #endregion

        #region Audio

        void UpdateVolume()
        {
            if (audioSource)
            {
                audioSource.volume = BDArmorySettings.BDARMORY_UI_VOLUME;
            }
            if (warningAudioSource)
            {
                warningAudioSource.volume = BDArmorySettings.BDARMORY_UI_VOLUME;
            }
            if (targetingAudioSource)
            {
                targetingAudioSource.volume = BDArmorySettings.BDARMORY_UI_VOLUME;
            }
        }

        void UpdateTargetingAudio()
        {
            if (BDArmorySettings.GameIsPaused)
            {
                if (targetingAudioSource.isPlaying)
                {
                    targetingAudioSource.Stop();
                }
                return;
            }

            if (selectedWeapon != null && selectedWeapon.GetWeaponClass() == WeaponClasses.Missile && vessel.isActiveVessel)
            {
                MissileBase ml = CurrentMissile;
                if (ml.TargetingMode == MissileBase.TargetingModes.Heat)
                {
                    if (targetingAudioSource.clip != heatGrowlSound)
                    {
                        targetingAudioSource.clip = heatGrowlSound;
                    }

                    if (heatTarget.exists)
                    {
                        targetingAudioSource.pitch = Mathf.MoveTowards(targetingAudioSource.pitch, 2, 8 * Time.deltaTime);
                    }
                    else
                    {
                        targetingAudioSource.pitch = Mathf.MoveTowards(targetingAudioSource.pitch, 1, 8 * Time.deltaTime);
                    }

                    if (!targetingAudioSource.isPlaying)
                    {
                        targetingAudioSource.Play();
                    }
                }
                else
                {
                    if (targetingAudioSource.isPlaying)
                    {
                        targetingAudioSource.Stop();
                    }
                }
            }
            else
            {
                targetingAudioSource.pitch = 1;
                if (targetingAudioSource.isPlaying)
                {
                    targetingAudioSource.Stop();
                }
            }
        }

        IEnumerator WarningSoundRoutine(float distance, MissileBase ml)//give distance parameter
        {
            if (distance < 4000)
            {
                warningSounding = true;
                BDArmorySettings.Instance.missileWarningTime = Time.time;
                BDArmorySettings.Instance.missileWarning = true;
                warningAudioSource.pitch = distance < 800 ? 1.45f : 1f;
                warningAudioSource.PlayOneShot(warningSound);

                float waitTime = distance < 800 ? .25f : 1.5f;

                yield return new WaitForSeconds(waitTime);

                if (ml.vessel && CanSeeTarget(ml.vessel))
                {
                    BDATargetManager.ReportVessel(ml.vessel, this);
                }
            }
            warningSounding = false;
        }

        #endregion

        #region CounterMeasure
        public bool isChaffing = false;
        public bool isFlaring = false;
        public bool isECMJamming = false;

        bool isLegacyCMing = false;

        int cmCounter = 0;
        int cmAmount = 5;

        public void FireAllCountermeasures(int count)
        {
            StartCoroutine(AllCMRoutine(count));
        }

        public void FireECM()
        {
            if (!isECMJamming)
            {
                StartCoroutine(ECMRoutine());
            }
        }
        
        public void FireChaff()
        {
            if (!isChaffing)
            {
                StartCoroutine(ChaffRoutine());
            }
        }

        
        IEnumerator ECMRoutine()
        {
            isECMJamming = true;
            //yield return new WaitForSeconds(UnityEngine.Random.Range(0.2f, 1f));

            foreach (var ecm in vessel.FindPartModulesImplementing<ModuleECMJammer>())
            {
                if (ecm.jammerEnabled) yield break;
                ecm.EnableJammer();
            }

            yield return new WaitForSeconds(10.0f);
            isECMJamming = false;

            foreach (var ecm in vessel.FindPartModulesImplementing<ModuleECMJammer>())
            {
                ecm.DisableJammer();
            }
        }

        IEnumerator ChaffRoutine()
        {
            isChaffing = true;
            yield return new WaitForSeconds(UnityEngine.Random.Range(0.2f, 1f));

            foreach (var cm in vessel.FindPartModulesImplementing<CMDropper>())
            {
                if (cm.cmType == CMDropper.CountermeasureTypes.Chaff)
                {
                    cm.DropCM();
                }
            }

            yield return new WaitForSeconds(0.6f);

            isChaffing = false;
        }

        IEnumerator FlareRoutine(float time)
        {
            if (isFlaring) yield break;
            time = Mathf.Clamp(time, 2, 8);
            isFlaring = true;
            yield return new WaitForSeconds(UnityEngine.Random.Range(0f, 1f));
            float flareStartTime = Time.time;
            while (Time.time - flareStartTime < time)
            {
                foreach (var cm in vessel.FindPartModulesImplementing<CMDropper>())
                {
                    if (cm.cmType == CMDropper.CountermeasureTypes.Flare)
                    {
                        cm.DropCM();
                    }
                }
                yield return new WaitForSeconds(0.6f);
            }
            isFlaring = false;
        }

        IEnumerator AllCMRoutine(int count)
        {
            for (int i = 0; i < count; i++)
            {
                foreach (var cm in vessel.FindPartModulesImplementing<CMDropper>())
                {
                    if ((cm.cmType == CMDropper.CountermeasureTypes.Flare && !isFlaring)
                        || (cm.cmType == CMDropper.CountermeasureTypes.Chaff && !isChaffing)
                        || (cm.cmType == CMDropper.CountermeasureTypes.Smoke))
                    {
                        cm.DropCM();
                    }
                }
                isFlaring = true;
                isChaffing = true;
                yield return new WaitForSeconds(1f);
            }
            isFlaring = false;
            isChaffing = false;
        }

        IEnumerator LegacyCMRoutine()
        {
            isLegacyCMing = true;
            yield return new WaitForSeconds(UnityEngine.Random.Range(.2f, 1f));
            if (incomingMissileDistance < 2500)
            {
                cmAmount = Mathf.RoundToInt((2500 - incomingMissileDistance) / 400);
                foreach (var cm in vessel.FindPartModulesImplementing<CMDropper>())
                {
                    cm.DropCM();
                }
                cmCounter++;
                if (cmCounter < cmAmount)
                {
                    yield return new WaitForSeconds(0.15f);
                }
                else
                {
                    cmCounter = 0;
                    yield return new WaitForSeconds(UnityEngine.Random.Range(.5f, 1f));
                }
            }
            isLegacyCMing = false;
        }
        
        public void MissileWarning(float distance, MissileBase ml)//take distance parameter
        {
            if (vessel.isActiveVessel && !warningSounding)
            {
                StartCoroutine(WarningSoundRoutine(distance, ml));
            }

            missileIsIncoming = true;
            incomingMissileDistance = distance;
        }

        #endregion

        #region Fire

        void FireCurrentMissile(bool checkClearance)
        {
            MissileBase missile = CurrentMissile;
            if (missile == null) return;

            if (missile is MissileBase)
            {

                var ml = missile;
                if (checkClearance && (!CheckBombClearance(ml) || (ml is MissileLauncher && ((MissileLauncher)ml).rotaryRail && !((MissileLauncher)ml).rotaryRail.readyMissile == ml)))
                {
                    foreach (var otherMissile in vessel.FindPartModulesImplementing<MissileBase>())
                    {
                        if (otherMissile != ml && otherMissile.GetShortName() == ml.GetShortName() &&
                            CheckBombClearance(otherMissile))
                        {
                            CurrentMissile = otherMissile;
                            selectedWeapon = otherMissile;
                            FireCurrentMissile(false);
                            return;
                        }
                    }
                    CurrentMissile = ml;
                    selectedWeapon = ml;
                    return;
                }
                                
                if (ml is MissileLauncher && ((MissileLauncher)ml).missileTurret)
                {
                    ((MissileLauncher)ml).missileTurret.FireMissile(((MissileLauncher)ml));
                }
                else if (ml is MissileLauncher && ((MissileLauncher)ml).rotaryRail)
                {
                    ((MissileLauncher)ml).rotaryRail.FireMissile(((MissileLauncher)ml));
                }
                else
                {
                    SendTargetDataToMissile(ml);
                    ml.FireMissile();
                }

                if (guardMode)
                {
                    if (ml.GetWeaponClass() == WeaponClasses.Bomb)
                    {
                        StartCoroutine(BombsAwayRoutine(ml));
                    }
                }
                else
                {
                    if (vesselRadarData && vesselRadarData.autoCycleLockOnFire)
                    {
                        vesselRadarData.CycleActiveLock();
                    }
                }
            }
            else
            {
                SendTargetDataToMissile(missile);
                missile.FireMissile();
            }

            UpdateList();
        }

        /*
        public void FireMissile()
        {
            bool hasFired = false;

            if(selectedWeapon == null)
            {
                return;
            }

            if(lastFiredSym && lastFiredSym.partInfo.title != selectedWeapon.GetPart().partInfo.title)
            {
                lastFiredSym = null;
            }

            IBDWeapon firedWeapon = null;

            if(lastFiredSym != null && lastFiredSym.partName == selectedWeapon.GetPart().partName)
            {
                Part nextPart;
                if(FindSym(lastFiredSym)!=null)
                {
                    nextPart = FindSym(lastFiredSym);
                }
                else 
                {
                    nextPart = null;
                }


				if(selectedWeapon.GetWeaponClass() == WeaponClasses.Missile || selectedWeapon.GetWeaponClass() == WeaponClasses.Bomb)
				{
					foreach(MissileBase ml in lastFiredSym.FindModulesImplementing<MissileBase>())
					{
						if(!CheckBombClearance(ml))
						{
							lastFiredSym = null;
							break;
						}

                        if(guardMode && guardTarget!=null && BDArmorySettings.ALLOW_LEGACY_TARGETING)
                        {
                            firedWeapon = ml;
                            if(ml.missileTurret)
                            {
                                ml.missileTurret.PrepMissileForFire(ml);
                                ml.FireMissileOnTarget(guardTarget);
                                ml.missileTurret.UpdateMissileChildren();
                            }
                            else if(ml.rotaryRail)
                            {
                                ml.rotaryRail.PrepMissileForFire(ml);
                                ml.FireMissileOnTarget(guardTarget);
                                ml.rotaryRail.UpdateMissileChildren();
                            }
                            else
                            {
                                ml.FireMissileOnTarget(guardTarget);
                            }
                        }
                        else
                        {
                            firedWeapon = ml;
                            SendTargetDataToMissile(ml);
                            if(ml.missileTurret)
                            {
                                ml.missileTurret.FireMissile(ml);
                            }
                            else if(ml.rotaryRail)
                            {
                                ml.rotaryRail.FireMissile(ml);
                            }
                            else
                            {
                                ml.FireMissile();
                            }
                        }

                        hasFired = true;

						lastFiredSym = nextPart;
						if(lastFiredSym != null)
						{
							CurrentMissile = lastFiredSym.GetComponent<MissileBase>();
							selectedWeapon = CurrentMissile;
							SetMissileTurrets();
						}
						break;
					}	
				}
				else if(selectedWeapon.GetWeaponClass() == WeaponClasses.Rocket)
				{
					foreach(RocketLauncher rl in lastFiredSym.FindModulesImplementing<RocketLauncher>())
					{
						hasFired = true;
						firedWeapon = rl;
						rl.FireRocket();
						//rippleRPM = rl.rippleRPM;
						if(nextPart!=null)
						{
							foreach(PartResource r in nextPart.Resources.list)
							{
								if(r.amount>0) lastFiredSym = nextPart;
								else lastFiredSym = null;
							}	
						}
						break;
					}
				}
					
			}



			if(!hasFired && lastFiredSym == null)
			{
				bool firedMissile = false;
				foreach(MissileBase ml in vessel.FindPartModulesImplementing<MissileBase>())
				{
					if(ml.part.partInfo.title == selectedWeapon.GetPart().partInfo.title)
					{
						if(!CheckBombClearance(ml))
						{
							continue;
						}

						lastFiredSym = FindSym(ml.part);
						
						if(guardMode && guardTarget!=null && BDArmorySettings.ALLOW_LEGACY_TARGETING)
						{
							firedWeapon = ml;
							if(ml.missileTurret)
							{
								ml.missileTurret.PrepMissileForFire(ml);
								ml.FireMissileOnTarget(guardTarget);
								ml.missileTurret.UpdateMissileChildren();
							}
							else if(ml.rotaryRail)
							{
								ml.rotaryRail.PrepMissileForFire(ml);
								ml.FireMissileOnTarget(guardTarget);
								ml.rotaryRail.UpdateMissileChildren();
							}
							else
							{
								ml.FireMissileOnTarget(guardTarget);
							}
						}
						else
						{
							firedWeapon = ml;
							SendTargetDataToMissile(ml);
							if(ml.missileTurret)
							{
								ml.missileTurret.FireMissile(ml);
							}
							else if(ml.rotaryRail)
							{
								ml.rotaryRail.FireMissile(ml);
							}
							else
							{
								ml.FireMissile();
							}
						}
						firedMissile = true;
						if(lastFiredSym != null)
						{
							CurrentMissile = lastFiredSym.GetComponent<MissileBase>();
							selectedWeapon = CurrentMissile;
							SetMissileTurrets();
						}
						
						break;
					}
				}

                if(!firedMissile)
                {
                    foreach(RocketLauncher rl in vessel.FindPartModulesImplementing<RocketLauncher>())
                    {
                        bool hasRocket = false;
                        foreach(PartResource r in rl.part.Resources.list)
                        {
                            if(r.amount>0) hasRocket = true;
                        }	
                        
                        if(rl.part.partInfo.title == selectedWeapon.GetPart().partInfo.title && hasRocket)
                        {
                            lastFiredSym = FindSym(rl.part);
                            firedWeapon = rl;
                            rl.FireRocket();
                            //rippleRPM = rl.rippleRPM;

                            break;
                        }
                    }
                }
            }


            UpdateList();
            if(GetWeaponName(selectedWeapon) != GetWeaponName(firedWeapon))
            {
                hasSingleFired = true;
            }
            if(weaponIndex >= weaponArray.Length)
            {
                triggerTimer = 0;
                hasSingleFired = true;
                weaponIndex = Mathf.Clamp(weaponIndex, 0, weaponArray.Length - 1);
                
                DisplaySelectedWeaponMessage();

            }

            if(vesselRadarData && vesselRadarData.autoCycleLockOnFire)
            {
                vesselRadarData.CycleActiveLock();
            }
    
        }*/

        void FireMissile()
        {
            if (weaponIndex == 0)
            {
                return;
            }

            if (selectedWeapon == null)
            {
                return;
            }

            if (selectedWeapon.GetWeaponClass() == WeaponClasses.Missile ||
                selectedWeapon.GetWeaponClass() == WeaponClasses.SLW ||
                selectedWeapon.GetWeaponClass() == WeaponClasses.Bomb )
            {
                FireCurrentMissile(true);
            }
            else if (selectedWeapon.GetWeaponClass() == WeaponClasses.Rocket)
            {
                if (!currentRocket || currentRocket.part.name != selectedWeapon.GetPart().name)
                {
                    FindNextRocket(null);
                }

                if (currentRocket)
                {
                    currentRocket.FireRocket();
                    FindNextRocket(currentRocket);
                }
            }

            UpdateList();
        }


        #endregion

        #region Weapon Info

        void DisplaySelectedWeaponMessage()
        {
            if (BDArmorySettings.GAME_UI_ENABLED && vessel == FlightGlobals.ActiveVessel)
            {
                ScreenMessages.RemoveMessage(selectionMessage);
                selectionMessage.textInstance = null;

                selectionText = "Selected Weapon: " + (GetWeaponName(weaponArray[weaponIndex])).ToString();
                selectionMessage.message = selectionText;
                selectionMessage.style = ScreenMessageStyle.UPPER_CENTER;

                ScreenMessages.PostScreenMessage(selectionMessage);
            }
        }

        string GetWeaponName(IBDWeapon weapon)
        {
            if (weapon == null)
            {
                return "None";
            }
            else
            {
                return weapon.GetShortName();
            }
        }

        public void UpdateList()
        {
            weaponTypes.Clear();
            // extension for feature_engagementenvelope: also clear engagement specific weapon lists
            weaponTypesAir.Clear();
            weaponTypesMissile.Clear();
            weaponTypesGround.Clear();
            weaponTypesSLW.Clear();

            foreach (IBDWeapon weapon in vessel.FindPartModulesImplementing<IBDWeapon>())
            {
                string weaponName = weapon.GetShortName();
                bool alreadyAdded = false;
                foreach (var weap in weaponTypes)
                {
                    if (weap.GetShortName() == weaponName)
                    {
                        alreadyAdded = true;
                        //break;
                    }
                }

                //dont add empty rocket pods
                if (weapon.GetWeaponClass() == WeaponClasses.Rocket &&
                    weapon.GetPart().FindModuleImplementing<RocketLauncher>().GetRocketResource().amount < 1)
                {
                    continue;
                }

                if (!alreadyAdded)
                {
                    weaponTypes.Add(weapon);
                }

                var engageableWeapon = weapon as EngageableWeapon;

                if (engageableWeapon != null)
                {
                    if (engageableWeapon.GetEngageAirTargets()) weaponTypesAir.Add(weapon);
                    if (engageableWeapon.GetEngageMissileTargets()) weaponTypesMissile.Add(weapon);
                    if (engageableWeapon.GetEngageGroundTargets()) weaponTypesGround.Add(weapon);
                    if (engageableWeapon.GetEngageSLWTargets()) weaponTypesSLW.Add(weapon);
                }
                else
                {
                    weaponTypesAir.Add(weapon);
                    weaponTypesMissile.Add(weapon);
                    weaponTypesGround.Add(weapon);
                    weaponTypesSLW.Add(weapon);
                }  
            }

            //weaponTypes.Sort();
            weaponTypes = weaponTypes.OrderBy(w => w.GetShortName()).ToList();

            List<IBDWeapon> tempList = new List<IBDWeapon> {null};
            tempList.AddRange(weaponTypes);

            weaponArray = tempList.ToArray();

            if (weaponIndex >= weaponArray.Length)
            {
                hasSingleFired = true;
                triggerTimer = 0;
            }
            PrepareWeapons();
        }

        // EXTRACTED METHOD FROM UpdateList()
        private void PrepareWeapons()
        {
            weaponIndex = Mathf.Clamp(weaponIndex, 0, weaponArray.Length - 1);

            if (selectedWeapon == null || selectedWeapon.GetPart() == null || selectedWeapon.GetPart().vessel != vessel ||
                GetWeaponName(selectedWeapon) != GetWeaponName(weaponArray[weaponIndex]))
            {
                selectedWeapon = weaponArray[weaponIndex];

                if (vessel.isActiveVessel && Time.time - startTime > 1)
                {
                    hasSingleFired = true;
                }

                if (vessel.isActiveVessel && weaponIndex != 0)
                {
                    DisplaySelectedWeaponMessage();
                }
            }

            if (weaponIndex == 0)
            {
                selectedWeapon = null;
                hasSingleFired = true;
            }

            MissileBase aMl = GetAsymMissile();
            if (aMl)
            {                
                selectedWeapon = aMl;
                CurrentMissile = aMl;
            }

            MissileBase rMl = GetRotaryReadyMissile();
            if (rMl)
            {             
                selectedWeapon = rMl;
                CurrentMissile = rMl;
            }

            if (selectedWeapon != null && (selectedWeapon.GetWeaponClass() == WeaponClasses.Bomb || selectedWeapon.GetWeaponClass() == WeaponClasses.Missile || selectedWeapon.GetWeaponClass() == WeaponClasses.SLW))
            {
                //Debug.Log("[BDArmory]: =====selected weapon: " + selectedWeapon.GetPart().name);
                if (!CurrentMissile || CurrentMissile.part.name != selectedWeapon.GetPart().name)
                {
                    CurrentMissile = selectedWeapon.GetPart().FindModuleImplementing<MissileBase>();
                }
            }
            else
            {
                CurrentMissile = null;
            }

            //selectedWeapon = weaponArray[weaponIndex];

            //bomb stuff
            if (selectedWeapon != null && selectedWeapon.GetWeaponClass() == WeaponClasses.Bomb)
            {
                bombPart = selectedWeapon.GetPart();
            }
            else
            {
                bombPart = null;
            }

            //gun ripple stuff
            if (selectedWeapon != null && selectedWeapon.GetWeaponClass() == WeaponClasses.Gun &&
                currentGun.roundsPerMinute < 1500)
            {
                float counter = 0;
                float weaponRPM = 0;
                gunRippleIndex = 0;
                rippleGunCount = 0;
                List<ModuleWeapon> tempListModuleWeapon = vessel.FindPartModulesImplementing<ModuleWeapon>();
                foreach (ModuleWeapon weapon in tempListModuleWeapon)
                {
                    if (selectedWeapon.GetShortName() == weapon.GetShortName())
                    {
                        weapon.rippleIndex = Mathf.RoundToInt(counter);
                        weaponRPM = weapon.roundsPerMinute;
                        ++counter;
                        rippleGunCount++;
                    }
                }
                gunRippleRpm = weaponRPM * counter;
                float timeDelayPerGun = 60f / (weaponRPM * counter);
                //number of seconds between each gun firing; will reduce with increasing RPM or number of guns
                foreach (ModuleWeapon weapon in tempListModuleWeapon)
                {
                    if (selectedWeapon.GetShortName() == weapon.GetShortName())
                    {
                        weapon.initialFireDelay = timeDelayPerGun; //set the time delay for moving to next index
                    }
                }

                RippleOption ro; //ripplesetup and stuff
                if (rippleDictionary.ContainsKey(selectedWeapon.GetShortName()))
                {
                    ro = rippleDictionary[selectedWeapon.GetShortName()];
                }
                else
                {
                    ro = new RippleOption(currentGun.useRippleFire, 650); //take from gun's persistant value
                    rippleDictionary.Add(selectedWeapon.GetShortName(), ro);
                }

                foreach (ModuleWeapon w in vessel.FindPartModulesImplementing<ModuleWeapon>())
                {
                    if (w.GetShortName() == selectedWeapon.GetShortName())
                        w.useRippleFire = ro.rippleFire;
                }
            }

            //rocket
            FindNextRocket(null);

            ToggleTurret();
            SetMissileTurrets();
            SetRocketTurrets();
            SetRotaryRails();
        }

        private bool SetCargoBays()
        {
            if (!guardMode) return false;
            bool openingBays = false;

            if (weaponIndex > 0 && CurrentMissile && guardTarget && Vector3.Dot(guardTarget.transform.position - CurrentMissile.transform.position, CurrentMissile.GetForwardTransform()) > 0)
            {
                if (CurrentMissile.part.ShieldedFromAirstream)
                {
                    foreach (var ml in vessel.FindPartModulesImplementing<MissileBase>())
                    {
                        if (ml.part.ShieldedFromAirstream)
                        {
                            ml.inCargoBay = true;
                        }
                    }
                }

                if (CurrentMissile.inCargoBay)
                {
                    foreach (var bay in vessel.FindPartModulesImplementing<ModuleCargoBay>())
                    {
                        if (CurrentMissile.part.airstreamShields.Contains(bay))
                        {
                            ModuleAnimateGeneric anim = (ModuleAnimateGeneric)bay.part.Modules.GetModule(bay.DeployModuleIndex);

                            string toggleOption = anim.Events["Toggle"].guiName;
                            if (toggleOption == "Open")
                            {
                                if (anim)
                                {
                                    anim.Toggle();
                                    openingBays = true;
                                }
                            }
                        }
                        else
                        {
                            ModuleAnimateGeneric anim =
                                (ModuleAnimateGeneric)bay.part.Modules.GetModule(bay.DeployModuleIndex);

                            string toggleOption = anim.Events["Toggle"].guiName;
                            if (toggleOption == "Close")
                            {
                                if (anim)
                                {
                                    anim.Toggle();
                                }
                            }
                        }
                    }
                }
                else
                {
                    foreach (var bay in vessel.FindPartModulesImplementing<ModuleCargoBay>())
                    {
                        ModuleAnimateGeneric anim =
                            (ModuleAnimateGeneric)bay.part.Modules.GetModule(bay.DeployModuleIndex);
                        string toggleOption = anim.Events["Toggle"].guiName;
                        if (toggleOption == "Close")
                        {
                            if (anim)
                            {
                                anim.Toggle();
                            }
                        }
                    }
                }
            }
            else
            {
                foreach (var bay in vessel.FindPartModulesImplementing<ModuleCargoBay>())
                {
                    ModuleAnimateGeneric anim = (ModuleAnimateGeneric)bay.part.Modules.GetModule(bay.DeployModuleIndex);
                    string toggleOption = anim.Events["Toggle"].guiName;
                    if (toggleOption == "Close")
                    {
                        if (anim)
                        {
                            anim.Toggle();
                        }
                    }
                }
            }

            return openingBays;
        }

        void SetRotaryRails()
        {
            if (weaponIndex == 0) return;

            if (selectedWeapon == null) return;

            if (
                !(selectedWeapon.GetWeaponClass() == WeaponClasses.Missile ||
                  selectedWeapon.GetWeaponClass() == WeaponClasses.Bomb)) return;

            if (!CurrentMissile) return;

            //TODO BDModularGuidance: Rotatory Rail?
            var cm = CurrentMissile as MissileLauncher;
            if (cm != null)
            {
                foreach (var rotRail in vessel.FindPartModulesImplementing<BDRotaryRail>())
                {
                    if (rotRail.missileCount == 0)
                    {
                        //Debug.Log("SetRotaryRails(): rail has no missiles");
                        continue;
                    }

                    //Debug.Log("[BDArmory]: SetRotaryRails(): rotRail.readyToFire: " + rotRail.readyToFire + ", rotRail.readyMissile: " + ((rotRail.readyMissile != null) ? rotRail.readyMissile.part.name : "null") + ", rotRail.nextMissile: " + ((rotRail.nextMissile != null) ? rotRail.nextMissile.part.name : "null"));

                    //Debug.Log("[BDArmory]: current missile: " + cm.part.name);

                    if (rotRail.readyToFire)
                    {
                        if (!rotRail.readyMissile)
                        {
                            rotRail.RotateToMissile(cm);
                            return;
                        }

                        if (rotRail.readyMissile.part.name != cm.part.name)
                        {
                            rotRail.RotateToMissile(cm);
                        }
                    }
                    else
                    {
                        if (!rotRail.nextMissile)
                        {
                            rotRail.RotateToMissile(cm);
                        }
                        else if (rotRail.nextMissile.part.name != cm.part.name)
                        {
                            rotRail.RotateToMissile(cm);
                        }
                    }

                    /*
                    if((rotRail.readyToFire && (!rotRail.ReadyMissile || rotRail.ReadyMissile.part.name!=cm.part.name)) || (!rotRail.NextMissile || rotRail.NextMissile.part.name!=cm.part.name))
                    {
                        rotRail.RotateToMissile(cm);
                    }
                    */
                }
            }

        }

        void SetMissileTurrets()
        {
            var cm = CurrentMissile as MissileLauncher;
            if (cm != null)
            {

                foreach (var mt in vessel.FindPartModulesImplementing<MissileTurret>())
                {
                    if (weaponIndex > 0 && cm && mt.ContainsMissileOfType(cm))
                    {
                        if (!mt.activeMissileOnly || cm.missileTurret == mt)
                        {
                            mt.EnableTurret();
                        }
                        else
                        {
                            mt.DisableTurret();
                        }
                    }
                    else
                    {
                        mt.DisableTurret();
                    }
                }
            }
        }

        void SetRocketTurrets()
        {
            RocketLauncher currentTurret = null;
            if (selectedWeapon != null && selectedWeapon.GetWeaponClass() == WeaponClasses.Rocket)
            {
                RocketLauncher rl = selectedWeapon.GetPart().FindModuleImplementing<RocketLauncher>();
                if (rl && rl.turret)
                {
                    currentTurret = rl;
                }
            }

            foreach (var rl in vessel.FindPartModulesImplementing<RocketLauncher>())
            {
                rl.weaponManager = this;
                if (rl.turret)
                {
                    if (currentTurret && rl.part.name == currentTurret.part.name)
                    {
                        rl.EnableTurret();
                    }
                    else
                    {
                        rl.DisableTurret();
                    }
                }
            }
        }

        void FindNextRocket(RocketLauncher lastFired)
        {
            if (weaponIndex > 0 && selectedWeapon.GetWeaponClass() == WeaponClasses.Rocket)
            {
                disabledRocketAimers = false;

                //first check sym of last fired
                if (lastFired && lastFired.part.name == selectedWeapon.GetPart().name)
                {
                    foreach (Part pSym in lastFired.part.symmetryCounterparts)
                    {
                        RocketLauncher rl = pSym.FindModuleImplementing<RocketLauncher>();
                        bool hasRocket = false;
                        foreach (PartResource r in rl.part.Resources)
                        {
                            if (r.resourceName == rl.rocketType && r.amount > 0)
                            {
                                hasRocket = true;
                                break;
                            }
                        }

                        if (hasRocket)
                        {
                            if (currentRocket) currentRocket.drawAimer = false;

                            rl.drawAimer = true;
                            currentRocket = rl;
                            selectedWeapon = currentRocket;
                            return;
                        }
                    }
                }

                if (!lastFired && currentRocket && currentRocket.part.name == selectedWeapon.GetPart().name)
                {
                    currentRocket.drawAimer = true;
                    selectedWeapon = currentRocket;
                    return;
                }

                //then check for other rocket
                bool foundRocket = false;
                foreach (RocketLauncher rl in vessel.FindPartModulesImplementing<RocketLauncher>())
                {
                    if (!foundRocket && rl.part.partInfo.title == selectedWeapon.GetPart().partInfo.title)
                    {
                        bool hasRocket = false;
                        foreach (PartResource r in rl.part.Resources)
                        {
                            if (r.amount > 0) hasRocket = true;
                            else
                            {
                                rl.drawAimer = false;
                            }
                        }

                        if (!hasRocket) continue;

                        if (currentRocket != null) currentRocket.drawAimer = false;
                        rl.drawAimer = true;
                        currentRocket = rl;
                        selectedWeapon = currentRocket;
                        //return;
                        foundRocket = true;
                    }
                    else
                    {
                        rl.drawAimer = false;
                    }
                }
            }
            //not using a rocket, disable reticles.
            else if (!disabledRocketAimers)
            {
                foreach (RocketLauncher rl in vessel.FindPartModulesImplementing<RocketLauncher>())
                {
                    rl.drawAimer = false;
                    currentRocket = null;
                }
                disabledRocketAimers = true;
            }
        }

        public void CycleWeapon(bool forward)
        {
            if (forward) weaponIndex++;
            else weaponIndex--;
            weaponIndex = (int)Mathf.Repeat(weaponIndex, weaponArray.Length);

            hasSingleFired = true;
            triggerTimer = 0;

            UpdateList();

            DisplaySelectedWeaponMessage();

            if (vessel.isActiveVessel && !guardMode)
            {
                audioSource.PlayOneShot(clickSound);
            }
        }

        public void CycleWeapon(int index)
        {
            if (index >= weaponArray.Length)
            {
                index = 0;
            }
            weaponIndex = index;

            UpdateList();

            if (vessel.isActiveVessel && !guardMode)
            {
                audioSource.PlayOneShot(clickSound);

                DisplaySelectedWeaponMessage();
            }
        }

        public Part FindSym(Part p)
        {
            foreach (Part pSym in p.symmetryCounterparts)
            {
                if (pSym != p && pSym.vessel == vessel)
                {
                    return pSym;
                }
            }

            return null;
        }

        private MissileBase GetAsymMissile()
        {
            if (weaponIndex == 0) return null;
            if (weaponArray[weaponIndex].GetWeaponClass() == WeaponClasses.Bomb ||
                weaponArray[weaponIndex].GetWeaponClass() == WeaponClasses.Missile ||
                weaponArray[weaponIndex].GetWeaponClass() ==WeaponClasses.SLW)
            {
                MissileBase firstMl = null;

                foreach (var ml in vessel.FindPartModulesImplementing<MissileBase>())
                {
                    var launcher = ml as MissileLauncher;
                    if (launcher != null)
                    {
                        if (launcher.part.name != weaponArray[weaponIndex].GetPart()?.name) continue;
                    }
                    else
                    {
                        var guidance = ml as BDModularGuidance;
                        if (guidance != null)
                        { //We have set of parts not only a part
                            if (guidance.GetShortName() != weaponArray[weaponIndex]?.GetShortName()) continue;
                        }
                    }
                    if (firstMl == null) firstMl = ml;

                    if (!FindSym(ml.part))
                    {
                        return ml;
                    }
                }
                return firstMl;
            }
            return null;
        }

        private MissileBase GetRotaryReadyMissile()
        {
            if (weaponIndex == 0) return null;
            if (weaponArray[weaponIndex].GetWeaponClass() == WeaponClasses.Bomb ||
                weaponArray[weaponIndex].GetWeaponClass() == WeaponClasses.Missile)
            {
                //TODO BDModularGuidance: Implemente rotaryRail support
                var missile = CurrentMissile as MissileLauncher;
                if (missile != null)
                {
                    if (missile && missile.part.name == weaponArray[weaponIndex].GetPart().name)
                    {
                        if (!missile.rotaryRail)
                        {
                            return missile;
                        }
                        if (missile.rotaryRail.readyToFire && missile.rotaryRail.readyMissile == CurrentMissile)
                        {
                            return missile;
                        }
                    }

                    foreach (var ml in vessel.FindPartModulesImplementing<MissileLauncher>())
                    {
                        if (ml.part.name != weaponArray[weaponIndex].GetPart().name) continue;

                        if (!ml.rotaryRail)
                        {
                            return ml;
                        }
                        if (ml.rotaryRail.readyToFire && ml.rotaryRail.readyMissile.part.name == weaponArray[weaponIndex].GetPart().name)
                        {
                            return ml.rotaryRail.readyMissile;
                        }
                    }
                }
                return null;
            }
            return null;
        }

        bool CheckBombClearance(MissileBase ml)
        {
            if (!BDArmorySettings.BOMB_CLEARANCE_CHECK) return true;

            if (ml.part.ShieldedFromAirstream)
            {
                /*
				foreach(var shield in ml.part.airstreamShields)
				{
					
					if(shield.GetType() == typeof(ModuleCargoBay))
					{
						ModuleCargoBay bay = (ModuleCargoBay)shield;
						ModuleAnimateGeneric anim = (ModuleAnimateGeneric) bay.part.Modules.GetModule(bay.DeployModuleIndex);
						if(anim && anim.Events["Toggle"].guiName == "Close")
						{
							return true;
						}
					}
				}
				*/
                return false;
            }

            //TODO BDModularGuidance: Bombs and turrents
            var launcher = ml as MissileLauncher;
            if (launcher != null)
            {
                if (launcher.rotaryRail && launcher.rotaryRail.readyMissile != ml)
                {
                    return false;
                }

                if (launcher.missileTurret && !launcher.missileTurret.turretEnabled)
                {
                    return false;
                }

                if (ml.dropTime > 0.3f)
                {
                    //debug lines
                    LineRenderer lr = null;
                    if (BDArmorySettings.DRAW_DEBUG_LINES && BDArmorySettings.DRAW_AIMERS)
                    {
                        lr = GetComponent<LineRenderer>();
                        if (!lr)
                        {
                            lr = gameObject.AddComponent<LineRenderer>();
                        }
                        lr.enabled = true;
                        lr.SetWidth(.1f, .1f);
                    }
                    else
                    {
                        if (gameObject.GetComponent<LineRenderer>())
                        {
                            gameObject.GetComponent<LineRenderer>().enabled = false;
                        }
                    }


                    float radius = 0.28f / 2;
                    float time = ml.dropTime;
                    Vector3 direction = ((launcher.decoupleForward
                        ? ml.MissileReferenceTransform.transform.forward
                        : -ml.MissileReferenceTransform.transform.up) * launcher.decoupleSpeed * time) +
                                        ((FlightGlobals.getGeeForceAtPosition(transform.position) - vessel.acceleration) *
                                         0.5f * time * time);
                    Vector3 crossAxis = Vector3.Cross(direction, ml.GetForwardTransform()).normalized;

                    float rayDistance;
                    if (launcher.thrust == 0 || launcher.cruiseThrust == 0)
                    {
                        rayDistance = 8;
                    }
                    else
                    {
                        //distance till engine starts based on grav accel and vessel accel
                        rayDistance = direction.magnitude;
                    }

                    Ray[] rays =
                    {
                        new Ray(ml.MissileReferenceTransform.position - (radius*crossAxis), direction),
                        new Ray(ml.MissileReferenceTransform.position + (radius*crossAxis), direction),
                        new Ray(ml.MissileReferenceTransform.position, direction)
                    };

                    if (lr)
                    {
                        lr.useWorldSpace = false;
                        lr.SetVertexCount(4);
                        lr.SetPosition(0, transform.InverseTransformPoint(rays[0].origin));
                        lr.SetPosition(1, transform.InverseTransformPoint(rays[0].GetPoint(rayDistance)));
                        lr.SetPosition(2, transform.InverseTransformPoint(rays[1].GetPoint(rayDistance)));
                        lr.SetPosition(3, transform.InverseTransformPoint(rays[1].origin));
                    }

                    for (int i = 0; i < rays.Length; i++)
                    {
                        RaycastHit[] hits = Physics.RaycastAll(rays[i], rayDistance, 557057);
                        for (int h = 0; h < hits.Length; h++)
                        {
                            Part p = hits[h].collider.GetComponentInParent<Part>();

                            if ((p != null && p != ml.part) || p == null)
                            {
                                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                                    Debug.Log("[BDArmory]: RAYCAST HIT, clearance is FALSE! part=" + p?.name + ", collider=" + p?.collider.ToString());
                                return false;
                            }
                        }
                    }

                    return true;
                }


                //forward check for no-drop missiles
                RaycastHit[] hitparts = Physics.RaycastAll(new Ray(ml.MissileReferenceTransform.position, ml.GetForwardTransform()), 50, 557057);
                for (int h = 0; h < hitparts.Length; h++)
                {
                    Part p = hitparts[h].collider.GetComponentInParent<Part>();
                    if ((p != null && p != ml.part) || p == null)
                    {

                        if (BDArmorySettings.DRAW_DEBUG_LABELS)
                            Debug.Log("[BDArmory]: RAYCAST HIT, clearance is FALSE! part=" + p?.name + ", collider=" + p?.collider.ToString());
                        return false;
                    }
                }


            }

            return true;
        }

        void RefreshTargetingModules()
        {
            foreach (var rad in radars)
            {
                rad.EnsureVesselRadarData();
                if (rad.radarEnabled)
                {
                    rad.EnableRadar();
                }
            }
            radars = vessel.FindPartModulesImplementing<ModuleRadar>();
            foreach (var rad in radars)
            {
                rad.EnsureVesselRadarData();
                if (rad.radarEnabled)
                {
                    rad.EnableRadar();
                }
            }
            jammers = vessel.FindPartModulesImplementing<ModuleECMJammer>();
            targetingPods = vessel.FindPartModulesImplementing<ModuleTargetingCamera>();
        }

        #endregion

        #region Weapon Choice

        bool TryPickAntiRad(TargetInfo target)
        {
            CycleWeapon(0); //go to start of array
            while (true)
            {
                CycleWeapon(true);
                if (selectedWeapon == null) return false;
                if (selectedWeapon.GetWeaponClass() == WeaponClasses.Missile)
                {
                    foreach (var ml in selectedWeapon.GetPart().FindModulesImplementing<MissileBase>())
                    {
                        if (ml.TargetingMode == MissileBase.TargetingModes.AntiRad)
                        {
                            return true;
                        }
                        break;
                    }
                    //return;
                }
            }
        }
        #endregion

        #region Targeting

        #region Smart Targeting
            
        void SmartFindTarget()
        {
            List<TargetInfo> targetsTried = new List<TargetInfo>();

            if (overrideTarget) //begin by checking the override target, since that takes priority
            {
                targetsTried.Add(overrideTarget);
                SetTarget(overrideTarget);
                if (SmartPickWeapon_EngagementEnvelope(overrideTarget))
                {
                    if (BDArmorySettings.DRAW_DEBUG_LABELS)
                    {
                        Debug.Log("[BDArmory]: " + vessel.vesselName + " is engaging an override target with " + selectedWeapon);
                    }
                    overrideTimer = 15f;
                    //overrideTarget = null;
                    return;
                }
                else if (BDArmorySettings.DRAW_DEBUG_LABELS)
                {
                    Debug.Log("[BDArmory]: " + vessel.vesselName + " is engaging an override target with failed to engage its override target!");
                }
            }
            overrideTarget = null; //null the override target if it cannot be used

            //if AIRBORNE, try to engage airborne target first
            if (!vessel.LandedOrSplashed && !targetMissiles)
            {
                if (pilotAI && pilotAI.IsExtending)
                {
                    TargetInfo potentialAirTarget = BDATargetManager.GetAirToAirTargetAbortExtend(this, 1500, 0.2f);
                    if (potentialAirTarget)
                    {
                        targetsTried.Add(potentialAirTarget);
                        SetTarget(potentialAirTarget);
                        if (SmartPickWeapon_EngagementEnvelope(potentialAirTarget))
                        {
                            if (BDArmorySettings.DRAW_DEBUG_LABELS)
                            {
                                Debug.Log("[BDArmory]: " + vessel.vesselName + " is aborting extend and engaging an incoming airborne target with " + selectedWeapon);
                            }
                            return;
                        }
                    }
                }
                else
                {
                    TargetInfo potentialAirTarget = BDATargetManager.GetAirToAirTarget(this);
                    if (potentialAirTarget)
                    {
                        targetsTried.Add(potentialAirTarget);
                        SetTarget(potentialAirTarget);
                        if (SmartPickWeapon_EngagementEnvelope(potentialAirTarget))
                        {
                            if (BDArmorySettings.DRAW_DEBUG_LABELS)
                            {
                                Debug.Log("[BDArmory]: " + vessel.vesselName + " is engaging an airborne target with " + selectedWeapon);
                            }
                            return;
                        }
                    }
                }
            }

            TargetInfo potentialTarget = null;
            //=========HIGH PRIORITY MISSILES=============
            //first engage any missiles targeting this vessel
            potentialTarget = BDATargetManager.GetMissileTarget(this, true);
            if (potentialTarget)
            {
                targetsTried.Add(potentialTarget);
                SetTarget(potentialTarget);
                if (SmartPickWeapon_EngagementEnvelope(potentialTarget))
                {
                    if (BDArmorySettings.DRAW_DEBUG_LABELS)
                    {
                        Debug.Log("[BDArmory]: " + vessel.vesselName + " is engaging incoming missile with " + selectedWeapon);
                    }
                    return;
                }
            }

            //then engage any missiles that are not engaged
            potentialTarget = BDATargetManager.GetUnengagedMissileTarget(this);
            if (potentialTarget)
            {
                targetsTried.Add(potentialTarget);
                SetTarget(potentialTarget);
                if (SmartPickWeapon_EngagementEnvelope(potentialTarget))
                {
                    if (BDArmorySettings.DRAW_DEBUG_LABELS)
                    {
                        Debug.Log("[BDArmory]: " + vessel.vesselName + " is engaging unengaged missile with " + selectedWeapon);
                    }
                    return;
                }
            }


            //=========END HIGH PRIORITY MISSILES=============

            //============VESSEL THREATS============
            if (!targetMissiles)
            {
                //then try to engage enemies with least friendlies already engaging them 
                potentialTarget = BDATargetManager.GetLeastEngagedTarget(this);
                if (potentialTarget)
                {
                    targetsTried.Add(potentialTarget);
                    SetTarget(potentialTarget);
                    if (!BDArmorySettings.ALLOW_LEGACY_TARGETING)
                    {
                        if (CrossCheckWithRWR(potentialTarget) && TryPickAntiRad(potentialTarget))
                        {
                            if (BDArmorySettings.DRAW_DEBUG_LABELS)
                            {
                                Debug.Log("[BDArmory]: " + vessel.vesselName + " is engaging the least engaged radar target with " +
                                          selectedWeapon.GetShortName());
                            }
                            return;
                        }
                    }
                    if (SmartPickWeapon_EngagementEnvelope(potentialTarget))
                    {
                        if (BDArmorySettings.DRAW_DEBUG_LABELS)
                        {
                            Debug.Log("[BDArmory]: " + vessel.vesselName + " is engaging the least engaged target with " +
                                      selectedWeapon.GetShortName());
                        }
                        return;
                    }
                }

                //then engage the closest enemy
                potentialTarget = BDATargetManager.GetClosestTarget(this);
                if (potentialTarget)
                {
                    targetsTried.Add(potentialTarget);
                    SetTarget(potentialTarget);
                    if (!BDArmorySettings.ALLOW_LEGACY_TARGETING)
                    {
                        if (CrossCheckWithRWR(potentialTarget) && TryPickAntiRad(potentialTarget))
                        {
                            if (BDArmorySettings.DRAW_DEBUG_LABELS)
                            {
                                Debug.Log("[BDArmory]: " + vessel.vesselName + " is engaging the closest radar target with " +
                                          selectedWeapon.GetShortName());
                            }
                            return;
                        }
                    }
                    if (SmartPickWeapon_EngagementEnvelope(potentialTarget))
                    {
                        if (BDArmorySettings.DRAW_DEBUG_LABELS)
                        {
                            Debug.Log("[BDArmory]: " + vessel.vesselName + " is engaging the closest target with " +
                                      selectedWeapon.GetShortName());
                        }
                        return;
                    }
                    /*
                    else
                    {
                        if(SmartPickWeapon(potentialTarget, 10000))
                        {
                            if(BDArmorySettings.DRAW_DEBUG_LABELS)
                            {
                                Debug.Log("[BDArmory]:"  + vessel.vesselName + " is engaging the closest target with extended turret range (" + selectedWeapon.GetShortName() + ")");
                            }
                            return;
                        }
                    }
                    */
                }
            }
            //============END VESSEL THREATS============


            //============LOW PRIORITY MISSILES=========
            //try to engage least engaged hostile missiles first
            potentialTarget = BDATargetManager.GetMissileTarget(this);
            if (potentialTarget)
            {
                targetsTried.Add(potentialTarget);
                SetTarget(potentialTarget);
                if (SmartPickWeapon_EngagementEnvelope(potentialTarget))
                {
                    if (BDArmorySettings.DRAW_DEBUG_LABELS)
                    {
                        Debug.Log("[BDArmory]:" + vessel.vesselName + " is engaging a missile with " + selectedWeapon.GetShortName());
                    }
                    return;
                }
            }

            //then try to engage closest hostile missile
            potentialTarget = BDATargetManager.GetClosestMissileTarget(this);
            if (potentialTarget)
            {
                targetsTried.Add(potentialTarget);
                SetTarget(potentialTarget);
                if (SmartPickWeapon_EngagementEnvelope(potentialTarget))
                {
                    if (BDArmorySettings.DRAW_DEBUG_LABELS)
                    {
                        Debug.Log("[BDArmory]:" + vessel.vesselName + " is engaging a missile with " + selectedWeapon.GetShortName());
                    }
                    return;
                }
            }
            //==========END LOW PRIORITY MISSILES=============

            if (targetMissiles) //NO MISSILES BEYOND THIS POINT//
            {
                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                {
                    Debug.Log("[BDArmory]:" + vessel.vesselName + " is disengaging - no valid weapons");
                }
                CycleWeapon(0);
                SetTarget(null);
                return;
            }

            //if nothing works, get all remaining targets and try weapons against them
            List<TargetInfo> finalTargets = BDATargetManager.GetAllTargetsExcluding(targetsTried, this);
            foreach (TargetInfo finalTarget in finalTargets)
            {
                SetTarget(finalTarget);
                if (SmartPickWeapon_EngagementEnvelope(finalTarget))
                {
                    if (BDArmorySettings.DRAW_DEBUG_LABELS)
                    {
                        Debug.Log("[BDArmory]: " + vessel.vesselName + " is engaging a final target with " +
                                  selectedWeapon.GetShortName());
                    }
                    return;
                }
            }


            //no valid targets found
            if (potentialTarget == null || selectedWeapon == null)
            {
                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                {
                    Debug.Log("[BDArmory]: " + vessel.vesselName + " is disengaging - no valid weapons - no valid targets");
                }
                CycleWeapon(0);
                SetTarget(null);
                if (vesselRadarData && vesselRadarData.locked)
                {
                    vesselRadarData.UnlockAllTargets();
                }
                return;
            }

            Debug.Log("[BDArmory]: Unhandled target case");
        }

        // OLD method without engagement envelope -> new method see: SmartPickWeapon_EngageEnvelope
        #region DEPRECATED
        /*
        bool SmartPickWeapon(TargetInfo target, float turretRange)
        {
            if (!target) return false;
            if (pilotAI && pilotAI.pilotEnabled && vessel.LandedOrSplashed) return false;
  
            float distance = Vector3.Distance(transform.position + vessel.srf_velocity,
                target.position + target.velocity); //take velocity into account (test)

            //Debug.Log("[BDArmory]: " + vessel.vesselName + " SmartPickWeapon: dist=" + distance + ", turretRange=" + turretRange + ", targetMissile=" + target.isMissile);

            if (distance < turretRange || (target.isMissile && distance < turretRange * 1.5f))
            {
                if ((target.isMissile) && SwitchToLaser()) //need to favor ballistic for ground units
                {
                    return true;
                }

                if (!targetMissiles && !vessel.LandedOrSplashed && target.isMissile)
                {
                    return false;
                }

                if (SwitchToTurret(distance))
                {
                    //dont fire on missiles if airborne unless equipped with laser
                    return true;
                }
            }

            if (distance > turretRange || !vessel.LandedOrSplashed)
            {
                //missiles
                if (!target.isLanded)
                {
                    if (!targetMissiles && target.isMissile && !vessel.LandedOrSplashed)
                    {
                        //don't fire on missiles if airborne
                        return false;
                    }

                    if (SwitchToAirMissile())
                    { //Use missiles if available
                        if (CurrentMissile.TargetingMode == MissileBase.TargetingModes.Radar)
                        {
                            foreach (var rd in radars)
                            {
                                if (rd.canLock)
                                {
                                    rd.EnableRadar();
                                    break;
                                }
                            }
                        }
                        return true;
                    }
                    //return SwitchToTurret(distance); //Long range turrets?
                    return false;
                }
                else
                {

                    if (target.isMissile)
                    {
                        //TRY to pick guns or aa missiles for defense
                        Debug.Log(vessel.vesselName + ": Trying to pick AA missile for Defense...");
                        if (SwitchToAirMissile())
                        {
                            if (CurrentMissile.TargetingMode == MissileBase.TargetingModes.Radar)
                            {
                                foreach (var rd in radars)
                                {
                                    if (rd.canLock)
                                    {
                                        rd.EnableRadar();
                                        break;
                                    }
                                }
                            }
                            return true;
                        }
                        else
                            SwitchToTurret(distance);
                    }

                    if (target.Vessel.LandedOrSplashed)
                    {

                        if (SwitchToGroundMissile())
                        {
                            return true;
                        }
                        else if (SwitchToBomb())
                        {
                            return true;
                        }
                    }

                    SwitchToTurret(distance);
                }
            }

            return false;
        }
        */
        #endregion

        // extension for feature_engagementenvelope: new smartpickweapon method
        bool SmartPickWeapon_EngagementEnvelope(TargetInfo target)
        {
            // Part 1: Guard conditions (when not to pick a weapon)
            // ------
            if (!target)
                return false;

            if (pilotAI && pilotAI.pilotEnabled && vessel.LandedOrSplashed) // This must be changed once pilots for ground/ships etc exist!
                return false;

            // Part 2: check weapons against individual target types
            // ------

            float distance = Vector3.Distance(transform.position + vessel.srf_velocity, target.position + target.velocity);
            IBDWeapon targetWeapon = null;
            float targetWeaponRPM = 0;
            float targetWeaponTDPS = 0;
            float targetWeaponImpact = 0;

            if (target.isMissile)
            {
                // iterate over weaponTypesMissile and pick suitable one based on engagementRange (and dynamic launch zone for missiles)
                // Prioritize by:
                // 1. Lasers
                // 2. Guns
                // 3. AA missiles
                foreach (var item in weaponTypesMissile)
                {

                    // candidate, check engagement envelope
                    if (CheckEngagementEnvelope(item, distance))
                    {
                        // weapon usable, if missile continue looking for lasers/guns, else take it
                        var candidateClass = item.GetWeaponClass();

                        if (candidateClass == WeaponClasses.DefenseLaser)
                        {
                            // TODO: compare lasers which one is better for AA
                            targetWeapon = item;
                            break; //always favour laser
                        }

                        if (candidateClass == WeaponClasses.Gun)
                        {
                            // For AAA, favour higher RPM
                            var candidateRPM = ((ModuleWeapon)item).roundsPerMinute;

                            if ((targetWeapon != null) && (targetWeaponRPM > candidateRPM))
                                continue; //dont replace better guns (but do replace missiles)

                            targetWeapon = item;
                            targetWeaponRPM = candidateRPM;
                        }

                        if (candidateClass == WeaponClasses.Missile)
                        {
                            // TODO: for AA, favour higher thrust+turnDPS

                            var mlauncher = item as MissileLauncher;
                            var candidateTDPS = 0f;

                            if (mlauncher != null)
                            {
                                candidateTDPS = mlauncher.thrust + mlauncher.maxTurnRateDPS;
                            }
                            else
                            { 
                                //is modular missile
                                var mm = item as BDModularGuidance;
                                candidateTDPS = 5000;
                            }

                            if ((targetWeapon != null) && ((targetWeapon.GetWeaponClass() == WeaponClasses.Gun) || (targetWeaponTDPS > candidateTDPS)))
                                continue; //dont replace guns or better missiles

                            targetWeapon = item;
                            targetWeaponTDPS = candidateTDPS;
                        }

                    }

                }

            }

            //else if (!target.isLanded)
            else if (target.isFlying)
            {
                // iterate over weaponTypesAir and pick suitable one based on engagementRange (and dynamic launch zone for missiles)
                // Prioritize by:
                // 1. AA missiles, if range > gunRange
                // 1. Lasers
                // 2. Guns
                // 
                foreach (var item in weaponTypesAir)
                {
                    // candidate, check engagement envelope
                    if (CheckEngagementEnvelope(item, distance))
                    {
                        // weapon usable, if missile continue looking for lasers/guns, else take it
                        var candidateClass = item.GetWeaponClass();

                        if (candidateClass == WeaponClasses.DefenseLaser)
                        {
                            // TODO: compare lasers which one is better for AA
                            targetWeapon = item;
                            if (distance <= gunRange)
                                break;
                        }

                        if (candidateClass == WeaponClasses.Gun)
                        {
                            // For AAA, favour higher RPM
                            var candidateRPM = ((ModuleWeapon)item).roundsPerMinute;

                            if ((targetWeapon != null) && (targetWeaponRPM > candidateRPM))
                                continue; //dont replace better guns (but do replace missiles)

                            targetWeapon = item;
                            targetWeaponRPM = candidateRPM;
                        }

                        if (candidateClass == WeaponClasses.Missile)
                        {
                            var mlauncher = item as MissileLauncher;
                            var candidateTDPS = 0f;

                            if (mlauncher != null)
                            {
                                candidateTDPS = mlauncher.thrust + mlauncher.maxTurnRateDPS;
                            }
                            else
                            { //is modular missile
                                var mm = item as BDModularGuidance;
                                candidateTDPS = 5000;
                            }

                            if (targetWeapon == null)
                            {
                                targetWeapon = item;
                                targetWeaponTDPS = candidateTDPS;
                            }
                            else if ((targetWeapon != null) && (distance > gunRange))
                            {
                                if ((targetWeapon != null) && ((targetWeapon.GetWeaponClass() == WeaponClasses.Gun) || (targetWeaponTDPS > candidateTDPS)))
                                    continue; //dont replace guns or better missiles

                                targetWeapon = item;
                                targetWeaponTDPS = candidateTDPS;
                            }
                        }

                    }

                }

            }

            else if (target.isLanded)
            {
                // iterate over weaponTypesGround and pick suitable one based on engagementRange (and dynamic launch zone for missiles)
                // Prioritize by:
                // 1. ground attack missiles (cruise, gps, unguided) if target not moving
                // 2. ground attack missiles (guided) if target is moving
                // 3. Bombs / Rockets
                // 4. Guns
                foreach (var item in weaponTypesGround)
                {
                    // candidate, check engagement envelope
                    if (CheckEngagementEnvelope(item, distance))
                    {
                        // weapon usable, if missile continue looking for lasers/guns, else take it
                        var candidateClass = item.GetWeaponClass();

                        if (candidateClass == WeaponClasses.Missile)
                        {
                            // TODO: compare missiles which one is better for ground attack
                            // Priority Sequence:
                            // - Antiradiation
                            // - guided missiles
                            // - by blast strength
                            targetWeapon = item;
                            if (distance > gunRange)
                                break;  //definitely use missiles
                        }

                        if (candidateClass == WeaponClasses.Bomb)
                        {
                            // only useful if we are flying
                            if (!vessel.LandedOrSplashed)
                            {
                                if ((targetWeapon != null) && (targetWeapon.GetWeaponClass() == WeaponClasses.Bomb))
                                    // dont replace bombs
                                    break;
                                else
                                    // TODO: compare bombs which one is better for ground attack
                                    // Priority Sequence:
                                    // - guided (JDAM)
                                    // - by blast strength
                                    targetWeapon = item;
                            }
                        }

                        if (candidateClass == WeaponClasses.Rocket)
                        {

                            if ((targetWeapon != null) && (targetWeapon.GetWeaponClass() == WeaponClasses.Bomb))
                                // dont replace bombs
                                continue;
                            else
                                // TODO: compare bombs which one is better for ground attack
                                // Priority Sequence:
                                // - by blast strength
                                targetWeapon = item;
                        }


                        if ((candidateClass == WeaponClasses.Gun))
                        {
                            // Flying: prefer bombs/rockets/missiles
                            if (!vessel.LandedOrSplashed)
                                if (targetWeapon != null)
                                    // dont replace bombs/rockets
                                    continue;
                            // else:
                            if ((distance > gunRange) && (targetWeapon != null))
                                continue;
                            else
                            {
                                // For Ground Attack, favour higher blast strength
                                var candidateImpact = ((ModuleWeapon)item).cannonShellPower * ((ModuleWeapon)item).cannonShellRadius + ((ModuleWeapon)item).cannonShellHeat;

                                if ((targetWeapon != null) && (targetWeaponImpact > candidateImpact))
                                    continue; //dont replace better guns

                                targetWeapon = item;
                                targetWeaponImpact = candidateImpact;
                            }
                        }

                    }

                }

                //if we have a torpedo use it
                if (target.isSplashed)
                foreach (var item in weaponTypesSLW)
                {
                    if (CheckEngagementEnvelope(item, distance))
                    {
                        var candidateClass = item.GetWeaponClass();
                        if (item.GetMissileType().ToLower() == "torpedo") targetWeapon = item;                        

                    }
                }

            }

            if (target.isUnderwater)
            {
                // iterate over weaponTypesSLW (Ship Launched Weapons) and pick suitable one based on engagementRange
                // Prioritize by:
                // 1. Depth Charges
                // 2. Torpedos

                foreach (var item in weaponTypesSLW)
                {
                    if (CheckEngagementEnvelope(item, distance))
                    {
                        var candidateClass = item.GetWeaponClass();
                        
                        if(item.GetMissileType().ToLower() == "depthcharge") targetWeapon = item;
                        else targetWeapon = item;

                    }
                }
            }

            // return result of weapon selection
            if (targetWeapon != null)
            {
                //update the legacy lists & arrays, especially selectedWeapon and weaponIndex
                selectedWeapon = targetWeapon;
                // find it in weaponArray
                for (int i = 1; i < weaponArray.Length; i++)
                {
                    weaponIndex = i;
                    if (selectedWeapon.GetShortName() == weaponArray[weaponIndex].GetShortName())
                    {
                        break;
                    }
                }

                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                {
                    Debug.Log("[BDArmory] : " + vessel.vesselName + " - Selected weapon " + selectedWeapon.GetShortName());
                }

                PrepareWeapons();
                DisplaySelectedWeaponMessage();
                return true;
            }
            else
            {

                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                {
                    Debug.Log("[BDArmory] : " + vessel.vesselName + " - No weapon selected.");
                }

                selectedWeapon = null;
                weaponIndex = 0;
                return false;
            }

        }

        // extension for feature_engagementenvelope: check engagement parameters of the weapon if it can be used against the current target
        bool CheckEngagementEnvelope(IBDWeapon weaponCandidate, float distanceToTarget)
        {
            if (BDArmorySettings.DRAW_DEBUG_LABELS)
            {
                Debug.Log("[BDArmory] : " + vessel.vesselName + " - Checking engagement envelope of " + weaponCandidate.GetShortName());
            }

            var engageableWeapon = weaponCandidate as EngageableWeapon;

            if (engageableWeapon == null) return true;
            if (distanceToTarget < engageableWeapon.GetEngagementRangeMin()) return false;
            if (distanceToTarget > engageableWeapon.GetEngagementRangeMax()) return false;

            switch (weaponCandidate.GetWeaponClass())
            {

                case WeaponClasses.DefenseLaser:
                // TODO: is laser treated like a gun?

                case WeaponClasses.Gun:
                    var gun = (ModuleWeapon)weaponCandidate;

                    // check yaw range of turret
                    var turret = gun.turret;
                    float gimbalTolerance = vessel.LandedOrSplashed ? 0 : 15;
                    if (turret != null)
                        if (!TargetInTurretRange(turret, gimbalTolerance))
                            return false;

                    // check overheat
                    if (gun.isOverheated)
                        return false;

                    // check ammo
                    if (CheckAmmo(gun))
                    {

                        if (BDArmorySettings.DRAW_DEBUG_LABELS)
                        {
                            Debug.Log("[BDArmory] : " + vessel.vesselName + " - Firing possible with " + weaponCandidate.GetShortName());
                        }
                        return true;
                    }
                    break;


                case WeaponClasses.Missile:
                    var ml = (MissileBase)weaponCandidate;

                    // lock radar if needed
                    if (ml.TargetingMode == MissileBase.TargetingModes.Radar)
                    {
                        foreach (var rd in radars)
                        {
                            if (rd.canLock)
                            {
                                rd.EnableRadar();
                                break;
                            }
                        }
                    }

                    // check DLZ
                    MissileLaunchParams dlz = MissileLaunchParams.GetDynamicLaunchParams(ml, guardTarget.srf_velocity, guardTarget.transform.position);
                    if (vessel.srfSpeed > ml.minLaunchSpeed && distanceToTarget < dlz.maxLaunchRange && distanceToTarget > dlz.minLaunchRange)
                    {
                            return true;
                    }
                    if (BDArmorySettings.DRAW_DEBUG_LABELS)
                    {
                        Debug.Log("[BDArmory] : " + vessel.vesselName + " - Failed DLZ test: " + weaponCandidate.GetShortName());
                    }
                    break;


                case WeaponClasses.Bomb:
                    if (!vessel.LandedOrSplashed)
                        return true;    // TODO: bomb always allowed?
                    break;


                case WeaponClasses.Rocket:
                    var rocketlauncher = (RocketLauncher)weaponCandidate;
                    // check yaw range of turret
                    turret = rocketlauncher.turret;
                    gimbalTolerance = vessel.LandedOrSplashed ? 0 : 15;
                    if (turret != null)
                        if (TargetInTurretRange(turret, gimbalTolerance))
                            return true;
                    break;

                case WeaponClasses.SLW:
                    return true;                    

                default:
                    throw new ArgumentOutOfRangeException();
            }                  

            return false;
        }
        
        void SetTarget(TargetInfo target)
        {
            if (target)
            {
                if (currentTarget)
                {
                    currentTarget.Disengage(this);
                }
                target.Engage(this);
                currentTarget = target;
                guardTarget = target.Vessel;
            }
            else
            {
                if (currentTarget)
                {
                    currentTarget.Disengage(this);
                }
                guardTarget = null;
                currentTarget = null;
            }
        }

        #endregion

        public bool CanSeeTarget(Vessel target)
        {
            if (RadarUtils.TerrainCheck(target.transform.position, transform.position))
            {
                return false;
            }
            return true;
        }

        void ScanAllTargets()
        {
            //get a target.
            //float angle = 0;

            foreach (Vessel v in FlightGlobals.Vessels)
            {
                if (v.loaded)
                {
                    float distance = (transform.position - v.transform.position).magnitude;
                    if (distance < guardRange && CanSeeTarget(v))
                    {
                        float angle = Vector3.Angle(-transform.forward, v.transform.position - transform.position);
                        if (angle < guardAngle / 2)
                        {
                            foreach (var missile in v.FindPartModulesImplementing<MissileBase>())
                            {
                                if (missile.HasFired && missile.Team != team)
                                {
                                    BDATargetManager.ReportVessel(v, this);
                                    if (!isFlaring && missile.TargetingMode == MissileBase.TargetingModes.Heat && Vector3.Angle(missile.GetForwardTransform(), transform.position - missile.transform.position) < 20)
                                    {
                                        StartCoroutine(FlareRoutine(targetScanInterval * 0.75f));
                                    }
                                    break;
                                }
                            }

                            foreach (var mF in v.FindPartModulesImplementing<MissileFire>())
                            {
                                if (mF.team != team && mF.vessel.IsControllable && mF.vessel.isCommandable)
                                //added iscommandable check
                                {
                                    BDATargetManager.ReportVessel(v, this);
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }

        void SearchForRadarSource()
        {
            antiRadTargetAcquired = false;

            if (rwr && rwr.rwrEnabled)
            {
                float closestAngle = 360;
                MissileBase missile = CurrentMissile;

                if (!missile) return;

                float maxOffBoresight = missile.maxOffBoresight;

                if (missile.TargetingMode != MissileBase.TargetingModes.AntiRad) return;

                for (int i = 0; i < rwr.pingsData.Length; i++)
                {
                    if (rwr.pingsData[i].exists && (rwr.pingsData[i].signalStrength == 0 || rwr.pingsData[i].signalStrength == 5))
                    {
                        float angle = Vector3.Angle(rwr.pingWorldPositions[i] - missile.transform.position, missile.GetForwardTransform());

                        if (angle < closestAngle && angle < maxOffBoresight)
                        {
                            closestAngle = angle;
                            antiRadiationTarget = rwr.pingWorldPositions[i];
                            antiRadTargetAcquired = true;
                        }
                    }
                }
            }
        }

        void SearchForLaserPoint()
        {
            MissileBase ml = CurrentMissile;
            if (!ml || ml.TargetingMode != MissileBase.TargetingModes.Laser)
            {
                return;
            }

            var launcher = ml as MissileLauncher;
            if (launcher != null)
            {
                foundCam = BDATargetManager.GetLaserTarget(launcher,
                    launcher.GuidanceMode == MissileBase.GuidanceModes.BeamRiding);
            }
            else
            {
                foundCam = BDATargetManager.GetLaserTarget((BDModularGuidance)ml, false);
            }

            if (foundCam)
            {
                laserPointDetected = true;
            }
            else
            {
                laserPointDetected = false;
            }
        }

        void SearchForHeatTarget()
        {
            if (CurrentMissile != null)
            {
                if (!CurrentMissile || CurrentMissile.TargetingMode != MissileBase.TargetingModes.Heat)
                {
                    return;
                }

                var scanRadius = CurrentMissile.lockedSensorFOV * 2;
                var maxOffBoresight = CurrentMissile.maxOffBoresight * 0.85f;

                if (vesselRadarData && vesselRadarData.locked)
                {
                    heatTarget = vesselRadarData.lockedTargetData.targetData;
                }

                Vector3 direction =
                    heatTarget.exists && Vector3.Angle(heatTarget.position - CurrentMissile.MissileReferenceTransform.position, CurrentMissile.GetForwardTransform()) < maxOffBoresight ?
                    heatTarget.predictedPosition - CurrentMissile.MissileReferenceTransform.position
                    : CurrentMissile.GetForwardTransform();

                heatTarget = BDATargetManager.GetHeatTarget(new Ray(CurrentMissile.MissileReferenceTransform.position + (50 * CurrentMissile.GetForwardTransform()), direction), scanRadius, CurrentMissile.heatThreshold, CurrentMissile.allAspect);
            }
        }

        bool CrossCheckWithRWR(TargetInfo v)
        {
            bool matchFound = false;
            if (rwr && rwr.rwrEnabled)
            {
                for (int i = 0; i < rwr.pingsData.Length; i++)
                {
                    if (rwr.pingsData[i].exists && (rwr.pingWorldPositions[i] - v.position).magnitude < 20)
                    {
                        matchFound = true;
                        break;
                    }
                }
            }

            return matchFound;
        }
                
        public void SendTargetDataToMissile(MissileBase ml)
        { //TODO BDModularGuidance: implement all targetings on base
            if (ml.TargetingMode == MissileBase.TargetingModes.Laser && laserPointDetected)
            {
                ml.lockedCamera = foundCam;
            }
            else if (ml.TargetingMode == MissileBase.TargetingModes.Gps)
            {
                if (BDArmorySettings.ALLOW_LEGACY_TARGETING)
                {
                    if (vessel.targetObject != null && vessel.targetObject.GetVessel() != null)
                    {
                        ml.TargetAcquired = true;
                        ml.legacyTargetVessel = vessel.targetObject.GetVessel();
                    }
                }
                else if (designatedGPSCoords != Vector3d.zero)
                {
                    ml.targetGPSCoords = designatedGPSCoords;
                    ml.TargetAcquired = true;
                }

            }
            else if (ml.TargetingMode == MissileBase.TargetingModes.Heat && heatTarget.exists)
            {

                ml.heatTarget = heatTarget;
                heatTarget = TargetSignatureData.noTarget;

            }
            else if (ml.TargetingMode == MissileBase.TargetingModes.Radar && vesselRadarData && vesselRadarData.locked)//&& radar && radar.lockedTarget.exists)
            {

                //ml.radarTarget = radar.lockedTarget;
                ml.radarTarget = vesselRadarData.lockedTargetData.targetData;

                ml.vrd = vesselRadarData;
                vesselRadarData.LastMissile = ml;

                /*

				if(radar.linked && radar.linkedRadar.locked)
				{
					ml.radar = radar.linkedRadar;
				}
				else
				{
					ml.radar = radar;
				}
				radar.LastMissile = ml;
				*/

            }
            else if (ml.TargetingMode == MissileBase.TargetingModes.AntiRad && antiRadTargetAcquired)
            {
                ml.TargetAcquired = true;
                ml.targetGPSCoords = VectorUtils.WorldPositionToGeoCoords(antiRadiationTarget,
                        vessel.mainBody);
            }
        }

        public void TargetAcquire()
        {
            if (isArmed && BDArmorySettings.ALLOW_LEGACY_TARGETING)
            {
                Vessel acquiredTarget = null;
                float smallestAngle = 8;

                if (Time.time - targetListTimer > 1)
                {
                    loadedVessels.Clear();

                    foreach (Vessel v in FlightGlobals.Vessels)
                    {
                        float viewAngle = Vector3.Angle(-transform.forward, v.transform.position - transform.position);
                        if (v.loaded && viewAngle < smallestAngle)
                        {
                            if (!v.vesselName.Contains("(fired)")) loadedVessels.Add(v);
                        }
                    }
                }

                foreach (Vessel v in loadedVessels)
                {
                    float viewAngle = Vector3.Angle(-transform.forward, v.transform.position - transform.position);
                    //if(v!= vessel && v.loaded) Debug.Log ("view angle: "+viewAngle);
                    if (v != null && v != vessel && v.loaded && viewAngle < smallestAngle && CanSeeTarget(v))
                    {
                        acquiredTarget = v;
                        smallestAngle = viewAngle;
                    }
                }

                if (acquiredTarget != null && acquiredTarget != (Vessel)FlightGlobals.fetch.VesselTarget)
                {
                    if (BDArmorySettings.DRAW_DEBUG_LABELS)
                    {
                        Debug.Log("[BDArmory]: found target! : " + acquiredTarget.name);
                    }
                    FlightGlobals.fetch.SetVesselTarget(acquiredTarget);
                }
            }
        }

        #endregion

        #region Gaurd

        public void ResetGuardInterval()
        {
            targetScanTimer = 0;
        }

        void GuardMode()
        {
            if (!gameObject.activeInHierarchy) return;
            if (BDArmorySettings.PEACE_MODE) return;

            if (!BDArmorySettings.ALLOW_LEGACY_TARGETING)
            {
                UpdateGuardViewScan();
            }

            //setting turrets to guard mode
            if (selectedWeapon != null && selectedWeapon.GetWeaponClass() == WeaponClasses.Gun)
            {
                foreach (var weapon in vessel.FindPartModulesImplementing<ModuleWeapon>())
                //make this not have to go every frame
                {
                    if (weapon.part.partInfo.title == selectedWeapon.GetPart().partInfo.title)
                    {
                        weapon.EnableWeapon();
                        weapon.aiControlled = true;
                        weapon.maxAutoFireCosAngle = vessel.LandedOrSplashed ? 0.9993908f : 0.9975641f; //2 : 4 degrees
                    }
                }
            }

            if (guardTarget)
            {
                //release target if out of range
                if (BDArmorySettings.ALLOW_LEGACY_TARGETING &&
                    (guardTarget.transform.position - transform.position).magnitude > guardRange)
                {
                    SetTarget(null);
                }
            }
            else if (selectedWeapon != null && selectedWeapon.GetWeaponClass() == WeaponClasses.Gun)
            {
                foreach (var weapon in vessel.FindPartModulesImplementing<ModuleWeapon>())
                {
                    if (weapon.part.partInfo.title == selectedWeapon.GetPart().partInfo.title)
                    {
                        weapon.autoFire = false;
                        weapon.legacyTargetVessel = null;
                    }
                }
            }

            if (missilesAway < 0)
                missilesAway = 0;

            if (missileIsIncoming)
            {
                if (!isLegacyCMing)
                {
                    StartCoroutine(LegacyCMRoutine());
                }

                targetScanTimer -= Time.fixedDeltaTime; //advance scan timing (increased urgency)
            }

            //scan and acquire new target
            if (Time.time - targetScanTimer > targetScanInterval)
            {
                targetScanTimer = Time.time;

                if (!guardFiringMissile)
                {
                    SetTarget(null);
                    if (BDArmorySettings.ALLOW_LEGACY_TARGETING)
                    {
                        ScanAllTargets();
                    }

                    SmartFindTarget();

                    if (guardTarget == null || selectedWeapon == null)
                    {
                        SetCargoBays();
                        return;
                    }

                    //firing
                    if (weaponIndex > 0)
                    {
                        if (selectedWeapon.GetWeaponClass() == WeaponClasses.Missile || selectedWeapon.GetWeaponClass() == WeaponClasses.SLW)
                        {
                            bool launchAuthorized = true;
                            bool pilotAuthorized = true;
                            //(!pilotAI || pilotAI.GetLaunchAuthorization(guardTarget, this));

                            float targetAngle = Vector3.Angle(-transform.forward, guardTarget.transform.position - transform.position);
                            float targetDistance = Vector3.Distance(currentTarget.position, transform.position);
                            MissileLaunchParams dlz = MissileLaunchParams.GetDynamicLaunchParams(CurrentMissile, guardTarget.srf_velocity, guardTarget.CoM);

                            if (targetAngle > guardAngle / 2) //dont fire yet if target out of guard angle
                            {
                                launchAuthorized = false;
                            }
                            else if (targetDistance >= dlz.maxLaunchRange || targetDistance <= dlz.minLaunchRange)  //fire the missile only if target is further than missiles min launch range
                            {
                                launchAuthorized = false;
                            }

                            Debug.Log("[BDArmory]:" + vessel.vesselName + " launchAuth=" + launchAuthorized + ", pilotAut=" +
                                      pilotAuthorized + ", missilesAway/Max=" + missilesAway + "/" + maxMissilesOnTarget);
                            if (missilesAway < maxMissilesOnTarget)
                            {
                                if (!guardFiringMissile && launchAuthorized &&
                                    (pilotAuthorized || !BDArmorySettings.ALLOW_LEGACY_TARGETING))
                                {
                                    StartCoroutine(GuardMissileRoutine());
                                }
                            }
                            else
                            {
                                Debug.Log("[BDArmory]:" + vessel.vesselName + " waiting for missile to be ready...");
                            }

                            if (!launchAuthorized || !pilotAuthorized || missilesAway >= maxMissilesOnTarget)
                            {
                                targetScanTimer -= 0.5f * targetScanInterval;
                            }
                        }
                        else if (selectedWeapon.GetWeaponClass() == WeaponClasses.Bomb)
                        {
                            if (!guardFiringMissile)
                            {
                                StartCoroutine(GuardBombRoutine());
                            }
                        }
                        else if (selectedWeapon.GetWeaponClass() == WeaponClasses.Gun ||
                                 selectedWeapon.GetWeaponClass() == WeaponClasses.Rocket ||
                                 selectedWeapon.GetWeaponClass() == WeaponClasses.DefenseLaser)
                        {
                            StartCoroutine(GuardTurretRoutine());
                        }
                    }
                }
                SetCargoBays();
            }

            if (overrideTimer > 0)
            {
                overrideTimer -= TimeWarp.fixedDeltaTime;
            }
            else
            {
                overrideTimer = 0;
                overrideTarget = null;
            }
        }

        void UpdateGuardViewScan()
        {
            float finalMaxAngle = guardAngle / 2;
            float finalScanDirectionAngle = currentGuardViewAngle;
            if (guardTarget != null)
            {
                if (focusingOnTarget)
                {
                    if (focusingOnTargetTimer > 3)
                    {
                        focusingOnTargetTimer = 0;
                        focusingOnTarget = false;
                    }
                    else
                    {
                        focusingOnTargetTimer += Time.fixedDeltaTime;
                    }
                    finalMaxAngle = 20;
                    finalScanDirectionAngle =
                        VectorUtils.SignedAngle(viewReferenceTransform.forward,
                            guardTarget.transform.position - viewReferenceTransform.position,
                            viewReferenceTransform.right) + currentGuardViewAngle;
                }
                else
                {
                    if (focusingOnTargetTimer > 2)
                    {
                        focusingOnTargetTimer = 0;
                        focusingOnTarget = true;
                    }
                    else
                    {
                        focusingOnTargetTimer += Time.fixedDeltaTime;
                    }
                }
            }


            float angleDelta = guardViewScanRate * Time.fixedDeltaTime;
            ViewScanResults results;
            debugGuardViewDirection = RadarUtils.GuardScanInDirection(this, finalScanDirectionAngle,
                viewReferenceTransform, angleDelta, out results, BDArmorySettings.MAX_GUARD_VISUAL_RANGE);

            currentGuardViewAngle += guardViewScanDirection * angleDelta;
            if (Mathf.Abs(currentGuardViewAngle) > finalMaxAngle)
            {
                currentGuardViewAngle = Mathf.Sign(currentGuardViewAngle) * finalMaxAngle;
                guardViewScanDirection = -guardViewScanDirection;
            }

            if (results.foundMissile)
            {
                if (rwr && !rwr.rwrEnabled)
                {
                    rwr.EnableRWR();
                }
            }

            if (results.foundHeatMissile)
            {
                if (!isFlaring)
                {
                    StartCoroutine(FlareRoutine(2.5f));
                    StartCoroutine(ResetMissileThreatDistanceRoutine());
                }
                incomingThreatPosition = results.threatPosition;

                if (results.threatVessel)
                {
                    if (!incomingMissileVessel ||
                        (incomingMissileVessel.transform.position - vessel.transform.position).sqrMagnitude >
                        (results.threatVessel.transform.position - vessel.transform.position).sqrMagnitude)
                    {
                        incomingMissileVessel = results.threatVessel;
                    }
                }
            }

            if (results.foundRadarMissile)
            {
                FireChaff();
                FireECM();

                incomingThreatPosition = results.threatPosition;

                if (results.threatVessel)
                {
                    if (!incomingMissileVessel ||
                        (incomingMissileVessel.transform.position - vessel.transform.position).sqrMagnitude >
                        (results.threatVessel.transform.position - vessel.transform.position).sqrMagnitude)
                    {
                        incomingMissileVessel = results.threatVessel;
                    }
                }
            }

            if (results.foundAGM)
            {
                //do smoke CM here.
                if (targetMissiles && guardTarget == null)
                {
                    //targetScanTimer = Mathf.Min(targetScanInterval, Time.time - targetScanInterval + 0.5f);
                    targetScanTimer -= targetScanInterval / 2;
                }
            }

            incomingMissileDistance = Mathf.Min(results.missileThreatDistance, incomingMissileDistance);

            if (results.firingAtMe)
            {
                incomingThreatPosition = results.threatPosition;
                if (ufRoutine != null)
                {
                    StopCoroutine(ufRoutine);
                    underFire = false;
                }
                if (results.threatWeaponManager != null)
                {
                    TargetInfo nearbyFriendly = BDATargetManager.GetClosestFriendly(this);
                    TargetInfo nearbyThreat = BDATargetManager.GetTargetFromWeaponManager(results.threatWeaponManager);

                    if (nearbyThreat?.weaponManager != null && nearbyFriendly?.weaponManager != null)
                        if (nearbyThreat.weaponManager.team != this.team &&
                            nearbyFriendly.weaponManager.team == this.team)
                        //turns out that there's no check for AI on the same team going after each other due to this.  Who knew?
                        {
                            if (nearbyThreat == this.currentTarget && nearbyFriendly.weaponManager.currentTarget != null)
                            //if being attacked by the current target, switch to the target that the nearby friendly was engaging instead
                            {
                                SetOverrideTarget(nearbyFriendly.weaponManager.currentTarget);
                                nearbyFriendly.weaponManager.SetOverrideTarget(nearbyThreat);
                                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                                    Debug.Log("[BDArmory]: " + vessel.vesselName + " called for help from " +
                                              nearbyFriendly.Vessel.vesselName + " and took its target in return");
                                //basically, swap targets to cover each other
                            }
                            else
                            {
                                //otherwise, continue engaging the current target for now
                                nearbyFriendly.weaponManager.SetOverrideTarget(nearbyThreat);
                                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                                    Debug.Log("[BDArmory]: " + vessel.vesselName + " called for help from " +
                                              nearbyFriendly.Vessel.vesselName);
                            }
                        }
                }
                ufRoutine = StartCoroutine(UnderFireRoutine());
            }
        }

        public void ForceWideViewScan()
        {
            focusingOnTarget = false;
            focusingOnTargetTimer = 1;
        }

        public void ForceScan()
        {
            targetScanTimer = -100;
        }
        
        void StartGuardTurretFiring()
        {
            if (!guardTarget) return;
            if (selectedWeapon == null) return;

            if (selectedWeapon.GetWeaponClass() == WeaponClasses.Rocket)
            {
                foreach (var weapon in vessel.FindPartModulesImplementing<RocketLauncher>())
                {
                    if (weapon.GetShortName() == selectedWeaponString)
                    {
                        if (BDArmorySettings.DRAW_DEBUG_LABELS)
                        {
                            Debug.Log("[BDArmory]: Setting rocket to auto fire");
                        }
                        weapon.legacyGuardTarget = guardTarget;
                        weapon.autoFireStartTime = Time.time;
                        //weapon.autoFireDuration = targetScanInterval / 2;
                        weapon.autoFireDuration = (fireBurstLength < 0.5) ? targetScanInterval / 2 : fireBurstLength;
                        weapon.autoRippleRate = rippleFire ? rippleRPM : 0;
                    }
                }
            }
            else
            {
                foreach (var weapon in vessel.FindPartModulesImplementing<ModuleWeapon>())
                {
                    if (weapon.part.partInfo.title == selectedWeapon.GetPart().partInfo.title)
                    {
                        weapon.legacyTargetVessel = guardTarget;
                        weapon.autoFireTimer = Time.time;
                        //weapon.autoFireLength = 3 * targetScanInterval / 4;
                        weapon.autoFireLength = (fireBurstLength < 0.5) ? targetScanInterval / 2 : fireBurstLength;
                    }
                }
            }
        }

        public void SetOverrideTarget(TargetInfo target)
        {
            overrideTarget = target;
            targetScanTimer = -100;
        }

        public void UpdateMaxGuardRange()
        {
            var rangeEditor = (UI_FloatRange)Fields["guardRange"].uiControlEditor;
            if (BDArmorySettings.PHYSICS_RANGE != 0)
            {
                if (BDArmorySettings.ALLOW_LEGACY_TARGETING)
                {
                    rangeEditor.maxValue = BDArmorySettings.PHYSICS_RANGE;
                }
                else
                {
                    rangeEditor.maxValue = BDArmorySettings.MAX_GUARD_VISUAL_RANGE;
                }
            }
            else
            {
                rangeEditor.maxValue = 5000;
            }
        }

        #endregion

        #region Turret
        int CheckTurret(float distance)
        {
            if (weaponIndex == 0 || selectedWeapon == null ||
                !(selectedWeapon.GetWeaponClass() == WeaponClasses.Gun ||
                  selectedWeapon.GetWeaponClass() == WeaponClasses.DefenseLaser ||
                  selectedWeapon.GetWeaponClass() == WeaponClasses.Rocket))
            {
                return 2;
            }
            if (BDArmorySettings.DRAW_DEBUG_LABELS)
            {
                Debug.Log("[BDArmory]: Checking turrets");
            }
            float finalDistance = distance;
            //vessel.LandedOrSplashed ? distance : distance/2; //decrease distance requirement if airborne

            if (selectedWeapon.GetWeaponClass() == WeaponClasses.Rocket)
            {
                foreach (var rl in vessel.FindPartModulesImplementing<RocketLauncher>())
                {
                    if (rl.part.partInfo.title == selectedWeapon.GetPart().partInfo.title)
                    {
                        float gimbalTolerance = vessel.LandedOrSplashed ? 0 : 15;
                        if (rl.maxTargetingRange >= finalDistance && TargetInTurretRange(rl.turret, gimbalTolerance))         //////check turret limits here
                        {
                            if (BDArmorySettings.DRAW_DEBUG_LABELS)
                            {
                                Debug.Log("[BDArmory]: " + selectedWeapon + " is valid!");
                            }
                            return 1;
                        }
                    }
                }
            }
            else
            {
                foreach (var weapon in vessel.FindPartModulesImplementing<ModuleWeapon>())
                {
                    if (weapon.part.partInfo.title == selectedWeapon.GetPart().partInfo.title)
                    {
                        float gimbalTolerance = vessel.LandedOrSplashed ? 0 : 15;
                        if (((!vessel.LandedOrSplashed && pilotAI) || (TargetInTurretRange(weapon.turret, gimbalTolerance))) && weapon.maxEffectiveDistance >= finalDistance)
                        {
                            if (weapon.isOverheated)
                            {
                                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                                {
                                    Debug.Log("[BDArmory]: " + selectedWeapon + " is overheated!");
                                }
                                return -1;
                            }
                            if (CheckAmmo(weapon) || BDArmorySettings.INFINITE_AMMO)
                            {
                                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                                {
                                    Debug.Log("[BDArmory]: " + selectedWeapon + " is valid!");
                                }
                                return 1;
                            }
                            if (BDArmorySettings.DRAW_DEBUG_LABELS)
                            {
                                Debug.Log("[BDArmory]: " + selectedWeapon + " has no ammo.");
                            }
                            return -1;
                        }
                        if (BDArmorySettings.DRAW_DEBUG_LABELS)
                        {
                            Debug.Log("[BDArmory]: " + selectedWeapon + " cannot reach target (" + distance + " vs " + weapon.maxEffectiveDistance + ", yawRange: " + weapon.yawRange + "). Continuing.");
                        }
                        //else return 0;
                    }
                }
            }
            return 2;
        }

        bool TargetInTurretRange(ModuleTurret turret, float tolerance)
        {
            if (!turret)
            {
                return false;
            }

            if (!guardTarget)
            {
                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                {
                    Debug.Log("[BDArmory]: Checking turret range but no guard target");
                }
                return false;
            }
            if (turret.yawRange == 360)
            {
                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                {
                    Debug.Log("[BDArmory]: Checking turret range - turret has full swivel");
                }
                return true;
            }

            Transform turretTransform = turret.yawTransform.parent;
            Vector3 direction = guardTarget.transform.position - turretTransform.position;
            Vector3 directionYaw = Vector3.ProjectOnPlane(direction, turretTransform.up);
            Vector3 directionPitch = Vector3.ProjectOnPlane(direction, turretTransform.right);

            float angleYaw = Vector3.Angle(turretTransform.forward, directionYaw);
            //float anglePitch = Vector3.Angle(-turret.transform.forward, directionPitch);
            float signedAnglePitch = Misc.SignedAngle(turretTransform.forward, directionPitch, turretTransform.up);
            if (Mathf.Abs(signedAnglePitch) > 90)
            {
                signedAnglePitch -= Mathf.Sign(signedAnglePitch) * 180;
            }
            bool withinPitchRange = (signedAnglePitch >= turret.minPitch && signedAnglePitch <= turret.maxPitch + tolerance);

            if (angleYaw < (turret.yawRange / 2) + tolerance && withinPitchRange)
            {
                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                {
                    Debug.Log("[BDArmory]: Checking turret range - target is INSIDE gimbal limits! signedAnglePitch: " + signedAnglePitch + ", minPitch: " + turret.minPitch + ", maxPitch: " + turret.maxPitch);
                }
                return true;
            }
            else
            {
                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                {
                    Debug.Log("[BDArmory]: Checking turret range - target is OUTSIDE gimbal limits! signedAnglePitch: " + signedAnglePitch + ", minPitch: " + turret.minPitch + ", maxPitch: " + turret.maxPitch + ", angleYaw: " + angleYaw);
                }
                return false;
            }
        }

        bool CheckAmmo(ModuleWeapon weapon)
        {
            string ammoName = weapon.ammoName;

            foreach (Part p in vessel.parts)
            {
                foreach (var resource in p.Resources)
                {
                    if (resource.resourceName == ammoName)
                    {
                        if (resource.amount > 0)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        void ToggleTurret()
        {
            foreach (var weapon in vessel.FindPartModulesImplementing<ModuleWeapon>())
            {
                if (selectedWeapon == null || weapon.part.partInfo.title != selectedWeapon.GetPart().partInfo.title)
                {
                    weapon.DisableWeapon();
                }
                else
                {
                    weapon.EnableWeapon();
                }
            }
        }


        #endregion

        #region Aimer

        void BombAimer()
        {
            if (selectedWeapon == null)
            {
                showBombAimer = false;
                return;
            }
            if (!bombPart || selectedWeapon.GetPart() != bombPart)
            {
                if (selectedWeapon.GetWeaponClass() == WeaponClasses.Bomb)
                {
                    bombPart = selectedWeapon.GetPart();
                }
                else
                {
                    showBombAimer = false;
                    return;
                }
            }

            showBombAimer =
            (
                !MapView.MapIsEnabled &&
                vessel.isActiveVessel &&
                selectedWeapon != null &&
                selectedWeapon.GetWeaponClass() == WeaponClasses.Bomb &&
                bombPart != null &&
                BDArmorySettings.DRAW_AIMERS &&
                vessel.verticalSpeed < 50 &&
                AltitudeTrigger()
            );

            if (showBombAimer || (guardMode && weaponIndex > 0 && selectedWeapon.GetWeaponClass() == WeaponClasses.Bomb))
            {
                MissileBase ml = bombPart.GetComponent<MissileBase>();

                float simDeltaTime = 0.1f;
                float simTime = 0;
                Vector3 dragForce = Vector3.zero;
                Vector3 prevPos = ml.MissileReferenceTransform.position;
                Vector3 currPos = ml.MissileReferenceTransform.position;
                //Vector3 simVelocity = vessel.rb_velocity;
                Vector3 simVelocity = vessel.srf_velocity; //Issue #92




                var launcher = ml as MissileLauncher;
                if (launcher != null)
                {
                    simVelocity += launcher.decoupleSpeed *
                                   (launcher.decoupleForward
                                       ? launcher.MissileReferenceTransform.forward
                                       : -launcher.MissileReferenceTransform.up);
                }
                else
                {   //TODO: BDModularGuidance review this value
                    simVelocity += 5 * -launcher.MissileReferenceTransform.up;
                }


                List<Vector3> pointPositions = new List<Vector3>();
                pointPositions.Add(currPos);


                prevPos = ml.MissileReferenceTransform.position;
                currPos = ml.MissileReferenceTransform.position;

                bombAimerPosition = Vector3.zero;


                bool simulating = true;
                while (simulating)
                {
                    prevPos = currPos;
                    currPos += simVelocity * simDeltaTime;
                    float atmDensity =
                        (float)
                        FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(currPos),
                            FlightGlobals.getExternalTemperature(), FlightGlobals.currentMainBody);

                    simVelocity += FlightGlobals.getGeeForceAtPosition(currPos) * simDeltaTime;
                    float simSpeedSquared = simVelocity.sqrMagnitude;

                    launcher = ml as MissileLauncher;
                    float drag = 0;
                    if (launcher != null)
                    {
                        drag = launcher.simpleDrag;
                        if (simTime > launcher.deployTime)
                        {
                            drag = launcher.deployedDrag;
                        }
                    }
                    else
                    {
                        //TODO:BDModularGuidance drag calculation
                        drag = ml.vessel.parts.Sum(x => x.dragScalar);

                    }

                    dragForce = (0.008f * bombPart.mass) * drag * 0.5f * simSpeedSquared * atmDensity * simVelocity.normalized;
                    simVelocity -= (dragForce / bombPart.mass) * simDeltaTime;

                    Ray ray = new Ray(prevPos, currPos - prevPos);
                    RaycastHit hitInfo;
                    if (Physics.Raycast(ray, out hitInfo, Vector3.Distance(prevPos, currPos), 1 << 15))
                    {
                        bombAimerPosition = hitInfo.point;
                        simulating = false;
                    }
                    else if (FlightGlobals.getAltitudeAtPos(currPos) < 0)
                    {
                        bombAimerPosition = currPos -
                                            (FlightGlobals.getAltitudeAtPos(currPos) * FlightGlobals.getUpAxis());
                        simulating = false;
                    }

                    simTime += simDeltaTime;
                    pointPositions.Add(currPos);
                }


                //debug lines
                if (BDArmorySettings.DRAW_DEBUG_LINES && BDArmorySettings.DRAW_AIMERS)
                {
                    Vector3[] pointsArray = pointPositions.ToArray();
                    LineRenderer lr = GetComponent<LineRenderer>();
                    if (!lr)
                    {
                        lr = gameObject.AddComponent<LineRenderer>();
                    }
                    lr.enabled = true;
                    lr.SetWidth(.1f, .1f);
                    lr.SetVertexCount(pointsArray.Length);
                    for (int i = 0; i < pointsArray.Length; i++)
                    {
                        lr.SetPosition(i, pointsArray[i]);
                    }
                }
                else
                {
                    if (gameObject.GetComponent<LineRenderer>())
                    {
                        gameObject.GetComponent<LineRenderer>().enabled = false;
                    }
                }
            }
        }

        bool AltitudeTrigger()
        {
            float maxAlt = Mathf.Clamp(BDArmorySettings.PHYSICS_RANGE * 0.75f, 2250, 10000);
            double asl = vessel.mainBody.GetAltitude(vessel.CoM);
            double radarAlt = asl - vessel.terrainAltitude;

            return radarAlt < maxAlt || asl < maxAlt;
        }


        #endregion
 	
	}
}

