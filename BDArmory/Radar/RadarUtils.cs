using BDArmory.Core.Extension;
using BDArmory.CounterMeasure;
using BDArmory.Misc;
using BDArmory.Parts;
using BDArmory.Shaders;
using BDArmory.UI;
using UnityEngine;

namespace BDArmory.Radar
{
	public static class RadarUtils
	{
		public static RenderTexture radarRT;
		public static Texture2D radarTex2D;
		public static Camera radarCam;
		public static Shader radarShader;
		public static int radarResolution = 32;

		public static void SetupRadarCamera()
		{
			if(radarRT && radarTex2D && radarCam && radarShader)
			{
				return;
			}

			//setup shader first
			if(!radarShader)
			{
				radarShader = BDAShaderLoader.UnlitBlackShader;//.LoadManifestShader("BahaTurret.UnlitBlack.shader");
			}

			//then setup textures
			radarRT = new RenderTexture(radarResolution,radarResolution,16);
			radarTex2D = new Texture2D(radarResolution,radarResolution, TextureFormat.ARGB32, false);

			//set up camera
			radarCam = (new GameObject("RadarCamera")).AddComponent<Camera>();
			radarCam.enabled = false;
			radarCam.clearFlags = CameraClearFlags.SolidColor;
			radarCam.backgroundColor = Color.white;
			radarCam.SetReplacementShader(radarShader, string.Empty);
			radarCam.cullingMask = 1<<0;
			radarCam.targetTexture = radarRT;
			//radarCam.nearClipPlane = 75;
			//radarCam.farClipPlane = 40000;
		}

		public static float GetRadarSnapshot(Vessel v, Vector3 origin, float camFoV)
		{
			
			TargetInfo ti = v.GetComponent<TargetInfo>();
			if(ti && ti.isMissile)
			{
				return 600;
			}

			float distance = (v.transform.position - origin).magnitude;

			radarCam.nearClipPlane = Mathf.Clamp(distance - 200, 20, 40000);
			radarCam.farClipPlane = Mathf.Clamp(distance + 200, 20, 40000);

			radarCam.fieldOfView = camFoV;

			radarCam.transform.position = origin;
			radarCam.transform.LookAt(v.CoM+(v.Velocity() * Time.fixedDeltaTime));

			float pixels = 0;
			RenderTexture.active = radarRT;

			radarCam.Render();
			
			radarTex2D.ReadPixels(new Rect(0,0,radarResolution,radarResolution), 0,0);

			for(int x = 0; x < radarResolution; x++)
			{
				for(int y = 0; y < radarResolution; y++)	
				{
					if(radarTex2D.GetPixel(x,y).r<1)
					{
						pixels++;	
					}
				}
			}


			return pixels*4;
		}

		public static void UpdateRadarLock(MissileFire myWpnManager, float directionAngle, Transform referenceTransform, float fov, Vector3 position, float minSignature, ref TargetSignatureData[] dataArray, float dataPersistTime, bool pingRWR, RadarWarningReceiver.RWRThreatTypes rwrType, bool radarSnapshot)
		{
			Vector3d geoPos = VectorUtils.WorldPositionToGeoCoords(position, FlightGlobals.currentMainBody);
			Vector3 forwardVector = referenceTransform.forward;
			Vector3 upVector = referenceTransform.up;//VectorUtils.GetUpDirection(position);
			Vector3 lookDirection = Quaternion.AngleAxis(directionAngle, upVector) * forwardVector;

			int dataIndex = 0;
			foreach(Vessel vessel in BDATargetManager.LoadedVessels)
			{
				if(vessel == null) continue;
				if(!vessel.loaded) continue;

				if(myWpnManager)
				{
					if(vessel == myWpnManager.vessel) continue; //ignore self
				}
				else if((vessel.transform.position - position).sqrMagnitude < 3600) continue;

				Vector3 vesselDirection = Vector3.ProjectOnPlane(vessel.CoM - position, upVector);

				if(Vector3.Angle(vesselDirection, lookDirection) < fov / 2)
				{
					if(TerrainCheck(referenceTransform.position, vessel.transform.position)) continue; //blocked by terrain

					float sig = float.MaxValue;
					if(radarSnapshot && minSignature > 0) sig = GetModifiedSignature(vessel, position);

					RadarWarningReceiver.PingRWR(vessel, position, rwrType, dataPersistTime);

					float detectSig = sig;

					VesselECMJInfo vesselJammer = vessel.GetComponent<VesselECMJInfo>();
					if(vesselJammer)
					{
						sig *= vesselJammer.rcsReductionFactor;
						detectSig += vesselJammer.jammerStrength;
					}

					if(detectSig > minSignature)
					{
						if(vessel.vesselType == VesselType.Debris)
						{
							vessel.gameObject.AddComponent<TargetInfo>();
						}
						else if(myWpnManager != null)
						{
							BDATargetManager.ReportVessel(vessel, myWpnManager);
						}

						while(dataIndex < dataArray.Length - 1)
						{
							if((dataArray[dataIndex].exists && Time.time - dataArray[dataIndex].timeAcquired > dataPersistTime) || !dataArray[dataIndex].exists)
							{
								break;
							}
							dataIndex++;
						}
						if(dataIndex >= dataArray.Length) break;
						dataArray[dataIndex] = new TargetSignatureData(vessel, sig);
						dataIndex++;
						if(dataIndex >= dataArray.Length) break;
					}
				}
			}

		}

