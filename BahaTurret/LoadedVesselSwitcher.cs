using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BahaTurret
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

        //gui params
        private float _windowHeight; //auto adjusting
        private Rect _windowRect;
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

            _windowRect = new Rect(10, Screen.height / 6f, _windowWidth, 10);

            _ready = false;
            StartCoroutine(WaitForBdaSettings());
        }

        private IEnumerator WaitForBdaSettings()
        {
            while (BDArmorySettings.Instance == null)
                yield return null;

            _ready = true;
            BDArmorySettings.Instance.hasVS = true;
            _guiCheckIndex = Misc.RegisterGUIRect(new Rect());
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
                    _showGui = BDArmorySettings.Instance.showVSGUI;
                    if (_showGui)
                        UpdateList();
                }

                if (_showGui)
                    Hotkeys();
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
            if (_wmgrsA == null)
                _wmgrsA = new List<MissileFire>();
            _wmgrsA.Clear();

            if (_wmgrsB == null)
                _wmgrsB = new List<MissileFire>();
            _wmgrsB.Clear();

            if (FlightGlobals.Vessels == null) return;
            foreach (var v in FlightGlobals.Vessels)
            {
                if (!v) continue;

                if (!v.loaded || v.packed)
                    continue;

                foreach (var wm in v.FindPartModulesImplementing<MissileFire>())
                {
                    if (!wm.team)
                        _wmgrsA.Add(wm);
                    else
                        _wmgrsB.Add(wm);
                    break;
                }
            }
        }

        private void OnGUI()
        {
            if (_ready)
            {
                if (_showGui && BDArmorySettings.GAME_UI_ENABLED)
                {
                    _windowRect.height = _windowHeight;
                    _windowRect = GUI.Window(10293444, _windowRect, ListWindow, "BDA Vessel Switcher",
                        HighLogic.Skin.window);
                    Misc.UpdateGUIRect(_windowRect, _guiCheckIndex);
                }
                else
                {
                    Misc.UpdateGUIRect(new Rect(), _guiCheckIndex);
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
            var vesselButtonWidth = _windowWidth - 2 * _margin;
            vesselButtonWidth -= 3 * _buttonHeight;
            foreach (var wm in _wmgrsA)
            {
                var lineY = height + vesselLineA * (_buttonHeight + _buttonGap);
                var buttonRect = new Rect(_margin, lineY, vesselButtonWidth, _buttonHeight);
                var vButtonStyle = wm.vessel.isActiveVessel ? HighLogic.Skin.box : HighLogic.Skin.button;

                var status = UpdateVesselStatus(wm, vButtonStyle);


                if (GUI.Button(buttonRect, status + wm.vessel.GetName(), vButtonStyle))
                    FlightGlobals.ForceSetActiveVessel(wm.vessel);

                //guard toggle
                var guardStyle = wm.guardMode ? HighLogic.Skin.box : HighLogic.Skin.button;
                var guardButtonRect = new Rect(_margin + vesselButtonWidth, lineY, _buttonHeight, _buttonHeight);
                if (GUI.Button(guardButtonRect, "G", guardStyle))
                    wm.ToggleGuardMode();

                //AI toggle
                if (wm.pilotAI)
                {
                    var aiStyle = wm.pilotAI.pilotEnabled ? HighLogic.Skin.box : HighLogic.Skin.button;
                    var aiButtonRect = new Rect(_margin + vesselButtonWidth + _buttonHeight, lineY, _buttonHeight,
                        _buttonHeight);
                    if (GUI.Button(aiButtonRect, "P", aiStyle))
                        wm.pilotAI.TogglePilot();
                }

                //team toggle
                var teamButtonRect = new Rect(_margin + vesselButtonWidth + _buttonHeight + _buttonHeight, lineY,
                    _buttonHeight, _buttonHeight);
                if (GUI.Button(teamButtonRect, "T", HighLogic.Skin.button))
                {
                    _wmToSwitchTeam = wm;
                    _teamSwitchDirty = true;
                }
                vesselLineA++;
            }
            height += vesselLineA * (_buttonHeight + _buttonGap);
            height += _margin;
            GUI.Label(new Rect(_margin, height, _windowWidth - 2 * _margin, _buttonHeight), "Team B:", HighLogic.Skin.label);
            height += _buttonHeight;
            foreach (var wm in _wmgrsB)
            {
                var lineY = height + vesselLineB * (_buttonHeight + _buttonGap);

                var buttonRect = new Rect(_margin, lineY, vesselButtonWidth, _buttonHeight);
                var vButtonStyle = wm.vessel.isActiveVessel ? HighLogic.Skin.box : HighLogic.Skin.button;

                var status = UpdateVesselStatus(wm, vButtonStyle);


                if (GUI.Button(buttonRect, status + wm.vessel.GetName(), vButtonStyle))
                    FlightGlobals.ForceSetActiveVessel(wm.vessel);


                //guard toggle
                var guardStyle = wm.guardMode ? HighLogic.Skin.box : HighLogic.Skin.button;
                var guardButtonRect = new Rect(_margin + vesselButtonWidth, lineY, _buttonHeight, _buttonHeight);
                if (GUI.Button(guardButtonRect, "G", guardStyle))
                    wm.ToggleGuardMode();

                //AI toggle
                if (wm.pilotAI)
                {
                    var aiStyle = wm.pilotAI.pilotEnabled ? HighLogic.Skin.box : HighLogic.Skin.button;
                    var aiButtonRect = new Rect(_margin + vesselButtonWidth + _buttonHeight, lineY, _buttonHeight,
                        _buttonHeight);
                    if (GUI.Button(aiButtonRect, "P", aiStyle))
                        wm.pilotAI.TogglePilot();
                }

                //team toggle
                var teamButtonRect = new Rect(_margin + vesselButtonWidth + _buttonHeight + _buttonHeight, lineY,
                    _buttonHeight, _buttonHeight);
                if (GUI.Button(teamButtonRect, "T", HighLogic.Skin.button))
                {
                    _wmToSwitchTeam = wm;
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
            var status = "";
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
            var switchNext = false;
            foreach (var wm in _wmgrsA)
                if (switchNext)
                {
                    FlightGlobals.ForceSetActiveVessel(wm.vessel);
                    return;
                }
                else if (wm.vessel.isActiveVessel)
                {
                    switchNext = true;
                }

            foreach (var wm in _wmgrsB)
                if (switchNext)
                {
                    FlightGlobals.ForceSetActiveVessel(wm.vessel);
                    return;
                }
                else if (wm.vessel.isActiveVessel)
                {
                    switchNext = true;
                }


            if (_wmgrsA.Count > 0 && _wmgrsA[0] && !_wmgrsA[0].vessel.isActiveVessel)
                FlightGlobals.ForceSetActiveVessel(_wmgrsA[0].vessel);
            else if (_wmgrsB.Count > 0 && _wmgrsB[0] && !_wmgrsB[0].vessel.isActiveVessel)
                FlightGlobals.ForceSetActiveVessel(_wmgrsB[0].vessel);
        }

        private void SwitchToPreviousVessel()
        {
            if (_wmgrsB.Count > 0)
                for (var i = _wmgrsB.Count - 1; i >= 0; i--)
                    if (_wmgrsB[i].vessel.isActiveVessel)
                        if (i > 0)
                        {
                            FlightGlobals.ForceSetActiveVessel(_wmgrsB[i - 1].vessel);
                            return;
                        }
                        else if (_wmgrsA.Count > 0)
                        {
                            FlightGlobals.ForceSetActiveVessel(_wmgrsA[_wmgrsA.Count - 1].vessel);
                            return;
                        }
                        else if (_wmgrsB.Count > 0)
                        {
                            FlightGlobals.ForceSetActiveVessel(_wmgrsB[_wmgrsB.Count - 1].vessel);
                            return;
                        }

            if (_wmgrsA.Count > 0)
                for (var i = _wmgrsA.Count - 1; i >= 0; i--)
                    if (_wmgrsA[i].vessel.isActiveVessel)
                        if (i > 0)
                        {
                            FlightGlobals.ForceSetActiveVessel(_wmgrsA[i - 1].vessel);
                            return;
                        }
                        else if (_wmgrsB.Count > 0)
                        {
                            FlightGlobals.ForceSetActiveVessel(_wmgrsB[_wmgrsB.Count - 1].vessel);
                            return;
                        }
                        else if (_wmgrsA.Count > 0)
                        {
                            FlightGlobals.ForceSetActiveVessel(_wmgrsA[_wmgrsA.Count - 1].vessel);
                            return;
                        }
        }
    }
}