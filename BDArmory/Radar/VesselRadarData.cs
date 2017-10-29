using System.Collections;
using System.Collections.Generic;
using BDArmory.Misc;
using BDArmory.Parts;
using BDArmory.UI;
using UnityEngine;

namespace BDArmory.Radar
{
    public class VesselRadarData : MonoBehaviour
    {
        private List<ModuleRadar> availableRadars;
        private List<ModuleRadar> externalRadars;
        private List<VesselRadarData> externalVRDs;

        private int rCount;

        public int radarCount
        {
            get { return rCount; }
        }

        public bool guiEnabled
        {
            get { return drawGUI; }
        }

        private bool drawGUI;

        public MissileFire weaponManager;
        public bool canReceiveRadarData;

        //GUI
        public static Rect radarWindowRect;
        public bool linkWindowOpen;
        float numberOfAvailableLinks;
        public Rect linkWindowRect = new Rect(0, 0, 0, 0);
        float linkRectWidth = 200;
        float linkRectEntryHeight = 26;

        public static bool radarRectInitialized;
        float radarScreenSize = 360;
        Rect radarRect;
        float windowBorder = 10;
        float headerHeight = 12;
        float controlsHeight = 58;
        Vector2 pingSize = new Vector2(16, 8);

        Texture2D rollIndicatorTexture =
            GameDatabase.Instance.GetTexture(BDArmorySettings.textureDir + "radarRollIndicator", false);

        public static Texture2D omniBgTexture =
            GameDatabase.Instance.GetTexture(BDArmorySettings.textureDir + "omniRadarTexture", false);

        Texture2D radialBgTexture = GameDatabase.Instance.GetTexture(
            BDArmorySettings.textureDir + "radialRadarTexture", false);

        Texture2D scanTexture = GameDatabase.Instance.GetTexture(BDArmorySettings.textureDir + "omniRadarScanTexture",
            false);

        Texture2D lockIcon = GameDatabase.Instance.GetTexture(BDArmorySettings.textureDir + "lockedRadarIcon", false);

        Texture2D lockIconActive =
            GameDatabase.Instance.GetTexture(BDArmorySettings.textureDir + "lockedRadarIconActive", false);

        Texture2D radarContactIcon = GameDatabase.Instance.GetTexture(BDArmorySettings.textureDir + "radarContactIcon",
            false);

        Texture2D friendlyContactIcon =
            GameDatabase.Instance.GetTexture(BDArmorySettings.textureDir + "friendlyContactIcon", false);

        float lockIconSize = 24;
        GUIStyle distanceStyle;
        GUIStyle lockStyle;
        GUIStyle radarTopStyle;

        bool noData;

        float guiInputTime;
        float guiInputCooldown = 0.2f;

        //range increments
        public float[] rIncrements = new float[] {500,2500,5000,10000,20000,40000,100000};
        int rangeIndex = 0;

        //lock cursor
        bool showSelector;
        Vector2 selectorPos = Vector2.zero;

        //data link
        private List<VesselRadarData> availableExternalVRDs;

        private Transform referenceTransform;
        private Transform vesselReferenceTransform;

		public MissileBase LastMissile;

        //bool boresightScan = false;

        //TargetSignatureData[] contacts = new TargetSignatureData[30];
        List<RadarDisplayData> displayedTargets;
        public bool locked;
        int activeLockedTargetIndex;
        List<int> lockedTargetIndexes;

        public bool hasLoadedExternalVRDs = false;

        public List<TargetSignatureData> GetLockedTargets()
        {
            List<TargetSignatureData> lockedTargets = new List<TargetSignatureData>();
            for (int i = 0; i < lockedTargetIndexes.Count; i++)
            {
                lockedTargets.Add(displayedTargets[lockedTargetIndexes[i]].targetData);
            }
            return lockedTargets;
        }

        public RadarDisplayData lockedTargetData
        {
            get { return displayedTargets[lockedTargetIndexes[activeLockedTargetIndex]]; }
        }

        //turret slaving
        public bool slaveTurrets;

        Vessel myVessel;

        public Vessel vessel
        {
            get { return myVessel; }
        }

        public void AddRadar(ModuleRadar mr)
        {
            if (availableRadars.Contains(mr))
            {
                return;
            }

            availableRadars.Add(mr);
            rCount = availableRadars.Count;
            //UpdateDataLinkCapability();
            linkCapabilityDirty = true;
        }

        public void RemoveRadar(ModuleRadar mr)
        {
            availableRadars.Remove(mr);
            rCount = availableRadars.Count;
            RemoveDataFromRadar(mr);
            //UpdateDataLinkCapability();
            linkCapabilityDirty = true;
        }

        public bool linkCapabilityDirty;
        public bool radarsReady;

        void Awake()
        {
            availableRadars = new List<ModuleRadar>();
            externalRadars = new List<ModuleRadar>();
            myVessel = GetComponent<Vessel>();
            lockedTargetIndexes = new List<int>();
            availableExternalVRDs = new List<VesselRadarData>();

            distanceStyle = new GUIStyle();
            distanceStyle.normal.textColor = new Color(0, 1, 0, 0.75f);
            distanceStyle.alignment = TextAnchor.UpperLeft;

            lockStyle = new GUIStyle();
            lockStyle.normal.textColor = new Color(0, 1, 0, 0.75f);
            lockStyle.alignment = TextAnchor.LowerCenter;
            lockStyle.fontSize = 16;

            radarTopStyle = new GUIStyle();
            radarTopStyle.normal.textColor = new Color(0, 1, 0, 0.65f);
            radarTopStyle.alignment = TextAnchor.UpperCenter;
            radarTopStyle.fontSize = 12;

            vesselReferenceTransform = (new GameObject()).transform;
            //vesselReferenceTransform.parent = vessel.transform;
            //vesselReferenceTransform.localPosition = Vector3.zero;
            vesselReferenceTransform.localScale = Vector3.one;

            displayedTargets = new List<RadarDisplayData>();
            externalVRDs = new List<VesselRadarData>();
            waitingForVessels = new List<string>();

            radarRect = new Rect(0, 0, radarScreenSize, radarScreenSize);

            if (!radarRectInitialized)
            {
                float width = radarScreenSize + (2*windowBorder);
                float height = radarScreenSize + (2*windowBorder) + headerHeight + controlsHeight;
                radarWindowRect = new Rect(Screen.width - width, Screen.height - height, width, height);
                radarRectInitialized = true;
            }
        }

        void Start()
        {
            rangeIndex = rIncrements.Length - 2;

            //determine configured physics ranges and add a radar range level for the highest range
            if (vessel.vesselRanges.flying.load > rIncrements[rIncrements.Length-1])
            {
                rIncrements = new float[] { 500, 2500, 5000, 10000, 20000, 40000, 100000, vessel.vesselRanges.flying.load };
                rangeIndex--;
            }

            UpdateLockedTargets();
            List<MissileFire>.Enumerator mf = vessel.FindPartModulesImplementing<MissileFire>().GetEnumerator();
            while (mf.MoveNext())
            {
                if (mf.Current == null) continue;
                mf.Current.vesselRadarData = this;
            }
            mf.Dispose();
            GameEvents.onVesselDestroy.Add(OnVesselDestroyed);
            GameEvents.onVesselCreate.Add(OnVesselDestroyed);
            MissileFire.OnToggleTeam += OnToggleTeam;
            GameEvents.onGameStateSave.Add(OnGameStateSave);
            GameEvents.onPartDestroyed.Add(PartDestroyed);

            if (!weaponManager)
            {
                List<MissileFire>.Enumerator mfa = vessel.FindPartModulesImplementing<MissileFire>().GetEnumerator();
                while (mfa.MoveNext())
                {
                    if (mfa.Current == null) continue;
                    weaponManager = mfa.Current;
                    break;
                }
                mfa.Dispose();
            }

            StartCoroutine(StartupRoutine());
        }

        IEnumerator StartupRoutine()
        {
            while (!FlightGlobals.ready || vessel.packed)
            {
                yield return null;
            }

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            radarsReady = true;
        }

        void OnGameStateSave(ConfigNode n)
        {
            SaveExternalVRDVessels();
        }