		public static void UpdateRadarLock(MissileFire myWpnManager, float directionAngle, Transform referenceTransform, float fov, Vector3 position, float minSignature, ModuleRadar radar, bool pingRWR, RadarWarningReceiver.RWRThreatTypes rwrType, bool radarSnapshot)
		{
			Vector3d geoPos = VectorUtils.WorldPositionToGeoCoords(position, FlightGlobals.currentMainBody);
			Vector3 forwardVector = referenceTransform.forward;
			Vector3 upVector = referenceTransform.up;//VectorUtils.GetUpDirection(position);
			Vector3 lookDirection = Quaternion.AngleAxis(directionAngle, upVector) * forwardVector;

			foreach(Vessel vessel in BDATargetManager.LoadedVessels)
			{
				if(vessel == null) continue;
				if(!vessel.loaded) continue;

				if(myWpnManager)
				{
					if(vessel == myWpnManager.vessel) continue; //ignore self
				}
				else if((vessel.transform.position - position).sqrMagnitude < 3600) continue;

				Vector3 vesselDirection = Vector3.ProjectOnPlane(vessel.CoM - position, upVector);

				if(Vector3.Angle(vesselDirection, lookDirection) < fov / 2)
				{
					if(TerrainCheck(referenceTransform.position, vessel.transform.position)) continue; //blocked by terrain

					float sig = float.MaxValue;
					if(radarSnapshot && minSignature > 0) sig = GetModifiedSignature(vessel, position);

					RadarWarningReceiver.PingRWR(vessel, position, rwrType, radar.signalPersistTime);

					float detectSig = sig;

					VesselECMJInfo vesselJammer = vessel.GetComponent<VesselECMJInfo>();
					if(vesselJammer)
					{
						sig *= vesselJammer.rcsReductionFactor;
						detectSig += vesselJammer.jammerStrength;
					}

					if(detectSig > minSignature)
					{
						if(vessel.vesselType == VesselType.Debris)
						{
							vessel.gameObject.AddComponent<TargetInfo>();
						}
						else if(myWpnManager != null)
						{
							BDATargetManager.ReportVessel(vessel, myWpnManager);
						}

						//radar.vesselRadarData.AddRadarContact(radar, new TargetSignatureData(vessel, detectSig), false);
						radar.ReceiveContactData(new TargetSignatureData(vessel, detectSig), false);
					}
				}
			}

		}

