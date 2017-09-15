using System;
using System.Collections;
using BDArmory.Armor;
using BDArmory.Core.Extension;
using BDArmory.FX;
using BDArmory.Parts;
using BDArmory.Shaders;
using BDArmory.UI;
using UnityEngine;

namespace BDArmory
{
    public class PooledBullet : MonoBehaviour
    {

        #region Declarations

        public BulletInfo bullet;
        public float leftPenetration;

        public enum PooledBulletTypes
        {
            Standard,
            Explosive
        }

        public enum BulletDragTypes
        {
            None,
            AnalyticEstimate,
            NumericalIntegration
        }

        public PooledBulletTypes bulletType;
        public BulletDragTypes dragType;

        public Vessel sourceVessel;
        public Color lightColor = Misc.Misc.ParseColor255("255, 235, 145, 255");
        public Color projectileColor;
        public string bulletTexturePath;
        public bool fadeColor;
        public Color startColor;
        Color currentColor;
        public bool bulletDrop = true;
        public float tracerStartWidth = 1;
        public float tracerEndWidth = 1;
        public float tracerLength = 0;
        public float tracerDeltaFactor = 1.35f;
        public float tracerLuminance = 1;
        public float initialSpeed;

        public Vector3 prevPosition;
        public Vector3 currPosition;

        //explosive parameters
        public float radius = 30;
        public float blastPower = 8;
        public float blastHeat = -1;
        public float bulletDmgMult = 1;
        public string explModelPath;
        public string explSoundPath;
        //

        Vector3 startPosition;
        public bool airDetonation = false;
        public float detonationRange = 3500;
        float randomWidthScale = 1;
        LineRenderer bulletTrail;
        Vector3 sourceOriginalV;
        public float maxDistance;
        Light lightFlash;
        bool wasInitiated;
        public Vector3 currentVelocity;
        public float mass;
        public float caliber;
        public float ballisticCoefficient;
        public float flightTimeElapsed;
        bool collisionEnabled;
        public static Shader bulletShader;
        public static bool shaderInitialized;

        #endregion
        
        void OnEnable()
        {
            startPosition = transform.position;
            collisionEnabled = false;

            if (!wasInitiated)
            {
                //projectileColor.a = projectileColor.a/2;
                //startColor.a = startColor.a/2;
            }

            projectileColor.a = Mathf.Clamp(projectileColor.a, 0.25f, 1f);
            startColor.a = Mathf.Clamp(startColor.a, 0.25f, 1f);
            currentColor = projectileColor;
            if (fadeColor)
            {
                currentColor = startColor;
            }

            prevPosition = gameObject.transform.position;

            sourceOriginalV = sourceVessel.Velocity();

            if (!lightFlash)
            {
                lightFlash = gameObject.AddComponent<Light>();
                lightFlash.type = LightType.Point;
                lightFlash.range = 8;
                lightFlash.intensity = 1;
            }
            lightFlash.color = lightColor;
            lightFlash.enabled = true;


            //tracer setup
            if (!bulletTrail)
            {
                bulletTrail = gameObject.AddComponent<LineRenderer>();
            }
            if (!wasInitiated)
            {
                bulletTrail.SetVertexCount(2);
            }
            bulletTrail.SetPosition(0, transform.position);
            bulletTrail.SetPosition(1, transform.position);

            if (!shaderInitialized)
            {
                shaderInitialized = true;
                bulletShader = BDAShaderLoader.BulletShader; 
            }

            if (!wasInitiated)
            {               
                bulletTrail.material = new Material(bulletShader);
                randomWidthScale = UnityEngine.Random.Range(0.5f, 1f);
                gameObject.layer = 15;
            }

            bulletTrail.material.mainTexture = GameDatabase.Instance.GetTexture(bulletTexturePath, false);
            bulletTrail.material.SetColor("_TintColor", currentColor);
            bulletTrail.material.SetFloat("_Lum", tracerLuminance);

            tracerStartWidth *= 2f;
            tracerEndWidth *= 2f;
            
            leftPenetration = 1;
            wasInitiated = true;
            StartCoroutine(FrameDelayedRoutine());
        }

        IEnumerator FrameDelayedRoutine()
        {
            yield return new WaitForFixedUpdate();
            lightFlash.enabled = false;
            collisionEnabled = true;
        }

