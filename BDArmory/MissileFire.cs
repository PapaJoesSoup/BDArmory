using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BDArmory.Control;
using BDArmory.Core.Extension;
using BDArmory.CounterMeasure;
using BDArmory.Misc;
using BDArmory.Parts;
using BDArmory.Radar;
using BDArmory.UI;
using UnityEngine;

namespace BDArmory
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

        [KSPField(guiActiveEditor = false, isPersistant = true, guiActive = false)] public int weaponIndex;

        //ScreenMessage armedMessage;
        ScreenMessage selectionMessage;
        string selectionText = "";

        Transform cameraTransform;

        float startTime;
        int missilesAway;

        public bool hasLoadedRippleData;
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

        float triggerTimer;
        int rippleGunCount;
        int _gunRippleIndex;
        public float gunRippleRpm;

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
        public bool canRipple;

        //public float triggerHoldTime = 0.3f;

        //[KSPField(isPersistant = true)]

        public bool rippleFire
        {
            get
            {
                if (selectedWeapon == null) return false;
                if (rippleDictionary.ContainsKey(selectedWeapon.GetShortName()))
                {
                    return rippleDictionary[selectedWeapon.GetShortName()].rippleFire;
                }
                //rippleDictionary.Add(selectedWeapon.GetShortName(), new RippleOption(false, 650));
                return false;
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
                    List<ModuleWeapon>.Enumerator w = vessel.FindPartModulesImplementing<ModuleWeapon>().GetEnumerator();
                    while (w.MoveNext())
                    {
                        if (w.Current == null) continue;
                        if (w.Current.GetShortName() == selectedWeapon.GetShortName())
                            w.Current.useRippleFire = ro.rippleFire;
                    }
                    w.Dispose();
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
                    IEnumerator<string> weapon = rippleData.Split(new char[] {';'}).AsEnumerable().GetEnumerator(); ;
                    while (weapon.MoveNext())
                    {
                        if (weapon.Current == string.Empty) continue;

                        string[] options = weapon.Current.Split(new char[] {','});
                        string wpnName = options[0];
                        bool rf = bool.Parse(options[1]);
                        float rpm = float.Parse(options[2]);
                        RippleOption ro = new RippleOption(rf, rpm);
                        rippleDictionary.Add(wpnName, ro);
                    }
                    weapon.Dispose();
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
                List<RocketLauncher>.Enumerator rl = vessel.FindPartModulesImplementing<RocketLauncher>().GetEnumerator();
                while (rl.MoveNext())
                {
                    if (rl.Current == null) continue;
                    if (!rippleDictionary.ContainsKey(rl.Current.GetShortName()))
                    {
                        rippleDictionary.Add(rl.Current.GetShortName(), new RippleOption(false, 650f));
                    }
                }
                rl.Dispose();
            }
            hasLoadedRippleData = true;
        }

        void SaveRippleOptions(ConfigNode node)
        {
            if (rippleDictionary != null)
            {
                rippleData = string.Empty;
                Dictionary<string, RippleOption>.KeyCollection.Enumerator wpnName = rippleDictionary.Keys.GetEnumerator();
                while (wpnName.MoveNext())
                {
                    if (wpnName.Current == null) continue;
                    rippleData += $"{wpnName},{rippleDictionary[wpnName.Current].rippleFire},{rippleDictionary[wpnName.Current].rpm};";
                }
                wpnName.Dispose();
                node.SetValue("RippleData", rippleData, true);
            }
            Debug.Log("[BDArmory]: Saved ripple data: " + rippleData);
        }

        public bool hasSingleFired;

        //bomb aimer
        Part bombPart;
        Vector3 bombAimerPosition = Vector3.zero;
        Texture2D bombAimerTexture = GameDatabase.Instance.GetTexture("BDArmory/Textures/grayCircle", false);
        bool showBombAimer;
        
        //targeting
        private List<Vessel> loadedVessels = new List<Vessel>();
        float targetListTimer;
        
        //rocket aimer handling
        RocketLauncher currentRocket;
        
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
        public bool missileIsIncoming;
        public float incomingMissileDistance = float.MaxValue;
        public Vessel incomingMissileVessel;

        //guard mode vars
        float targetScanTimer;
        Vessel guardTarget;
        public TargetInfo currentTarget;
        TargetInfo overrideTarget; //used for setting target next guard scan for stuff like assisting teammates
        float overrideTimer;

        public bool TargetOverride
        {
            get { return overrideTimer > 0; }
        }

        //AIPilot
        public BDModulePilotAI pilotAI;
        public float timeBombReleased;

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
        private RadarWarningReceiver radarWarn;

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
        float currentGuardViewAngle;
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
                
        public bool underFire;
        Coroutine ufRoutine;

        Vector3 debugGuardViewDirection;
        bool focusingOnTarget;
        float focusingOnTargetTimer;
        public Vector3 incomingThreatPosition;
        public Vessel incomingThreatVessel;

        bool guardFiringMissile;
        bool disabledRocketAimers;
        bool antiRadTargetAcquired;
        Vector3 antiRadiationTarget;
        bool laserPointDetected;

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
            guardRange = 10000;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Guns Range"),
         UI_FloatRange(minValue = 0f, maxValue = 10000f, stepIncrement = 10f, scene = UI_Scene.All)] public float
            gunRange = 2500f;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Missiles/Target"), UI_FloatRange(minValue = 1f, maxValue = 6f, stepIncrement = 1f, scene = UI_Scene.All)]
        public float maxMissilesOnTarget = 1;
        
        public void ToggleGuardMode()
        {
            guardMode = !guardMode;

            if (guardMode) return;
            //disable turret firing and guard mode
            List<ModuleWeapon>.Enumerator weapon = vessel.FindPartModulesImplementing<ModuleWeapon>().GetEnumerator();
            while (weapon.MoveNext())
            {
                if (weapon.Current == null) continue;
                weapon.Current.legacyTargetVessel = null;
                weapon.Current.autoFire = false;
                weapon.Current.aiControlled = false;
            }
            weapon.Dispose();
        }


        [KSPAction("Toggle Guard Mode")]
        public void AGToggleGuardMode(KSPActionParam param)
        {
            ToggleGuardMode();
        }


        //[KSPField(isPersistant = true)] public bool guardMode;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "Gaurd Mode: "),
            UI_Toggle(disabledText = "OFF", enabledText = "ON")]
        public bool guardMode;


        //[KSPField(isPersistant = false, guiActive = false, guiActiveEditor = false, guiName = "Target Type: "), UI_Toggle(disabledText = "Vessels", enabledText = "Missiles")]
        public bool targetMissiles = false;

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
			    List<MissileBase>.Enumerator missile = vessel.FindPartModulesImplementing<MissileBase>().GetEnumerator();
				while(missile.MoveNext())
				{
				    if(missile.Current == null) continue;
					if(missile.Current.GetShortName() == CurrentMissile.GetShortName())
					{
						missile.Current.Jettison();
					}
				}
                missile.Dispose();
			}
			else if(selectedWeapon!=null && selectedWeapon.GetWeaponClass() == WeaponClasses.Rocket)
			{
			    List<RocketLauncher>.Enumerator rocket = vessel.FindPartModulesImplementing<RocketLauncher>().GetEnumerator();
				while(rocket.MoveNext())
				{
				    if (rocket.Current == null) continue;
					rocket.Current.Jettison();
				}
                rocket.Dispose();
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
                List<MissileFire>.Enumerator wpnMgr = vessel.FindPartModulesImplementing<MissileFire>().GetEnumerator();
                while (wpnMgr.MoveNext())
                {
                    if (wpnMgr.Current == null) continue;
                    wpnMgr.Current.team = team;
                }
                wpnMgr.Dispose();
                if (vessel.gameObject.GetComponent<TargetInfo>())
                {
                    vessel.gameObject.GetComponent<TargetInfo>().RemoveFromDatabases();
                    Destroy(vessel.gameObject.GetComponent<TargetInfo>());
                }
                OnToggleTeam?.Invoke(this, BDATargetManager.BoolToTeam(team));
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

        IBDWeapon sw;

        public IBDWeapon selectedWeapon
        {
            get
            {
                if ((sw != null && sw.GetPart().vessel == vessel) || weaponIndex <= 0) return sw;
                List<IBDWeapon>.Enumerator weapon = vessel.FindPartModulesImplementing<IBDWeapon>().GetEnumerator();
                while (weapon.MoveNext())
                {
                    if (weapon.Current == null) continue;
                    if (weapon.Current.GetShortName() != selectedWeaponString) continue;
                    sw = weapon.Current;
                    break;
                }
                weapon.Dispose();
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
            if (weaponIndex <= 0 || (selectedWeapon.GetWeaponClass() != WeaponClasses.Gun &&
                                     selectedWeapon.GetWeaponClass() != WeaponClasses.DefenseLaser)) return;
            List<ModuleWeapon>.Enumerator weap = vessel.FindPartModulesImplementing<ModuleWeapon>().GetEnumerator();
            while (weap.MoveNext())
            {
                if (weap.Current == null) continue;
                if (weap.Current.weaponState != ModuleWeapon.WeaponStates.Enabled ||
                    weap.Current.GetShortName() != selectedWeapon.GetShortName()) continue;
                weap.Current.AGFireHold(param);
            }
            weap.Dispose();
        }

        [KSPAction("Fire Guns (Toggle)")]
        public void AGFireGunsToggle(KSPActionParam param)
        {
            if (weaponIndex <= 0 || (selectedWeapon.GetWeaponClass() != WeaponClasses.Gun &&
                                     selectedWeapon.GetWeaponClass() != WeaponClasses.DefenseLaser)) return;
            List<ModuleWeapon>.Enumerator weap = vessel.FindPartModulesImplementing<ModuleWeapon>().GetEnumerator();
            while (weap.MoveNext())
            {
                if (weap.Current == null) continue;
                if (weap.Current.weaponState != ModuleWeapon.WeaponStates.Enabled ||
                    weap.Current.GetShortName() != selectedWeapon.GetShortName()) continue;
                weap.Current.AGFireToggle(param);
            }
            weap.Dispose();
        }

        [KSPAction("Next Weapon")]
        public void AGCycle(KSPActionParam param)
        {
            CycleWeapon(true);
        }

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

        public override void OnStart(StartState state)
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

                List<BDModulePilotAI>.Enumerator aipilot = vessel.FindPartModulesImplementing<BDModulePilotAI>().GetEnumerator();
                while (aipilot.MoveNext())
                {
                    if (aipilot.Current == null) continue;
                    pilotAI = aipilot.Current;
                    break;
                }
                aipilot.Dispose();
            }
        }

        void OnPartDie(Part p = null)
        {
            if (p == part)
            {
                try
                {
                    GameEvents.onPartDie.Remove(OnPartDie);
                    GameEvents.onPartJointBreak.Remove(OnPartJointBreak);
                    GameEvents.onVesselCreate.Remove(OnVesselCreate);
                }
                catch(Exception e)
                {
                    if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory]: Error OnPartDie: " + e.Message);
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

            if (!HighLogic.LoadedSceneIsFlight)
            {
                return;
            }

            base.OnUpdate();
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

                        List<ModuleTargetingCamera>.Enumerator cam = BDATargetManager.ActiveLasers.GetEnumerator();
                        while (cam.MoveNext())
                        {
                            if (cam.Current == null) continue;
                            if (cam.Current.vessel != vessel && cam.Current.surfaceDetected && cam.Current.groundStabilized && !cam.Current.gimbalLimitReached)
                            {
                                BDGUIUtils.DrawTextureOnWorldPos(cam.Current.groundTargetPosition, BDArmorySettings.Instance.greenDiamondTexture, new Vector2(18, 18), 0);
                            }
                        }
                        cam.Dispose();
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
                    GUI.Label(new Rect(600, 900, 100, 100), "Missiles away: " + missilesAway);
                }
            }

        }

        bool CheckMouseIsOnGui()
        {
            return Misc.Misc.CheckMouseIsOnGui();
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
                        List<ModuleTargetingCamera>.Enumerator tgp = targetingPods.GetEnumerator();
                        while (tgp.MoveNext())
                        {
                            if (tgp.Current == null) continue;
                            if (!tgp.Current.enabled || (tgp.Current.cameraEnabled && tgp.Current.groundStabilized &&
                                                         !((tgp.Current.groundTargetPosition -
                                                            guardTarget.transform.position).sqrMagnitude > 20*20))) continue;
                            tgp.Current.EnableCamera();
                            yield return StartCoroutine(tgp.Current.PointToPositionRoutine(guardTarget.CoM));
                            if (!tgp.Current) continue;
                            if (tgp.Current.groundStabilized && guardTarget &&
                                (tgp.Current.groundTargetPosition - guardTarget.transform.position).sqrMagnitude < 20*20)
                            {
                                tgp.Current.slaveTurrets = true;
                                StartGuardTurretFiring();
                                yield break;
                            }
                            tgp.Current.DisableCamera();
                        }
                        tgp.Dispose();
                    }

                    if (!guardTarget || (guardTarget.transform.position - transform.position).sqrMagnitude > guardRange*guardRange)
                    {
                        SetTarget(null); //disengage, sensors unavailable.
                        yield break;
                    }
                }
                else
                {
                    if (!vesselRadarData || !(vesselRadarData.radarCount > 0))
                    {
                        List<ModuleRadar>.Enumerator rd = radars.GetEnumerator();
                        while (rd.MoveNext())
                        {
                            if (rd.Current == null) continue;
                            if (!rd.Current.canLock) continue;
                            rd.Current.EnableRadar();
                            break;
                        }
                        rd.Dispose();
                    }

                    if (vesselRadarData &&
                        (!vesselRadarData.locked ||
                         (vesselRadarData.lockedTargetData.targetData.predictedPosition - guardTarget.transform.position)
                             .sqrMagnitude > 40*40))
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

                    if (!guardTarget || (guardTarget.transform.position - transform.position).sqrMagnitude > guardRange*guardRange)
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
                    MissileLauncher mlauncher = ml as MissileLauncher;
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
                    while (ml && Time.time - attemptStartTime < attemptDuration && (!heatTarget.exists || (heatTarget.predictedPosition - guardTarget.transform.position).sqrMagnitude > 40*40))
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
                             guardTarget.transform.position).sqrMagnitude > 40*40)
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

                    if (FireCurrentMissile(true))
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
                           (!antiRadTargetAcquired || (antiRadiationTarget - guardTarget.CoM).sqrMagnitude > 20*20))
                    {
                        yield return new WaitForFixedUpdate();
                    }

                    if (SetCargoBays())
                    {
                        yield return new WaitForSeconds(1f);
                    }

                    if (ml && antiRadTargetAcquired && (antiRadiationTarget - guardTarget.CoM).sqrMagnitude < 20*20)
                    {
                        FireCurrentMissile(true);
                        StartCoroutine(MissileAwayRoutine(ml));
                    }
                }
                else if (ml.TargetingMode == MissileBase.TargetingModes.Laser)
                {
                    if (targetingPods.Count > 0) //if targeting pods are available, slew them onto target and lock.
                    {
                        List<ModuleTargetingCamera>.Enumerator tgp = targetingPods.GetEnumerator();
                        while (tgp.MoveNext())
                        {
                            if (tgp.Current == null) continue;
                            tgp.Current.EnableCamera();
                            yield return StartCoroutine(tgp.Current.PointToPositionRoutine(guardTarget.CoM));
                            if (tgp.Current.groundStabilized && (tgp.Current.groundTargetPosition - guardTarget.transform.position).sqrMagnitude < 20*20)
                            {
                                break;
                            }
                        }
                        tgp.Dispose();
                    }

                    //search for a laser point that corresponds with target vessel
                    float attemptStartTime = Time.time;
                    float attemptDuration = targetScanInterval * 0.75f;
                    while (Time.time - attemptStartTime < attemptDuration && (!laserPointDetected || (foundCam && (foundCam.groundTargetPosition - guardTarget.CoM).sqrMagnitude > 20*20)))
                    {
                        yield return new WaitForFixedUpdate();
                    }
                    if (SetCargoBays())
                    {
                        yield return new WaitForSeconds(1f);
                    }
                    if (ml && laserPointDetected && foundCam && (foundCam.groundTargetPosition - guardTarget.CoM).sqrMagnitude < 20*20)
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
            if (CurrentMissile.TargetingMode == MissileBase.TargetingModes.Gps && (designatedGPSInfo.worldPos - guardTarget.CoM).sqrMagnitude > CurrentMissile.GetBlastRadius()*CurrentMissile.GetBlastRadius())
            {
                //check database for target first
                float twoxsqrRad = 4f * radius * radius;
                bool foundTargetInDatabase = false;
                List<GPSTargetInfo>.Enumerator gps = BDATargetManager.GPSTargets[BDATargetManager.BoolToTeam(team)].GetEnumerator();
                while (gps.MoveNext())
                {
                    if (!((gps.Current.worldPos - guardTarget.CoM).sqrMagnitude < twoxsqrRad)) continue;
                    designatedGPSInfo = gps.Current;
                    foundTargetInDatabase = true;
                    break;
                }
                gps.Dispose();

                //no target in gps database, acquire via targeting pod
                if (!foundTargetInDatabase)
                {
                    ModuleTargetingCamera tgp = null;
                    List<ModuleTargetingCamera>.Enumerator t = targetingPods.GetEnumerator();
                    while (t.MoveNext())
                    {
                        if (t.Current) tgp = t.Current;
                    }
                    t.Dispose();

                    if (tgp != null)
                    {
                        tgp.EnableCamera();
                        yield return StartCoroutine(tgp.PointToPositionRoutine(guardTarget.CoM));

                        if (tgp)
                        {
                            if (guardTarget && tgp.groundStabilized && (tgp.groundTargetPosition - guardTarget.transform.position).sqrMagnitude < CurrentMissile.GetBlastRadius()*CurrentMissile.GetBlastRadius())
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

            MissileLauncher launcher = ml as MissileLauncher;
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
            if (distance < this.guardRange)
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
        public bool isChaffing;
        public bool isFlaring;
        public bool isECMJamming;

        bool isLegacyCMing;

        int cmCounter;
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
            List<ModuleECMJammer>.Enumerator ecm = vessel.FindPartModulesImplementing<ModuleECMJammer>().GetEnumerator();
            while (ecm.MoveNext())
            {
                if (ecm.Current == null) continue;
                if (ecm.Current.jammerEnabled) yield break;
                ecm.Current.EnableJammer();
            }
            ecm.Dispose();
            yield return new WaitForSeconds(10.0f);
            isECMJamming = false;

            List<ModuleECMJammer>.Enumerator ecm1 = vessel.FindPartModulesImplementing<ModuleECMJammer>().GetEnumerator();
            while (ecm1.MoveNext())
            {
                if (ecm1.Current == null) continue;
                ecm1.Current.DisableJammer();
            }
            ecm1.Dispose();
        }

        IEnumerator ChaffRoutine()
        {
            isChaffing = true;
            yield return new WaitForSeconds(UnityEngine.Random.Range(0.2f, 1f));
            List<CMDropper>.Enumerator cm = vessel.FindPartModulesImplementing<CMDropper>().GetEnumerator();
            while (cm.MoveNext())
            {
                if (cm.Current == null) continue;
                if (cm.Current.cmType == CMDropper.CountermeasureTypes.Chaff)
                {
                    cm.Current.DropCM();
                }
            }
            cm.Dispose();

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
                List<CMDropper>.Enumerator cm = vessel.FindPartModulesImplementing<CMDropper>().GetEnumerator();
                while (cm.MoveNext())
                {
                    if (cm.Current == null) continue;
                    if (cm.Current.cmType == CMDropper.CountermeasureTypes.Flare)
                    {
                        cm.Current.DropCM();
                    }
                }
                cm.Dispose();
                yield return new WaitForSeconds(0.6f);
            }
            isFlaring = false;
        }

        IEnumerator AllCMRoutine(int count)
        {
            for (int i = 0; i < count; i++)
            {
                List<CMDropper>.Enumerator cm = vessel.FindPartModulesImplementing<CMDropper>().GetEnumerator();
                while (cm.MoveNext())
                {
                    if (cm.Current == null) continue;
                    if ((cm.Current.cmType == CMDropper.CountermeasureTypes.Flare && !isFlaring)
                        || (cm.Current.cmType == CMDropper.CountermeasureTypes.Chaff && !isChaffing)
                        || (cm.Current.cmType == CMDropper.CountermeasureTypes.Smoke))
                    {
                        cm.Current.DropCM();
                    }
                }
                cm.Dispose();
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
                List<CMDropper>.Enumerator cm = vessel.FindPartModulesImplementing<CMDropper>().GetEnumerator();
                while (cm.MoveNext())
                {
                    if (cm.Current == null) continue;
                    cm.Current.DropCM();
                }
                cm.Dispose();
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

        bool FireCurrentMissile(bool checkClearance)
        {
            MissileBase missile = CurrentMissile;
            if (missile == null) return false;

            if (missile is MissileBase)
            {

                MissileBase ml = missile;
                if (checkClearance && (!CheckBombClearance(ml) || (ml is MissileLauncher && ((MissileLauncher)ml).rotaryRail && !((MissileLauncher)ml).rotaryRail.readyMissile == ml)))
                {
                    List<MissileBase>.Enumerator otherMissile = vessel.FindPartModulesImplementing<MissileBase>().GetEnumerator();
                    while (otherMissile.MoveNext())
                    {
                        if (otherMissile.Current == null) continue;
                        if (otherMissile.Current == ml || otherMissile.Current.GetShortName() != ml.GetShortName() ||
                            !CheckBombClearance(otherMissile.Current)) continue;
                        CurrentMissile = otherMissile.Current;
                        selectedWeapon = otherMissile.Current;
                        FireCurrentMissile(false);
                        return true;
                    }
                    otherMissile.Dispose();
                    CurrentMissile = ml;
                    selectedWeapon = ml;
                    return false;
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
            return true;
        }

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

            List<IBDWeapon>.Enumerator weapon = vessel.FindPartModulesImplementing<IBDWeapon>().GetEnumerator();
            while (weapon.MoveNext() )
            {
                if (weapon.Current == null) continue;
                string weaponName = weapon.Current.GetShortName();
                bool alreadyAdded = false;
                List<IBDWeapon>.Enumerator weap = weaponTypes.GetEnumerator();
                while (weap.MoveNext())
                {
                    if (weap.Current == null) continue;
                    if (weap.Current.GetShortName() == weaponName)
                    {
                        alreadyAdded = true;
                        //break;
                    }
                }
                weap.Dispose();

                //dont add empty rocket pods
                if (weapon.Current.GetWeaponClass() == WeaponClasses.Rocket &&
                    weapon.Current.GetPart().FindModuleImplementing<RocketLauncher>().GetRocketResource().amount < 1)
                {
                    continue;
                }

                if (!alreadyAdded)
                {
                    weaponTypes.Add(weapon.Current);
                }

                EngageableWeapon engageableWeapon = weapon.Current as EngageableWeapon;

                if (engageableWeapon != null)
                {
                    if (engageableWeapon.GetEngageAirTargets()) weaponTypesAir.Add(weapon.Current);
                    if (engageableWeapon.GetEngageMissileTargets()) weaponTypesMissile.Add(weapon.Current);
                    if (engageableWeapon.GetEngageGroundTargets()) weaponTypesGround.Add(weapon.Current);
                    if (engageableWeapon.GetEngageSLWTargets()) weaponTypesSLW.Add(weapon.Current);
                }
                else
                {
                    weaponTypesAir.Add(weapon.Current);
                    weaponTypesMissile.Add(weapon.Current);
                    weaponTypesGround.Add(weapon.Current);
                    weaponTypesSLW.Add(weapon.Current);
                }  
            }
            weapon.Dispose();

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
                float counter = 0; // Used to get a count of the ripple weapons.  a float version of rippleGunCount.
                gunRippleIndex = 0;
                // This value will be incremented as we set the ripple weapons
                rippleGunCount = 0;
                float weaponRpm = 0;  // used to set the rippleGunRPM

                // JDK:  this looks like it can be greatly simplified...
                #region Old Code (for reference.  remove when satisfied new code works as expected.
                //List<ModuleWeapon> tempListModuleWeapon = vessel.FindPartModulesImplementing<ModuleWeapon>();
                //foreach (ModuleWeapon weapon in tempListModuleWeapon)
                //{
                //    if (selectedWeapon.GetShortName() == weapon.GetShortName())
                //    {
                //        weapon.rippleIndex = Mathf.RoundToInt(counter);
                //        weaponRPM = weapon.roundsPerMinute;
                //        ++counter;
                //        rippleGunCount++;
                //    }
                //}
                //gunRippleRpm = weaponRPM * counter;
                //float timeDelayPerGun = 60f / (weaponRPM * counter);
                ////number of seconds between each gun firing; will reduce with increasing RPM or number of guns
                //foreach (ModuleWeapon weapon in tempListModuleWeapon)
                //{
                //    if (selectedWeapon.GetShortName() == weapon.GetShortName())
                //    {
                //        weapon.initialFireDelay = timeDelayPerGun; //set the time delay for moving to next index
                //    }
                //}

                //RippleOption ro; //ripplesetup and stuff
                //if (rippleDictionary.ContainsKey(selectedWeapon.GetShortName()))
                //{
                //    ro = rippleDictionary[selectedWeapon.GetShortName()];
                //}
                //else
                //{
                //    ro = new RippleOption(currentGun.useRippleFire, 650); //take from gun's persistant value
                //    rippleDictionary.Add(selectedWeapon.GetShortName(), ro);
                //}

                //foreach (ModuleWeapon w in vessel.FindPartModulesImplementing<ModuleWeapon>())
                //{
                //    if (w.GetShortName() == selectedWeapon.GetShortName())
                //        w.useRippleFire = ro.rippleFire;
                //}
                #endregion

                // TODO:  JDK verify new code works as expected.
                // New code, simplified. 

                //First lest set the Ripple Option. Doing it first eliminates a loop.
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

                //Get ripple weapon count, so we don't have to enumerate the whole list again.
                List<ModuleWeapon> rippleWeapons = new List<ModuleWeapon>();
                List<ModuleWeapon>.Enumerator weapCnt = vessel.FindPartModulesImplementing<ModuleWeapon>().GetEnumerator();
                while (weapCnt.MoveNext())
                {
                    if (weapCnt.Current == null) continue;
                    if (selectedWeapon.GetShortName() != weapCnt.Current.GetShortName()) continue;
                    //guessing that all weapons of the same shortname have the same rpm, as we are storing the last found weapon value...
                    weaponRpm = weapCnt.Current.roundsPerMinute;
                    rippleWeapons.Add(weapCnt.Current);
                    counter++;
                }
                weapCnt.Dispose();

                gunRippleRpm = weaponRpm * counter; 
                //number of seconds between each gun firing; will reduce with increasing RPM or number of guns
                float timeDelayPerGun = 60f / (weaponRpm * counter);

                // Now lets act on the filtered list.
                List<ModuleWeapon>.Enumerator weapon = rippleWeapons.GetEnumerator();
                while (weapon.MoveNext())
                {
                    if (weapon.Current == null) continue;
                    // set the weapon ripple index just before we increment rippleGunCount.
                    weapon.Current.rippleIndex = rippleGunCount;
                    //set the time delay for moving to next index
                    weapon.Current.initialFireDelay = timeDelayPerGun;
                    weapon.Current.useRippleFire = ro.rippleFire;
                    rippleGunCount++;
                }
                weapon.Dispose();
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
                    List<MissileBase>.Enumerator ml = vessel.FindPartModulesImplementing<MissileBase>().GetEnumerator();
                    while (ml.MoveNext())
                    {
                        if (ml.Current == null) continue;
                        if (ml.Current.part.ShieldedFromAirstream) ml.Current.inCargoBay = true;
                    }
                    ml.Dispose();
                }

                if (CurrentMissile.inCargoBay)
                {
                    List<ModuleCargoBay>.Enumerator bay = vessel.FindPartModulesImplementing<ModuleCargoBay>().GetEnumerator();
                    while (bay.MoveNext())
                    {
                        if (bay.Current == null) continue;
                        if (CurrentMissile.part.airstreamShields.Contains(bay.Current))
                        {
                            ModuleAnimateGeneric anim = (ModuleAnimateGeneric)bay.Current.part.Modules.GetModule(bay.Current.DeployModuleIndex);

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
                                (ModuleAnimateGeneric)bay.Current.part.Modules.GetModule(bay.Current.DeployModuleIndex);

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
                    bay.Dispose();
                }
                else
                {
                    List<ModuleCargoBay>.Enumerator bay = vessel.FindPartModulesImplementing<ModuleCargoBay>().GetEnumerator();
                    while (bay.MoveNext())
                    {
                        if (bay.Current == null) continue;
                        ModuleAnimateGeneric anim =
                            (ModuleAnimateGeneric)bay.Current.part.Modules.GetModule(bay.Current.DeployModuleIndex);
                        string toggleOption = anim.Events["Toggle"].guiName;
                        if (toggleOption == "Close")
                        {
                            if (anim)
                            {
                                anim.Toggle();
                            }
                        }
                    }
                    bay.Dispose();
                }
            }
            else
            {
                List<ModuleCargoBay>.Enumerator bay = vessel.FindPartModulesImplementing<ModuleCargoBay>().GetEnumerator();
                while (bay.MoveNext())
                {
                    if (bay.Current == null) continue;
                    ModuleAnimateGeneric anim = (ModuleAnimateGeneric)bay.Current.part.Modules.GetModule(bay.Current.DeployModuleIndex);
                    string toggleOption = anim.Events["Toggle"].guiName;
                    if (toggleOption == "Close")
                    {
                        if (anim)
                        {
                            anim.Toggle();
                        }
                    }
                }
                bay.Dispose();
            }

            return openingBays;
        }

        void SetRotaryRails()
        {
            if (weaponIndex == 0) return;

            if (selectedWeapon == null) return;

            if (
                !(selectedWeapon.GetWeaponClass() == WeaponClasses.Missile ||
                  selectedWeapon.GetWeaponClass() == WeaponClasses.Bomb ||
                  selectedWeapon.GetWeaponClass() == WeaponClasses.SLW)) return;

            if (!CurrentMissile) return;

            //TODO BDModularGuidance: Rotatory Rail?
            MissileLauncher cm = CurrentMissile as MissileLauncher;
            if (cm == null) return;
            List<BDRotaryRail>.Enumerator rotRail = vessel.FindPartModulesImplementing<BDRotaryRail>().GetEnumerator();
            while (rotRail.MoveNext())
            {
                if (rotRail.Current == null) continue;
                if (rotRail.Current.missileCount == 0)
                {
                    //Debug.Log("SetRotaryRails(): rail has no missiles");
                    continue;
                }

                //Debug.Log("[BDArmory]: SetRotaryRails(): rotRail.Current.readyToFire: " + rotRail.Current.readyToFire + ", rotRail.Current.readyMissile: " + ((rotRail.Current.readyMissile != null) ? rotRail.Current.readyMissile.part.name : "null") + ", rotRail.Current.nextMissile: " + ((rotRail.Current.nextMissile != null) ? rotRail.Current.nextMissile.part.name : "null"));

                //Debug.Log("[BDArmory]: current missile: " + cm.part.name);

                if (rotRail.Current.readyToFire)
                {
                    if (!rotRail.Current.readyMissile)
                    {
                        rotRail.Current.RotateToMissile(cm);
                        return;
                    }

                    if (rotRail.Current.readyMissile.part.name != cm.part.name)
                    {
                        rotRail.Current.RotateToMissile(cm);
                    }
                }
                else
                {
                    if (!rotRail.Current.nextMissile)
                    {
                        rotRail.Current.RotateToMissile(cm);
                    }
                    else if (rotRail.Current.nextMissile.part.name != cm.part.name)
                    {
                        rotRail.Current.RotateToMissile(cm);
                    }
                }
            }
            rotRail.Dispose();
        }

        void SetMissileTurrets()
        {
            MissileLauncher cm = CurrentMissile as MissileLauncher;
            if (cm == null) return;
            List<MissileTurret>.Enumerator mt = vessel.FindPartModulesImplementing<MissileTurret>().GetEnumerator();
            while (mt.MoveNext())
            {
                if (mt.Current == null) continue;
                if (weaponIndex > 0 && cm && mt.Current.ContainsMissileOfType(cm))
                {
                    if (!mt.Current.activeMissileOnly || cm.missileTurret == mt.Current)
                    {
                        mt.Current.EnableTurret();
                    }
                    else
                    {
                        mt.Current.DisableTurret();
                    }
                }
                else
                {
                    mt.Current.DisableTurret();
                }
            }
            mt.Dispose();
        }

        void SetRocketTurrets()
        {
            RocketLauncher currentTurret = null;
            if (selectedWeapon != null && selectedWeapon.GetWeaponClass() == WeaponClasses.Rocket)
            {
                RocketLauncher srl = selectedWeapon.GetPart().FindModuleImplementing<RocketLauncher>();
                if (srl && srl.turret)
                {
                    currentTurret = srl;
                }
            }

            List<RocketLauncher>.Enumerator rl = vessel.FindPartModulesImplementing<RocketLauncher>().GetEnumerator();
            while (rl.MoveNext())
            {
                if (rl.Current == null) continue;
                rl.Current.weaponManager = this;
                if (rl.Current.turret)
                {
                    if (currentTurret && rl.Current.part.name == currentTurret.part.name)
                    {
                        rl.Current.EnableTurret();
                    }
                    else
                    {
                        rl.Current.DisableTurret();
                    }
                }
            }
            rl.Dispose();
        }

        void FindNextRocket(RocketLauncher lastFired)
        {
            if (weaponIndex > 0 && selectedWeapon.GetWeaponClass() == WeaponClasses.Rocket)
            {
                disabledRocketAimers = false;

                //first check sym of last fired
                if (lastFired && lastFired.part.name == selectedWeapon.GetPart().name)
                {
                    List<Part>.Enumerator pSym = lastFired.part.symmetryCounterparts.GetEnumerator();
                    while (pSym.MoveNext())
                    {
                        if (pSym.Current == null) continue;
                        bool hasRocket = false;
                        RocketLauncher rl = pSym.Current.FindModuleImplementing<RocketLauncher>();
                        IEnumerator<PartResource> r = rl.part.Resources.GetEnumerator();
                        while (r.MoveNext())
                        {
                            if (r.Current == null) continue;
                            if (r.Current.resourceName != rl.rocketType || !(r.Current.amount > 0)) continue;
                            hasRocket = true;
                            break;
                        }
                        r.Dispose();

                        if (!hasRocket) continue;
                        if (currentRocket) currentRocket.drawAimer = false;

                        rl.drawAimer = true;
                        currentRocket = rl;
                        selectedWeapon = currentRocket;
                        return;
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
                List<RocketLauncher>.Enumerator orl = vessel.FindPartModulesImplementing<RocketLauncher>().GetEnumerator();
                while (orl.MoveNext())
                {
                    if (orl.Current == null) continue;
                    if (!foundRocket && orl.Current.part.partInfo.title == selectedWeapon.GetPart().partInfo.title)
                    {
                        bool hasRocket = false;
                        IEnumerator<PartResource> r = orl.Current.part.Resources.GetEnumerator();
                        while (r.MoveNext())
                        {
                            if (r.Current == null) continue;
                            if (r.Current.amount > 0) hasRocket = true;
                            else orl.Current.drawAimer = false;
                        }
                        r.Dispose();

                        if (!hasRocket) continue;
                        if (currentRocket != null) currentRocket.drawAimer = false;
                        orl.Current.drawAimer = true;
                        currentRocket = orl.Current;
                        selectedWeapon = currentRocket;
                        //return;
                        foundRocket = true;
                    }
                    else
                    {
                        orl.Current.drawAimer = false;
                    }
                }
                orl.Dispose();
            }
            //not using a rocket, disable reticles.
            else if (!disabledRocketAimers)
            {
                List<RocketLauncher>.Enumerator rl = vessel.FindPartModulesImplementing<RocketLauncher>().GetEnumerator();
                while (rl.MoveNext())
                {
                    if (rl.Current == null) continue;
                    rl.Current.drawAimer = false;
                    currentRocket = null;
                }
                rl.Dispose();
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
            List<Part>.Enumerator pSym = p.symmetryCounterparts.GetEnumerator();
            while (pSym.MoveNext())
            {
                if (pSym.Current == null) continue;
                if (pSym.Current != p && pSym.Current.vessel == vessel)
                {
                    return pSym.Current;
                }
            }
            pSym.Dispose();

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
                List<MissileBase>.Enumerator ml = vessel.FindPartModulesImplementing<MissileBase>().GetEnumerator();
                while (ml.MoveNext())
                {
                    if (ml.Current == null) continue;
                    MissileLauncher launcher = ml.Current as MissileLauncher;
                    if (launcher != null)
                    {
                        if (launcher.part.name != weaponArray[weaponIndex].GetPart()?.name) continue;
                    }
                    else
                    {
                        BDModularGuidance guidance = ml.Current as BDModularGuidance;
                        if (guidance != null)
                        { //We have set of parts not only a part
                            if (guidance.GetShortName() != weaponArray[weaponIndex]?.GetShortName()) continue;
                        }
                    }
                    if (firstMl == null) firstMl = ml.Current;

                    if (!FindSym(ml.Current.part))
                    {
                        return ml.Current;
                    }
                }
                ml.Dispose();
                return firstMl;
            }
            return null;
        }

        private MissileBase GetRotaryReadyMissile()
        {
            if (weaponIndex == 0) return null;
            if (weaponArray[weaponIndex].GetWeaponClass() == WeaponClasses.Bomb ||
                weaponArray[weaponIndex].GetWeaponClass() == WeaponClasses.Missile ||
                weaponArray[weaponIndex].GetWeaponClass() == WeaponClasses.SLW)
            {
                //TODO BDModularGuidance: Implemente rotaryRail support
                MissileLauncher missile = CurrentMissile as MissileLauncher;
                if (missile == null) return null;
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
                List<MissileLauncher>.Enumerator ml = vessel.FindPartModulesImplementing<MissileLauncher>().GetEnumerator();
                while (ml.MoveNext())
                {
                    if (ml.Current == null) continue;
                    if (ml.Current.part.name != weaponArray[weaponIndex].GetPart().name) continue;

                    if (!ml.Current.rotaryRail)
                    {
                        return ml.Current;
                    }
                    if (ml.Current.rotaryRail.readyToFire && ml.Current.rotaryRail.readyMissile.part.name == weaponArray[weaponIndex].GetPart().name)
                    {
                        return ml.Current.rotaryRail.readyMissile;
                    }
                }
                ml.Dispose();
                return null;
            }
            return null;
        }

        bool CheckBombClearance(MissileBase ml)
        {
            if (!BDArmorySettings.BOMB_CLEARANCE_CHECK) return true;

            if (ml.part.ShieldedFromAirstream)
            {
                return false;
            }

            //TODO BDModularGuidance: Bombs and turrents
            MissileLauncher launcher = ml as MissileLauncher;
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

                    IEnumerator<Ray> rt = rays.AsEnumerable().GetEnumerator();
                    while (rt.MoveNext())
                    {
                        RaycastHit[] hits = Physics.RaycastAll(rt.Current, rayDistance, 557057);
                        IEnumerator<RaycastHit> t1 = hits.AsEnumerable().GetEnumerator();
                        while (t1.MoveNext() )
                        {
                            Part p = t1.Current.collider.GetComponentInParent<Part>();

                            if ((p == null || p == ml.part) && p != null) continue;
                            if (BDArmorySettings.DRAW_DEBUG_LABELS)
                                Debug.Log("[BDArmory]: RAYCAST HIT, clearance is FALSE! part=" + p?.name + ", collider=" + p?.collider);
                            return false;
                        }
                        t1.Dispose();
                    }
                    rt.Dispose();
                    return true;
                }

                //forward check for no-drop missiles
                RaycastHit[] hitparts = Physics.RaycastAll(new Ray(ml.MissileReferenceTransform.position, ml.GetForwardTransform()), 50, 557057);
                IEnumerator<RaycastHit> t = hitparts.AsEnumerable().GetEnumerator();
                while (t.MoveNext())
                {
                    Part p = t.Current.collider.GetComponentInParent<Part>();
                    if ((p == null || p == ml.part) && p != null) continue;
                    if (BDArmorySettings.DRAW_DEBUG_LABELS)
                        Debug.Log("[BDArmory]: RAYCAST HIT, clearance is FALSE! part=" + p?.name + ", collider=" + p?.collider);
                    return false;
                }
                t.Dispose();
            }
            return true;
        }

        void RefreshTargetingModules()
        {
            List<ModuleRadar>.Enumerator rad = radars.GetEnumerator();
            while (rad.MoveNext())
            {
                if (rad.Current == null) continue;
                rad.Current.EnsureVesselRadarData();
                if (rad.Current.radarEnabled) rad.Current.EnableRadar();
            }
            rad.Dispose();

            radars = vessel.FindPartModulesImplementing<ModuleRadar>();

            List<ModuleRadar>.Enumerator nRad = radars.GetEnumerator();
            while (nRad.MoveNext())
            {
                if (nRad.Current == null) continue;
                nRad.Current.EnsureVesselRadarData();
                if (nRad.Current.radarEnabled)
                {
                    nRad.Current.EnableRadar();
                }
            }
            nRad.Dispose();

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
                if (selectedWeapon.GetWeaponClass() != WeaponClasses.Missile) continue;
                List<MissileBase>.Enumerator ml = selectedWeapon.GetPart().FindModulesImplementing<MissileBase>().GetEnumerator();
                while (ml.MoveNext())
                {
                    if (ml.Current == null) continue;
                    if (ml.Current.TargetingMode == MissileBase.TargetingModes.AntiRad)
                    {
                        return true;
                    }
                    break;
                }
                ml.Dispose();
                //return;
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
            List<TargetInfo>.Enumerator finalTargets = BDATargetManager.GetAllTargetsExcluding(targetsTried, this).GetEnumerator();
            while (finalTargets.MoveNext())
            {
                if (finalTargets.Current == null) continue;
                SetTarget(finalTargets.Current);
                if (!SmartPickWeapon_EngagementEnvelope(finalTargets.Current)) continue;
                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                {
                    Debug.Log("[BDArmory]: " + vessel.vesselName + " is engaging a final target with " +
                              selectedWeapon.GetShortName());
                }
                return;
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

            float distance = Vector3.Distance(transform.position + vessel.Velocity(), target.position + target.velocity);
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
                List<IBDWeapon>.Enumerator item = weaponTypesMissile.GetEnumerator();
                while (item.MoveNext())
                {
                    if (item.Current == null) continue;
                    // candidate, check engagement envelope
                    if (!CheckEngagementEnvelope(item.Current, distance)) continue;
                    // weapon usable, if missile continue looking for lasers/guns, else take it
                    WeaponClasses candidateClass = item.Current.GetWeaponClass();

                    if (candidateClass == WeaponClasses.DefenseLaser)
                    {
                        // TODO: compare lasers which one is better for AA
                        targetWeapon = item.Current;
                        break; //always favour laser
                    }

                    if (candidateClass == WeaponClasses.Gun)
                    {
                        // For AAA, favour higher RPM
                        float candidateRPM = ((ModuleWeapon)item.Current).roundsPerMinute;

                        if ((targetWeapon != null) && (targetWeaponRPM > candidateRPM))
                            continue; //dont replace better guns (but do replace missiles)

                        targetWeapon = item.Current;
                        targetWeaponRPM = candidateRPM;
                    }

                    if (candidateClass != WeaponClasses.Missile) continue;
                    // TODO: for AA, favour higher thrust+turnDPS

                    MissileLauncher mlauncher = item.Current as MissileLauncher;
                    float candidateTDPS = 0f;
                        
                    if (mlauncher != null)
                    {
                        candidateTDPS = mlauncher.thrust + mlauncher.maxTurnRateDPS;
                    }
                    else
                    { //is modular missile
                        BDModularGuidance mm = item.Current as BDModularGuidance;
                        candidateTDPS = 5000;                                                     
                    }

                    if ((targetWeapon != null) && ((targetWeapon.GetWeaponClass() == WeaponClasses.Gun) || (targetWeaponTDPS > candidateTDPS)))
                        continue; //dont replace guns or better missiles

                    targetWeapon = item.Current;
                    targetWeaponTDPS = candidateTDPS;
                }
                item.Dispose();

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
                List<IBDWeapon>.Enumerator item = weaponTypesAir.GetEnumerator();
                while (item.MoveNext())
                {
                    if (item.Current == null) continue;
                    // candidate, check engagement envelope
                    if (!CheckEngagementEnvelope(item.Current, distance)) continue;
                    // weapon usable, if missile continue looking for lasers/guns, else take it
                    WeaponClasses candidateClass = item.Current.GetWeaponClass();

                    if (candidateClass == WeaponClasses.DefenseLaser)
                    {
                        // TODO: compare lasers which one is better for AA
                        targetWeapon = item.Current;
                        if (distance <= gunRange)
                            break;
                    }

                    if (candidateClass == WeaponClasses.Gun)
                    {
                        // For AAA, favour higher RPM
                        float candidateRPM = ((ModuleWeapon)item.Current).roundsPerMinute;

                        if ((targetWeapon != null) && (targetWeaponRPM > candidateRPM))
                            continue; //dont replace better guns (but do replace missiles)

                        targetWeapon = item.Current;
                        targetWeaponRPM = candidateRPM;
                    }

                    if (candidateClass != WeaponClasses.Missile) continue;
                    MissileLauncher mlauncher = item.Current as MissileLauncher;
                    float candidateTDPS = 0f;

                    if (mlauncher != null)
                    {
                        candidateTDPS = mlauncher.thrust + mlauncher.maxTurnRateDPS;
                    }
                    else
                    { //is modular missile
                        BDModularGuidance mm = item.Current as BDModularGuidance;
                        candidateTDPS = 5000;
                    }

                    if (targetWeapon == null)
                    {
                        targetWeapon = item.Current;
                        targetWeaponTDPS = candidateTDPS;
                    }
                    else if (distance > gunRange)
                    {
                        if (targetWeapon.GetWeaponClass() == WeaponClasses.Gun || targetWeaponTDPS > candidateTDPS)
                            continue; //dont replace guns or better missiles

                        targetWeapon = item.Current;
                        targetWeaponTDPS = candidateTDPS;
                    }
                }
                item.Dispose();
            }

            else if (target.isLanded)
            {
                // iterate over weaponTypesGround and pick suitable one based on engagementRange (and dynamic launch zone for missiles)
                // Prioritize by:
                // 1. ground attack missiles (cruise, gps, unguided) if target not moving
                // 2. ground attack missiles (guided) if target is moving
                // 3. Bombs / Rockets
                // 4. Guns
                List<IBDWeapon>.Enumerator item = weaponTypesGround.GetEnumerator();
                while (item.MoveNext())
                {
                    if (item.Current == null) continue;
                    // candidate, check engagement envelope
                    if (!CheckEngagementEnvelope(item.Current, distance)) continue;
                    // weapon usable, if missile continue looking for lasers/guns, else take it
                    WeaponClasses candidateClass = item.Current.GetWeaponClass();

                    if (candidateClass == WeaponClasses.Missile)
                    {
                        // TODO: compare missiles which one is better for ground attack
                        // Priority Sequence:
                        // - Antiradiation
                        // - guided missiles
                        // - by blast strength
                        targetWeapon = item.Current;
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
                                targetWeapon = item.Current;
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
                            targetWeapon = item.Current;
                    }


                    if ((candidateClass != WeaponClasses.Gun)) continue;
                    // Flying: prefer bombs/rockets/missiles
                    if (!vessel.LandedOrSplashed)
                        if (targetWeapon != null)
                            // dont replace bombs/rockets
                            continue;
                    // else:
                    if ((distance > gunRange) && (targetWeapon != null))
                        continue;
                    // For Ground Attack, favour higher blast strength
                    float candidateImpact = ((ModuleWeapon)item.Current).cannonShellPower * ((ModuleWeapon)item.Current).cannonShellRadius + ((ModuleWeapon)item.Current).cannonShellHeat;

                    if ((targetWeapon != null) && (targetWeaponImpact > candidateImpact))
                        continue; //dont replace better guns

                    targetWeapon = item.Current;
                    targetWeaponImpact = candidateImpact;
                }

            }
                //if we have a torpedo use it
            else if (target.isSplashed)
            { 
                List<IBDWeapon>.Enumerator item = weaponTypesSLW.GetEnumerator();
                while (item.MoveNext())
                {
                    if (item.Current == null) continue;
                    if (CheckEngagementEnvelope(item.Current, distance))
                    {
                        if (item.Current.GetMissileType().ToLower() == "torpedo")
                        {
                            targetWeapon = item.Current;
                            break;
                        }
                    }
                }
                item.Dispose();
            }              
            else if (target.isUnderwater)
            {
                // iterate over weaponTypesSLW (Ship Launched Weapons) and pick suitable one based on engagementRange
                // Prioritize by:
                // 1. Depth Charges
                // 2. Torpedos
                List<IBDWeapon>.Enumerator item = weaponTypesSLW.GetEnumerator();
                while (item.MoveNext())
                {
                    if (item.Current == null) continue;
                    if (CheckEngagementEnvelope(item.Current, distance))
                    {
                        if (item.Current.GetMissileType().ToLower() == "depthcharge")
                        {
                            targetWeapon = item.Current;
                            break;
                        }
                        if (item.Current.GetMissileType().ToLower() != "torpedo") continue;
                        targetWeapon = item.Current;
                        break;
                    }
                }
                item.Dispose();
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
            EngageableWeapon engageableWeapon = weaponCandidate as EngageableWeapon;

            if (engageableWeapon == null) return true;
            if (!engageableWeapon.engageEnabled) return true;
            if (distanceToTarget < engageableWeapon.GetEngagementRangeMin()) return false;
            if (distanceToTarget > engageableWeapon.GetEngagementRangeMax()) return false;

            switch (weaponCandidate.GetWeaponClass())
            {

                case WeaponClasses.DefenseLaser:
                // TODO: is laser treated like a gun?

                case WeaponClasses.Gun:
                    ModuleWeapon gun = (ModuleWeapon)weaponCandidate;

                    // check yaw range of turret
                    ModuleTurret turret = gun.turret;
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
                    MissileBase ml = (MissileBase)weaponCandidate;

                    // lock radar if needed
                    if (ml.TargetingMode == MissileBase.TargetingModes.Radar)
                    {
                        List<ModuleRadar>.Enumerator rd = radars.GetEnumerator();
                        while (rd.MoveNext())
                        {
                            if (rd.Current == null) continue;
                            if (!rd.Current.canLock) continue;
                            rd.Current.EnableRadar();
                            break;
                        }
                        rd.Dispose();
                    }

                    // check DLZ
                    MissileLaunchParams dlz = MissileLaunchParams.GetDynamicLaunchParams(ml, guardTarget.Velocity(), guardTarget.transform.position);
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
                    RocketLauncher rocketlauncher = (RocketLauncher)weaponCandidate;
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

        public bool CanSeeTarget(TargetInfo target)
        {
            // fix cheating: we can see a target IF we either have a visual on it, OR it has been detected on radar/sonar
            if (Time.time - target.detectedTime < targetScanInterval)
                return true;

            // can we get a visual sight of the target?
            if ((target.Vessel.transform.position - transform.position).sqrMagnitude < guardRange*guardRange)
            {
                if (RadarUtils.TerrainCheck(target.Vessel.transform.position, transform.position))
                {
                    return false;
                }

                return true;
            }

            return false;
        }


        /// <summary>
        /// Override for legacy targeting only! Remove when removing legcy mode!
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public bool CanSeeTarget(Vessel target)
        {
            // can we get a visual sight of the target?
            if ((target.transform.position - transform.position).sqrMagnitude < guardRange * guardRange)
            {
                if (RadarUtils.TerrainCheck(target.transform.position, transform.position))
                {
                    return false;
                }

                return true;
            }

            return false;
        }


        void ScanAllTargets()
        {
            //get a target.
            //float angle = 0;
            List<Vessel>.Enumerator v = FlightGlobals.Vessels.GetEnumerator();
            while (v.MoveNext())
            {
                if (v.Current == null) continue;
                if (!v.Current.loaded) continue;
                float distance = (transform.position - v.Current.transform.position).sqrMagnitude;
                if (!(distance < guardRange*guardRange) || !CanSeeTarget(v.Current)) continue;
                float angle = Vector3.Angle(-transform.forward, v.Current.transform.position - transform.position);
                if (!(angle < guardAngle / 2)) continue;
                List<MissileBase>.Enumerator missile = v.Current.FindPartModulesImplementing<MissileBase>().GetEnumerator();
                while (missile.MoveNext())
                {
                    if (missile.Current == null) continue;
                    if (!missile.Current.HasFired || missile.Current.Team == team) continue;
                    BDATargetManager.ReportVessel(v.Current, this);
                    if (!isFlaring && missile.Current.TargetingMode == MissileBase.TargetingModes.Heat && Vector3.Angle(missile.Current.GetForwardTransform(), transform.position - missile.Current.transform.position) < 20)
                    {
                        StartCoroutine(FlareRoutine(targetScanInterval * 0.75f));
                    }
                    break;
                }
                missile.Dispose();

                List<MissileFire>.Enumerator mF = v.Current.FindPartModulesImplementing<MissileFire>().GetEnumerator();
                while (mF.MoveNext())
                {
                    if (mF.Current == null) continue;
                    if (mF.Current.team == team || !mF.Current.vessel.IsControllable || !mF.Current.vessel.isCommandable) continue;
                    BDATargetManager.ReportVessel(v.Current, this);
                    break;
                }
                mF.Dispose();
            }
            v.Dispose();
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

            MissileLauncher launcher = ml as MissileLauncher;
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

                float scanRadius = CurrentMissile.lockedSensorFOV * 2;
                float maxOffBoresight = CurrentMissile.maxOffBoresight * 0.85f;

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
                    if (rwr.pingsData[i].exists && (rwr.pingWorldPositions[i] - v.position).sqrMagnitude < 20*20)
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
            if (!isArmed || !BDArmorySettings.ALLOW_LEGACY_TARGETING) return;
            Vessel acquiredTarget = null;
            float smallestAngle = 8;

            if (Time.time - targetListTimer > 1)
            {
                loadedVessels.Clear();
                List<Vessel>.Enumerator v = FlightGlobals.Vessels.GetEnumerator();
                while (v.MoveNext())
                {
                    if (v.Current == null) continue;
                    float viewAngle = Vector3.Angle(-transform.forward, v.Current.transform.position - transform.position);
                    if (!v.Current.loaded || !(viewAngle < smallestAngle)) continue;
                    if (!v.Current.vesselName.Contains("(fired)")) loadedVessels.Add(v.Current);
                }
                v.Dispose();
            }

            List<Vessel>.Enumerator vl = loadedVessels.GetEnumerator();
            while (vl.MoveNext())
            {
                if (vl.Current == null) continue;
                float viewAngle = Vector3.Angle(-transform.forward, vl.Current.transform.position - transform.position);
                //if(vl.Current!= vessel && vl.Current.loaded) Debug.Log ("view angle: " + viewAngle);
                if (vl.Current == vessel || !vl.Current.loaded || !(viewAngle < smallestAngle) ||
                    !CanSeeTarget(vl.Current)) continue;
                acquiredTarget = vl.Current;
                smallestAngle = viewAngle;
            }
            vl.Dispose();

            if (acquiredTarget == null || acquiredTarget == (Vessel) FlightGlobals.fetch.VesselTarget) return;
            if (BDArmorySettings.DRAW_DEBUG_LABELS)
            {
                Debug.Log("[BDArmory]: found target! : " + acquiredTarget.name);
            }
            FlightGlobals.fetch.SetVesselTarget(acquiredTarget);
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
                //make this not have to go every frame
                List<ModuleWeapon>.Enumerator weapon = vessel.FindPartModulesImplementing<ModuleWeapon>().GetEnumerator();
                while (weapon.MoveNext())
                {
                    if (weapon.Current == null) continue;
                    if (weapon.Current.part.partInfo.title != selectedWeapon.GetPart().partInfo.title) continue;
                    weapon.Current.EnableWeapon();
                    weapon.Current.aiControlled = true;
                    weapon.Current.maxAutoFireCosAngle = vessel.LandedOrSplashed ? 0.9993908f : 0.9975641f; //2 : 4 degrees
                }
                weapon.Dispose();
            }

            if (guardTarget)
            {
                //release target if out of range
                if (BDArmorySettings.ALLOW_LEGACY_TARGETING &&
                    (guardTarget.transform.position - transform.position).sqrMagnitude > guardRange*guardRange)
                {
                    SetTarget(null);
                }
            }
            else if (selectedWeapon != null && selectedWeapon.GetWeaponClass() == WeaponClasses.Gun)
            {
                List<ModuleWeapon>.Enumerator weapon = vessel.FindPartModulesImplementing<ModuleWeapon>().GetEnumerator();
                while (weapon.MoveNext())
                {
                    if (weapon.Current == null) continue;
                    if (weapon.Current.part.partInfo.title != selectedWeapon.GetPart().partInfo.title) continue;
                    weapon.Current.autoFire = false;
                    weapon.Current.legacyTargetVessel = null;
                }
                weapon.Dispose();
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
                            MissileLaunchParams dlz = MissileLaunchParams.GetDynamicLaunchParams(CurrentMissile, guardTarget.Velocity(), guardTarget.CoM);

                            if (targetAngle > guardAngle / 2) //dont fire yet if target out of guard angle
                            {
                                launchAuthorized = false;
                            }
                            else if (targetDistance >= dlz.maxLaunchRange || targetDistance <= dlz.minLaunchRange)  //fire the missile only if target is further than missiles min launch range
                            {
                                launchAuthorized = false;
                            }

                            if (BDArmorySettings.DRAW_DEBUG_LABELS)
                                Debug.Log("[BDArmory]:" + vessel.vesselName + " launchAuth=" + launchAuthorized + ", pilotAut=" + pilotAuthorized + ", missilesAway/Max=" + missilesAway + "/" + maxMissilesOnTarget);

                            if (missilesAway < maxMissilesOnTarget)
                            {
                                if (!guardFiringMissile && launchAuthorized &&
                                    (pilotAuthorized || !BDArmorySettings.ALLOW_LEGACY_TARGETING))
                                {
                                    StartCoroutine(GuardMissileRoutine());
                                }
                            }
                            else if (BDArmorySettings.DRAW_DEBUG_LABELS)
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
                viewReferenceTransform, angleDelta, out results, guardRange);

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
                        if (nearbyThreat.weaponManager.team != team &&
                            nearbyFriendly.weaponManager.team == team)
                        //turns out that there's no check for AI on the same team going after each other due to this.  Who knew?
                        {
                            if (nearbyThreat == currentTarget && nearbyFriendly.weaponManager.currentTarget != null)
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
                List<RocketLauncher>.Enumerator weapon = vessel.FindPartModulesImplementing<RocketLauncher>().GetEnumerator();
                while (weapon.MoveNext())
                {
                    if (weapon.Current == null) continue;
                    if (weapon.Current.GetShortName() != selectedWeaponString) continue;
                    if (BDArmorySettings.DRAW_DEBUG_LABELS)
                    {
                        Debug.Log("[BDArmory]: Setting rocket to auto fire");
                    }
                    weapon.Current.legacyGuardTarget = guardTarget;
                    weapon.Current.autoFireStartTime = Time.time;
                    //weapon.Current.autoFireDuration = targetScanInterval / 2;
                    weapon.Current.autoFireDuration = (fireBurstLength < 0.5) ? targetScanInterval / 2 : fireBurstLength;
                    weapon.Current.autoRippleRate = rippleFire ? rippleRPM : 0;
                }
                weapon.Dispose();
            }
            else
            {
                List<ModuleWeapon>.Enumerator weapon = vessel.FindPartModulesImplementing<ModuleWeapon>().GetEnumerator();
                while (weapon.MoveNext())
                {
                    if (weapon.Current == null) continue;
                    if (weapon.Current.part.partInfo.title != selectedWeapon.GetPart().partInfo.title) continue;
                    weapon.Current.legacyTargetVessel = guardTarget;
                    weapon.Current.autoFireTimer = Time.time;
                    //weapon.Current.autoFireLength = 3 * targetScanInterval / 4;
                    weapon.Current.autoFireLength = (fireBurstLength < 0.5) ? targetScanInterval / 2 : fireBurstLength;
                }
                weapon.Dispose();
            }
        }

        public void SetOverrideTarget(TargetInfo target)
        {
            overrideTarget = target;
            targetScanTimer = -100;
        }

        public void UpdateMaxGuardRange()
        {
            UI_FloatRange rangeEditor = (UI_FloatRange)Fields["guardRange"].uiControlEditor;
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
                List<RocketLauncher>.Enumerator rl = vessel.FindPartModulesImplementing<RocketLauncher>().GetEnumerator();
                while (rl.MoveNext())
                {
                    if (rl.Current == null) continue;
                    if (rl.Current.part.partInfo.title != selectedWeapon.GetPart().partInfo.title) continue;
                    float gimbalTolerance = vessel.LandedOrSplashed ? 0 : 15;
                    if (!(rl.Current.maxTargetingRange >= finalDistance) ||
                        !TargetInTurretRange(rl.Current.turret, gimbalTolerance)) continue;
                    if (BDArmorySettings.DRAW_DEBUG_LABELS)
                    {
                        Debug.Log("[BDArmory]: " + selectedWeapon + " is valid!");
                    }
                    return 1;
                }
                rl.Dispose();
            }
            else
            {
                List<ModuleWeapon>.Enumerator weapon = vessel.FindPartModulesImplementing<ModuleWeapon>().GetEnumerator();
                while (weapon.MoveNext())
                {
                    if (weapon.Current == null) continue;
                    if (weapon.Current.part.partInfo.title != selectedWeapon.GetPart().partInfo.title) continue;
                    float gimbalTolerance = vessel.LandedOrSplashed ? 0 : 15;
                    if (((!vessel.LandedOrSplashed && pilotAI) || (TargetInTurretRange(weapon.Current.turret, gimbalTolerance))) && weapon.Current.maxEffectiveDistance >= finalDistance)
                    {
                        if (weapon.Current.isOverheated)
                        {
                            if (BDArmorySettings.DRAW_DEBUG_LABELS)
                            {
                                Debug.Log("[BDArmory]: " + selectedWeapon + " is overheated!");
                            }
                            return -1;
                        }
                        if (CheckAmmo(weapon.Current) || BDArmorySettings.INFINITE_AMMO)
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
                        Debug.Log("[BDArmory]: " + selectedWeapon + " cannot reach target (" + distance + " vs " + weapon.Current.maxEffectiveDistance + ", yawRange: " + weapon.Current.yawRange + "). Continuing.");
                    }
                    //else return 0;
                }
                weapon.Dispose();
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
            float signedAnglePitch = Misc.Misc.SignedAngle(turretTransform.forward, directionPitch, turretTransform.up);
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
            List<Part>.Enumerator p = vessel.parts.GetEnumerator();
            while (p.MoveNext())
            {
                if (p.Current == null) continue;
                IEnumerator<PartResource> resource = p.Current.Resources.GetEnumerator();
                while (resource.MoveNext())
                {
                    if (resource.Current == null) continue;
                    if (resource.Current.resourceName != ammoName) continue;
                    if (resource.Current.amount > 0)
                    {
                        return true;
                    }
                }
                resource.Dispose();
            }
            p.Dispose();

            return false;
        }

        void ToggleTurret()
        {
            List<ModuleWeapon>.Enumerator weapon = vessel.FindPartModulesImplementing<ModuleWeapon>().GetEnumerator();
            while (weapon.MoveNext())
            {
                if (weapon.Current == null) continue;
                if (selectedWeapon == null || weapon.Current.part.partInfo.title != selectedWeapon.GetPart().partInfo.title)
                {
                    weapon.Current.DisableWeapon();
                }
                else
                {
                    weapon.Current.EnableWeapon();
                }
            }
            weapon.Dispose();
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

            if (!showBombAimer && (!guardMode || weaponIndex <= 0 ||
                                   selectedWeapon.GetWeaponClass() != WeaponClasses.Bomb)) return;
            MissileBase ml = bombPart.GetComponent<MissileBase>();

            float simDeltaTime = 0.1f;
            float simTime = 0;
            Vector3 dragForce = Vector3.zero;
            Vector3 prevPos = ml.MissileReferenceTransform.position;
            Vector3 currPos = ml.MissileReferenceTransform.position;
            //Vector3 simVelocity = vessel.rb_velocity;
            Vector3 simVelocity = vessel.Velocity(); //Issue #92

            MissileLauncher launcher = ml as MissileLauncher;
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
                if (Physics.Raycast(ray, out hitInfo, Vector3.Distance(prevPos, currPos), (1 << 15) | (1 << 17)))
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

