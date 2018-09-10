using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using BDArmory.Core;
using BDArmory.Misc;
using BDArmory.Radar;
using BDArmory.Targeting;
using BDArmory.UI;
using UnityEngine;

namespace BDArmory.Modules
{
    public class ModuleRadar : PartModule
    {

        #region KSPFields (Part Configuration)

        #region General Configuration  	
        [KSPField]
        public string radarName;

        [KSPField]
        public int turretID = 0;

        [KSPField]
        public string rotationTransformName = string.Empty;
        Transform rotationTransform;
        #endregion


        #region Radar Capabilities
        [KSPField]
        public int rwrThreatType = 0;               //IMPORTANT, configures which type of radar it will show up as on the RWR		
        public RadarWarningReceiver.RWRThreatTypes rwrType = RadarWarningReceiver.RWRThreatTypes.SAM;

        [KSPField]
        public double resourceDrain = 0.825;        //resource (EC/sec) usage of active radar

        [KSPField]
        public bool omnidirectional = true;			//false=boresight only

        [KSPField]
        public float directionalFieldOfView = 90;	//relevant for omnidirectional only

        [KSPField]
        public float boresightFOV = 10;				//relevant for boresight only

        [KSPField]
        public float scanRotationSpeed = 120; 		//in degrees per second, relevant for omni and directional

        [KSPField]
        public float lockRotationSpeed = 120;		//in degrees per second, relevant for omni only

        [KSPField]
        public float lockRotationAngle = 4;         //???

        [KSPField]
        public bool showDirectionWhileScan = false; //radar can show direction indicator of contacts (false: can show contacts as blocks only)

        [KSPField]
        public float multiLockFOV = 30;             //??

        [KSPField]
        public float lockAttemptFOV = 2;            //??

        [KSPField]
        public bool canScan = true;                 //radar has detection capabilities

        [KSPField]
        public bool canLock = true;					//radar has locking/tracking capabilities

        [KSPField]
        public int maxLocks = 1;					//how many targets can be locked/tracked simultaneously

        [KSPField]
        public bool canTrackWhileScan = false;      //when tracking/locking, can we still detect/scan?

        [KSPField]
        public bool canRecieveRadarData = false;    //can radar data be received from friendly sources?		

        [KSPField]
        public FloatCurve radarDetectionCurve = new FloatCurve();		//FloatCurve defining at what range which RCS size can be detected

        [KSPField]
        public FloatCurve radarLockTrackCurve = new FloatCurve();		//FloatCurve defining at what range which RCS size can be locked/tracked        

        [KSPField]
        public float radarGroundClutterFactor = 0.25f; //Factor defining how effective the radar is for look-down, compensating for ground clutter (0=ineffective, 1=fully effective) 
                                                       //default to 0.25, so all cross sections of landed/splashed/submerged vessels are reduced to 1/4th, as these vessel usually a quite large
        #endregion


        #region Persisted State in flight
        [KSPField(isPersistant = true)]
        public string linkedVesselID;

        [KSPField(isPersistant = true)]
        public bool radarEnabled;

        [KSPField(isPersistant = true)]
        public int rangeIndex = 99;

        [KSPField(isPersistant = true)]
        public float currentAngle;
        #endregion


        #region DEPRECATED! ->see Radar Capabilities section for new detectionCurve + trackingCurve
        [Obsolete]
        [KSPField]
        public float minSignalThreshold = 90;

        [Obsolete]
        [KSPField]
        public float minLockedSignalThreshold = 90;
        #endregion


        #endregion

        #region KSP Events & Actions
        [KSPAction("Toggle Radar")]
        public void AGEnable(KSPActionParam param)
        {
            if (radarEnabled)
            {
                DisableRadar();
            }
            else
            {
                EnableRadar();
            }
        }

        [KSPEvent(active = true, guiActive = true, guiActiveEditor = false, guiName = "Toggle Radar")]
        public void Toggle()
        {
            if (radarEnabled)
            {
                DisableRadar();
            }
            else
            {
                EnableRadar();
            }
        }

        [KSPAction("Target Next")]
        public void TargetNext(KSPActionParam param)
        {
            vesselRadarData.TargetNext();
        }

        [KSPAction("Target Prev")]
        public void TargetPrev(KSPActionParam param)
        {
            vesselRadarData.TargetPrev();
        }