        void OnWillRenderObject()
        {
            if (!gameObject.activeInHierarchy)
            {
                return;
            }
            Camera currentCam = Camera.current;
            if (TargetingCamera.IsTGPCamera(currentCam))
            {
                UpdateWidth(currentCam, 4);
            }
            else
            {
                UpdateWidth(currentCam, 1);
            }
        }

        void FixedUpdate()
        {
            float distanceFromStart = Vector3.Distance(transform.position, startPosition);
            if (!gameObject.activeInHierarchy)
            {
                return;
            }
            flightTimeElapsed += TimeWarp.fixedDeltaTime; //calculate flight time for drag purposes

            if (bulletDrop && FlightGlobals.RefFrameIsRotating)
            {
                currentVelocity += FlightGlobals.getGeeForceAtPosition(transform.position)*TimeWarp.fixedDeltaTime;
            }
            if (dragType == BulletDragTypes.NumericalIntegration)
            {
                Vector3 dragAcc = currentVelocity*currentVelocity.magnitude*
                                  (float)
                                  FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(transform.position),
                                      FlightGlobals.getExternalTemperature(transform.position));
                dragAcc *= 0.5f;
                dragAcc /= ballisticCoefficient;

                currentVelocity -= dragAcc*TimeWarp.fixedDeltaTime;
                //numerical integration; using Euler is silly, but let's go with it anyway
            }

            if (tracerLength == 0)
            {
                bulletTrail.SetPosition(0,
                    transform.position +
                    (currentVelocity*tracerDeltaFactor*TimeWarp.fixedDeltaTime/TimeWarp.CurrentRate) -
                    (FlightGlobals.ActiveVessel.Velocity() * TimeWarp.fixedDeltaTime));
            }
            else
            {
                bulletTrail.SetPosition(0,
                    transform.position + ((currentVelocity - sourceOriginalV).normalized*tracerLength));
            }
            if (fadeColor)
            {
                FadeColor();
                bulletTrail.material.SetColor("_TintColor", currentColor*tracerLuminance);
            }

            bulletTrail.SetPosition(1, transform.position);

            currPosition = gameObject.transform.position;

            if (distanceFromStart > maxDistance)//kill bullet if it goes past the max allowed distance
            {
                KillBullet();
                return;
            }

            if (collisionEnabled)
            {
                float dist = initialSpeed*TimeWarp.fixedDeltaTime;

                Ray ray = new Ray(prevPosition, currPosition - prevPosition);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, dist, 557057))
                {
                    Part hitPart = null;

                    try
                    {
                        hitPart = hit.collider.gameObject.GetComponentInParent<Part>();
                    }
                    catch (NullReferenceException)
                    {
                        return;
                    }
                    ///////////////////////////////////////////////////////////////////////                                 
                    // High Explosive Detonation
                    ///////////////////////////////////////////////////////////////////////
                    if (hitPart == null || (hitPart != null && hitPart.vessel != sourceVessel))
                    {

                        if (bulletType == PooledBulletTypes.Explosive)//if bullet hits and is HE, detonate and kill bullet, skip the rest as to not do resource intensive penetration calcs
                        {
                            ExplosionFX.CreateExplosion(hit.point - (ray.direction * 0.1f), radius, blastPower,
                                blastHeat, sourceVessel, currentVelocity.normalized, explModelPath, explSoundPath);
                            KillBullet();
                            return;
                        }
                    }
                    ///////////////////////////////////////////////////////////////////////                                 
                    // KE Impact Damage                                                                
                    ///////////////////////////////////////////////////////////////////////
                    float hitAngle = Vector3.Angle(currentVelocity, -hit.normal);
                    float anglemultiplier = (float)Math.Cos(3.14 * hitAngle / 180.0);
                    float impactVelocity = currentVelocity.magnitude;
                    if (dragType == BulletDragTypes.AnalyticEstimate)
                    {
                        float analyticDragVelAdjustment =
                            (float)
                            FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(currPosition),
                                FlightGlobals.getExternalTemperature(currPosition));
                        analyticDragVelAdjustment *= flightTimeElapsed * initialSpeed;
                        analyticDragVelAdjustment += 2 * ballisticCoefficient;

                        analyticDragVelAdjustment = 2 * ballisticCoefficient * initialSpeed / analyticDragVelAdjustment;
                        //velocity as a function of time under the assumption of a projectile only acted upon by drag with a constant drag area

                        analyticDragVelAdjustment = analyticDragVelAdjustment - initialSpeed;
                        //since the above was velocity as a function of time, but we need a difference in drag, subtract the initial velocity
                        //the above number should be negative...
                        impactVelocity += analyticDragVelAdjustment; //so add it to the impact velocity
                    }

