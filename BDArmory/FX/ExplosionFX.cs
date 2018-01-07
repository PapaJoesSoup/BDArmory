using System.Collections.Generic;
using System.Linq;
using BDArmory.Core.Extension;
using BDArmory.Misc;
using BDArmory.UI;
using UnityEngine;
using System;
using BDArmory.Core;
using BDArmory.Core.Enum;
using BDArmory.Core.Utils;

namespace BDArmory.FX
{
    public class ExplosionFx : MonoBehaviour
    {
        public KSPParticleEmitter[] PEmitters { get; set; }
        public Light LightFx { get; set; }
        public float StartTime { get; set; }
        public AudioClip ExSound { get; set; }
        public AudioSource AudioSource { get; set; }
        private float MaxTime { get; set; }
        public float Range { get; set; }
        public float Caliber { get; set; }
        public bool IsMissile { get; set; }
        public float Power { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Direction { get; set; }
        public Part ExplosivePart { get; set; }

        public float TimeIndex => Time.time - StartTime;

        public Queue<BlastHitEvent> ExplosionEvents = new Queue<BlastHitEvent>();

        public static List<Part> IgnoreParts = new List<Part>();

        public static List<DestructibleBuilding> IgnoreBuildings = new List<DestructibleBuilding>();

        internal static readonly float ExplosionVelocity = 343f;

        private float particlesMaxEnergy;

        private void Start()
        {
            StartTime = Time.time;
            MaxTime = (Range / ExplosionVelocity)*3f;
            CalculateBlastEvents();
            PEmitters = gameObject.GetComponentsInChildren<KSPParticleEmitter>();
            IEnumerator<KSPParticleEmitter> pe = PEmitters.AsEnumerable().GetEnumerator();
            while (pe.MoveNext())
            {
                if (pe.Current == null) continue;
                EffectBehaviour.AddParticleEmitter(pe.Current);
                pe.Current.emit = true;
                if (pe.Current.maxEnergy > particlesMaxEnergy)
                {
                    particlesMaxEnergy = pe.Current.maxEnergy;
                }
            }
            pe.Dispose();

            LightFx = gameObject.AddComponent<Light>();
            LightFx.color = Misc.Misc.ParseColor255("255,238,184,255");
            LightFx.intensity = 8;
            LightFx.range = Range*3f;
            LightFx.shadows = LightShadows.None;

            if (BDArmorySettings.DRAW_DEBUG_LABELS)
            {
                Debug.Log(
                    "[BDArmory]:Explosion started tntMass: {" + Power + "}  BlastRadius: {" + Range+ "} StartTime: {"+ StartTime + "}, Duration: {" + MaxTime + "}");
            }
        }

        private void CalculateBlastEvents()
        {  
            var temporalEventList = new List<BlastHitEvent>();

            temporalEventList.AddRange(ProcessingBlastSphere());
      

            //Let's convert this temporal list on a ordered queue
            using (var enuEvents = temporalEventList.OrderBy(e => e.TimeToImpact).GetEnumerator())
            {
                while (enuEvents.MoveNext())
                {
                    if(enuEvents.Current == null) continue;

                    if (BDArmorySettings.DRAW_DEBUG_LABELS)
                    {
                        Debug.Log(
                            "[BDArmory]: Enqueueing Blast Event");
                    }

                    ExplosionEvents.Enqueue(enuEvents.Current);
                }
            }
        }

        private List<BlastHitEvent> ProcessingBlastSphere()
        {
            List<BlastHitEvent> result = new List<BlastHitEvent>();
            List<Part> parstAdded = new List<Part>();
            List<DestructibleBuilding> bulidingAdded = new List<DestructibleBuilding>();

            using (var hitCollidersEnu = Physics.OverlapSphere(Position, Range, 9076737).AsEnumerable().GetEnumerator())
            {
                while (hitCollidersEnu.MoveNext())
                {
                    if(hitCollidersEnu.Current == null) continue;

                    Part partHit = hitCollidersEnu.Current.GetComponentInParent<Part>();

                    if (partHit != null && partHit.mass > 0  && !parstAdded.Contains(partHit))
                    {
                        ProcessPartEvent(partHit, result, parstAdded);
                    }
                    else
                    {
                         DestructibleBuilding building = hitCollidersEnu.Current.GetComponentInParent<DestructibleBuilding>();

                        if (building != null && !bulidingAdded.Contains(building))
                        {
                            ProcessBuildingEvent(building, result, bulidingAdded);
                        }
                    }
                }
            }
            return result;
        }

        private void ProcessBuildingEvent(DestructibleBuilding building, List<BlastHitEvent> eventList, List<DestructibleBuilding> bulidingAdded)
        {
            Ray ray = new Ray(Position, building.transform.position - Position);
            RaycastHit rayHit;
            if (Physics.Raycast(ray, out rayHit, Range, 557057))
            {
                //TODO: Maybe we are not hitting building because we are hitting explosive parts. 

                DestructibleBuilding destructibleBuilding = rayHit.collider.GetComponentInParent<DestructibleBuilding>();

                // Is not a direct hit, because we are hitting a different part
                if (destructibleBuilding != null && destructibleBuilding.Equals(building))
                {
                    var distance = Vector3.Distance(Position, rayHit.point);
                    eventList.Add(new BuildingBlastHitEvent() { Distance = Vector3.Distance(Position, rayHit.point), Building = building, TimeToImpact = distance / ExplosionVelocity });
                    bulidingAdded.Add(building);
                }
            }
        }

        private void ProcessPartEvent(Part part, List<BlastHitEvent> eventList, List<Part> partsAdded)
        {
            RaycastHit hit;
        
            if (IsInLineOfSight(part, ExplosivePart, out hit))
            {
                if (IsAngleAllowed(Direction, hit))
                {
                    var realDistance = Vector3.Distance(Position, hit.point);
                    //Adding damage hit
                    eventList.Add(new PartBlastHitEvent()
                    {
                        Distance = realDistance,
                        Part = part,
                        TimeToImpact = realDistance / ExplosionVelocity,
                        HitPoint = hit.point,
                    });
                    partsAdded.Add(part);
                }
            }               
        }

        private bool IsAngleAllowed(Vector3 direction, RaycastHit hit)
        {
            if (IsMissile || direction == default(Vector3))
            {
                return true;
            }

            return Vector3.Angle(direction, (hit.point - Position).normalized) < 100f;
        }
        /// <summary>
        /// This method will calculate if there is valid line of sight between the explosion origin and the specific Part
        /// In order to avoid collisions with the same missile part, It will not take into account those parts beloging to same vessel that contains the explosive part
        /// </summary>
        /// <param name="part"></param>
        /// <param name="explosivePart"></param>
        /// <param name="hit"> out property with the actual hit</param>
        /// <returns></returns>
        private bool IsInLineOfSight(Part part, Part explosivePart, out RaycastHit hit)
        {
            Ray partRay = new Ray(Position, part.transform.position - Position);

            var hits = Physics.RaycastAll(partRay, Range, 9076737).AsEnumerable();
            using (var hitsEnu = hits.OrderBy(x => x.distance).GetEnumerator())
            {
                while (hitsEnu.MoveNext())
                {
                    Part partHit = hitsEnu.Current.collider.GetComponentInParent<Part>();
                    if (partHit == null) continue;
                    hit = hitsEnu.Current;

                    if (partHit == part)
                    {
                        return true;
                    }
                    if (partHit != part)
                    {
                        // ignoring collsions against the explosive
                        if (explosivePart != null && partHit.vessel == explosivePart.vessel)
                        {
                            continue;
                        }
                        return false;
                    }
                }
            }

            hit = new RaycastHit();
            return false;
        }

        public void Update()
        {
            LightFx.intensity -= 12 * Time.deltaTime;
            if (TimeIndex > 0.2f)
            {
                IEnumerator<KSPParticleEmitter> pe = PEmitters.AsEnumerable().GetEnumerator();
                while (pe.MoveNext())
                {
                    if (pe.Current == null) continue;
                    pe.Current.emit = false;
                }
                pe.Dispose();
            }

            if (ExplosionEvents.Count == 0 && TimeIndex > Math.Max(MaxTime, particlesMaxEnergy))
            {
                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                {
                    Debug.Log(
                        "[BDArmory]:Explosion Finished");
                }
                Destroy(gameObject);
                return;
            }
        }

        public void FixedUpdate()
        {
            while (ExplosionEvents.Count > 0 && ExplosionEvents.Peek().TimeToImpact <= TimeIndex)
            {
                BlastHitEvent eventToExecute = ExplosionEvents.Dequeue();

                var partBlastHitEvent = eventToExecute as PartBlastHitEvent;
                if (partBlastHitEvent != null)
                {
                    ExecutePartBlastEvent(partBlastHitEvent);
                }
                else
                {
                    ExecuteBuildingBlastEvent((BuildingBlastHitEvent) eventToExecute);
                }
            }   
        }

        private void ExecuteBuildingBlastEvent(BuildingBlastHitEvent eventToExecute)
        {
            //TODO: Review if the damage is sensible after so many changes
            //buildings
            DestructibleBuilding building = eventToExecute.Building;
            building.damageDecay = 600f;
            
            if (building)
            {
                var distanceFactor = Mathf.Clamp01((Range - eventToExecute.Distance) / Range);
                float damageToBuilding = (BDArmorySettings.DMG_MULTIPLIER / 100) * BDArmorySettings.EXP_DMG_MOD_BALLISTIC * Power * distanceFactor;

                damageToBuilding *= 2f;

                building.AddDamage(damageToBuilding);

                if (building.Damage  > building.impactMomentumThreshold)
                {
                    building.Demolish();
                }
                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                {
                      Debug.Log("[BDArmory]: Explosion hit destructible building! Hitpoints Applied: " + Mathf.Round(damageToBuilding) +
                               ", Building Damage : " + Mathf.Round(building.Damage) +
                               " Building Threshold : " + building.impactMomentumThreshold);
                }   
            }
        }

        private void ExecutePartBlastEvent(PartBlastHitEvent eventToExecute)
        {
            if (eventToExecute.Part == null ||eventToExecute.Part.Rigidbody == null || eventToExecute.Part.vessel == null || eventToExecute.Part.partInfo == null ) return;

            try
            {
                Part part = eventToExecute.Part;
                Rigidbody rb = part.Rigidbody;
                var realDistance = eventToExecute.Distance;


                if (!eventToExecute.IsNegativePressure)
                {
                    BlastInfo blastInfo =
                        BlastPhysicsUtils.CalculatePartBlastEffects(part, realDistance,
                            part.vessel.totalMass * 1000f, Power, Range);

                    if (BDArmorySettings.DRAW_DEBUG_LABELS)
                    {
                        Debug.Log(
                            "[BDArmory]: Executing blast event Part: {" + part.name + "}, " +
                            " VelocityChange: {" + blastInfo.VelocityChange + "}," +
                            " Distance: {" + realDistance + "}," +
                            " TotalPressure: {" + blastInfo.TotalPressure + "}," +
                            " Damage: {" + blastInfo.Damage + "}," +
                            " EffectiveArea: {" + blastInfo.EffectivePartArea + "}," +
                            " Positive Phase duration: {" + blastInfo.PositivePhaseDuration + "}," +
                            " Vessel mass: {" + Math.Round(part.vessel.totalMass * 1000f) + "}," +
                            " TimeIndex: {" + TimeIndex + "}," +
                            " TimePlanned: {" + eventToExecute.TimeToImpact + "}," +
                            " NegativePressure: {" + eventToExecute.IsNegativePressure + "}");
                    }

                    // Add Reverse Negative Event
                    ExplosionEvents.Enqueue(new PartBlastHitEvent()
                    {
                        Distance = Range - realDistance,
                        Part = part,
                        TimeToImpact = 2 * (Range / ExplosionVelocity) + (Range - realDistance) / ExplosionVelocity,
                        IsNegativePressure = true,
                        NegativeForce = blastInfo.VelocityChange * 0.25f
                    });

                    AddForceAtPosition(rb,
                        (eventToExecute.HitPoint + part.rb.velocity * TimeIndex - Position).normalized *
                        blastInfo.VelocityChange *
                        BDArmorySettings.EXP_IMP_MOD,
                        eventToExecute.HitPoint + part.rb.velocity * TimeIndex);

                    part.AddExplosiveDamage(blastInfo.Damage,
                                            Caliber, IsMissile);
                }
                else
                {
                    if (BDArmorySettings.DRAW_DEBUG_LABELS)
                    {
                        Debug.Log(
                            "[BDArmory]: Executing blast event Part: {" + part.name + "}, " +
                            " VelocityChange: {" + eventToExecute.NegativeForce + "}," +
                            " Distance: {" + realDistance + "}," +
                            " Vessel mass: {" + Math.Round(part.vessel.totalMass * 1000f) + "}," +
                            " TimeIndex: {" + TimeIndex + "}," +
                            " TimePlanned: {" + eventToExecute.TimeToImpact + "}," +
                            " NegativePressure: {" + eventToExecute.IsNegativePressure + "}");
                    }
                    AddForceAtPosition(rb, (Position - part.transform.position).normalized * eventToExecute.NegativeForce * BDArmorySettings.EXP_IMP_MOD * 0.25f, part.transform.position);
                }
            }
            catch
            {
                // ignored due to depending on previous event an object could be disposed
            }
        }        

        public static void CreateExplosion(Vector3 position, float tntMassEquivalent, string explModelPath, string soundPath, bool isMissile = true,float caliber = 0, Part explosivePart = null, Vector3 direction = default(Vector3))
        {
            var go = GameDatabase.Instance.GetModel(explModelPath);
            var soundClip = GameDatabase.Instance.GetAudioClip(soundPath);

            Quaternion rotation;
            if (direction == default(Vector3))
            {
                rotation = Quaternion.LookRotation(VectorUtils.GetUpDirection(position));
            }
            else
            {
                rotation = Quaternion.LookRotation(direction);
            }

            GameObject newExplosion = (GameObject) Instantiate(go, position, rotation);
            ExplosionFx eFx = newExplosion.AddComponent<ExplosionFx>();
            eFx.ExSound = soundClip;
            eFx.AudioSource = newExplosion.AddComponent<AudioSource>();
            eFx.AudioSource.minDistance = 200;
            eFx.AudioSource.maxDistance = 5500;
            eFx.AudioSource.spatialBlend = 1;
            eFx.Range = BlastPhysicsUtils.CalculateBlastRange(tntMassEquivalent);
            eFx.Position = position;
            eFx.Power = tntMassEquivalent;
            eFx.IsMissile = isMissile;
            eFx.Caliber = caliber;
            eFx.ExplosivePart = explosivePart;
            eFx.Direction = direction;

            if (tntMassEquivalent <= 5)
            {
                eFx.AudioSource.minDistance = 4f;
                eFx.AudioSource.maxDistance = 3000;
                eFx.AudioSource.priority = 9999;
            }
            newExplosion.SetActive(true);
            IEnumerator<KSPParticleEmitter> pe = newExplosion.GetComponentsInChildren<KSPParticleEmitter>().Cast<KSPParticleEmitter>()
                .GetEnumerator();
            while (pe.MoveNext())
            {
                if (pe.Current == null) continue;
                pe.Current.emit = true;

            }
            pe.Dispose();
        }

        public static void AddForceAtPosition(Rigidbody rb,Vector3 force,Vector3 position)
        {
            //////////////////////////////////////////////////////////
            // Add The force to part
            //////////////////////////////////////////////////////////
            if (rb == null) return;
            rb.AddForceAtPosition(force , position, ForceMode.VelocityChange);
            if (BDArmorySettings.DRAW_DEBUG_LABELS)
            {
                 Debug.Log("[BDArmory]: Force Applied | Explosive : " + Math.Round(force.magnitude, 2));
            }   
        }
    }

    public abstract class BlastHitEvent
    {
        public float Distance { get; set; }
        public float TimeToImpact { get; set; }
        public bool IsNegativePressure { get; set; }
    }

    internal class PartBlastHitEvent : BlastHitEvent
    {
        public Part Part { get; set; }
        public Vector3 HitPoint { get; set; }
        public float NegativeForce { get; set; }
    }

    internal class BuildingBlastHitEvent : BlastHitEvent
    {
        public DestructibleBuilding Building { get; set; }
    }
}