        #endregion

        #region Part members
        //locks
        [KSPField(isPersistant = false, guiActive = true, guiActiveEditor = false, guiName = "Current Locks")]
        public int currLocks;
        public bool locked
        {
            get { return currLocks > 0; }
        }
        public int currentLocks
        {
            get { return currLocks; }
        }

        private TargetSignatureData[] attemptedLocks;
        private List<TargetSignatureData> lockedTargets;
        public TargetSignatureData lockedTarget
        {
            get
            {
                if (currLocks == 0) return TargetSignatureData.noTarget;
                else
                {
                    return lockedTargets[lockedTargetIndex];
                }
            }
        }

        private int lockedTargetIndex;
        public int currentLockIndex
        {
            get { return lockedTargetIndex; }
        }

        public float radarMinDistanceDetect
        {
            get { return radarDetectionCurve.minTime; }
        }
        //[KSPField(isPersistant = false, guiActive = true, guiActiveEditor = true, guiName = "Detection Range")]
        public float radarMaxDistanceDetect
        {
            get { return radarDetectionCurve.maxTime; }
        }
        public float radarMinDistanceLockTrack
        {
            get { return radarLockTrackCurve.minTime; }
        }
        //[KSPField(isPersistant = false, guiActive = true, guiActiveEditor = true, guiName = "Locking Range")]
        public float radarMaxDistanceLockTrack
        {
            get { return radarLockTrackCurve.maxTime; }
        }

        //linked vessels
        private List<VesselRadarData> linkedToVessels;
        public List<ModuleRadar> availableRadarLinks;
        private bool unlinkOnDestroy = true;
        
        //GUI
        private bool drawGUI;
        public float signalPersistTime;
        public float signalPersistTimeForRwr;

        //scanning
        private float currentAngleLock;
        public Transform referenceTransform;
        private float radialScanDirection = 1;
        private float lockScanDirection = 1;

        public bool boresightScan;

        //locking
        public float lockScanAngle;
        public bool slaveTurrets;
        public ModuleTurret lockingTurret;
        public bool lockingPitch = true;
        public bool lockingYaw = true;

        //vessel
        private MissileFire wpmr;
        public MissileFire weaponManager
        {
            get
            {
                if (wpmr != null && wpmr.vessel == vessel) return wpmr;
                wpmr = null;
                List<MissileFire>.Enumerator mf = vessel.FindPartModulesImplementing<MissileFire>().GetEnumerator();
                while (mf.MoveNext())
                {
                    if (mf.Current == null) continue;
                    wpmr = mf.Current;
                }
                mf.Dispose();
                return wpmr;
            }
            set { wpmr = value; }
        }
        public VesselRadarData vesselRadarData;
        private string myVesselID;
        
        // part state
        private bool startupComplete;
        public float leftLimit;
        public float rightLimit;
        private int snapshotTicker;
        #endregion       
        

        void UpdateToggleGuiName()
        {
            Events["Toggle"].guiName = radarEnabled ? "Disable Radar" : "Enable Radar";
        }
        
        public void EnsureVesselRadarData()
        {
            if (vessel == null) return;
            //myVesselID = vessel.id.ToString();
            
            if (vesselRadarData != null && vesselRadarData.vessel == vessel) return;
            vesselRadarData = vessel.gameObject.GetComponent<VesselRadarData>();

            if (vesselRadarData == null)
            {
	            vesselRadarData = vessel.gameObject.AddComponent<VesselRadarData>();
    	        vesselRadarData.weaponManager = weaponManager;
            }
        }
        
        public void EnableRadar()
        {
            EnsureVesselRadarData();
            radarEnabled = true;
            
            List<MissileFire>.Enumerator mf = vessel.FindPartModulesImplementing<MissileFire>().GetEnumerator();
            while (mf.MoveNext())
            {
                if (mf.Current == null) continue;
                weaponManager = mf.Current;
                if (vesselRadarData)
                {
                    vesselRadarData.weaponManager = mf.Current;
                }
                break;
            }
            mf.Dispose();
            
            UpdateToggleGuiName();
            vesselRadarData.AddRadar(this);
        }
        
