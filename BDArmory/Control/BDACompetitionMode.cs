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
            Dictionary<BDArmorySetup.BDATeams, List<IBDAIControl>> pilots =
                new Dictionary<BDArmorySetup.BDATeams, List<IBDAIControl>>();
            pilots.Add(BDArmorySetup.BDATeams.A, new List<IBDAIControl>());
            pilots.Add(BDArmorySetup.BDATeams.B, new List<IBDAIControl>());
            List<Vessel>.Enumerator loadedVessels = BDATargetManager.LoadedVessels.GetEnumerator();
            while (loadedVessels.MoveNext())
            {
                if (loadedVessels.Current == null) continue;
                if (!loadedVessels.Current.loaded) continue;
                IBDAIControl pilot = null;
                IEnumerator<IBDAIControl> ePilots = loadedVessels.Current.FindPartModulesImplementing<IBDAIControl>().AsEnumerable().GetEnumerator();
                while (ePilots.MoveNext())
                {
                    pilot = ePilots.Current;
                    break;
                }
                ePilots.Dispose();
                if (pilot == null || !pilot.weaponManager) continue;

                pilots[BDATargetManager.BoolToTeam(pilot.weaponManager.team)].Add(pilot);
                pilot.CommandTakeOff();
                if (pilot.weaponManager.guardMode)
                {
                    pilot.weaponManager.ToggleGuardMode();
                }
            }
            loadedVessels.Dispose();
            
            //clear target database so pilots don't attack yet
            BDATargetManager.ClearDatabase();

            if (pilots[BDArmorySetup.BDATeams.A].Count == 0 || pilots[BDArmorySetup.BDATeams.B].Count == 0)
            {
                Debug.Log("[BDArmory]: Unable to start competition mode - one or more teams is empty");
                competitionStatus = "Competition: Failed!  One or more teams is empty.";
                yield return new WaitForSeconds(2);
                competitionStarting = false;
                yield break;
            }

            IBDAIControl aLeader = pilots[BDArmorySetup.BDATeams.A][0];
            IBDAIControl bLeader = pilots[BDArmorySetup.BDATeams.B][0];

            aLeader.weaponManager.wingCommander.CommandAllFollow();
            bLeader.weaponManager.wingCommander.CommandAllFollow();


            //wait till the leaders are ready to engage (airborne for PilotAI)
            while (aLeader != null && bLeader != null && (!aLeader.CanEngage() || !bLeader.CanEngage()))
            {
                yield return null;
            }

            if (aLeader == null || bLeader == null)
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

                if (aLeader == null || bLeader == null)
                {
                    StopCompetition();
                }

                if (Vector3.Distance(aLeader.transform.position, bLeader.transform.position) < distance*1.95f)
                {
                    waiting = true;
                }
                else
                {
                    Dictionary<BDArmorySetup.BDATeams, List<IBDAIControl>>.KeyCollection.Enumerator keys = pilots.Keys.GetEnumerator();
                    while (keys.MoveNext())
                    {
                        List<IBDAIControl>.Enumerator ePilots = pilots[keys.Current].GetEnumerator();
                        while (ePilots.MoveNext())
                        {
                            if (ePilots.Current == null) continue;
                            if (ePilots.Current.currentCommand != PilotCommands.Follow ||
                                !(Vector3.ProjectOnPlane(
                                    ePilots.Current.vessel.CoM - ePilots.Current.commandLeader.vessel.CoM,
                                    VectorUtils.GetUpDirection(ePilots.Current.commandLeader.vessel.transform.position)
                                    ).sqrMagnitude > 1000f*1000f)) continue;
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
            Dictionary<BDArmorySetup.BDATeams, List<IBDAIControl>>.KeyCollection.Enumerator pKeys = pilots.Keys.GetEnumerator();
            while (pKeys.MoveNext())
            {
                List<IBDAIControl>.Enumerator pPilots = pilots[pKeys.Current].GetEnumerator();
                while (pPilots.MoveNext())
                {
                    if (pPilots.Current == null) continue;

                    //enable guard mode
                    if (!pPilots.Current.weaponManager.guardMode)
                    {
                      pPilots.Current.weaponManager.ToggleGuardMode();
                    }

                    //report all vessels
                    if (BDATargetManager.BoolToTeam(pPilots.Current.weaponManager.team) == BDArmorySetup.BDATeams.B)
                    {
                        BDATargetManager.ReportVessel(pPilots.Current.vessel, aLeader.weaponManager);
                    }
                    else
                    {
                        BDATargetManager.ReportVessel(pPilots.Current.vessel, bLeader.weaponManager);
                    }

                    //release command
                    pPilots.Current.ReleaseCommand();
                    pPilots.Current.CommandAttack(centerGPS);
                }
            }
            pKeys.Dispose();
            competitionStatus = "Competition starting!  Good luck!";
            yield return new WaitForSeconds(2);
            competitionStarting = false;
        }
    }
}