        void SaveExternalVRDVessels()
        {
            string linkedVesselID = "";

            List<VesselRadarData>.Enumerator v = externalVRDs.GetEnumerator();
            while (v.MoveNext())
            {
                if (v.Current == null) continue;
                linkedVesselID += v.Current.vessel.id + ",";
            }
            v.Dispose();

            List<string>.Enumerator id = waitingForVessels.GetEnumerator();
            while (id.MoveNext())
            {
                if (id.Current == null) continue;
                linkedVesselID += id.Current + ",";
            }
            id.Dispose();

            List<ModuleRadar>.Enumerator radar = availableRadars.GetEnumerator();
            while (radar.MoveNext())
            {
                if (radar.Current == null) continue;
                if (radar.Current.vessel != vessel) continue;
                radar.Current.linkedVesselID = linkedVesselID;
                return;
            }
            radar.Dispose();
        }

        void OnDestroy()
        {
            GameEvents.onVesselDestroy.Remove(OnVesselDestroyed);
            GameEvents.onVesselCreate.Remove(OnVesselDestroyed);
            MissileFire.OnToggleTeam -= OnToggleTeam;
            GameEvents.onGameStateSave.Remove(OnGameStateSave);
            GameEvents.onPartDestroyed.Remove(PartDestroyed);

            if (weaponManager)
            {
                if (slaveTurrets)
                {
                    weaponManager.slavingTurrets = false;
                }
            }
        }

        void OnToggleTeam(MissileFire wm, BDArmorySettings.BDATeams team)
        {
            if (!weaponManager || !wm) return;

            if (team != BDATargetManager.BoolToTeam(weaponManager.team))
            {
                if (wm.vesselRadarData)
                {
                    UnlinkVRD(wm.vesselRadarData);
                }
            }
            else if (wm.vessel == vessel)
            {
                UnlinkAllExternalRadars();
            }

            RemoveDisconnectedRadars();
        }

        void UpdateDataLinkCapability()
        {
            canReceiveRadarData = false;
            noData = true;
            List<ModuleRadar>.Enumerator rad = availableRadars.GetEnumerator();
            while (rad.MoveNext())
            {
                if (rad.Current == null) continue;
                if (rad.Current.vessel == vessel && rad.Current.canRecieveRadarData)
                {
                    canReceiveRadarData = true;
                }

                if (rad.Current.canScan)
                {
                    noData = false;
                }
            }
            rad.Dispose();

            if (!canReceiveRadarData)
            {
                UnlinkAllExternalRadars();
            }

            List<ModuleRadar>.Enumerator mr = availableRadars.GetEnumerator();
            while (mr.MoveNext())
            {
                if (mr.Current == null) continue;
                if (mr.Current.canScan)
                {
                    noData = false;
                }
            }
            mr.Dispose();
        }

        void UpdateReferenceTransform()
        {
            if (radarCount == 1 && !availableRadars[0].omnidirectional && !vessel.Landed)
            {
                referenceTransform = availableRadars[0].referenceTransform;
            }
            else
            {
                referenceTransform = vesselReferenceTransform;
            }
        }

        void PartDestroyed(Part p)
        {
            RemoveDisconnectedRadars();
            UpdateLockedTargets();
            RefreshAvailableLinks();
        }

        void OnVesselDestroyed(Vessel v)
        {
            RemoveDisconnectedRadars();
            UpdateLockedTargets();
            RefreshAvailableLinks();
        }

        void RemoveDisconnectedRadars()
        {
            availableRadars.RemoveAll(r => r == null);
            List<ModuleRadar> radarsToRemove = new List<ModuleRadar>();
            List<ModuleRadar>.Enumerator radar = availableRadars.GetEnumerator();
            while (radar.MoveNext())
            {
                if (radar.Current == null) continue;
                if (!radar.Current.radarEnabled || (radar.Current.vessel != vessel && !externalRadars.Contains(radar.Current)))
                {
                    radarsToRemove.Add(radar.Current);
                }
                else if (!radar.Current.weaponManager || (weaponManager && radar.Current.weaponManager.team != weaponManager.team))
                {
                    radarsToRemove.Add(radar.Current);
                }
            }
            radar.Dispose();

            List<ModuleRadar>.Enumerator rrad = radarsToRemove.GetEnumerator();
            while (rrad.MoveNext())
            {
                if (rrad.Current == null) continue;
                RemoveRadar(rrad.Current);
            }
            rrad.Dispose();
            rCount = availableRadars.Count;

            RemoveEmptyVRDs();
        }

        public void UpdateLockedTargets()
        {
            locked = false;

            lockedTargetIndexes.Clear(); // = new List<int>();

            for (int i = 0; i < displayedTargets.Count; i++)
            {
                if (!displayedTargets[i].vessel || !displayedTargets[i].locked) continue;
                locked = true;
                lockedTargetIndexes.Add(i);
            }

            activeLockedTargetIndex = locked 
                ? Mathf.Clamp(activeLockedTargetIndex, 0, lockedTargetIndexes.Count - 1) 
                : 0;
        }

        void UpdateSlaveData()
        {
            if (!slaveTurrets || !weaponManager) return;
            weaponManager.slavingTurrets = true;
            if (!locked) return;
            TargetSignatureData lockedTarget = lockedTargetData.targetData;
            weaponManager.slavedPosition = lockedTarget.predictedPosition;
            weaponManager.slavedVelocity = lockedTarget.velocity;
            weaponManager.slavedAcceleration = lockedTarget.acceleration;
        }

        void Update()
        {
            if (!vessel)
            {
                Destroy(this);
                return;
            }

            UpdateReferenceTransform();

            if (radarCount > 0)
            {
                //vesselReferenceTransform.parent = linkedRadars[0].transform;
                vesselReferenceTransform.localScale = Vector3.one;
                vesselReferenceTransform.position = vessel.CoM;

                if (vessel.LandedOrSplashed)
                {
                    vesselReferenceTransform.rotation =
                        Quaternion.LookRotation(VectorUtils.GetNorthVector(vessel.transform.position, vessel.mainBody),
                            vessel.upAxis);
                }
                else
                {
                    vesselReferenceTransform.rotation =
                        Quaternion.LookRotation(Vector3.ProjectOnPlane(vessel.transform.up, vessel.upAxis),
                            vessel.upAxis);
                }

                CleanDisplayedContacts();

                UpdateInputs();

                UpdateSlaveData();
            }
            else
            {
                if (slaveTurrets)
                {
                    UnslaveTurrets();
                }
            }


            if (linkCapabilityDirty)
            {
                UpdateDataLinkCapability();

                linkCapabilityDirty = false;
            }

            drawGUI = (HighLogic.LoadedSceneIsFlight && FlightGlobals.ready && !vessel.packed && rCount > 0 &&
                       vessel.isActiveVessel && BDArmorySettings.GAME_UI_ENABLED && !MapView.MapIsEnabled);

            if (!vessel.loaded && radarCount == 0)
            {
                Destroy(this);
            }
        }

        public bool autoCycleLockOnFire = true;

        public void CycleActiveLock()
        {
            if (locked)
            {
                activeLockedTargetIndex++;
                if (activeLockedTargetIndex >= lockedTargetIndexes.Count)
                {
                    activeLockedTargetIndex = 0;
                }

                lockedTargetData.detectedByRadar.SetActiveLock(lockedTargetData.targetData);

                UpdateLockedTargets();
            }
        }

        void IncreaseRange()
        {
            int origIndex = rangeIndex;
            rangeIndex = Mathf.Clamp(rangeIndex + 1, 0, rIncrements.Length - 1);
            if (origIndex != rangeIndex)
            {
                pingPositionsDirty = true;
                UpdateRWRRange();
            }
        }

        void DecreaseRange()
        {
            int origIndex = rangeIndex;
            rangeIndex = Mathf.Clamp(rangeIndex - 1, 0, rIncrements.Length - 1);
            if (origIndex != rangeIndex)
            {
                pingPositionsDirty = true;
                UpdateRWRRange();
            }
        }

        /// <summary>
        /// Update the radar range also on the rwr display
        /// </summary>
        void UpdateRWRRange()
        {
            List<RadarWarningReceiver>.Enumerator rwr = vessel.FindPartModulesImplementing<RadarWarningReceiver>().GetEnumerator();
            while (rwr.MoveNext())
            {
                rwr.Current.rwrDisplayRange = rIncrements[rangeIndex];
            }
        }

        bool TryLockTarget(RadarDisplayData radarTarget)
        {
            if (radarTarget.locked) return false;

            ModuleRadar lockingRadar = null;
            //first try using the last radar to detect that target
            if (CheckRadarForLock(radarTarget.detectedByRadar, radarTarget))
            {
                lockingRadar = radarTarget.detectedByRadar;
            }
            else
            {
                List<ModuleRadar>.Enumerator radar = availableRadars.GetEnumerator();
                while (radar.MoveNext())
                {
                    if (radar.Current == null) continue;
                    if (!CheckRadarForLock(radar.Current, radarTarget)) continue;
                    lockingRadar = radar.Current;
                    break;
                }
                radar.Dispose();
            }

            if (lockingRadar != null)
            {
                return lockingRadar.TryLockTarget(radarTarget.targetData.predictedPosition);
            }

            UpdateLockedTargets();
            StartCoroutine(UpdateLocksAfterFrame());
            return false;
        }