        public void DisableRadar()
        {
            if (locked)
            {
                UnlockAllTargets();
            }

            radarEnabled = false;
            UpdateToggleGuiName();

            if (vesselRadarData)
            {
                vesselRadarData.RemoveRadar(this);
            }
            
            List<VesselRadarData>.Enumerator vrd = linkedToVessels.GetEnumerator();
            while (vrd.MoveNext())
            {
                if (vrd.Current == null) continue;
                vrd.Current.UnlinkDisabledRadar(this);
            }
            vrd.Dispose();
        }

        void OnDestroy()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (vesselRadarData)
                {
                    vesselRadarData.RemoveRadar(this);
                    vesselRadarData.RemoveDataFromRadar(this);
                }

                if (linkedToVessels != null)
                {
                    List<VesselRadarData>.Enumerator vrd = linkedToVessels.GetEnumerator();
                    while (vrd.MoveNext())
                    {
                        if (vrd.Current == null) continue;
                        if (unlinkOnDestroy)
                        {
                            vrd.Current.UnlinkDisabledRadar(this);
                        }
                        else
                        {
                            vrd.Current.BeginWaitForUnloadedLinkedRadar(this, myVesselID);
                        }
                    }
                    vrd.Dispose();
                }
            }
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            if (HighLogic.LoadedSceneIsFlight)
            {
                myVesselID = vessel.id.ToString();
                RadarUtils.SetupResources();

                if (string.IsNullOrEmpty(radarName))
                {
                    radarName = part.partInfo.title;
                }

                linkedToVessels = new List<VesselRadarData>();
                
                signalPersistTime = omnidirectional
                    ? 360/(scanRotationSpeed + 5)
                    : directionalFieldOfView/(scanRotationSpeed + 5);

                rwrType = (RadarWarningReceiver.RWRThreatTypes)rwrThreatType;
                if (rwrType == RadarWarningReceiver.RWRThreatTypes.Sonar)
                    signalPersistTimeForRwr = RadarUtils.ACTIVE_MISSILE_PING_PERISTS_TIME;
                else
                    signalPersistTimeForRwr = signalPersistTime / 2;


                if (rotationTransformName != string.Empty)
                {
                    rotationTransform = part.FindModelTransform(rotationTransformName);
                }

                attemptedLocks = new TargetSignatureData[3];
                TargetSignatureData.ResetTSDArray(ref attemptedLocks);
                lockedTargets = new List<TargetSignatureData>();               
               
                referenceTransform = (new GameObject()).transform;
                referenceTransform.parent = transform;
                referenceTransform.localPosition = Vector3.zero;

                List<ModuleTurret>.Enumerator turr = part.FindModulesImplementing<ModuleTurret>().GetEnumerator();
                while (turr.MoveNext())
                {
                    if (turr.Current == null) continue;
                    if (turr.Current.turretID != turretID) continue;
                    lockingTurret = turr.Current;
                    break;
                }
                turr.Dispose();

                //GameEvents.onVesselGoOnRails.Add(OnGoOnRails);    //not needed
                EnsureVesselRadarData();
                StartCoroutine(StartUpRoutine());
            }
            else if (HighLogic.LoadedSceneIsEditor) 
            {         
	            //Editor only:
	            List<ModuleTurret>.Enumerator tur = part.FindModulesImplementing<ModuleTurret>().GetEnumerator();
	            while (tur.MoveNext())
	            {
	                if (tur.Current == null) continue;
	                if (tur.Current.turretID != turretID) continue;
	                lockingTurret = tur.Current;
	                break;
	            }
	            tur.Dispose();
                if (lockingTurret)
                {
                    lockingTurret.Fields["minPitch"].guiActiveEditor = false;
                    lockingTurret.Fields["maxPitch"].guiActiveEditor = false;
                    lockingTurret.Fields["yawRange"].guiActiveEditor = false;
                }
            }

