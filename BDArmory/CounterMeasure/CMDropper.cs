using System.Collections;
using System.Collections.Generic;
using BDArmory.Misc;
using BDArmory.UI;
using UniLinq;
using UnityEngine;
using System.Text;
using System;

namespace BDArmory.CounterMeasure
{
    public class CMDropper : PartModule
    {
        public static ObjectPool flarePool;
        public static ObjectPool chaffPool;
        public static ObjectPool smokePool;

        public enum CountermeasureTypes
        {
            Flare,
            Chaff,
            Smoke
        }

        public CountermeasureTypes cmType = CountermeasureTypes.Flare;
        [KSPField] public string countermeasureType = "flare";

        [KSPField] public float ejectVelocity = 30;

        [KSPField] public string ejectTransformName;
        Transform ejectTransform;

        [KSPField] public string effectsTransformName = string.Empty;
        Transform effectsTransform;


        AudioSource audioSource;
        AudioClip cmSound;
        AudioClip smokePoofSound;

        string resourceName;

        VesselChaffInfo vci;

        [KSPAction("Fire Countermeasure")]
        public void AGDropCM(KSPActionParam param)
        {
            DropCM();
        }

        [KSPEvent(guiActive = true, guiName = "Fire Countermeasure", active = true)]
        public void DropCM()
        {
            switch (cmType)
            {
                case CountermeasureTypes.Flare:
                    DropFlare();
                    break;
                case CountermeasureTypes.Chaff:
                    DropChaff();
                    break;
                case CountermeasureTypes.Smoke:
                    PopSmoke();
                    break;
            }
        }

        public override void OnStart(StartState state)
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                SetupCM();

                ejectTransform = part.FindModelTransform(ejectTransformName);

                if (effectsTransformName != string.Empty)
                {
                    effectsTransform = part.FindModelTransform(effectsTransformName);
                }

                part.force_activate();

                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.minDistance = 1;
                audioSource.maxDistance = 1000;
                audioSource.spatialBlend = 1;

