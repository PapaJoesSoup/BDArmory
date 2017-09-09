using BDArmory.Core.Extension;
using BDArmory.Parts;
using BDArmory.UI;
using UnityEngine;

namespace BDArmory
{
	public struct MissileLaunchParams
	{
		public float minLaunchRange;
		public float maxLaunchRange;

		private float rtr;
		/// <summary>
		/// Gets the maximum no-escape range.
		/// </summary>
		/// <value>The max no-escape range.</value>
		public float rangeTr
		{
			get
			{
				return rtr;
			}
		}


		public MissileLaunchParams(float min, float max)
		{
			minLaunchRange = min;
			maxLaunchRange = max;
			rtr = (max + min) / 2;
		}

		/// <summary>
		/// Gets the dynamic launch parameters.
		/// </summary>
		/// <returns>The dynamic launch parameters.</returns>
		/// <param name="launcherVelocity">Launcher velocity.</param>
		/// <param name="targetVelocity">Target velocity.</param>
		public static MissileLaunchParams GetDynamicLaunchParams(MissileBase missile, Vector3 targetVelocity, Vector3 targetPosition)
		{
			Vector3 launcherVelocity = missile.vessel.Velocity();
			float launcherSpeed = (float)missile.vessel.srfSpeed;
			float minLaunchRange = missile.minStaticLaunchRange;
			float maxLaunchRange = missile.maxStaticLaunchRange;

			float rangeAddMin = 0;
			float rangeAddMax = 0;
			float relSpeed;

			Vector3 relV = targetVelocity - launcherVelocity;
			Vector3 vectorToTarget = targetPosition - missile.part.transform.position;
			Vector3 relVProjected = Vector3.Project(relV, vectorToTarget);
			relSpeed = -Mathf.Sign(Vector3.Dot(relVProjected, vectorToTarget)) * relVProjected.magnitude;
			

			rangeAddMin += relSpeed * 2;
			rangeAddMax += relSpeed * 8;
			rangeAddMin += launcherSpeed * 2;
			rangeAddMax += launcherSpeed * 2;

			double diffAlt = missile.vessel.altitude - FlightGlobals.getAltitudeAtPos(targetPosition);

			rangeAddMax += (float)diffAlt;

			float min = Mathf.Clamp(minLaunchRange + rangeAddMin, 0, BDArmorySettings.MAX_ENGAGEMENT_RANGE);
			float max = Mathf.Clamp(maxLaunchRange + rangeAddMax, min+100, BDArmorySettings.MAX_ENGAGEMENT_RANGE);

			return new MissileLaunchParams(min, max);
		}


	}
}

