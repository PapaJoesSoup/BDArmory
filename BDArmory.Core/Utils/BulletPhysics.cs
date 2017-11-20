using UnityEngine;
using System.Collections;

namespace BDArmory.Core.Utils
{
    public class BulletPhysics : MonoBehaviour
    {

        public static Vector3 CalculateDrag(Vector3 velocity,float bulletMass,float caliber)
        {
            //F_drag = k * v^2 = m * a
            //k = 0.5 * C_d * rho * A 

            //float m = 0.2f; // kg
            //float C_d = 0.295f;
            //float A = Mathf.PI * 0.05f * 0.05f; // m^2
            float rho = 1.225f; // kg/m3

            //float k = 0.5f * C_d * rho * A;

            //float vSqr = velocityVec.sqrMagnitude;

            //float aDrag = (k * vSqr) / m;

            //Has to be in a direction opposite of the bullet's velocity vector
            //Vector3 dragVec = aDrag * velocityVec.normalized * -1f;

            ///////////////////////////////////////////////////////
            float bulletDragArea = Mathf.PI * Mathf.Pow(caliber / 2f, 2f);
            float bulletBallisticCoefficient = ((bulletMass * 1000) / (bulletDragArea * 0.295f));

            float k = 0.5f * bulletBallisticCoefficient * rho * bulletDragArea;
            float vSqr = velocity.sqrMagnitude;
            float aDrag = (k * vSqr) / bulletMass;

            Vector3 dragVec = aDrag * velocity.normalized * -1f;

            return dragVec;
        }

    }
}