        IEnumerator UpdateLocksAfterFrame()
        {
            yield return null;
            UpdateLockedTargets();
        }

        public void TryLockTarget(Vector3 worldPosition)
        {
            List<RadarDisplayData>.Enumerator displayData = displayedTargets.GetEnumerator();
            while (displayData.MoveNext())
            {
                if (!(Vector3.SqrMagnitude(worldPosition - displayData.Current.targetData.predictedPosition) <
                      40 * 40)) continue;
                TryLockTarget(displayData.Current);
                return;
            }
            displayData.Dispose();
            return;
        }

        public bool TryLockTarget(Vessel v)
        {
            if (!v) return false;

            List<RadarDisplayData>.Enumerator displayData = displayedTargets.GetEnumerator();
            while (displayData.MoveNext())
            {
                if (v == displayData.Current.vessel)
                {
                    return TryLockTarget(displayData.Current);
                }
            }
            displayData.Dispose();

            RadarDisplayData newData = new RadarDisplayData();
            newData.vessel = v;
            newData.detectedByRadar = null;
            newData.targetData = new TargetSignatureData(v, 999);

            return TryLockTarget(newData);

            //return false;
        }

        bool CheckRadarForLock(ModuleRadar radar, RadarDisplayData radarTarget)
        {
            if (!radar) return false;

            return
            (
                radar.canLock
                && (!radar.locked || radar.currentLocks < radar.maxLocks)
                && radarTarget.targetData.signalStrength > radar.radarLockTrackCurve.Evaluate((radarTarget.targetData.predictedPosition - radar.transform.position).magnitude / 1000f)
                &&
                (radar.omnidirectional ||
                 Vector3.Angle(radar.transform.up, radarTarget.targetData.predictedPosition - radar.transform.position) <
                 radar.directionalFieldOfView/2)
            );
        }

        void DisableAllRadars()
        {
            //rCount = 0;
            UnlinkAllExternalRadars();

            List<ModuleRadar>.Enumerator radar = vessel.FindPartModulesImplementing<ModuleRadar>().GetEnumerator();
            while (radar.MoveNext())
            {
                if (radar.Current == null) continue;
                radar.Current.DisableRadar();
            }
            radar.Dispose();
        }

        public void SlaveTurrets()
        {
            List<ModuleTargetingCamera>.Enumerator mtc = vessel.FindPartModulesImplementing<ModuleTargetingCamera>().GetEnumerator();
            while (mtc.MoveNext())
            {
                if (mtc.Current == null) continue;
                mtc.Current.slaveTurrets = false;
            }
            mtc.Dispose();
            slaveTurrets = true;
        }

        public void UnslaveTurrets()
        {
            List<ModuleTargetingCamera>.Enumerator mtc = vessel.FindPartModulesImplementing<ModuleTargetingCamera>().GetEnumerator();
            while (mtc.MoveNext())
            {
                if (mtc.Current == null) continue;
                mtc.Current.slaveTurrets = false;
            }
            mtc.Dispose();

            slaveTurrets = false;

            if (weaponManager)
            {
                weaponManager.slavingTurrets = false;
            }
        }

        void OnGUI()
        {
            if (!drawGUI) return;

            for (int i = 0; i < lockedTargetIndexes.Count; i++)
            {
                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                {
                    string label = string.Empty;
                    if (i == activeLockedTargetIndex)
                    {
                        label += "Active: ";
                    }
                    if (!displayedTargets[lockedTargetIndexes[i]].vessel)
                    {
                        label += "data with no vessel";
                    }
                    else
                    {
                        label += displayedTargets[lockedTargetIndexes[i]].vessel.vesselName;
                    }
                    GUI.Label(new Rect(20, 60 + (i*26), 800, 446), label);
                }

                TargetSignatureData lockedTarget = displayedTargets[lockedTargetIndexes[i]].targetData;
                if (i == activeLockedTargetIndex)
                {
                    if (weaponManager && lockedTarget.team == BDATargetManager.BoolToTeam(weaponManager.team))
                    {
                        BDGUIUtils.DrawTextureOnWorldPos(lockedTarget.predictedPosition,
                            BDArmorySettings.Instance.crossedGreenSquare, new Vector2(20, 20), 0);
                    }
                    else
                    {
                        BDGUIUtils.DrawTextureOnWorldPos(lockedTarget.predictedPosition,
                            BDArmorySettings.Instance.openGreenSquare, new Vector2(20, 20), 0);
                    }
                }
                else
                {
                    BDGUIUtils.DrawTextureOnWorldPos(lockedTarget.predictedPosition,
                        BDArmorySettings.Instance.greenDiamondTexture, new Vector2(17, 17), 0);
                }
            }


            const string windowTitle = "Radar";
            radarWindowRect = GUI.Window(524141, radarWindowRect, RadarWindow, windowTitle, HighLogic.Skin.window);
            BDGUIUtils.UseMouseEventInRect(radarWindowRect);

            if (linkWindowOpen && canReceiveRadarData)
            {
                linkWindowRect = new Rect(radarWindowRect.x - linkRectWidth, radarWindowRect.y + 16, linkRectWidth,
                    16 + (numberOfAvailableLinks*linkRectEntryHeight));
                LinkRadarWindow();

                BDGUIUtils.UseMouseEventInRect(linkWindowRect);
            }

        }


        //GUI