		public static void UpdateRadarLock(Ray ray, float fov, float minSignature, ref TargetSignatureData[] dataArray, float dataPersistTime, bool pingRWR, RadarWarningReceiver.RWRThreatTypes rwrType, bool radarSnapshot)
		{
			int dataIndex = 0;
			foreach(Vessel vessel in BDATargetManager.LoadedVessels)
			{
				if(vessel == null) continue;
				if(!vessel.loaded) continue;
				//if(vessel.Landed) continue;

				Vector3 vectorToTarget = vessel.transform.position - ray.origin;
				if((vectorToTarget).sqrMagnitude < 10) continue; //ignore self

				if(Vector3.Dot(vectorToTarget, ray.direction) < 0) continue; //ignore behind ray

				if(Vector3.Angle(vessel.CoM - ray.origin, ray.direction) < fov / 2)
				{
					if(TerrainCheck(ray.origin, vessel.transform.position)) continue; //blocked by terrain
					float sig = float.MaxValue;
					if(radarSnapshot) sig = GetModifiedSignature(vessel, ray.origin);

					if(pingRWR && sig > minSignature * 0.66f)
					{
						RadarWarningReceiver.PingRWR(vessel, ray.origin, rwrType, dataPersistTime);
					}

					if(sig > minSignature)
					{
						while(dataIndex < dataArray.Length - 1)
						{
							if((dataArray[dataIndex].exists && Time.time - dataArray[dataIndex].timeAcquired > dataPersistTime) || !dataArray[dataIndex].exists)
							{
								break;
							}
							dataIndex++;
						}
						if(dataIndex >= dataArray.Length) break;
						dataArray[dataIndex] = new TargetSignatureData(vessel, sig);
						dataIndex++;
						if(dataIndex >= dataArray.Length) break;
					}
				}

			}
		}

		public static void UpdateRadarLock(Ray ray, Vector3 predictedPos, float fov, float minSignature, ModuleRadar radar, bool pingRWR, bool radarSnapshot, float dataPersistTime, bool locked, int lockIndex, Vessel lockedVessel)
		{
			RadarWarningReceiver.RWRThreatTypes rwrType = radar.rwrType;
			//Vessel lockedVessel = null;
			float closestSqrDist = 100;

			if(lockedVessel == null)
			{
				foreach(Vessel vessel in BDATargetManager.LoadedVessels)
				{
					if(vessel == null) continue;
					if(!vessel.loaded) continue;
					//if(vessel.Landed) continue;

					Vector3 vectorToTarget = vessel.transform.position - ray.origin;
					if((vectorToTarget).sqrMagnitude < 10) continue; //ignore self

					if(Vector3.Dot(vectorToTarget, ray.direction) < 0) continue; //ignore behind ray

					if(Vector3.Angle(vessel.CoM - ray.origin, ray.direction) < fov / 2)
					{
						float sqrDist = Vector3.SqrMagnitude(vessel.CoM - predictedPos);
						if(sqrDist < closestSqrDist)
						{
							closestSqrDist = sqrDist;
							lockedVessel = vessel;
						}
					}
				}
			}

			if(lockedVessel != null)
			{
				if(TerrainCheck(ray.origin, lockedVessel.transform.position))
				{
					radar.UnlockTargetAt(lockIndex, true); //blocked by terrain
					return;
				}

				float sig = float.MaxValue;
				if(radarSnapshot) sig = GetModifiedSignature(lockedVessel, ray.origin);

				if(pingRWR && sig > minSignature * 0.66f)
				{
					RadarWarningReceiver.PingRWR(lockedVessel, ray.origin, rwrType, dataPersistTime);
				}

				if(sig > minSignature)
				{
					//radar.vesselRadarData.AddRadarContact(radar, new TargetSignatureData(lockedVessel, sig), locked);
					radar.ReceiveContactData(new TargetSignatureData(lockedVessel, sig), locked);
				}
				else
				{
					radar.UnlockTargetAt(lockIndex, true);
					return;
				}
			}
			else
			{
				radar.UnlockTargetAt(lockIndex, true);
			}
		}

