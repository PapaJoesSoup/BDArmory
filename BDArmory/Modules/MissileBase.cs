using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BDArmory.Core;
using BDArmory.Core.Extension;
using BDArmory.CounterMeasure;
using BDArmory.FX;
using BDArmory.Guidances;
using BDArmory.Misc;
using BDArmory.Radar;
using BDArmory.Targeting;
using BDArmory.UI;
using UnityEngine;

namespace BDArmory.Modules
{
    public abstract class MissileBase : EngageableWeapon, IBDWeapon
    {
        // High Speed missile fix
        /// //////////////////////////////////
        [KSPField(isPersistant = true)]
        public float DetonationOffset = 0.1f;

        [KSPField(isPersistant = true)]
        public bool autoDetCalc = false;
        /// //////////////////////////////////

        protected WeaponClasses weaponClass;

        public WeaponClasses GetWeaponClass()
        {
            return weaponClass;
        }

        public string GetMissileType()
        {
            return missileType;
        }

        [KSPField]
        public string missileType = "missile";

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_MaxStaticLaunchRange"), UI_FloatRange(minValue = 5000f, maxValue = 50000f, stepIncrement = 1000f, scene = UI_Scene.Editor, affectSymCounterparts = UI_Scene.All)]//Max Static Launch Range
        public float maxStaticLaunchRange = 5000;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_MinStaticLaunchRange"), UI_FloatRange(minValue = 10f, maxValue = 4000f, stepIncrement = 100f, scene = UI_Scene.Editor, affectSymCounterparts = UI_Scene.All)]//Min Static Launch Range
        public float minStaticLaunchRange = 10;

        [KSPField]
        public float minLaunchSpeed = 0;

        public virtual float ClearanceRadius => 0.14f;

        public virtual float ClearanceLength => 0.14f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_MaxOffBoresight"),//Max Off Boresight
            UI_FloatRange(minValue = 0f, maxValue = 360f, stepIncrement = 5f, scene = UI_Scene.Editor, affectSymCounterparts = UI_Scene.All)]
        public float maxOffBoresight = 360;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_DetonationDistanceOverride"), UI_FloatRange(minValue = 0f, maxValue = 100f, stepIncrement = 1f, scene = UI_Scene.Editor, affectSymCounterparts = UI_Scene.All)]//Detonation distance override
        public float DetonationDistance = -1;

        //[KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "SLW Offset"), UI_FloatRange(minValue = -1000f, maxValue = 0f, stepIncrement = 100f, affectSymCounterparts = UI_Scene.All)]
        public float SLWOffset = 0;

        public float getSWLWOffset
        {
            get
            {
                return SLWOffset;
            }
        }

        [KSPField]
        public bool guidanceActive = true;

        [KSPField]
        public float lockedSensorFOV = 2.5f;

        [KSPField]
        public float heatThreshold = 150;

        [KSPField]
        public bool allAspect = false;

        [KSPField]
        public bool isTimed = false;

