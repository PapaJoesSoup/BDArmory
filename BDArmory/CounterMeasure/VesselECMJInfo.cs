using System.Collections;
using System.Collections.Generic;
using BDArmory.Parts;
using BDArmory.UI;
using UnityEngine;

namespace BDArmory.CounterMeasure
{
    [RequireComponent(typeof(Vessel))]
    public class VesselECMJInfo : MonoBehaviour
    {
        List<ModuleECMJammer> jammers;
        public Vessel vessel;

        bool jEnabled;

        public bool jammerEnabled
        {
            get { return jEnabled; }
        }

        float jStrength;

        public float jammerStrength
        {
            get { return jStrength; }
        }

        float lbs;

        public float lockBreakStrength
        {
            get { return lbs; }
        }

        float rcsr;

        public float rcsReductionFactor
        {
            get { return rcsr; }
        }


        void Awake()
        {
            jammers = new List<ModuleECMJammer>();
            vessel = GetComponent<Vessel>();

            vessel.OnJustAboutToBeDestroyed += AboutToBeDestroyed;
            GameEvents.onVesselCreate.Add(OnVesselCreate);
            GameEvents.onPartJointBreak.Add(OnPartJointBreak);
            GameEvents.onPartDie.Add(OnPartDie);
        }

        void OnDestroy()
        {
            vessel.OnJustAboutToBeDestroyed -= AboutToBeDestroyed;
            GameEvents.onVesselCreate.Remove(OnVesselCreate);
            GameEvents.onPartJointBreak.Remove(OnPartJointBreak);
            GameEvents.onPartDie.Remove(OnPartDie);
        }

        void AboutToBeDestroyed()
        {
            Destroy(this);
        }

        void OnPartDie(Part p = null)
        {
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(DelayedCleanJammerListRoutine());
            }
        }

        void OnVesselCreate(Vessel v)
        {
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(DelayedCleanJammerListRoutine());
            }
        }

        void OnPartJointBreak(PartJoint j, float breakForce)
        {
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(DelayedCleanJammerListRoutine());
            }
        }

        public void AddJammer(ModuleECMJammer jammer)
        {
            if (!jammers.Contains(jammer))
            {
                jammers.Add(jammer);
            }

            UpdateJammerStrength();
        }

        public void RemoveJammer(ModuleECMJammer jammer)
        {
            jammers.Remove(jammer);

            UpdateJammerStrength();
        }

        void UpdateJammerStrength()
        {
            jEnabled = jammers.Count > 0;

            if (!jammerEnabled)
            {
                jStrength = 0;
            }

            float totaljStrength = 0;
            float totalLBstrength = 0;
            float jSpamFactor = 1;
            float lbreakFactor = 1;

            float rcsrTotal = 1;
            float rcsrCount = 0;

            List<ModuleECMJammer>.Enumerator jammer = jammers.GetEnumerator();
            while (jammer.MoveNext())
            {
                if (jammer.Current == null) continue;
                if (jammer.Current.signalSpam)
                {
                    totaljStrength += jSpamFactor* jammer.Current.jammerStrength;
                    jSpamFactor *= 0.75f;
                }
                if (jammer.Current.lockBreaker)
                {
                    totalLBstrength += lbreakFactor* jammer.Current.lockBreakerStrength;
                    lbreakFactor *= 0.65f;
                }
                if (jammer.Current.rcsReduction)
                {
                    rcsrTotal *= jammer.Current.rcsReductionFactor;
                    rcsrCount++;
                }
            }
            jammer.Dispose();

            lbs = totalLBstrength;
            jStrength = totaljStrength;

            if (rcsrCount > 0)
            {
                rcsr = Mathf.Clamp((rcsrTotal*rcsrCount), 0.0f, 1); //allow for 100% stealth (cloaking device)
            }
            else
            {
                rcsr = 1;
            }
        }

        public void DelayedCleanJammerList()
        {
            StartCoroutine(DelayedCleanJammerListRoutine());
        }

        IEnumerator DelayedCleanJammerListRoutine()
        {
            yield return null;
            yield return null;
            CleanJammerList();
        }

        void CleanJammerList()
        {
            vessel = GetComponent<Vessel>();

            if (!vessel)
            {
                Destroy(this);
            }
            jammers.RemoveAll(j => j == null);
            jammers.RemoveAll(j => j.vessel != vessel);

            List<ModuleECMJammer>.Enumerator jam = vessel.FindPartModulesImplementing<ModuleECMJammer>().GetEnumerator();
            while (jam.MoveNext())
            {
                if (jam.Current == null) continue;
                if (jam.Current.jammerEnabled)
                {
                    AddJammer(jam.Current);
                }
            }
            jam.Dispose();
            UpdateJammerStrength();
        }

    }
}