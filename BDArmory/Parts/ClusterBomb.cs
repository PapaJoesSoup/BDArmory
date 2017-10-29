using System;
using System.Collections.Generic;
using BDArmory.Core.Extension;
using BDArmory.FX;
using BDArmory.Misc;
using BDArmory.UI;
using UniLinq;
using UnityEngine;

namespace BDArmory.Parts
{
    public class ClusterBomb : PartModule
    {
        List<GameObject> submunitions;
        List<GameObject> fairings;
        MissileLauncher missileLauncher;

        bool deployed;

        [KSPField(isPersistant = false)] public string subExplModelPath = "BDArmory/Models/explosion/explosion";

        [KSPField(isPersistant = false)] public string subExplSoundPath = "BDArmory/Sounds/subExplode";


        [KSPField(isPersistant = false)] public float deployDelay = 2.5f;


        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Deploy Altitude"),
         UI_FloatRange(minValue = 100f, maxValue = 1000f, stepIncrement = 10f, scene = UI_Scene.Editor)] public float
            deployAltitude = 400;

        [KSPField(isPersistant = false)] public float submunitionMaxSpeed = 10;


        [KSPField(isPersistant = false)] public bool swapCollidersOnDeploy = true;


        public override void OnStart(StartState state)
        {
            submunitions = new List<GameObject>();
            IEnumerator<Transform> sub = part.FindModelTransforms("submunition").AsEnumerable().GetEnumerator();
            while (sub.MoveNext())
            {
                if (sub.Current == null) continue;
                submunitions.Add(sub.Current.gameObject);
                if (HighLogic.LoadedSceneIsFlight)
                {
                    Rigidbody subRb = sub.Current.gameObject.GetComponent<Rigidbody>();
                    if (!subRb)
                    {
                        subRb = sub.Current.gameObject.AddComponent<Rigidbody>();
                    }
                    subRb.isKinematic = true;
                    subRb.mass = part.mass/part.FindModelTransforms("submunition").Length;
                }
                sub.Current.gameObject.SetActive(false);
            }
            sub.Dispose();

            fairings = new List<GameObject>();
            IEnumerator<Transform> fairing = part.FindModelTransforms("fairing").AsEnumerable().GetEnumerator();
            while (fairing.MoveNext())
            {
                if (fairing.Current == null) continue;
                fairings.Add(fairing.Current.gameObject);
                if (!HighLogic.LoadedSceneIsFlight) continue;
                Rigidbody fairingRb = fairing.Current.gameObject.GetComponent<Rigidbody>();
                if (!fairingRb)
                {
                    fairingRb = fairing.Current.gameObject.AddComponent<Rigidbody>();
                }
                fairingRb.isKinematic = true;
                fairingRb.mass = 0.05f;
            }
            fairing.Dispose();

            missileLauncher = part.GetComponent<MissileLauncher>();
            //missileLauncher.deployTime = deployDelay;
        }

        public override void OnFixedUpdate()
        {
            if (missileLauncher != null && missileLauncher.HasFired && missileLauncher.TimeIndex > deployDelay && !deployed && AltitudeTrigger())
            {
                DeploySubmunitions();
            }
        }

