using System;
using System.Collections;
using System.Collections.Generic;
using BDArmory.UI;
using UnityEngine;

namespace BDArmory
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class LoadedVesselSwitcher : MonoBehaviour
    {
        private readonly float _buttonGap = 1;
        private readonly float _buttonHeight = 20;

        private int _guiCheckIndex;
        public LoadedVesselSwitcher Instance;
        private readonly float _margin = 5;

        private bool _ready;
        private bool _showGui;
        private bool _teamSwitchDirty;
        private readonly float _titleHeight = 30;
        private float updateTimer = 0;

        //gui params
        private float _windowHeight; //auto adjusting
        private Rect _windowRect
        {
            get { return BDArmorySettings.WindowRectVesselSwitcher; }
            set { BDArmorySettings.WindowRectVesselSwitcher = value; }
        }
        private readonly float _windowWidth = 250;

        private List<MissileFire> _wmgrsA;
        private List<MissileFire> _wmgrsB;

        private MissileFire _wmToSwitchTeam;

        private void Awake()
        {
            if (Instance)
                Destroy(this);
            else
                Instance = this;
        }

        private void Start()
        {
            UpdateList();
            GameEvents.onVesselCreate.Add(VesselEventUpdate);
            GameEvents.onVesselDestroy.Add(VesselEventUpdate);
            GameEvents.onVesselGoOffRails.Add(VesselEventUpdate);
            GameEvents.onVesselGoOnRails.Add(VesselEventUpdate);
            MissileFire.OnToggleTeam += MissileFireOnToggleTeam;

            _ready = false;
            StartCoroutine(WaitForBdaSettings());

            // TEST
            FloatingOrigin.fetch.threshold = 20000; //20km
            FloatingOrigin.fetch.thresholdSqr = 20000*20000; //20km
            Debug.Log($"FLOATINGORIGIN: threshold is {FloatingOrigin.fetch.threshold}");

            //_windowRect = new Rect(10, Screen.height / 6f, _windowWidth, 10); // now tied to BDArmorySettings persisted field!
        }

        private void OnDestroy()
        {
                GameEvents.onVesselCreate.Remove(VesselEventUpdate);
                GameEvents.onVesselDestroy.Remove(VesselEventUpdate);
                GameEvents.onVesselGoOffRails.Remove(VesselEventUpdate);
                GameEvents.onVesselGoOnRails.Remove(VesselEventUpdate);
                MissileFire.OnToggleTeam -= MissileFireOnToggleTeam;

                _ready = false;

            // TEST
            Debug.Log($"FLOATINGORIGIN: threshold is {FloatingOrigin.fetch.threshold}");
        }

        private IEnumerator WaitForBdaSettings()
        {
            while (BDArmorySettings.Instance == null)
                yield return null;

            _ready = true;
            BDArmorySettings.Instance.hasVS = true;
            _guiCheckIndex = Misc.Misc.RegisterGUIRect(new Rect());
        }

        private void MissileFireOnToggleTeam(MissileFire wm, BDArmorySettings.BDATeams team)
        {
            if (_showGui)
                UpdateList();
        }

        private void VesselEventUpdate(Vessel v)
        {
            if (_showGui)
                UpdateList();
        }

        private void Update()
        {
            if (_ready)
            {
                if (BDArmorySettings.Instance.showVSGUI != _showGui)
                {
                    updateTimer -= Time.fixedDeltaTime;
                    _showGui = BDArmorySettings.Instance.showVSGUI;
                    if (_showGui && updateTimer < 0)
                    {
                        UpdateList();
                        updateTimer = 0.5f;    //next update in half a sec only
                    }
                }

                if (_showGui)
                {                    
                    Hotkeys();
                }
            }
        }

        private void Hotkeys()
        {
            if (Input.GetKeyDown(KeyCode.PageDown))
                SwitchToNextVessel();
            if (Input.GetKeyDown(KeyCode.PageUp))
                SwitchToPreviousVessel();
        }

        private void UpdateList()
        {
            if (_wmgrsA == null) _wmgrsA = new List<MissileFire>();
            _wmgrsA.Clear();

            if (_wmgrsB == null) _wmgrsB = new List<MissileFire>();
            _wmgrsB.Clear();

            List<Vessel>.Enumerator v = FlightGlobals.Vessels.GetEnumerator();
            while (v.MoveNext())
            {
                if (v.Current == null) continue;
                if (!v.Current.loaded || v.Current.packed) continue;
                List<MissileFire>.Enumerator wm = v.Current.FindPartModulesImplementing<MissileFire>().GetEnumerator();
                while (wm.MoveNext())
                {
                    if (wm.Current == null) continue;
                    if (!wm.Current.team) _wmgrsA.Add(wm.Current);
                    else _wmgrsB.Add(wm.Current);
                    break;
                }
                wm.Dispose();
            }
            v.Dispose();
        }

        private void OnGUI()
        {
            if (_ready)
            {
                if (_showGui && BDArmorySettings.GAME_UI_ENABLED)
                {
                    SetNewHeight(_windowHeight);
                    _windowRect = GUI.Window(10293444, _windowRect, ListWindow, "BDA Vessel Switcher",
                        HighLogic.Skin.window);
                    Misc.Misc.UpdateGUIRect(_windowRect, _guiCheckIndex);
                }
                else
                {
                    Misc.Misc.UpdateGUIRect(new Rect(), _guiCheckIndex);
                }

                if (_teamSwitchDirty)
                {
                    if (_wmToSwitchTeam)
                        _wmToSwitchTeam.ToggleTeam();
                    _teamSwitchDirty = false;
                    _wmToSwitchTeam = null;
                }
            }
        }

        private void SetNewHeight(float windowHeight)
        {
            BDArmorySettings.WindowRectVesselSwitcher.height = windowHeight;
        }

        private void ListWindow(int id)
        {
            GUI.DragWindow(new Rect(0, 0, _windowWidth - _buttonHeight - 4, _titleHeight));
            if (GUI.Button(new Rect(_windowWidth - _buttonHeight - 4, 4, _buttonHeight, _buttonHeight), "X",
                HighLogic.Skin.button))
            {
                BDArmorySettings.Instance.showVSGUI = false;
                return;
            }
            float height = 0;
            float vesselLineA = 0;
            float vesselLineB = 0;
            height += _margin + _titleHeight;
            GUI.Label(new Rect(_margin, height, _windowWidth - 2 * _margin, _buttonHeight), "Team A:", HighLogic.Skin.label);
            height += _buttonHeight;
            float vesselButtonWidth = _windowWidth - 2 * _margin;
            vesselButtonWidth -= 3 * _buttonHeight;

            List<MissileFire>.Enumerator wma = _wmgrsA.GetEnumerator();
            while (wma.MoveNext())
            {
                if (wma.Current == null) continue;
                float lineY = height + vesselLineA * (_buttonHeight + _buttonGap);
                Rect buttonRect = new Rect(_margin, lineY, vesselButtonWidth, _buttonHeight);
                GUIStyle vButtonStyle = wma.Current.vessel.isActiveVessel ? HighLogic.Skin.box : HighLogic.Skin.button;

                string status = UpdateVesselStatus(wma.Current, vButtonStyle);

                if (GUI.Button(buttonRect, status + wma.Current.vessel.GetName(), vButtonStyle))
                {
                    ForceSwitchVessel(wma.Current.vessel);
                }

                //guard toggle
                GUIStyle guardStyle = wma.Current.guardMode ? HighLogic.Skin.box : HighLogic.Skin.button;
                Rect guardButtonRect = new Rect(_margin + vesselButtonWidth, lineY, _buttonHeight, _buttonHeight);
                if (GUI.Button(guardButtonRect, "G", guardStyle))
                    wma.Current.ToggleGuardMode();

                //AI toggle
                if (wma.Current.pilotAI)
                {
                    GUIStyle aiStyle = wma.Current.pilotAI.pilotEnabled ? HighLogic.Skin.box : HighLogic.Skin.button;
                    Rect aiButtonRect = new Rect(_margin + vesselButtonWidth + _buttonHeight, lineY, _buttonHeight,
                        _buttonHeight);
                    if (GUI.Button(aiButtonRect, "P", aiStyle))
                        wma.Current.pilotAI.TogglePilot();
                }

                //team toggle
                Rect teamButtonRect = new Rect(_margin + vesselButtonWidth + _buttonHeight + _buttonHeight, lineY,
                    _buttonHeight, _buttonHeight);
                if (GUI.Button(teamButtonRect, "T", HighLogic.Skin.button))
                {
                    _wmToSwitchTeam = wma.Current;
                    _teamSwitchDirty = true;
                }
                vesselLineA++;
            }
            wma.Dispose();

            height += vesselLineA * (_buttonHeight + _buttonGap);
            height += _margin;
            GUI.Label(new Rect(_margin, height, _windowWidth - 2 * _margin, _buttonHeight), "Team B:", HighLogic.Skin.label);
            height += _buttonHeight;

            List<MissileFire>.Enumerator wmb = _wmgrsB.GetEnumerator();
            while (wmb.MoveNext())
            {
                if (wmb.Current == null) continue;
                float lineY = height + vesselLineB * (_buttonHeight + _buttonGap);

                Rect buttonRect = new Rect(_margin, lineY, vesselButtonWidth, _buttonHeight);
                GUIStyle vButtonStyle = wmb.Current.vessel.isActiveVessel ? HighLogic.Skin.box : HighLogic.Skin.button;

                string status = UpdateVesselStatus(wmb.Current, vButtonStyle);


                if (GUI.Button(buttonRect, status + wmb.Current.vessel.GetName(), vButtonStyle))
                {
                    ForceSwitchVessel(wmb.Current.vessel);
                }


                //guard toggle
                GUIStyle guardStyle = wmb.Current.guardMode ? HighLogic.Skin.box : HighLogic.Skin.button;
                Rect guardButtonRect = new Rect(_margin + vesselButtonWidth, lineY, _buttonHeight, _buttonHeight);
                if (GUI.Button(guardButtonRect, "G", guardStyle))
                    wmb.Current.ToggleGuardMode();

                //AI toggle
                if (wmb.Current.pilotAI)
                {
                    GUIStyle aiStyle = wmb.Current.pilotAI.pilotEnabled ? HighLogic.Skin.box : HighLogic.Skin.button;
                    Rect aiButtonRect = new Rect(_margin + vesselButtonWidth + _buttonHeight, lineY, _buttonHeight,
                        _buttonHeight);
                    if (GUI.Button(aiButtonRect, "P", aiStyle))
                        wmb.Current.pilotAI.TogglePilot();
                }

                //team toggle
                Rect teamButtonRect = new Rect(_margin + vesselButtonWidth + _buttonHeight + _buttonHeight, lineY,
                    _buttonHeight, _buttonHeight);
                if (GUI.Button(teamButtonRect, "T", HighLogic.Skin.button))
                {
                    _wmToSwitchTeam = wmb.Current;
                    _teamSwitchDirty = true;
                }
                vesselLineB++;
            }
            height += vesselLineB * (_buttonHeight + _buttonGap);
            height += _margin;

            _windowHeight = height;
        }

        private string UpdateVesselStatus(MissileFire wm, GUIStyle vButtonStyle)
        {
            string status = "";
            if (wm.vessel.LandedOrSplashed)
            {
                if (wm.vessel.Landed)
                    status = "(Landed)";
                else
                    status = "(Splashed)";
                vButtonStyle.fontStyle = FontStyle.Italic;
            }
            else
            {
                vButtonStyle.fontStyle = FontStyle.Normal;
            }
            return status;
        }

        private void SwitchToNextVessel()
        {
            bool switchNext = false;

            List<MissileFire>.Enumerator wma = _wmgrsA.GetEnumerator();
            while (wma.MoveNext())
            {
                if (wma.Current == null) continue;
                if (switchNext)
                {
                    ForceSwitchVessel(wma.Current.vessel);
                    return;
                }
                if (wma.Current.vessel.isActiveVessel) switchNext = true;
            }
            wma.Dispose();

            List<MissileFire>.Enumerator wmb = _wmgrsB.GetEnumerator();
            while (wmb.MoveNext())
            {
                if (wmb.Current == null) continue;
                if (switchNext)
                {
                    ForceSwitchVessel(wmb.Current.vessel);
                    return;
                }
                if (wmb.Current.vessel.isActiveVessel) switchNext = true;
            }
            wmb.Dispose();

            if (_wmgrsA.Count > 0 && _wmgrsA[0] && !_wmgrsA[0].vessel.isActiveVessel)
            {
                ForceSwitchVessel(_wmgrsA[0].vessel);
            }
            else if (_wmgrsB.Count > 0 && _wmgrsB[0] && !_wmgrsB[0].vessel.isActiveVessel)
            {
                ForceSwitchVessel(_wmgrsB[0].vessel);
            }
        }

        private void SwitchToPreviousVessel()
        {
            if (_wmgrsB.Count > 0)
                for (int i = _wmgrsB.Count - 1; i >= 0; i--)
                    if (_wmgrsB[i].vessel.isActiveVessel)
                        if (i > 0)
                        {
                            ForceSwitchVessel(_wmgrsB[i - 1].vessel);
                            return;
                        }
                        else if (_wmgrsA.Count > 0)
                        {
                            ForceSwitchVessel(_wmgrsA[_wmgrsA.Count - 1].vessel);
                            return;
                        }
                        else if (_wmgrsB.Count > 0)
                        {
                            ForceSwitchVessel(_wmgrsB[_wmgrsB.Count - 1].vessel);
                            return;
                        }

            if (_wmgrsA.Count > 0)
                for (int i = _wmgrsA.Count - 1; i >= 0; i--)
                    if (_wmgrsA[i].vessel.isActiveVessel)
                        if (i > 0)
                        {
                            ForceSwitchVessel(_wmgrsA[i - 1].vessel);
                            return;
                        }
                        else if (_wmgrsB.Count > 0)
                        {
                            ForceSwitchVessel(_wmgrsB[_wmgrsB.Count - 1].vessel);
                            return;
                        }
                        else if (_wmgrsA.Count > 0)
                        {
                            ForceSwitchVessel(_wmgrsA[_wmgrsA.Count - 1].vessel);
                            return;
                        }
        }


        // Extracted method, so we dont have to call these two lines everywhere
        private void ForceSwitchVessel(Vessel v)
        {
            FlightGlobals.ForceSetActiveVessel(v);
            FlightInputHandler.ResumeVesselCtrlState(v);
        }

    }
}