		/// <summary>
		/// Scans for targets in direction with field of view.
		/// Returns the direction scanned for debug 
		/// </summary>
		/// <returns>The scan direction.</returns>
		/// <param name="myWpnManager">My wpn manager.</param>
		/// <param name="directionAngle">Direction angle.</param>
		/// <param name="referenceTransform">Reference transform.</param>
		/// <param name="fov">Fov.</param>
		/// <param name="results">Results.</param>
		/// <param name="maxDistance">Max distance.</param>
		public static Vector3 GuardScanInDirection(MissileFire myWpnManager, float directionAngle, Transform referenceTransform, float fov, out ViewScanResults results, float maxDistance)
		{
			fov *= 1.1f;
			results = new ViewScanResults();
			results.foundMissile = false;
			results.foundHeatMissile = false;
			results.foundRadarMissile = false;
			results.foundAGM = false;
			results.firingAtMe = false;
			results.missileThreatDistance = float.MaxValue;
            results.threatVessel = null;
            results.threatWeaponManager = null;

			if(!myWpnManager || !referenceTransform)
			{
				return Vector3.zero;
			}

			Vector3 position = referenceTransform.position;
			Vector3d geoPos = VectorUtils.WorldPositionToGeoCoords(position, FlightGlobals.currentMainBody);
			Vector3 forwardVector = referenceTransform.forward;
			Vector3 upVector = referenceTransform.up;
			Vector3 lookDirection = Quaternion.AngleAxis(directionAngle, upVector) * forwardVector;



			foreach(Vessel vessel in BDATargetManager.LoadedVessels)
			{
				if(vessel == null) continue;

				if(vessel.loaded)
				{
					if(vessel == myWpnManager.vessel) continue; //ignore self

					Vector3 vesselProjectedDirection = Vector3.ProjectOnPlane(vessel.transform.position-position, upVector);
					Vector3 vesselDirection = vessel.transform.position - position;


					if(Vector3.Dot(vesselDirection, lookDirection) < 0) continue;

					float vesselDistance = (vessel.transform.position - position).magnitude;

					if(vesselDistance < maxDistance && Vector3.Angle(vesselProjectedDirection, lookDirection) < fov / 2 && Vector3.Angle(vessel.transform.position-position, -myWpnManager.transform.forward) < myWpnManager.guardAngle/2)
					{
						//Debug.Log("Found vessel: " + vessel.vesselName);
						if(TerrainCheck(referenceTransform.position, vessel.transform.position)) continue; //blocked by terrain

						BDATargetManager.ReportVessel(vessel, myWpnManager);

						TargetInfo tInfo;
						if((tInfo = vessel.GetComponent<TargetInfo>()))
						{
							if(tInfo.isMissile)
							{
								MissileBase missileBase;
								if(missileBase = tInfo.MissileBaseModule)
								{
									results.foundMissile = true;
									results.threatVessel = missileBase.vessel;
									Vector3 vectorFromMissile = myWpnManager.vessel.CoM - missileBase.part.transform.position;
									Vector3 relV = missileBase.vessel.Velocity() - myWpnManager.vessel.Velocity();
									bool approaching = Vector3.Dot(relV, vectorFromMissile) > 0;
									if(missileBase.HasFired && missileBase.TimeIndex > 1 && approaching && (missileBase.TargetPosition - (myWpnManager.vessel.CoM + (myWpnManager.vessel.Velocity() * Time.fixedDeltaTime))).sqrMagnitude < 3600)
									{
										if(missileBase.TargetingMode == MissileBase.TargetingModes.Heat)
										{
											results.foundHeatMissile = true;
											results.missileThreatDistance = Mathf.Min(results.missileThreatDistance, Vector3.Distance(missileBase.part.transform.position, myWpnManager.part.transform.position));
											results.threatPosition = missileBase.transform.position;
											break;
										}
										else if(missileBase.TargetingMode == MissileBase.TargetingModes.Radar)
										{
											results.foundRadarMissile = true;
											results.missileThreatDistance = Mathf.Min(results.missileThreatDistance, Vector3.Distance(missileBase.part.transform.position, myWpnManager.part.transform.position));
											results.threatPosition = missileBase.transform.position;
										}
										else if(missileBase.TargetingMode == MissileBase.TargetingModes.Laser)
										{
											results.foundAGM = true;
											results.missileThreatDistance = Mathf.Min(results.missileThreatDistance, Vector3.Distance(missileBase.part.transform.position, myWpnManager.part.transform.position));
											break;
										}
									}
									else
									{
										break;
									}
								}
							}
							else
							{
								//check if its shooting guns at me
								//if(!results.firingAtMe)       //more work, but we can't afford to be incorrect picking the closest threat
								//{
									foreach(ModuleWeapon weapon in vessel.FindPartModulesImplementing<ModuleWeapon>())
									{
										if(!weapon.recentlyFiring) continue;
										if(Vector3.Dot(weapon.fireTransforms[0].forward, vesselDirection) > 0) continue;

										if(Vector3.Angle(weapon.fireTransforms[0].forward, -vesselDirection) < 6500 / vesselDistance && (!results.firingAtMe || (weapon.vessel.ReferenceTransform.position - position).magnitude < (results.threatPosition - position).magnitude))
										{
											results.firingAtMe = true;
											results.threatPosition = weapon.vessel.transform.position;
                                            results.threatVessel = weapon.vessel;
                                            results.threatWeaponManager = weapon.weaponManager;
                                            break;
										}
									}
								//}
							}
						}
					}
				}
			}

			return lookDirection;
		}