        void RadarWindow(int windowID)
        {
            GUI.DragWindow(new Rect(0, 0, radarScreenSize + (2*windowBorder), windowBorder + headerHeight));

            if (!referenceTransform) return;

            Rect displayRect = new Rect(windowBorder, 12 + windowBorder, radarScreenSize, radarScreenSize);


            //==============================
            GUI.BeginGroup(displayRect);

            if (availableRadars.Count == 0) return;
            //bool omnidirectionalDisplay = (radarCount == 1 && linkedRadars[0].omnidirectional);
            float directionalFieldOfView = omniDisplay ? 0 : availableRadars[0].directionalFieldOfView;
            //bool linked = (radarCount > 1);
            if (omniDisplay)
            {
                GUI.DrawTexture(radarRect, omniBgTexture, ScaleMode.StretchToFill, true);

                if (vessel.LandedOrSplashed)
                {
                    GUI.Label(radarRect, "  N", radarTopStyle);
                }

                GUI.Label(new Rect(radarScreenSize*0.85f, radarScreenSize*0.1f, 60, 24),
                    (rIncrements[rangeIndex]/1000).ToString("0") + "km", distanceStyle);

                //my ship direction icon
                float directionSize = 16;
                Vector3 projectedVesselFwd = Vector3.ProjectOnPlane(vessel.ReferenceTransform.up, referenceTransform.up);
                float dAngle = Vector3.Angle(projectedVesselFwd, referenceTransform.forward);
                if (referenceTransform.InverseTransformVector(vessel.ReferenceTransform.up).x < 0)
                {
                    dAngle = -dAngle;
                }
                GUIUtility.RotateAroundPivot(dAngle, radarRect.center);
                GUI.DrawTexture(
                    new Rect(radarRect.center.x - (directionSize/2), radarRect.center.y - (directionSize/2),
                        directionSize, directionSize), BDArmorySettings.Instance.directionTriangleIcon,
                    ScaleMode.StretchToFill, true);
                GUI.matrix = Matrix4x4.identity;

                for (int i = 0; i < rCount; i++)
                {
                    bool canScan = availableRadars[i].canScan;
                    bool canTrackWhileScan = availableRadars[i].canTrackWhileScan;
                    bool islocked = availableRadars[i].locked;
                    float currentAngle = availableRadars[i].currentAngle;

                    float radarAngle = VectorUtils.SignedAngle(projectedVesselFwd,
                        Vector3.ProjectOnPlane(availableRadars[i].transform.up, referenceTransform.up),
                        referenceTransform.right);

                    if (!canScan || availableRadars[i].vessel != vessel) continue;
                    if ((!islocked || canTrackWhileScan))
                    {
                        if (!availableRadars[i].omnidirectional)
                        {
                            currentAngle += radarAngle + dAngle;
                        }
                        else if (!vessel.Landed)
                        {
                            Vector3 north = VectorUtils.GetNorthVector(referenceTransform.position, vessel.mainBody);
                            float angleFromNorth = VectorUtils.SignedAngle(north, projectedVesselFwd,
                                Vector3.Cross(north, vessel.upAxis));
                            currentAngle += angleFromNorth;
                        }


                        GUIUtility.RotateAroundPivot(currentAngle, new Vector2(radarScreenSize/2, radarScreenSize/2));
                        if (availableRadars[i].omnidirectional && radarCount == 1)
                        {
                            GUI.DrawTexture(radarRect, scanTexture, ScaleMode.StretchToFill, true);
                        }
                        else
                        {
                            BDGUIUtils.DrawRectangle(
                                new Rect(radarRect.x + (radarRect.width/2) - 1, radarRect.y, 2, radarRect.height/2),
                                new Color(0, 1, 0, 0.35f));
                        }
                        GUI.matrix = Matrix4x4.identity;
                    }

                    //if linked and directional, draw FOV lines
                    if (availableRadars[i].omnidirectional) continue;
                    float fovAngle = availableRadars[i].directionalFieldOfView/2;
                    float lineWidth = 2;
                    Rect verticalLineRect = new Rect(radarRect.center.x - (lineWidth/2), 0, lineWidth,
                        radarRect.center.y);
                    GUIUtility.RotateAroundPivot(dAngle + fovAngle + radarAngle, radarRect.center);
                    BDGUIUtils.DrawRectangle(verticalLineRect, new Color(0, 1, 0, 0.6f));
                    GUI.matrix = Matrix4x4.identity;
                    GUIUtility.RotateAroundPivot(dAngle - fovAngle + radarAngle, radarRect.center);
                    BDGUIUtils.DrawRectangle(verticalLineRect, new Color(0, 1, 0, 0.4f));
                    GUI.matrix = Matrix4x4.identity;
                }
            }
            else
            {
                GUI.DrawTexture(radarRect, radialBgTexture, ScaleMode.StretchToFill, true);
                GUI.Label(new Rect(5, 5, 60, 24), (rIncrements[rangeIndex]/1000).ToString("0") + "km", distanceStyle);

                for (int i = 0; i < rCount; i++)
                {
                    bool canScan = availableRadars[i].canScan;
                    bool islocked = availableRadars[i].locked;
                    //float lockScanAngle = linkedRadars[i].lockScanAngle;
                    float currentAngle = availableRadars[i].currentAngle;
                    if (!canScan) continue;
                    float indicatorAngle = currentAngle; //locked ? lockScanAngle : currentAngle;
                    Vector2 scanIndicatorPos =
                        RadarUtils.WorldToRadarRadial(
                            referenceTransform.position +
                            (Quaternion.AngleAxis(indicatorAngle, referenceTransform.up)*referenceTransform.forward),
                            referenceTransform, radarRect, 5000, directionalFieldOfView/2);
                    GUI.DrawTexture(new Rect(scanIndicatorPos.x - 7, scanIndicatorPos.y - 10, 14, 20),
                        BDArmorySettings.Instance.greenDiamondTexture, ScaleMode.StretchToFill, true);

                    if (!islocked || !availableRadars[i].canTrackWhileScan) continue;
                    Vector2 leftPos =
                        RadarUtils.WorldToRadarRadial(
                            referenceTransform.position +
                            (Quaternion.AngleAxis(availableRadars[i].leftLimit, referenceTransform.up)*
                             referenceTransform.forward), referenceTransform, radarRect, 5000,
                            directionalFieldOfView/2);
                    Vector2 rightPos =
                        RadarUtils.WorldToRadarRadial(
                            referenceTransform.position +
                            (Quaternion.AngleAxis(availableRadars[i].rightLimit, referenceTransform.up)*
                             referenceTransform.forward), referenceTransform, radarRect, 5000,
                            directionalFieldOfView/2);
                    float barWidth = 2;
                    float barHeight = 15;
                    Color origColor = GUI.color;
                    GUI.color = Color.green;
                    GUI.DrawTexture(new Rect(leftPos.x - barWidth, leftPos.y - barHeight, barWidth, barHeight),
                        Texture2D.whiteTexture, ScaleMode.StretchToFill, true);
                    GUI.DrawTexture(new Rect(rightPos.x, rightPos.y - barHeight, barWidth, barHeight),
                        Texture2D.whiteTexture, ScaleMode.StretchToFill, true);
                    GUI.color = origColor;
                }
            }

            //selector
            if (showSelector)
            {
                float selectorSize = 18;
                Rect selectorRect = new Rect(selectorPos.x - (selectorSize/2), selectorPos.y - (selectorSize/2),
                    selectorSize, selectorSize);
                Rect sLeftRect = new Rect(selectorRect.x, selectorRect.y, selectorSize/6, selectorRect.height);
                Rect sRightRect = new Rect(selectorRect.x + selectorRect.width - (selectorSize/6), selectorRect.y,
                    selectorSize/6, selectorRect.height);
                BDGUIUtils.DrawRectangle(sLeftRect, Color.green);
                BDGUIUtils.DrawRectangle(sRightRect, Color.green);
            }

            //missile data
            if (LastMissile && LastMissile.TargetAcquired)
            {
                Rect missileDataRect = new Rect(5, radarRect.height - 65, radarRect.width - 5, 60);
                string missileDataString = LastMissile.GetShortName();
                missileDataString += "\nT-" + LastMissile.TimeToImpact.ToString("0");

                if (LastMissile.ActiveRadar && Mathf.Round(Time.time*3)%2 == 0)
                {
                    missileDataString += "\nACTIVE";
                }
                GUI.Label(missileDataRect, missileDataString, distanceStyle);
            }


            //roll indicator
            if (!vessel.Landed)
            {
                Vector3 localUp = vessel.ReferenceTransform.InverseTransformDirection(referenceTransform.up);
                localUp = Vector3.ProjectOnPlane(localUp, Vector3.up).normalized;
                float rollAngle = -Misc.Misc.SignedAngle(-Vector3.forward, localUp, Vector3.right);
                GUIUtility.RotateAroundPivot(rollAngle, radarRect.center);
                GUI.DrawTexture(radarRect, rollIndicatorTexture, ScaleMode.StretchToFill, true);
                GUI.matrix = Matrix4x4.identity;
            }


            if (noData)
            {
                GUI.Label(radarRect, "NO DATA\n", lockStyle);
            }
            else
            {
                DrawDisplayedContacts();
            }


            GUI.EndGroup();
            //=========================================


            float buttonWidth = 70;
            float gap = 2;
            float buttonHeight = (controlsHeight/2) - (2*gap);
            float controlsStartY = headerHeight + radarScreenSize + windowBorder + windowBorder;
            float controlsStartY2 = controlsStartY + buttonHeight + gap;

            Rect rangeUpRect = new Rect(windowBorder, controlsStartY, buttonWidth, buttonHeight);
            if (GUI.Button(rangeUpRect, "Range +", HighLogic.Skin.button))
            {
                IncreaseRange();
            }
            Rect rangeDnRect = new Rect(rangeUpRect.x, controlsStartY2, rangeUpRect.width, rangeUpRect.height);
            if (GUI.Button(rangeDnRect, "Range -", HighLogic.Skin.button))
            {
                DecreaseRange();
            }

            Rect dataLinkRect = new Rect(rangeUpRect.x + gap + rangeUpRect.width, rangeUpRect.y, buttonWidth,
                buttonHeight);
            if (canReceiveRadarData)
            {
                if (GUI.Button(dataLinkRect, "Data Link", linkWindowOpen ? HighLogic.Skin.box : HighLogic.Skin.button))
                {
                    if (linkWindowOpen)
                    {
                        CloseLinkRadarWindow();
                    }
                    else
                    {
                        OpenLinkRadarWindow();
                    }
                }
            }
            else
            {
                Color oCol = GUI.color;
                GUI.color = new Color(1, 1, 1, 0.35f);
                GUI.Box(dataLinkRect, "Link N/A", HighLogic.Skin.button);
                GUI.color = oCol;
            }

            Rect lockModeCycleRect = new Rect(windowBorder + gap + buttonWidth + gap + buttonWidth, controlsStartY,
                buttonWidth, buttonHeight);

            if (locked)
            {
                if (GUI.Button(lockModeCycleRect, "Cycle Lock", HighLogic.Skin.button))
                {
                    CycleActiveLock();
                }
            }
            else if (!omniDisplay) //SCAN MODE SELECTOR
            {
                if (!locked)
                {
                    string boresightToggle = availableRadars[0].boresightScan ? "Scan" : "Boresight";
                    if (GUI.Button(lockModeCycleRect, boresightToggle, HighLogic.Skin.button))
                    {
                        availableRadars[0].boresightScan = !availableRadars[0].boresightScan;
                    }
                }
            }

            Rect slaveRect = new Rect(lockModeCycleRect.x + gap + lockModeCycleRect.width, lockModeCycleRect.y,
                buttonWidth*1.5f, buttonHeight);
            //slave button
            if (GUI.Button(slaveRect, slaveTurrets ? "Unslave Turrets" : "Slave Turrets",
                slaveTurrets ? HighLogic.Skin.box : HighLogic.Skin.button))
            {
                if (slaveTurrets)
                {
                    UnslaveTurrets();
                }
                else
                {
                    SlaveTurrets();
                }
            }

            //unlocking
            Rect unlockRect = new Rect(lockModeCycleRect);
            unlockRect.y += unlockRect.height + gap;
            Rect unlockAllRect = new Rect(slaveRect);
            unlockAllRect.y = unlockRect.y;
            if (locked)
            {
                if (GUI.Button(unlockRect, "Unlock", HighLogic.Skin.button))
                {
                    UnlockCurrentTarget();
                }

                if (GUI.Button(unlockAllRect, "Unlock All", HighLogic.Skin.button))
                {
                    UnlockAllTargets();
                }
            }


            Rect offRect = new Rect(slaveRect.x + gap + slaveRect.width, controlsStartY, 0, (2*buttonHeight) + gap);
            offRect.width = radarWindowRect.width - offRect.x - windowBorder;
            if (GUI.Button(offRect, "O\nF\nF", HighLogic.Skin.button))
            {
                DisableAllRadars();
            }
        }

