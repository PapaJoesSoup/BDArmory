using System.Collections;
using System.Collections.Generic;
using BDArmory.Misc;
using BDArmory.UI;
using UniLinq;
using UnityEngine;

namespace BDArmory.Control
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class BDACompetitionMode : MonoBehaviour
    {
        public static BDACompetitionMode Instance;

        void Awake()
        {
            if (Instance)
            {
                Destroy(Instance);
            }

            Instance = this;
        }

        void OnGUI()
        {
            if (competitionStarting)
            {
                GUIStyle cStyle = new GUIStyle(HighLogic.Skin.label);
                cStyle.fontStyle = FontStyle.Bold;
                cStyle.fontSize = 22;
                cStyle.alignment = TextAnchor.UpperCenter;
                Rect cLabelRect = new Rect(0, Screen.height/6, Screen.width, 100);


                GUIStyle cShadowStyle = new GUIStyle(cStyle);
                Rect cShadowRect = new Rect(cLabelRect);
                cShadowRect.x += 2;
                cShadowRect.y += 2;
                cShadowStyle.normal.textColor = new Color(0, 0, 0, 0.75f);

                GUI.Label(cShadowRect, competitionStatus, cShadowStyle);
                GUI.Label(cLabelRect, competitionStatus, cStyle);
            }
        }

        //Competition mode
        public bool competitionStarting;
        string competitionStatus = "";
        Coroutine competitionRoutine;

        public void StartCompetitionMode(float distance)
        {
            if (!competitionStarting)
            {
                competitionRoutine = StartCoroutine(DogfightCompetitionModeRoutine(distance/2));
            }
        }

        public void StopCompetition()
        {
            if (competitionRoutine != null)
            {
                StopCoroutine(competitionRoutine);
            }

            competitionStarting = false;
        }

        IEnumerator DogfightCompetitionModeRoutine(float distance)
        {
            competitionStarting = true;
            competitionStatus = "Competition: Pilots are taking off.";
            Dictionary<BDArmorySettings.BDATeams, List<BDModulePilotAI>> pilots =
                new Dictionary<BDArmorySettings.BDATeams, List<BDModulePilotAI>>();
            pilots.Add(BDArmorySettings.BDATeams.A, new List<BDModulePilotAI>());
            pilots.Add(BDArmorySettings.BDATeams.B, new List<BDModulePilotAI>());
            List<Vessel>.Enumerator loadedVessels = BDATargetManager.LoadedVessels.GetEnumerator();
            while (loadedVessels.MoveNext())
            {
                if (loadedVessels.Current == null) continue;
                if (!loadedVessels.Current.loaded) continue;
                BDModulePilotAI pilot = null;
                IEnumerator<BDModulePilotAI> ePilots = loadedVessels.Current.FindPartModulesImplementing<BDModulePilotAI>().AsEnumerable().GetEnumerator();
                while (ePilots.MoveNext())
                {
                    pilot = ePilots.Current;
                    break;
                }
                ePilots.Dispose();
                if (!pilot || !pilot.weaponManager) continue;

                pilots[BDATargetManager.BoolToTeam(pilot.weaponManager.team)].Add(pilot);
                pilot.ActivatePilot();
                pilot.standbyMode = false;
                if (pilot.weaponManager.guardMode)
                {
                    pilot.weaponManager.ToggleGuardMode();
                }
            }
            loadedVessels.Dispose();
            
            //clear target database so pilots don't attack yet
            BDATargetManager.ClearDatabase();

            if (pilots[BDArmorySettings.BDATeams.A].Count == 0 || pilots[BDArmorySettings.BDATeams.B].Count == 0)
            {
                Debug.Log("[BDArmory]: Unable to start competition mode - one or more teams is empty");
                competitionStatus = "Competition: Failed!  One or more teams is empty.";
                yield return new WaitForSeconds(2);
                competitionStarting = false;
                yield break;
            }

            BDModulePilotAI aLeader = pilots[BDArmorySettings.BDATeams.A][0];
            BDModulePilotAI bLeader = pilots[BDArmorySettings.BDATeams.B][0];

            aLeader.weaponManager.wingCommander.CommandAllFollow();
            bLeader.weaponManager.wingCommander.CommandAllFollow();


            //wait till the leaders are airborne
            while (aLeader && bLeader && (aLeader.vessel.LandedOrSplashed || bLeader.vessel.LandedOrSplashed))
            {
                yield return null;
            }

            if (!aLeader || !bLeader)
            {
                StopCompetition();
            }

            competitionStatus = "Competition: Sending pilots to start position.";
            Vector3 aDirection =
                Vector3.ProjectOnPlane(aLeader.vessel.CoM - bLeader.vessel.CoM, aLeader.vessel.upAxis).normalized;
            Vector3 bDirection =
                Vector3.ProjectOnPlane(bLeader.vessel.CoM - aLeader.vessel.CoM, bLeader.vessel.upAxis).normalized;

            Vector3 center = (aLeader.vessel.CoM + bLeader.vessel.CoM)/2f;
            Vector3 aDestination = center + (aDirection*(distance + 1250f));
            Vector3 bDestination = center + (bDirection*(distance + 1250f));
            aDestination = VectorUtils.WorldPositionToGeoCoords(aDestination, FlightGlobals.currentMainBody);
            bDestination = VectorUtils.WorldPositionToGeoCoords(bDestination, FlightGlobals.currentMainBody);

            aLeader.CommandFlyTo(aDestination);
            bLeader.CommandFlyTo(bDestination);

            Vector3 centerGPS = VectorUtils.WorldPositionToGeoCoords(center, FlightGlobals.currentMainBody);

            //wait till everyone is in position
            bool waiting = true;
            while (waiting)
            {
                waiting = false;

                if (!aLeader || !bLeader)
                {
                    StopCompetition();
                }

                if (Vector3.Distance(aLeader.transform.position, bLeader.transform.position) < distance*1.95f)
                {
                    waiting = true;
                }
                else
                {
                    Dictionary<BDArmorySettings.BDATeams, List<BDModulePilotAI>>.KeyCollection.Enumerator keys = pilots.Keys.GetEnumerator();
                    while (keys.MoveNext())
                    {
                        List<BDModulePilotAI>.Enumerator ePilots = pilots[keys.Current].GetEnumerator();
                        while (ePilots.MoveNext())
                        {
                            if (ePilots.Current == null) continue;
                            if (ePilots.Current.currentCommand != BDModulePilotAI.PilotCommands.Follow ||
                                !((ePilots.Current.vessel.CoM - ePilots.Current.commandLeader.vessel.CoM).sqrMagnitude > 1000f*1000f)) continue;
                            competitionStatus = "Competition: Waiting for teams to get in position.";
                            waiting = true;
                        }
                        ePilots.Dispose();
                    }
                    keys.Dispose();
                }

                yield return null;
            }

            //start the match
            Dictionary<BDArmorySettings.BDATeams, List<BDModulePilotAI>>.KeyCollection.Enumerator pKeys = pilots.Keys.GetEnumerator();
            while (pKeys.MoveNext())
            {
                List<BDModulePilotAI>.Enumerator pPilots = pilots[pKeys.Current].GetEnumerator();
                while (pPilots.MoveNext())
                {
                    if (pPilots.Current == null) continue;

                    //enable guard mode
                    if (!pPilots.Current.weaponManager.guardMode)
                    {
                      pPilots.Current.weaponManager.ToggleGuardMode();
                    }

                    //report all vessels
                    if (BDATargetManager.BoolToTeam(pPilots.Current.weaponManager.team) == BDArmorySettings.BDATeams.B)
                    {
                        BDATargetManager.ReportVessel(pPilots.Current.vessel, aLeader.weaponManager);
                    }
                    else
                    {
                        BDATargetManager.ReportVessel(pPilots.Current.vessel, bLeader.weaponManager);
                    }

                    //release command
                    pPilots.Current.ReleaseCommand();
                    pPilots.Current.defaultOrbitCoords = centerGPS;
                }
            }
            pKeys.Dispose();
            competitionStatus = "Competition starting!  Good luck!";
            yield return new WaitForSeconds(2);
            competitionStarting = false;
        }
    }
}