        [KSPField]
        public bool radarLOAL = false;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "#LOC_BDArmory_DropTime"),//Drop Time
            UI_FloatRange(minValue = 0f, maxValue = 5f, stepIncrement = 0.5f, scene = UI_Scene.Editor)]
        public float dropTime = 0.5f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_InCargoBay"),//In Cargo Bay: 
            UI_Toggle(disabledText = "#LOC_BDArmory_false", enabledText = "#LOC_BDArmory_true", affectSymCounterparts = UI_Scene.All)]//False--True
        public bool inCargoBay = false;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = false, guiName = "#LOC_BDArmory_DetonationTime"),//Detonation Time
            UI_FloatRange(minValue = 2f, maxValue = 30f, stepIncrement = 0.5f, scene = UI_Scene.Editor)]
        public float detonationTime = 2;

        [KSPField]
        public float activeRadarRange = 6000;

        [Obsolete("Use activeRadarLockTrackCurve!")]
        [KSPField]
        public float activeRadarMinThresh = 140;

        [KSPField]
        public FloatCurve activeRadarLockTrackCurve = new FloatCurve();             // floatcurve to define min/max range and lockable radar cross section

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_BallisticOvershootFactor"),//Ballistic Overshoot factor
         UI_FloatRange(minValue = 0.5f, maxValue = 1.5f, stepIncrement = 0.01f, scene = UI_Scene.Editor)]
        public float BallisticOverShootFactor = 0.7f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_BallisticAnglePath"),//Ballistic Angle path
         UI_FloatRange(minValue = 5f, maxValue = 60f, stepIncrement = 5f, scene = UI_Scene.Editor)]
        public float BallisticAngle = 45.0f;


        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_CruiseAltitude"), UI_FloatRange(minValue = 1f, maxValue = 500f, stepIncrement = 10f, scene = UI_Scene.Editor, affectSymCounterparts = UI_Scene.All)]//Cruise Altitude
        public float CruiseAltitude = 500;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_CruiseSpeed"), UI_FloatRange(minValue = 100f, maxValue = 6000f, stepIncrement = 50f, scene = UI_Scene.Editor, affectSymCounterparts = UI_Scene.All)]//Cruise speed
        public float CruiseSpeed = 300;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_CruisePredictionTime"), UI_FloatRange(minValue = 1f, maxValue = 15f, stepIncrement = 1f, scene = UI_Scene.Editor, affectSymCounterparts = UI_Scene.All)]//Cruise prediction time
        public float CruisePredictionTime = 5;

        [KSPField]
        public float missileRadarCrossSection = RadarUtils.RCS_MISSILES;            // radar cross section of this missile for detection purposes

        public enum MissileStates { Idle, Drop, Boost, Cruise, PostThrust }

        public enum DetonationDistanceStates { NotSafe, Cruising, CheckingProximity, Detonate }

        public enum TargetingModes { None, Radar, Heat, Laser, Gps, AntiRad }

        public MissileStates MissileState { get; set; } = MissileStates.Idle;

        public DetonationDistanceStates DetonationDistanceState { get; set; } = DetonationDistanceStates.NotSafe;

        public enum GuidanceModes { None, AAMLead, AAMPure, AGM, AGMBallistic, Cruise, STS, Bomb, RCS, BeamRiding, SLW }

        public GuidanceModes GuidanceMode;

        public bool HasFired { get; set; } = false;

        public BDTeam Team { get; set; }

        public bool HasMissed { get; set; } = false;

        public Vector3 TargetPosition { get; set; } = Vector3.zero;

        public Vector3 TargetVelocity { get; set; } = Vector3.zero;

        public Vector3 TargetAcceleration { get; set; } = Vector3.zero;

        public float TimeIndex => Time.time - TimeFired;

        public TargetingModes TargetingMode { get; set; }

        public TargetingModes TargetingModeTerminal { get; set; }

        public float TimeToImpact { get; set; }

        public bool TargetAcquired { get; set; }

        public bool ActiveRadar { get; set; }

        public Vessel SourceVessel { get; set; } = null;

        public bool HasExploded { get; set; } = false;

        protected IGuidance _guidance;

        private double _lastVerticalSpeed;
        private double _lastHorizontalSpeed;

        public double HorizontalAcceleration
        {
            get
            {
                var result = (vessel.horizontalSrfSpeed - _lastHorizontalSpeed);
                _lastHorizontalSpeed = vessel.horizontalSrfSpeed;
                return result;

            }
        }

        public double VerticalAcceleration
        {
            get
            {
                var result = (vessel.horizontalSrfSpeed - _lastHorizontalSpeed);
                _lastVerticalSpeed = vessel.verticalSpeed;
                return result;
            }
        }

       

        public float Throttle
        {
            get
            {
                return _throttle;
            }

            set
            {
                _throttle = Mathf.Clamp01(value);
            }
        }

        public float TimeFired = -1;

        protected float lockFailTimer = -1;

        public Vessel legacyTargetVessel;

        public Transform MissileReferenceTransform;

        protected ModuleTargetingCamera targetingPod;

        //laser stuff
        public ModuleTargetingCamera lockedCamera;
        protected Vector3 lastLaserPoint;
        protected Vector3 laserStartPosition;
        protected Vector3 startDirection;

        //GPS stuff
        public Vector3d targetGPSCoords;

        //heat stuff
        public TargetSignatureData heatTarget;

        //radar stuff
        public VesselRadarData vrd;
        public TargetSignatureData radarTarget;
        private TargetSignatureData[] scannedTargets;
        public MissileFire TargetMf = null;
        private LineRenderer LR;

        private int snapshotTicker;
        private int locksCount = 0;
        private float _radarFailTimer = 0;
        private float maxRadarFailTime = 5;
        private float lastRWRPing = 0;
        private bool radarLOALSearching = false;
        protected bool checkMiss = false;
        public StringBuilder debugString = new StringBuilder();

        private float _throttle = 1f;
        Vector3 previousPos;
        
		public string Sublabel;
		public int missilecount = 0; //#191

		public void GetMissileCount() // could stick this in GetSublabel, but that gets called every frame by BDArmorySetup?
		{
			missilecount = 0;
			List<Part>.Enumerator craftPart = vessel.parts.GetEnumerator();
			while (craftPart.MoveNext())
			{
				if (craftPart.Current == null) continue;
                if (part == null) continue;
                if (part.name == null) continue;
                if (craftPart.Current.name != part.name) continue;
				missilecount++;
			}
			craftPart.Dispose();
		}

        public string GetSubLabel()
        {
			return Sublabel = "Guidance: " + Enum.GetName(typeof(TargetingModes), TargetingMode) + "; Remaining: "+ missilecount; //
        }

        public Part GetPart()
        {
            return part;
        }

        public abstract void FireMissile();

        public abstract void Jettison();

        public abstract float GetBlastRadius();

        protected abstract void PartDie(Part p);

        protected void DisablingExplosives(Part p)
        {
            if (p == null) return;

            var explosive = p.FindModuleImplementing<BDExplosivePart>();
            if (explosive != null)
            {
                p.FindModuleImplementing<BDExplosivePart>().Armed = false;
            }
        }

        protected void SetupExplosive(Part p)
        {
            if (p == null) return;

            var explosive = p.FindModuleImplementing<BDExplosivePart>();
            if (explosive != null)
            {
                p.FindModuleImplementing<BDExplosivePart>().Armed = true;
                if (GuidanceMode == GuidanceModes.AGM || GuidanceMode == GuidanceModes.AGMBallistic)
                {
                    p.FindModuleImplementing<BDExplosivePart>().Shaped = true;
                }
            }
        }

        public abstract void Detonate();

        public abstract Vector3 GetForwardTransform();

        protected void AddTargetInfoToVessel()
        {
            TargetInfo info = vessel.gameObject.AddComponent<TargetInfo>();
            info.Team = Team;
            info.isMissile = true;
            info.MissileBaseModule = this;
        }

        [KSPEvent(guiActive = true, guiActiveEditor = false, guiName = "#LOC_BDArmory_GPSTarget", active = true, name = "GPSTarget")]//GPS Target
        public void assignGPSTarget()
        {
            if (HighLogic.LoadedSceneIsFlight)
                PickGPSTarget();
        }

        [KSPField(isPersistant = true)]
        public bool gpsSet = false;

        [KSPField(isPersistant = true)]
        public Vector3 assignedGPSCoords;

        [KSPField(isPersistant = true, guiName = "#LOC_BDArmory_GPSTarget")]//GPS Target
        public string gpsTargetName = "";

       

        void PickGPSTarget()
        {
            gpsSet = true;
            Fields["gpsTargetName"].guiActive = true;
            gpsTargetName = BDArmorySetup.Instance.ActiveWeaponManager.designatedGPSInfo.name;
            assignedGPSCoords = BDArmorySetup.Instance.ActiveWeaponManager.designatedGPSCoords;
        }

        public Vector3d UpdateGPSTarget()
        {
            Vector3 gpsTargetCoords_;

            if (gpsSet && assignedGPSCoords != null)
            {
                gpsTargetCoords_ = assignedGPSCoords;
            }
            else
            {
                gpsTargetCoords_ = targetGPSCoords;
            }

            if (TargetAcquired)
            {
                TargetPosition = VectorUtils.GetWorldSurfacePostion(gpsTargetCoords_, vessel.mainBody);
                TargetVelocity = Vector3.zero;
                TargetAcceleration = Vector3.zero;
            }
            else
            {
                guidanceActive = false;
            }

            return gpsTargetCoords_;
        }

        protected void UpdateHeatTarget()
        {
            if (lockFailTimer > 1)
            {
                legacyTargetVessel = null;
                TargetAcquired = false;
                return;
            }
           
            
            if (heatTarget.exists && lockFailTimer < 0)
            {
                lockFailTimer = 0;
            }
            if (lockFailTimer >= 0)
            {
                Ray lookRay = new Ray(transform.position, heatTarget.position + (heatTarget.velocity * Time.fixedDeltaTime) - transform.position);
                heatTarget = BDATargetManager.GetHeatTarget(SourceVessel, vessel,  lookRay, lockedSensorFOV / 2, heatThreshold, allAspect, SourceVessel?.gameObject?.GetComponent<MissileFire>());

                if (heatTarget.exists)
                {
                    TargetAcquired = true;
                    TargetPosition = heatTarget.position + (2 * heatTarget.velocity * Time.fixedDeltaTime);
                    TargetVelocity = heatTarget.velocity;
                    TargetAcceleration = heatTarget.acceleration;
                    lockFailTimer = 0;
                }
                else
                {
                    TargetAcquired = false;
                    if (FlightGlobals.ready)
                    {
                        lockFailTimer += Time.fixedDeltaTime;
                    }
                }
            }
        }

        protected void SetAntiRadTargeting()
        {
            if (TargetingMode == TargetingModes.AntiRad && TargetAcquired)
            {
                RadarWarningReceiver.OnRadarPing += ReceiveRadarPing;
            }
        }

        protected void SetLaserTargeting()
        {
            if (TargetingMode == TargetingModes.Laser)
            {
                laserStartPosition = MissileReferenceTransform.position;
                if (lockedCamera)
                {
                    TargetAcquired = true;
                    TargetPosition = lastLaserPoint = lockedCamera.groundTargetPosition;
                    targetingPod = lockedCamera;
                }
            }
        }

        protected void UpdateLaserTarget()
        {
            if (TargetAcquired)
            {
                if (lockedCamera && lockedCamera.groundStabilized && !lockedCamera.gimbalLimitReached && lockedCamera.surfaceDetected) //active laser target
                {
                    TargetPosition = lockedCamera.groundTargetPosition;
                    TargetVelocity = (TargetPosition - lastLaserPoint) / Time.fixedDeltaTime;
                    TargetAcceleration = Vector3.zero;
                    lastLaserPoint = TargetPosition;

                    if (GuidanceMode == GuidanceModes.BeamRiding && TimeIndex > 0.25f && Vector3.Dot(GetForwardTransform(), part.transform.position - lockedCamera.transform.position) < 0)
                    {
                        TargetAcquired = false;
                        lockedCamera = null;
                    }
                }
                else //lost active laser target, home on last known position
                {
                    if (CMSmoke.RaycastSmoke(new Ray(transform.position, lastLaserPoint - transform.position)))
                    {
                        //Debug.Log("Laser missileBase affected by smoke countermeasure");
                        float angle = VectorUtils.FullRangePerlinNoise(0.75f * Time.time, 10) * BDArmorySettings.SMOKE_DEFLECTION_FACTOR;
                        TargetPosition = VectorUtils.RotatePointAround(lastLaserPoint, transform.position, VectorUtils.GetUpDirection(transform.position), angle);
                        TargetVelocity = Vector3.zero;
                        TargetAcceleration = Vector3.zero;
                        lastLaserPoint = TargetPosition;
                    }
                    else
                    {
                        TargetPosition = lastLaserPoint;
                    }
                }
            }
            else
            {
                ModuleTargetingCamera foundCam = null;
                bool parentOnly = (GuidanceMode == GuidanceModes.BeamRiding);
                foundCam = BDATargetManager.GetLaserTarget(this, parentOnly);
                if (foundCam != null && foundCam.cameraEnabled && foundCam.groundStabilized && BDATargetManager.CanSeePosition(foundCam.groundTargetPosition, vessel.transform.position, MissileReferenceTransform.position))
                {
                    Debug.Log("[BDArmory]: Laser guided missileBase actively found laser point. Enabling guidance.");
                    lockedCamera = foundCam;
                    TargetAcquired = true;
                }
            }
        }

        protected void UpdateRadarTarget()
        {
            TargetAcquired = false;

            float angleToTarget = Vector3.Angle(radarTarget.predictedPosition - transform.position, GetForwardTransform());

            if (radarTarget.exists)
            {
                // locked-on before launch, passive radar guidance or waiting till in active radar range:
                if (!ActiveRadar && ((radarTarget.predictedPosition - transform.position).sqrMagnitude > Mathf.Pow(activeRadarRange, 2) || angleToTarget > maxOffBoresight * 0.75f))
                {
                    if (vrd)
                    {
                        TargetSignatureData t = TargetSignatureData.noTarget;
                        List<TargetSignatureData> possibleTargets = vrd.GetLockedTargets();
                        for (int i = 0; i < possibleTargets.Count; i++)
                        {
                            if (possibleTargets[i].vessel == radarTarget.vessel)
                            {
                                t = possibleTargets[i];
                            }
                        }

                        if (t.exists)
                        {
                            TargetAcquired = true;
                            radarTarget = t;
                            TargetPosition = radarTarget.predictedPositionWithChaffFactor;
                            TargetVelocity = radarTarget.velocity;
                            TargetAcceleration = radarTarget.acceleration;
                            _radarFailTimer = 0;
                            return;
                        }
                        else
                        {
                            if (_radarFailTimer > maxRadarFailTime)
                            {
                                Debug.Log("[BDArmory]: Semi-Active Radar guidance failed. Parent radar lost target.");
                                radarTarget = TargetSignatureData.noTarget;
                                legacyTargetVessel = null;
                                return;
                            }
                            else
                            {
                                if (_radarFailTimer == 0)
                                {
                                    Debug.Log("[BDArmory]: Semi-Active Radar guidance failed - waiting for data");
                                }
                                _radarFailTimer += Time.fixedDeltaTime;
                                radarTarget.timeAcquired = Time.time;
                                radarTarget.position = radarTarget.predictedPosition;
                                TargetPosition = radarTarget.predictedPositionWithChaffFactor;
                                TargetVelocity = radarTarget.velocity;
                                TargetAcceleration = Vector3.zero;
                                TargetAcquired = true;
                            }
                        }
                    }
                    else
                    {
                        Debug.Log("[BDArmory]: Semi-Active Radar guidance failed. Out of range and no data feed.");
                        radarTarget = TargetSignatureData.noTarget;
                        legacyTargetVessel = null;
                        return;
                    }
                }
                else
                {
                    // active radar with target locked:
                    vrd = null;

                    if (angleToTarget > maxOffBoresight)
                    {
                        Debug.Log("[BDArmory]: Active Radar guidance failed.  Target is out of active seeker gimbal limits.");
                        radarTarget = TargetSignatureData.noTarget;
                        legacyTargetVessel = null;
                        return;
                    }
                    else
                    {
                        if (scannedTargets == null) scannedTargets = new TargetSignatureData[5];
                        TargetSignatureData.ResetTSDArray(ref scannedTargets);
                        Ray ray = new Ray(transform.position, radarTarget.predictedPosition - transform.position);
                        bool pingRWR = Time.time - lastRWRPing > 0.4f;
                        if (pingRWR) lastRWRPing = Time.time;
                        bool radarSnapshot = (snapshotTicker > 10);
                        if (radarSnapshot)
                        {
                            snapshotTicker = 0;
                        }
                        else
                        {
                            snapshotTicker++;
                        }

                        //RadarUtils.UpdateRadarLock(ray, lockedSensorFOV, activeRadarMinThresh, ref scannedTargets, 0.4f, pingRWR, RadarWarningReceiver.RWRThreatTypes.MissileLock, radarSnapshot);
                        RadarUtils.RadarUpdateMissileLock(ray, lockedSensorFOV, ref scannedTargets, 0.4f, this);

                        float sqrThresh = radarLOALSearching ? Mathf.Pow(500, 2) : Mathf.Pow(40, 2);

                        if (radarLOAL && radarLOALSearching && !radarSnapshot)
                        {
                            //only scan on snapshot interval
                        }
                        else
                        {
                            for (int i = 0; i < scannedTargets.Length; i++)
                            {
                                if (scannedTargets[i].exists && (scannedTargets[i].predictedPosition - radarTarget.predictedPosition).sqrMagnitude < sqrThresh)
                                {
                                    //re-check engagement envelope, only lock appropriate targets
                                    if (CheckTargetEngagementEnvelope(scannedTargets[i].targetInfo))
                                    {
                                        radarTarget = scannedTargets[i];
                                        TargetAcquired = true;
                                        radarLOALSearching = false;
                                        TargetPosition = radarTarget.predictedPositionWithChaffFactor + (radarTarget.velocity * Time.fixedDeltaTime);
                                        TargetVelocity = radarTarget.velocity;
                                        TargetAcceleration = radarTarget.acceleration;
                                        _radarFailTimer = 0;
                                        if (!ActiveRadar && Time.time - TimeFired > 1)
                                        {
                                            if (locksCount == 0)
                                            {
                                                if (weaponClass == WeaponClasses.SLW)
                                                    RadarWarningReceiver.PingRWR(ray, lockedSensorFOV, RadarWarningReceiver.RWRThreatTypes.Torpedo, 2f);
                                                else
                                                    RadarWarningReceiver.PingRWR(ray, lockedSensorFOV, RadarWarningReceiver.RWRThreatTypes.MissileLaunch, 2f);
                                                Debug.Log("[BDArmory]: Pitbull! Radar missilebase has gone active.  Radar sig strength: " + radarTarget.signalStrength.ToString("0.0"));
                                            }
                                            else if (locksCount > 2)
                                            {
                                                guidanceActive = false;
                                                checkMiss = true;
                                                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                                                {
                                                    Debug.Log("[BDArmory]: Active Radar guidance failed. Radar missileBase reached max re-lock attempts.");
                                                }
                                            }
                                            locksCount++;
                                        }
                                        ActiveRadar = true;
                                        return;
                                    }
                                }
                            }
                        }

                        if (radarLOAL)
                        {
                            radarLOALSearching = true;
                            TargetAcquired = true;
                            TargetPosition = radarTarget.predictedPositionWithChaffFactor + (radarTarget.velocity * Time.fixedDeltaTime);
                            TargetVelocity = radarTarget.velocity;
                            TargetAcceleration = Vector3.zero;
                            ActiveRadar = false;
                            _radarFailTimer = 0;
                        }
                        else
                        {
                            Debug.Log("[BDArmory]: Active Radar guidance failed.  No target locked.");
                            radarTarget = TargetSignatureData.noTarget;
                            legacyTargetVessel = null;
                            radarLOALSearching = false;
                            TargetAcquired = false;
                            ActiveRadar = false;
                        }
                    }
                }
            }
            else if (radarLOAL && radarLOALSearching)
            {
                // not locked on before launch, trying lock-on after launch:

                if (scannedTargets == null) scannedTargets = new TargetSignatureData[5];
                TargetSignatureData.ResetTSDArray(ref scannedTargets);
                Ray ray = new Ray(transform.position, GetForwardTransform());
                bool pingRWR = Time.time - lastRWRPing > 0.4f;
                if (pingRWR) lastRWRPing = Time.time;
                bool radarSnapshot = (snapshotTicker > 5);
                if (radarSnapshot)
                {
                    snapshotTicker = 0;
                }
                else
                {
                    snapshotTicker++;
                }

                //RadarUtils.UpdateRadarLock(ray, lockedSensorFOV * 3, activeRadarMinThresh * 2, ref scannedTargets, 0.4f, pingRWR, RadarWarningReceiver.RWRThreatTypes.MissileLock, radarSnapshot);
                RadarUtils.RadarUpdateMissileLock(ray, lockedSensorFOV * 3, ref scannedTargets, 0.4f, this);

                float sqrThresh = Mathf.Pow(300, 2);

                float smallestAngle = 360;
                TargetSignatureData lockedTarget = TargetSignatureData.noTarget;

                for (int i = 0; i < scannedTargets.Length; i++)
                {
                    if (scannedTargets[i].exists && (scannedTargets[i].predictedPosition - radarTarget.predictedPosition).sqrMagnitude < sqrThresh)
                    {
                        //re-check engagement envelope, only lock appropriate targets
                        if (CheckTargetEngagementEnvelope(scannedTargets[i].targetInfo))
                        {
                            float angle = Vector3.Angle(scannedTargets[i].predictedPosition - transform.position, GetForwardTransform());
                            if (angle < smallestAngle)
                            {
                                lockedTarget = scannedTargets[i];
                                smallestAngle = angle;
                            }

                            ActiveRadar = true;
                            return;
                        }
                    }
                }

                if (lockedTarget.exists)
                {
                    radarTarget = lockedTarget;
                    TargetAcquired = true;
                    radarLOALSearching = false;
                    TargetPosition = radarTarget.predictedPositionWithChaffFactor + (radarTarget.velocity * Time.fixedDeltaTime);
                    TargetVelocity = radarTarget.velocity;
                    TargetAcceleration = radarTarget.acceleration;

                    if (!ActiveRadar && Time.time - TimeFired > 1)
                    {
                        if (weaponClass == WeaponClasses.SLW)
                            RadarWarningReceiver.PingRWR(new Ray(transform.position, radarTarget.predictedPosition - transform.position), lockedSensorFOV, RadarWarningReceiver.RWRThreatTypes.Torpedo, 2f);
                        else
                            RadarWarningReceiver.PingRWR(new Ray(transform.position, radarTarget.predictedPosition - transform.position), lockedSensorFOV, RadarWarningReceiver.RWRThreatTypes.MissileLaunch, 2f);

                        Debug.Log("[BDArmory]: Pitbull! Radar missileBase has gone active.  Radar sig strength: " + radarTarget.signalStrength.ToString("0.0"));
                    }
                    return;
                }
                else
                {
                    TargetAcquired = true;
                    TargetPosition = transform.position + (startDirection * 500);
                    TargetVelocity = Vector3.zero;
                    TargetAcceleration = Vector3.zero;
                    radarLOALSearching = true;
                    _radarFailTimer += Time.fixedDeltaTime;
                    if (_radarFailTimer > maxRadarFailTime)
                    {
                        Debug.Log("[BDArmory]: Active Radar guidance failed. LOAL could not lock a target.");
                        radarTarget = TargetSignatureData.noTarget;
                        legacyTargetVessel = null;
                        radarLOALSearching = false;
                        TargetAcquired = false;
                        ActiveRadar = false;
                    }
                    return;
                }
            }

            if (!radarTarget.exists)
            {
                legacyTargetVessel = null;
            }
        }

        protected bool CheckTargetEngagementEnvelope(TargetInfo ti)
        {
            return (ti.isMissile && engageMissile) ||
                    (!ti.isMissile && ti.isFlying && engageAir) ||
                    ((ti.isLandedOrSurfaceSplashed || ti.isSplashed) && engageGround) ||
                    (ti.isUnderwater && engageSLW);
        }

        protected void ReceiveRadarPing(Vessel v, Vector3 source, RadarWarningReceiver.RWRThreatTypes type, float persistTime)
        {
            if (TargetingMode == TargetingModes.AntiRad && TargetAcquired && v == vessel)
            {
                if ((source - VectorUtils.GetWorldSurfacePostion(targetGPSCoords, vessel.mainBody)).sqrMagnitude < Mathf.Pow(maxStaticLaunchRange / 4, 2) //drastically increase update range for anti-radiation missile to track moving targets!
                    && Vector3.Angle(source - transform.position, GetForwardTransform()) < maxOffBoresight)
                {
                    TargetAcquired = true;
                    TargetPosition = source;
                    targetGPSCoords = VectorUtils.WorldPositionToGeoCoords(TargetPosition, vessel.mainBody);
                    TargetVelocity = Vector3.zero;
                    TargetAcceleration = Vector3.zero;
                    lockFailTimer = 0;
                }
            }
        }

        protected void UpdateAntiRadiationTarget()
        {
            if (!TargetAcquired)
            {
                guidanceActive = false;
                return;
            }

            if (FlightGlobals.ready)
            {
                if (lockFailTimer < 0)
                {
                    lockFailTimer = 0;
                }
                lockFailTimer += Time.fixedDeltaTime;
            }

            if (lockFailTimer > 8)
            {
                guidanceActive = false;
                TargetAcquired = false;
            }
            else
            {
                TargetPosition = VectorUtils.GetWorldSurfacePostion(targetGPSCoords, vessel.mainBody);
            }
        }

        public void DrawDebugLine(Vector3 start, Vector3 end, Color color = default(Color))
        {
            if (BDArmorySettings.DRAW_DEBUG_LINES)
            {
                if (!gameObject.GetComponent<LineRenderer>())
                {
                    LR = gameObject.AddComponent<LineRenderer>();
                    LR.material = new Material(Shader.Find("KSP/Emissive/Diffuse"));
                    LR.material.SetColor("_EmissiveColor", color);
                }
                else
                {
                    LR = gameObject.GetComponent<LineRenderer>();
                }
                LR.positionCount = 2;
                LR.SetPosition(0, start);
                LR.SetPosition(1, end);
            }
        }

        protected void CheckDetonationDistance()
        {
            if (DetonationDistanceState == DetonationDistanceStates.Detonate)
            {
                Debug.Log("[BDArmory]: Target detected inside sphere - detonating");

                Detonate();
            }
        }

        protected Vector3 CalculateAGMBallisticGuidance(MissileBase missile, Vector3 targetPosition)
        {
            if (this._guidance == null)
            {
                _guidance = new BallisticGuidance();
            }

            var newDirection = this._guidance.GetDirection(this, TargetPosition, out var timeToImpact);

            TimeToImpact = (float)timeToImpact;

            return newDirection;
        }

      
        
       

        protected void drawLabels()
        {
            if (vessel == null || !vessel.isActiveVessel) return;
            if (BDArmorySettings.DRAW_DEBUG_LABELS)
            {
                GUI.Label(new Rect(200, Screen.height - 200, 400, 400), this.shortName + ":" + debugString.ToString());
            }
        }

        public float GetTntMass()
        {
            return vessel.FindPartModulesImplementing<BDExplosivePart>().Max(x => x.tntMass);
        }

        public void CheckDetonationState()
        {
            //Guard clauses
            if (!TargetAcquired) return;

            var targetDistancePerFrame = TargetVelocity * Time.fixedDeltaTime;
            var missileDistancePerFrame = vessel.Velocity() * Time.fixedDeltaTime;

            var futureTargetPosition = (TargetPosition + targetDistancePerFrame);
            var futureMissilePosition = (vessel.CoM + missileDistancePerFrame);

            var relativeSpeed = (TargetVelocity - vessel.Velocity()).magnitude * Time.fixedDeltaTime;

            switch (DetonationDistanceState)
            {
                case DetonationDistanceStates.NotSafe:
                    //Lets check if we are at a safe distance from the source vessel

                    using (var hitsEnu = Physics.OverlapSphere(futureMissilePosition, GetBlastRadius() * 3f, 557057).AsEnumerable().GetEnumerator())
                    {
                        while (hitsEnu.MoveNext())
                        {
                            if (hitsEnu.Current == null) continue;
                            try
                            {
                                Part partHit = hitsEnu.Current.GetComponentInParent<Part>();

                                if (partHit?.vessel == SourceVessel)
                                {
                                    //We found a hit to the vessel
                                    return;
                                }
                            }
                            catch
                            {
                                // ignored
                            }
                        }
                    }

                    //We are safe and we can continue with the cruising phase

                    DetonationDistanceState = DetonationDistanceStates.Cruising;
                    break;

                case DetonationDistanceStates.Cruising:

                    if (Vector3.Distance(futureMissilePosition, futureTargetPosition) < GetBlastRadius() * 10)
                    {
                        //We are now close enough to start checking the detonation distance
                        DetonationDistanceState = DetonationDistanceStates.CheckingProximity;
                    }
                    else
                    {
                        BDModularGuidance bdModularGuidance = this as BDModularGuidance;

                        if (bdModularGuidance == null) return;

                        if (Vector3.Distance(futureMissilePosition, futureTargetPosition) > this.DetonationDistance) return;

                        DetonationDistanceState = DetonationDistanceStates.CheckingProximity;
                    }
                    break;

                case DetonationDistanceStates.CheckingProximity:

                    if (DetonationDistance == 0)
                    {
                        if (weaponClass == WeaponClasses.Bomb) return;

                        if (TimeIndex > 1f)
                        {
                            //Vector3 floatingorigin_current = FloatingOrigin.Offset;

                            Ray rayFuturePosition = new Ray(vessel.CoM, futureMissilePosition);

                            var hitsFuture = Physics.RaycastAll(rayFuturePosition, (float)missileDistancePerFrame.magnitude, 557057).AsEnumerable();

                            using (var hitsEnu = hitsFuture.GetEnumerator())
                            {
                                while (hitsEnu.MoveNext())
                                {
                                    RaycastHit hit = hitsEnu.Current;

                                    try
                                    {
                                        var hitPart = hit.collider.gameObject.GetComponentInParent<Part>();

                                        if (hitPart?.vessel != SourceVessel && hitPart?.vessel != vessel)
                                        {
                                            //We found a hit to other vessel
                                            vessel.SetPosition(hit.point);
                                            DetonationDistanceState = DetonationDistanceStates.Detonate;
                                            Detonate();
                                            return;
                                        }
                                    }
                                    catch
                                    {
                                        // ignored
                                    }
                                }
                            }
                        }

                        previousPos = part.transform.position;
                    }
                    else
                    {
                        float optimalDistance = (float)(Math.Max(DetonationDistance, relativeSpeed));

                        using (var hitsEnu = Physics.OverlapSphere(vessel.CoM, optimalDistance, 557057).AsEnumerable().GetEnumerator())
                        {
                            while (hitsEnu.MoveNext())
                            {
                                if (hitsEnu.Current == null) continue;

                                try
                                {
                                    Part partHit = hitsEnu.Current.GetComponentInParent<Part>();

                                    if (partHit?.vessel == vessel || partHit?.vessel == SourceVessel) continue;

                                    Debug.Log("[BDArmory]: Missile proximity sphere hit | Distance overlap = " + optimalDistance + "| Part name = " + partHit.name);

                                    //We found a hit a different vessel than ours
                                    DetonationDistanceState = DetonationDistanceStates.Detonate;
                                    return;
                                }
                                catch
                                {
                                    // ignored
                                }
                            }
                        }
                    }

                    break;
            }

            if (BDArmorySettings.DRAW_DEBUG_LABELS)
            {
                Debug.Log("[BDArmory]: DetonationDistanceState = : " + DetonationDistanceState);
            }
        }

        protected void SetInitialDetonationDistance()
        {
            if (this.DetonationDistance == -1)
            {
                if (GuidanceMode == GuidanceModes.AAMLead || GuidanceMode == GuidanceModes.AAMPure)
                {
                    DetonationDistance = GetBlastRadius() * 0.25f;
                }
                else
                {
                    //DetonationDistance = GetBlastRadius() * 0.05f;
                    DetonationDistance = 0f;
                }
            }
            if (BDArmorySettings.DRAW_DEBUG_LABELS)
            {
                Debug.Log("[BDArmory]: DetonationDistance = : " + DetonationDistance);
            }
        }

        protected void CollisionEnter(Collision col)
        {
            if (BDArmorySettings.DRAW_DEBUG_LABELS)
                Debug.Log("[BDArmory]: Missile Collided");

            if (TimeIndex > 2 && HasFired && col.collider.gameObject.GetComponentInParent<Part>().GetFireFX())
            {
                ContactPoint contact = col.contacts[0];
                Vector3 pos = contact.point;
                BulletHitFX.AttachFlames(pos, col.collider.gameObject.GetComponentInParent<Part>());
            }

            if (HasExploded || !HasFired) return;

            if (DetonationDistanceState != DetonationDistanceStates.CheckingProximity) return;

            Debug.Log("[BDArmory]: Missile Collided - Triggering Detonation");
            Detonate();
        }

        [KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "#LOC_BDArmory_ChangetoLowAltitudeRange", active = true)]//Change to Low Altitude Range
        public void CruiseAltitudeRange()
        {
            if (Events["CruiseAltitudeRange"].guiName == "Change to Low Altitude Range")
            {
                Events["CruiseAltitudeRange"].guiName = "Change to High Altitude Range";

                UI_FloatRange cruiseAltitudField = (UI_FloatRange)Fields["CruiseAltitude"].uiControlEditor;
                cruiseAltitudField.maxValue = 500f;
                cruiseAltitudField.minValue = 1f;
                cruiseAltitudField.stepIncrement = 5f;
            }
            else
            {
                Events["CruiseAltitudeRange"].guiName = "Change to Low Altitude Range";
                UI_FloatRange cruiseAltitudField = (UI_FloatRange)Fields["CruiseAltitude"].uiControlEditor;
                cruiseAltitudField.maxValue = 25000f;
                cruiseAltitudField.minValue = 500;
                cruiseAltitudField.stepIncrement = 500f;
            }
            this.part.RefreshAssociatedWindows();
        }
    }
}
