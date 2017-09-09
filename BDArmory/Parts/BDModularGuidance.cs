using System;
using System.Collections.Generic;
using BDArmory.Core.Extension;
using BDArmory.Misc;
using BDArmory.Radar;
using BDArmory.UI;
using KSP.UI.Screens;
using UniLinq;
using UnityEngine;

namespace BDArmory.Parts
{
    public class BDModularGuidance : MissileBase
    {
        
        private bool _missileIgnited;
        private int _nextStage = 1;

        private PartModule _targetDecoupler;

        private readonly Vessel _targetVessel = new Vessel();

        private Transform _velocityTransform;

        public Vessel LegacyTargetVessel;

        private readonly List<Part> _vesselParts = new List<Part>();

        #region KSP FIELDS

        [KSPField]
        public string ForwardTransform = "ForwardNegative";
        [KSPField]
        public string UpTransform = "RightPositive";

        [KSPField(isPersistant = true, guiActive = true, guiName = "Weapon Name ", guiActiveEditor = true), UI_Label(affectSymCounterparts = UI_Scene.All, scene = UI_Scene.All)]
        public string WeaponName;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "CruiseAltitude"), UI_FloatRange(minValue = 50f, maxValue = 1500f, stepIncrement = 50f, scene = UI_Scene.Editor, affectSymCounterparts = UI_Scene.All)]
        public float CruiseAltitude = 500;

        [KSPField(isPersistant = false, guiActive = true, guiName = "Guidance Type ", guiActiveEditor = true)]
        public string GuidanceLabel = "AGM/STS";

        [KSPField(isPersistant = true, guiActive = true, guiName = "Targeting Mode ", guiActiveEditor = true), UI_Label(affectSymCounterparts = UI_Scene.All, scene = UI_Scene.All)]
        private string _targetingLabel = TargetingModes.None.ToString();

        [KSPField(isPersistant = true)]
        public int GuidanceIndex = 2;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Active Radar Range"), UI_FloatRange(minValue = 6000f, maxValue = 50000f, stepIncrement = 1000f, scene = UI_Scene.Editor, affectSymCounterparts = UI_Scene.All)]
        public float ActiveRadarRange = 6000;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Steer Limiter"), UI_FloatRange(minValue = .1f, maxValue = 1f, stepIncrement = .05f, scene = UI_Scene.Editor, affectSymCounterparts = UI_Scene.All)]
        public float MaxSteer = 1;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Stages Number"), UI_FloatRange(minValue = 1f, maxValue = 5f, stepIncrement = 1f, scene = UI_Scene.Editor, affectSymCounterparts = UI_Scene.All)]
        public float StagesNumber = 1;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Stage to Trigger On Proximity"), UI_FloatRange(minValue = 0f, maxValue = 6f, stepIncrement = 1f, scene = UI_Scene.Editor, affectSymCounterparts = UI_Scene.All)]
        public float StageToTriggerOnProximity = 0;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Steer Damping"), UI_FloatRange(minValue = 0f, maxValue = 20f, stepIncrement = .05f, scene = UI_Scene.Editor, affectSymCounterparts = UI_Scene.All)]
        public float SteerDamping = 5;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Steer Factor"), UI_FloatRange(minValue = 0.1f, maxValue = 20f, stepIncrement = .1f, scene = UI_Scene.Editor, affectSymCounterparts = UI_Scene.All)]
        public float SteerMult = 10;

        #endregion

        public TransformAxisVectors ForwardTransformAxis { get; set; }
        public TransformAxisVectors UpTransformAxis { get; set; }

        public float Mass => (float) vessel.totalMass;

        public enum TransformAxisVectors
        {
            UpPositive,
            UpNegative,
            ForwardPositive,
            ForwardNegative,
            RightPositive,
            RightNegative
        }
        private void RefreshGuidanceMode()
        {
            switch (GuidanceIndex)
            {
                case 1:
                    GuidanceMode = GuidanceModes.AAMPure;
                    GuidanceLabel = "AAM";
                    break;
                case 2:
                    GuidanceMode = GuidanceModes.AGM;
                    GuidanceLabel = "AGM/STS";
                    break;
                case 3:
                    GuidanceMode = GuidanceModes.Cruise;
                    GuidanceLabel = "Cruise";
                    break;
                case 4:
                    GuidanceMode = GuidanceModes.AGMBallistic;
                    GuidanceLabel = "Ballistic";
                    break;
            }

            if (Fields["CruiseAltitude"] != null)
            {
                Fields["CruiseAltitude"].guiActive = GuidanceMode == GuidanceModes.Cruise;
                Fields["CruiseAltitude"].guiActiveEditor = GuidanceMode == GuidanceModes.Cruise;
            }
            if (Fields["BallisticOverShootFactor"] != null)
            {
                Fields["BallisticOverShootFactor"].guiActive = GuidanceMode == GuidanceModes.AGMBallistic;
                Fields["BallisticOverShootFactor"].guiActiveEditor = GuidanceMode == GuidanceModes.AGMBallistic;
            }

            Misc.Misc.RefreshAssociatedWindows(part);
        }
        public override void OnFixedUpdate()
        {
            if (HasFired && !HasExploded)
            {
                UpdateGuidance();
                CheckDetonationDistance();
                CheckDelayedFired();
                CheckNextStage();            

                if (isTimed && TimeIndex > detonationTime)
                {
                    AutoDestruction();
                } 
            }
        }

        private void CheckNextStage()
        {
            if (ShouldExecuteNextStage())
            {
                ExecuteNextStage();

            }
        }

        private void CheckDelayedFired()
        {
            if (_missileIgnited) return;
            if (TimeIndex > dropTime)
            {
                MissileIgnition();
            }
        }

        private void DisableRecursiveFlow(List<Part> children)
        {
            List<Part>.Enumerator child = children.GetEnumerator();
            while (child.MoveNext())
            {
                if (child.Current == null) continue;
                IEnumerator<PartResource> resource = child.Current.Resources.GetEnumerator();
                while (resource.MoveNext())
                {
                    if (resource.Current == null) continue;
                    if (resource.Current.flowState)
                    {
                        resource.Current.flowState = false;
                    }
                }
                resource.Dispose();

                if (child.Current.children.Count > 0)
                {
                    DisableRecursiveFlow(child.Current.children);
                }
                if (!_vesselParts.Contains(child.Current)) _vesselParts.Add(child.Current);
            }
            child.Dispose();
        }

        private void EnableResourceFlow(List<Part> children)
        {
            List<Part>.Enumerator child = children.GetEnumerator();
            while (child.MoveNext())
            {
                if (child.Current == null) continue;
                IEnumerator<PartResource> resource = child.Current.Resources.GetEnumerator();
                while (resource.MoveNext())
                {
                    if (resource.Current == null) continue;
                    if (!resource.Current.flowState)
                    {
                        resource.Current.flowState = true;
                    }
                }
                resource.Dispose();
                if (child.Current.children.Count > 0)
                {
                    EnableResourceFlow(child.Current.children);
                }
            }
            child.Dispose();
        }

        private void DisableResourcesFlow()
        {
            if (_targetDecoupler != null)
            {
                if (_targetDecoupler.part.children.Count == 0) return;
                _vesselParts.Clear();
                DisableRecursiveFlow(_targetDecoupler.part.children);

            }
        }

        private void MissileIgnition()
        {
            EnableResourceFlow(_vesselParts);
            GameObject velocityObject = new GameObject("velObject");
            velocityObject.transform.position = vessel.transform.position;
            velocityObject.transform.parent = vessel.transform;
            _velocityTransform = velocityObject.transform;

            MissileState = MissileStates.Boost;

            ExecuteNextStage();

            MissileState = MissileStates.Cruise;

            _missileIgnited = true;
            RadarWarningReceiver.WarnMissileLaunch(MissileReferenceTransform.position, GetForwardTransform());
        }

        private bool ShouldExecuteNextStage()
        {
            if (!_missileIgnited) return false;
            if (TimeIndex < 1) return false;

            // Replaced Linq expression...
            List<Part>.Enumerator parts = vessel.parts.GetEnumerator();
         
            while (parts.MoveNext())
            {
                if (parts.Current == null || !IsEngine(parts.Current)) continue;
                if (EngineIgnitedAndHasFuel(parts.Current))
                {
                    return false;
                }  
            }
            parts.Dispose();

            //If the next stage is greater than the number defined of stages the missile is done
            if (_nextStage > StagesNumber)
            {
                MissileState = MissileStates.PostThrust;
                return false;
            }
              
            return true;
        }

        public bool IsEngine(Part p)
        {
            List<PartModule>.Enumerator m = p.Modules.GetEnumerator();
            while (m.MoveNext())
            {
                if (m.Current == null) continue;
                if (m.Current is ModuleEngines) return true;
            }
            m.Dispose();
            return false;
        }

        public static bool EngineIgnitedAndHasFuel(Part p)
        {
            List<PartModule>.Enumerator m = p.Modules.GetEnumerator();
            
            while (m.MoveNext())
            {
                PartModule pm = m.Current;
                ModuleEngines eng = pm as ModuleEngines;
                if (eng != null)
                {
                    return (eng.EngineIgnited && (!eng.getFlameoutState || eng.flameoutBar == 0 || eng.status == "Nominal"));
                }
            }
            m.Dispose();
            return false;
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            SetupsFields();

            if (string.IsNullOrEmpty(GetShortName()))
            {
                shortName = "Unnamed";
            }
            part.force_activate();
            RefreshGuidanceMode();

            UpdateTargetingMode((TargetingModes)Enum.Parse(typeof(TargetingModes), _targetingLabel));

            _targetDecoupler = FindFirstDecoupler(part.parent, null);

            DisableResourcesFlow();

            weaponClass = WeaponClasses.Missile;
            WeaponName = GetShortName();

            activeRadarRange = ActiveRadarRange;

            if (this.DetonationDistance == -1)
            {
                this.DetonationDistance = GetBlastRadius();
            }


            //TODO: BDModularGuidance should be configurable?
            heatThreshold = 50;
            lockedSensorFOV = 5;
            radarLOAL = true;
        }

        private void SetupsFields()
        {
            Events["HideUI"].active = false;
            Events["ShowUI"].active = true;

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

            if (HighLogic.LoadedSceneIsEditor)
            {
                WeaponNameWindow.OnActionGroupEditorOpened.Add(OnActionGroupEditorOpened);
                WeaponNameWindow.OnActionGroupEditorClosed.Add(OnActionGroupEditorClosed);
                Fields["CruiseAltitude"].guiActiveEditor = true;
                Events["SwitchTargetingMode"].guiActiveEditor = true;
                Events["SwitchGuidanceMode"].guiActiveEditor = true;
            }
            else
            {
                Fields["CruiseAltitude"].guiActiveEditor = false;
                Events["SwitchTargetingMode"].guiActiveEditor = false;
                Events["SwitchGuidanceMode"].guiActiveEditor = false;
                SetMissileTransform();
                DisablingExplosives();

            }

            UI_FloatRange staticMin = (UI_FloatRange)Fields["minStaticLaunchRange"].uiControlEditor;
            UI_FloatRange staticMax = (UI_FloatRange)Fields["maxStaticLaunchRange"].uiControlEditor;
            UI_FloatRange radarMax = (UI_FloatRange)Fields["ActiveRadarRange"].uiControlEditor;

            staticMin.onFieldChanged += OnStaticRangeUpdated;
            staticMax.onFieldChanged += OnStaticRangeUpdated;
            staticMax.maxValue = BDArmorySettings.MAX_ENGAGEMENT_RANGE;
            staticMax.stepIncrement = BDArmorySettings.MAX_ENGAGEMENT_RANGE / 100;
            radarMax.maxValue = BDArmorySettings.MAX_ENGAGEMENT_RANGE;
            radarMax.stepIncrement = BDArmorySettings.MAX_ENGAGEMENT_RANGE / 100;

            UI_FloatRange stageOnProximity = (UI_FloatRange)Fields["StageToTriggerOnProximity"].uiControlEditor;
            stageOnProximity.onFieldChanged = OnStageOnProximity;


            OnStageOnProximity(Fields["StageToTriggerOnProximity"], null);
            InitializeEngagementRange(minStaticLaunchRange, maxStaticLaunchRange);
        }

        private void OnStageOnProximity(BaseField baseField, object o)
        {
            UI_FloatRange detonationDistance = (UI_FloatRange)Fields["DetonationDistance"].uiControlEditor;

            if (StageToTriggerOnProximity != 0)
            {
                detonationDistance = (UI_FloatRange) Fields["DetonationDistance"].uiControlEditor;

                detonationDistance.maxValue = 2000;

                detonationDistance.stepIncrement = 50;
            }
            else
            {
                detonationDistance.maxValue = 500;

                detonationDistance.stepIncrement = 10;
            }
        }

        private void OnStaticRangeUpdated(BaseField baseField, object o)
        {
            InitializeEngagementRange(minStaticLaunchRange, maxStaticLaunchRange);
        }

        private void DisablingExplosives()
        {
            vessel.FindPartModulesImplementing<BDExplosivePart>().Where( exp => exp != null && exp ).Select(exp => exp.Armed = false);
        }

        private void ArmingExplosive()
        {
            vessel.FindPartModulesImplementing<BDExplosivePart>().Where(exp => exp != null && exp).Select(exp => exp.Armed = true);
        }

        private void UpdateTargetingMode(TargetingModes newTargetingMode)
        {
            if (newTargetingMode == TargetingModes.Radar)
            {
                Fields["ActiveRadarRange"].guiActive = true;
                Fields["ActiveRadarRange"].guiActiveEditor = true;
            }
            else
            {
                Fields["ActiveRadarRange"].guiActive = false;
                Fields["ActiveRadarRange"].guiActiveEditor = false;
            }
            TargetingMode = newTargetingMode;
            _targetingLabel = newTargetingMode.ToString();

            Misc.Misc.RefreshAssociatedWindows(part);
        }


        private void OnDestroy()
        {
            WeaponNameWindow.OnActionGroupEditorOpened.Remove(OnActionGroupEditorOpened);
            WeaponNameWindow.OnActionGroupEditorClosed.Remove(OnActionGroupEditorClosed);
        }

        private void SetMissileTransform()
        {
            MissileReferenceTransform = part.transform;
            ForwardTransformAxis = (TransformAxisVectors) Enum.Parse(typeof(TransformAxisVectors), ForwardTransform);
            UpTransformAxis = (TransformAxisVectors)Enum.Parse(typeof(TransformAxisVectors), UpTransform);
        }      

        void UpdateGuidance()
        {
            if (guidanceActive)
            {
                switch (TargetingMode)
                {
                    case TargetingModes.None:
                        if (_targetVessel != null)
                        {
                            TargetPosition = _targetVessel.CurrentCoM;
                            TargetVelocity = _targetVessel.Velocity();
                            TargetAcceleration = _targetVessel.acceleration;
                        }
                        break;
                    case TargetingModes.Radar:
                        UpdateRadarTarget();
                        break;
                    case TargetingModes.Heat:
                        UpdateHeatTarget();
                        break;
                    case TargetingModes.Laser:
                        UpdateLaserTarget();
                        break;
                    case TargetingModes.Gps:
                         UpdateGPSTarget();
                        break;
                    case TargetingModes.AntiRad:
                        UpdateAntiRadiationTarget();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
           
        }        

        private  Vector3 previousTargetVelocity { get; set; } = Vector3.zero;
        private Vector3 previousMissileVelocity { get; set; } = Vector3.zero;

        private Vector3 AAMGuidance()
        {
            Vector3 aamTarget;
            if (TargetAcquired)
            {
                float timeToImpact;
                aamTarget = MissileGuidance.GetAirToAirTargetModular(TargetPosition, TargetVelocity, previousTargetVelocity, TargetAcceleration, vessel, previousMissileVelocity, out timeToImpact);
                previousTargetVelocity = TargetVelocity;
                previousMissileVelocity = vessel.Velocity();
                TimeToImpact = timeToImpact;
                if (Vector3.Angle(aamTarget - vessel.CoM, vessel.transform.forward) > maxOffBoresight * 0.75f)
                {
                    Debug.LogFormat("[BDArmory]: Missile with Name={0} has exceeded the max off boresight, checking missed target ",vessel.vesselName);
                    aamTarget = TargetPosition;
                }
                DrawDebugLine(vessel.CoM, aamTarget);
            }
            else
            {
                aamTarget = vessel.CoM + (20 * vessel.srfSpeed * vessel.Velocity().normalized);
            }

            return aamTarget;
        }

        private Vector3 AGMGuidance()
        {
            if (TargetingMode != TargetingModes.Gps)
            {
                if (TargetAcquired)
                {
                    //lose lock if seeker reaches gimbal limit
                    float targetViewAngle = Vector3.Angle(vessel.transform.forward, TargetPosition - vessel.CoM);

                    if (targetViewAngle > maxOffBoresight)
                    {
                        Debug.Log("[BDArmory]: AGM Missile guidance failed - target out of view");
                        guidanceActive = false;
                    }
                }
                else
                {
                    if (TargetingMode == TargetingModes.Laser)
                    {
                        //keep going straight until found laser point
                        TargetPosition = laserStartPosition + (20000 * startDirection);
                    }
                }
            }
            Vector3 agmTarget = MissileGuidance.GetAirToGroundTarget(TargetPosition, vessel, 1.85f);
            return agmTarget;
        }

        private Vector3 CruiseGuidance()
        {
            Vector3 cruiseTarget = Vector3.zero;
            float distance = Vector3.Distance(TargetPosition, vessel.CoM);

            if (distance < 4500)
            {
                cruiseTarget = MissileGuidance.GetAirToGroundTarget(TargetPosition, vessel, 1.85f);
                debugString += "\nDescending On Target";
            }
            else
            {
                cruiseTarget = MissileGuidance.GetCruiseTarget(TargetPosition, vessel, CruiseAltitude);
                debugString += "\nCruising";
            }

            debugString += "\nRadarAlt: " + MissileGuidance.GetRadarAltitude(vessel);

            return cruiseTarget;
        }

        private void CheckMiss(Vector3 targetPosition)
        {
            if (HasMissed) return;
            // if I'm to close to my vessel avoid explosion
            if (Vector3.Distance(vessel.CoM, SourceVessel.CoM) < 4 * DetonationDistance) return;
            // if I'm getting closer to  my target avoid explosion
            if (Vector3.Distance(vessel.CoM, targetPosition) >
                Vector3.Distance(vessel.CoM + (vessel.Velocity() * Time.fixedDeltaTime), targetPosition + (TargetVelocity * Time.fixedDeltaTime))) return;

            if (MissileState != MissileStates.PostThrust) return;
            if (Vector3.Dot(targetPosition - vessel.CoM, vessel.transform.forward) > 0) return;


            Debug.Log("[BDArmory]: Missile CheckMiss showed miss");
            HasMissed = true;
            guidanceActive = false;
            TargetMf = null;
            isTimed = true;
            detonationTime = TimeIndex + 1.5f;
        }

        public void GuidanceSteer(FlightCtrlState s)
        {
            debugString = "";
            if (guidanceActive && MissileReferenceTransform != null && _velocityTransform != null)
            {
                Vector3 newTargetPosition = new Vector3();
                if (GuidanceIndex == 1)
                {
                    newTargetPosition = AAMGuidance();
                    CheckMiss(newTargetPosition);

                }
                else if (GuidanceIndex == 2)
                {
                    newTargetPosition = AGMGuidance();
                }
                else if (GuidanceIndex == 3)
                {
                    newTargetPosition = CruiseGuidance();
                }
                else if (GuidanceIndex == 4)
                {
                    newTargetPosition = BallisticGuidance();
                }

                //Updating aero surfaces
                if (TimeIndex > dropTime + 0.5f)
                {
                    _velocityTransform.rotation = Quaternion.LookRotation(vessel.Velocity(), -vessel.transform.forward);
                    Vector3 targetDirection = _velocityTransform.InverseTransformPoint(newTargetPosition).normalized;
                    targetDirection = Vector3.RotateTowards(Vector3.forward, targetDirection, 15*Mathf.Deg2Rad, 0);

                    Vector3 localAngVel = vessel.angularVelocity;
                    float steerYaw = SteerMult*targetDirection.x - SteerDamping*-localAngVel.z;
                    float steerPitch = SteerMult*targetDirection.y - SteerDamping*-localAngVel.x;

                    s.yaw = Mathf.Clamp(steerYaw, -MaxSteer, MaxSteer);
                    s.pitch = Mathf.Clamp(steerPitch, -MaxSteer, MaxSteer);
                }

                s.mainThrottle = Throttle;
            }
           
        }

        private Vector3 BallisticGuidance()
        {
            return CalculateAGMBallisticGuidance(this, TargetPosition);
        }

        private void UpdateMenus(bool visible)
        {
            Events["HideUI"].active = visible;
            Events["ShowUI"].active = !visible;
        }

        private void OnActionGroupEditorOpened()
        {
            Events["HideUI"].active = false;
            Events["ShowUI"].active = false;
        }

        private void OnActionGroupEditorClosed()
        {
            Events["HideUI"].active = false;
            Events["ShowUI"].active = true;
        }

        /// <summary>
        ///     Recursive method to find the top decoupler that should be used to jettison the missile.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="last"></param>
        /// <returns></returns>
        private PartModule FindFirstDecoupler(Part parent, PartModule last)
        {
            if (parent == null || !parent) return last;

            PartModule newModuleDecouple = parent.FindModuleImplementing<ModuleDecouple>();
            if (newModuleDecouple == null)
            {
                newModuleDecouple = parent.FindModuleImplementing<ModuleAnchoredDecoupler>();
            }
            if (newModuleDecouple != null && newModuleDecouple)
            {
                return FindFirstDecoupler(parent.parent, newModuleDecouple);
            }
            return FindFirstDecoupler(parent.parent, last);
        }

        /// <summary>
        ///     This method will execute the next ActionGroup. Due to StageManager is designed to work with an active vessel
        ///     And a missile is not an active vessel. I had to use a different way handle stages. And action groups works perfect!
        /// </summary>
        public void ExecuteNextStage()
        {
            Debug.LogFormat("[BDArmory]: BDModularGuidance - executing next stage {0}", _nextStage);
            vessel.ActionGroups.ToggleGroup(
                (KSPActionGroup)Enum.Parse(typeof(KSPActionGroup), "Custom0" + (int)_nextStage));

            _nextStage++;

            vessel.OnFlyByWire += GuidanceSteer;

            //todo: find a way to fly by wire vessel decoupled
        }

        void OnGUI()
        {
            if (!HighLogic.LoadedSceneIsEditor)
            {
                drawLabels(); 
            }
        }

        #region KSP ACTIONS
        [KSPAction("Fire Missile")]
        public void AgFire(KSPActionParam param)
        {
            FireMissile();
        }

        #endregion

        #region KSP EVENTS

        [KSPEvent(guiActive = true, guiActiveEditor = false, guiName = "Fire Missile", active = true)]
        public override void FireMissile()
        {
            if (!HasFired)
            {
                if (BDArmorySettings.Instance.ActiveWeaponManager != null)
                {
                     BDArmorySettings.Instance.ActiveWeaponManager.UpdateList();
                }                   

                GameEvents.onPartDie.Add(PartDie);
                BDATargetManager.FiredMissiles.Add(this);

                List<MissileFire>.Enumerator wpm = vessel.FindPartModulesImplementing<MissileFire>().GetEnumerator();
                while (wpm.MoveNext())
                {
                    if (wpm.Current == null) continue;
                    Team = wpm.Current.team;
                    break;
                }
                wpm.Dispose();

                SourceVessel = vessel;
                SetTargeting();
                Jettison();
                AddTargetInfoToVessel();

                vessel.vesselName = GetShortName();
                vessel.vesselType = VesselType.Plane;

                if (!vessel.ActionGroups[KSPActionGroup.SAS])
                {
                    vessel.ActionGroups.ToggleGroup(KSPActionGroup.SAS);
                }

                TimeFired = Time.time;
                MissileState = MissileStates.Drop;

                Misc.Misc.RefreshAssociatedWindows(part);

                if (StageToTriggerOnProximity == 0)
                {
                     ArmingExplosive();
                }
               
                HasFired = true;
            }
        }

        private void SetTargeting()
        {
            startDirection = GetForwardTransform();
            SetLaserTargeting();
            SetAntiRadTargeting();
        }

        void OnDisable()
        {
            if (TargetingMode == TargetingModes.AntiRad)
            {
                RadarWarningReceiver.OnRadarPing -= ReceiveRadarPing;
            }
        }

        public Vector3 StartDirection { get; set; }

        [KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "Guidance Mode", active = true)]
        public void SwitchGuidanceMode()
        {
            GuidanceIndex++;
            if (GuidanceIndex > 4)
            {
                GuidanceIndex = 1;
            }

            RefreshGuidanceMode();
        }

        [KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "Targeting Mode", active = true)]
        public void SwitchTargetingMode()
        {
            string[] targetingModes = Enum.GetNames(typeof(TargetingModes));

            int currentIndex = targetingModes.IndexOf(TargetingMode.ToString());

            if (currentIndex < targetingModes.Length - 1)
            {
                UpdateTargetingMode((TargetingModes) Enum.Parse(typeof(TargetingModes), targetingModes[currentIndex + 1]));
            }
            else
            {
                UpdateTargetingMode((TargetingModes) Enum.Parse(typeof(TargetingModes), targetingModes[0]));
            }
        }


        [KSPEvent(guiActive = true, guiActiveEditor = false, active = true, guiName = "Jettison")]
        public override void Jettison()
        {
            if (_targetDecoupler == null || !_targetDecoupler || !(_targetDecoupler is IStageSeparator)) return;


            ModuleDecouple decouple = _targetDecoupler as ModuleDecouple;
            if (decouple != null)
            {
                decouple.ejectionForce *= 5; 
                 decouple.Decouple();
            }
            else
            {
                ((ModuleAnchoredDecoupler) _targetDecoupler).ejectionForce *= 5;
                ((ModuleAnchoredDecoupler) _targetDecoupler).Decouple();
            }

            if (BDArmorySettings.Instance.ActiveWeaponManager != null)
                BDArmorySettings.Instance.ActiveWeaponManager.UpdateList();
        }

     
        public override float GetBlastRadius()
        {
            if (vessel.FindPartModulesImplementing<BDExplosivePart>().Count > 0)
            {
                return vessel.FindPartModulesImplementing<BDExplosivePart>().Max(x => x.blastRadius);
            }
            else
            {
                return 5;
            }
        }

        protected override void PartDie(Part p)
        {
            if (p != part) return;
            AutoDestruction();
            BDATargetManager.FiredMissiles.Remove(this);
            GameEvents.onPartDie.Remove(PartDie);
        }

        private void AutoDestruction()
        {
            List<Part>.Enumerator vesselPart = vessel.Parts.GetEnumerator();
            while (vesselPart.MoveNext())
            {
                if (vesselPart.Current == null) continue;
                vesselPart.Current.SetDamage(vesselPart.Current.maxTemp * 2);
            }
            vesselPart.Dispose();
        }

        public override void Detonate()
        {
            if (HasExploded || !HasFired) return;
            if (SourceVessel == null) SourceVessel = vessel;

                if (StageToTriggerOnProximity != 0)
                {
                    ArmingExplosive();

                    vessel.ActionGroups.ToggleGroup(
                        (KSPActionGroup) Enum.Parse(typeof(KSPActionGroup), "Custom0" + (int)StageToTriggerOnProximity));
                }
                else
                {
                    vessel.FindPartModulesImplementing<BDExplosivePart>().ForEach(explosivePart => explosivePart.DetonateIfPossible());
                    AutoDestruction();
                }
                
               
            HasExploded = true;
        }

        public override Vector3 GetForwardTransform()
        {
            return GetTransform(ForwardTransformAxis);
        }

        public Vector3 GetTransform(TransformAxisVectors transformAxis)
        {
            switch (transformAxis)
            {
                case TransformAxisVectors.UpPositive:
                    return MissileReferenceTransform.up;
                case TransformAxisVectors.UpNegative:
                    return -MissileReferenceTransform.up;
                case TransformAxisVectors.ForwardPositive:
                    return MissileReferenceTransform.forward;
                case TransformAxisVectors.ForwardNegative:
                    return -MissileReferenceTransform.forward;
                case TransformAxisVectors.RightNegative:
                    return -MissileReferenceTransform.right;
                case TransformAxisVectors.RightPositive:
                    return MissileReferenceTransform.right;
                default:
                    return MissileReferenceTransform.forward;
            }
        }


        [KSPEvent(guiActiveEditor = true, guiName = "Hide Weapon Name UI", active = false)]
        public void HideUI()
        {
            WeaponNameWindow.HideGUI();
            UpdateMenus(false);
        }

        [KSPEvent(guiActiveEditor = true, guiName = "Set Weapon Name UI", active = false)]
        public void ShowUI()
        {
            WeaponNameWindow.ShowGUI(this);
            UpdateMenus(true);
        }

        void OnCollisionEnter(Collision col)
        {
            Debug.Log("[BDArmory]: Something Collided");

            if (TimeIndex> 1 && this.part.vessel.speed > 10)
            {
                Detonate();
            }
        }

        #endregion
    }

    #region UI
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class WeaponNameWindow : MonoBehaviour
    {
        internal static EventVoid OnActionGroupEditorOpened = new EventVoid("OnActionGroupEditorOpened");
        internal static EventVoid OnActionGroupEditorClosed = new EventVoid("OnActionGroupEditorClosed");

        private static GUIStyle unchanged;
        private static GUIStyle changed;
        private static GUIStyle greyed;
        private static GUIStyle overfull;

        private static WeaponNameWindow instance;
        private static Vector3 mousePos = Vector3.zero;

        private bool ActionGroupMode;

        private Rect guiWindowRect = new Rect(0, 0, 0, 0);

        private BDModularGuidance missile_module;

        [KSPField] public int offsetGUIPos = -1;

        private Vector2 scrollPos;

        [KSPField(isPersistant = false, guiActiveEditor = true, guiActive = false, guiName = "Show Weapon Name Editor"), UI_Toggle(enabledText = "Weapon Name GUI", disabledText = "GUI")] [NonSerialized] public bool showRFGUI;

        private bool styleSetup;

        private string txtName = string.Empty;

        public static void HideGUI()
        {
            if (instance != null && instance.missile_module != null)
            {
                instance.missile_module.WeaponName = instance.missile_module.shortName;
                instance.missile_module = null;
                instance.UpdateGUIState();
            }
            EditorLogic editor = EditorLogic.fetch;
            if (editor != null)
                editor.Unlock("BD_MN_GUILock");
        }

        public static void ShowGUI(BDModularGuidance missile_module)
        {
            if (instance != null)
            {
                instance.missile_module = missile_module;
                instance.UpdateGUIState();
            }
        }

        private void UpdateGUIState()
        {
            enabled = missile_module != null;
            EditorLogic editor = EditorLogic.fetch;
            if (!enabled && editor != null)
                editor.Unlock("BD_MN_GUILock");
        }

        private IEnumerator<YieldInstruction> CheckActionGroupEditor()
        {
            while (EditorLogic.fetch == null)
            {
                yield return null;
            }
            EditorLogic editor = EditorLogic.fetch;
            while (EditorLogic.fetch != null)
            {
                if (editor.editorScreen == EditorScreen.Actions)
                {
                    if (!ActionGroupMode)
                    {
                        HideGUI();
                        OnActionGroupEditorOpened.Fire();
                    }
                    EditorActionGroups age = EditorActionGroups.Instance;
                    if (missile_module && !age.GetSelectedParts().Contains(missile_module.part))
                    {
                        HideGUI();
                    }
                    ActionGroupMode = true;
                }
                else
                {
                    if (ActionGroupMode)
                    {
                        HideGUI();
                        OnActionGroupEditorClosed.Fire();
                    }
                    ActionGroupMode = false;
                }
                yield return null;
            }
        }

        private void Awake()
        {
            enabled = false;
            instance = this;
            StartCoroutine(CheckActionGroupEditor());
        }

        private void OnDestroy()
        {
            instance = null;
        }

        public void OnGUI()
        {
            if (!styleSetup)
            {
                styleSetup = true;
                Styles.InitStyles();
            }

            EditorLogic editor = EditorLogic.fetch;
            if (!HighLogic.LoadedSceneIsEditor || !editor)
            {
                return;
            }
            bool cursorInGUI = false; // nicked the locking code from Ferram
            mousePos = Input.mousePosition; //Mouse location; based on Kerbal Engineer Redux code
            mousePos.y = Screen.height - mousePos.y;

            int posMult = 0;
            if (offsetGUIPos != -1)
            {
                posMult = offsetGUIPos;
            }
            if (ActionGroupMode)
            {
                if (guiWindowRect.width == 0)
                {
                    guiWindowRect = new Rect(430*posMult, 365, 438, 50);
                }
                new Rect(guiWindowRect.xMin + 440, mousePos.y - 5, 300, 20);
            }
            else
            {
                if (guiWindowRect.width == 0)
                {
                    //guiWindowRect = new Rect(Screen.width - 8 - 430 * (posMult + 1), 365, 438, (Screen.height - 365));
                    guiWindowRect = new Rect(Screen.width - 8 - 430*(posMult + 1), 365, 438, 50);
                }
                new Rect(guiWindowRect.xMin - (230 - 8), mousePos.y - 5, 220, 20);
            }
            cursorInGUI = guiWindowRect.Contains(mousePos);
            if (cursorInGUI)
            {
                editor.Lock(false, false, false, "BD_MN_GUILock");
                //if (EditorTooltip.Instance != null)
                //    EditorTooltip.Instance.HideToolTip();
            }
            else
            {
                editor.Unlock("BD_MN_GUILock");
            }
            guiWindowRect = GUILayout.Window(GetInstanceID(), guiWindowRect, GUIWindow, "Weapon Name GUI", Styles.styleEditorPanel);
        }

        public void GUIWindow(int windowID)
        {
            InitializeStyles();

            GUILayout.BeginVertical();
            GUILayout.Space(20);

            GUILayout.BeginHorizontal();

            GUILayout.Label("Weapon Name: ");


            txtName = GUILayout.TextField(txtName);


            if (GUILayout.Button("Save & Close"))
            {
                missile_module.WeaponName = txtName;
                missile_module.shortName = txtName;
                instance.missile_module.HideUI();
            }

            GUILayout.EndHorizontal();

            scrollPos = GUILayout.BeginScrollView(scrollPos);

            GUILayout.EndScrollView();

            GUILayout.EndVertical();

            GUI.DragWindow();
        }

        private static void InitializeStyles()
        {
            if (unchanged == null)
            {
                if (GUI.skin == null)
                {
                    unchanged = new GUIStyle();
                    changed = new GUIStyle();
                    greyed = new GUIStyle();
                    overfull = new GUIStyle();
                }
                else
                {
                    unchanged = new GUIStyle(GUI.skin.textField);
                    changed = new GUIStyle(GUI.skin.textField);
                    greyed = new GUIStyle(GUI.skin.textField);
                    overfull = new GUIStyle(GUI.skin.label);
                }

                unchanged.normal.textColor = Color.white;
                unchanged.active.textColor = Color.white;
                unchanged.focused.textColor = Color.white;
                unchanged.hover.textColor = Color.white;

                changed.normal.textColor = Color.yellow;
                changed.active.textColor = Color.yellow;
                changed.focused.textColor = Color.yellow;
                changed.hover.textColor = Color.yellow;

                greyed.normal.textColor = Color.gray;

                overfull.normal.textColor = Color.red;
            }
        }
    }

    internal class Styles
    {
        // Base styles
        public static GUIStyle styleEditorTooltip;
        public static GUIStyle styleEditorPanel;


        /// <summary>
        ///     This one sets up the styles we use
        /// </summary>
        internal static void InitStyles()
        {
            styleEditorTooltip = new GUIStyle();
            styleEditorTooltip.name = "Tooltip";
            styleEditorTooltip.fontSize = 12;
            styleEditorTooltip.normal.textColor = new Color32(207, 207, 207, 255);
            styleEditorTooltip.stretchHeight = true;
            styleEditorTooltip.wordWrap = true;
            styleEditorTooltip.normal.background = CreateColorPixel(new Color32(7, 54, 66, 200));
            styleEditorTooltip.border = new RectOffset(3, 3, 3, 3);
            styleEditorTooltip.padding = new RectOffset(4, 4, 6, 4);
            styleEditorTooltip.alignment = TextAnchor.MiddleLeft;

            styleEditorPanel = new GUIStyle();
            styleEditorPanel.normal.background = CreateColorPixel(new Color32(7, 54, 66, 200));
            styleEditorPanel.border = new RectOffset(27, 27, 27, 27);
            styleEditorPanel.padding = new RectOffset(10, 10, 10, 10);
            styleEditorPanel.normal.textColor = new Color32(147, 161, 161, 255);
            styleEditorPanel.fontSize = 12;
        }


        /// <summary>
        ///     Creates a 1x1 texture
        /// </summary>
        /// <param name="Background">Color of the texture</param>
        /// <returns></returns>
        internal static Texture2D CreateColorPixel(Color32 Background)
        {
            Texture2D retTex = new Texture2D(1, 1);
            retTex.SetPixel(0, 0, Background);
            retTex.Apply();
            return retTex;
        }
    }

    #endregion
}