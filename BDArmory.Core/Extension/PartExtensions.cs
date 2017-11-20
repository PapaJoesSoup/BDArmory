using System.Collections.Generic;
using System;
using System.Net;
using BDArmory.Core.Module;
using BDArmory.Core.Services;
using UniLinq;
using UnityEngine;

namespace BDArmory.Core.Extension
{
    public static class PartExtensions
    {
        public static void AddDamage(this Part p, float damage)
        {
            //////////////////////////////////////////////////////////
            // Basic Add Hitpoints for compatibility
            //////////////////////////////////////////////////////////
            damage = (float)Math.Round(damage, 2);
            Dependencies.Get<DamageService>().AddDamageToPart_svc(p, damage);
            Debug.Log("[BDArmory]: Standard Hitpoints Applied : " + damage);

        }

        public static void AddExplosiveDamage(this Part p,
                                               float explosiveDamage,
                                               float DMG_MULTIPLIER,
                                               float EXP_HEAT_MOD,
                                               float caliber,
                                               bool isMissile)
        {

            //////////////////////////////////////////////////////////
            // Explosive Hitpoints
            //////////////////////////////////////////////////////////
            float damage = (DMG_MULTIPLIER / 100) * EXP_HEAT_MOD * explosiveDamage;
                          
                           

            //////////////////////////////////////////////////////////
            // Caliber Adjustments
            //////////////////////////////////////////////////////////
            if (caliber < 50 && !isMissile)
            {
                damage *= 0.0235f;
            }
                                  
            //////////////////////////////////////////////////////////
            // Armor Reduction factors
            //////////////////////////////////////////////////////////
            if (p.HasArmor())
            {
                float armorReduction = 0;
                damage = damage - ((damage * p.GetArmorPercentage()) / 10);

                if (!isMissile)
                {
                    if (caliber < 50)
                    {
                        damage /= 100; //penalty for low-mid caliber HE rounds hitting armor panels
                    }
                    armorReduction = damage / 2;
                }
                else
                {
                    armorReduction = damage / 8;
                }
                p.ReduceArmor(armorReduction);

            }

            //////////////////////////////////////////////////////////
            // Do The Hitpoints
            //////////////////////////////////////////////////////////

            Dependencies.Get<DamageService>().AddDamageToPart_svc(p, damage);
            Debug.Log("[BDArmory]: Explosive Hitpoints Applied to "+p.name+": " + Math.Round(damage, 2));
        }

        public static void AddDamage_Ballistic(this Part p,
                                               float mass,
                                               float caliber,
                                               float multiplier,
                                               float penetrationfactor,
                                               float DMG_MULTIPLIER,
                                               float bulletDmgMult,
                                               float impactVelocity,
                                               bool explosive)
        {
            

            //////////////////////////////////////////////////////////
            // Basic Kinetic Formula
            //////////////////////////////////////////////////////////
            //Hitpoints mult for scaling in settings
            //1e-4 constant for adjusting MegaJoules for gameplay

            double damage = ((0.5f * (mass * Math.Pow(impactVelocity, 2)))
                            * (DMG_MULTIPLIER / 100) * bulletDmgMult
                            * 1e-4f);

            //Explosive bullets should not cause much penetration damage, most damage needs to come from explosion
            if (explosive) damage *= 0.25f;
            
            //penetration multipliers   
            damage *= multiplier * Mathf.Clamp(penetrationfactor,penetrationfactor,1.5f);

            //Caliber Adjustments for Gameplay balance
            if (caliber <= 30f) 
            {
               damage *= 5f;
            }

            //As armor is decreased level of damage should increase
            //Ideally this would be logarithmic but my math is lacking right now... 
                
            //damage /= Mathf.Max(1,(float) armorPCT_ * 100);
            //double damage_d = (Mathf.Clamp((float)Math.Log10(armorPCT_),10f,100f) + 5f) * damage;
            //damage = (float)damage_d;

            if (p.HasArmor())
            {
                double armorMass_ = p.GetArmorMass();
                double armorPCT_ = p.GetArmorPercentage();
                
                //Armor limits Damage
                damage = damage - (damage * armorPCT_);

                //penalty for low caliber rounds,not if armor is very low
                if (caliber <= 30f && armorMass_ >= 20d) damage *= 0.125f;
            }
            

            //////////////////////////////////////////////////////////
            // Do The Hitpoints
            //////////////////////////////////////////////////////////
            Dependencies.Get<DamageService>().AddDamageToPart_svc(p, (float)damage);
            Debug.Log("[BDArmory]: mass: " + mass + " caliber: " + caliber + " multiplier: " + multiplier + " velocity: "+ impactVelocity +" penetrationfactor: " + penetrationfactor);
            Debug.Log("[BDArmory]: Ballistic Hitpoints Applied : " + Math.Round(damage, 2));
        }