        void LinkRadarWindow()
        {
            GUI.Box(linkWindowRect, string.Empty, HighLogic.Skin.window);

            numberOfAvailableLinks = 0;

            GUI.BeginGroup(linkWindowRect);

            if (GUI.Button(new Rect(8, 8, 100, linkRectEntryHeight), "Refresh", HighLogic.Skin.button))
            {
                RefreshAvailableLinks();
            }
            numberOfAvailableLinks += 1.25f;

            List<VesselRadarData>.Enumerator v = availableExternalVRDs.GetEnumerator();
            while (v.MoveNext())
            {
                if (v.Current == null) continue;
                if (!v.Current.vessel || !v.Current.vessel.loaded) continue;
                bool linked = externalVRDs.Contains(v.Current);
                GUIStyle style = linked ? HighLogic.Skin.box : HighLogic.Skin.button;
                if (
                    GUI.Button(
                        new Rect(8, 8 + (linkRectEntryHeight*numberOfAvailableLinks), linkRectWidth - 16,
                            linkRectEntryHeight), v.Current.vessel.vesselName, style))
                {
                    if (linked)
                    {
                        //UnlinkRadar(v);
                        UnlinkVRD(v.Current);
                    }
                    else
                    {
                        //LinkToRadar(v);
                        LinkVRD(v.Current);
                    }
                }
                numberOfAvailableLinks++;
            }
            v.Dispose();

            GUI.EndGroup();
        }

        public void RemoveDataFromRadar(ModuleRadar radar)
        {
            displayedTargets.RemoveAll(t => t.detectedByRadar == radar);
            UpdateLockedTargets();
        }

        void UnlinkVRD(VesselRadarData vrd)
        {
            Debug.Log("[BDArmory]: Unlinking VRD: " + vrd.vessel.vesselName);
            externalVRDs.Remove(vrd);

            List<ModuleRadar> radarsToUnlink = new List<ModuleRadar>();

            List<ModuleRadar>.Enumerator mra = availableRadars.GetEnumerator();
            while (mra.MoveNext())
            {
                if (mra.Current == null) continue;
                if (mra.Current.vesselRadarData == vrd)
                {
                    radarsToUnlink.Add(mra.Current);
                }
            }
            mra.Dispose();

            List<ModuleRadar>.Enumerator mr = radarsToUnlink.GetEnumerator();
            while (mr.MoveNext())
            {
                if (mr.Current == null) continue;
                Debug.Log("[BDArmory]:  - Unlinking radar: " + mr.Current.radarName);
                UnlinkRadar(mr.Current);
            }
            mr.Dispose();

            SaveExternalVRDVessels();
        }

        void UnlinkRadar(ModuleRadar mr)
        {
            if (mr && mr.vessel)
            {
                RemoveRadar(mr);
                externalRadars.Remove(mr);
                mr.RemoveExternalVRD(this);

                bool noMoreExternalRadar = true;
                List<ModuleRadar>.Enumerator rad = externalRadars.GetEnumerator();
                while (rad.MoveNext())
                {
                    if (rad.Current == null) continue;
                    if (rad.Current.vessel != mr.vessel) continue;
                    noMoreExternalRadar = false;
                    break;
                }
                rad.Dispose();

                if (noMoreExternalRadar)
                {
                    externalVRDs.Remove(mr.vesselRadarData);
                }
            }
            else
            {
                externalRadars.RemoveAll(r => r == null);
            }
        }

        void RemoveEmptyVRDs()
        {
            externalVRDs.RemoveAll(vrd => vrd == null);
            List<VesselRadarData> vrdsToRemove = new List<VesselRadarData>();
            List<VesselRadarData>.Enumerator vrda = externalVRDs.GetEnumerator();
            while (vrda.MoveNext())
            {
                if (vrda.Current == null) continue;
                if (vrda.Current.rCount == 0)
                {
                    vrdsToRemove.Add(vrda.Current);
                }
            }
            vrda.Dispose();

            List<VesselRadarData>.Enumerator vrdr = vrdsToRemove.GetEnumerator();
            while (vrdr.MoveNext())
            {
                if (vrdr.Current == null) continue;
                externalVRDs.Remove(vrdr.Current);
            }
            vrdr.Dispose();
        }

        public void UnlinkDisabledRadar(ModuleRadar mr)
        {
            RemoveRadar(mr);
            externalRadars.Remove(mr);
            SaveExternalVRDVessels();
        }

        public void BeginWaitForUnloadedLinkedRadar(ModuleRadar mr, string vesselID)
        {
            UnlinkDisabledRadar(mr);

            if (waitingForVessels.Contains(vesselID))
            {
                return;
            }

            waitingForVessels.Add(vesselID);
            SaveExternalVRDVessels();
            StartCoroutine(RecoverUnloadedLinkedVesselRoutine(vesselID));
        }

        List<string> waitingForVessels;

        IEnumerator RecoverUnloadedLinkedVesselRoutine(string vesselID)
        {
            while (true)
            {
                List<Vessel>.Enumerator v = BDATargetManager.LoadedVessels.GetEnumerator();
                while (v.MoveNext())
                {
                    if (v.Current == null || !v.Current.loaded || v.Current == vessel) continue;
                    if (v.Current.id.ToString() != vesselID) continue;
                    VesselRadarData vrd = v.Current.gameObject.GetComponent<VesselRadarData>();
                    if (!vrd) continue;
                    waitingForVessels.Remove(vesselID);
                    StartCoroutine(LinkVRDWhenReady(vrd));
                    yield break;
                }
                v.Dispose();

                yield return new WaitForSeconds(0.5f);
            }
        }

        IEnumerator LinkVRDWhenReady(VesselRadarData vrd)
        {
            while (!vrd.radarsReady || vrd.vessel.packed || vrd.radarCount < 1)
            {
                yield return null;
            }
            LinkVRD(vrd);
            Debug.Log("[BDArmory]: Radar data link recovered: Local - " + vessel.vesselName + ", External - " +
                      vrd.vessel.vesselName);
        }

        public void UnlinkAllExternalRadars()
        {
            externalRadars.RemoveAll(r => r == null);
            List<ModuleRadar>.Enumerator eRad = externalRadars.GetEnumerator();
            while (eRad.MoveNext())
            {
                if (eRad.Current == null) continue;
                eRad.Current.RemoveExternalVRD(this);
            }
            eRad.Dispose();
            externalRadars.Clear();

            externalVRDs.Clear();

            availableRadars.RemoveAll(r => r == null);
            availableRadars.RemoveAll(r => r.vessel != vessel);
            rCount = availableRadars.Count;

            RefreshAvailableLinks();
        }

        void OpenLinkRadarWindow()
        {
            RefreshAvailableLinks();
            linkWindowOpen = true;
        }

        void CloseLinkRadarWindow()
        {
            linkWindowOpen = false;
        }