            // check for not updated legacy part:
            if ((canScan && (radarMinDistanceDetect == float.MaxValue)) || (canLock && (radarMinDistanceLockTrack == float.MaxValue)))
            {
                Debug.Log("[BDArmory]: WARNING: " + part.name + " has legacy definition, missing new radarDetectionCurve and radarLockTrackCurve definitions! Please update for the part to be usable!");
            }
        }
        /*
        void OnGoOnRails(Vessel v)
        {
            if (v != vessel) return;
            unlinkOnDestroy = false;
            //myVesselID = vessel.id.ToString();
        }
        */
        
        IEnumerator StartUpRoutine()
        {
            if (BDArmorySettings.DRAW_DEBUG_LABELS)
                Debug.Log("[BDArmory]: StartupRoutine: " + radarName + " enabled: " + radarEnabled);
            while (!FlightGlobals.ready || vessel.packed)
            {
                yield return null;
            }

            yield return new WaitForFixedUpdate();

            if (radarEnabled)
            {
                EnableRadar();
            }

            yield return null;

            if (!vesselRadarData.hasLoadedExternalVRDs)
            {
                RecoverLinkedVessels();
                vesselRadarData.hasLoadedExternalVRDs = true;
            }

            UpdateToggleGuiName();
            startupComplete = true;
        }
                
        void Update()
        {
            if (HighLogic.LoadedSceneIsFlight && FlightGlobals.ready && !vessel.packed && radarEnabled)
            {
                if (omnidirectional)
                {
                    referenceTransform.position = vessel.transform.position;
                    referenceTransform.rotation =
                        Quaternion.LookRotation(VectorUtils.GetNorthVector(transform.position, vessel.mainBody),
                            VectorUtils.GetUpDirection(transform.position));
                }
                else
                {
                    referenceTransform.position = vessel.transform.position;
                    referenceTransform.rotation = Quaternion.LookRotation(part.transform.up,
                        VectorUtils.GetUpDirection(referenceTransform.position));
                }
                //UpdateInputs();
            }

            drawGUI = (HighLogic.LoadedSceneIsFlight && FlightGlobals.ready && !vessel.packed && radarEnabled &&
                       vessel.isActiveVessel && BDArmorySetup.GAME_UI_ENABLED && !MapView.MapIsEnabled);
        }
               
        void FixedUpdate()
        {
            if (HighLogic.LoadedSceneIsFlight && FlightGlobals.ready && startupComplete)
            {
                if (!vessel.IsControllable && radarEnabled)
                {
                    DisableRadar();
                }

                if (radarEnabled)
                {
                    DrainElectricity(); //physics behaviour, thus moved here from update

                    if (locked)
                    {
                        for (int i = 0; i < lockedTargets.Count; i++)
                        {
                            UpdateLock(i);
                        }

                        if (canTrackWhileScan)
                        {
                            Scan();
                        }
                    }
                    else if (boresightScan)
                    {
                        BoresightScan();
                    }
                    else if (canScan)
                    {
                        Scan();
                    }
                }
            }

        }
                
        void UpdateSlaveData()
        {
            if (slaveTurrets && weaponManager)
            {
                weaponManager.slavingTurrets = true;
                if (locked)
                {
                    weaponManager.slavedPosition = lockedTarget.predictedPosition;
                    weaponManager.slavedVelocity = lockedTarget.velocity;
                    weaponManager.slavedAcceleration = lockedTarget.acceleration;
                    weaponManager.slavedTarget = lockedTarget;
                }
            }
        }
                
        void LateUpdate()
        {
            if (HighLogic.LoadedSceneIsFlight && (canScan || canLock))
            {
                UpdateModel();
            }
        }
                
        void UpdateModel()
        {
            //model rotation
            if (radarEnabled)
            {
                if (rotationTransform && canScan)
                {
                    Vector3 direction;
                    if (locked)
                    {
                        direction =
                            Quaternion.AngleAxis(canTrackWhileScan ? currentAngle : lockScanAngle, referenceTransform.up)*
                            referenceTransform.forward;
                    }
                    else
                    {
                        direction = Quaternion.AngleAxis(currentAngle, referenceTransform.up)*referenceTransform.forward;
                    }

                    Vector3 localDirection =
                        Vector3.ProjectOnPlane(rotationTransform.parent.InverseTransformDirection(direction), Vector3.up);
                    if (localDirection != Vector3.zero)
                    {
                        rotationTransform.localRotation = Quaternion.Lerp(rotationTransform.localRotation,
                            Quaternion.LookRotation(localDirection, Vector3.up), 10*TimeWarp.fixedDeltaTime);
                    }
                }

                //lock turret
                if (lockingTurret && canLock)
                {
                    if (locked)
                    {
                        lockingTurret.AimToTarget(lockedTarget.predictedPosition, lockingPitch, lockingYaw);
                    }
                    else
                    {
                        lockingTurret.ReturnTurret();
                    }
                }
            }
            else
            {
                if (rotationTransform)
                {
                    rotationTransform.localRotation = Quaternion.Lerp(rotationTransform.localRotation,
                        Quaternion.identity, 5*TimeWarp.fixedDeltaTime);
                }

                if (lockingTurret)
                {
                    lockingTurret.ReturnTurret();
                }
            }
        }
        
        void Scan()
        {
            float angleDelta = scanRotationSpeed*Time.fixedDeltaTime;
            RadarUtils.RadarUpdateScanLock(weaponManager, currentAngle, referenceTransform, angleDelta, referenceTransform.position, this, false, ref attemptedLocks);

            if (omnidirectional)
            {
                currentAngle = Mathf.Repeat(currentAngle + angleDelta, 360);
            }
            else
            {
                currentAngle += radialScanDirection*angleDelta;

                if (locked)
                {
                    float targetAngle = VectorUtils.SignedAngle(referenceTransform.forward,
                        Vector3.ProjectOnPlane(lockedTarget.position - referenceTransform.position,
                            referenceTransform.up), referenceTransform.right);
                    leftLimit = Mathf.Clamp(targetAngle - (multiLockFOV/2), -directionalFieldOfView/2,
                        directionalFieldOfView/2);
                    rightLimit = Mathf.Clamp(targetAngle + (multiLockFOV/2), -directionalFieldOfView/2,
                        directionalFieldOfView/2);

                    if (radialScanDirection < 0 && currentAngle < leftLimit)
                    {
                        currentAngle = leftLimit;
                        radialScanDirection = 1;
                    }
                    else if (radialScanDirection > 0 && currentAngle > rightLimit)
                    {
                        currentAngle = rightLimit;
                        radialScanDirection = -1;
                    }
                }
                else
                {
                    if (Mathf.Abs(currentAngle) > directionalFieldOfView/2)
                    {
                        currentAngle = Mathf.Sign(currentAngle)*directionalFieldOfView/2;
                        radialScanDirection = -radialScanDirection;
                    }
                }
            }
        }

        public bool TryLockTarget(Vector3 position)
        {
            if (!canLock)
            {
                return false;
            }

            if (BDArmorySettings.DRAW_DEBUG_LABELS)
                Debug.Log("[BDArmory]: Trying to radar lock target with (" + radarName + ")");

            if (currentLocks == maxLocks)
            {
                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                    Debug.Log("[BDArmory]: - Failed, this radar already has the maximum allowed targets locked.");
                return false;
            }

            Vector3 targetPlanarDirection = Vector3.ProjectOnPlane(position - referenceTransform.position,
                referenceTransform.up);
            float angle = Vector3.Angle(targetPlanarDirection, referenceTransform.forward);
            if (referenceTransform.InverseTransformPoint(position).x < 0)
            {
                angle = -angle;
            }
            //TargetSignatureData.ResetTSDArray(ref attemptedLocks);
            RadarUtils.RadarUpdateScanLock(weaponManager, angle, referenceTransform, lockAttemptFOV, referenceTransform.position, this, true, ref attemptedLocks, signalPersistTime);

            for (int i = 0; i < attemptedLocks.Length; i++)
            {
                if (attemptedLocks[i].exists && (attemptedLocks[i].predictedPosition - position).sqrMagnitude < 40 * 40)
                {
                    if (!locked && !omnidirectional)
                    {
                        float targetAngle = VectorUtils.SignedAngle(referenceTransform.forward,
                            Vector3.ProjectOnPlane(attemptedLocks[i].position - referenceTransform.position,
                                referenceTransform.up), referenceTransform.right);
                        currentAngle = targetAngle;
                    }
                    lockedTargets.Add(attemptedLocks[i]);
                    currLocks = lockedTargets.Count;

                    if (BDArmorySettings.DRAW_DEBUG_LABELS)
                        Debug.Log("[BDArmory]: - Acquired lock on target (" + attemptedLocks[i].vessel?.name + ")");

                    vesselRadarData.AddRadarContact(this, lockedTarget, true);
                    vesselRadarData.UpdateLockedTargets();
                    return true;
                }
            }

            if (BDArmorySettings.DRAW_DEBUG_LABELS)
                Debug.Log("[BDArmory]: - Failed to lock on target.");

            return false;
        }        

        void BoresightScan()
        {
            if (locked)
            {
                boresightScan = false;
                return;
            }

            currentAngle = Mathf.Lerp(currentAngle, 0, 0.08f);
            RadarUtils.RadarUpdateScanBoresight(new Ray(transform.position, transform.up), boresightFOV, ref attemptedLocks, Time.fixedDeltaTime, this);

            for (int i = 0; i < attemptedLocks.Length; i++)
            {
                if (!attemptedLocks[i].exists || !(attemptedLocks[i].age < 0.1f)) continue;
                TryLockTarget(attemptedLocks[i].predictedPosition);
                boresightScan = false;
                return;
            }
        }
        
        void UpdateLock(int index)
        {
            TargetSignatureData lockedTarget = lockedTargets[index];

            Vector3 targetPlanarDirection =
                Vector3.ProjectOnPlane(lockedTarget.predictedPosition - referenceTransform.position,
                    referenceTransform.up);
            float lookAngle = Vector3.Angle(targetPlanarDirection, referenceTransform.forward);
            if (referenceTransform.InverseTransformPoint(lockedTarget.predictedPosition).x < 0)
            {
                lookAngle = -lookAngle;
            }

            if (omnidirectional)
            {
                if (lookAngle < 0) lookAngle += 360;
            }


            lockScanAngle = lookAngle + currentAngleLock;
            if (!canTrackWhileScan && index == lockedTargetIndex)
            {
                currentAngle = lockScanAngle;
            }
            float angleDelta = lockRotationSpeed*Time.fixedDeltaTime;
            float lockedSignalPersist = lockRotationAngle/lockRotationSpeed;
            //RadarUtils.ScanInDirection(lockScanAngle, referenceTransform, angleDelta, referenceTransform.position, minLockedSignalThreshold, ref attemptedLocks, lockedSignalPersist);
            bool radarSnapshot = (snapshotTicker > 30);
            if (radarSnapshot)
            {
                snapshotTicker = 0;
            }
            else
            {
                snapshotTicker++;
            }
            //RadarUtils.ScanInDirection (new Ray (referenceTransform.position, lockedTarget.predictedPosition - referenceTransform.position), lockRotationAngle * 2, minLockedSignalThreshold, ref attemptedLocks, lockedSignalPersist, true, rwrType, radarSnapshot);

            if (
                Vector3.Angle(lockedTarget.position - referenceTransform.position,
                    this.lockedTarget.position - referenceTransform.position) > multiLockFOV/2)
            {
                UnlockTargetAt(index, true);
                return;
            }

            RadarUtils.RadarUpdateLockTrack(
                new Ray(referenceTransform.position, lockedTarget.predictedPosition - referenceTransform.position),
                lockedTarget.predictedPosition, lockRotationAngle*2, this, lockedSignalPersist, true, index, lockedTarget.vessel);

            //if still failed or out of FOV, unlock.
            if (!lockedTarget.exists ||
                (!omnidirectional &&
                 Vector3.Angle(lockedTarget.position - referenceTransform.position, transform.up) >
                 directionalFieldOfView/2))
            {
                //UnlockAllTargets();
                UnlockTargetAt(index, true);
                return;
            }

            //unlock if over-jammed
            // MOVED TO RADARUTILS!

            //cycle scan direction
            if (index == lockedTargetIndex)
            {
                currentAngleLock += lockScanDirection*angleDelta;
                if (Mathf.Abs(currentAngleLock) > lockRotationAngle/2)
                {
                    currentAngleLock = Mathf.Sign(currentAngleLock)*lockRotationAngle/2;
                    lockScanDirection = -lockScanDirection;
                }
            }
        }
                
        public void UnlockAllTargets()
        {
            if (!locked) return;

            lockedTargets.Clear();
            currLocks = 0;
            lockedTargetIndex = 0;
            
            if (vesselRadarData)
            {
                vesselRadarData.UnlockAllTargetsOfRadar(this);
            }

            if (BDArmorySettings.DRAW_DEBUG_LABELS)
                Debug.Log("[BDArmory]: Radar Targets were cleared (" + radarName + ").");
        }
                
        public void SetActiveLock(TargetSignatureData target)
        {
            for (int i = 0; i < lockedTargets.Count; i++)
            {
                if (target.vessel == lockedTargets[i].vessel)
                {
                    lockedTargetIndex = i;
                    return;
                }
            }
        }
                
        public void UnlockTargetAt(int index, bool tryRelock = false)
        {
            Vessel rVess = lockedTargets[index].vessel;

            if (tryRelock)
            {
                UnlockTargetAt(index, false);
                if (rVess)
                {
                    StartCoroutine(RetryLockRoutine(rVess));
                }
                return;
            }


            lockedTargets.RemoveAt(index);
            currLocks = lockedTargets.Count;
            if (lockedTargetIndex > index)
            {
                lockedTargetIndex--;
            }

            lockedTargetIndex = Mathf.Clamp(lockedTargetIndex, 0, currLocks - 1);
            lockedTargetIndex = Mathf.Max(lockedTargetIndex, 0);

            if (vesselRadarData)
            {
                //vesselRadarData.UnlockTargetAtPosition(position);
                vesselRadarData.RemoveVesselFromTargets(rVess);
            }
        }
                
        IEnumerator RetryLockRoutine(Vessel v)
        {
            yield return null;
            vesselRadarData.TryLockTarget(v);
        }
                
        public void UnlockTargetVessel(Vessel v)
        {
            for (int i = 0; i < lockedTargets.Count; i++)
            {
                if (lockedTargets[i].vessel == v)
                {
                    UnlockTargetAt(i);
                    return;
                }
            }
        }
                
        void SlaveTurrets()
        {
            List<ModuleTargetingCamera>.Enumerator mtc = vessel.FindPartModulesImplementing<ModuleTargetingCamera>().GetEnumerator();
            while (mtc.MoveNext())
            {
                if (mtc.Current == null) continue;
                mtc.Current.slaveTurrets = false;
            }
            mtc.Dispose();

            List<ModuleRadar>.Enumerator rad = vessel.FindPartModulesImplementing<ModuleRadar>().GetEnumerator();
            while (rad.MoveNext())
            {
                if (rad.Current == null) continue;
                rad.Current.slaveTurrets = false;
            }
            rad.Dispose();

            slaveTurrets = true;
        }
                
        void UnslaveTurrets()
        {
            List<ModuleTargetingCamera>.Enumerator mtc = vessel.FindPartModulesImplementing<ModuleTargetingCamera>().GetEnumerator();
            while (mtc.MoveNext())
            {
                if (mtc.Current == null) continue;
                mtc.Current.slaveTurrets = false;
            }

            List<ModuleRadar>.Enumerator rad = vessel.FindPartModulesImplementing<ModuleRadar>().GetEnumerator();
            while (rad.MoveNext())
            {
                if (rad.Current == null) continue;
                rad.Current.slaveTurrets = false;
            }

            if (weaponManager)
            {
                weaponManager.slavingTurrets = false;
            }

            slaveTurrets = false;
        }
                
        public void UpdateLockedTargetInfo(TargetSignatureData newData)
        {
            int index = -1;
            for (int i = 0; i < lockedTargets.Count; i++)
            {
                if (lockedTargets[i].vessel != newData.vessel) continue;
                index = i;
                break;
            }

            if (index >= 0)
            {
                lockedTargets[index] = newData;
            }
        }
                
        public void ReceiveContactData(TargetSignatureData contactData, bool _locked)
        {
            if (vesselRadarData)
            {
                vesselRadarData.AddRadarContact(this, contactData, _locked);
            }

            List<VesselRadarData>.Enumerator vrd = linkedToVessels.GetEnumerator();
            while (vrd.MoveNext())
            {
                if (vrd.Current == null) continue;
                if (vrd.Current.canReceiveRadarData && vrd.Current.vessel != contactData.vessel)
                {
                    vrd.Current.AddRadarContact(this, contactData, _locked);
                }
            }
            vrd.Dispose();
        }
                
        public void AddExternalVRD(VesselRadarData vrd)
        {
            if (!linkedToVessels.Contains(vrd))
            {
                linkedToVessels.Add(vrd);
            }
        }
                
        public void RemoveExternalVRD(VesselRadarData vrd)
        {
            linkedToVessels.Remove(vrd);
        }
                
        void OnGUI()
        {
            if (drawGUI)
            {
                if (boresightScan)
                {
                    BDGUIUtils.DrawTextureOnWorldPos(transform.position + (3500*transform.up),
                        BDArmorySetup.Instance.dottedLargeGreenCircle, new Vector2(156, 156), 0);
                }
            }
        }
                
        public void RecoverLinkedVessels()
        {
            string[] vesselIDs = linkedVesselID.Split(new char[] {','});
            for (int i = 0; i < vesselIDs.Length; i++)
            {
                StartCoroutine(RecoverLinkedVesselRoutine(vesselIDs[i]));
            }
        }
                
        IEnumerator RecoverLinkedVesselRoutine(string vesselID)
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
                    StartCoroutine(RelinkVRDWhenReadyRoutine(vrd));
                    yield break;
                }
                v.Dispose();

                yield return new WaitForSeconds(0.5f);
            }
        }
                
        IEnumerator RelinkVRDWhenReadyRoutine(VesselRadarData vrd)
        {
            while (!vrd.radarsReady || vrd.vessel.packed)
            {
                yield return null;
            }
            yield return null;
            vesselRadarData.LinkVRD(vrd);
            Debug.Log("[BDArmory]: Radar data link recovered: Local - " + vessel.vesselName + ", External - " +
                      vrd.vessel.vesselName);
        }
        
        public string getRWRType(int i)
        {
            switch (i)
            {
                case 0:
                    return "SAM";
                case 1:
                    return "FIGHTER";
                case 2:
                    return "AWACS";
                case 3:
                case 4:
                    return "MISSILE";
                case 5:
                    return "DETECTION";
                case 6:
                    return "SONAR";
            }
            return "UNKNOWN";
            //{SAM = 0, Fighter = 1, AWACS = 2, MissileLaunch = 3, MissileLock = 4, Detection = 5, Sonar = 6}
        }

        // RMB info in editor
        public override string GetInfo()
        {
            bool isLinkOnly = (canRecieveRadarData && !canScan && !canLock);

            StringBuilder output = new StringBuilder();
            output.Append(Environment.NewLine);
            output.AppendLine($"Radar Type: {(isLinkOnly ? "datalink only" : omnidirectional ? "omnidirectional" : "boresight")}");

        output.AppendLine($"EC/sec: {resourceDrain}");
            if (!isLinkOnly)
            {
                output.AppendLine($"Field of view: {directionalFieldOfView}°");
                output.AppendLine($"RWR Threat Type: {getRWRType(rwrThreatType)}");

                output.Append(Environment.NewLine);
                output.AppendLine($"Capabilities:");
                output.AppendLine($"- Scanning: {canScan}");
                output.AppendLine($"- Track-While-Scan: {canTrackWhileScan}");
                output.AppendLine($"- Locking: {canLock}");
                if (canLock)
                {
                    output.AppendLine($"- Max Locks: {maxLocks}");
                }
                output.AppendLine($"- Receive Data: {canRecieveRadarData}");

                output.Append(Environment.NewLine);
                output.AppendLine($"Performance:");

                if (canScan)
                    output.AppendLine($"- Detection: {radarDetectionCurve.Evaluate(radarMaxDistanceDetect)} m^2 @ {radarMaxDistanceDetect} km");
                else
                    output.AppendLine($"- Detection: (none)");
                if (canLock)
                    output.AppendLine($"- Lock/Track: {radarLockTrackCurve.Evaluate(radarMaxDistanceLockTrack)} m^2 @ {radarMaxDistanceLockTrack} km");
                else
                    output.AppendLine($"- Lock/Track: (none)");
                output.AppendLine($"- Ground clutter factor: {radarGroundClutterFactor}");
            }

            return output.ToString();
        }
        
        void DrainElectricity()
        {
            if (resourceDrain <= 0)
            {
                return;
            }

            double drainAmount = resourceDrain*TimeWarp.fixedDeltaTime;
            double chargeAvailable = part.RequestResource("ElectricCharge", drainAmount, ResourceFlowMode.ALL_VESSEL);
            if (chargeAvailable < drainAmount*0.95f)
            {
                ScreenMessages.PostScreenMessage("Radar Requires EC", 5.0f, ScreenMessageStyle.UPPER_CENTER);
                DisableRadar();
            }
        }
    }
}