using System;
using System.Collections.Generic;
using BDArmory.FX;
using BDArmory.Misc;
using BDArmory.UI;
using UniLinq;
using UnityEngine;

namespace BDArmory.CounterMeasure
{
    public class CMFlare : MonoBehaviour
    {
        public Vessel sourceVessel;
        Vector3 relativePos;

        List<KSPParticleEmitter> pEmitters; // = new List<KSPParticleEmitter>();
        List<BDAGaplessParticleEmitter> gaplessEmitters; // = new List<BDAGaplessParticleEmitter>();

        Light[] lights;
        float startTime;

        public Vector3 startVelocity;

        public bool alive = true;

        Vector3 upDirection;

        public Vector3 velocity;

        public float thermal; //heat value
        float minThermal;
        float startThermal;

        float lifeTime = 5;

        void OnEnable()
        {
            // OLD:
            //thermal = BDArmorySettings.FLARE_THERMAL*UnityEngine.Random.Range(0.45f, 1.25f);
            // NEW: generate flare within spectrum of emitting vessel's heat signature
            thermal = BDATargetManager.GetVesselHeatSignature(sourceVessel) * UnityEngine.Random.Range(0.65f, 1.75f);
            startThermal = thermal;
            minThermal = startThermal * 0.3f;

            if (gaplessEmitters == null || pEmitters == null)
            {
                gaplessEmitters = new List<BDAGaplessParticleEmitter>();

                pEmitters = new List<KSPParticleEmitter>();

                IEnumerator<KSPParticleEmitter> pe = gameObject.GetComponentsInChildren<KSPParticleEmitter>().Cast<KSPParticleEmitter>().GetEnumerator();
                while (pe.MoveNext())
                {
                    if (pe.Current == null) continue;
                    if (pe.Current.useWorldSpace)
                    {
                        BDAGaplessParticleEmitter gpe = pe.Current.gameObject.AddComponent<BDAGaplessParticleEmitter>();
                        gaplessEmitters.Add(gpe);
                        gpe.emit = true;
                    }
                    else
                    {
                        EffectBehaviour.AddParticleEmitter(pe.Current);
                        pEmitters.Add(pe.Current);                     
                        pe.Current.emit = true;
                    }
                }
                pe.Dispose();
            }
            List<BDAGaplessParticleEmitter>.Enumerator gEmitter = gaplessEmitters.GetEnumerator();
            while (gEmitter.MoveNext())
            {
                if (gEmitter.Current == null) continue;
                gEmitter.Current.emit = true;
            }
            gEmitter.Dispose();

            List<KSPParticleEmitter>.Enumerator pEmitter = pEmitters.GetEnumerator();
            while (pEmitter.MoveNext())
            {
                if (pEmitter.Current == null) continue;
                pEmitter.Current.emit = true;
            }
            pEmitter.Dispose();

            BDArmorySettings.numberOfParticleEmitters++;


            if (lights == null)
            {
                lights = gameObject.GetComponentsInChildren<Light>();
            }

            IEnumerator<Light> lgt = lights.AsEnumerable().GetEnumerator();
            while (lgt.MoveNext())
            {
                if (lgt.Current == null) continue;
                lgt.Current.enabled = true;
            }
            lgt.Dispose();
            startTime = Time.time;

            //ksp force applier
            //gameObject.AddComponent<KSPForceApplier>().drag = 0.4f;


            BDArmorySettings.Flares.Add(this);            

            if (sourceVessel != null)
            {
                relativePos = transform.position - sourceVessel.transform.position;
            }

            upDirection = VectorUtils.GetUpDirection(transform.position);

            velocity = startVelocity;
        }

        void FixedUpdate()
        {
            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            if (velocity != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(velocity, upDirection);
            }


            //Particle effects
            Vector3 downForce = Vector3.zero;
            //downforce

            if (sourceVessel != null)
            {
                downForce = (Mathf.Clamp((float) sourceVessel.srfSpeed, 0.1f, 150)/150)*
                            Mathf.Clamp01(20/Vector3.Distance(sourceVessel.transform.position, transform.position))*20*
                            -upDirection;
            }

            //turbulence
            List<BDAGaplessParticleEmitter>.Enumerator gEmitter = gaplessEmitters.GetEnumerator();
            while (gEmitter.MoveNext())
            {
                if (gEmitter.Current == null) continue;
                if (!gEmitter.Current.pEmitter) continue;
                try
                {
                    gEmitter.Current.pEmitter.worldVelocity = 2*ParticleTurbulence.flareTurbulence + downForce;
                }
                catch (NullReferenceException)
                {
                    Debug.LogWarning("CMFlare NRE setting worldVelocity");
                }

                try
                {
                    if (FlightGlobals.ActiveVessel && FlightGlobals.ActiveVessel.atmDensity <= 0)
                    {
                        gEmitter.Current.emit = false;
                    }
                }
                catch (NullReferenceException)
                {
                    Debug.LogWarning("CMFlare NRE checking density");
                }
            }
            gEmitter.Dispose();
            //

            //thermal decay
            thermal = Mathf.MoveTowards(thermal, minThermal,
                ((thermal - minThermal)/lifeTime)*Time.fixedDeltaTime);


            //floatingOrigin fix
            if (sourceVessel != null)
            {
                if (((transform.position - sourceVessel.transform.position) - relativePos).sqrMagnitude > 800*800)
                {
                    transform.position = sourceVessel.transform.position + relativePos;
                }

                relativePos = transform.position - sourceVessel.transform.position;
            }
            //


            if (Time.time - startTime > lifeTime) //stop emitting after lifeTime seconds
            {
                alive = false;
                BDArmorySettings.Flares.Remove(this);

                List<KSPParticleEmitter>.Enumerator pe = pEmitters.GetEnumerator();
                while (pe.MoveNext())
                {
                    if (pe.Current == null) continue;
                    pe.Current.emit = false;
                }
                pe.Dispose();

                List<BDAGaplessParticleEmitter>.Enumerator gpe = gaplessEmitters.GetEnumerator();
                while (gpe.MoveNext())
                {
                    if (gpe.Current == null) continue;
                    gpe.Current.emit = false;
                }
                gpe.Dispose();

                IEnumerator<Light> lgt = lights.AsEnumerable().GetEnumerator();
                while (lgt.MoveNext())
                {
                    if (lgt.Current == null) continue;
                    lgt.Current.enabled = false;
                }
                lgt.Dispose();
            }


            if (Time.time - startTime > lifeTime + 11) //disable object after x seconds
            {
                BDArmorySettings.numberOfParticleEmitters--;
                gameObject.SetActive(false);
                return;
            }


            //physics
            //atmospheric drag (stock)
            float simSpeedSquared = velocity.sqrMagnitude;
            Vector3 currPos = transform.position;
            float mass = 0.001f;
            float drag = 1f;
            Vector3 dragForce = (0.008f*mass)*drag*0.5f*simSpeedSquared*
                                (float)
                                FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(currPos),
                                    FlightGlobals.getExternalTemperature(), FlightGlobals.currentMainBody)*
                                velocity.normalized;

            velocity -= (dragForce/mass)*Time.fixedDeltaTime;
            //

            //gravity
            if (FlightGlobals.RefFrameIsRotating)
                velocity += FlightGlobals.getGeeForceAtPosition(transform.position)*Time.fixedDeltaTime;

            transform.position += velocity*Time.fixedDeltaTime;
        }
    }
}