        void RefreshAvailableLinks()
        {
            if (!HighLogic.LoadedSceneIsFlight || !weaponManager || (FlightGlobals.Vessels == null) || (!FlightGlobals.ready))
            {                
                return;
            }

            availableExternalVRDs = new List<VesselRadarData>();
            List<Vessel>.Enumerator v = FlightGlobals.Vessels.GetEnumerator();
            while (v.MoveNext())
            {
                if (v.Current == null || !v.Current.loaded || vessel == null || v.Current == vessel) continue;

                BDArmorySettings.BDATeams team = BDArmorySettings.BDATeams.None;
                List<MissileFire>.Enumerator mf = v.Current.FindPartModulesImplementing<MissileFire>().GetEnumerator();
                while (mf.MoveNext())
                {
                    if (mf.Current == null) continue;
                    team = BDATargetManager.BoolToTeam(mf.Current.team);
                    break;
                }
                mf.Dispose();

                if (team != BDATargetManager.BoolToTeam(weaponManager.team)) continue;
                VesselRadarData vrd = v.Current.gameObject.GetComponent<VesselRadarData>();
                if (vrd && vrd.radarCount > 0)
                {
                    availableExternalVRDs.Add(vrd);
                }
            }
            v.Dispose();
        }


        public void LinkVRD(VesselRadarData vrd)
        {
            if (!externalVRDs.Contains(vrd))
            {
                externalVRDs.Add(vrd);
            }

            List<ModuleRadar>.Enumerator mr = vrd.availableRadars.GetEnumerator();
            while (mr.MoveNext())
            {
                if (mr.Current == null) continue;
                LinkToRadar(mr.Current);
            }
            mr.Dispose();
            SaveExternalVRDVessels();
        }

        public void LinkToRadar(ModuleRadar mr)
        {
            if (!mr)
            {
                return;
            }

            if (externalRadars.Contains(mr))
            {
                return;
            }

            externalRadars.Add(mr);
            AddRadar(mr);

            mr.AddExternalVRD(this);
       }


        public void AddRadarContact(ModuleRadar radar, TargetSignatureData contactData, bool _locked)
        {
            bool addContact = true;

            RadarDisplayData rData = new RadarDisplayData();
            rData.vessel = contactData.vessel;

            if (rData.vessel == vessel) return;
                                    
            if (rData.vessel.altitude < -20 && radar.rwrThreatType != (int)RadarWarningReceiver.RWRThreatTypes.Sonar) addContact = false; // Normal Radar Should not detect Underwater vessels
            if (!rData.vessel.LandedOrSplashed && radar.rwrThreatType == (int)RadarWarningReceiver.RWRThreatTypes.Sonar) addContact = false; //Sonar should not detect Aircraft
            if (rData.vessel.altitude < 0 && radar.rwrThreatType == (int)RadarWarningReceiver.RWRThreatTypes.Sonar && vessel.Splashed) addContact = true; //Sonar only detects underwater vessels // Sonar should only work when in the water
            if (!vessel.Splashed && radar.rwrThreatType == (int)RadarWarningReceiver.RWRThreatTypes.Sonar) addContact = false; // Sonar should only work when in the water
            if (rData.vessel.Landed && radar.rwrThreatType == (int)RadarWarningReceiver.RWRThreatTypes.Sonar) addContact = false; //Sonar should not detect land vessels

            if (addContact == false) return;
            
            rData.signalPersistTime = radar.signalPersistTime;
            rData.detectedByRadar = radar;
            rData.locked = _locked;
            rData.targetData = contactData;
            rData.pingPosition = UpdatedPingPosition(contactData.position, radar);

            if (_locked)
            {
                radar.UpdateLockedTargetInfo(contactData);
            }

            bool dontOverwrite = false;

            int replaceIndex = -1;
            for (int i = 0; i < displayedTargets.Count; i++)
            {
                if (displayedTargets[i].vessel == rData.vessel)
                {
                    if (displayedTargets[i].locked && !_locked)
                    {
                        dontOverwrite = true;
                        break;
                    }

                    replaceIndex = i;
                    break;
                }
            }

            if (replaceIndex >= 0)
            {
                displayedTargets[replaceIndex] = rData;
                //UpdateLockedTargets();
                return;
            }
            else if (dontOverwrite)
            {
                //UpdateLockedTargets();
                return;
            }
            else
            {
                displayedTargets.Add(rData);
                UpdateLockedTargets();
                return;
            }
        }

        public void UnlockAllTargetsOfRadar(ModuleRadar radar)
        {
            //radar.UnlockTarget();
            displayedTargets.RemoveAll(t => t.detectedByRadar == radar);
            UpdateLockedTargets();
        }

        public void RemoveVesselFromTargets(Vessel _vessel)
        {
            displayedTargets.RemoveAll(t => t.vessel == _vessel);
            UpdateLockedTargets();
        }

        public void UnlockAllTargets()
        {
            List<ModuleRadar>.Enumerator radar = weaponManager.radars.GetEnumerator();
            while (radar.MoveNext())
            {
                if (radar.Current == null) continue;
                radar.Current.UnlockAllTargets();
            }
            radar.Dispose();
        }

        public void UnlockCurrentTarget()
        {
            if (!locked) return;

            ModuleRadar rad = displayedTargets[lockedTargetIndexes[activeLockedTargetIndex]].detectedByRadar;
            rad.UnlockTargetAt(rad.currentLockIndex);
        }

        void CleanDisplayedContacts()
        {
            int count = displayedTargets.Count;
            displayedTargets.RemoveAll(t => t.targetData.age > t.signalPersistTime*2);
            if (count != displayedTargets.Count)
            {
                UpdateLockedTargets();
            }
        }

        Vector2 UpdatedPingPosition(Vector3 worldPosition, ModuleRadar radar)
        {
            if (omniDisplay)
            {
                return RadarUtils.WorldToRadar(worldPosition, referenceTransform, radarRect, rIncrements[rangeIndex]);
            }
            else
            {
                return RadarUtils.WorldToRadarRadial(worldPosition, referenceTransform, radarRect,
                    rIncrements[rangeIndex], radar.directionalFieldOfView/2);
            }
        }

        bool pingPositionsDirty = true;

