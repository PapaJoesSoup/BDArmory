using System.Linq;

namespace BDArmory.Modules
{
    public class ModuleDrainEC : PartModule
    {
        public bool armed = true;
        private bool setup = true;
        private int minCrew = 0;

        public override void OnStart(StartState state)
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                part.force_activate();
                vessel.OnFlyByWire += ThrottleControl;
                part.OnJustAboutToBeDestroyed += EnableVessel;
            }
            base.OnStart(state);
        }

        public void Update()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (armed)
                {
                    DisableVessel();
                }
                else
                {
                    EnableVessel();
                }
            }
        }

        void ThrottleControl(FlightCtrlState s)
        {
            this.vessel.Autopilot.Enabled = false;
            s.mainThrottle = 0;
            s.rollTrim += 0.1f;
            s.wheelSteerTrim += 0.1f;
            s.yawTrim += 0.1f;
            s.yaw += 0.1f;
            s.roll += 0.1f;
            s.pitch -= 0.1f;
            s.pitchTrim -= 0.1f;
        }

        private void EnableVessel()
        {
            foreach (Part p in vessel.parts)
            {
                var command = p.FindModuleImplementing<ModuleCommand>();

                if (command != null)
                {
                    //command.minimumCrew = minCrew;
                }

                foreach (ModuleEnginesFX engineFX in p.Modules)
                {
                    engineFX.allowRestart = true;
                }

                foreach (ModuleEngines engine in p.Modules)
                {
                    engine.allowRestart = true;
                }
            }

            Destroy(this);
        }

        private void DisableVessel()
        {
            vessel.OnFlyByWire -= ThrottleControl;
            vessel.OnFlyByWire += ThrottleControl;

            if (setup)
            {
                setup = false;

                foreach (Part p in this.vessel.parts)
                {
                    var camera = p.FindModuleImplementing<ModuleTargetingCamera>();
                    var radar = p.FindModuleImplementing<ModuleRadar>();
                    var spaceRadar = p.FindModuleImplementing<ModuleSpaceRadar>();

                    if (radar != null)
                    {
                        if (radar.radarEnabled)
                        {
                            radar.DisableRadar();
                        }
                    }

                    if (spaceRadar != null)
                    {
                        if (spaceRadar.radarEnabled)
                        {
                            spaceRadar.DisableRadar();
                        }
                    }

                    if (camera != null)
                    {
                        if (camera.cameraEnabled)
                        {
                            camera.DisableCamera();
                        }
                    }

                    var command = p.FindModuleImplementing<ModuleCommand>();
                    var weapon = p.FindModuleImplementing<ModuleWeapon>();
                    var turret = p.FindModuleImplementing<ModuleTurret>();

                    if (turret != null)
                    {
                        turret.maxPitch = 0;
                        turret.yawRange = 0;
                        turret.yawRangeLimit = 0;
                    }

                    if (weapon != null)
                    {
                        weapon.onlyFireInRange = true;
                        weapon.engageRangeMax = 0;
                        weapon.engageRangeMin = 0;
                        weapon.engageSLW = false;
                        weapon.engageMissile = false;
                        weapon.engageGround = false;
                        weapon.engageAir = false;
                    }

                    if (command != null)
                    {
                        minCrew = command.minimumCrew;
                        //command.minimumCrew = 100;
                    }

                    var PAI = this.vessel.FindPartModuleImplementing<BDModulePilotAI>();
                    var SAI = this.vessel.FindPartModuleImplementing<BDModuleSurfaceAI>();

                    if (SAI != null)
                    {
                        if (SAI.pilotEnabled)
                        {
                            SAI.TogglePilot();
                        }
                    }

                    if (PAI != null)
                    {
                        if (PAI.pilotEnabled)
                        {
                            PAI.TogglePilot();
                        }
                    }
                }
            }

            foreach (Part p in vessel.parts)
            {
                PartResource r = p.Resources.Where(pr => pr.resourceName == "ElectricCharge").FirstOrDefault();
                if (r != null)
                {
                    if (r.amount >= 0)
                    {
                        p.RequestResource("ElectricCharge", r.amount);
                    }
                }
            }
        }
    }
}