using UnityEngine;

namespace BDArmory.Core.Extension
{
    public static class VesselExtensions
    {
        public static bool InOrbit(this Vessel v)
        {
            return !v.LandedOrSplashed &&
                   (v.situation == Vessel.Situations.ORBITING ||
                    v.situation == Vessel.Situations.SUB_ORBITAL ||
                    v.situation == Vessel.Situations.ESCAPING);
        }

        public static bool InVacuum(this Vessel v)
        {
            return v.atmDensity <= 0.001;
        }

        public static Vector3d Velocity(this Vessel v)
        {
            if (!v.InOrbit())
            {
                return v.srf_velocity;
            }
            else
            {
                return v.obt_velocity;
            }
        }
       
    }
}