                    //hitting a vessel Part
                    if (hitPart != null && !hitPart.partInfo.name.Contains("Strut")) //when a part is hit, execute damage code (ignores struts to keep those from being abused as armor)(no, because they caused weird bugs :) -BahamutoD)
                    {
                        float heatDamage = (mass / (hitPart.crashTolerance * hitPart.mass)) *
                                           (impactVelocity * impactVelocity / 10) * // was impactVelocity * ImpactVelocity
                                           BDArmorySettings.DMG_MULTIPLIER;// global damage multiplier (100% used for balancing)

                        //bulletDmgMult;// individual bullet modifier, default 1

                        //damage penalties for weapons using new penetration system (most does not apply to "legacy" parts or mods)
                        if (hitPart.crashTolerance >= 80)//armor gets damage penalty based on angle of impact
                        {
                            heatDamage *= anglemultiplier;
                        }
                        if (hitPart.crashTolerance >= 80 && caliber <= 100 && caliber >= 0)//penalty for guns below 150mm hitting armor plate (lower caliber AP needs to rely on penetration)
                        {
                            heatDamage *= caliber / 100;
                        }

                        if (BDArmorySettings.DRAW_DEBUG_LABELS)
                            Debug.Log("[BDArmory]: Hit! damage applied: " + heatDamage);

                        if (hitPart.vessel != sourceVessel)
                        {
                            hitPart.AddDamage(heatDamage);
                        }
                    }

                    //hitting a Building
                    DestructibleBuilding hitBuilding = null;
                    try
                    {
                        hitBuilding = hit.collider.gameObject.GetComponentUpwards<DestructibleBuilding>();
                    }
                    catch (NullReferenceException)
                    {
                    }
                    if (hitBuilding != null && hitBuilding.IsIntact)
                    {
                        float damageToBuilding = mass * initialSpeed * initialSpeed * BDArmorySettings.DMG_MULTIPLIER /
                                                 12000;
                        hitBuilding.AddDamage(damageToBuilding);
                        if (hitBuilding.Damage > hitBuilding.impactMomentumThreshold)
                        {
                            hitBuilding.Demolish();
                        }
                        if (BDArmorySettings.DRAW_DEBUG_LABELS)
                            Debug.Log("[BDArmory]: bullet hit destructible building! Damage: " +
                                      (damageToBuilding).ToString("0.00") + ", total Damage: " + hitBuilding.Damage);
                    }
                    ///////////////////////////////////////////////////////////////////////                                 
                    // Hitting Ground
                    ///////////////////////////////////////////////////////////////////////
                    if (hitPart == null)//kill bullet if impacted part isnt defined
                    {
                        if (BDArmorySettings.BULLET_HITS)
                        {
                            BulletHitFX.CreateBulletHit(hit.point, hit.normal, true);
                        }
                        KillBullet();
                        return;
                    }
                    ///////////////////////////////////////////////////////////////////////                                 
                    // Armor Penetration
                    ///////////////////////////////////////////////////////////////////////
                    ArmorPenetration.BulletPenetrationData armorData = new ArmorPenetration.BulletPenetrationData(ray, hit);
                    ArmorPenetration.DoPenetrationRay(armorData);
                    float penetration = 0;//penetration of 0 for legacy support
                    if (caliber > 10)//use the "krupp" penetration formula for anything larger then HMGs
                    {
                        penetration = 16 * impactVelocity * (float)Math.Sqrt(mass) / (float)Math.Sqrt(caliber);
                    }
                    float thickness = 10;//regular KSP parts: 10mm armor
                    if (hitPart.crashTolerance >= 80)//structural parts: 30mm armor
                    {
                        thickness = 30 / anglemultiplier;
                    }
                    for (int i = 0; i < hitPart.Resources.Count; i++){//"armor" resource containing parts, armor set to how much resource inside
                        PartResource currentr = hitPart.Resources[i];
                        if (currentr.resourceName == "Armor")
                        {
                                thickness = (float)currentr.amount / anglemultiplier;
                                break;
                        }
                    }