        public static void AddForceToPart(Rigidbody rb, Vector3 force, Vector3 position,ForceMode mode)
        {

            //////////////////////////////////////////////////////////
            // Add The force to part
            //////////////////////////////////////////////////////////

            rb.AddForceAtPosition(force, position, mode);
            Debug.Log("[BDArmory]: Force Applied : " + Math.Round(force.magnitude,2));
        }

        public static void Destroy(this Part p)
        {
            Dependencies.Get<DamageService>().SetDamageToPart_svc(p,-1);
        }

        public static bool HasArmor(this Part p)
        {
            return p.GetArmorMass() > 15f;
        }

        public static float Damage(this Part p)
         {		
             return Dependencies.Get<DamageService>().GetPartDamage_svc(p);		
         }		
 		
        public static float MaxDamage(this Part p)
         {		
             return Dependencies.Get<DamageService>().GetMaxPartDamage_svc(p);		
         }

        public static void ReduceArmor(this Part p, double massToReduce)
        {
            if (!p.HasArmor()) return;
            massToReduce = Math.Max(0.10,Math.Round(massToReduce, 2));
            Dependencies.Get<DamageService>().ReduceArmor_svc(p, (float) massToReduce );
            Debug.Log("[BDArmory]: Armor Removed : " + massToReduce);
        }
        
        /// <summary>
        /// This method returns the amount of Armor resource
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public static double GetArmorMass(this Part p)
        {
            if (p == null) return 0d;        
            return Dependencies.Get<DamageService>().GetPartArmor_svc(p);
        }

        public static float GetArmorPercentage(this Part p)
        {
            if (p == null) return 0;
            float armor_ = Dependencies.Get<DamageService>().GetPartArmor_svc(p);
            float maxArmor_ = Dependencies.Get<DamageService>().GetMaxArmor_svc(p);

            return armor_ / maxArmor_;
        }

        //Thanks FlowerChild
        //refreshes part action window
        public static void RefreshAssociatedWindows(this Part part)
        {
            IEnumerator<UIPartActionWindow> window = UnityEngine.Object.FindObjectsOfType(typeof(UIPartActionWindow)).Cast<UIPartActionWindow>().GetEnumerator();
            while (window.MoveNext())
            {
                if (window.Current == null) continue;
                if (window.Current.part == part)
                {
                    window.Current.displayDirty = true;
                }
            }
            window.Dispose();
        }

        public static bool IsMissile(this Part part)
        {
            return part.Modules.Contains("MissileBase") || part.Modules.Contains("MissileLauncher") ||
                   part.Modules.Contains("BDModularGuidance");
        }

        public static float GetArea(this Part part)
        {
            //var boundsSize = PartGeometryUtil.MergeBounds(part.GetRendererBounds(), part.transform).size;
            //float sfcAreaCalc =  2f * (boundsSize.x * boundsSize.y) + 2f * (boundsSize.y * boundsSize.z) + 2f * (boundsSize.x * boundsSize.z);
            //Debug.Log("[BDArmory]: Surface Area1: " + part.surfaceAreas.magnitude);
            //Debug.Log("[BDArmory]: Surface Area2: " + sfcAreaCalc);

            return part.surfaceAreas.magnitude;
        }

        public static float GetVolume(this Part part)
        {
            var boundsSize = PartGeometryUtil.MergeBounds(part.GetRendererBounds(), part.transform).size;
            return boundsSize.x * boundsSize.y * boundsSize.z;
        }

        public static float GetDensity (this Part part)
        {
            return (part.mass * 1000) / part.GetVolume();
        }
        public static bool IsAero(this Part part)
        {
            return part.Modules.Contains("ModuleControlSurface") ||
                   part.Modules.Contains("ModuleLiftingSurface");
        }

    }
}