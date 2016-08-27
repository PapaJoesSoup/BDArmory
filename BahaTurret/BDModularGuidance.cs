using System;
using UniLinq;
using UnityEngine;

namespace BahaTurret
{
    public class BDModularGuidance : PartModule, IBDWeapon
    {
        private DateTime _firedTime;
        private bool _firedTriggered;
        private int _nextStage = (int) KSPActionGroup.Custom01;

        private Vessel _parentVessel;
        private bool _readyForGuidance;

        private Vessel _targetVessel;

        private Transform _velocityTransform;

        private Transform _vesselTransform;

        private PartModule _targetDecoupler;

        public bool HasFired;

        public Vessel LegacyTargetVessel;

        public AudioSource SfAudioSource;

        public Vessel SourceVessel;

        public MissileLauncher.TargetingModes TargetingMode = MissileLauncher.TargetingModes.Radar;

        public MissileFire TargetMf;

        public Vector3 TargetPosition = Vector3.zero;

        public bool Team;

        public float TimeToImpact;



        public WeaponClasses GetWeaponClass()
        {
            return WeaponClasses.Missile;
        }

        public string GetShortName()
        {
            return ShortName;
        }

        public string GetSubLabel()
        {
            return Enum.GetName(typeof(MissileLauncher.TargetingModes), MissileLauncher.TargetingModes.GPS);
        }

        public Part GetPart()
        {
            return part;
        }

        private void RefreshGuidanceMode()
        {
            switch (GuidanceMode)
            {
                case 1:
                    GuidanceLabel = "AAM";
                    break;
                case 2:
                    GuidanceLabel = "AGM/STS";
                    break;
                case 3:
                    GuidanceLabel = "Cruise";
                    break;
            }

            if (Fields["CruiseAltitude"] != null)
            {
                Fields["CruiseAltitude"].guiActive = GuidanceMode == 3;
                Fields["CruiseAltitude"].guiActiveEditor = GuidanceMode == 3;
            }
            

            Misc.RefreshAssociatedWindows(part);
        }

        public override void OnFixedUpdate()
        {
            base.OnFixedUpdate();

            CheckGuidanceInit();

            CheckDelayedFired();

            CheckDetonationDistance();

            CheckNextStage();
        }

        private void CheckDetonationDistance()
        {
            if (_targetVessel == null) return;
            if (!HasFired) return;
            if (DetonationDistance == 0) return;

            if (!((_targetVessel.transform.position - vessel.transform.position).magnitude <=
                  DetonationDistance)) return;

            foreach (var highExplosive in vessel.FindPartModulesImplementing<BDExplosivePart>())
            {
                highExplosive.Detonate();
            }
        }

        private void CheckNextStage()
        {
            if (HasFired && ShouldExecuteNextStage())
            {
                ExecuteNextStage();
            }
        }

        private void CheckDelayedFired()
        {
            if (_firedTriggered && !HasFired)
            {
                if (DateTime.Now - _firedTime > TimeSpan.FromSeconds(DropTime))
                {
                    MissileIgnition();
                }
            }
        }

        private void MissileIgnition()
        {
            StartGuidance();
            ExecuteNextStage();
            HasFired = true;
        }

        private void CheckGuidanceInit()
        {
            if (_readyForGuidance)
            {
                _readyForGuidance = false;
                GuidanceActive = true;
                part.vessel.SetReferenceTransform(part);

                var velocityObject = new GameObject("velObject");
                velocityObject.transform.position = transform.position;
                velocityObject.transform.parent = transform;
                _velocityTransform = velocityObject.transform;

                Events["StartGuidance"].guiActive = false;
                Misc.RefreshAssociatedWindows(part);
            }
        }


        private bool ShouldExecuteNextStage()
        {
            var ret = true;
            //If the next stage is greater than the number defined of stages the missile is done
            if (_nextStage >= 128*(StagesNumber + 1))
            {
                return false;
            }

            foreach (
                var engine in
                    vessel.parts.Where(IsEngine).Select(x => x.FindModuleImplementing<ModuleEngines>()))
            {
                if (engine.EngineIgnited && !engine.getFlameoutState)
                {
                    ret = false;
                    break;
                }
            }
            return ret;
        }

        public bool IsEngine(Part p)
        {
            for (var i = 0; i < p.Modules.Count; i++)
            {
                var m = p.Modules[i];
                if (m is ModuleEngines)
                    return true;
            }
            return false;
        }

        public override void OnStart(StartState state)
        {
            part.force_activate();

            UpdateVesselTransform();

            RefreshGuidanceMode();

            _targetDecoupler = FindFirstDecoupler(part.parent, null);

        }

        private void UpdateVesselTransform()
        {
            if (this.part.vessel != null && this.part.vessel.vesselTransform != null)
            {
                 _vesselTransform = part.vessel.vesselTransform;
                _parentVessel = vessel;

                part.SetReferenceTransform(_vesselTransform);
            }
        }


