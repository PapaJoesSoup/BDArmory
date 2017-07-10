using UnityEngine;
using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;

namespace BahaTurret
{
    public class ModuleRadar : PartModule
    {
        [KSPField]
        public string radarName;

        [KSPField]
        public int turretID = 0;

        [KSPField]
        public bool canLock = true;

        [KSPField]
        public int maxLocks = 1;

        [KSPField]
        public bool canScan = true;

        [KSPField]
        public bool canRecieveRadarData = false;

        [KSPField(isPersistant = true)]
        public string linkedVesselID;

        [KSPField]
        public bool omnidirectional = true;

        [KSPField]
        public float directionalFieldOfView = 90;

        [KSPField]
        public float boresightFOV = 10;

        [KSPField]
        public float scanRotationSpeed = 120; //in degrees per second

        [KSPField]
        public float lockRotationSpeed = 120;

        [KSPField]
        public float lockRotationAngle = 4;

        [KSPField]
        public string rotationTransformName = string.Empty;
        Transform rotationTransform;

        [KSPField(isPersistant = true)]
        public bool radarEnabled = false;

        [KSPField]
        public float minSignalThreshold = 90;

        [KSPField]
        public float minLockedSignalThreshold = 90;

        [KSPField]
        public bool canTrackWhileScan = false;

        [KSPField]
        public float multiLockFOV = 30;

        [KSPField]
        public int rwrThreatType = 0;

        public bool locked
        {
            get { return currLocks > 0; }
        }
        public int currentLocks
        {
            get { return currLocks; }
        }
        private int currLocks = 0;
        private List<VesselRadarData> linkedToVessels;
 
        //public string rangeIncrements = "5000,10000,20000";
        //public float[] rIncrements;

        [KSPField(isPersistant = true)] public int rangeIndex = 99;

        public float maxRange;

        public RadarWarningReceiver.RWRThreatTypes rwrType = RadarWarningReceiver.RWRThreatTypes.SAM;

        //contacts
        TargetSignatureData[] attemptedLocks;

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

        public List<TargetSignatureData> lockedTargets;
        int lockedTargetIndex = 0;

        public int currentLockIndex
        {
            get { return lockedTargetIndex; }
        }

        //GUI
        bool drawGUI = false;
        public float signalPersistTime;

        //scanning
        [KSPField] public bool showDirectionWhileScan = false;
        [KSPField(isPersistant = true)] public float currentAngle = 0;
        float currentAngleLock = 0;
        public Transform referenceTransform;
        float radialScanDirection = 1;
        float lockScanDirection = 1;

        public bool boresightScan = false;

        public List<ModuleRadar> availableRadarLinks;

        //locking
        [KSPField] public float lockAttemptFOV = 2;
        public float lockScanAngle = 0;

        public bool slaveTurrets = false;

        public MissileLauncher lastMissile;

        public ModuleTurret lockingTurret;
        public bool lockingPitch = true;
        public bool lockingYaw = true;

        private MissileFire wpmr;

        public MissileFire weaponManager
        {
            get
            {
                if (wpmr == null || wpmr.vessel != vessel)
                {
                    wpmr = null;
                    foreach (var mf in vessel.FindPartModulesImplementing<MissileFire>())
                    {
                        wpmr = mf;
                    }
                }

                return wpmr;
            }
            set { wpmr = value; }
        }

        public VesselRadarData vesselRadarData;

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

        void UpdateToggleGuiName()
        {
            Events["Toggle"].guiName = radarEnabled ? "Disable Radar" : "Enable Radar";
        }

        public void EnsureVesselRadarData()
        {
            if (vessel == null) return;

            myVesselID = vessel.id.ToString();

            if (vesselRadarData == null || vesselRadarData.vessel != vessel)
            {
                vesselRadarData = vessel.gameObject.GetComponent<VesselRadarData>();

                if (vesselRadarData == null)
                {
                    vesselRadarData = vessel.gameObject.AddComponent<VesselRadarData>();
                    vesselRadarData.weaponManager = weaponManager;
                }
            }
        }

