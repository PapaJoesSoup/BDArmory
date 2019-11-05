using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BDArmory.Control;
using BDArmory.Core;
using BDArmory.Core.Extension;
using BDArmory.Guidances;
using BDArmory.Misc;
using BDArmory.Targeting;
using BDArmory.UI;
using UnityEngine;

namespace BDArmory.Modules
{
    public class BDModulePilotAI : BDGenericAIBase, IBDAIControl
    {
        public enum SteerModes { NormalFlight, Aiming }

        SteerModes steerMode = SteerModes.NormalFlight;

        public enum FightingModes { Energy, Angles }

        FightingModes fightingMode = FightingModes.Angles;

        bool belowMinAltitude;
        bool extending;

        bool requestedExtend;
        Vector3 requestedExtendTpos;

        public bool IsExtending
        {
            get { return extending || requestedExtend; }
        }

        public void RequestExtend(Vector3 tPosition)
        {
            requestedExtend = true;
            requestedExtendTpos = tPosition;
        }

        public override bool CanEngage()
        {
            return !vessel.LandedOrSplashed;
        }

        GameObject vobj;

        Transform velocityTransform
        {
            get
            {
                if (!vobj)
                {
                    vobj = new GameObject("velObject");
                    vobj.transform.position = vessel.ReferenceTransform.position;
                    vobj.transform.parent = vessel.ReferenceTransform;
                }

                return vobj.transform;
            }
        }