        void DrawDisplayedContacts()
        {
            float myAlt = (float) vessel.altitude;

            bool drewLockLabel = false;

            bool lockDirty = false;

            for (int i = 0; i < displayedTargets.Count; i++)
            {
                if (displayedTargets[i].locked && locked)
                {
                    TargetSignatureData lockedTarget = displayedTargets[i].targetData;
                    //LOCKED GUI
                    Vector2 pingPosition;
                    if (omniDisplay)
                    {
                        pingPosition = RadarUtils.WorldToRadar(lockedTarget.position, referenceTransform, radarRect,
                            rIncrements[rangeIndex]);
                    }
                    else
                    {
                        pingPosition = RadarUtils.WorldToRadarRadial(lockedTarget.position, referenceTransform,
                            radarRect, rIncrements[rangeIndex],
                            displayedTargets[i].detectedByRadar.directionalFieldOfView/2);
                    }

                    //BDGUIUtils.DrawRectangle(new Rect(pingPosition.x-(4),pingPosition.y-(4),8, 8), Color.green);
                    float vAngle = Vector3.Angle(Vector3.ProjectOnPlane(lockedTarget.velocity, referenceTransform.up),
                        referenceTransform.forward);
                    if (referenceTransform.InverseTransformVector(lockedTarget.velocity).x < 0)
                    {
                        vAngle = -vAngle;
                    }
                    GUIUtility.RotateAroundPivot(vAngle, pingPosition);
                    Rect pingRect = new Rect(pingPosition.x - (lockIconSize/2), pingPosition.y - (lockIconSize/2),
                        lockIconSize, lockIconSize);


                    Texture2D txtr = (i == lockedTargetIndexes[activeLockedTargetIndex]) ? lockIconActive : lockIcon;
                    GUI.DrawTexture(pingRect, txtr, ScaleMode.StretchToFill, true);
                    GUI.matrix = Matrix4x4.identity;
                    GUI.Label(new Rect(pingPosition.x + (lockIconSize*0.35f) + 2, pingPosition.y, 100, 24),
                        (lockedTarget.altitude/1000).ToString("0"), distanceStyle);


                    if (!drewLockLabel)
                    {
                        GUI.Label(radarRect, "-LOCK-\n", lockStyle);
                        drewLockLabel = true;

                        if (slaveTurrets)
                        {
                            GUI.Label(radarRect, "TURRETS\n\n", lockStyle);
                        }
                    }

                    if (BDArmorySettings.DRAW_DEBUG_LABELS)
                    {
                        GUI.Label(new Rect(pingPosition.x + (pingSize.x/2), pingPosition.y, 100, 24),
                            lockedTarget.signalStrength.ToString("0.0"));
                    }

                    if (GUI.Button(pingRect, GUIContent.none, GUIStyle.none) &&
                        Time.time - guiInputTime > guiInputCooldown)
                    {
                        guiInputTime = Time.time;
                        if (i == lockedTargetIndexes[activeLockedTargetIndex])
                        {
                            //UnlockTarget(displayedTargets[i].detectedByRadar);
                            //displayedTargets[i].detectedByRadar.UnlockTargetAtPosition(displayedTargets[i].targetData.position);
                            displayedTargets[i].detectedByRadar.UnlockTargetVessel(displayedTargets[i].vessel);
                            UpdateLockedTargets();
                            lockDirty = true;
                        }
                        else
                        {
                            for (int x = 0; x < lockedTargetIndexes.Count; x++)
                            {
                                if (i == lockedTargetIndexes[x])
                                {
                                    activeLockedTargetIndex = x;
                                    break;
                                }
                            }

                            displayedTargets[i].detectedByRadar.SetActiveLock(displayedTargets[i].targetData);

                            UpdateLockedTargets();
                        }
                    }


                    //DLZ
                    if (!lockDirty)
                    {
                        int lTarInd = lockedTargetIndexes[activeLockedTargetIndex];

						if(i == lTarInd && weaponManager && weaponManager.selectedWeapon != null)
						{
							if(weaponManager.selectedWeapon.GetWeaponClass() == WeaponClasses.Missile || weaponManager.selectedWeapon.GetWeaponClass() == WeaponClasses.SLW)
							{
								MissileBase currMissile = weaponManager.CurrentMissile;
								if(currMissile.TargetingMode == MissileBase.TargetingModes.Radar || currMissile.TargetingMode == MissileBase.TargetingModes.Heat)
								{
									MissileLaunchParams dlz = MissileLaunchParams.GetDynamicLaunchParams(currMissile, lockedTarget.velocity, lockedTarget.predictedPosition);
									float rangeToPixels = (1 / rIncrements[rangeIndex]) * radarRect.height;
									float dlzWidth = 12;
									float lineWidth = 2;
									float dlzX = radarRect.width - dlzWidth - lineWidth;

                                    BDGUIUtils.DrawRectangle(new Rect(dlzX, 0, dlzWidth, radarRect.height), Color.black);

                                    Rect maxRangeVertLineRect = new Rect(radarRect.width - lineWidth,
                                        Mathf.Clamp(radarRect.height - (dlz.maxLaunchRange*rangeToPixels), 0,
                                            radarRect.height), lineWidth,
                                        Mathf.Clamp(dlz.maxLaunchRange*rangeToPixels, 0, radarRect.height));
                                    BDGUIUtils.DrawRectangle(maxRangeVertLineRect, Color.green);

                                    Rect maxRangeTickRect = new Rect(dlzX, maxRangeVertLineRect.y, dlzWidth, lineWidth);
                                    BDGUIUtils.DrawRectangle(maxRangeTickRect, Color.green);

                                    Rect minRangeTickRect = new Rect(dlzX,
                                        Mathf.Clamp(radarRect.height - (dlz.minLaunchRange*rangeToPixels), 0,
                                            radarRect.height), dlzWidth, lineWidth);
                                    BDGUIUtils.DrawRectangle(minRangeTickRect, Color.green);

                                    Rect rTrTickRect = new Rect(dlzX,
                                        Mathf.Clamp(radarRect.height - (dlz.rangeTr*rangeToPixels), 0, radarRect.height),
                                        dlzWidth, lineWidth);
                                    BDGUIUtils.DrawRectangle(rTrTickRect, Color.green);

                                    Rect noEscapeLineRect = new Rect(dlzX, rTrTickRect.y, lineWidth,
                                        minRangeTickRect.y - rTrTickRect.y);
                                    BDGUIUtils.DrawRectangle(noEscapeLineRect, Color.green);

                                    float targetDistIconSize = 16;
                                    float targetDistY;
                                    if (!omniDisplay)
                                    {
                                        targetDistY = pingPosition.y - (targetDistIconSize/2);
                                    }
                                    else
                                    {
                                        targetDistY = radarRect.height -
                                                      (Vector3.Distance(lockedTarget.predictedPosition,
                                                           referenceTransform.position)*rangeToPixels) -
                                                      (targetDistIconSize/2);
                                    }

                                    Rect targetDistanceRect = new Rect(dlzX - (targetDistIconSize/2), targetDistY,
                                        targetDistIconSize, targetDistIconSize);
                                    GUIUtility.RotateAroundPivot(90, targetDistanceRect.center);
                                    GUI.DrawTexture(targetDistanceRect, BDArmorySettings.Instance.directionTriangleIcon,
                                        ScaleMode.StretchToFill, true);
                                    GUI.matrix = Matrix4x4.identity;
                                }
                            }
                        }
                    }
                }
                else
                {
                    float minusAlpha =
                    (Mathf.Clamp01((Time.time - displayedTargets[i].targetData.timeAcquired)/
                                   displayedTargets[i].signalPersistTime)*2) - 1;

                    //jamming
                    // NEW: evaluation via radarutils!
                    bool jammed = false;
                    float distanceToTarget = (this.vessel.transform.position - displayedTargets[i].targetData.position).sqrMagnitude;
                    float jamDistance = RadarUtils.GetVesselECMJammingDistance(displayedTargets[i].targetData.vessel);
                    if (displayedTargets[i].targetData.vesselJammer && jamDistance*jamDistance > distanceToTarget)
                    {
                        jammed = true;
                    }

                    if (pingPositionsDirty)
                    {
                        //displayedTargets[i].pingPosition = UpdatedPingPosition(displayedTargets[i].targetData.position, displayedTargets[i].detectedByRadar);
                        RadarDisplayData newData = new RadarDisplayData();
                        newData.detectedByRadar = displayedTargets[i].detectedByRadar;
                        newData.locked = displayedTargets[i].locked;
                        newData.pingPosition = UpdatedPingPosition(displayedTargets[i].targetData.position,
                            displayedTargets[i].detectedByRadar);
                        newData.signalPersistTime = displayedTargets[i].signalPersistTime;
                        newData.targetData = displayedTargets[i].targetData;
                        newData.vessel = displayedTargets[i].vessel;
                        displayedTargets[i] = newData;
                    }
                    Vector2 pingPosition = displayedTargets[i].pingPosition;

                    Rect pingRect;
                    //draw missiles and debris as dots
                    if ((displayedTargets[i].targetData.targetInfo &&
                         displayedTargets[i].targetData.targetInfo.isMissile) ||
                        displayedTargets[i].targetData.team == BDArmorySettings.BDATeams.None)
                    {
                        float mDotSize = 6;
                        pingRect = new Rect(pingPosition.x - (mDotSize/2), pingPosition.y - (mDotSize/2), mDotSize,
                            mDotSize);
                        Color origGUIColor = GUI.color;
                        GUI.color = Color.white - new Color(0, 0, 0, minusAlpha);
                        GUI.DrawTexture(pingRect, BDArmorySettings.Instance.greenDotTexture, ScaleMode.StretchToFill,
                            true);
                        GUI.color = origGUIColor;
                    }
                    //draw contacts with direction indicator
                    else if (!jammed && (displayedTargets[i].detectedByRadar.showDirectionWhileScan) &&
                             displayedTargets[i].targetData.velocity.sqrMagnitude > 100)
                    {
                        pingRect = new Rect(pingPosition.x - (lockIconSize/2), pingPosition.y - (lockIconSize/2),
                            lockIconSize, lockIconSize);
                        float vAngle =
                            Vector3.Angle(
                                Vector3.ProjectOnPlane(displayedTargets[i].targetData.velocity, referenceTransform.up),
                                referenceTransform.forward);
                        if (referenceTransform.InverseTransformVector(displayedTargets[i].targetData.velocity).x < 0)
                        {
                            vAngle = -vAngle;
                        }
                        GUIUtility.RotateAroundPivot(vAngle, pingPosition);
                        Color origGUIColor = GUI.color;
                        GUI.color = Color.white - new Color(0, 0, 0, minusAlpha);
                        if (weaponManager &&
                            displayedTargets[i].targetData.team == BDATargetManager.BoolToTeam(weaponManager.team))
                        {
                            GUI.DrawTexture(pingRect, friendlyContactIcon, ScaleMode.StretchToFill, true);
                        }
                        else
                        {
                            GUI.DrawTexture(pingRect, radarContactIcon, ScaleMode.StretchToFill, true);
                        }

                        GUI.matrix = Matrix4x4.identity;
                        GUI.Label(new Rect(pingPosition.x + (lockIconSize*0.35f) + 2, pingPosition.y, 100, 24),
                            (displayedTargets[i].targetData.altitude/1000).ToString("0"), distanceStyle);
                        GUI.color = origGUIColor;
                    }
                    else //draw contacts as rectangles
                    {
                        int drawCount = jammed ? 4 : 1;
                        pingRect = new Rect(pingPosition.x - (pingSize.x/2), pingPosition.y - (pingSize.y/2), pingSize.x,
                            pingSize.y);
                        for (int d = 0; d < drawCount; d++)
                        {
                            Rect jammedRect = new Rect(pingRect);
                            Vector3 contactPosition = displayedTargets[i].targetData.position;
                            if (jammed)
                            {
                                //jamming
                                Vector3 jammedPosition = transform.position +
                                                         ((displayedTargets[i].targetData.position - transform.position)
                                                              .normalized*
                                                          Random.Range(100, rIncrements[rangeIndex]));
                                float bearingVariation =
                                    Mathf.Clamp(
                                        Mathf.Pow(32000, 2)/
                                        (displayedTargets[i].targetData.position - transform.position).sqrMagnitude, 0,
                                        80);
                                jammedPosition = transform.position +
                                                 (Quaternion.AngleAxis(
                                                      Random.Range(-bearingVariation, bearingVariation),
                                                      referenceTransform.up)*(jammedPosition - transform.position));
                                if (omniDisplay)
                                {
                                    pingPosition = RadarUtils.WorldToRadar(jammedPosition, referenceTransform, radarRect,
                                        rIncrements[rangeIndex]);
                                }
                                else
                                {
                                    pingPosition = RadarUtils.WorldToRadarRadial(jammedPosition, referenceTransform,
                                        radarRect, rIncrements[rangeIndex],
                                        displayedTargets[i].detectedByRadar.directionalFieldOfView/2);
                                }

                                jammedRect = new Rect(pingPosition.x - (pingSize.x/2),
                                    pingPosition.y - (pingSize.y/2) - (pingSize.y/3), pingSize.x, pingSize.y/3);
                                contactPosition = jammedPosition;
                            }

                            Color iconColor = Color.green;
                            float contactAlt = displayedTargets[i].targetData.altitude;
                            if (!omniDisplay && !jammed)
                            {
                                if (contactAlt - myAlt > 1000)
                                {
                                    iconColor = new Color(0, 0.6f, 1f, 1);
                                }
                                else if (contactAlt - myAlt < -1000)
                                {
                                    iconColor = new Color(1f, 0.68f, 0, 1);
                                }
                            }

                            if (omniDisplay)
                            {
                                Vector3 localPos = referenceTransform.InverseTransformPoint(contactPosition);
                                localPos.y = 0;
                                float angleToContact = Vector3.Angle(localPos, Vector3.forward);
                                if (localPos.x < 0) angleToContact = -angleToContact;
                                GUIUtility.RotateAroundPivot(angleToContact, pingPosition);
                            }

                            if (jammed ||
                                displayedTargets[i].targetData.team != BDATargetManager.BoolToTeam(weaponManager.team))
                            {
                                BDGUIUtils.DrawRectangle(jammedRect, iconColor - new Color(0, 0, 0, minusAlpha));
                            }
                            else
                            {
                                float friendlySize = 12;
                                Rect friendlyRect = new Rect(pingPosition.x - (friendlySize/2),
                                    pingPosition.y - (friendlySize/2), friendlySize, friendlySize);
                                Color origGuiColor = GUI.color;
                                GUI.color = iconColor - new Color(0, 0, 0, minusAlpha);
                                GUI.DrawTexture(friendlyRect, BDArmorySettings.Instance.greenDotTexture,
                                    ScaleMode.StretchToFill, true);
                                GUI.color = origGuiColor;
                            }

                            GUI.matrix = Matrix4x4.identity;
                        }
                    }


                    if (GUI.Button(pingRect, GUIContent.none, GUIStyle.none) &&
                        Time.time - guiInputTime > guiInputCooldown)
                    {
                        guiInputTime = Time.time;
                        TryLockTarget(displayedTargets[i]);
                    }

                    if (BDArmorySettings.DRAW_DEBUG_LABELS)
                    {
                        GUI.Label(new Rect(pingPosition.x + (pingSize.x/2), pingPosition.y, 100, 24),
                            displayedTargets[i].targetData.signalStrength.ToString("0.0"));
                    }
                }
            }
            pingPositionsDirty = false;
        }

