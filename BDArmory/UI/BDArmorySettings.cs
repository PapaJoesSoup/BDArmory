using System;
using System.Collections.Generic;
using System.Reflection;
using BDArmory.Armor;
using BDArmory.Control;
using BDArmory.Core.Extension;
using BDArmory.CounterMeasure;
using BDArmory.Misc;
using BDArmory.Parts;
using BDArmory.Radar;
using UnityEngine;

namespace BDArmory.UI
{
    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    public class BDArmorySettings : MonoBehaviour
    {
        public static string settingsConfigURL = "GameData/BDArmory/settings.cfg";
        
        //=======configurable settings
        [BDAPersistantSettingsField] public static bool INSTAKILL;
        [BDAPersistantSettingsField] public static bool BULLET_HITS = true;
        [BDAPersistantSettingsField] public static float PHYSICS_RANGE = 0;
        [BDAPersistantSettingsField] public static bool EJECT_SHELLS = true;
        [BDAPersistantSettingsField] public static bool SHELL_COLLISIONS = true;
        [BDAPersistantSettingsField] public static bool INFINITE_AMMO;
        [BDAPersistantSettingsField] public static bool DRAW_DEBUG_LINES;
        [BDAPersistantSettingsField] public static bool DRAW_DEBUG_LABELS;
        [BDAPersistantSettingsField] public static bool DRAW_AIMERS = true;
        [BDAPersistantSettingsField] public static bool AIM_ASSIST = true;
        [BDAPersistantSettingsField] public static bool REMOTE_SHOOTING;
        [BDAPersistantSettingsField] public static bool BOMB_CLEARANCE_CHECK = true;
        [BDAPersistantSettingsField] public static float DMG_MULTIPLIER = 100;
        [BDAPersistantSettingsField] public static float FLARE_CHANCE_FACTOR = 25;

        public static bool SMART_GUARDS = true;

        [BDAPersistantSettingsField] public static float MAX_BULLET_RANGE = 8000;
        [BDAPersistantSettingsField] public static float TRIGGER_HOLD_TIME = 0.3f;
        [BDAPersistantSettingsField] public static bool ALLOW_LEGACY_TARGETING = true;
        [BDAPersistantSettingsField] public static float TARGET_CAM_RESOLUTION = 1024;
        [BDAPersistantSettingsField] public static bool BW_TARGET_CAM = true;
        [BDAPersistantSettingsField] public static float SMOKE_DEFLECTION_FACTOR = 10;
        [BDAPersistantSettingsField] public static float FLARE_THERMAL = 1900;
        [BDAPersistantSettingsField] public static float BDARMORY_UI_VOLUME = 0.35f;
        [BDAPersistantSettingsField] public static float BDARMORY_WEAPONS_VOLUME = 0.32f;
        [BDAPersistantSettingsField] public static float MAX_GUARD_VISUAL_RANGE = 40000;
        [BDAPersistantSettingsField] public static float MAX_ACTIVE_RADAR_RANGE = 40000;
        [BDAPersistantSettingsField] public static float MAX_ENGAGEMENT_RANGE = 40000;
        [BDAPersistantSettingsField] public static float GLOBAL_LIFT_MULTIPLIER = 0.20f;
        [BDAPersistantSettingsField] public static float GLOBAL_DRAG_MULTIPLIER = 4f;
        [BDAPersistantSettingsField] public static float IVA_LOWPASS_FREQ = 2500;
        [BDAPersistantSettingsField] public static bool PEACE_MODE = false;
        [BDAPersistantSettingsField] public static bool ADVANCED_EDIT = false;
        [BDAPersistantSettingsField] public static bool IGNORE_TERRAIN_CHECK= false;



        //=======Window position settings Git Issue #13
        [BDAWindowSettingsField] public static Rect WindowRectToolbar;
        [BDAWindowSettingsField] public static Rect WindowRectGps;
        [BDAWindowSettingsField] public static Rect WindowRectSettings;
        [BDAWindowSettingsField] public static Rect WindowRectRwr;

        //reflection field lists
        FieldInfo[] iFs;

        FieldInfo[] inputFields
        {
            get
            {
                if (iFs == null)
                {
                    iFs = typeof(BDInputSettingsFields).GetFields();
                }
                return iFs;
            }
        }

        //EVENTS
        public delegate void VolumeChange();
        public static event VolumeChange OnVolumeChange;
        public delegate void SavedSettings();
        public static event SavedSettings OnSavedSettings;
        public delegate void PeaceEnabled();
        public static event PeaceEnabled OnPeaceEnabled;

        //particle optimization
        public static int numberOfParticleEmitters = 0;
        public static BDArmorySettings Instance;
        public static bool GAME_UI_ENABLED = true;

        //settings gui
        public bool settingsGuiEnabled;
        public string fireKeyGui;

        //editor alignment
        public static bool showWeaponAlignment;

        //toolbar gui
        public static bool hasAddedButton = false;
        public static bool toolbarGuiEnabled;
        float toolWindowWidth = 300;
        float toolWindowHeight = 100;
        bool showWeaponList;
        bool showGuardMenu;
        bool showModules;
        int numberOfModules;

        //gps window
        public bool showingGPSWindow
        {
            get { return showGPSWindow; }
        }

        bool showGPSWindow;
        float gpsEntryCount;
        float gpsEntryHeight = 24;
        float gpsBorder = 5;
        bool editingGPSName;
        int editingGPSNameIndex;
        bool hasEnteredGPSName;
        string newGPSName = String.Empty;

        public MissileFire ActiveWeaponManager;
        public bool missileWarning;
        public float missileWarningTime = 0;

        //load range stuff
        VesselRanges combatVesselRanges = new VesselRanges();
        float physRangeTimer;

        public static List<CMFlare> Flares = new List<CMFlare>();

        //gui styles
        GUIStyle centerLabel;
        GUIStyle centerLabelRed;
        GUIStyle centerLabelOrange;
        GUIStyle centerLabelBlue;
        GUIStyle leftLabel;
        GUIStyle leftLabelRed;
        GUIStyle rightLabelRed;
        GUIStyle leftLabelGray;
        GUIStyle rippleSliderStyle;
        GUIStyle rippleThumbStyle;
        GUIStyle kspTitleLabel;
        GUIStyle middleLeftLabel;
        GUIStyle middleLeftLabelOrange;
        GUIStyle targetModeStyle;
        GUIStyle targetModeStyleSelected;

        public enum BDATeams
        {
            A,
            B,
            None
        };

        //competition mode
        float competitionDist = 8000;
        string compDistGui = "8000";

        #region Textures
        public static string textureDir = "BDArmory/Textures/";

        bool drawCursor;
        Texture2D cursorTexture = GameDatabase.Instance.GetTexture(textureDir + "aimer", false);

        private Texture2D dti;

        public Texture2D directionTriangleIcon
        {
            get { return dti ? dti : dti = GameDatabase.Instance.GetTexture(textureDir + "directionIcon", false); }
        }

        private Texture2D cgs;

        public Texture2D crossedGreenSquare
        {
            get { return cgs ? cgs : cgs = GameDatabase.Instance.GetTexture(textureDir + "crossedGreenSquare", false); }
        }

        private Texture2D dlgs;

        public Texture2D dottedLargeGreenCircle
        {
            get
            {
                return dlgs
                    ? dlgs
                    : dlgs = GameDatabase.Instance.GetTexture(textureDir + "dottedLargeGreenCircle", false);
            }
        }

        private Texture2D ogs;

        public Texture2D openGreenSquare
        {
            get { return ogs ? ogs : ogs = GameDatabase.Instance.GetTexture(textureDir + "openGreenSquare", false); }
        }

        private Texture2D gdott;

        public Texture2D greenDotTexture
        {
            get { return gdott ? gdott : gdott = GameDatabase.Instance.GetTexture(textureDir + "greenDot", false); }
        }

        private Texture2D gdt;

        public Texture2D greenDiamondTexture
        {
            get { return gdt ? gdt : gdt = GameDatabase.Instance.GetTexture(textureDir + "greenDiamond", false); }
        }

        private Texture2D lgct;

        public Texture2D largeGreenCircleTexture
        {
            get { return lgct ? lgct : lgct = GameDatabase.Instance.GetTexture(textureDir + "greenCircle3", false); }
        }

        private Texture2D gct;

        public Texture2D greenCircleTexture
        {
            get { return gct ? gct : gct = GameDatabase.Instance.GetTexture(textureDir + "greenCircle2", false); }
        }

        private Texture2D gpct;

        public Texture2D greenPointCircleTexture
        {
            get
            {
                if (gpct == null)
                {
                    gpct = GameDatabase.Instance.GetTexture(textureDir + "greenPointCircle", false);
                }
                return gpct;
            }
        }

        private Texture2D gspct;

        public Texture2D greenSpikedPointCircleTexture
        {
            get
            {
                return gspct ? gspct : gspct = GameDatabase.Instance.GetTexture(textureDir + "greenSpikedCircle", false);
            }
        }

        private Texture2D wSqr;

        public Texture2D whiteSquareTexture
        {
            get { return wSqr ? wSqr : wSqr = GameDatabase.Instance.GetTexture(textureDir + "whiteSquare", false); }
        }

        private Texture2D oWSqr;

        public Texture2D openWhiteSquareTexture
        {
            get
            {
                return oWSqr ? oWSqr : oWSqr = GameDatabase.Instance.GetTexture(textureDir + "openWhiteSquare", false);
                ;
            }
        }

        private Texture2D tDir;

        public Texture2D targetDirectionTexture
        {
            get
            {
                return tDir
                    ? tDir
                    : tDir = GameDatabase.Instance.GetTexture(textureDir + "targetDirectionIndicator", false);
            }
        }

        private Texture2D hInd;

        public Texture2D horizonIndicatorTexture
        {
            get
            {
                return hInd ? hInd : hInd = GameDatabase.Instance.GetTexture(textureDir + "horizonIndicator", false);
            }
        }

        private Texture2D si;

        public Texture2D settingsIconTexture
        {
            get { return si ? si : si = GameDatabase.Instance.GetTexture(textureDir + "settingsIcon", false); }
        }

        #endregion 

        public static bool GameIsPaused
        {
            get { return PauseMenu.isOpen || Time.timeScale == 0; }
        }
        
        void Start()
        {
            Instance = this;

            //wmgr toolbar

            // window position settings
            WindowRectToolbar = new Rect(Screen.width - toolWindowWidth - 40, 150, toolWindowWidth, toolWindowHeight);
            // Default, if not in file.
            WindowRectGps = new Rect(0, 0, WindowRectToolbar.width - 10, 0);
            SetupSettingsSize();
            BDAWindowSettingsField.Load();
            CheckIfWindowsSettingsAreWithinScreen();

            WindowRectGps.width = WindowRectToolbar.width - 10;

            //settings
            LoadConfig();

            physRangeTimer = Time.time;
            GAME_UI_ENABLED = true;
            fireKeyGui = BDInputSettingsFields.WEAP_FIRE_KEY.inputString;

            //setup gui styles
            centerLabel = new GUIStyle();
            centerLabel.alignment = TextAnchor.UpperCenter;
            centerLabel.normal.textColor = Color.white;

            centerLabelRed = new GUIStyle();
            centerLabelRed.alignment = TextAnchor.UpperCenter;
            centerLabelRed.normal.textColor = Color.red;

            centerLabelOrange = new GUIStyle();
            centerLabelOrange.alignment = TextAnchor.UpperCenter;
            centerLabelOrange.normal.textColor = XKCDColors.BloodOrange;

            centerLabelBlue = new GUIStyle();
            centerLabelBlue.alignment = TextAnchor.UpperCenter;
            centerLabelBlue.normal.textColor = XKCDColors.AquaBlue;

            leftLabel = new GUIStyle();
            leftLabel.alignment = TextAnchor.UpperLeft;
            leftLabel.normal.textColor = Color.white;

            middleLeftLabel = new GUIStyle(leftLabel);
            middleLeftLabel.alignment = TextAnchor.MiddleLeft;

            middleLeftLabelOrange = new GUIStyle(middleLeftLabel);
            middleLeftLabelOrange.normal.textColor = XKCDColors.BloodOrange;

            targetModeStyle = new GUIStyle();
            targetModeStyle.alignment = TextAnchor.MiddleRight;
            targetModeStyle.fontSize = 9;
            targetModeStyle.normal.textColor = Color.white;

            targetModeStyleSelected = new GUIStyle(targetModeStyle);
            targetModeStyleSelected.normal.textColor = XKCDColors.BloodOrange;

            leftLabelRed = new GUIStyle();
            leftLabelRed.alignment = TextAnchor.UpperLeft;
            leftLabelRed.normal.textColor = Color.red;

            rightLabelRed = new GUIStyle();
            rightLabelRed.alignment = TextAnchor.UpperRight;
            rightLabelRed.normal.textColor = Color.red;

            leftLabelGray = new GUIStyle();
            leftLabelGray.alignment = TextAnchor.UpperLeft;
            leftLabelGray.normal.textColor = Color.gray;

            rippleSliderStyle = new GUIStyle(HighLogic.Skin.horizontalSlider);
            rippleThumbStyle = new GUIStyle(HighLogic.Skin.horizontalSliderThumb);
            rippleSliderStyle.fixedHeight = rippleThumbStyle.fixedHeight = 0;

            kspTitleLabel = new GUIStyle();
            kspTitleLabel.normal.textColor = HighLogic.Skin.window.normal.textColor;
            kspTitleLabel.font = HighLogic.Skin.window.font;
            kspTitleLabel.fontSize = HighLogic.Skin.window.fontSize;
            kspTitleLabel.fontStyle = HighLogic.Skin.window.fontStyle;
            kspTitleLabel.alignment = TextAnchor.UpperCenter;
            //

            if (HighLogic.LoadedSceneIsFlight)
            {
                SaveVolumeSettings();

                GameEvents.onHideUI.Add(HideGameUI);
                GameEvents.onShowUI.Add(ShowGameUI);
                GameEvents.onVesselGoOffRails.Add(OnVesselGoOffRails);
                GameEvents.OnGameSettingsApplied.Add(SaveVolumeSettings);
               
                GameEvents.onVesselChange.Add(VesselChange);
            }

            if (BulletInfo.bullets == null)
                BulletInfo.Load();
        }

        private void CheckIfWindowsSettingsAreWithinScreen()
        {
            if (WindowRectToolbar.x > Screen.width - toolWindowWidth - 40 ||
                WindowRectToolbar.y > Screen.height - toolWindowHeight )
            {
                WindowRectToolbar = new Rect(Screen.width - toolWindowWidth - 40, 150, toolWindowWidth, toolWindowHeight);
            }

            if (WindowRectSettings.x > Screen.width - 420 - 40 ||
               WindowRectSettings.y > Screen.height - 480)
            {
                WindowRectSettings = new Rect(settingsLeft, settingsTop, 420, 480);
            }

            if (WindowRectRwr.x > Screen.width - 276 - 40 ||
                WindowRectRwr.y > Screen.height - 296)
            {
                WindowRectRwr = new Rect(40, Screen.height - 296, 276, 296);
            }
        }

        void Update()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (missileWarning && Time.time - missileWarningTime > 1.5f)
                {
                    missileWarning = false;
                }

                if (Input.GetKeyDown(KeyCode.KeypadMultiply))
                {
                    toolbarGuiEnabled = !toolbarGuiEnabled;
                }
            }
            else if (HighLogic.LoadedSceneIsEditor)
            {
                if (Input.GetKeyDown(KeyCode.F2))
                {
                    showWeaponAlignment = !showWeaponAlignment;
                }
            }

            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
            {
                if (Input.GetKeyDown(KeyCode.B))
                {
                    ToggleSettingsGUI();
                }
            }
        }

        void ToggleSettingsGUI()
        {
            if (HighLogic.LoadedScene == GameScenes.LOADING || HighLogic.LoadedScene == GameScenes.LOADINGBUFFER)
            {
                return;
            }

            settingsGuiEnabled = !settingsGuiEnabled;
            if (settingsGuiEnabled)
            {
                LoadConfig();
            }
            else
            {
                SaveConfig();
            }
        }

        void LateUpdate()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                //UpdateCursorState();
            }
        }
        
        public void UpdateCursorState()
        {
            if (ActiveWeaponManager == null)
            {
                drawCursor = false;
                //Screen.showCursor = true;
                Cursor.visible = true;
                return;
            }

            if (!GAME_UI_ENABLED || CameraMouseLook.MouseLocked)
            {
                drawCursor = false;
                Cursor.visible = false;
                return;
            }


            drawCursor = false;
            if (!MapView.MapIsEnabled && !Misc.Misc.CheckMouseIsOnGui() && !PauseMenu.isOpen)
            {
                if (ActiveWeaponManager.selectedWeapon != null && ActiveWeaponManager.weaponIndex > 0 &&
                    !ActiveWeaponManager.guardMode)
                {
                    if (ActiveWeaponManager.selectedWeapon.GetWeaponClass() == WeaponClasses.Gun ||
                        ActiveWeaponManager.selectedWeapon.GetWeaponClass() == WeaponClasses.DefenseLaser)
                    {
                        ModuleWeapon mw =
                            ActiveWeaponManager.selectedWeapon.GetPart().FindModuleImplementing<ModuleWeapon>();
                        if (mw.weaponState == ModuleWeapon.WeaponStates.Enabled && mw.maxPitch > 1 && !mw.slaved &&
                            !mw.aiControlled)
                        {
                            //Screen.showCursor = false;
                            Cursor.visible = false;
                            drawCursor = true;
                            return;
                        }
                    }
                    else if (ActiveWeaponManager.selectedWeapon.GetWeaponClass() == WeaponClasses.Rocket)
                    {
                        RocketLauncher rl =
                            ActiveWeaponManager.selectedWeapon.GetPart().FindModuleImplementing<RocketLauncher>();
                        if (rl.readyToFire && rl.turret)
                        {
                            //Screen.showCursor = false;
                            Cursor.visible = false;
                            drawCursor = true;
                            return;
                        }
                    }
                }
            }

            //Screen.showCursor = true;
            Cursor.visible = true;
        }
        
        void VesselChange(Vessel v)
        {
            if (!v.isActiveVessel) return;
            GetWeaponManager();
            Instance.UpdateCursorState();
        }

        void GetWeaponManager()
        {
            List<MissileFire>.Enumerator mf = FlightGlobals.ActiveVessel.FindPartModulesImplementing<MissileFire>().GetEnumerator();
            while (mf.MoveNext())
            {
                if (mf.Current == null) continue;
                ActiveWeaponManager = mf.Current;
                return;
            }
            mf.Dispose();
            ActiveWeaponManager = null;
            return;
        }

        public static void LoadConfig()
        {
            try
            {
                Debug.Log("[BDArmory]=== Loading settings.cfg ===");

                BDAPersistantSettingsField.Load();
                BDInputSettingsFields.LoadSettings();
            }
            catch (NullReferenceException)
            {
                Debug.Log("[BDArmory]=== Failed to load settings config ===");
            }
        }

        public static void SaveConfig()
        {
            try
            {
                Debug.Log("[BDArmory] == Saving settings.cfg ==	");

                BDAPersistantSettingsField.Save();

                BDInputSettingsFields.SaveSettings();

                if (OnSavedSettings != null)
                {
                    OnSavedSettings();
                }
            }
            catch (NullReferenceException)
            {
                Debug.Log("[BDArmory]: === Failed to save settings.cfg ====");
            }
        }

        #region GUI

        void OnGUI()
        {
            if (GAME_UI_ENABLED)
            {
                if (settingsGuiEnabled)
                {
                    WindowRectSettings = GUI.Window(129419, WindowRectSettings, SettingsGUI, GUIContent.none);
                }


                if (drawCursor)
                {
                    //mouse cursor
                    int origDepth = GUI.depth;
                    GUI.depth = -100;
                    float cursorSize = 40;
                    Vector3 cursorPos = Input.mousePosition;
                    Rect cursorRect = new Rect(cursorPos.x - (cursorSize/2),
                        Screen.height - cursorPos.y - (cursorSize/2), cursorSize, cursorSize);
                    GUI.DrawTexture(cursorRect, cursorTexture);
                    GUI.depth = origDepth;
                }

                if (toolbarGuiEnabled && HighLogic.LoadedSceneIsFlight)
                {
                    WindowRectToolbar = GUI.Window(321, WindowRectToolbar, ToolbarGUI, "BDA Weapon Manager",
                        HighLogic.Skin.window);
                    BDGUIUtils.UseMouseEventInRect(WindowRectToolbar);
                    if (showGPSWindow && ActiveWeaponManager)
                    {
                        //gpsWindowRect = GUI.Window(424333, gpsWindowRect, GPSWindow, "", GUI.skin.box);
                        BDGUIUtils.UseMouseEventInRect(WindowRectGps);
                        List<GPSTargetInfo>.Enumerator coord = 
                            BDATargetManager.GPSTargets[BDATargetManager.BoolToTeam(ActiveWeaponManager.team)].GetEnumerator();
                        while (coord.MoveNext())
                        {
                            BDGUIUtils.DrawTextureOnWorldPos(coord.Current.worldPos, Instance.greenDotTexture, new Vector2(8, 8), 0);
                        }
                        coord.Dispose();
                    }
                }
            }


            if (DRAW_DEBUG_LABELS && HighLogic.LoadedSceneIsFlight)
            {
                if (RadarUtils.radarRT)
                {
                    GUI.DrawTexture(new Rect(20, 20, 128, 128), RadarUtils.radarRT, ScaleMode.StretchToFill, true);
                }
            }
        }


        public bool hasVS = false;
        public bool showVSGUI;


        float rippleHeight;
        float weaponsHeight;
        float guardHeight;
        float modulesHeight;
        float gpsHeight;
        bool toolMinimized;

        void ToolbarGUI(int windowID)
        {
            GUI.DragWindow(new Rect(30, 0, toolWindowWidth - 90, 30));

            float line = 0;
            float leftIndent = 10;
            float contentWidth = (toolWindowWidth) - (2*leftIndent);
            float contentTop = 10;
            float entryHeight = 20;

            line += 1.25f;
            line += 0.25f;

            //SETTINGS BUTTON
            if (!BDKeyBinder.current &&
                GUI.Button(new Rect(toolWindowWidth - 30, 4, 26, 26), settingsIconTexture, HighLogic.Skin.button))
            {
                ToggleSettingsGUI();
            }

            //vesselswitcher button
            if (hasVS)
            {
                GUIStyle vsStyle = showVSGUI ? HighLogic.Skin.box : HighLogic.Skin.button;
                if (GUI.Button(new Rect(toolWindowWidth - 30 - 28, 4, 26, 26), "VS", vsStyle))
                {
                    showVSGUI = !showVSGUI;
                }
            }


            if (ActiveWeaponManager != null)
            {
                //MINIMIZE BUTTON
                toolMinimized = GUI.Toggle(new Rect(4, 4, 26, 26), toolMinimized, "_",
                    toolMinimized ? HighLogic.Skin.box : HighLogic.Skin.button);


                GUIStyle armedLabelStyle;
                Rect armedRect = new Rect(leftIndent, contentTop + (line*entryHeight), contentWidth/2, entryHeight);
                if (ActiveWeaponManager.guardMode)
                {
                    if (GUI.Button(armedRect, "- Guard Mode -", HighLogic.Skin.box))
                    {
                        showGuardMenu = true;
                    }
                }
                else
                {
                    string armedText = "Trigger is ";
                    if (ActiveWeaponManager.isArmed)
                    {
                        armedText += "ARMED.";
                        armedLabelStyle = HighLogic.Skin.box;
                    }
                    else
                    {
                        armedText += "disarmed.";
                        armedLabelStyle = HighLogic.Skin.button;
                    }
                    if (GUI.Button(armedRect, armedText, armedLabelStyle))
                    {
                        ActiveWeaponManager.ToggleArm();
                    }
                }

                GUIStyle teamButtonStyle;
                string teamText = "Team: ";
                if (ActiveWeaponManager.team)
                {
                    teamButtonStyle = HighLogic.Skin.box;
                    teamText += "B";
                }
                else
                {
                    teamButtonStyle = HighLogic.Skin.button;
                    teamText += "A";
                }

                if (
                    GUI.Button(
                        new Rect(leftIndent + (contentWidth/2), contentTop + (line*entryHeight), contentWidth/2,
                            entryHeight), teamText, teamButtonStyle))
                {
                    ActiveWeaponManager.ToggleTeam();
                }
                line++;
                line += 0.25f;
                string weaponName = ActiveWeaponManager.selectedWeaponString;
                // = ActiveWeaponManager.selectedWeapon == null ? "None" : ActiveWeaponManager.selectedWeapon.GetShortName();
                string selectionText = "Weapon: " + weaponName;
                GUI.Label(new Rect(leftIndent, contentTop + (line*entryHeight), contentWidth, entryHeight*1.25f),
                    selectionText, HighLogic.Skin.box);
                line += 1.25f;
                line += 0.1f;
                //if weapon can ripple, show option and slider.
                if (ActiveWeaponManager.hasLoadedRippleData && ActiveWeaponManager.canRipple)
                {
                    if (ActiveWeaponManager.selectedWeapon.GetWeaponClass() == WeaponClasses.Gun)
                    {
                        string rippleText = ActiveWeaponManager.rippleFire
                            ? "Barrage: " + ActiveWeaponManager.gunRippleRpm.ToString("0") + " RPM"
                            : "Salvo";
                        GUIStyle rippleStyle = ActiveWeaponManager.rippleFire
                            ? HighLogic.Skin.box
                            : HighLogic.Skin.button;
                        if (
                            GUI.Button(
                                new Rect(leftIndent, contentTop + (line*entryHeight), contentWidth/2, entryHeight*1.25f),
                                rippleText, rippleStyle))
                        {
                            ActiveWeaponManager.ToggleRippleFire();
                        }

                        rippleHeight = Mathf.Lerp(rippleHeight, 1.25f, 0.15f);
                    }
                    else
                    {
                        string rippleText = ActiveWeaponManager.rippleFire
                            ? "Ripple: " + ActiveWeaponManager.rippleRPM.ToString("0") + " RPM"
                            : "Ripple: OFF";
                        GUIStyle rippleStyle = ActiveWeaponManager.rippleFire
                            ? HighLogic.Skin.box
                            : HighLogic.Skin.button;
                        if (
                            GUI.Button(
                                new Rect(leftIndent, contentTop + (line*entryHeight), contentWidth/2, entryHeight*1.25f),
                                rippleText, rippleStyle))
                        {
                            ActiveWeaponManager.ToggleRippleFire();
                        }
                        if (ActiveWeaponManager.rippleFire)
                        {
                            Rect sliderRect = new Rect(leftIndent + (contentWidth/2) + 2,
                                contentTop + (line*entryHeight) + 6.5f, (contentWidth/2) - 2, 12);
                            ActiveWeaponManager.rippleRPM = GUI.HorizontalSlider(sliderRect,
                                ActiveWeaponManager.rippleRPM, 100, 1600, rippleSliderStyle, rippleThumbStyle);
                        }
                        rippleHeight = Mathf.Lerp(rippleHeight, 1.25f, 0.15f);
                    }
                }
                else
                {
                    rippleHeight = Mathf.Lerp(rippleHeight, 0, 0.15f);
                }
                //line += 1.25f;
                line += rippleHeight;
                line += 0.1f;

                if (!toolMinimized)
                {
                    showWeaponList =
                        GUI.Toggle(new Rect(leftIndent, contentTop + (line*entryHeight), contentWidth/3, entryHeight),
                            showWeaponList, "Weapons", showWeaponList ? HighLogic.Skin.box : HighLogic.Skin.button);
                    showGuardMenu =
                        GUI.Toggle(
                            new Rect(leftIndent + (contentWidth/3), contentTop + (line*entryHeight), contentWidth/3,
                                entryHeight), showGuardMenu, "Guard Menu",
                            showGuardMenu ? HighLogic.Skin.box : HighLogic.Skin.button);
                    showModules =
                        GUI.Toggle(
                            new Rect(leftIndent + (2*contentWidth/3), contentTop + (line*entryHeight), contentWidth/3,
                                entryHeight), showModules, "Modules",
                            showModules ? HighLogic.Skin.box : HighLogic.Skin.button);
                    line++;
                }

                float weaponLines = 0;
                if (showWeaponList && !toolMinimized)
                {
                    line += 0.25f;
                    Rect weaponListGroupRect = new Rect(5, contentTop + (line*entryHeight), toolWindowWidth - 10,
                        ((float) ActiveWeaponManager.weaponArray.Length + 0.1f)*entryHeight);
                    GUI.BeginGroup(weaponListGroupRect, GUIContent.none, HighLogic.Skin.box); //darker box
                    weaponLines += 0.1f;
                    for (int i = 0; i < ActiveWeaponManager.weaponArray.Length; i++)
                    {
                        GUIStyle wpnListStyle;
                        GUIStyle tgtStyle;
                        if (i == ActiveWeaponManager.weaponIndex)
                        {
                            wpnListStyle = middleLeftLabelOrange;
                            tgtStyle = targetModeStyleSelected;
                        }
                        else
                        {
                            wpnListStyle = middleLeftLabel;
                            tgtStyle = targetModeStyle;
                        }
                        string label;
                        string subLabel;
                        if (ActiveWeaponManager.weaponArray[i] != null)
                        {
                            label = ActiveWeaponManager.weaponArray[i].GetShortName();
                            subLabel = ActiveWeaponManager.weaponArray[i].GetSubLabel();
                        }
                        else
                        {
                            label = "None";
                            subLabel = String.Empty;
                        }
                        Rect weaponButtonRect = new Rect(leftIndent, (weaponLines*entryHeight),
                            weaponListGroupRect.width - (2*leftIndent), entryHeight);

                        GUI.Label(weaponButtonRect, subLabel, tgtStyle);

                        if (GUI.Button(weaponButtonRect, label, wpnListStyle))
                        {
                            ActiveWeaponManager.CycleWeapon(i);
                        }


                        if (i < ActiveWeaponManager.weaponArray.Length - 1)
                        {
                            BDGUIUtils.DrawRectangle(
                                new Rect(weaponButtonRect.x, weaponButtonRect.y + weaponButtonRect.height,
                                    weaponButtonRect.width, 1), Color.white);
                        }
                        weaponLines++;
                    }
                    weaponLines += 0.1f;
                    GUI.EndGroup();
                }
                weaponsHeight = Mathf.Lerp(weaponsHeight, weaponLines, 0.15f);
                line += weaponsHeight;

                float guardLines = 0;
                if (showGuardMenu && !toolMinimized)
                {
                    line += 0.25f;
                    GUI.BeginGroup(
                        new Rect(5, contentTop + (line*entryHeight), toolWindowWidth - 10, 7.45f*entryHeight),
                        GUIContent.none, HighLogic.Skin.box);
                    guardLines += 0.1f;
                    contentWidth -= 16;
                    leftIndent += 3;
                    string guardButtonLabel = "Guard Mode " + (ActiveWeaponManager.guardMode ? "ON" : "Off");
                    if (GUI.Button(new Rect(leftIndent, (guardLines*entryHeight), contentWidth, entryHeight),
                        guardButtonLabel, ActiveWeaponManager.guardMode ? HighLogic.Skin.box : HighLogic.Skin.button))
                    {
                        ActiveWeaponManager.ToggleGuardMode();
                    }
                    guardLines += 1.25f;

                    string scanLabel = ALLOW_LEGACY_TARGETING ? "Scan Interval" : "Firing Interval";
                    GUI.Label(new Rect(leftIndent, (guardLines*entryHeight), 85, entryHeight), scanLabel, leftLabel);
                    ActiveWeaponManager.targetScanInterval =
                        GUI.HorizontalSlider(
                            new Rect(leftIndent + (90), (guardLines*entryHeight), contentWidth - 90 - 38, entryHeight),
                            ActiveWeaponManager.targetScanInterval, 1, 60);
                    ActiveWeaponManager.targetScanInterval = Mathf.Round(ActiveWeaponManager.targetScanInterval);
                    GUI.Label(new Rect(leftIndent + (contentWidth - 35), (guardLines*entryHeight), 35, entryHeight),
                        ActiveWeaponManager.targetScanInterval.ToString(), leftLabel);
                    guardLines++;

                    // extension for feature_engagementenvelope: set the firing burst length
                    string burstLabel = "Burst Length";
                    GUI.Label(new Rect(leftIndent, (guardLines * entryHeight), 85, entryHeight), burstLabel, leftLabel);
                    ActiveWeaponManager.fireBurstLength =
                        GUI.HorizontalSlider(
                            new Rect(leftIndent + (90), (guardLines * entryHeight), contentWidth - 90 - 38, entryHeight),
                            ActiveWeaponManager.fireBurstLength, 0, 60);
                    ActiveWeaponManager.fireBurstLength = Mathf.Round(ActiveWeaponManager.fireBurstLength * 2) / 2;
                    GUI.Label(new Rect(leftIndent + (contentWidth - 35), (guardLines * entryHeight), 35, entryHeight),
                        ActiveWeaponManager.fireBurstLength.ToString(), leftLabel);
                    guardLines++;

                    GUI.Label(new Rect(leftIndent, (guardLines*entryHeight), 85, entryHeight), "Field of View",
                        leftLabel);
                    float guardAngle = ActiveWeaponManager.guardAngle;
                    guardAngle =
                        GUI.HorizontalSlider(
                            new Rect(leftIndent + 90, (guardLines*entryHeight), contentWidth - 90 - 38, entryHeight),
                            guardAngle, 10, 360);
                    guardAngle = guardAngle/10;
                    guardAngle = Mathf.Round(guardAngle);
                    ActiveWeaponManager.guardAngle = guardAngle*10;
                    GUI.Label(new Rect(leftIndent + (contentWidth - 35), (guardLines*entryHeight), 35, entryHeight),
                        ActiveWeaponManager.guardAngle.ToString(), leftLabel);
                    guardLines++;

                    string rangeLabel = ALLOW_LEGACY_TARGETING ? "Guard Range" : "Visual Range";
                    GUI.Label(new Rect(leftIndent, (guardLines*entryHeight), 85, entryHeight), rangeLabel, leftLabel);
                    float guardRange = ActiveWeaponManager.guardRange;
                    float maxVisRange = ALLOW_LEGACY_TARGETING
                        ? Mathf.Clamp(PHYSICS_RANGE, 2500, 100000)
                        : MAX_GUARD_VISUAL_RANGE;
                    guardRange =
                        GUI.HorizontalSlider(
                            new Rect(leftIndent + 90, (guardLines*entryHeight), contentWidth - 90 - 38, entryHeight),
                            guardRange, 100, maxVisRange);
                    guardRange = guardRange/100;
                    guardRange = Mathf.Round(guardRange);
                    ActiveWeaponManager.guardRange = guardRange*100;
                    GUI.Label(new Rect(leftIndent + (contentWidth - 35), (guardLines*entryHeight), 35, entryHeight),
                        ActiveWeaponManager.guardRange.ToString(), leftLabel);
                    guardLines++;

                    GUI.Label(new Rect(leftIndent, (guardLines*entryHeight), 85, entryHeight), "Guns Range", leftLabel);
                    float gRange = ActiveWeaponManager.gunRange;
                    gRange =
                        GUI.HorizontalSlider(
                            new Rect(leftIndent + 90, (guardLines*entryHeight), contentWidth - 90 - 38, entryHeight),
                            gRange, 0, BDArmorySettings.MAX_BULLET_RANGE);
                    gRange /= 100f;
                    gRange = Mathf.Round(gRange);
                    gRange *= 100f;
                    ActiveWeaponManager.gunRange = gRange;
                    GUI.Label(new Rect(leftIndent + (contentWidth - 35), (guardLines*entryHeight), 35, entryHeight),
                        ActiveWeaponManager.gunRange.ToString(), leftLabel);
                    guardLines++;

                    GUI.Label(new Rect(leftIndent, (guardLines*entryHeight), 85, entryHeight), "Missiles/Tgt", leftLabel);
                    float mslCount = ActiveWeaponManager.maxMissilesOnTarget;
                    mslCount =
                        GUI.HorizontalSlider(
                            new Rect(leftIndent + 90, (guardLines*entryHeight), contentWidth - 90 - 38, entryHeight),
                            mslCount, 1, 6);
                    mslCount = Mathf.Round(mslCount);
                    ActiveWeaponManager.maxMissilesOnTarget = mslCount;
                    GUI.Label(new Rect(leftIndent + (contentWidth - 35), (guardLines*entryHeight), 35, entryHeight),
                        ActiveWeaponManager.maxMissilesOnTarget.ToString(), leftLabel);
                    guardLines++;

                    string targetType = "Target Type: ";
                    if (ActiveWeaponManager.targetMissiles)
                    {
                        targetType += "Missiles";
                    }
                    else
                    {
                        targetType += "All Targets";
                    }

                    if (GUI.Button(new Rect(leftIndent, (guardLines*entryHeight), contentWidth, entryHeight), targetType,
                        HighLogic.Skin.button))
                    {
                        ActiveWeaponManager.ToggleTargetType();
                    }
                    guardLines++;
                    GUI.EndGroup();
                    line += 0.1f;
                }
                guardHeight = Mathf.Lerp(guardHeight, guardLines, 0.15f);
                line += guardHeight;

                float moduleLines = 0;
                if (showModules && !toolMinimized)
                {
                    line += 0.25f;
                    GUI.BeginGroup(
                        new Rect(5, contentTop + (line*entryHeight), toolWindowWidth - 10, numberOfModules*entryHeight),
                        GUIContent.none, HighLogic.Skin.box);

                    numberOfModules = 0;
                    moduleLines += 0.1f;
                    //RWR
                    if (ActiveWeaponManager.rwr)
                    {
                        numberOfModules++;
                        bool isEnabled = ActiveWeaponManager.rwr.rwrEnabled;
                        string label = "Radar Warning Receiver";
                        Rect rwrRect = new Rect(leftIndent, +(moduleLines*entryHeight), contentWidth, entryHeight);
                        if (GUI.Button(rwrRect, label, isEnabled ? centerLabelOrange : centerLabel))
                        {
                            if (isEnabled)
                            {
                                ActiveWeaponManager.rwr.DisableRWR();
                            }
                            else
                            {
                                ActiveWeaponManager.rwr.EnableRWR();
                            }
                        }
                        moduleLines++;
                    }

                    //TGP
                    List<ModuleTargetingCamera>.Enumerator mtc = ActiveWeaponManager.targetingPods.GetEnumerator();
                    while (mtc.MoveNext())
                    {
                        if (mtc.Current == null) continue;
                        numberOfModules++;
                        bool isEnabled = (mtc.Current.cameraEnabled);
                        bool isActive = (mtc.Current == ModuleTargetingCamera.activeCam);
                        GUIStyle moduleStyle = isEnabled ? centerLabelOrange : centerLabel; // = mtc 
                        string label = mtc.Current.part.partInfo.title;
                        if (isActive)
                        {
                            moduleStyle = centerLabelRed;
                            label = "[" + label + "]";
                        }
                        if (GUI.Button(new Rect(leftIndent, +(moduleLines*entryHeight), contentWidth, entryHeight),
                            label, moduleStyle))
                        {
                            if (isActive)
                            {
                                mtc.Current.ToggleCamera();
                            }
                            else
                            {
                                mtc.Current.EnableCamera();
                            }
                        }
                        moduleLines++;
                    }
                    mtc.Dispose();

                    //RADAR
                    List<ModuleRadar>.Enumerator mr = ActiveWeaponManager.radars.GetEnumerator();
                    while (mr.MoveNext())
                    {
                        if (mr.Current == null) continue;
                        numberOfModules++;
                        GUIStyle moduleStyle = mr.Current.radarEnabled ? centerLabelBlue : centerLabel;
                        string label = mr.Current.radarName;
                        if (GUI.Button(new Rect(leftIndent, +(moduleLines*entryHeight), contentWidth, entryHeight),
                            label, moduleStyle))
                        {
                            mr.Current.Toggle();
                        }
                        moduleLines++;
                    }
                    mr.Dispose();

                    //JAMMERS
                    List<ModuleECMJammer>.Enumerator jammer = ActiveWeaponManager.jammers.GetEnumerator();
                    while (jammer.MoveNext())
                    {
                        if (jammer.Current == null) continue;
                        if (jammer.Current.alwaysOn) continue;

                        numberOfModules++;
                        GUIStyle moduleStyle = jammer.Current.jammerEnabled ? centerLabelBlue : centerLabel;
                        string label = jammer.Current.part.partInfo.title;
                        if (GUI.Button(new Rect(leftIndent, +(moduleLines*entryHeight), contentWidth, entryHeight),
                            label, moduleStyle))
                        {
                            jammer.Current.Toggle();
                        }
                        moduleLines++;
                    }
                    jammer.Dispose();

                    //GPS coordinator
                    GUIStyle gpsModuleStyle = showGPSWindow ? centerLabelBlue : centerLabel;
                    numberOfModules++;
                    if (GUI.Button(new Rect(leftIndent, +(moduleLines*entryHeight), contentWidth, entryHeight),
                        "GPS Coordinator", gpsModuleStyle))
                    {
                        showGPSWindow = !showGPSWindow;
                    }
                    moduleLines++;

                    //wingCommander
                    if (ActiveWeaponManager.wingCommander)
                    {
                        GUIStyle wingComStyle = ActiveWeaponManager.wingCommander.showGUI
                            ? centerLabelBlue
                            : centerLabel;
                        numberOfModules++;
                        if (GUI.Button(new Rect(leftIndent, +(moduleLines*entryHeight), contentWidth, entryHeight),
                            "Wing Command", wingComStyle))
                        {
                            ActiveWeaponManager.wingCommander.ToggleGUI();
                        }
                        moduleLines++;
                    }

                    GUI.EndGroup();

                    line += 0.1f;
                }
                modulesHeight = Mathf.Lerp(modulesHeight, moduleLines, 0.15f);
                line += modulesHeight;

                float gpsLines = 0;
                if (showGPSWindow && !toolMinimized)
                {
                    line += 0.25f;
                    GUI.BeginGroup(new Rect(5, contentTop + (line*entryHeight), toolWindowWidth, WindowRectGps.height));
                    GPSWindow();
                    GUI.EndGroup();
                    gpsLines = WindowRectGps.height/entryHeight;
                }
                gpsHeight = Mathf.Lerp(gpsHeight, gpsLines, 0.15f);
                line += gpsHeight;
            }
            else
            {
                GUI.Label(new Rect(leftIndent, contentTop + (line*entryHeight), contentWidth, entryHeight),
                    "No Weapon Manager found.", HighLogic.Skin.box);
                line++;
            }


            toolWindowHeight = Mathf.Lerp(toolWindowHeight, contentTop + (line*entryHeight) + 5, 1);
            WindowRectToolbar.height = toolWindowHeight;
            // = new Rect(toolbarWindowRect.position.x, toolbarWindowRect.position.y, toolWindowWidth, toolWindowHeight);
        }

        bool validGPSName = true;

        //GPS window
        void GPSWindow()
        {
            GUI.Box(WindowRectGps, GUIContent.none, HighLogic.Skin.box);
            gpsEntryCount = 0;
            Rect listRect = new Rect(gpsBorder, gpsBorder, WindowRectGps.width - (2*gpsBorder),
                WindowRectGps.height - (2*gpsBorder));
            GUI.BeginGroup(listRect);
            string targetLabel = "GPS Target: " + ActiveWeaponManager.designatedGPSInfo.name;
            GUI.Label(new Rect(0, 0, listRect.width, gpsEntryHeight), targetLabel, kspTitleLabel);
            gpsEntryCount += 0.85f;
            if (ActiveWeaponManager.designatedGPSCoords != Vector3d.zero)
            {
                GUI.Label(new Rect(0, gpsEntryCount*gpsEntryHeight, listRect.width - gpsEntryHeight, gpsEntryHeight),
                    Misc.Misc.FormattedGeoPos(ActiveWeaponManager.designatedGPSCoords, true), HighLogic.Skin.box);
                if (
                    GUI.Button(
                        new Rect(listRect.width - gpsEntryHeight, gpsEntryCount*gpsEntryHeight, gpsEntryHeight,
                            gpsEntryHeight), "X", HighLogic.Skin.button))
                {
                    ActiveWeaponManager.designatedGPSInfo = new GPSTargetInfo();
                }
            }
            else
            {
                GUI.Label(new Rect(0, gpsEntryCount*gpsEntryHeight, listRect.width - gpsEntryHeight, gpsEntryHeight),
                    "No Target", HighLogic.Skin.box);
            }
            gpsEntryCount += 1.35f;
            int indexToRemove = -1;
            int index = 0;
            BDATeams myTeam = BDATargetManager.BoolToTeam(ActiveWeaponManager.team);
            List<GPSTargetInfo>.Enumerator coordinate = BDATargetManager.GPSTargets[myTeam].GetEnumerator();
            while (coordinate.MoveNext())
            {
                Color origWColor = GUI.color;
                if (coordinate.Current.EqualsTarget(ActiveWeaponManager.designatedGPSInfo))
                {
                    GUI.color = XKCDColors.LightOrange;
                }
                string label = Misc.Misc.FormattedGeoPosShort(coordinate.Current.gpsCoordinates, false);
                float nameWidth = 100;
                if (editingGPSName && index == editingGPSNameIndex)
                {
                    if (validGPSName && Event.current.type == EventType.KeyDown &&
                        Event.current.keyCode == KeyCode.Return)
                    {
                        editingGPSName = false;
                        hasEnteredGPSName = true;
                    }
                    else
                    {
                        Color origColor = GUI.color;
                        if (newGPSName.Contains(";") || newGPSName.Contains(":") || newGPSName.Contains(","))
                        {
                            validGPSName = false;
                            GUI.color = Color.red;
                        }
                        else
                        {
                            validGPSName = true;
                        }
                        newGPSName = GUI.TextField(
                            new Rect(0, gpsEntryCount*gpsEntryHeight, nameWidth, gpsEntryHeight), newGPSName, 12);
                        GUI.color = origColor;
                    }
                }
                else
                {
                    if (GUI.Button(new Rect(0, gpsEntryCount*gpsEntryHeight, nameWidth, gpsEntryHeight), coordinate.Current.name,
                        HighLogic.Skin.button))
                    {
                        editingGPSName = true;
                        editingGPSNameIndex = index;
                        newGPSName = coordinate.Current.name;
                    }
                }
                if (
                    GUI.Button(
                        new Rect(nameWidth, gpsEntryCount*gpsEntryHeight, listRect.width - gpsEntryHeight - nameWidth,
                            gpsEntryHeight), label, HighLogic.Skin.button))
                {
                    ActiveWeaponManager.designatedGPSInfo = coordinate.Current;
                    editingGPSName = false;
                }
                if (
                    GUI.Button(
                        new Rect(listRect.width - gpsEntryHeight, gpsEntryCount*gpsEntryHeight, gpsEntryHeight,
                            gpsEntryHeight), "X", HighLogic.Skin.button))
                {
                    indexToRemove = index;
                }
                gpsEntryCount++;
                index++;
                GUI.color = origWColor;
            }
            coordinate.Dispose();

            if (hasEnteredGPSName && editingGPSNameIndex < BDATargetManager.GPSTargets[myTeam].Count)
            {
                hasEnteredGPSName = false;
                GPSTargetInfo old = BDATargetManager.GPSTargets[myTeam][editingGPSNameIndex];
                if (ActiveWeaponManager.designatedGPSInfo.EqualsTarget(old))
                {
                    ActiveWeaponManager.designatedGPSInfo.name = newGPSName;
                }
                BDATargetManager.GPSTargets[myTeam][editingGPSNameIndex] =
                    new GPSTargetInfo(BDATargetManager.GPSTargets[myTeam][editingGPSNameIndex].gpsCoordinates,
                        newGPSName);
                editingGPSNameIndex = 0;
            }

            GUI.EndGroup();

            if (indexToRemove >= 0)
            {
                BDATargetManager.GPSTargets[myTeam].RemoveAt(indexToRemove);
            }

            //gpsWindowRect.x = toolbarWindowRect.x;
            //gpsWindowRect.y = toolbarWindowRect.y + toolbarWindowRect.height;
            WindowRectGps.height = (2*gpsBorder) + (gpsEntryCount*gpsEntryHeight);
        }


        Rect SLineRect(float line)
        {
            return new Rect(settingsMargin, line*settingsLineHeight, settingsWidth - (2*settingsMargin),
                settingsLineHeight);
        }

        Rect SRightRect(float line)
        {
            return new Rect(settingsMargin + ((settingsWidth - 2*settingsLineHeight)/2), line*settingsLineHeight,
                (settingsWidth - (2*settingsMargin))/2, settingsLineHeight);
        }

        Rect SLeftRect(float line)
        {
            return new Rect(settingsMargin, (line*settingsLineHeight), (settingsWidth - (2*settingsMargin))/2,
                settingsLineHeight);
        }

        float settingsWidth;
        float settingsHeight;
        float settingsLeft;
        float settingsTop;
        float settingsLineHeight;
        float settingsMargin;

        bool editKeys;

        void SetupSettingsSize()
        {
            settingsWidth = 420;
            settingsHeight = 480;
            settingsLeft = Screen.width/2 - settingsWidth/2;
            settingsTop = 100;
            settingsLineHeight = 22;
            settingsMargin = 18;
            WindowRectSettings = new Rect(settingsLeft, settingsTop, 420, 480);
        }

        void SettingsGUI(int windowID)
        {
            float line = 1.25f;
            GUI.Box(new Rect(0, 0, settingsWidth, settingsHeight), "BDArmory Settings");
            GUI.DragWindow(new Rect(0, 0, settingsWidth, 25));
            if (editKeys)
            {
                InputSettings();
                return;
            }
            INSTAKILL = GUI.Toggle(SLeftRect(line), INSTAKILL, "Instakill");
            INFINITE_AMMO = GUI.Toggle(SRightRect(line), INFINITE_AMMO, "Infinte Ammo");
            line++;
            BULLET_HITS = GUI.Toggle(SLeftRect(line), BULLET_HITS, "Bullet Hits");
            EJECT_SHELLS = GUI.Toggle(SRightRect(line), EJECT_SHELLS, "Eject Shells");
            line++;
            AIM_ASSIST = GUI.Toggle(SLeftRect(line), AIM_ASSIST, "Aim Assist");
            DRAW_AIMERS = GUI.Toggle(SRightRect(line), DRAW_AIMERS, "Draw Aimers");
            line++;
            DRAW_DEBUG_LINES = GUI.Toggle(SLeftRect(line), DRAW_DEBUG_LINES, "Debug Lines");
            DRAW_DEBUG_LABELS = GUI.Toggle(SRightRect(line), DRAW_DEBUG_LABELS, "Debug Labels");
            line++;
            REMOTE_SHOOTING = GUI.Toggle(SLeftRect(line), REMOTE_SHOOTING, "Remote Firing");
            BOMB_CLEARANCE_CHECK = GUI.Toggle(SRightRect(line), BOMB_CLEARANCE_CHECK, "Clearance Check");
            line++;
            ALLOW_LEGACY_TARGETING = GUI.Toggle(SLeftRect(line), ALLOW_LEGACY_TARGETING, "Legacy Targeting");
            SHELL_COLLISIONS = GUI.Toggle(SRightRect(line), SHELL_COLLISIONS, "Shell Collisions");
            line++;
            line++;

            bool origPm = PEACE_MODE;
            PEACE_MODE = GUI.Toggle(SLeftRect(line), PEACE_MODE, "Peace Mode");
            if (PEACE_MODE && !origPm)
            {
                BDATargetManager.ClearDatabase();
                if (OnPeaceEnabled != null)
                {
                    OnPeaceEnabled();
                }
            }
            line++;
            line++;


            GUI.Label(SLeftRect(line), "Trigger Hold: " + TRIGGER_HOLD_TIME.ToString("0.00") + "s", leftLabel);
            TRIGGER_HOLD_TIME = GUI.HorizontalSlider(SRightRect(line), TRIGGER_HOLD_TIME, 0.02f, 1f);
            line++;


            GUI.Label(SLeftRect(line), "UI Volume: " + (BDARMORY_UI_VOLUME*100).ToString("0"), leftLabel);
            float uiVol = BDARMORY_UI_VOLUME;
            uiVol = GUI.HorizontalSlider(SRightRect(line), uiVol, 0f, 1f);
            if (uiVol != BDARMORY_UI_VOLUME && OnVolumeChange != null)
            {
                OnVolumeChange();
            }
            BDARMORY_UI_VOLUME = uiVol;
            line++;

            GUI.Label(SLeftRect(line), "Weapon Volume: " + (BDARMORY_WEAPONS_VOLUME*100).ToString("0"), leftLabel);
            float weaponVol = BDARMORY_WEAPONS_VOLUME;
            weaponVol = GUI.HorizontalSlider(SRightRect(line), weaponVol, 0f, 1f);
            if (uiVol != BDARMORY_WEAPONS_VOLUME && OnVolumeChange != null)
            {
                OnVolumeChange();
            }
            BDARMORY_WEAPONS_VOLUME = weaponVol;
            line++;
            line++;
 
            //competition mode
            if (HighLogic.LoadedSceneIsFlight)
            {
                GUI.Label(SLineRect(line), "= Dogfight Competition =", centerLabel);
                line++;
                if (!BDACompetitionMode.Instance.competitionStarting)
                {
                    compDistGui = GUI.TextField(SRightRect(line), compDistGui);
                    GUI.Label(SLeftRect(line), "Competition Distance");
                    float cDist;
                    if (Single.TryParse(compDistGui, out cDist))
                    {
                        competitionDist = cDist;
                    }
                    line++;

                    if (GUI.Button(SRightRect(line), "Start Competition"))
                    {
                        competitionDist = Mathf.Clamp(competitionDist, 2000f, 20000f);
                        compDistGui = competitionDist.ToString();
                        BDACompetitionMode.Instance.StartCompetitionMode(competitionDist);
                        SaveConfig();
                        settingsGuiEnabled = false;
                    }
                }
                else
                {
                    GUI.Label(SLeftRect(line), "Starting Competition... (" + compDistGui + ")");
                    line++;
                    if (GUI.Button(SLeftRect(line), "Cancel"))
                    {
                        BDACompetitionMode.Instance.StopCompetition();
                    }
                }
            }

            line++;
            line++;
            if (GUI.Button(SLineRect(line), "Edit Inputs"))
            {
                editKeys = true;
            }
            line++;
            line++;
            if (!BDKeyBinder.current && GUI.Button(SLineRect(line), "Save and Close"))
            {
                SaveConfig();
                settingsGuiEnabled = false;
            }

            line += 1.5f;
            settingsHeight = (line*settingsLineHeight);
            WindowRectSettings.height = settingsHeight;
            BDGUIUtils.UseMouseEventInRect(WindowRectSettings);
        }

        void InputSettings()
        {
            float line = 1.25f;
            int inputID = 0;


            GUI.Label(SLineRect(line), "- Weapons -", centerLabel);
            line++;
            InputSettingsList("WEAP_", ref inputID, ref line);
            line++;

            GUI.Label(SLineRect(line), "- Targeting Pod -", centerLabel);
            line++;
            InputSettingsList("TGP_", ref inputID, ref line);
            line++;

            GUI.Label(SLineRect(line), "- Radar -", centerLabel);
            line++;
            InputSettingsList("RADAR_", ref inputID, ref line);

            line += 2;
            if (!BDKeyBinder.current && GUI.Button(SLineRect(line), "Back"))
            {
                editKeys = false;
            }

            line += 1.5f;
            settingsHeight = (line*settingsLineHeight);
            WindowRectSettings.height = settingsHeight;
            BDGUIUtils.UseMouseEventInRect(WindowRectSettings);
        }

        void InputSettingsList(string prefix, ref int id, ref float line)
        {
            if (inputFields != null)
            {
                for (int i = 0; i < inputFields.Length; i++)
                {
                    string fieldName = inputFields[i].Name;
                    if (fieldName.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        InputSettingsLine(fieldName, id++, ref line);
                    }
                }
            }
        }

        void InputSettingsLine(string fieldName, int id, ref float line)
        {
            GUI.Box(SLineRect(line), GUIContent.none);
            string label = String.Empty;
            if (BDKeyBinder.IsRecordingID(id))
            {
                string recordedInput;
                if (BDKeyBinder.current.AcquireInputString(out recordedInput))
                {
                    BDInputInfo orig = (BDInputInfo) typeof(BDInputSettingsFields).GetField(fieldName).GetValue(null);
                    BDInputInfo recorded = new BDInputInfo(recordedInput, orig.description);
                    typeof(BDInputSettingsFields).GetField(fieldName).SetValue(null, recorded);
                }

                label = "      Press a key or button.";
            }
            else
            {
                BDInputInfo inputInfo = new BDInputInfo();
                try
                {
                    inputInfo = (BDInputInfo) typeof(BDInputSettingsFields).GetField(fieldName).GetValue(null);
                }
                catch (NullReferenceException)
                {
                    Debug.Log("[BDArmory]: Reflection failed to find input info of field: " + fieldName);
                    editKeys = false;
                    return;
                }
                label = " " + inputInfo.description + " : " + inputInfo.inputString;

                if (GUI.Button(SSetKeyRect(line), "Set Key"))
                {
                    BDKeyBinder.BindKey(id);
                }
                if (GUI.Button(SClearKeyRect(line), "Clear"))
                {
                    typeof(BDInputSettingsFields).GetField(fieldName)
                        .SetValue(null, new BDInputInfo(inputInfo.description));
                }
            }
            GUI.Label(SLeftRect(line), label);
            line++;
        }

        Rect SSetKeyRect(float line)
        {
            return new Rect(settingsMargin + (2*(settingsWidth - 2*settingsMargin)/3), line*settingsLineHeight,
                (settingsWidth - (2*settingsMargin))/6, settingsLineHeight);
        }

        Rect SClearKeyRect(float line)
        {
            return
                new Rect(
                    settingsMargin + (2*(settingsWidth - 2*settingsMargin)/3) + (settingsWidth - 2*settingsMargin)/6,
                    line*settingsLineHeight, (settingsWidth - (2*settingsMargin))/6, settingsLineHeight);
        }

        #endregion

        void HideGameUI()
        {
            GAME_UI_ENABLED = false;
        }

        void ShowGameUI()
        {
            GAME_UI_ENABLED = true;
        }
        
        internal void OnDestroy()
        {
            if (HighLogic.LoadedSceneIsFlight) BDAWindowSettingsField.Save();
        }
        
        void OnVesselGoOffRails(Vessel v)
        {
            if (v.Landed && DRAW_DEBUG_LABELS)
            {
                Debug.Log("[BDArmory]: Loaded vessel: " + v.vesselName + ", Velocity: " + v.Velocity() + ", packed: " + v.packed);
                //v.SetWorldVelocity(Vector3d.zero);	
            }
        }

        public void SaveVolumeSettings()
        {
            SeismicChargeFX.originalShipVolume = GameSettings.SHIP_VOLUME;
            SeismicChargeFX.originalMusicVolume = GameSettings.MUSIC_VOLUME;
            SeismicChargeFX.originalAmbienceVolume = GameSettings.AMBIENCE_VOLUME;
        }

        public static object ParseValue(Type type, string value)
        {
            if (type == typeof(string))
            {
                return value;
            }

            if (type == typeof(bool))
            {
                return Boolean.Parse(value);
            }
            else if (type.IsEnum)
            {
                return Enum.Parse(type, value);
            }
            else if (type == typeof(float))
            {
                return Single.Parse(value);
            }
            else if (type == typeof(Single))
            {
                return Single.Parse(value);
            }
            else if (type == typeof(Rect))
            {
                string[] strings = value.Split(',');
                int xVal = Int32.Parse(strings[0].Split(':')[1].Split('.')[0]);
                int yVal = Int32.Parse(strings[1].Split(':')[1].Split('.')[0]);
                int wVal = Int32.Parse(strings[2].Split(':')[1].Split('.')[0]);
                int hVal = Int32.Parse(strings[3].Split(':')[1].Split('.')[0]);
                Rect rectVal = new Rect
                {
                    x = xVal,
                    y = yVal,
                    width = wVal,
                    height = hVal
                };
                return rectVal;
            }
            Debug.LogError("[BDArmory]: BDAPersistantSettingsField to parse settings field of type " + type.ToString() +
                           " and value " + value);

            return null;
        }
    }
}