        Vector3 upDirection = Vector3.up;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Fighting Tactics ", advancedTweakable = true),
            UI_Toggle(enabledText = "Angles", disabledText = "Energy", scene = UI_Scene.All),]
        public bool isAngles = true;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Default Alt."),
            UI_FloatRange(minValue = 500f, maxValue = 15000f, stepIncrement = 25f, scene = UI_Scene.All)]
        public float defaultAltitude = 1500;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Turn Radius"),
            UI_FloatRange(minValue = 0f, maxValue = 5000f, stepIncrement = 25f, scene = UI_Scene.All)]
        public float cornerRadius = 500f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Min Altitude"),
            UI_FloatRange(minValue = 0f, maxValue = 6000, stepIncrement = 50f, scene = UI_Scene.All)]
        public float minAltitude = 150f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Steer Factor"),
            UI_FloatRange(minValue = 0.1f, maxValue = 20f, stepIncrement = .1f, scene = UI_Scene.All)]
        public float steerMult = 6;
        //make a combat steer mult and idle steer mult

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Steer Ki"),
            UI_FloatRange(minValue = 0.01f, maxValue = 1f, stepIncrement = 0.01f, scene = UI_Scene.All)]
        public float steerKiAdjust = 0.05f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Steer Limiter"),
            UI_FloatRange(minValue = .1f, maxValue = 1f, stepIncrement = .05f, scene = UI_Scene.All)]
        public float maxSteer = 1;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Steer Damping"),
            UI_FloatRange(minValue = 1f, maxValue = 8f, stepIncrement = 0.1f, scene = UI_Scene.All)]
        public float steerDamping = 3;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Dynamic Damper ", advancedTweakable = true),
            UI_Toggle(enabledText = "On", disabledText = "Off", scene = UI_Scene.All),]
        public bool dynamicDamper = false;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Dynamic Damping Factor", advancedTweakable = true),
           UI_FloatRange(minValue = 1f, maxValue = 10f, stepIncrement = 0.1f, scene = UI_Scene.All)]
        public float dynamicDampingFactor = 1;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Corner Speed"),
            UI_FloatRange(minValue = 0f, maxValue = 650f, stepIncrement = 5f, scene = UI_Scene.All)]
        public float evasiveSpeed = 250f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Max Speed"),
            UI_FloatRange(minValue = 20f, maxValue = 800f, stepIncrement = 1.0f, scene = UI_Scene.All)]
        public float maxSpeed = 325;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "TakeOff Speed"),
            UI_FloatRange(minValue = 10f, maxValue = 200f, stepIncrement = 1.0f, scene = UI_Scene.All)]
        public float takeOffSpeed = 70;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "MinCombatSpeed"),
            UI_FloatRange(minValue = 20f, maxValue = 200, stepIncrement = 1.0f, scene = UI_Scene.All)]
        public float minSpeed = 60f;
        float minSpeedAllowed;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Idle Speed"),
            UI_FloatRange(minValue = 10f, maxValue = 200f, stepIncrement = 1.0f, scene = UI_Scene.All)]
        public float idleSpeed = 120f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Missile Evasion Distance"),
            UI_FloatRange(minValue = 0f, maxValue = 10000f, stepIncrement = 10f, scene = UI_Scene.All)]
        public float evasionDistance = 2000;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Extend Multiplier", advancedTweakable = true),
            UI_FloatRange(minValue = 0f, maxValue = 5f, stepIncrement = 0.1f, scene = UI_Scene.All)]
        public float extendMulti = 1;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Max G"),
            UI_FloatRange(minValue = 2f, maxValue = 45f, stepIncrement = 0.25f, scene = UI_Scene.All)]
        public float maxAllowedGForce = 10;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Max AoA"),
            UI_FloatRange(minValue = 0f, maxValue = 85f, stepIncrement = 2.5f, scene = UI_Scene.All)]
        public float maxAllowedAoA = 35;
        float maxAllowedCosAoA;
        float lastAllowedAoA;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Extensions "),
            UI_Toggle(enabledText = "Enabled", disabledText = "Disabled", scene = UI_Scene.All),]
        public bool canExtend = true;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Orbit ", advancedTweakable = true),
            UI_Toggle(enabledText = "Starboard (CW)", disabledText = "Port (CCW)", scene = UI_Scene.All),]
        public bool ClockwiseOrbit = true;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Unclamp tuning ", advancedTweakable = true),
            UI_Toggle(enabledText = "Unclamped", disabledText = "Clamped", scene = UI_Scene.All),]
        public bool UpToEleven = false;
        bool toEleven = false;

        Dictionary<string, float> altMaxValues = new Dictionary<string, float>
        {
            { nameof(defaultAltitude), 100000f },
            { nameof(cornerRadius), 50000f },
            { nameof(minAltitude), 30000f },      
            { nameof(steerMult), 200f },
            { nameof(steerKiAdjust), 20f },
            { nameof(steerDamping), 100f },
            { nameof(evasiveSpeed), 2000f },
            { nameof(maxSpeed), 3000f },
            { nameof(takeOffSpeed), 2000f },
            { nameof(minSpeed), 2000f },
            { nameof(idleSpeed), 3000f },
            { nameof(extendMulti), 200f },
            { nameof(maxAllowedGForce), 1000f },
            { nameof(maxAllowedAoA), 180f },
        };

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Standby Mode"),
            UI_Toggle(enabledText = "On", disabledText = "Off")]
        public bool standbyMode = false;

        //manueuverability and g loading data
        float maxDynPresGRecorded;

        float maxPosG;
        float cosAoAAtMaxPosG;

        float maxNegG;
        float cosAoAAtMaxNegG;

        float[] gLoadMovingAvgArray = new float[32];
        float[] cosAoAMovingAvgArray = new float[32];
        int movingAvgIndex;

        float gLoadMovingAvg;
        float cosAoAMovingAvg;

        float gaoASlopePerDynPres;        //used to limit control input at very high dynamic pressures to avoid structural failure
        float gOffsetPerDynPres;

        float posPitchDynPresLimitIntegrator = 1;
        float negPitchDynPresLimitIntegrator = -1;

        float lastCosAoA;
        float lastPitchInput;

        //Controller Integral
        float pitchIntegral;
        float yawIntegral;
        //DynamicDamping
        float damperFactor = 1;

        //instantaneous turn radius and possible acceleration from lift
        //properties can be used so that other AI modules can read this for future maneuverability comparisons between craft
        float turnRadius;

        public float TurnRadius
        {
            get { return turnRadius; }
            private set { turnRadius = value; }
        }

        float maxLiftAcceleration;

        public float MaxLiftAcceleration
        {
            get { return maxLiftAcceleration; }
            private set { maxLiftAcceleration = value; }
        }

        float turningTimer;
        float evasiveTimer;
        Vector3 lastTargetPosition;

        LineRenderer lr;
        Vector3 flyingToPosition;
        Vector3 rollTarget;
        Vector3 angVelRollTarget;

        //speed controller
        bool useAB = true;
        bool useBrakes = true;
        bool regainEnergy = false;

        //collision detection
        int collisionDetectionTicker;
        float collisionDetectionTimer;
        Vector3 collisionAvoidDirection;

        //wing command
        bool useRollHint;
        private Vector3d debugFollowPosition;

        double commandSpeed;
        Vector3d commandHeading;

        float finalMaxSteer = 1;

        #region RMB info in editor

        // <color={XKCDColors.HexFormat.Lime}>Yes</color>
        public override string GetInfo()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<b>Available settings</b>:");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Default Alt.</color> - altitude to fly at when cruising/idle");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Min Altitude</color> - below this altitude AI will prioritize gaining altitude over combat");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Steer Factor</color> - higher will make the AI apply more control input for the same desired rotation");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Steer Ki</color> - higher will make the AI apply control trim faster");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Steer Limiter</color> - limit AI from applying full control input");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Steer Damping</color> - higher will make the AI apply more control input when it wants to stop rotation");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Max Speed</color> - AI will not fly faster than this");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- TakeOff Speed</color> - speed at which to start pitching up when taking off");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- MinCombat Speed</color> - AI will prioritize regaining speed over combat below this");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Idle Speed</color> - Cruising speed when not in combat");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Max G</color> - AI will try not to perform maneuvers at higher G than this");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Max AoA</color> - AI will try not to exceed this angle of attack");
            if (GameSettings.ADVANCED_TWEAKABLES)
            {
                sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Orbit</color> - Which direction to orbit when idling over a location");
                sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Unclamp tuning</color> - Increases variable limits, no direct effect on behaviour");
            }
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Standby Mode</color> - AI will not take off until an enemy is detected");

            return sb.ToString();
        }

        #endregion RMB info in editor

        protected override void Start()
        {
            base.Start();

            if (HighLogic.LoadedSceneIsFlight)
            {
                maxAllowedCosAoA = (float)Math.Cos(maxAllowedAoA * Math.PI / 180.0);
                lastAllowedAoA = maxAllowedAoA;
            }
        }

        public override void ActivatePilot()
        {
            base.ActivatePilot();

            belowMinAltitude = vessel.LandedOrSplashed;
            prevTargetDir = vesselTransform.up;
        }

        void Update()
        {
            if (BDArmorySettings.DRAW_DEBUG_LINES && pilotEnabled)
            {
                if (lr)
                {
                    lr.enabled = true;
                    lr.SetPosition(0, vessel.ReferenceTransform.position);
                    lr.SetPosition(1, flyingToPosition);
                }
                else
                {
                    lr = gameObject.AddComponent<LineRenderer>();
                    lr.positionCount = 2;
                    lr.startWidth = 0.5f;
                    lr.endWidth = 0.5f;
                }

                minSpeed = Mathf.Clamp(minSpeed, 0, idleSpeed - 20);
                minSpeed = Mathf.Clamp(minSpeed, 0, maxSpeed - 20);
            }
            else
            {
                if (lr)
                {
                    lr.enabled = false;
                }
            }

            // switch up the alt values if up to eleven is toggled
            if (UpToEleven != toEleven)
            {
                using (var s = altMaxValues.Keys.ToList().GetEnumerator())
                    while (s.MoveNext())
                    {
                        UI_FloatRange euic = (UI_FloatRange)
                            (HighLogic.LoadedSceneIsFlight ? Fields[s.Current].uiControlFlight : Fields[s.Current].uiControlEditor);
                        float tempValue = euic.maxValue;
                        euic.maxValue = altMaxValues[s.Current];
                        altMaxValues[s.Current] = tempValue;
                        // change the value back to what it is now after fixed update, because changing the max value will clamp it down
                        // using reflection here, don't look at me like that, this does not run often
                        StartCoroutine(setVar(s.Current, (float)typeof(BDModulePilotAI).GetField(s.Current).GetValue(this)));
                    }
                toEleven = UpToEleven;
            }
        }

        IEnumerator setVar(string name, float value)
        {
            yield return new WaitForFixedUpdate();
            typeof(BDModulePilotAI).GetField(name).SetValue(this, value);
        }

        void FixedUpdate()
        {
            //floating origin and velocity offloading corrections
            if (lastTargetPosition != null && (!FloatingOrigin.Offset.IsZero() || !Krakensbane.GetFrameVelocity().IsZero()))
            {
                lastTargetPosition -= FloatingOrigin.OffsetNonKrakensbane;
            }
        }

        protected override void AutoPilot(FlightCtrlState s)
        {
            finalMaxSteer = maxSteer;

            //default brakes off full throttle
            //s.mainThrottle = 1;

            //vessel.ActionGroups.SetGroup(KSPActionGroup.Brakes, false);
            AdjustThrottle(maxSpeed, true);
            useAB = true;
            useBrakes = true;
            vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, true);

            steerMode = SteerModes.NormalFlight;
            useVelRollTarget = false;

            
            if (isAngles == true)
            {
                fightingMode = FightingModes.Angles;         
            }
            else
            {
                fightingMode = FightingModes.Energy; //regain energy if in NormalFlight, and airspeed is under avg of CornerSpeed and minSpeed
            }

            if (vessel.LandedOrSplashed && standbyMode && weaponManager && (BDATargetManager.GetClosestTarget(this.weaponManager) == null || BDArmorySettings.PEACE_MODE)) //TheDog: replaced querying of targetdatabase with actual check if a target can be detected
            {
                //s.mainThrottle = 0;
                //vessel.ActionGroups.SetGroup(KSPActionGroup.Brakes, true);
                AdjustThrottle(0, true);
                return;
            }

            //upDirection = -FlightGlobals.getGeeForceAtPosition(transform.position).normalized;
            upDirection = VectorUtils.GetUpDirection(vessel.transform.position);

            CalculateAccelerationAndTurningCircle();
            float minAltNeeded = MinAltitudeNeeded();
            debugString.Append($"minAltNeeded: {minAltNeeded}");
            debugString.Append(Environment.NewLine);

            if ((fightingMode == FightingModes.Angles) || (steerMode == SteerModes.Aiming))
            {
                minSpeedAllowed = minSpeed;
            }
            else if ((fightingMode == FightingModes.Energy) && (steerMode == SteerModes.NormalFlight))
            {
                minSpeedAllowed = (evasiveSpeed + minSpeed) / 2;
            }
            else
            {
                minSpeedAllowed = evasiveSpeed;
            }
            
            if (MissileGuidance.GetRadarAltitude(vessel) < minAltNeeded)
            {
                belowMinAltitude = true;
            }

            if (vessel.srfSpeed < minSpeedAllowed) 
            {
                regainEnergy = true;
            }
            else if (!belowMinAltitude && vessel.srfSpeed > Mathf.Min(minSpeed + 20f, idleSpeed))
            {
                regainEnergy = false;
            }

            if (belowMinAltitude)
            {
                if (command != PilotCommands.Follow)
                {
                    currentStatus = "Gain Alt.";
                }
                TakeOff(s);
                turningTimer = 0;
            }
            else
            {
                if (FlyAvoidCollision(s))
                {
                    turningTimer = 0;
                }
                else if (command != PilotCommands.Free)
                {
                    UpdateCommand(s);
                }
                else
                {
                    UpdateAI(s);
                }
            }
            UpdateGAndAoALimits(s);
            AdjustPitchForGAndAoALimits(s);
        }

        void UpdateAI(FlightCtrlState s)
        {
            currentStatus = "Free";

            if (requestedExtend)
            {
                requestedExtend = false;
                extending = true;
                lastTargetPosition = requestedExtendTpos;
            }

            if (evasiveTimer > 0 || (weaponManager && (weaponManager.missileIsIncoming || weaponManager.isChaffing || weaponManager.isFlaring || weaponManager.underFire)))
            {
                if (evasiveTimer < 1)
                {
                    threatRelativePosition = vessel.Velocity().normalized + vesselTransform.right;

                    if (weaponManager)
                    {
                        if (weaponManager.rwr?.rwrEnabled ?? false) //use rwr to check missile threat direction
                        {
                            Vector3 missileThreat = Vector3.zero;
                            bool missileThreatDetected = false;
                            float closestMissileThreat = float.MaxValue;
                            for (int i = 0; i < weaponManager.rwr.pingsData.Length; i++)
                            {
                                TargetSignatureData threat = weaponManager.rwr.pingsData[i];
                                if (threat.exists && threat.signalStrength == 4)
                                {
                                    missileThreatDetected = true;
                                    float dist = (weaponManager.rwr.pingWorldPositions[i] - vesselTransform.position).sqrMagnitude;
                                    if (dist < closestMissileThreat)
                                    {
                                        closestMissileThreat = dist;
                                        missileThreat = weaponManager.rwr.pingWorldPositions[i];
                                    }
                                }
                            }
                            if (missileThreatDetected)
                            {
                                threatRelativePosition = missileThreat - vesselTransform.position;
                            }
                        }

                        if (weaponManager.underFire)
                        {
                            threatRelativePosition = weaponManager.incomingThreatPosition - vesselTransform.position;
                        }
                    }
                }
                Evasive(s);
                evasiveTimer += Time.fixedDeltaTime;
                turningTimer = 0;

                if (evasiveTimer > 3)
                {
                    evasiveTimer = 0;
                    collisionDetectionTicker = 21; //check for collision again after exiting evasion routine
                }
            }
            else if (!extending && weaponManager && targetVessel != null && targetVessel.transform != null)
            {
                evasiveTimer = 0;
                if (!targetVessel.LandedOrSplashed)
                {
                    Vector3 targetVesselRelPos = targetVessel.vesselTransform.position - vesselTransform.position;
                    if (vessel.altitude < defaultAltitude && Vector3.Angle(targetVesselRelPos, -upDirection) < 35)
                    {
                        //dangerous if low altitude and target is far below you - don't dive into ground!
                        extending = true;
                        lastTargetPosition = targetVessel.vesselTransform.position;
                    }

                    if (Vector3.Angle(targetVessel.vesselTransform.position - vesselTransform.position, vesselTransform.up) > 35)
                    {
                        turningTimer += Time.deltaTime;
                    }
                    else
                    {
                        turningTimer = 0;
                    }

                    debugString.Append($"turningTimer: {turningTimer}");
                    debugString.Append(Environment.NewLine);

                    float targetForwardDot = Vector3.Dot(targetVesselRelPos.normalized, vesselTransform.up);
                    float targetVelFrac = (float)(targetVessel.srfSpeed / vessel.srfSpeed);      //this is the ratio of the target vessel's velocity to this vessel's srfSpeed in the forward direction; this allows smart decisions about when to break off the attack

                    if (targetVelFrac < 0.8f && targetForwardDot < 0.2f && targetVesselRelPos.magnitude < 400)
                    {
                        extending = true;
                        lastTargetPosition = targetVessel.vesselTransform.position - vessel.Velocity();       //we'll set our last target pos based on the enemy vessel and where we were 1 seconds ago
                        weaponManager.ForceScan();
                    }
                    if (turningTimer > 15)
                    {
                        //extend if turning circles for too long
                        //extending = true;
                        RequestExtend(targetVessel.vesselTransform.position);
                        turningTimer = 0;
                        weaponManager.ForceScan();
                        //lastTargetPosition = targetVessel.transform.position;
                    }
                }
                else //extend if too close for agm attack
                {
                    float extendDistance = Mathf.Clamp(weaponManager.guardRange - 1800, 2500, 4000);
                    float srfDist = (GetSurfacePosition(targetVessel.transform.position) - GetSurfacePosition(vessel.transform.position)).sqrMagnitude;

                    if (srfDist < extendDistance * extendDistance && Vector3.Angle(vesselTransform.up, targetVessel.transform.position - vessel.transform.position) > 45)
                    {
                        extending = true;
                        lastTargetPosition = targetVessel.transform.position;
                        weaponManager.ForceScan();
                    }
                }

                if (!extending)
                {
                    currentStatus = "Engaging";
                    debugString.Append($"Flying to target");
                    debugString.Append(Environment.NewLine);
                    FlyToTargetVessel(s, targetVessel);
                }
            }
            else
            {
                evasiveTimer = 0;
                if (!extending)
                {
                    currentStatus = "Orbiting";
                    FlyOrbit(s, assignedPositionGeo, 2000, idleSpeed, ClockwiseOrbit);
                }
            }

            if (extending)
            {
                evasiveTimer = 0;
                currentStatus = "Extending";
                debugString.Append($"Extending");
                debugString.Append(Environment.NewLine);
                FlyExtend(s, lastTargetPosition);
            }
        }

        bool FlyAvoidCollision(FlightCtrlState s)
        {
            if (collisionDetectionTimer > 2)
            {
                collisionDetectionTimer = 0;
                collisionDetectionTicker = 20;
            }
            if (collisionDetectionTimer > 0)
            {
                //fly avoid
                currentStatus = "AvoidCollision";
                debugString.Append($"Avoiding Collision");
                debugString.Append(Environment.NewLine);
                collisionDetectionTimer += Time.fixedDeltaTime;

                Vector3 target = vesselTransform.position + collisionAvoidDirection;
                FlyToPosition(s, target);
                return true;
            }
            else if (collisionDetectionTicker > 20)
            {
                collisionDetectionTicker = 0;
                bool avoid = false;
                Vector3 badDirection;
                if (DetectCollision(flyingToPosition - vesselTransform.position, out badDirection))
                {
                    avoid = true;
                }
                else if (command != PilotCommands.Follow) //check collisions with other flying vessels
                {
                    List<Vessel>.Enumerator vs = BDATargetManager.LoadedVessels.GetEnumerator();
                    while (vs.MoveNext())
                    {
                        if (vs.Current == null) continue;
                        if (vs.Current == vessel || vs.Current.Landed ||
                            !(Vector3.Dot(vs.Current.transform.position - vesselTransform.position,
                                  vesselTransform.up) > 0)) continue;
                        if (!PredictCollisionWithVessel(vs.Current, 2.5f, 0.5f, out badDirection)) continue;
                        // the 'isLeadingFormation' check was bad anyway, as releasing one member would unset it, while still having other followers, moving it here
                        if (vs.Current.FindPartModuleImplementing<IBDAIControl>()?.commandLeader?.vessel == vessel) continue;
                        avoid = true;
                        break;
                    }
                    vs.Dispose();
                }

                if (!avoid) return false;
                collisionDetectionTimer += Time.fixedDeltaTime;
                Vector3 axis = -Vector3.Cross(vesselTransform.up, badDirection);
                collisionAvoidDirection = Quaternion.AngleAxis(25, axis) * badDirection;        //don't need to change the angle that much to avoid, and it should prevent stupid suicidal manuevers as well

                FlyAvoidCollision(s);
                return true;
            }
            else
            {
                collisionDetectionTicker++;
            }

            return false;
        }

        bool PredictCollisionWithVessel(Vessel v, float maxTime, float interval, out Vector3 badDirection)
        {
            if (vessel == null || v == null || v == weaponManager?.incomingMissileVessel
                || v.rootPart.FindModuleImplementing<MissileBase>() != null) //evasive will handle avoiding missiles
            {
                badDirection = Vector3.zero;
                return false;
            }

            float time = Mathf.Min(0.5f, maxTime);
            while (time < maxTime)
            {
                Vector3 tPos = AIUtils.PredictPosition(v, time);
                Vector3 myPos = AIUtils.PredictPosition(vessel, time);
                if (Vector3.SqrMagnitude(tPos - myPos) < 900f)
                {
                    badDirection = tPos - vesselTransform.position;
                    return true;
                }

                time = Mathf.MoveTowards(time, maxTime, interval);
            }

            badDirection = Vector3.zero;
            return false;
        }

        void FlyToTargetVessel(FlightCtrlState s, Vessel v)
        {
            Vector3 target = v.CoM;
            MissileBase missile = null;
            Vector3 vectorToTarget = v.transform.position - vesselTransform.position;
            float distanceToTarget = vectorToTarget.magnitude;
            float planarDistanceToTarget = Vector3.ProjectOnPlane(vectorToTarget, upDirection).magnitude;
            float angleToTarget = Vector3.Angle(target - vesselTransform.position, vesselTransform.up);
           
            if (weaponManager)
            {
                missile = weaponManager.CurrentMissile;
                if (missile != null)
                {
                    if (missile.GetWeaponClass() == WeaponClasses.Missile)
                    {
                        if (distanceToTarget > 5500f)
                        {
                            finalMaxSteer = GetSteerLimiterForSpeedAndPower();
                        }

                        if (missile.TargetingMode == MissileBase.TargetingModes.Heat && !weaponManager.heatTarget.exists)
                        {
                            debugString.Append($"Attempting heat lock");
                            debugString.Append(Environment.NewLine);
                            target += v.srf_velocity.normalized * 10;
                        }
                        else
                        {
                            target = MissileGuidance.GetAirToAirFireSolution(missile, v);
                        }

                        if (angleToTarget < 20f)
                        {
                            steerMode = SteerModes.Aiming;
                        }
                    }
                    else //bombing
                    {
                        if (distanceToTarget > 4500f)
                        {
                            finalMaxSteer = GetSteerLimiterForSpeedAndPower();
                        }

                        if (angleToTarget < 45f)
                        {
                            target = target + (Mathf.Max(defaultAltitude - 500f, minAltitude) * upDirection);
                            Vector3 tDir = (target - vesselTransform.position).normalized;
                            tDir = (1000 * tDir) - (vessel.Velocity().normalized * 600);
                            target = vesselTransform.position + tDir;
                        }
                        else
                        {
                            target = target + (Mathf.Max(defaultAltitude - 500f, minAltitude) * upDirection);
                        }
                    }
                }
                else if (weaponManager.currentGun)
                {
                    ModuleWeapon weapon = weaponManager.currentGun;
                    if (weapon != null)
                    {
                        Vector3 leadOffset = weapon.GetLeadOffset();

                        float targetAngVel = Vector3.Angle(v.transform.position - vessel.transform.position, v.transform.position + (vessel.Velocity()) - vessel.transform.position);
                        debugString.Append($"targetAngVel: {targetAngVel}");
                        debugString.Append(Environment.NewLine);
                        float magnifier = Mathf.Clamp(targetAngVel, 1f, 2f);
                        magnifier += ((magnifier - 1f) * Mathf.Sin(Time.time * 0.75f));
                        target -= magnifier * leadOffset;

                        angleToTarget = Vector3.Angle(vesselTransform.up, target - vesselTransform.position);
                        if (distanceToTarget < weaponManager.gunRange && angleToTarget < 20)
                        {
                            steerMode = SteerModes.Aiming; //steer to aim
                        }
                        else
                        {
                            if (distanceToTarget > 3500f || vessel.srfSpeed < takeOffSpeed)
                            {
                                finalMaxSteer = GetSteerLimiterForSpeedAndPower();
                            }
                            else
                            {
                                //figuring how much to lead the target's movement to get there after its movement assuming we can manage a constant speed turn
                                //this only runs if we're not aiming and not that far from the target
                                float curVesselMaxAccel = Math.Min(maxDynPresGRecorded * (float)vessel.dynamicPressurekPa, maxAllowedGForce * 9.81f);
                                if (curVesselMaxAccel > 0)
                                {
                                    float timeToTurn = (float)vessel.srfSpeed * angleToTarget * Mathf.Deg2Rad / curVesselMaxAccel;
                                    target += v.Velocity() * timeToTurn;
                                    //target += 0.5f * v.acceleration * timeToTurn * timeToTurn;
                                }
                            }
                        }

                        if (v.LandedOrSplashed)
                        {
                            if (distanceToTarget > defaultAltitude * 2.2f)
                            {
                                target = FlightPosition(target, defaultAltitude);
                            }
                            else
                            {
                                steerMode = SteerModes.Aiming;
                            }
                        }
                        else if (distanceToTarget > weaponManager.gunRange * 1.5f || Vector3.Dot(target - vesselTransform.position, vesselTransform.up) < 0)
                        {
                            target = v.CoM;
                        }
                    }
                }
                else if (planarDistanceToTarget > weaponManager.gunRange * 1.25f && (vessel.altitude < targetVessel.altitude || MissileGuidance.GetRadarAltitude(vessel) < defaultAltitude)) //climb to target vessel's altitude if lower and still too far for guns
                {
                    finalMaxSteer = GetSteerLimiterForSpeedAndPower();
                    target = vesselTransform.position + GetLimitedClimbDirectionForSpeed(vectorToTarget);
                }
                else
                {
                    finalMaxSteer = GetSteerLimiterForSpeedAndPower();
                }
            }

            float targetDot = Vector3.Dot(vesselTransform.up, v.transform.position - vessel.transform.position);

            //manage speed when close to enemy
            float finalMaxSpeed = maxSpeed;
            if (targetDot > 0)
            {
                finalMaxSpeed = Mathf.Max((distanceToTarget - 100) / 8, 0) + (float)v.srfSpeed;
                finalMaxSpeed = Mathf.Max(finalMaxSpeed, minSpeed + 25f);
            }
            AdjustThrottle(finalMaxSpeed, true);

            if ((targetDot < 0 && vessel.srfSpeed > finalMaxSpeed)
                && distanceToTarget < 300 && vessel.srfSpeed < v.srfSpeed * 1.25f && Vector3.Dot(vessel.Velocity(), v.Velocity()) > 0) //distance is less than 800m
            {
                debugString.Append($"Enemy on tail. Braking!");
                debugString.Append(Environment.NewLine);
                AdjustThrottle(evasiveSpeed, true);
            }
            if (missile != null
                && targetDot > 0
                && distanceToTarget < MissileLaunchParams.GetDynamicLaunchParams(missile, v.Velocity(), v.transform.position).minLaunchRange
                && vessel.srfSpeed > idleSpeed)
            {
                //extending = true;
                //lastTargetPosition = v.transform.position;
                RequestExtend(lastTargetPosition);
            }

            if (regainEnergy && angleToTarget > 30f)
            {
                RegainEnergy(s, target - vesselTransform.position);
                return;
            }
            else
            {
                useVelRollTarget = true;
                FlyToPosition(s, target);
                return;
            }
        }

        void RegainEnergy(FlightCtrlState s, Vector3 direction)
        {
            debugString.Append($"Regaining energy");
            debugString.Append(Environment.NewLine);

            steerMode = SteerModes.Aiming;
            Vector3 planarDirection = Vector3.ProjectOnPlane(direction, upDirection);
            float angle = (Mathf.Clamp(MissileGuidance.GetRadarAltitude(vessel) - minAltitude, 0, 1500) / 1500) * 90;
            angle = Mathf.Clamp(angle, 0, 55) * Mathf.Deg2Rad;

            Vector3 targetDirection = Vector3.RotateTowards(planarDirection, -upDirection, angle, 0);
            targetDirection = Vector3.RotateTowards(vessel.Velocity(), targetDirection, 15f * Mathf.Deg2Rad, 0).normalized;

            AdjustThrottle(maxSpeed, false);
            FlyToPosition(s, vesselTransform.position + (targetDirection * 100));
        }

        float GetSteerLimiterForSpeedAndPower()
        {
            float possibleAccel = speedController.GetPossibleAccel();
            float speed = (float)vessel.srfSpeed;

            debugString.Append($"possibleAccel: {possibleAccel}");
            debugString.Append(Environment.NewLine);

            float limiter = ((speed - 50) / 330f) + possibleAccel / 15f;
            debugString.Append($"unclamped limiter: { limiter}");
            debugString.Append(Environment.NewLine);

            return Mathf.Clamp01(limiter);
        }

        Vector3 prevTargetDir;
        Vector3 debugPos;
        bool useVelRollTarget;

        void FlyToPosition(FlightCtrlState s, Vector3 targetPosition)
        {
            if (!belowMinAltitude)
            {
                if (weaponManager && Time.time - weaponManager.timeBombReleased < 1.5f)
                {
                    targetPosition = vessel.transform.position + vessel.Velocity();
                }

                targetPosition = FlightPosition(targetPosition, minAltitude);
                targetPosition = vesselTransform.position + ((targetPosition - vesselTransform.position).normalized * 100);
            }

            Vector3d srfVel = vessel.Velocity();
            if (srfVel != Vector3d.zero)
            {
                velocityTransform.rotation = Quaternion.LookRotation(srfVel, -vesselTransform.forward);
            }
            velocityTransform.rotation = Quaternion.AngleAxis(90, velocityTransform.right) * velocityTransform.rotation;

            //ang vel
            Vector3 localAngVel = vessel.angularVelocity;
            //test
            Vector3 currTargetDir = (targetPosition - vesselTransform.position).normalized;
            if (steerMode == SteerModes.NormalFlight)
            {
                float gRotVel = ((10f * maxAllowedGForce) / ((float)vessel.srfSpeed));
                //currTargetDir = Vector3.RotateTowards(prevTargetDir, currTargetDir, gRotVel*Mathf.Deg2Rad, 0);
            }
            Vector3 targetAngVel = Vector3.Cross(prevTargetDir, currTargetDir) / Time.fixedDeltaTime;
            Vector3 localTargetAngVel = vesselTransform.InverseTransformVector(targetAngVel);
            prevTargetDir = currTargetDir;
            targetPosition = vessel.transform.position + (currTargetDir * 100);

            flyingToPosition = targetPosition;

            //test poststall
            float AoA = Vector3.Angle(vessel.ReferenceTransform.up, vessel.Velocity());
            if (AoA > 30f)
            {
                steerMode = SteerModes.Aiming;
            }

            //slow down for tighter turns
            float velAngleToTarget = Vector3.Angle(targetPosition - vesselTransform.position, vessel.Velocity());
            float normVelAngleToTarget = Mathf.Clamp(velAngleToTarget, 0, 90) / 90;
            float speedReductionFactor = 1.25f;
            float finalSpeed = Mathf.Min(speedController.targetSpeed, Mathf.Clamp(maxSpeed - (speedReductionFactor * normVelAngleToTarget), idleSpeed, maxSpeed));
            debugString.Append($"Final Target Speed: {finalSpeed}");
            debugString.Append(Environment.NewLine);
            AdjustThrottle(finalSpeed, useBrakes, useAB);

            if (steerMode == SteerModes.Aiming)
            {
                localAngVel -= localTargetAngVel;
            }

            Vector3 targetDirection;
            Vector3 targetDirectionYaw;
            float yawError;
            float pitchError;
            //float postYawFactor;
            //float postPitchFactor;
            if (steerMode == SteerModes.NormalFlight)
            {
                targetDirection = velocityTransform.InverseTransformDirection(targetPosition - velocityTransform.position).normalized;
                targetDirection = Vector3.RotateTowards(Vector3.up, targetDirection, 45 * Mathf.Deg2Rad, 0);

                targetDirectionYaw = vesselTransform.InverseTransformDirection(vessel.Velocity()).normalized;
                targetDirectionYaw = Vector3.RotateTowards(Vector3.up, targetDirectionYaw, 45 * Mathf.Deg2Rad, 0);
            }
            else//(steerMode == SteerModes.Aiming)
            {
                targetDirection = vesselTransform.InverseTransformDirection(targetPosition - vesselTransform.position).normalized;
                targetDirection = Vector3.RotateTowards(Vector3.up, targetDirection, 25 * Mathf.Deg2Rad, 0);
                targetDirectionYaw = targetDirection;
            }
            debugPos = vessel.transform.position + (targetPosition - vesselTransform.position) * 5000;

            pitchError = VectorUtils.SignedAngle(Vector3.up, Vector3.ProjectOnPlane(targetDirection, Vector3.right), Vector3.back);
            yawError = VectorUtils.SignedAngle(Vector3.up, Vector3.ProjectOnPlane(targetDirectionYaw, Vector3.forward), Vector3.right);

            //test
            debugString.Append($"finalMaxSteer: {finalMaxSteer}");
            debugString.Append(Environment.NewLine);

            //roll
            Vector3 currentRoll = -vesselTransform.forward;
            float rollUp = (steerMode == SteerModes.Aiming ? 5f : 10f);
            if (steerMode == SteerModes.NormalFlight)
            {
                rollUp += (1 - finalMaxSteer) * 10f;
            }
            rollTarget = (targetPosition + (rollUp * upDirection)) - vesselTransform.position;

            //test
            if (steerMode == SteerModes.Aiming && !belowMinAltitude)
            {
                angVelRollTarget = -140 * vesselTransform.TransformVector(Quaternion.AngleAxis(90f, Vector3.up) * localTargetAngVel);
                rollTarget += angVelRollTarget;
            }

            if (command == PilotCommands.Follow && useRollHint)
            {
                rollTarget = -commandLeader.vessel.ReferenceTransform.forward;
            }

            //
            if (belowMinAltitude)
            {
                rollTarget = vessel.upAxis * 100;
            }
            if (useVelRollTarget && !belowMinAltitude)
            {
                rollTarget = Vector3.ProjectOnPlane(rollTarget, vessel.Velocity());
                currentRoll = Vector3.ProjectOnPlane(currentRoll, vessel.Velocity());
            }
            else
            {
                rollTarget = Vector3.ProjectOnPlane(rollTarget, vesselTransform.up);
            }
            if (dynamicDamper == true)
            {
                damperFactor = (float)Math.Pow(vessel.srfSpeed / evasiveSpeed, 1 / dynamicDampingFactor);
            }
            else
            {
                damperFactor = 1;
            }
            
            //v/q
            float dynamicAdjustment = Mathf.Clamp(16 * (float)(vessel.srfSpeed / vessel.dynamicPressurekPa), 0, 1.2f);

            float rollError = Misc.Misc.SignedAngle(currentRoll, rollTarget, vesselTransform.right);
            float steerRoll = (steerMult * 0.0015f * rollError);
            float rollDamping = (.10f * (steerDamping * damperFactor) * -localAngVel.y);
            steerRoll -= rollDamping;
            steerRoll *= dynamicAdjustment;

            if (steerMode == SteerModes.NormalFlight)
            {
                //premature dive fix
                pitchError = pitchError * Mathf.Clamp01((21 - Mathf.Exp(Mathf.Abs(rollError) / 30)) / 20);
            }

            float steerPitch = (0.015f * steerMult * pitchError) - ((steerDamping * damperFactor) * -localAngVel.x * (1 + steerKiAdjust));
            float steerYaw = (0.005f * steerMult * yawError) - ((steerDamping * damperFactor) * 0.2f * -localAngVel.z * (1 + steerKiAdjust));

            pitchIntegral += pitchError;
            yawIntegral += yawError;

            steerPitch *= dynamicAdjustment;
            steerYaw *= dynamicAdjustment;

            float pitchKi = 0.1f * (steerKiAdjust / 5); //This is what should be allowed to be tweaked by the player, just like the steerMult, it is very low right now
            pitchIntegral = Mathf.Clamp(pitchIntegral, -0.2f / (pitchKi * dynamicAdjustment), 0.2f / (pitchKi * dynamicAdjustment)); //0.2f is the limit of the integral variable, making it bigger increases overshoot
            steerPitch += pitchIntegral * pitchKi * dynamicAdjustment; //Adds the integral component to the mix

            float yawKi = 0.1f * (steerKiAdjust / 15);
            yawIntegral = Mathf.Clamp(yawIntegral, -0.2f / (yawKi * dynamicAdjustment), 0.2f / (yawKi * dynamicAdjustment));
            steerYaw += yawIntegral * yawKi * dynamicAdjustment;

            float roll = Mathf.Clamp(steerRoll, -maxSteer, maxSteer);
            s.roll = roll;
            s.yaw = Mathf.Clamp(steerYaw, -finalMaxSteer, finalMaxSteer);
            s.pitch = Mathf.Clamp(steerPitch, Mathf.Min(-finalMaxSteer, -0.2f), finalMaxSteer);
        }

        void FlyExtend(FlightCtrlState s, Vector3 tPosition)
        {
            if (weaponManager)
            {
                if (weaponManager.TargetOverride)
                {
                    extending = false;
                }

                float extendDistance;

                if (canExtend == false)
                {
                    extendDistance = 0;
                }
                else
                {
                    extendDistance = Mathf.Clamp(weaponManager.guardRange - 1800, 2500, 4000) * extendMulti;
                }


                if (weaponManager.CurrentMissile && weaponManager.CurrentMissile.GetWeaponClass() == WeaponClasses.Bomb)
                {
                    extendDistance = 4500;
                }

                if (targetVessel != null && !targetVessel.LandedOrSplashed)      //this is just asking for trouble at 800m
                {
                    extendDistance = 1600;
                }

                Vector3 srfVector = Vector3.ProjectOnPlane(vessel.transform.position - tPosition, upDirection);
                float srfDist = srfVector.magnitude;
                if (srfDist < extendDistance)
                {
                    Vector3 targetDirection = srfVector.normalized * extendDistance;
                    Vector3 target = vessel.transform.position + targetDirection;
                    target = GetTerrainSurfacePosition(target) + (vessel.upAxis * Mathf.Min(defaultAltitude, MissileGuidance.GetRaycastRadarAltitude(vesselTransform.position)));
                    target = FlightPosition(target, defaultAltitude);
                    if (regainEnergy)
                    {
                        RegainEnergy(s, target - vesselTransform.position);
                        return;
                    }
                    else
                    {
                        FlyToPosition(s, target);
                    }
                }
                else
                {
                    extending = false;
                }
            }
            else
            {
                extending = false;
            }
        }

        void FlyOrbit(FlightCtrlState s, Vector3d centerGPS, float radius, float speed, bool clockwise)
        {
            if (regainEnergy)
            {
                RegainEnergy(s, vessel.Velocity());
                return;
            }

            finalMaxSteer = GetSteerLimiterForSpeedAndPower();

            debugString.Append($"Flying orbit");
            debugString.Append(Environment.NewLine);
            Vector3 flightCenter = GetTerrainSurfacePosition(VectorUtils.GetWorldSurfacePostion(centerGPS, vessel.mainBody)) + (defaultAltitude * upDirection);

            Vector3 myVectorFromCenter = Vector3.ProjectOnPlane(vessel.transform.position - flightCenter, upDirection);
            Vector3 myVectorOnOrbit = myVectorFromCenter.normalized * radius;

            Vector3 targetVectorFromCenter = Quaternion.AngleAxis(clockwise ? 15f : -15f, upDirection) * myVectorOnOrbit;

            Vector3 verticalVelVector = Vector3.Project(vessel.Velocity(), upDirection); //for vv damping

            Vector3 targetPosition = flightCenter + targetVectorFromCenter - (verticalVelVector * 0.25f);

            Vector3 vectorToTarget = targetPosition - vesselTransform.position;
            //Vector3 planarVel = Vector3.ProjectOnPlane(vessel.Velocity(), upDirection);
            //vectorToTarget = Vector3.RotateTowards(planarVel, vectorToTarget, 25f * Mathf.Deg2Rad, 0);
            vectorToTarget = GetLimitedClimbDirectionForSpeed(vectorToTarget);
            targetPosition = vesselTransform.position + vectorToTarget;

            if (command != PilotCommands.Free && (vessel.transform.position - flightCenter).sqrMagnitude < radius * radius * 1.5f)
            {
                Debug.Log("[BDArmory]: AI Pilot reached command destination.");
                command = PilotCommands.Free;
            }

            useVelRollTarget = true;

            AdjustThrottle(speed, false);
            FlyToPosition(s, targetPosition);
        }

        //sends target speed to speedController
        void AdjustThrottle(float targetSpeed, bool useBrakes, bool allowAfterburner = true)
        {
            speedController.targetSpeed = targetSpeed;
            speedController.useBrakes = useBrakes;
            speedController.allowAfterburner = allowAfterburner;
        }

        Vector3 threatRelativePosition;

        void Evasive(FlightCtrlState s)
        {
            if (s == null) return;
            if (vessel == null) return;
            if (weaponManager == null) return;

            currentStatus = "Evading";
            debugString.Append($"Evasive");
            debugString.Append(Environment.NewLine);
            debugString.Append($"Threat Distance: {weaponManager.incomingMissileDistance}");
            debugString.Append(Environment.NewLine);

            collisionDetectionTicker += 2;

            if (weaponManager)
            {
                if (weaponManager.isFlaring)
                {
                    useAB = vessel.srfSpeed < minSpeed;
                    useBrakes = false;
                    float targetSpeed = evasiveSpeed;
                    if (weaponManager.isChaffing)
                    {
                        targetSpeed = maxSpeed;
                    }
                    AdjustThrottle(targetSpeed, false, useAB);
                }

                if ((weaponManager.isChaffing || weaponManager.isFlaring) && (weaponManager.incomingMissileDistance > evasionDistance))
                {
                    debugString.Append($"Breaking from missile threat!");
                    debugString.Append(Environment.NewLine);

                    Vector3 axis = -Vector3.Cross(vesselTransform.up, threatRelativePosition);
                    Vector3 breakDirection = Quaternion.AngleAxis(90, axis) * threatRelativePosition;
                    //Vector3 breakTarget = vesselTransform.position + breakDirection;
                    RegainEnergy(s, breakDirection);
                    return;
                }
                else if (weaponManager.underFire)
                {
                    debugString.Append($"Dodging gunfire");
                    float threatDirectionFactor = Vector3.Dot(vesselTransform.up, threatRelativePosition.normalized);
                    //Vector3 axis = -Vector3.Cross(vesselTransform.up, threatRelativePosition);

                    Vector3 breakTarget = threatRelativePosition * 2f;       //for the most part, we want to turn _towards_ the threat in order to increase the rel ang vel and get under its guns

                    if (threatDirectionFactor > 0.9f)     //within 28 degrees in front
                    {
                        breakTarget += Vector3.Cross(threatRelativePosition.normalized, Mathf.Sign(Mathf.Sin((float)vessel.missionTime / 2)) * vessel.upAxis);
                        debugString.Append($" from directly ahead!");
                    }
                    else if (threatDirectionFactor < -0.9) //within ~28 degrees behind
                    {
                        float threatDistance = threatRelativePosition.magnitude;
                        if (threatDistance > 400)
                        {
                            breakTarget = vesselTransform.position + vesselTransform.up * 1500 - 500 * vessel.upAxis;
                            breakTarget += Mathf.Sin((float)vessel.missionTime / 2) * vesselTransform.right * 1000 - Mathf.Cos((float)vessel.missionTime / 2) * vesselTransform.forward * 1000;
                            if (threatDistance > 800)
                                debugString.Append($" from behind afar; engaging barrel roll");
                            else
                            {
                                debugString.Append($" from behind moderate distance; engaging aggressvie barrel roll and braking");
                                steerMode = SteerModes.Aiming;
                                AdjustThrottle(evasiveSpeed, true, false);
                            }
                        }
                        else
                        {
                            breakTarget = threatRelativePosition;
                            if (evasiveTimer < 1.5f)
                                breakTarget += Mathf.Sin((float)vessel.missionTime * 2) * vesselTransform.right * 500;
                            else
                                breakTarget += -Math.Sign(Mathf.Sin((float)vessel.missionTime * 2)) * vesselTransform.right * 150;

                            debugString.Append($" from directly behind and close; breaking hard");
                            steerMode = SteerModes.Aiming;
                        }
                    }
                    else
                    {
                        float threatDistance = threatRelativePosition.magnitude;
                        if (threatDistance < 400)
                        {
                            breakTarget += Mathf.Sin((float)vessel.missionTime * 2) * vesselTransform.right * 100;

                            steerMode = SteerModes.Aiming;
                        }
                        else
                        {
                            breakTarget = vesselTransform.position + vesselTransform.up * 1500;
                            breakTarget += Mathf.Sin((float)vessel.missionTime / 2) * vesselTransform.right * 1000 - Mathf.Cos((float)vessel.missionTime / 2) * vesselTransform.forward * 1000;
                            debugString.Append($" from far side; engaging barrel roll");
                        }
                    }

                    float threatAltitudeDiff = Vector3.Dot(threatRelativePosition, vessel.upAxis);
                    if (threatAltitudeDiff > 500)
                        breakTarget += threatAltitudeDiff * vessel.upAxis;      //if it's trying to spike us from below, don't go crazy trying to dive below it
                    else
                        breakTarget += -150 * vessel.upAxis;   //dive a bit to escape

                    FlyToPosition(s, breakTarget);
                    return;
                }
                else if (weaponManager.incomingMissileVessel)
                {
                    float mSqrDist = Vector3.SqrMagnitude(weaponManager.incomingMissileVessel.transform.position - vesselTransform.position);
                    if (mSqrDist < 810000) //900m
                    {
                        debugString.Append($"Missile about to impact! pull away!");
                        debugString.Append(Environment.NewLine);

                        AdjustThrottle(maxSpeed, false, false);
                        Vector3 cross = Vector3.Cross(weaponManager.incomingMissileVessel.transform.position - vesselTransform.position, vessel.Velocity()).normalized;
                        if (Vector3.Dot(cross, -vesselTransform.forward) < 0)
                        {
                            cross = -cross;
                        }
                        FlyToPosition(s, vesselTransform.position + (50 * vessel.Velocity() / vessel.srfSpeed) + (100 * cross));
                        return;
                    }
                }
            }

            Vector3 target = (vessel.srfSpeed < 200) ? FlightPosition(vessel.transform.position, minAltitude) : vesselTransform.position;
            float angleOff = Mathf.Sin(Time.time * 0.75f) * 180;
            angleOff = Mathf.Clamp(angleOff, -45, 45);
            target +=
                (Quaternion.AngleAxis(angleOff, upDirection) * Vector3.ProjectOnPlane(vesselTransform.up * 500, upDirection));
            //+ (Mathf.Sin (Time.time/3) * upDirection * minAltitude/3);

            FlyToPosition(s, target);
        }

        void TakeOff(FlightCtrlState s)
        {
            debugString.Append($"Taking off/Gaining altitude");
            debugString.Append(Environment.NewLine);

            if (vessel.LandedOrSplashed && vessel.srfSpeed < takeOffSpeed)
            {
                assignedPositionWorld = vessel.transform.position;
                return;
            }

            steerMode = SteerModes.Aiming;

            float radarAlt = MissileGuidance.GetRadarAltitude(vessel);

            Vector3 forwardPoint = vessel.transform.position + Vector3.ProjectOnPlane((vessel.horizontalSrfSpeed < 10 ? vesselTransform.up : (Vector3)vessel.srf_vel_direction) * 100, upDirection);
            float terrainDiff = MissileGuidance.GetRaycastRadarAltitude(forwardPoint) - radarAlt;
            terrainDiff = Mathf.Max(terrainDiff, 0);

            float rise = Mathf.Clamp((float)vessel.srfSpeed * 0.215f, 5, 100);

            if (radarAlt > 70)
            {
                vessel.ActionGroups.SetGroup(KSPActionGroup.Gear, false);
            }
            else
            {
                vessel.ActionGroups.SetGroup(KSPActionGroup.Gear, true);
            }

            FlyToPosition(s, forwardPoint + (upDirection * (rise + terrainDiff)));

            if (radarAlt > minAltitude)
            {
                belowMinAltitude = false;
            }
        }

        Vector3 GetLimitedClimbDirectionForSpeed(Vector3 direction)
        {
            if (Vector3.Dot(direction, upDirection) < 0)
            {
                debugString.Append($"climb limit angle: unlimited");
                debugString.Append(Environment.NewLine);
                return direction; //only use this if climbing
            }

            Vector3 planarDirection = Vector3.ProjectOnPlane(direction, upDirection).normalized * 100;

            float angle = Mathf.Clamp((float)vessel.srfSpeed * 0.13f, 5, 90);

            debugString.Append($"climb limit angle: {angle}");
            debugString.Append(Environment.NewLine);
            return Vector3.RotateTowards(planarDirection, direction, angle * Mathf.Deg2Rad, 0);
        }

        void UpdateGAndAoALimits(FlightCtrlState s)
        {
            if (vessel.dynamicPressurekPa <= 0 || vessel.srfSpeed < takeOffSpeed || belowMinAltitude && -Vector3.Dot(vessel.ReferenceTransform.forward, vessel.upAxis) < 0.8f)
            {
                return;
            }

            if (lastAllowedAoA != maxAllowedAoA)
            {
                lastAllowedAoA = maxAllowedAoA;
                maxAllowedCosAoA = (float)Math.Cos(lastAllowedAoA * Math.PI / 180.0);
            }
            float pitchG = -Vector3.Dot(vessel.acceleration, vessel.ReferenceTransform.forward);       //should provide g force in vessel up / down direction, assuming a standard plane
            float pitchGPerDynPres = pitchG / (float)vessel.dynamicPressurekPa;

            float curCosAoA = Vector3.Dot(vessel.Velocity().normalized, vessel.ReferenceTransform.forward);

            //adjust moving averages
            //adjust gLoad average
            gLoadMovingAvg *= 32f;
            gLoadMovingAvg -= gLoadMovingAvgArray[movingAvgIndex];
            gLoadMovingAvgArray[movingAvgIndex] = pitchGPerDynPres;
            gLoadMovingAvg += pitchGPerDynPres;
            gLoadMovingAvg /= 32f;

            //adjusting cosAoAAvg
            cosAoAMovingAvg *= 32f;
            cosAoAMovingAvg -= cosAoAMovingAvgArray[movingAvgIndex];
            cosAoAMovingAvgArray[movingAvgIndex] = curCosAoA;
            cosAoAMovingAvg += curCosAoA;
            cosAoAMovingAvg /= 32f;

            ++movingAvgIndex;
            if (movingAvgIndex == gLoadMovingAvgArray.Length)
                movingAvgIndex = 0;

            if (gLoadMovingAvg < maxNegG || Math.Abs(cosAoAMovingAvg - cosAoAAtMaxNegG) < 0.005f)
            {
                maxNegG = gLoadMovingAvg;
                cosAoAAtMaxNegG = cosAoAMovingAvg;
            }
            if (gLoadMovingAvg > maxPosG || Math.Abs(cosAoAMovingAvg - cosAoAAtMaxPosG) < 0.005f)
            {
                maxPosG = gLoadMovingAvg;
                cosAoAAtMaxPosG = cosAoAMovingAvg;
            }

            if (cosAoAAtMaxNegG >= cosAoAAtMaxPosG)
            {
                cosAoAAtMaxNegG = cosAoAAtMaxPosG = maxNegG = maxPosG = 0;
                gOffsetPerDynPres = gaoASlopePerDynPres = 0;
                return;
            }

            if (maxPosG > maxDynPresGRecorded)
                maxDynPresGRecorded = maxPosG;

            float aoADiff = cosAoAAtMaxPosG - cosAoAAtMaxNegG;

            //if (Math.Abs(pitchControlDiff) < 0.005f)
            //    return;                 //if the pitch control values are too similar, don't bother to avoid numerical errors

            gaoASlopePerDynPres = (maxPosG - maxNegG) / aoADiff;
            gOffsetPerDynPres = maxPosG - gaoASlopePerDynPres * cosAoAAtMaxPosG;     //g force offset
        }

        void AdjustPitchForGAndAoALimits(FlightCtrlState s)
        {
            float minCosAoA, maxCosAoA;
            //debugString += "\nMax Pos G: " + maxPosG + " @ " + cosAoAAtMaxPosG;
            //debugString += "\nMax Neg G: " + maxNegG + " @ " + cosAoAAtMaxNegG;

            if (vessel.LandedOrSplashed || vessel.srfSpeed < Math.Min(minSpeed, takeOffSpeed))         //if we're going too slow, don't use this
            {
                float speed = Math.Max(takeOffSpeed, minSpeed);
                negPitchDynPresLimitIntegrator = -1f * 0.001f * 0.5f * 1.225f * speed * speed;
                posPitchDynPresLimitIntegrator = 1f * 0.001f * 0.5f * 1.225f * speed * speed;
                return;
            }

            float invVesselDynPreskPa = 1f / (float)vessel.dynamicPressurekPa;

            maxCosAoA = maxAllowedGForce * 9.81f * invVesselDynPreskPa;
            minCosAoA = -maxCosAoA;

            maxCosAoA -= gOffsetPerDynPres;
            minCosAoA -= gOffsetPerDynPres;

            maxCosAoA /= gaoASlopePerDynPres;
            minCosAoA /= gaoASlopePerDynPres;

            if (maxCosAoA > maxAllowedCosAoA)
                maxCosAoA = maxAllowedCosAoA;

            if (minCosAoA < -maxAllowedCosAoA)
                minCosAoA = -maxAllowedCosAoA;

            float curCosAoA = Vector3.Dot(vessel.Velocity() / vessel.srfSpeed, vessel.ReferenceTransform.forward);

            float centerCosAoA = (minCosAoA + maxCosAoA) * 0.5f;
            float curCosAoACentered = curCosAoA - centerCosAoA;
            float cosAoADiff = 0.5f * Math.Abs(maxCosAoA - minCosAoA);
            float curCosAoANorm = curCosAoACentered / cosAoADiff;      //scaled so that from centerAoA to maxAoA is 1

            float negPitchScalar, posPitchScalar;
            negPitchScalar = negPitchDynPresLimitIntegrator * invVesselDynPreskPa - lastPitchInput;
            posPitchScalar = lastPitchInput - posPitchDynPresLimitIntegrator * invVesselDynPreskPa;

            //update pitch control limits as needed
            float negPitchDynPresLimit, posPitchDynPresLimit;
            negPitchDynPresLimit = posPitchDynPresLimit = 0;
            if (curCosAoANorm < -0.15f)// || Math.Abs(negPitchScalar) < 0.01f)
            {
                float cosAoAOffset = curCosAoANorm + 1;     //set max neg aoa to be 0
                float aoALimScalar = Math.Abs(curCosAoANorm);
                aoALimScalar *= aoALimScalar;
                aoALimScalar *= aoALimScalar;
                aoALimScalar *= aoALimScalar;
                if (aoALimScalar > 1)
                    aoALimScalar = 1;

                float pitchInputScalar = negPitchScalar;
                pitchInputScalar = 1 - Mathf.Clamp01(Math.Abs(pitchInputScalar));
                pitchInputScalar *= pitchInputScalar;
                pitchInputScalar *= pitchInputScalar;
                pitchInputScalar *= pitchInputScalar;
                if (pitchInputScalar < 0)
                    pitchInputScalar = 0;

                float deltaCosAoANorm = curCosAoA - lastCosAoA;
                deltaCosAoANorm /= cosAoADiff;

                debugString.Append($"Updating Neg Gs");
                debugString.Append(Environment.NewLine);
                negPitchDynPresLimitIntegrator -= 0.01f * Mathf.Clamp01(aoALimScalar + pitchInputScalar) * cosAoAOffset * (float)vessel.dynamicPressurekPa;
                negPitchDynPresLimitIntegrator -= 0.005f * deltaCosAoANorm * (float)vessel.dynamicPressurekPa;
                if (cosAoAOffset < 0)
                    negPitchDynPresLimit = -0.3f * cosAoAOffset;
            }
            if (curCosAoANorm > 0.15f)// || Math.Abs(posPitchScalar) < 0.01f)
            {
                float cosAoAOffset = curCosAoANorm - 1;     //set max pos aoa to be 0
                float aoALimScalar = Math.Abs(curCosAoANorm);
                aoALimScalar *= aoALimScalar;
                aoALimScalar *= aoALimScalar;
                aoALimScalar *= aoALimScalar;
                if (aoALimScalar > 1)
                    aoALimScalar = 1;

                float pitchInputScalar = posPitchScalar;
                pitchInputScalar = 1 - Mathf.Clamp01(Math.Abs(pitchInputScalar));
                pitchInputScalar *= pitchInputScalar;
                pitchInputScalar *= pitchInputScalar;
                pitchInputScalar *= pitchInputScalar;
                if (pitchInputScalar < 0)
                    pitchInputScalar = 0;

                float deltaCosAoANorm = curCosAoA - lastCosAoA;
                deltaCosAoANorm /= cosAoADiff;

                debugString.Append($"Updating Pos Gs");
                debugString.Append(Environment.NewLine);
                posPitchDynPresLimitIntegrator -= 0.01f * Mathf.Clamp01(aoALimScalar + pitchInputScalar) * cosAoAOffset * (float)vessel.dynamicPressurekPa;
                posPitchDynPresLimitIntegrator -= 0.005f * deltaCosAoANorm * (float)vessel.dynamicPressurekPa;
                if (cosAoAOffset > 0)
                    posPitchDynPresLimit = -0.3f * cosAoAOffset;
            }

            float currentG = -Vector3.Dot(vessel.acceleration, vessel.ReferenceTransform.forward);
            float negLim, posLim;
            negLim = negPitchDynPresLimitIntegrator * invVesselDynPreskPa + negPitchDynPresLimit;
            if (negLim > s.pitch)
            {
                if (currentG > -(maxAllowedGForce * 0.97f * 9.81f))
                {
                    negPitchDynPresLimitIntegrator -= (float)(0.15 * vessel.dynamicPressurekPa);        //jsut an override in case things break

                    maxNegG = currentG * invVesselDynPreskPa;
                    cosAoAAtMaxNegG = curCosAoA;

                    negPitchDynPresLimit = 0;

                    //maxPosG = 0;
                    //cosAoAAtMaxPosG = 0;
                }

                s.pitch = negLim;
                debugString.Append($"Limiting Neg Gs");
                debugString.Append(Environment.NewLine);
            }
            posLim = posPitchDynPresLimitIntegrator * invVesselDynPreskPa + posPitchDynPresLimit;
            if (posLim < s.pitch)
            {
                if (currentG < (maxAllowedGForce * 0.97f * 9.81f))
                {
                    posPitchDynPresLimitIntegrator += (float)(0.15 * vessel.dynamicPressurekPa);        //jsut an override in case things break

                    maxPosG = currentG * invVesselDynPreskPa;
                    cosAoAAtMaxPosG = curCosAoA;

                    posPitchDynPresLimit = 0;

                    //maxNegG = 0;
                    //cosAoAAtMaxNegG = 0;
                }

                s.pitch = posLim;
                debugString.Append($"Limiting Pos Gs");
                debugString.Append(Environment.NewLine);
            }

            lastPitchInput = s.pitch;
            lastCosAoA = curCosAoA;

            debugString.Append($"Neg Pitch Lim: {negLim}");
            debugString.Append(Environment.NewLine);
            debugString.Append($"Pos Pitch Lim: {posLim}");
            debugString.Append(Environment.NewLine);
        }

        void CalculateAccelerationAndTurningCircle()
        {
            maxLiftAcceleration = maxDynPresGRecorded;
            maxLiftAcceleration *= (float)vessel.dynamicPressurekPa;       //maximum acceleration from lift that the vehicle can provide

            maxLiftAcceleration = Math.Min(maxLiftAcceleration, maxAllowedGForce * 9.81f);       //limit it to whichever is smaller, what we can provide or what we can handle
            maxLiftAcceleration = maxAllowedGForce * 9.81f;

            if (maxLiftAcceleration > 0)
                turnRadius = (float)vessel.Velocity().sqrMagnitude / maxLiftAcceleration;     //radius that we can turn in assuming constant velocity, assuming simple circular motion
        }

        float MinAltitudeNeeded()         //min altitude adjusted for G limits; let's try _not_ to overcook dives and faceplant into the ground
        {
            //for a pure vertical dive, turnRadius will be the altitude that we need to turn.  However, for shallower dives we don't need that much.  Let's account for that.
            //actual altitude needed will be radius * (1 - cos(theta)), where theta is the angle of the arc from dive entry to the turning circle to the bottom
            //we can calculate that from the velocity vector mag dotted with the up vector

            float speedCorrection = (float)Math.Sqrt((float)vessel.srfSpeed) / (float)Math.Sqrt(evasiveSpeed);

            float diveAngleCorrection = -Vector3.Dot(vessel.Velocity() / vessel.srfSpeed, vessel.upAxis); //normalize the vector and dot it with upAxis
                                                                                                          //this gives us sin(theta)
            if (diveAngleCorrection > 0)         //we're headed downwards
            {
                diveAngleCorrection *= diveAngleCorrection;
                diveAngleCorrection = 1 - diveAngleCorrection;
                diveAngleCorrection = Math.Max(0f, diveAngleCorrection);    //remember to check to make sure numerical errors haven't crept in!  Can't have NaN showing up
                diveAngleCorrection = Mathf.Sqrt(diveAngleCorrection);      //convert sin(theta) to cos(theta)

                diveAngleCorrection = 1 - diveAngleCorrection;      //and convert to 1 - cos(theta)
            }
            else
            {
                diveAngleCorrection = 0;
            }

            return minAltitude + (cornerRadius * diveAngleCorrection * speedCorrection);
        }

        Vector3 DefaultAltPosition()
        {
            return (vessel.transform.position + (-(float)vessel.altitude * upDirection) + (defaultAltitude * upDirection));
        }

        Vector3 GetSurfacePosition(Vector3 position)
        {
            return position - ((float)FlightGlobals.getAltitudeAtPos(position) * upDirection);
        }

        Vector3 GetTerrainSurfacePosition(Vector3 position)
        {
            return position - (MissileGuidance.GetRaycastRadarAltitude(position) * upDirection);
        }

        Vector3 FlightPosition(Vector3 targetPosition, float minAlt)
        {
            Vector3 forwardDirection = vesselTransform.up;
            Vector3 targetDirection = (targetPosition - vesselTransform.position).normalized;

            float vertFactor = 0;
            vertFactor += (((float)vessel.srfSpeed / minSpeed) - 2f) * 0.3f;          //speeds greater than 2x minSpeed encourage going upwards; below encourages downwards
            vertFactor += (((targetPosition - vesselTransform.position).magnitude / 1000f) - 1f) * 0.3f;    //distances greater than 1000m encourage going upwards; closer encourages going downwards
            vertFactor -= Mathf.Clamp01(Vector3.Dot(vesselTransform.position - targetPosition, upDirection) / 1600f - 1f) * 0.5f;       //being higher than 1600m above a target encourages going downwards
            if (targetVessel)
                vertFactor += Vector3.Dot(targetVessel.Velocity() / targetVessel.srfSpeed, (targetVessel.ReferenceTransform.position - vesselTransform.position).normalized) * 0.3f;   //the target moving away from us encourages upward motion, moving towards us encourages downward motion
            else
                vertFactor += 0.4f;
            vertFactor -= weaponManager.underFire ? 0.5f : 0;   //being under fire encourages going downwards as well, to gain energy

            float alt = MissileGuidance.GetRadarAltitude(vessel);

            if (vertFactor > 2)
                vertFactor = 2;
            if (vertFactor < -2)
                vertFactor = -2;

            vertFactor += 0.15f * Mathf.Sin((float)vessel.missionTime * 0.25f);     //some randomness in there

            Vector3 projectedDirection = Vector3.ProjectOnPlane(forwardDirection, upDirection);
            Vector3 projectedTargetDirection = Vector3.ProjectOnPlane(targetDirection, upDirection);
            if (Vector3.Dot(targetDirection, forwardDirection) < 0)
            {
                if (Vector3.Angle(targetDirection, forwardDirection) > 165f)
                {
                    targetPosition = vesselTransform.position + (Quaternion.AngleAxis(Mathf.Sign(Mathf.Sin((float)vessel.missionTime / 4)) * 45, upDirection) * (projectedDirection.normalized * 200));
                    targetDirection = (targetPosition - vesselTransform.position).normalized;
                }

                targetPosition = vesselTransform.position + Vector3.Cross(Vector3.Cross(forwardDirection, targetDirection), forwardDirection).normalized * 200;
            }
            else if (steerMode != SteerModes.Aiming)
            {
                float distance = (targetPosition - vesselTransform.position).magnitude;
                if (vertFactor < 0)
                    distance = Math.Min(distance, Math.Abs((alt - minAlt) / vertFactor));

                targetPosition += upDirection * Math.Min(distance, 1000) * vertFactor * Mathf.Clamp01(0.7f - Math.Abs(Vector3.Dot(projectedTargetDirection, projectedDirection)));
            }

            if (MissileGuidance.GetRadarAltitude(vessel) > minAlt * 1.1f)
            {
                return targetPosition;
            }

            float pointRadarAlt = MissileGuidance.GetRaycastRadarAltitude(targetPosition);
            if (pointRadarAlt < minAlt)
            {
                float adjustment = (minAlt - pointRadarAlt);
                debugString.Append($"Target position is below minAlt. Adjusting by {adjustment}");
                debugString.Append(Environment.NewLine);
                return targetPosition + (adjustment * upDirection);
            }
            else
            {
                return targetPosition;
            }
        }

        public override bool IsValidFixedWeaponTarget(Vessel target)
        {
            if (!vessel) return false;
            // aircraft can aim at anything
            return true;
        }

        bool DetectCollision(Vector3 direction, out Vector3 badDirection)
        {
            badDirection = Vector3.zero;
            if (MissileGuidance.GetRadarAltitude(vessel) < 20) return false;

            direction = direction.normalized;
            int layerMask = 1 << 15;
            Ray ray = new Ray(vesselTransform.position + (50 * vesselTransform.up), direction);
            float distance = Mathf.Clamp((float)vessel.srfSpeed * 4f, 125f, 2500);
            RaycastHit hit;
            if (!Physics.SphereCast(ray, 10, out hit, distance, layerMask)) return false;
            Rigidbody otherRb = hit.collider.attachedRigidbody;
            if (otherRb)
            {
                if (!(Vector3.Dot(otherRb.velocity, vessel.Velocity()) < 0)) return false;
                badDirection = hit.point - ray.origin;
                return true;
            }
            badDirection = hit.point - ray.origin;
            return true;
        }

        void UpdateCommand(FlightCtrlState s)
        {
            if (command == PilotCommands.Follow && !commandLeader)
            {
                ReleaseCommand();
                return;
            }

            if (command == PilotCommands.Follow)
            {
                currentStatus = "Follow";
                UpdateFollowCommand(s);
            }
            else if (command == PilotCommands.FlyTo)
            {
                currentStatus = "Fly To";
                FlyOrbit(s, assignedPositionGeo, 2500, idleSpeed, ClockwiseOrbit);
            }
            else if (command == PilotCommands.Attack)
            {
                currentStatus = "Attack";
                FlyOrbit(s, assignedPositionGeo, 4500, maxSpeed, ClockwiseOrbit);
            }
        }

        void UpdateFollowCommand(FlightCtrlState s)
        {
            steerMode = SteerModes.NormalFlight;
            vessel.ActionGroups.SetGroup(KSPActionGroup.Brakes, false);

            commandSpeed = commandLeader.vessel.srfSpeed;
            commandHeading = commandLeader.vessel.Velocity().normalized;

            //formation position
            Vector3d commandPosition = GetFormationPosition();
            debugFollowPosition = commandPosition;

            float distanceToPos = Vector3.Distance(vesselTransform.position, commandPosition);

            float dotToPos = Vector3.Dot(vesselTransform.up, commandPosition - vesselTransform.position);
            Vector3 flyPos;
            useRollHint = false;

            float ctrlModeThresh = 1000;

            if (distanceToPos < ctrlModeThresh)
            {
                flyPos = commandPosition + (ctrlModeThresh * commandHeading);

                Vector3 vectorToFlyPos = flyPos - vessel.ReferenceTransform.position;
                Vector3 projectedPosOffset = Vector3.ProjectOnPlane(commandPosition - vessel.ReferenceTransform.position, commandHeading);
                float posOffsetMag = projectedPosOffset.magnitude;
                float adjustAngle = (Mathf.Clamp(posOffsetMag * 0.27f, 0, 25));
                Vector3 projVel = Vector3.Project(vessel.Velocity() - commandLeader.vessel.Velocity(), projectedPosOffset);
                adjustAngle -= Mathf.Clamp(Mathf.Sign(Vector3.Dot(projVel, projectedPosOffset)) * projVel.magnitude * 0.12f, -10, 10);

                adjustAngle *= Mathf.Deg2Rad;

                vectorToFlyPos = Vector3.RotateTowards(vectorToFlyPos, projectedPosOffset, adjustAngle, 0);

                flyPos = vessel.ReferenceTransform.position + vectorToFlyPos;

                if (distanceToPos < 400)
                {
                    steerMode = SteerModes.Aiming;
                }
                else
                {
                    steerMode = SteerModes.NormalFlight;
                }

                if (distanceToPos < 10)
                {
                    useRollHint = true;
                }
            }
            else
            {
                steerMode = SteerModes.NormalFlight;
                flyPos = commandPosition;
            }

            double finalMaxSpeed = commandSpeed;
            if (dotToPos > 0)
            {
                finalMaxSpeed += (distanceToPos / 8);
            }
            else
            {
                finalMaxSpeed -= (distanceToPos / 2);
            }

            AdjustThrottle((float)finalMaxSpeed, true);

            FlyToPosition(s, flyPos);
        }

        Vector3d GetFormationPosition()
        {
            Quaternion origVRot = velocityTransform.rotation;
            Vector3 origVLPos = velocityTransform.localPosition;

            velocityTransform.position = commandLeader.vessel.ReferenceTransform.position;
            if (commandLeader.vessel.Velocity() != Vector3d.zero)
            {
                velocityTransform.rotation = Quaternion.LookRotation(commandLeader.vessel.Velocity(), upDirection);
                velocityTransform.rotation = Quaternion.AngleAxis(90, velocityTransform.right) * velocityTransform.rotation;
            }
            else
            {
                velocityTransform.rotation = commandLeader.vessel.ReferenceTransform.rotation;
            }

            Vector3d pos = velocityTransform.TransformPoint(this.GetLocalFormationPosition(commandFollowIndex));// - lateralVelVector - verticalVelVector;

            velocityTransform.localPosition = origVLPos;
            velocityTransform.rotation = origVRot;

            return pos;
        }

        public override void CommandTakeOff()
        {
            base.CommandTakeOff();
            standbyMode = false;
        }

        protected override void OnGUI()
        {
            base.OnGUI();

            if (!pilotEnabled || !vessel.isActiveVessel) return;

            if (!BDArmorySettings.DRAW_DEBUG_LINES) return;
            if (command == PilotCommands.Follow)
            {
                BDGUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, debugFollowPosition, 2, Color.red);
            }

            BDGUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, debugPos, 5, Color.red);
            BDGUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, vesselTransform.position + vesselTransform.up * 5000, 3, Color.white);

            BDGUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, vesselTransform.position + rollTarget, 2, Color.blue);
            BDGUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position + (0.05f * vesselTransform.right), vesselTransform.position + (0.05f * vesselTransform.right) + angVelRollTarget, 2, Color.green);
        }
    }
}