        public void EnableRadar()
        {
            EnsureVesselRadarData();

            radarEnabled = true;
            foreach (var mf in vessel.FindPartModulesImplementing<MissileFire>())
            {
                weaponManager = mf;
                if (vesselRadarData)
                {
                    vesselRadarData.weaponManager = mf;
                }
                break;
            }

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

            foreach (var mf in vessel.FindPartModulesImplementing<MissileFire>())
            {
                //mf.radar = null;
                break;
            }
            UpdateToggleGuiName();

            if (vesselRadarData)
            {
                vesselRadarData.RemoveRadar(this);
            }

            foreach (var vrd in linkedToVessels)
            {
                vrd.UnlinkDisabledRadar(this);
            }
        }

        bool unlinkOnDestroy = true;
        string myVesselID;

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
                    foreach (var vrd in linkedToVessels)
                    {
                        if (unlinkOnDestroy)
                        {
                            vrd.UnlinkDisabledRadar(this);
                        }
                        else
                        {
                            vrd.BeginWaitForUnloadedLinkedRadar(this, myVesselID);
                        }
                    }
                }
            }
        }

        bool startupComplete = false;

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            if (HighLogic.LoadedSceneIsFlight)
            {
                myVesselID = vessel.id.ToString();
                RadarUtils.SetupRadarCamera();

                if (string.IsNullOrEmpty(radarName))
                {
                    radarName = part.partInfo.title;
                }

                linkedToVessels = new List<VesselRadarData>();


                //rIncrements = Misc.ParseToFloatArray(rangeIncrements);
                //rangeIndex = Mathf.Clamp(rangeIndex, 0, rIncrements.Length-1);
                //maxRange = rIncrements[rIncrements.Length-1];
                signalPersistTime = omnidirectional
                    ? 360/(scanRotationSpeed + 5)
                    : directionalFieldOfView/(scanRotationSpeed + 5);

                if (rotationTransformName != string.Empty)
                {
                    rotationTransform = part.FindModelTransform(rotationTransformName);
                }


                //lockedTarget = TargetSignatureData.noTarget;


                attemptedLocks = new TargetSignatureData[3];
                TargetSignatureData.ResetTSDArray(ref attemptedLocks);

                lockedTargets = new List<TargetSignatureData>();

                referenceTransform = (new GameObject()).transform;
                referenceTransform.parent = transform;
                referenceTransform.localPosition = Vector3.zero;

                foreach (var tur in part.FindModulesImplementing<ModuleTurret>())
                {
                    if (tur.turretID == turretID)
                    {
                        lockingTurret = tur;
                        break;
                    }
                }

                rwrType = (RadarWarningReceiver.RWRThreatTypes) rwrThreatType;


                foreach (var wm in vessel.FindPartModulesImplementing<MissileFire>())
                {
                    wm.radars.Add(this);
                }

                GameEvents.onVesselGoOnRails.Add(OnGoOnRails);


                EnsureVesselRadarData();

                StartCoroutine(StartUpRoutine());
            }

            if (HighLogic.LoadedSceneIsEditor)
            {
                foreach (var tur in part.FindModulesImplementing<ModuleTurret>())
                {
                    if (tur.turretID == turretID)
                    {
                        lockingTurret = tur;
                        break;
                    }
                }

                if (lockingTurret)
                {
                    lockingTurret.Fields["minPitch"].guiActiveEditor = false;
                    lockingTurret.Fields["maxPitch"].guiActiveEditor = false;
                    lockingTurret.Fields["yawRange"].guiActiveEditor = false;
                }
            }
        }

        void OnGoOnRails(Vessel v)
        {
            if (v == vessel)
            {
                unlinkOnDestroy = false;
                myVesselID = vessel.id.ToString();
            }
        }

        IEnumerator StartUpRoutine()
        {
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

            /*
            if(linked)
            {
                foreach(var v in FlightGlobals.Vessels)
                {
                    if(v.id.ToString() == linkedVesselID)
                    {
                        foreach(var mr in v.FindPartModulesImplementing<ModuleRadar>())
                        {
                            if(mr.radarEnabled)
                            {
                                //LinkToRadar(mr);
                                break;
                            }
                        }
                        break;
                    }
                }
                if(!linkedRadar)
                {
                    Debug.Log("[BDArmory]: Radar was linked, but linked radar doesn't exist.");
                    UnlinkRadar();
                }

            }
            */
            yield return null;
            if (!vesselRadarData.hasLoadedExternalVRDs)
            {
                RecoverLinkedVessels();
                vesselRadarData.hasLoadedExternalVRDs = true;
            }

            UpdateToggleGuiName();


            startupComplete = true;
        }

        // Update is called once per frame
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
                       vessel.isActiveVessel && BDArmorySettings.GAME_UI_ENABLED && !MapView.MapIsEnabled);

            //UpdateSlaveData();
            if (radarEnabled)
            {
                DrainElectricity();
            }
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

        public float leftLimit;
        public float rightLimit;

        void Scan()
        {
            float angleDelta = scanRotationSpeed*Time.fixedDeltaTime;
            //RadarUtils.ScanInDirection(weaponManager, currentAngle, referenceTransform, angleDelta, vessel.transform.position, minSignalThreshold, ref contacts, signalPersistTime, true, rwrType, true);
            RadarUtils.UpdateRadarLock(weaponManager, currentAngle, referenceTransform, angleDelta,
                vessel.transform.position, minSignalThreshold, this, true, rwrType, true);

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
            Debug.Log("[BDArmory]: Trying to radar lock target");

            if (currentLocks == maxLocks)
            {
                Debug.Log("[BDArmory]: This radar (" + radarName + ") already has the maximum allowed targets locked.");
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
            RadarUtils.UpdateRadarLock(weaponManager, angle, referenceTransform, lockAttemptFOV,
                referenceTransform.position, minLockedSignalThreshold, ref attemptedLocks, signalPersistTime, true,
                rwrType, true);

            for (int i = 0; i < attemptedLocks.Length; i++)
            {
                if (attemptedLocks[i].exists && (attemptedLocks[i].predictedPosition - position).sqrMagnitude < 40*40)
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
                    Debug.Log("[BDArmory]: - Acquired lock on target.");
                    vesselRadarData.AddRadarContact(this, lockedTarget, true);
                    vesselRadarData.UpdateLockedTargets();
                    return true;
                }
            }

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
            RadarUtils.UpdateRadarLock(new Ray(transform.position, transform.up), boresightFOV, minLockedSignalThreshold,
                ref attemptedLocks, Time.fixedDeltaTime, true, rwrType, true);


            for (int i = 0; i < attemptedLocks.Length; i++)
            {
                if (attemptedLocks[i].exists && attemptedLocks[i].age < 0.1f)
                {
                    TryLockTarget(attemptedLocks[i].predictedPosition);
                    /*
                    locked = true;
                    //lockedTarget = attemptedLocks[i];
                    lockedTargets = new List<TargetSignatureData>();
                    lockedTargets.Add(attemptedLocks[i]);
                    lockedTargetIndex = 0;
                    Debug.Log ("- Acquired lock on target.");
                    */
                    boresightScan = false;
                    return;
                }
            }
        }

        int snapshotTicker = 0;

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

            RadarUtils.UpdateRadarLock(
                new Ray(referenceTransform.position, lockedTarget.predictedPosition - referenceTransform.position),
                lockedTarget.predictedPosition, lockRotationAngle*2, minLockedSignalThreshold, this, true, radarSnapshot,
                lockedSignalPersist, true, index, lockedTarget.vessel);

            /*
            TargetSignatureData prevLock = lockedTarget;
            lockedTarget = TargetSignatureData.noTarget;
            for(int i = 0; i < attemptedLocks.Length; i++)
            {
                if(attemptedLocks[i].exists && (attemptedLocks[i].predictedPosition-prevLock.predictedPosition).sqrMagnitude < Mathf.Pow(20,2) && attemptedLocks[i].age < 2*lockedSignalPersist)
                {
                    lockedTarget = attemptedLocks[i];
                    break;
                }
            }

            if(!lockedTarget.exists) //if failed to maintain lock, get lock data from linked radar
            {
                if(linked && linkedRadar && linkedRadar.locked && (linkedRadar.lockedTarget.predictedPosition-prevLock.predictedPosition).sqrMagnitude < Mathf.Pow(20,2))
                {
                    lockedTarget = linkedRadar.lockedTarget;
                    //if(lockedTarget.exists) return;
                }
            }
            */

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
            if (lockedTarget.vesselJammer &&
                lockedTarget.vesselJammer.lockBreakStrength >
                lockedTarget.signalStrength*lockedTarget.vesselJammer.rcsReductionFactor)
            {
                //UnlockAllTargets();
                UnlockTargetAt(index, true);
                return;
            }


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
            if (BDArmorySettings.DRAW_DEBUG_LINES) Debug.Log("[BDArmory]: Radar Targets were Cleared");
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

        /*
        public void UnlockTargetAtPosition(Vector3 position)
        {
            for(int i = 0; i < lockedTargets.Count; i++)
            {
                if(Vector3.SqrMagnitude(lockedTargets[i].position - position) < 10)
                {
                    UnlockTargetAt(i);
                    return;
                }
            }
        }
        */

        void SlaveTurrets()
        {
            foreach (var mtc in vessel.FindPartModulesImplementing<ModuleTargetingCamera>())
            {
                mtc.slaveTurrets = false;
            }

            foreach (var rad in vessel.FindPartModulesImplementing<ModuleRadar>())
            {
                rad.slaveTurrets = false;
            }

            slaveTurrets = true;
        }

        void UnslaveTurrets()
        {
            foreach (var mtc in vessel.FindPartModulesImplementing<ModuleTargetingCamera>())
            {
                mtc.slaveTurrets = false;
            }

            foreach (var rad in vessel.FindPartModulesImplementing<ModuleRadar>())
            {
                rad.slaveTurrets = false;
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
                if (lockedTargets[i].vessel == newData.vessel)
                {
                    index = i;
                    break;
                }
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

            foreach (var vrd in linkedToVessels)
            {
                if (vrd && vrd.canReceiveRadarData && vrd.vessel != contactData.vessel)
                {
                    vrd.AddRadarContact(this, contactData, _locked);
                }
            }
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
                        BDArmorySettings.Instance.dottedLargeGreenCircle, new Vector2(156, 156), 0);
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
                foreach (var v in BDATargetManager.LoadedVessels)
                {
                    if (!v || !v.loaded || v == vessel) continue;
                    if (v.id.ToString() == vesselID)
                    {
                        VesselRadarData vrd = v.GetComponent<VesselRadarData>();
                        if (vrd)
                        {
                            StartCoroutine(RelinkVRDWhenReadyRoutine(vrd));
                            yield break;
                        }
                    }
                }

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
            //{SAM = 0, Fighter = 1, AWACS = 2, MissileLaunch = 3, MissileLock = 4, Detection = 5}
        }

        // RMB info in editor
        public override string GetInfo()
        {
            var output = new StringBuilder();
            output.Append(Environment.NewLine);
            output.Append(String.Format("Radar Type: {0}", this.radarName));
            output.Append(Environment.NewLine);
            output.Append(String.Format("Range: {0} meters", this.maxRange));
            output.Append(Environment.NewLine);
            output.Append(String.Format("RWR Threat Type: {0}", getRWRType(rwrThreatType)));
            output.Append(Environment.NewLine);
            output.Append(String.Format("Can Scan: {0}", canScan));
            output.Append(Environment.NewLine);
            output.Append(String.Format("Track-While-Scan: {0}", canTrackWhileScan));
            output.Append(Environment.NewLine);
            output.Append(String.Format("Can Lock: {0}", canLock));
            output.Append(Environment.NewLine);
            output.Append(String.Format("Can Receive Data: {0}", canRecieveRadarData));
            output.Append(Environment.NewLine);
            if (canLock)
            {
                output.Append(String.Format("Simultaneous Locks: {0}", maxLocks));
                output.Append(Environment.NewLine);
            }

            return output.ToString();
        }

        [KSPField] public double resourceDrain = 0.825;

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