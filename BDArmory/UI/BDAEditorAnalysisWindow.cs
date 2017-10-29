using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KSP.UI.Screens;
using BDArmory.Radar;
using BDArmory.Parts;
using BDArmory.Misc;

namespace BDArmory.UI
{
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    class BDAEditorAnalysisWindow : MonoBehaviour
    {

        public static BDAEditorAnalysisWindow Instance = null;
        private ApplicationLauncherButton toolbarButton = null;

        private bool showRcsWindow = false;
        private string windowTitle = "BDArmory Radar Cross Section Analysis";
        private Rect windowRect = new Rect(300, 150, 650, 500);

        private bool takeSnapshot = false;
        private float rcsReductionFactor;
        private float rcsGCF = 1.0f;

        private ModuleRadar[] radars;
        private GUIContent[] radarsGUI;
        private GUIContent radarBoxText;
        private BDGUIComboBox radarBox;
        private int previous_index = -1;
        private string text_detection;
        private string text_locktrack;
        private string text_sonar;
        private bool bLandedSplashed;

        void Awake()
        {
        }

        void Start()
        {
            Instance = this;
            AddToolbarButton();

            RadarUtils.SetupResources();
            GameEvents.onEditorShipModified.Add(OnEditorShipModifiedEvent);
        }

        private void FillRadarList()
        {
            radars = BDAEditorCategory.getRadars().ToArray();

            // first pass, then sort
            for (int i=0; i<radars.Length; i++)
            {
                if (string.IsNullOrEmpty(radars[i].radarName)) radars[i].radarName = radars[i].part?.partInfo?.title;
                GUIContent gui = new GUIContent(radars[i].radarName);
            }
            Array.Sort(radars, delegate (ModuleRadar r1, ModuleRadar r2) { return r1.radarName.CompareTo(r2.radarName); });

            // second pass to copy
            radarsGUI = new GUIContent[radars.Length];
            for (int i = 0; i < radars.Length; i++)
            {
                GUIContent gui = new GUIContent(radars[i].radarName);
                radarsGUI[i] = gui;
            }

            radarBoxText = new GUIContent();
            radarBoxText.text = "Select Radar... **";
        }


        private void OnEditorShipModifiedEvent(ShipConstruct data)
        {
            takeSnapshot = true;
            previous_index = -1;
        }

        private void OnDestroy()
        {
            GameEvents.onEditorShipModified.Remove(OnEditorShipModifiedEvent);
            RadarUtils.CleanupResources();

            if (toolbarButton)
            {
                ApplicationLauncher.Instance.RemoveModApplication(toolbarButton);
                toolbarButton = null;
            }
         }

        IEnumerator ToolbarButtonRoutine()
        {
            if (toolbarButton || (!HighLogic.LoadedSceneIsEditor)) yield break;
            while (!ApplicationLauncher.Ready)
            {
                yield return null;
            }

            AddToolbarButton();
        }

        void AddToolbarButton()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                if (toolbarButton == null)
                {
                    Texture buttonTexture = GameDatabase.Instance.GetTexture(BDArmorySettings.textureDir + "icon_rcs", false);
                    toolbarButton = ApplicationLauncher.Instance.AddModApplication(ShowToolbarGUI, HideToolbarGUI, Dummy, Dummy, Dummy, Dummy, ApplicationLauncher.AppScenes.SPH | ApplicationLauncher.AppScenes.VAB, buttonTexture);
                }
            }
        }

        public void ShowToolbarGUI()
        {
            showRcsWindow = true;
            takeSnapshot = true;
        }

        public void HideToolbarGUI()
        {
            showRcsWindow = false;
            takeSnapshot = false;
        }

        void Dummy()
        { }

        void OnGUI()
        {
            if (showRcsWindow)
            {
               windowRect = GUI.Window(this.GetInstanceID(), windowRect, RcsWindow, windowTitle, HighLogic.Skin.window);
            }

            PreventClickThrough();
        }

        void RcsWindow(int windowID)
        {
            GUI.Label(new Rect(10, 40, 200, 20), "Frontal", HighLogic.Skin.box);
            GUI.Label(new Rect(220, 40, 200, 20), "Lateral", HighLogic.Skin.box);
            GUI.Label(new Rect(430, 40, 200, 20), "Ventral",  HighLogic.Skin.box);

            if (takeSnapshot)
                takeRadarSnapshot();

            // for each view draw the rendering with the higher cross section (normal or 45°):
            if (RadarUtils.rcsFrontal > RadarUtils.rcsFrontal45)
                GUI.DrawTexture(new Rect(10, 70, 200, 200), RadarUtils.GetTextureFrontal, ScaleMode.StretchToFill);
            else
                GUI.DrawTexture(new Rect(10, 70, 200, 200), RadarUtils.GetTextureFrontal45, ScaleMode.StretchToFill);

            if (RadarUtils.rcsLateral > RadarUtils.rcsLateral45)
                GUI.DrawTexture(new Rect(220, 70, 200, 200), RadarUtils.GetTextureLateral, ScaleMode.StretchToFill);
            else
                GUI.DrawTexture(new Rect(220, 70, 200, 200), RadarUtils.GetTextureLateral45, ScaleMode.StretchToFill);

            if (RadarUtils.rcsVentral > RadarUtils.rcsVentral45)
                GUI.DrawTexture(new Rect(430, 70, 200, 200), RadarUtils.GetTextureVentral, ScaleMode.StretchToFill);
            else
                GUI.DrawTexture(new Rect(430, 70, 200, 200), RadarUtils.GetTextureVentral45, ScaleMode.StretchToFill);


            GUI.Label(new Rect(10, 275, 200, 20), string.Format("{0:0.00}", Mathf.Max(RadarUtils.rcsFrontal, RadarUtils.rcsFrontal45)) + " m^2", HighLogic.Skin.label);
            GUI.Label(new Rect(220, 275, 200, 20), string.Format("{0:0.00}", Mathf.Max(RadarUtils.rcsLateral, RadarUtils.rcsLateral45)) + " m^2", HighLogic.Skin.label);
            GUI.Label(new Rect(430, 275, 200, 20), string.Format("{0:0.00}", Mathf.Max(RadarUtils.rcsVentral, RadarUtils.rcsVentral45)) + " m^2", HighLogic.Skin.label);

            GUIStyle style = HighLogic.Skin.label;
            style.fontStyle = FontStyle.Bold;
            GUI.Label(new Rect(10, 300, 600, 20), "Base radar cross section for vessel: " + string.Format("{0:0.00} m^2 (without ECM/countermeasures)", RadarUtils.rcsTotal) , style);
            GUI.Label(new Rect(10, 320, 600, 20), "Total radar cross section for vessel: " + string.Format("{0:0.00} m^2 (with RCS reduction/stealth/ground clutter)", RadarUtils.rcsTotal * rcsReductionFactor * rcsGCF), style);

            style.fontStyle = FontStyle.Normal;
            GUI.Label(new Rect(10, 380, 600, 20), "** (Range evaluation not accounting for ECM/countermeasures)", style);
            GUI.Label(new Rect(10, 410, 600, 20), text_detection, style);
            GUI.Label(new Rect(10, 430, 600, 20), text_locktrack, style);
            GUI.Label(new Rect(10, 450, 600, 20), text_sonar, style);

            bool bNewValue = GUI.Toggle(new Rect(490, 348, 150, 20), bLandedSplashed, "Splashed/Landed", HighLogic.Skin.toggle);

            if (radars == null)
            {
                FillRadarList();
                GUIStyle listStyle = new GUIStyle(HighLogic.Skin.button);
                listStyle.fixedHeight = 18; //make list contents slightly smaller
                radarBox = new BDGUIComboBox(new Rect(10, 350, 600, 20), new Rect(10, 350, 250, 20), radarBoxText, radarsGUI, 124, listStyle);
            }
            
            int selected_index = radarBox.Show();

            if ((selected_index != previous_index) || (bNewValue != bLandedSplashed))
            {
                text_sonar = "";
                bLandedSplashed = bNewValue;

                // selected radar changed - evaluate craft RCS against this radar
                if (selected_index != -1)
                {
                    var selected_radar = radars[selected_index];

                    // ground clutter factor from radar
                    if (bLandedSplashed)
                        rcsGCF = selected_radar.radarGroundClutterFactor;
                    else
                        rcsGCF = 1.0f;

                    if (selected_radar.canScan)
                    {
                        for (float distance = selected_radar.radarMaxDistanceDetect; distance >= 0; distance--)
                        {
                            text_detection = $"Detection: undetectable by this radar.";
                            if (selected_radar.radarDetectionCurve.Evaluate(distance) <= (RadarUtils.rcsTotal * rcsReductionFactor * rcsGCF))
                            {
                                text_detection = $"Detection: detected at {distance} km and closer";
                                break;
                            }
                        }

                    }
                    else
                    {
                        text_detection = "Detection: This radar does not have detection capabilities.";
                    }

                    if (selected_radar.canLock)
                    {
                        text_locktrack = $"Lock/Track: untrackable by this radar.";
                        for (float distance = selected_radar.radarMaxDistanceLockTrack; distance >= 0; distance--)
                        {
                            if (selected_radar.radarLockTrackCurve.Evaluate(distance) <= (RadarUtils.rcsTotal * rcsReductionFactor * rcsGCF))
                            {
                                text_locktrack = $"Lock/Track: tracked at {distance} km and closer";
                                break;
                            }
                        }

                    }
                    else
                    {
                        text_locktrack = "Lock/Track: This radar does not have locking/tracking capabilities.";
                    }

                    if (selected_radar.getRWRType(selected_radar.rwrThreatType) == "SONAR")
                        text_sonar = "SONAR - will only be able to detect/track splashed or submerged vessels!";
                }
            }
            previous_index = selected_index;

            GUI.DragWindow();
        }


        void takeRadarSnapshot()
        {
            if (EditorLogic.RootPart == null)
                return;

            // Encapsulate editor ShipConstruct into a vessel:
            Vessel v = new Vessel();
            v.parts = EditorLogic.fetch.ship.Parts;
            RadarUtils.RenderVesselRadarSnapshot(v, EditorLogic.RootPart.transform);  //first rendering for true RCS
            RadarUtils.RenderVesselRadarSnapshot(v, EditorLogic.RootPart.transform, true);  //second rendering for nice zoomed-in view
            takeSnapshot = false;

            // get RCS reduction measures (stealth/low observability)
            rcsReductionFactor = 1.0f;
            int rcsCount = 0;
            List<Part>.Enumerator parts = EditorLogic.fetch.ship.Parts.GetEnumerator();
            while (parts.MoveNext())
            {
                ModuleECMJammer rcsJammer = parts.Current.GetComponent<ModuleECMJammer>();
                if (rcsJammer != null)
                {
                    if (rcsJammer.rcsReduction)
                    {
                        rcsReductionFactor *= rcsJammer.rcsReductionFactor;
                        rcsCount++;
                    }
                }
            }
            parts.Dispose();

            if (rcsCount > 0)
                rcsReductionFactor = Mathf.Clamp((rcsReductionFactor * rcsCount), 0.0f, 1);    //same formula as in VesselECMJInfo must be used here!
        }


        /// <summary> 
        /// Lock the model if our own window is shown and has cursor focus to prevent click-through. 
        /// Code adapted from FAR Editor GUI
        /// </summary>
        private void PreventClickThrough()
        {
            bool cursorInGUI = false;
            EditorLogic EdLogInstance = EditorLogic.fetch;
            if (!EdLogInstance)
            {
                return;
            }
            if (showRcsWindow)
            {
                cursorInGUI = windowRect.Contains(GetMousePos());
            }
            if (cursorInGUI)
            {
                if (!CameraMouseLook.GetMouseLook())
                    EdLogInstance.Lock(false, false, false, "BDARCSLOCK");
                else
                    EdLogInstance.Unlock("BDARCSLOCK");
            }
            else if (!cursorInGUI)
            {
                EdLogInstance.Unlock("BDARCSLOCK");
            }
        }


        private Vector3 GetMousePos()
        {
            Vector3 mousePos = Input.mousePosition;
            mousePos.y = Screen.height - mousePos.y;
            return mousePos;
        }


    } //EditorRCsWindow
}
