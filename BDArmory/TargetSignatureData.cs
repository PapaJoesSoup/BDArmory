using System;
using BDArmory.CounterMeasure;
using BDArmory.Misc;
using BDArmory.Radar;
using BDArmory.UI;
using UnityEngine;

namespace BDArmory
{
	public struct TargetSignatureData : IEquatable<TargetSignatureData>
	{
		public Vector3 velocity;
		public Vector3 geoPos;
		public Vector3 acceleration;
		public bool exists;
		public float timeAcquired;
        public float signalStrength;
		public TargetInfo targetInfo;
		public BDArmorySettings.BDATeams team;
		public Vector2 pingPosition;
		public VesselECMJInfo vesselJammer;
		public ModuleRadar lockedByRadar;
		public Vessel vessel;
		bool orbital;
		Orbit orbit;

		public bool Equals(TargetSignatureData other)
		{
			return 
				exists == other.exists &&
				geoPos == other.geoPos &&
				timeAcquired == other.timeAcquired;
		}
        
		public TargetSignatureData(Vessel v, float _signalStrength)
		{
			orbital = false;
			orbit = null;
			velocity = v.srf_velocity;

			vessel = v;
			geoPos =  VectorUtils.WorldPositionToGeoCoords(v.CoM, v.mainBody);
			acceleration = v.acceleration;
			exists = true;
			timeAcquired = Time.time;
			signalStrength = _signalStrength;

			targetInfo = v.gameObject.GetComponent<TargetInfo> ();

            // vessel never been picked up on radar before: create new targetinfo record
            if (targetInfo == null)
            {
                targetInfo = v.gameObject.AddComponent<TargetInfo>();
            }

			team = BDArmorySettings.BDATeams.None;

			if(targetInfo)
			{
				team = targetInfo.team;
			}
			else
			{
				foreach(MissileFire mf in v.FindPartModulesImplementing<MissileFire>())
				{
					team = BDATargetManager.BoolToTeam(mf.team);
					break;
				}
			}

			vesselJammer = v.gameObject.GetComponent<VesselECMJInfo>();

			pingPosition = Vector2.zero;

			lockedByRadar = null;
		}

		public TargetSignatureData(CMFlare flare, float _signalStrength)
		{
			velocity = flare.velocity;
			geoPos =  VectorUtils.WorldPositionToGeoCoords(flare.transform.position, FlightGlobals.currentMainBody);
			exists = true;
			acceleration = Vector3.zero;
			timeAcquired = Time.time;
			signalStrength = _signalStrength;
			targetInfo = null;
			vesselJammer = null;
			team = BDArmorySettings.BDATeams.None;
			pingPosition = Vector2.zero;
			orbital = false;
			orbit = null;
			lockedByRadar = null;
			vessel = null;
		}

		public TargetSignatureData(Vector3 _velocity, Vector3 _position, Vector3 _acceleration, bool _exists, float _signalStrength)
		{
			velocity = _velocity;
			geoPos =  VectorUtils.WorldPositionToGeoCoords(_position, FlightGlobals.currentMainBody);
			acceleration = _acceleration;
			exists = _exists;
			timeAcquired = Time.time;
			signalStrength = _signalStrength;
			targetInfo = null;
			vesselJammer = null;
			team = BDArmorySettings.BDATeams.None;
			pingPosition = Vector2.zero;
			orbital = false;
			orbit = null;
			lockedByRadar = null;
			vessel = null;
		}

		public Vector3 position
		{
			get
			{
				if(orbital)
				{
					return orbit.pos.xzy;
				}
				else
				{
					//return FlightGlobals.currentMainBody.GetWorldSurfacePosition(geoPos.x, geoPos.y, geoPos.z);
					return VectorUtils.GetWorldSurfacePostion(geoPos, FlightGlobals.currentMainBody);
				}
			}
			set
			{
				geoPos = VectorUtils.WorldPositionToGeoCoords(value, FlightGlobals.currentMainBody);
			}
		}

		public Vector3 predictedPosition
		{
			get
			{
				if(orbital)
				{
					return orbit.getPositionAtUT(Planetarium.GetUniversalTime()).xzy;
				}
				else
				{
					return position + (velocity * age) + (0.5f * acceleration * age * age);
				}
			}
		}

		public float altitude
		{
			get
			{
				return geoPos.z;
			}
		}

		public float age
		{
			get
			{
				return Time.time-timeAcquired;
			}
		}

		public static TargetSignatureData noTarget
		{
			get
			{
				return new TargetSignatureData(Vector3.zero, Vector3.zero, Vector3.zero, false, 0);
			}
		}

		public static void ResetTSDArray(ref TargetSignatureData[] tsdArray)
		{
			for(int i = 0; i < tsdArray.Length; i++)
			{
				tsdArray[i] = noTarget;
			}
		}


	}
}