        void DeploySubmunitions()
        {
            missileLauncher.sfAudioSource.PlayOneShot(GameDatabase.Instance.GetAudioClip("BDArmory/Sounds/flare"));
            FXMonger.Explode(part, transform.position + part.rb.velocity*Time.fixedDeltaTime, 0.1f);

            deployed = true;
            if (swapCollidersOnDeploy)
            {
                IEnumerator<Collider> col = part.GetComponentsInChildren<Collider>().AsEnumerable().GetEnumerator();
                while (col.MoveNext())
                {
                    if (col.Current == null) continue;
                    col.Current.enabled = !col.Current.enabled;
                }
                col.Dispose();
            }

            missileLauncher.sfAudioSource.priority = 999;
            //missileLauncher.explosionSize = 3;
            List<GameObject>.Enumerator sub = submunitions.GetEnumerator();
            while (sub.MoveNext())
            {
                if (sub.Current == null) continue;
                sub.Current.SetActive(true);
                sub.Current.transform.parent = null;
                Vector3 direction = (sub.Current.transform.position - part.transform.position).normalized;
                Rigidbody subRB = sub.Current.GetComponent<Rigidbody>();
                subRB.isKinematic = false;
                subRB.velocity = part.rb.velocity +
                                 (UnityEngine.Random.Range(submunitionMaxSpeed/10, submunitionMaxSpeed)*direction);

                Submunition subScript = sub.Current.AddComponent<Submunition>();
                subScript.enabled = true;
                subScript.deployed = true;
                subScript.sourceVessel = missileLauncher.SourceVessel;
                subScript.blastForce = missileLauncher.blastPower;
                subScript.blastHeat = missileLauncher.blastHeat;
                subScript.blastRadius = missileLauncher.blastRadius;
                subScript.subExplModelPath = subExplModelPath;
                subScript.subExplSoundPath = subExplSoundPath;
                sub.Current.AddComponent<KSPForceApplier>();
            }

            List<GameObject>.Enumerator fairing = fairings.GetEnumerator();
            while (fairing.MoveNext())
            {
                if (fairing.Current == null) continue;
                Vector3 direction = (fairing.Current.transform.position - part.transform.position).normalized;
                Rigidbody fRB = fairing.Current.GetComponent<Rigidbody>();
                fRB.isKinematic = false;
                fRB.velocity = part.rb.velocity + ((submunitionMaxSpeed + 2)*direction);
                fairing.Current.AddComponent<KSPForceApplier>();
                fairing.Current.GetComponent<KSPForceApplier>().drag = 0.2f;
                ClusterBombFairing fairingScript = fairing.Current.AddComponent<ClusterBombFairing>();
                fairingScript.deployed = true;
                fairingScript.sourceVessel = vessel;
            }
            fairing.Dispose();

            part.explosionPotential = 0;
            missileLauncher.HasFired = false;

            part.SetDamage(part.maxTemp*2);
        }


