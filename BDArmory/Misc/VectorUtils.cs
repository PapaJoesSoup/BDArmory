using System;
using UnityEngine;

namespace BDArmory.Misc
{
	public static class VectorUtils
	{
		public static float SignedAngle(Vector3 fromDirection, Vector3 toDirection, Vector3 referenceRight)
		{
			float angle = Vector3.Angle(fromDirection, toDirection);
			float sign = Mathf.Sign(Vector3.Dot(toDirection, referenceRight));
			float finalAngle = sign * angle;
			return finalAngle;
		}

		/// <summary>
		/// Same as SignedAngle, just using double precision for the cosine calculation.
        /// For very small angles the floating point precision starts to matter, as the cosine is close to 1, not to 0.
		/// </summary>
		public static float SignedAngleDP(Vector3 fromDirection, Vector3 toDirection, Vector3 referenceRight)
		{
			float angle = (float)Vector3d.Angle(fromDirection, toDirection);
			float sign = Mathf.Sign(Vector3.Dot(toDirection, referenceRight));
			float finalAngle = sign * angle;
			return finalAngle;
		}


		//from howlingmoonsoftware.com
		//calculates how long it will take for a target to be where it will be when a bullet fired now can reach it.
		//delta = initial relative position, vr = relative velocity, muzzleV = bullet velocity.
		public static float CalculateLeadTime(Vector3 delta, Vector3 vr, float muzzleV)
		{
			// Quadratic equation coefficients a*t^2 + b*t + c = 0
			float a = Vector3.Dot(vr, vr) - muzzleV*muzzleV;
			float b = 2f*Vector3.Dot(vr, delta);
			float c = Vector3.Dot(delta, delta);
			
			float det = b*b - 4f*a*c;
			
			// If the determinant is negative, then there is no solution
			if(det > 0f)
			{
				return 2f*c/(Mathf.Sqrt(det) - b);
			} 
			else 
			{
				return -1f;
			}	
		}

		/// <summary>
		/// Returns a value between -1 and 1 via Perlin noise.
		/// </summary>
		/// <returns>Returns a value between -1 and 1 via Perlin noise.</returns>
		/// <param name="x">The x coordinate.</param>
		/// <param name="y">The y coordinate.</param>
		public static float FullRangePerlinNoise(float x, float y)
		{
			float perlin = Mathf.PerlinNoise(x,y);

			perlin -= 0.5f;
			perlin *= 2;

			return perlin;
		}


		public static Vector3 RandomDirectionDeviation(Vector3 direction, float maxAngle)
		{
			return Vector3.RotateTowards(direction, UnityEngine.Random.rotation*direction, UnityEngine.Random.Range(0, maxAngle*Mathf.Deg2Rad), 0).normalized;
		}

		public static Vector3 WeightedDirectionDeviation(Vector3 direction, float maxAngle)
		{
			float random = UnityEngine.Random.Range(0f, 1f);
			float maxRotate = maxAngle*(random * random);
			maxRotate = Mathf.Clamp(maxRotate, 0, maxAngle)*Mathf.Deg2Rad;
			return Vector3.RotateTowards(direction, Vector3.ProjectOnPlane(UnityEngine.Random.onUnitSphere, direction), maxRotate, 0).normalized;
		}

		/// <summary>
		/// Converts world position to Lat,Long,Alt form.
		/// </summary>
		/// <returns>The position in geo coords.</returns>
		/// <param name="worldPosition">World position.</param>
		/// <param name="body">Body.</param>
		public static Vector3d WorldPositionToGeoCoords(Vector3d worldPosition, CelestialBody body)
		{
			if(!body)
			{
				//Debug.Log ("BahaTurret.VectorUtils.WorldPositionToGeoCoords body is null");
				return Vector3d.zero;
			}

			double lat = body.GetLatitude(worldPosition);
			double longi = body.GetLongitude(worldPosition);
			double alt = body.GetAltitude(worldPosition);
			return new Vector3d(lat,longi,alt);
		}

		public static Vector3 RotatePointAround(Vector3 pointToRotate, Vector3 pivotPoint, Vector3 axis, float angle)
		{
			Vector3 line = pointToRotate-pivotPoint;
			line = Quaternion.AngleAxis(angle, axis) * line;
			return pivotPoint + line;
		}

		public static Vector3 GetNorthVector(Vector3 position, CelestialBody body)
		{
			Vector3 geoPosA = WorldPositionToGeoCoords(position, body);
			Vector3 geoPosB = new Vector3(geoPosA.x+1, geoPosA.y, geoPosA.z);
			Vector3 north = GetWorldSurfacePostion(geoPosB, body)-GetWorldSurfacePostion(geoPosA, body);
			return Vector3.ProjectOnPlane(north, body.GetSurfaceNVector(geoPosA.x, geoPosA.y)).normalized;
		}
		
		public static Vector3 GetWorldSurfacePostion(Vector3d geoPosition, CelestialBody body)
		{
			if(!body)
			{
				return Vector3.zero;
			}
			return body.GetWorldSurfacePosition(geoPosition.x, geoPosition.y, geoPosition.z);
		}

		public static Vector3 GetUpDirection(Vector3 position)
		{
            if(FlightGlobals.currentMainBody == null) return Vector3.up;
			return (position-FlightGlobals.currentMainBody.transform.position).normalized;
		}

		public static bool SphereRayIntersect(Ray ray, Vector3 sphereCenter, double sphereRadius, out double distance)
		{
			Vector3 o = ray.origin;
			Vector3 l = ray.direction;
			Vector3d c = sphereCenter;
			double r = sphereRadius;

			double d;

			d = -(Vector3.Dot(l, o - c) + Math.Sqrt(Mathf.Pow(Vector3.Dot(l, o - c), 2) - (o - c).sqrMagnitude + (r * r))); 

			if(double.IsNaN(d))
			{
				distance = 0;
				return false;
			}
			else
			{
				distance = d;
				return true;
			}
		}
	}
}