		public static float GetModifiedSignature(Vessel vessel, Vector3 origin)
		{
			//float sig = GetBaseRadarSignature(vessel);
			float sig = GetRadarSnapshot(vessel, origin, 0.1f);

			Vector3 upVector = VectorUtils.GetUpDirection(origin);
			
			//sig *= Mathf.Pow(15000,2)/(vessel.transform.position-origin).sqrMagnitude;
			
			if(vessel.Landed)
			{
				sig *= 0.25f;
			}
			if(vessel.Splashed)
			{
				sig *= 0.4f;
			}
			
			//notching and ground clutter
			Vector3 targetDirection = (vessel.transform.position-origin).normalized;
			Vector3 targetClosureV = Vector3.ProjectOnPlane(Vector3.Project(vessel.Velocity(), targetDirection), upVector);
			float notchFactor = 1;
			float angleFromUp = Vector3.Angle(targetDirection,upVector);
			float lookDownAngle = angleFromUp-90;

			if(lookDownAngle > 5)
			{
				notchFactor = Mathf.Clamp(targetClosureV.sqrMagnitude / 3600f, 0.1f, 1f);
			}
			else
			{
				notchFactor = Mathf.Clamp(targetClosureV.sqrMagnitude / 3600f, 0.8f, 3f);
			}

			float groundClutterFactor = Mathf.Clamp((90/angleFromUp), 0.25f, 1.85f);
			sig *= groundClutterFactor;
			sig *= notchFactor;

			VesselChaffInfo vci = vessel.GetComponent<VesselChaffInfo>();
			if(vci) sig *= vci.GetChaffMultiplier();

			return sig;
		}

		public static bool TerrainCheck(Vector3 start, Vector3 end)
		{
		    if (!BDArmorySettings.IGNORE_TERRAIN_CHECK)
		    {
		        return Physics.Linecast(start, end, 1 << 15);
		    }
	
		    return false;
		    
		}

	    public static Vector2 WorldToRadar(Vector3 worldPosition, Transform referenceTransform, Rect radarRect, float maxDistance)
		{
			float scale = maxDistance/(radarRect.height/2);
			Vector3 localPosition = referenceTransform.InverseTransformPoint(worldPosition);
			localPosition.y = 0;
			Vector2 radarPos = new Vector2((radarRect.width/2)+(localPosition.x/scale), (radarRect.height/2)-(localPosition.z/scale));
			return radarPos;
		}
		
		public static Vector2 WorldToRadarRadial(Vector3 worldPosition, Transform referenceTransform, Rect radarRect, float maxDistance, float maxAngle)
		{
			float scale = maxDistance/(radarRect.height);
			Vector3 localPosition = referenceTransform.InverseTransformPoint(worldPosition);
			localPosition.y = 0;
			float angle = Vector3.Angle(localPosition, Vector3.forward);
			if(localPosition.x < 0) angle = -angle;
			float xPos = (radarRect.width/2) + ((angle/maxAngle)*radarRect.width/2);
			float yPos = radarRect.height - (new Vector2 (localPosition.x, localPosition.z)).magnitude / scale;
			Vector2 radarPos = new Vector2(xPos, yPos);
			return radarPos;
		}

        
	}
}

