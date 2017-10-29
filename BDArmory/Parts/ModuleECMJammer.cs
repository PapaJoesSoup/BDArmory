using System.Collections.Generic;
using BDArmory.CounterMeasure;
using System.Text;
using System;

namespace BDArmory.Parts
{
    public class ModuleECMJammer : PartModule
    {
        [KSPField] public float jammerStrength = 700;

        [KSPField] public float lockBreakerStrength = 500;

        [KSPField] public float rcsReductionFactor = 0.75f;

        [KSPField] public double resourceDrain = 5;

        [KSPField] public bool alwaysOn = false;

        [KSPField] public bool signalSpam = true;

        [KSPField] public bool lockBreaker = true;

        [KSPField] public bool rcsReduction = false;

        [KSPField(isPersistant = true, guiActive = true, guiName = "Enabled")] public bool jammerEnabled;

        VesselECMJInfo vesselJammer;

        [KSPAction("Enable")]
        public void AGEnable(KSPActionParam param)
        {
            if (!jammerEnabled)
            {
                EnableJammer();
            }
        }

        [KSPAction("Disable")]
        public void AGDisable(KSPActionParam param)
        {
            if (jammerEnabled)
            {
                DisableJammer();
            }
        }


        [KSPAction("Toggle")]
        public void AGToggle(KSPActionParam param)
        {
            Toggle();
        }

        [KSPEvent(guiActiveEditor = false, guiActive = true, guiName = "Toggle")]
        public void Toggle()
        {
            if (jammerEnabled)
            {
                DisableJammer();
            }
            else
            {
                EnableJammer();
            }
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (!HighLogic.LoadedSceneIsFlight) return;
            part.force_activate();
            List<MissileFire>.Enumerator wm = vessel.FindPartModulesImplementing<MissileFire>().GetEnumerator();
            while (wm.MoveNext())
            {
                if (wm.Current == null) continue;
                wm.Current.jammers.Add(this);
            }
            wm.Dispose();

            GameEvents.onVesselCreate.Add(OnVesselCreate);
        }

        void OnDestroy()
        {
            GameEvents.onVesselCreate.Remove(OnVesselCreate);
        }

        void OnVesselCreate(Vessel v)
        {
            EnsureVesselJammer();
        }

        public void EnableJammer()
        {
            EnsureVesselJammer();
            vesselJammer.AddJammer(this);
            jammerEnabled = true;
        }

        public void DisableJammer()
        {
            EnsureVesselJammer();

            vesselJammer.RemoveJammer(this);
            jammerEnabled = false;
        }

        public override void OnFixedUpdate()
        {
            base.OnFixedUpdate();

            if (alwaysOn && !jammerEnabled)
            {
                EnableJammer();
            }

            if (jammerEnabled)
            {
                EnsureVesselJammer();

                DrainElectricity();
            }
        }

        void EnsureVesselJammer()
        {
            if (!vesselJammer || vesselJammer.vessel != vessel)
            {
                vesselJammer = vessel.gameObject.GetComponent<VesselECMJInfo>();
                if (!vesselJammer)
                {
                    vesselJammer = vessel.gameObject.AddComponent<VesselECMJInfo>();
                }
            }

            vesselJammer.DelayedCleanJammerList();
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
                DisableJammer();
            }
        }


        // RMB info in editor
        public override string GetInfo()
        {
            StringBuilder output = new StringBuilder();
            output.Append($"EC/sec: {resourceDrain}");
            output.Append(Environment.NewLine);
            output.Append($"Always on: {alwaysOn}");
            output.Append(Environment.NewLine);
            output.Append($"RCS reduction: {rcsReduction}");
            output.Append(Environment.NewLine);
            if (rcsReduction)
            {
                output.Append($" - factor: {rcsReductionFactor}");
                output.Append(Environment.NewLine);
            }
            output.Append($"Lockbreaker: {lockBreaker}");
            output.Append(Environment.NewLine);
            if (lockBreaker)
            {
                output.Append($" - strength: {lockBreakerStrength}");
                output.Append(Environment.NewLine);
            }
            output.Append($"Signal strength: {jammerStrength}");
            output.Append(Environment.NewLine);
            output.Append($"(increases detectability!)");
            output.Append(Environment.NewLine);


            return output.ToString();
        }


    }
}