        bool AltitudeTrigger()
        {
            double asl = vessel.mainBody.GetAltitude(vessel.CoM);
            double radarAlt = asl - vessel.terrainAltitude;

            return (radarAlt < deployAltitude || asl < deployAltitude) && vessel.verticalSpeed < 0;
        }
    }


    public class Submunition : MonoBehaviour
    {
        public bool deployed;
        public float blastRadius;
        public float blastForce;
        public float blastHeat;
        public string subExplModelPath;
        public string subExplSoundPath;
        public Vessel sourceVessel;
        Vector3 currPosition;
        Vector3 prevPosition;
        Vector3 relativePos;

        float startTime;

        Rigidbody rb;

        void Start()
        {
            startTime = Time.time;
            relativePos = transform.position - sourceVessel.transform.position;
            currPosition = transform.position;
            prevPosition = transform.position;
            rb = GetComponent<Rigidbody>();
        }

        void FixedUpdate()
        {
            if (deployed)
            {
                if (Time.time - startTime > 30)
                {
                    Destroy(gameObject);
                    return;
                }

                //floatingOrigin fix
                if (sourceVessel != null &&
                    ((transform.position - sourceVessel.transform.position) - relativePos).sqrMagnitude > 800*800)
                {
                    transform.position = sourceVessel.transform.position + relativePos +
                                         (rb.velocity*Time.fixedDeltaTime);
                }
                if (sourceVessel != null) relativePos = transform.position - sourceVessel.transform.position;
                //

                currPosition = transform.position;
                float dist = (currPosition - prevPosition).magnitude;
                Ray ray = new Ray(prevPosition, currPosition - prevPosition);
                RaycastHit hit;
                //if (Physics.Raycast(ray, out hit, dist, 2228224))
                //{
                //    try
                //    {
                //        hitEVA = hit.collider.gameObject.GetComponentUpwards<KerbalEVA>();
                //        if (hitEVA != null)
                //            Debug.Log("[BDArmory]:Hit on kerbal confirmed!");
                //    }
                //    catch (NullReferenceException)
                //    {
                //        Debug.Log("[BDArmory]:Whoops ran amok of the exception handler");
                //    }

                //    Part hitPart = hitEVA.part;

                //    float destroyChance = (rb.mass / hitPart.crashTolerance) *
                //                              (rb.velocity - hit.rigidbody.velocity).magnitude * 8000;
                //    if (BDArmorySettings.INSTAKILL)
                //    {
                //        destroyChance = 100;
                //    }
                //    Debug.Log("[BDArmory]: Hit part: " + hitPart.name + ", chance of destroy: " + destroyChance);
                //    if (UnityEngine.Random.Range(0f, 100f) < destroyChance)
                //    {
                //        hitPart.SetDamage(hitPart.maxTemp + 100);
                //    }
                //    if (hitPart.vessel != sourceVessel)
                //    {
                //        Detonate(hit.point);
                //    }
                //}

                if (Physics.Raycast(ray, out hit, dist, 688129))
                {
                    Part hitPart = null;
                    try
                    {
                        KerbalEVA eva = hit.collider.gameObject.GetComponentUpwards<KerbalEVA>();
                        hitPart = eva ? eva.part : hit.collider.gameObject.GetComponentInParent<Part>();
                    }
                    catch (NullReferenceException)
                    {
                    }

                    if (hitPart != null)
                    {
                        float destroyChance = (rb.mass/hitPart.crashTolerance)*
                                              (rb.velocity - hit.rigidbody.velocity).magnitude*8000;
                        if (BDArmorySettings.INSTAKILL)
                        {
                            destroyChance = 100;
                        }
                        Debug.Log("[BDArmory]: Hit part: " + hitPart.name + ", chance of destroy: " + destroyChance);
                        if (UnityEngine.Random.Range(0f, 100f) < destroyChance)
                        {
                            hitPart.SetDamage(hitPart.maxTemp*2);
                        }
                    }
                    if (hitPart == null || (hitPart != null && hitPart.vessel != sourceVessel))
                    {
                        Detonate(hit.point);
                    }
                }
                else if (FlightGlobals.getAltitudeAtPos(currPosition) <= 0)
                {
                    Detonate(currPosition);
                }
            }
        }

        void Detonate(Vector3 pos)
        {
            ExplosionFX.CreateExplosion(pos, blastRadius, blastForce, blastHeat, sourceVessel, FlightGlobals.getUpAxis(),
                subExplModelPath, subExplSoundPath);
            Destroy(gameObject); //destroy bullet on collision
        }
    }

    public class ClusterBombFairing : MonoBehaviour
    {
        public bool deployed;

        public Vessel sourceVessel;
        Vector3 currPosition;
        Vector3 prevPosition;
        Vector3 relativePos;
        float startTime;

        Rigidbody rb;

        void Start()
        {
            startTime = Time.time;
            currPosition = transform.position;
            prevPosition = transform.position;
            relativePos = transform.position - sourceVessel.transform.position;
            rb = GetComponent<Rigidbody>();
        }

        void FixedUpdate()
        {
            if (deployed)
            {
                //floatingOrigin fix
                if (sourceVessel != null &&
                    ((transform.position - sourceVessel.transform.position) - relativePos).sqrMagnitude > 800*800)
                {
                    transform.position = sourceVessel.transform.position + relativePos +
                                         (rb.velocity*Time.fixedDeltaTime);
                }
                if (sourceVessel != null) relativePos = transform.position - sourceVessel.transform.position;
                //

                currPosition = transform.position;
                float dist = (currPosition - prevPosition).magnitude;
                Ray ray = new Ray(prevPosition, currPosition - prevPosition);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, dist, 688129))
                {
                    Destroy(gameObject);
                }
                else if (FlightGlobals.getAltitudeAtPos(currPosition) <= 0)
                {
                    Destroy(gameObject);
                }
                else if (Time.time - startTime > 20)
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}