                    bool fulllyPenetrated = penetration > thickness;//check whether bullet penetrates the plate
                    if (fulllyPenetrated)
                    {
                        if (BDArmorySettings.BULLET_HITS)
                        {
                            BulletHitFX.CreateBulletHit(hit.point, hit.normal, false);
                        }
                        currentVelocity = currentVelocity * (float)Math.Sqrt(thickness / penetration);//lower velocity on penetrating armor plate (does not affect low impact parts so that rounds can go through entire tank easily)                                transform.position = armorData.hitResultOut.point;
                        flightTimeElapsed -= Time.fixedDeltaTime;
                        prevPosition = transform.position;
                        FixedUpdate();
                        return;
                    }
                    else
                    {
                        if (BDArmorySettings.BULLET_HITS)
                        {
                            BulletHitFX.CreateBulletHit(hit.point, hit.normal, false);
                        }
                        KillBullet();//kill bullet if no penetration
                        return;
                    }
                    
                    ///////////////////////////////////////////////////////////////////////
                    //Post Impact
                    ///////////////////////////////////////////////////////////////////////

                    
                }
            }

            ///////////////////////////////////////////////////////////////////////
            //Flak Explosion (air detonation/proximity fuse)
            ///////////////////////////////////////////////////////////////////////
            if (bulletType == PooledBulletTypes.Explosive && airDetonation && distanceFromStart > detonationRange)
            {
                //detonate
                ExplosionFX.CreateExplosion(transform.position, radius, blastPower, blastHeat, sourceVessel,
                    currentVelocity.normalized, explModelPath, explSoundPath);
                KillBullet();
                return;
            }

            ///////////////////////////////////////////////////////////////////////
            //Bullet Translation
            ///////////////////////////////////////////////////////////////////////
            prevPosition = currPosition;
            //move bullet            
            transform.position += currentVelocity*Time.fixedDeltaTime;
        }

        public void UpdateWidth(Camera c, float resizeFactor)
        {
            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            float fov = c.fieldOfView;
            float factor = (fov/60)*resizeFactor*
                           Mathf.Clamp(Vector3.Distance(transform.position, c.transform.position), 0, 3000)/50;
            float width1 = tracerStartWidth*factor*randomWidthScale;
            float width2 = tracerEndWidth*factor*randomWidthScale;

            bulletTrail.SetWidth(width1, width2);
        }

        void KillBullet()
        {
            gameObject.SetActive(false);
        }

        void FadeColor()
        {
            Vector4 currentColorV = new Vector4(currentColor.r, currentColor.g, currentColor.b, currentColor.a);
            Vector4 endColorV = new Vector4(projectileColor.r, projectileColor.g, projectileColor.b, projectileColor.a);
            //float delta = (Vector4.Distance(currentColorV, endColorV)/0.15f) * TimeWarp.fixedDeltaTime;
            float delta = TimeWarp.fixedDeltaTime;
            Vector4 finalColorV = Vector4.MoveTowards(currentColor, endColorV, delta);
            currentColor = new Color(finalColorV.x, finalColorV.y, finalColorV.z, Mathf.Clamp(finalColorV.w, 0.25f, 1f));
        }

        bool RicochetOnPart(Part p, float angleFromNormal, float impactVel)
        {
            float hitTolerance = p.crashTolerance;
            //15 degrees should virtually guarantee a ricochet, but 75 degrees should nearly always be fine
            float chance = (((angleFromNormal - 5)/75)*(hitTolerance/150))*100/Mathf.Clamp01(impactVel/600);
            float random = UnityEngine.Random.Range(0f, 100f);
            //if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory]: Ricochet chance: "+chance);
            if (random < chance)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private float CalculateExplosionProbability(Part part)
        {
            float probability = 0;
            for (int i = 0; i < part.Resources.Count; i++)
            {
                PartResource current = part.Resources[i];
                switch (current.resourceName)
                {
                    case "LiquidFuel":
                        probability += (float) (current.amount/current.maxAmount);
                        break;
                    case "Oxidizer":
                        probability += (float) (current.amount/current.maxAmount);
                        break;
                }
            }
            if (bulletType == PooledBulletTypes.Explosive)
                probability += 0.1f;
            return probability;
        }
    }
}