        public void GuidanceSteer(FlightCtrlState s)
        {
            if (GuidanceActive && _targetVessel != null && vessel != null && _vesselTransform != null &&
                _velocityTransform != null)
            {
                _velocityTransform.rotation = Quaternion.LookRotation(vessel.srf_velocity, -_vesselTransform.forward);
                var targetPosition = _targetVessel.CurrentCoM;
                var targetVelocity = _targetVessel.rb_velocity;
                var targetAcceleration = _targetVessel.acceleration;
                var localAngVel = vessel.angularVelocity;

                if (GuidanceMode == 1)
                {
                    targetPosition = MissileGuidance.GetAirToAirTarget(targetPosition, targetVelocity,
                        targetAcceleration, vessel, out TimeToImpact);
                }
                else if (GuidanceMode == 2)
                {
                    targetPosition = MissileGuidance.GetAirToGroundTarget(targetPosition, vessel, 1.85f);
                }
                else
                {
                    targetPosition = MissileGuidance.GetCruiseTarget(targetPosition, vessel, CruiseAltitude);
                }

                var targetDirection = _velocityTransform.InverseTransformPoint(targetPosition).normalized;
                targetDirection = Vector3.RotateTowards(Vector3.forward, targetDirection, 15*Mathf.Deg2Rad, 0);


                var steerYaw = SteerMult*targetDirection.x - SteerDamping*-localAngVel.z;
                var steerPitch = SteerMult*targetDirection.y - SteerDamping*-localAngVel.x;

                s.yaw = Mathf.Clamp(steerYaw, -MaxSteer, MaxSteer);
                s.pitch = Mathf.Clamp(steerPitch, -MaxSteer, MaxSteer);

                s.mainThrottle = 1;
            }
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
            part.vessel.OnFlyByWire -= GuidanceSteer;
            part.vessel.ActionGroups.ToggleGroup((KSPActionGroup) _nextStage);

            _nextStage *= 2;

            part.vessel.OnFlyByWire += GuidanceSteer;
        }

        #region KSP FIELDS

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "CruiseAltitude"),
         UI_FloatRange(minValue = 50f, maxValue = 1500f, stepIncrement = 50f, scene = UI_Scene.All)] public float
            CruiseAltitude = 500;

        public bool GuidanceActive;

        [KSPField(isPersistant = true, guiActive = true, guiName = "Guidance Type ", guiActiveEditor = true)] public
            string GuidanceLabel =
                "AGM/STS";

        [KSPField(isPersistant = true)] public int GuidanceMode = 2;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "SteerLimiter"),
         UI_FloatRange(minValue = .1f, maxValue = 1f, stepIncrement = .05f, scene = UI_Scene.All)] public float MaxSteer
             = 1;

        [KSPField] public string ShortName = "CustomMissile";

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "StagesNumber"),
         UI_FloatRange(minValue = 1f, maxValue = 5f, stepIncrement = 1f, scene = UI_Scene.All)] public float
            StagesNumber = 1;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Drop time"),
         UI_FloatRange(minValue = 0f, maxValue = 5f, stepIncrement = 0.1f, scene = UI_Scene.All)] public float
            DropTime;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Detonation distance"),
         UI_FloatRange(minValue = 0f, maxValue = 500f, stepIncrement = 1f, scene = UI_Scene.All)] public float
            DetonationDistance;


        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "SteerDamping"),
         UI_FloatRange(minValue = 0f, maxValue = 20f, stepIncrement = .05f, scene = UI_Scene.All)] public float
            SteerDamping = 5;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "SteerFactor"),
         UI_FloatRange(minValue = 0.1f, maxValue = 20f, stepIncrement = .1f, scene = UI_Scene.All)] public float
            SteerMult = 10;
        


        #endregion

        #region KSP ACTIONS

        [KSPAction("Start Guidance")]
        public void AgStartGuidance(KSPActionParam param)
        {
            StartGuidance();
        }

        [KSPAction("Fire Missile")]
        public void AgFire(KSPActionParam param)
        {
            FireMissile();
            if (BDArmorySettings.Instance.ActiveWeaponManager != null) BDArmorySettings.Instance.ActiveWeaponManager.UpdateList();
        }

        #endregion

        #region KSP EVENTS

        [KSPEvent(guiActive = true, guiActiveEditor = false, guiName = "Fire Missile", active = true)]
        public void FireMissile()
        {
            //BDATargetManager.FiredMissiles.Add(this);

            foreach (var wpm in vessel.FindPartModulesImplementing<MissileFire>())
            {
                Team = wpm.team;
                break;
            }

            SourceVessel = vessel;

            SetTargeting();


            Jettison();

            vessel.vesselName = GetShortName();
            vessel.vesselType = VesselType.Station;

            _firedTime = DateTime.Now;
            _firedTriggered = true;



        }

        private void SetTargeting()
        {
        }

        public Vector3 StartDirection { get; set; }

        [KSPEvent(guiActive = true, guiActiveEditor = false, guiName = "Start Guidance", active = true)]
        public void StartGuidance()
        {
            if (vessel.targetObject != null && vessel.targetObject.GetVessel() != null)
            {
                _targetVessel = vessel.targetObject.GetVessel();
            }
            else if (_parentVessel != null && _parentVessel.targetObject != null &&
                     _parentVessel.targetObject.GetVessel() != null)
            {
                _targetVessel = _parentVessel.targetObject.GetVessel();
            }
            else
            {
                return;
            }

            if (_targetVessel != null)
            {
                _readyForGuidance = true;
            }
        }

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "GuidanceMode", active = true)]
        public void SwitchGuidanceMode()
        {
            GuidanceMode++;
            if (GuidanceMode > 3)
            {
                GuidanceMode = 1;
            }

            RefreshGuidanceMode();
        }

        [KSPEvent(guiActive = true, guiActiveEditor = false, active = true, guiName = "Jettison")]
        public void Jettison()
        {

            if (_targetDecoupler == null || !_targetDecoupler || !(_targetDecoupler is IStageSeparator)) return;
            if (_targetDecoupler is ModuleDecouple)
            {
                (_targetDecoupler as ModuleDecouple).Decouple();
            }
            else
            {
                (_targetDecoupler as ModuleAnchoredDecoupler).Decouple();
            }

            if (BDArmorySettings.Instance.ActiveWeaponManager != null)
                BDArmorySettings.Instance.ActiveWeaponManager.UpdateList();
        }

        #endregion
    }
}