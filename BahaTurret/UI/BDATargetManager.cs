using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KSP.UI.Screens;


namespace BahaTurret
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	public class BDATargetManager : MonoBehaviour
	{
		public static Dictionary<BDArmorySettings.BDATeams, List<TargetInfo>> TargetDatabase;

		public static Dictionary<BDArmorySettings.BDATeams, List<GPSTargetInfo>> GPSTargets;

		public static List<ModuleTargetingCamera> ActiveLasers;

		public static List<IBDWeapon> FiredMissiles;

		public static List<DestructibleBuilding> LoadedBuildings;

		public static List<Vessel> LoadedVessels;

		public static BDATargetManager Instance;



		string debugString = string.Empty;

		public static float heatScore = 0;
		public static float flareScore = 0;

		public static bool hasAddedButton = false;

		void Awake()
		{
			GameEvents.onGameStateLoad.Add(LoadGPSTargets);
			GameEvents.onGameStateSave.Add(SaveGPSTargets);
			LoadedBuildings = new List<DestructibleBuilding>();
			DestructibleBuilding.OnLoaded.Add(AddBuilding);
			LoadedVessels = new List<Vessel>();
			GameEvents.onVesselLoaded.Add(AddVessel);
			GameEvents.onVesselGoOnRails.Add(RemoveVessel);
			GameEvents.onVesselGoOffRails.Add(AddVessel);
			GameEvents.onVesselCreate.Add(AddVessel);
			GameEvents.onVesselDestroy.Add(CleanVesselList);

			Instance = this;
		}

		void OnDestroy()
		{
			if(GameEvents.onGameStateLoad != null && GameEvents.onGameStateSave != null)
			{
				GameEvents.onGameStateLoad.Remove(LoadGPSTargets);
				GameEvents.onGameStateSave.Remove(SaveGPSTargets);
			}

			GPSTargets = new Dictionary<BDArmorySettings.BDATeams, List<GPSTargetInfo>>();
			GPSTargets.Add(BDArmorySettings.BDATeams.A, new List<GPSTargetInfo>());
			GPSTargets.Add(BDArmorySettings.BDATeams.B, new List<GPSTargetInfo>());


			GameEvents.onVesselLoaded.Remove(AddVessel);
			GameEvents.onVesselGoOnRails.Remove(RemoveVessel);
			GameEvents.onVesselGoOffRails.Remove(AddVessel);
			GameEvents.onVesselCreate.Remove(AddVessel);
			GameEvents.onVesselDestroy.Remove(CleanVesselList);
		}

		void Start()
		{
			//legacy targetDatabase
			TargetDatabase = new Dictionary<BDArmorySettings.BDATeams, List<TargetInfo>>();
			TargetDatabase.Add(BDArmorySettings.BDATeams.A, new List<TargetInfo>());
			TargetDatabase.Add(BDArmorySettings.BDATeams.B, new List<TargetInfo>());
			StartCoroutine(CleanDatabaseRoutine());

			if(GPSTargets == null)
			{
				GPSTargets = new Dictionary<BDArmorySettings.BDATeams, List<GPSTargetInfo>>();
				GPSTargets.Add(BDArmorySettings.BDATeams.A, new List<GPSTargetInfo>());
				GPSTargets.Add(BDArmorySettings.BDATeams.B, new List<GPSTargetInfo>());
			}

			//Laser points
			ActiveLasers = new List<ModuleTargetingCamera>();

			FiredMissiles = new List<IBDWeapon>();

			//AddToolbarButton();
			StartCoroutine(ToolbarButtonRoutine());

		}


		void AddBuilding(DestructibleBuilding b)
		{
			if(!LoadedBuildings.Contains(b))
			{
				LoadedBuildings.Add(b);
			}

			LoadedBuildings.RemoveAll(x => x == null);
		}

		void AddVessel(Vessel v)
		{
			if(!LoadedVessels.Contains(v))
			{
				LoadedVessels.Add(v);
			}
			CleanVesselList(v);
		}

		void RemoveVessel(Vessel v)
		{
			if(v != null)
			{
				LoadedVessels.Remove(v);
			}
			CleanVesselList(v);
		}

		void CleanVesselList(Vessel v)
		{
			LoadedVessels.RemoveAll(ves => ves == null);
			LoadedVessels.RemoveAll(ves => ves.loaded == false);
		}

		void AddToolbarButton()
		{
			if(HighLogic.LoadedSceneIsFlight)
			{
				if(!hasAddedButton)
				{
					Texture buttonTexture = GameDatabase.Instance.GetTexture(BDArmorySettings.textureDir + "icon", false);
					ApplicationLauncher.Instance.AddModApplication(ShowToolbarGUI, HideToolbarGUI, Dummy, Dummy, Dummy, Dummy, ApplicationLauncher.AppScenes.FLIGHT, buttonTexture);
					hasAddedButton = true;

				}
			}
		}

		IEnumerator ToolbarButtonRoutine()
		{
			if(hasAddedButton) yield break;
			if(!HighLogic.LoadedSceneIsFlight) yield break;
			while(!ApplicationLauncher.Ready)
			{
				yield return null;
			}

			AddToolbarButton();
		}
		public void ShowToolbarGUI()
		{
			BDArmorySettings.toolbarGuiEnabled = true;	
		}

		public void HideToolbarGUI()
		{
			BDArmorySettings.toolbarGuiEnabled = false;	
		}
		void Dummy()
		{}

		void Update()
		{
			if(BDArmorySettings.DRAW_DEBUG_LABELS)
			{
				UpdateDebugLabels();
			}

		}


		//Laser point stuff
		public static void RegisterLaserPoint(ModuleTargetingCamera cam)
		{
			if(ActiveLasers.Contains(cam))
			{
				return;
			}
			else
			{
				ActiveLasers.Add(cam);
			}
		}

		///// <summary>
		///// Gets the laser target painter with the least angle off boresight. Set the missileBase as the reference missilePosition.
		///// </summary>
		///// <returns>The laser target painter.</returns>
		///// <param name="referenceTransform">Reference missilePosition.</param>
		///// <param name="maxBoreSight">Max bore sight.</param>
		//public static ModuleTargetingCamera GetLaserTarget(MissileLauncher ml, bool parentOnly)
		//{
  //          return GetModuleTargeting(parentOnly, ml.transform.forward, ml.transform.position, ml.maxOffBoresight, ml.vessel, ml.SourceVessel);
  //      }

  //      public static ModuleTargetingCamera GetLaserTarget(BDModularGuidance ml, bool parentOnly)
  //      {
  //          float maxOffBoresight = 45;
           
  //          return GetModuleTargeting(parentOnly, ml.MissileReferenceTransform.forward, ml.MissileReferenceTransform.position, maxOffBoresight,ml.vessel,ml.SourceVessel);
  //      }

        /// <summary>
		/// Gets the laser target painter with the least angle off boresight. Set the missileBase as the reference missilePosition.
		/// </summary>
		/// <returns>The laser target painter.</returns>
	    public static ModuleTargetingCamera GetLaserTarget(MissileBase ml, bool parentOnly)
	    {
            return GetModuleTargeting(parentOnly, ml.GetForwardTransform(), ml.MissileReferenceTransform.position, ml.maxOffBoresight, ml.vessel, ml.SourceVessel);
        }


        private static ModuleTargetingCamera GetModuleTargeting(bool parentOnly, Vector3 missilePosition, Vector3 position, float maxOffBoresight,Vessel vessel, Vessel sourceVessel)
	    {
            ModuleTargetingCamera finalCam = null;
            float smallestAngle = 360;
            foreach (var cam in ActiveLasers)
            {
                if (!cam)
                {
                    continue;
                }

                if (parentOnly && !(cam.vessel == vessel || cam.vessel == sourceVessel))
                {
                    continue;
                }


                if (cam.cameraEnabled && cam.groundStabilized && cam.surfaceDetected && !cam.gimbalLimitReached)
                {
                    float angle = Vector3.Angle(missilePosition, cam.groundTargetPosition - position);
                    if (angle < maxOffBoresight && angle < smallestAngle && CanSeePosition(cam.groundTargetPosition, vessel.transform.position, missilePosition))
                    {
                        smallestAngle = angle;
                        finalCam = cam;
                    }
                }
            }
            return finalCam;
        }

        public static bool CanSeePosition(Vector3 groundTargetPosition, Vector3 vesselPosition, Vector3 missilePosition)
        {
            if ((groundTargetPosition - vesselPosition).sqrMagnitude < Mathf.Pow(20, 2))
            {
                return false;
            }

            float dist = 10000;
            Ray ray = new Ray(missilePosition, groundTargetPosition - missilePosition);
            ray.origin += 10 * ray.direction;
            RaycastHit rayHit;
            if (Physics.Raycast(ray, out rayHit, dist, 557057))
            {
                if ((rayHit.point - groundTargetPosition).sqrMagnitude < 200)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        //public static TargetSignatureData GetHeatTarget(Ray ray, float scanRadius, float highpassThreshold, bool allAspect, MissileFire mf = null)
        public static TargetSignatureData GetHeatTarget(Ray ray, float scanRadius, float highpassThreshold, bool allAspect, MissileFire mf = null, bool favorGroundTargets = false)
        {
			float minScore = highpassThreshold;
            float minMass = 0.15f;  //otherwise the RAMs have trouble shooting down incoming missiles
            TargetSignatureData finalData = TargetSignatureData.noTarget;
			float finalScore = 0;
			foreach(var vessel in BDATargetManager.LoadedVessels)
			{
				if(!vessel || !vessel.loaded)
				{
					continue;
				}

                if (favorGroundTargets && !vessel.LandedOrSplashed) // for AGM heat guidance
                    continue;

                TargetInfo tInfo = vessel.gameObject.GetComponent<TargetInfo>();
				if(mf == null || 
					!tInfo || 
					!(mf && tInfo.isMissile && tInfo.team != BDATargetManager.BoolToTeam(mf.team) && (tInfo.MissileBaseModule.MissileState == MissileBase.MissileStates.Boost || tInfo.MissileBaseModule.MissileState == MissileBase.MissileStates.Cruise)))
				{
					if(vessel.GetTotalMass() < minMass)
					{
						continue;
					}
				}

				if(RadarUtils.TerrainCheck(ray.origin, vessel.transform.position))
				{
					continue;
				}

				float angle = Vector3.Angle(vessel.CoM-ray.origin, ray.direction);
				if(angle < scanRadius)
				{
					float score = 0;
					foreach(var part in vessel.Parts)
					{
						if(!part) continue;
						if(!allAspect)
						{
							if(!Misc.CheckSightLineExactDistance(ray.origin, part.transform.position+vessel.srf_velocity, Vector3.Distance(part.transform.position,ray.origin), 5, 5)) continue;
						}

						float thisScore = (float)(part.thermalInternalFluxPrevious+part.skinTemperature) * (15/Mathf.Max(15,angle));
						thisScore *= Mathf.Pow(1400,2)/Mathf.Clamp((vessel.CoM-ray.origin).sqrMagnitude, 90000, 36000000);
						score = Mathf.Max (score, thisScore);
					}

					if(vessel.LandedOrSplashed && !favorGroundTargets)
					{
						score /= 4;
					}

					score *= Mathf.Clamp(Vector3.Angle(vessel.transform.position-ray.origin, -VectorUtils.GetUpDirection(ray.origin))/90, 0.5f, 1.5f);

					if(score > finalScore)
					{
						finalScore = score;
						finalData = new TargetSignatureData(vessel, score);
					}
				}
			}

			heatScore = finalScore;//DEBUG
			flareScore = 0; //DEBUG
			foreach(var flare in BDArmorySettings.Flares)
			{
				if(!flare) continue;

				float angle = Vector3.Angle(flare.transform.position-ray.origin, ray.direction);
				if(angle < scanRadius)
				{
					float score = flare.thermal * Mathf.Clamp01(15/angle);
					score *= Mathf.Pow(1400,2)/Mathf.Clamp((flare.transform.position-ray.origin).sqrMagnitude, 90000, 36000000);

					score *= Mathf.Clamp(Vector3.Angle(flare.transform.position-ray.origin, -VectorUtils.GetUpDirection(ray.origin))/90, 0.5f, 1.5f);

					if(score > finalScore)
					{
						flareScore = score;//DEBUG
						finalScore = score;
						finalData = new TargetSignatureData(flare, score);
					}
				}
			}



			if(finalScore < minScore)
			{
				finalData = TargetSignatureData.noTarget;
			}

			return finalData;
		}


		void UpdateDebugLabels()
		{
			debugString = string.Empty;
			debugString+= ("Team A's targets:");
			foreach(var targetInfo in TargetDatabase[BDArmorySettings.BDATeams.A])
			{
				if(targetInfo)
				{
					if(!targetInfo.Vessel)
					{
						debugString+= ("\n - A target with no vessel reference.");
					}
					else
					{
						debugString+= ("\n - "+targetInfo.Vessel.vesselName+", Engaged by "+targetInfo.numFriendliesEngaging);
					}
				}
				else
				{
					debugString+= ("\n - A null target info.");
				}
			}
			debugString+= ("\nTeam B's targets:");
			foreach(var targetInfo in TargetDatabase[BDArmorySettings.BDATeams.B])
			{
				if(targetInfo)
				{
					if(!targetInfo.Vessel)
					{
						debugString+= ("\n - A target with no vessel reference.");
					}
					else
					{
						debugString+= ("\n - "+targetInfo.Vessel.vesselName+", Engaged by "+targetInfo.numFriendliesEngaging);
					}
				}
				else
				{
					debugString+= ("\n - A null target info.");
				}
			}

			debugString += "\n\nHeat score: "+heatScore;
			debugString += "\nFlare score: "+flareScore;

			/*
			debugString += "\n\nLoaded vessels: ";
			foreach(var v in LoadedVessels)
			{
				if(v)
				{
					debugString += "\n" + v.vesselName;
				}
			}
			*/
		}




		//gps stuff
		void SaveGPSTargets(ConfigNode saveNode)
		{
			string saveTitle = HighLogic.CurrentGame.Title;
			Debug.Log("[BDArmory]: Save title: " + saveTitle);
			ConfigNode fileNode = ConfigNode.Load("GameData/BDArmory/gpsTargets.cfg");
			if(fileNode == null)
			{
				fileNode = new ConfigNode();
				fileNode.AddNode("BDARMORY");
				fileNode.Save("GameData/BDArmory/gpsTargets.cfg");

			}
		
			if(fileNode!=null && fileNode.HasNode("BDARMORY"))
			{
				ConfigNode node = fileNode.GetNode("BDARMORY");

				if(GPSTargets == null || !FlightGlobals.ready)
				{
					return;
				}

				ConfigNode gpsNode = null;
				if(node.HasNode("BDAGPSTargets"))
				{
					foreach(var n in node.GetNodes("BDAGPSTargets"))
					{
						if(n.GetValue("SaveGame") == saveTitle)
						{
							gpsNode = n;
							break;
						}
					}

					if(gpsNode == null)
					{
						gpsNode = node.AddNode("BDAGPSTargets");
						gpsNode.AddValue("SaveGame", saveTitle);
					}
				}
				else
				{
					gpsNode = node.AddNode("BDAGPSTargets");
					gpsNode.AddValue("SaveGame", saveTitle);
				}

				if(GPSTargets[BDArmorySettings.BDATeams.A].Count == 0 && GPSTargets[BDArmorySettings.BDATeams.B].Count == 0)
				{
					//gpsNode.SetValue("Targets", string.Empty, true);
					return;
				}

				string targetString = GPSListToString();
				gpsNode.SetValue("Targets", targetString, true);
				fileNode.Save("GameData/BDArmory/gpsTargets.cfg");
				Debug.Log("[BDArmory]: ==== Saved BDA GPS Targets ====");
			}
		}

	

		void LoadGPSTargets(ConfigNode saveNode)
		{
			ConfigNode fileNode = ConfigNode.Load("GameData/BDArmory/gpsTargets.cfg");
			string saveTitle = HighLogic.CurrentGame.Title;

			if(fileNode != null && fileNode.HasNode("BDARMORY"))
			{
				ConfigNode node = fileNode.GetNode("BDARMORY");

				foreach(var gpsNode in node.GetNodes("BDAGPSTargets"))
				{
					if(gpsNode.HasValue("SaveGame") && gpsNode.GetValue("SaveGame") == saveTitle)
					{
						if(gpsNode.HasValue("Targets"))
						{
							string targetString = gpsNode.GetValue("Targets");
							if(targetString == string.Empty)
							{
								Debug.Log("[BDArmory]: ==== BDA GPS Target string was empty! ====");
								return;
							}
							else
							{
								StringToGPSList(targetString);
								Debug.Log("[BDArmory]: ==== Loaded BDA GPS Targets ====");
							}
						}
						else
						{
							Debug.Log("[BDArmory]: ==== No BDA GPS Targets value found! ====");
						}
					}
				}
			}
		}

		//format: SAVENAME&name,lat,long,alt;name,lat,long,alt:name,lat,long,alt  (A;A;A:B;B)
		private string GPSListToString()
		{
			string finalString = string.Empty;
			string aString = string.Empty;
			foreach(var gpsInfo in GPSTargets[BDArmorySettings.BDATeams.A])
			{
				aString += gpsInfo.name;
				aString += ",";
				aString += gpsInfo.gpsCoordinates.x;
				aString += ",";
				aString += gpsInfo.gpsCoordinates.y;
				aString += ",";
				aString += gpsInfo.gpsCoordinates.z;
				aString += ";";
			}
			if(aString == string.Empty)
			{
				aString = "null";
			}
			finalString += aString;
			finalString += ":";

			string bString = string.Empty;
			foreach(var gpsInfo in GPSTargets[BDArmorySettings.BDATeams.B])
			{
				bString += gpsInfo.name;
				bString += ",";
				bString += gpsInfo.gpsCoordinates.x;
				bString += ",";
				bString += gpsInfo.gpsCoordinates.y;
				bString += ",";
				bString += gpsInfo.gpsCoordinates.z;
				bString += ";";
			}
			if(bString == string.Empty)
			{
				bString = "null";
			}
			finalString += bString;

			return finalString;
		}

		private void StringToGPSList(string listString)
		{
			if(GPSTargets == null)
			{
				GPSTargets = new Dictionary<BDArmorySettings.BDATeams, List<GPSTargetInfo>>();
			}
			GPSTargets.Clear();
			GPSTargets.Add(BDArmorySettings.BDATeams.A, new List<GPSTargetInfo>());
			GPSTargets.Add(BDArmorySettings.BDATeams.B, new List<GPSTargetInfo>());

			if(listString == null || listString == string.Empty)
			{
				Debug.Log("[BDArmory]: === GPS List string was empty or null ===");
				return;
			}

			string[] teams = listString.Split(new char[]{ ':' });

			Debug.Log("[BDArmory]: ==== Loading GPS Targets. Number of teams: " + teams.Length);

			if(teams[0] != null && teams[0].Length > 0 && teams[0] != "null")
			{
				string[] teamACoords = teams[0].Split(new char[]{ ';' });
				for(int i = 0; i < teamACoords.Length; i++)
				{
					if(teamACoords[i] != null && teamACoords[i].Length > 0)
					{
						string[] data = teamACoords[i].Split(new char[]{ ',' });
						string name = data[0];
						double lat = double.Parse(data[1]);
						double longi = double.Parse(data[2]);
						double alt = double.Parse(data[3]);
						GPSTargetInfo newInfo = new GPSTargetInfo(new Vector3d(lat, longi, alt), name);
						GPSTargets[BDArmorySettings.BDATeams.A].Add(newInfo);
					}
				}
			}

			if(teams[1] != null && teams[1].Length > 0 && teams[1] != "null")
			{
				string[] teamBCoords = teams[1].Split(new char[]{ ';' });
				for(int i = 0; i < teamBCoords.Length; i++)
				{
					if(teamBCoords[i] != null && teamBCoords[i].Length > 0)
					{
						string[] data = teamBCoords[i].Split(new char[]{ ',' });
						string name = data[0];
						double lat = double.Parse(data[1]);
						double longi = double.Parse(data[2]);
						double alt = double.Parse(data[3]);
						GPSTargetInfo newInfo = new GPSTargetInfo(new Vector3d(lat, longi, alt), name);
						GPSTargets[BDArmorySettings.BDATeams.B].Add(newInfo);
					}
				}
			}
		}








		//Legacy target managing stuff

		public static BDArmorySettings.BDATeams BoolToTeam(bool team)
		{
			return team ? BDArmorySettings.BDATeams.B : BDArmorySettings.BDATeams.A;
		}

		public static BDArmorySettings.BDATeams OtherTeam(BDArmorySettings.BDATeams team)
		{
			return team == BDArmorySettings.BDATeams.A ? BDArmorySettings.BDATeams.B : BDArmorySettings.BDATeams.A;
		}

		IEnumerator CleanDatabaseRoutine()
		{
			while(enabled)
			{
				yield return new WaitForSeconds(5);
			
				TargetDatabase[BDArmorySettings.BDATeams.A].RemoveAll(target => target == null);
				TargetDatabase[BDArmorySettings.BDATeams.A].RemoveAll(target => target.team == BDArmorySettings.BDATeams.A);
				TargetDatabase[BDArmorySettings.BDATeams.A].RemoveAll(target => !target.isThreat);

				TargetDatabase[BDArmorySettings.BDATeams.B].RemoveAll(target => target == null);
				TargetDatabase[BDArmorySettings.BDATeams.B].RemoveAll(target => target.team == BDArmorySettings.BDATeams.B);
				TargetDatabase[BDArmorySettings.BDATeams.B].RemoveAll(target => !target.isThreat);
			}
		}

		void RemoveTarget(TargetInfo target, BDArmorySettings.BDATeams team)
		{
			TargetDatabase[team].Remove(target);
		}

		public static void ReportVessel(Vessel v, MissileFire reporter)
		{
			if(!v) return;
			if(!reporter) return;

			TargetInfo info = v.gameObject.GetComponent<TargetInfo>();
			if(!info)
			{
				foreach(var mf in v.FindPartModulesImplementing<MissileFire>())
				{
					if(mf.team != reporter.team)
					{
						info = v.gameObject.AddComponent<TargetInfo>();
					}
					return;
				}

				foreach(var ml in v.FindPartModulesImplementing<MissileBase>())
				{
					if(ml.HasFired)
					{
						if(ml.Team != reporter.team)
						{
							info = v.gameObject.AddComponent<TargetInfo>();
						}
					}

					return;
				}
			}
			else
			{
				info.detectedTime = Time.time;
			}
		}

		public static void ClearDatabase()
		{
			foreach(var t in TargetDatabase.Keys)
			{
				foreach(var target in TargetDatabase[t])
				{
					target.detectedTime = 0;
				}
			}

			TargetDatabase[BDArmorySettings.BDATeams.A].Clear();
			TargetDatabase[BDArmorySettings.BDATeams.B].Clear();
		}

		public static TargetInfo GetAirToAirTarget(MissileFire mf)
		{
			BDArmorySettings.BDATeams team = mf.team ? BDArmorySettings.BDATeams.B : BDArmorySettings.BDATeams.A;
			TargetInfo finalTarget = null;

            float finalTargetSuitability = 0;        //this will determine how suitable the target is, based on where it is located relative to the targeting vessel and how far it is

			foreach(var target in TargetDatabase[team])
			{
				if(target.numFriendliesEngaging >= 2) continue;
				if(target && target.Vessel && target.isFlying && !target.isMissile)
				{
                    Vector3 targetRelPos = target.Vessel.vesselTransform.position - mf.vessel.vesselTransform.position;
                    float targetSuitability = Vector3.Dot(targetRelPos.normalized, mf.vessel.ReferenceTransform.up);       //prefer targets ahead to those behind
                    targetSuitability += 500 / (targetRelPos.magnitude + 100);

                    if (finalTarget == null || (target.numFriendliesEngaging < finalTarget.numFriendliesEngaging) || targetSuitability > finalTargetSuitability + finalTarget.numFriendliesEngaging)
					{
						finalTarget = target;
                        finalTargetSuitability = targetSuitability;
					}
				}
			}

			return finalTarget;
		}

        //this will search for an AA target that is immediately in front of the AI during an extend when it would otherwise be helpless
        public static TargetInfo GetAirToAirTargetAbortExtend(MissileFire mf, float maxDistance, float cosAngleCheck)
        {
            BDArmorySettings.BDATeams team = mf.team ? BDArmorySettings.BDATeams.B : BDArmorySettings.BDATeams.A;
            TargetInfo finalTarget = null;

            float finalTargetSuitability = 0;    //this will determine how suitable the target is, based on where it is located relative to the targeting vessel and how far it is

            foreach (var target in TargetDatabase[team])
            {
                if (target && target.Vessel && target.isFlying && !target.isMissile)
                {
                    Vector3 targetRelPos = target.Vessel.vesselTransform.position - mf.vessel.vesselTransform.position;

                    float distance, dot;
                    distance = targetRelPos.magnitude;
                    dot = Vector3.Dot(targetRelPos.normalized, mf.vessel.ReferenceTransform.up);

                    if (distance > maxDistance || cosAngleCheck > dot)
                        continue;

                    float targetSuitability = dot;       //prefer targets ahead to those behind
                    targetSuitability += 500 / (distance + 100);        //same suitability check as above

                    if (finalTarget == null || targetSuitability > finalTargetSuitability)      //just pick the most suitable one
                    {
                        finalTarget = target;
                        finalTargetSuitability = targetSuitability;
                    }
                }
            }

            return finalTarget;
        }
        
        //returns the nearest friendly target
        public static TargetInfo GetClosestFriendly(MissileFire mf)
        {
            BDArmorySettings.BDATeams team = mf.team ? BDArmorySettings.BDATeams.A : BDArmorySettings.BDATeams.B;
            TargetInfo finalTarget = null;

            foreach (var target in TargetDatabase[team])
            {
                if (target && target.Vessel && target.weaponManager != mf)
                {
                    if (finalTarget == null || (target.IsCloser(finalTarget, mf)))
                    {
                        finalTarget = target;
                    }
                }
            }

            return finalTarget;
        }

        //returns the target that owns this weapon manager
        public static TargetInfo GetTargetFromWeaponManager(MissileFire mf)
        {
            BDArmorySettings.BDATeams team = mf.team ? BDArmorySettings.BDATeams.A : BDArmorySettings.BDATeams.B;

            foreach (var target in TargetDatabase[team])
            {
                if (target && target.Vessel && target.weaponManager == mf)
                {
                    return target;
                }
            }

            return null;
        }

        public static TargetInfo GetClosestTarget(MissileFire mf)
		{
			BDArmorySettings.BDATeams team = mf.team ? BDArmorySettings.BDATeams.B : BDArmorySettings.BDATeams.A;
			TargetInfo finalTarget = null;

			foreach(var target in TargetDatabase[team])
			{
				if(target && target.Vessel && mf.CanSeeTarget(target.Vessel) && !target.isMissile)
				{
					if(finalTarget == null || (target.IsCloser(finalTarget, mf)))
					{
						finalTarget = target;
					}
				}
			}

			return finalTarget;
		}

		public static List<TargetInfo> GetAllTargetsExcluding(List<TargetInfo> excluding, MissileFire mf)
		{
			List<TargetInfo> finalTargets = new List<TargetInfo>();
			BDArmorySettings.BDATeams team = BoolToTeam(mf.team);

			foreach(var target in TargetDatabase[team])
			{
				if(target && target.Vessel && mf.CanSeeTarget(target.Vessel) && !excluding.Contains(target))
				{
					finalTargets.Add(target);
				}
			}

			return finalTargets;
		}

		public static TargetInfo GetLeastEngagedTarget(MissileFire mf)
		{
			BDArmorySettings.BDATeams team = mf.team ? BDArmorySettings.BDATeams.B : BDArmorySettings.BDATeams.A;
			TargetInfo finalTarget = null;
			
			foreach(var target in TargetDatabase[team])
			{
				if(target && target.Vessel && mf.CanSeeTarget(target.Vessel) && !target.isMissile)
				{
					if(finalTarget == null || target.numFriendliesEngaging < finalTarget.numFriendliesEngaging)
					{
						finalTarget = target;
					}
				}
			}
		
			return finalTarget;
		}

		public static TargetInfo GetMissileTarget(MissileFire mf, bool targetingMeOnly = false)
		{
			BDArmorySettings.BDATeams team = mf.team ? BDArmorySettings.BDATeams.B : BDArmorySettings.BDATeams.A;
			TargetInfo finalTarget = null;

			foreach(var target in TargetDatabase[team])
			{
				if(target && target.Vessel && target.isMissile && target.isThreat && mf.CanSeeTarget(target.Vessel) )
				{
					if(target.MissileBaseModule)
					{
						if(targetingMeOnly)
						{
							if(Vector3.SqrMagnitude(target.MissileBaseModule.TargetPosition - mf.vessel.CoM) > 60 * 60)
							{
								continue;
							}
						}
					}
					else
					{
						Debug.LogWarning("checking target missile -  doesn't have missile module");
					}


					if(((finalTarget == null && target.numFriendliesEngaging < 2) || (finalTarget != null && target.numFriendliesEngaging < finalTarget.numFriendliesEngaging)))
					{
						finalTarget = target;
					}
				}
			}
			
			return finalTarget;
		}

		public static TargetInfo GetUnengagedMissileTarget(MissileFire mf)
		{
			BDArmorySettings.BDATeams team = mf.team ? BDArmorySettings.BDATeams.B : BDArmorySettings.BDATeams.A;

			foreach(var target in TargetDatabase[team])
			{
				if(target && target.Vessel && mf.CanSeeTarget(target.Vessel) && target.isMissile && target.isThreat)
				{
					if(target.numFriendliesEngaging == 0)
					{
						return target;
					}
				}
			}
			
			return null;
		}

		public static TargetInfo GetClosestMissileTarget(MissileFire mf)
		{
			BDArmorySettings.BDATeams team = BoolToTeam(mf.team);
			TargetInfo finalTarget = null;
			
			foreach(var target in TargetDatabase[team])
			{
				if(target && target.Vessel && mf.CanSeeTarget(target.Vessel) && target.isMissile)
				{
					bool isHostile = false;
					if(target.isThreat)
					{
						isHostile = true;
					}

					if(isHostile && (finalTarget == null || target.IsCloser(finalTarget, mf)))
					{
						finalTarget = target;
					}
				}
			}
			
			return finalTarget;
		}

        //checks to see if a friendly is too close to the gun trajectory to fire them
        public static bool CheckSafeToFireGuns(MissileFire weaponManager, Vector3 aimDirection, float safeDistance, float cosUnsafeAngle)
        {
            BDArmorySettings.BDATeams team = weaponManager.team ? BDArmorySettings.BDATeams.A : BDArmorySettings.BDATeams.B;
            foreach (var friendlyTarget in TargetDatabase[team])
            {
                if (friendlyTarget && friendlyTarget.Vessel)
                {
                    float friendlyPosDot = Vector3.Dot(friendlyTarget.position - weaponManager.vessel.vesselTransform.position, aimDirection);
                    if (friendlyPosDot > 0)  //only bother if the friendly is actually in front of us
                    {
                        float friendlyDistance = (friendlyTarget.position - weaponManager.vessel.vesselTransform.position).magnitude;
                        float friendlyPosDotNorm = friendlyPosDot / friendlyDistance;       //scale down the dot to be a 0-1 so we can check it againts cosUnsafeAngle

                        if (friendlyDistance < safeDistance && cosUnsafeAngle < friendlyPosDotNorm)           //if it's too close and it's within the Unsafe Angle, don't fire
                            return false;
                    }
                }
            }

            return true;
        }


		void OnGUI()
		{
			if(BDArmorySettings.DRAW_DEBUG_LABELS)	
			{
				GUI.Label(new Rect(600,100,600,600), debugString);	
			}


		}



	}
}