        bool omniDisplay
        {
            get { return (radarCount > 1 || (radarCount == 1 && availableRadars[0].omnidirectional)); }
        }

        void UpdateInputs()
        {
            if (!vessel.isActiveVessel)
            {
                return;
            }


            if (BDInputUtils.GetKey(BDInputSettingsFields.RADAR_SLEW_RIGHT))
            {
                ShowSelector();
                SlewSelector(Vector2.right);
            }
            else if (BDInputUtils.GetKey(BDInputSettingsFields.RADAR_SLEW_LEFT))
            {
                ShowSelector();
                SlewSelector(-Vector2.right);
            }

            if (BDInputUtils.GetKey(BDInputSettingsFields.RADAR_SLEW_UP))
            {
                ShowSelector();
                SlewSelector(-Vector2.up);
            }
            else if (BDInputUtils.GetKey(BDInputSettingsFields.RADAR_SLEW_DOWN))
            {
                ShowSelector();
                SlewSelector(Vector2.up);
            }

            if (BDInputUtils.GetKeyDown(BDInputSettingsFields.RADAR_LOCK))
            {
                if (showSelector)
                {
                    TryLockViaSelector();
                }
                ShowSelector();
            }

            if (BDInputUtils.GetKeyDown(BDInputSettingsFields.RADAR_CYCLE_LOCK))
            {
                if (locked)
                {
                    CycleActiveLock();
                }
            }

            if (BDInputUtils.GetKeyDown(BDInputSettingsFields.RADAR_SCAN_MODE))
            {
                if (!locked && radarCount > 0 && !omniDisplay)
                {
                    availableRadars[0].boresightScan = !availableRadars[0].boresightScan;
                }
            }

            if (BDInputUtils.GetKeyDown(BDInputSettingsFields.RADAR_TURRETS))
            {
                if (slaveTurrets)
                {
                    UnslaveTurrets();
                }
                else
                {
                    SlaveTurrets();
                }
            }

            if (BDInputUtils.GetKeyDown(BDInputSettingsFields.RADAR_RANGE_UP))
            {
                IncreaseRange();
            }
            else if (BDInputUtils.GetKeyDown(BDInputSettingsFields.RADAR_RANGE_DN))
            {
                DecreaseRange();
            }
        }

        void TryLockViaSelector()
        {
            bool found = false;
            Vector3 closestPos = Vector3.zero;
            float closestSqrMag = float.MaxValue;
            for (int i = 0; i < displayedTargets.Count; i++)
            {
                float sqrMag = (displayedTargets[i].pingPosition - selectorPos).sqrMagnitude;
                if (sqrMag < closestSqrMag)
                {
                    if (sqrMag < Mathf.Pow(20, 2))
                    {
                        closestPos = displayedTargets[i].targetData.predictedPosition;
                        found = true;
                    }
                }
            }

            if (found)
            {
                TryLockTarget(closestPos);
            }
            else if (closestSqrMag > Mathf.Pow(40, 2))
            {
                UnlockCurrentTarget();
            }
        }

        void SlewSelector(Vector2 direction)
        {
            float rate = 150;
            selectorPos += direction*rate*Time.deltaTime;

            if (!omniDisplay)
            {
                if (selectorPos.y > radarScreenSize*0.975f)
                {
                    if (rangeIndex > 0)
                    {
                        DecreaseRange();
                        selectorPos.y = radarScreenSize*0.75f;
                    }
                }
                else if (selectorPos.y < radarScreenSize*0.025f)
                {
                    if (rangeIndex < rIncrements.Length - 1)
                    {
                        IncreaseRange();
                        selectorPos.y = radarScreenSize*0.25f;
                    }
                }
            }

            selectorPos.y = Mathf.Clamp(selectorPos.y, 10, radarScreenSize - 10);
            selectorPos.x = Mathf.Clamp(selectorPos.x, 10, radarScreenSize - 10);
        }

        void ShowSelector()
        {
            if (!showSelector)
            {
                showSelector = true;
                selectorPos = new Vector2(radarScreenSize/2, radarScreenSize/2);
            }
        }
    }
}