                UpdateVolume();
                BDArmorySettings.OnVolumeChange += UpdateVolume;
            }
        }

        void UpdateVolume()
        {
            if (audioSource)
            {
                audioSource.volume = BDArmorySettings.BDARMORY_WEAPONS_VOLUME;
            }
        }

        void OnDestroy()
        {
            BDArmorySettings.OnVolumeChange -= UpdateVolume;
        }

        public override void OnUpdate()
        {
            if (audioSource)
            {
                if (vessel.isActiveVessel)
                {
                    audioSource.dopplerLevel = 0;
                }
                else
                {
                    audioSource.dopplerLevel = 1;
                }
            }
        }

        void FireParticleEffects()
        {
            if (!effectsTransform) return;
            IEnumerator<KSPParticleEmitter> pe = effectsTransform.gameObject.GetComponentsInChildren<KSPParticleEmitter>().Cast<KSPParticleEmitter>().GetEnumerator();
            while (pe.MoveNext())
            {
                if (pe.Current == null) continue;
                EffectBehaviour.AddParticleEmitter(pe.Current);
                pe.Current.Emit();
            }
            pe.Dispose();
        }


        PartResource GetCMResource()
        {
            IEnumerator<PartResource> res = part.Resources.GetEnumerator();
            while (res.MoveNext())
            {
                if (res.Current == null) continue;
                if (res.Current.resourceName == resourceName) return res.Current;
            }
            res.Dispose();
            return null;
        }

        void SetupCM()
        {
            countermeasureType = countermeasureType.ToLower();
            switch (countermeasureType)
            {
                case "flare":
                    cmType = CountermeasureTypes.Flare;
                    cmSound = GameDatabase.Instance.GetAudioClip("BDArmory/Sounds/flareSound");
                    if (!flarePool)
                    {
                        SetupFlarePool();
                    }
                    resourceName = "CMFlare";
                    break;
                case "chaff":
                    cmType = CountermeasureTypes.Chaff;
                    cmSound = GameDatabase.Instance.GetAudioClip("BDArmory/Sounds/smokeEject");
                    resourceName = "CMChaff";
                    vci = vessel.gameObject.GetComponent<VesselChaffInfo>();
                    if (!vci)
                    {
                        vci = vessel.gameObject.AddComponent<VesselChaffInfo>();
                    }
                    if (!chaffPool)
                    {
                        SetupChaffPool();
                    }
                    break;
                case "smoke":
                    cmType = CountermeasureTypes.Smoke;
                    cmSound = GameDatabase.Instance.GetAudioClip("BDArmory/Sounds/smokeEject");
                    smokePoofSound = GameDatabase.Instance.GetAudioClip("BDArmory/Sounds/smokePoof");
                    resourceName = "CMSmoke";
                    if (smokePool == null)
                    {
                        SetupSmokePool();
                    }
                    break;
            }
        }

        void DropFlare()
        {
            PartResource cmResource = GetCMResource();
            if (cmResource == null || !(cmResource.amount >= 1)) return;
            cmResource.amount--;
            audioSource.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(cmSound);

            GameObject cm = flarePool.GetPooledObject();
            cm.transform.position = transform.position;
            CMFlare cmf = cm.GetComponent<CMFlare>();
            cmf.startVelocity = part.rb.velocity + (ejectVelocity*transform.up) +
                                (UnityEngine.Random.Range(-3f, 3f)*transform.forward) +
                                (UnityEngine.Random.Range(-3f, 3f)*transform.right);
            cmf.sourceVessel = vessel;

            cm.SetActive(true);

            FireParticleEffects();
        }

        void DropChaff()
        {
            PartResource cmResource = GetCMResource();
            if (cmResource == null || !(cmResource.amount >= 1)) return;
            cmResource.amount--;
            audioSource.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(cmSound);

            if (!vci)
            {
                vci = vessel.gameObject.AddComponent<VesselChaffInfo>();
            }
            vci.Chaff();

            GameObject cm = chaffPool.GetPooledObject();
            CMChaff chaff = cm.GetComponent<CMChaff>();
            chaff.Emit(ejectTransform.position, ejectVelocity*ejectTransform.forward);

            FireParticleEffects();
        }

        void PopSmoke()
        {
            PartResource smokeResource = GetCMResource();
            if (smokeResource.amount >= 1)
            {
                smokeResource.amount--;
                audioSource.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
                audioSource.PlayOneShot(cmSound);

                StartCoroutine(SmokeRoutine());

                FireParticleEffects();
            }
        }

        IEnumerator SmokeRoutine()
        {
            yield return new WaitForSeconds(0.2f);
            GameObject smokeCMObject = smokePool.GetPooledObject();
            smokeCMObject.SetActive(true);
            smokeCMObject.transform.position = ejectTransform.position + (10*ejectTransform.forward);
            float longestLife = 0;
            IEnumerator<KSPParticleEmitter> emitter = smokeCMObject.GetComponentsInChildren<KSPParticleEmitter>().Cast<KSPParticleEmitter>().GetEnumerator();
            while (emitter.MoveNext())
            {
                if (emitter.Current == null) continue;
                EffectBehaviour.AddParticleEmitter(emitter.Current);
                emitter.Current.Emit();
                if (emitter.Current.maxEnergy > longestLife) longestLife = emitter.Current.maxEnergy;
            }
            emitter.Dispose();

            audioSource.PlayOneShot(smokePoofSound);
            yield return new WaitForSeconds(longestLife);
            smokeCMObject.SetActive(false);
        }

        void SetupFlarePool()
        {
            GameObject cm = (GameObject) Instantiate(GameDatabase.Instance.GetModel("BDArmory/Models/CMFlare/model"));
            cm.SetActive(false);
            cm.AddComponent<CMFlare>();
            flarePool = ObjectPool.CreateObjectPool(cm, 10, true, true);
        }

        void SetupSmokePool()
        {
            GameObject cm =
                (GameObject) Instantiate(GameDatabase.Instance.GetModel("BDArmory/Models/CMSmoke/cmSmokeModel"));
            cm.SetActive(false);
            cm.AddComponent<CMSmoke>();

            smokePool = ObjectPool.CreateObjectPool(cm, 10, true, true);
        }

        void SetupChaffPool()
        {
            GameObject cm = (GameObject) Instantiate(GameDatabase.Instance.GetModel("BDArmory/Models/CMChaff/model"));
            cm.SetActive(false);
            cm.AddComponent<CMChaff>();

            chaffPool = ObjectPool.CreateObjectPool(cm, 10, true, true);
        }


        // RMB info in editor
        public override string GetInfo()
        {
            StringBuilder output = new StringBuilder();
            output.Append(Environment.NewLine);
            output.Append($"Countermeasure: {countermeasureType}");
            output.Append(Environment.NewLine);

            return output.ToString();

        }
    }
}