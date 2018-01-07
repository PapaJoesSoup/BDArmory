using System.Collections.Generic;
using BDArmory.Core.Extension;
using BDArmory.FX;
using BDArmory.Misc;
using UniLinq;
using UnityEngine;
using BDArmory.Core;
using System;

namespace BDArmory.Parts
{
    public class ClusterBomb : PartModule
    {
        List<GameObject> submunitions;
        List<GameObject> fairings;
        MissileLauncher missileLauncher;

        bool deployed;

        [KSPField(isPersistant = false)]
        public string subExplModelPath = "BDArmory/Models/explosion/explosion";

        [KSPField(isPersistant = false)]
        public string subExplSoundPath = "BDArmory/Sounds/subExplode";
        
        [KSPField(isPersistant = false)]
        public float deployDelay = 2.5f;
        
        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Deploy Altitude"),
         UI_FloatRange(minValue = 100f, maxValue = 1000f, stepIncrement = 10f, scene = UI_Scene.Editor)]
        public float deployAltitude = 400;

        [KSPField(isPersistant = false)]
        public float submunitionMaxSpeed = 10;
        
        [KSPField(isPersistant = false)]
        public bool swapCollidersOnDeploy = true;


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
                    subRb.mass = part.mass / part.FindModelTransforms("submunition").Length;                    

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
        }

        public override void OnFixedUpdate()
        {
            if (missileLauncher != null && missileLauncher.HasFired &&
                missileLauncher.TimeIndex > deployDelay && 
                !deployed && AltitudeTrigger())
            {
                DeploySubmunitions();
            }
        }

        void DeploySubmunitions()
        {
            missileLauncher.sfAudioSource.PlayOneShot(GameDatabase.Instance.GetAudioClip("BDArmory/Sounds/flare"));
            FXMonger.Explode(part, transform.position + part.rb.velocity * Time.fixedDeltaTime, 0.1f);

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
                subScript.blastForce = missileLauncher.GetTntMass();
                subScript.blastHeat = missileLauncher.blastHeat;
                subScript.blastRadius = missileLauncher.GetBlastRadius();
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

            part.Destroy();
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

        void OnCollisionEnter(Collision col)
        {
            ContactPoint contact = col.contacts[0];
            Vector3 pos = contact.point;
            ExplosionFx.CreateExplosion(pos, blastForce, subExplModelPath, subExplSoundPath, true);
        }

        void FixedUpdate()
        {
            if (deployed)
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
                        ((transform.position - sourceVessel.transform.position) - relativePos).sqrMagnitude > 800 * 800)
                    {
                        transform.position = sourceVessel.transform.position + relativePos +
                                             (rb.velocity * Time.fixedDeltaTime);
                    }
                    if (sourceVessel != null) relativePos = transform.position - sourceVessel.transform.position;
                    //

                    currPosition = transform.position;
                    float dist = (currPosition - prevPosition).magnitude;
                    Ray ray = new Ray(prevPosition, currPosition - prevPosition);
                    RaycastHit hit;

                    if (Physics.Raycast(ray, out hit, dist, 9076737))
                    {
                        Part hitPart = null;
                        try
                        {
                            hitPart = hit.collider.gameObject.GetComponentInParent<Part>();                            
                        }
                        catch (NullReferenceException)
                        {
                            Debug.Log("[BDArmory]:NullReferenceException for Submunition Hit");
                            return;
                        }

                        if (hitPart?.vessel == sourceVessel) return;

                        if (hitPart != null || CheckBuildingHit(hit))
                        {
                            Detonate(hit.point);
                        }
                        else if (hitPart == null)
                        {
                            Detonate(currPosition);
                        }
                        
                    }
                    else if (FlightGlobals.getAltitudeAtPos(currPosition) <= 0)
                    {
                        Detonate(currPosition);
                    }
                }
            }
        }

        void Detonate(Vector3 pos)
        {
            ExplosionFx.CreateExplosion(pos, blastForce, subExplModelPath, subExplSoundPath,true);
            Destroy(gameObject); 
        }

        private bool CheckBuildingHit(RaycastHit hit)
        {
            DestructibleBuilding building = null;
            try
            {
                building = hit.collider.gameObject.GetComponentUpwards<DestructibleBuilding>();
            }
            catch (Exception) { }

            if (building != null && building.IsIntact)
            {
                return true;
            }
            return false;

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
                if (Physics.Raycast(ray, out hit, dist, 9076737))
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