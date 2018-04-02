using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BDArmory.Misc;
using BDArmory.UI;
using BDArmory.Core;
using UnityEngine;

namespace BDArmory.Control
{
	public class BDModuleSurfaceAI : BDGenericAIBase, IBDAIControl
	{
		#region Declarations
		
		Vessel extendingTarget = null;
        Vessel bypassTarget = null;
        Vector3 bypassTargetPos;

		Vector3 targetDirection;
		float targetVelocity; // the velocity the ship should target, not the velocity of its target
        bool aimingMode = false;

		int collisionDetectionTicker = 0;
		Vector3? dodgeVector;
		float weaveAdjustment = 0;
		float weaveDirection = 1;
		const float weaveLimit = 15;
		const float weaveFactor = 6.5f;
        int sideSlipDirection = 0;

		Vector3 upDir;

        AIUtils.TraversabilityMatrix pathingMatrix;
        List<Vector3> waypoints = new List<Vector3>();
        bool leftPath = false;

        protected override Vector3d assignedPositionGeo
        {
            get { return intermediatePositionGeo; }
            set
            {
                finalPositionGeo = value;
                leftPath = true;
            }
        }
        Vector3d finalPositionGeo;
        Vector3d intermediatePositionGeo;
        public override Vector3d commandGPS => finalPositionGeo;

        private BDLandSpeedControl motorControl;

        //settings
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Vehicle type"),
            UI_ChooseOption(options = new string[3] { "Land", "Amphibious", "Water" })]
        public string SurfaceTypeName = "Land";
        public AIUtils.VehicleMovementType SurfaceType 
            => (AIUtils.VehicleMovementType)Enum.Parse(typeof(AIUtils.VehicleMovementType), SurfaceTypeName);

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Max slope angle"),
            UI_FloatRange(minValue = 1f, maxValue = 30f, stepIncrement = 1f, scene = UI_Scene.All)]
        public float MaxSlopeAngle = 10f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Cruise speed"),
			UI_FloatRange(minValue = 5f, maxValue = 60f, stepIncrement = 1f, scene = UI_Scene.All)]
		public float CruiseSpeed = 20;

		[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Max speed"),
			UI_FloatRange(minValue = 5f, maxValue = 80f, stepIncrement = 1f, scene = UI_Scene.All)]
		public float MaxSpeed = 30;

		[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Max drift"),
			UI_FloatRange(minValue = 1f, maxValue = 180f, stepIncrement = 1f, scene = UI_Scene.All)]
		public float MaxDrift = 10;

		[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Moving pitch"),
			UI_FloatRange(minValue = -10f, maxValue = 10f, stepIncrement = .1f, scene = UI_Scene.All)]
		public float TargetPitch = 0;

		[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Bank angle"),
			UI_FloatRange(minValue = -45f, maxValue = 45f, stepIncrement = 1f, scene = UI_Scene.All)]
		public float BankAngle = 0;

		[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Steer Factor"),
			UI_FloatRange(minValue = 0.2f, maxValue = 20f, stepIncrement = .1f, scene = UI_Scene.All)]
		public float steerMult = 6;

		[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Steer Damping"),
			UI_FloatRange(minValue = 0.1f, maxValue = 10f, stepIncrement = .1f, scene = UI_Scene.All)]
		public float steerDamping = 3;

		//[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Steering"),
		//	UI_Toggle(enabledText = "Powered", disabledText = "Passive")]
		public bool PoweredSteering = true;

		[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Attack vector"),
			UI_Toggle(enabledText = "Broadside", disabledText = "Bow")]
		public bool BroadsideAttack = false;

		[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Min engagement range"),
			UI_FloatRange(minValue = 0f, maxValue = 6000f, stepIncrement = 100f, scene = UI_Scene.All)]
		public float MinEngagementRange = 500;

		[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Max engagement range"),
			UI_FloatRange(minValue = 500f, maxValue = 8000f, stepIncrement = 100f, scene = UI_Scene.All)]
		public float MaxEngagementRange = 4000;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "RCS active"),
            UI_Toggle(enabledText = "Maneuvers", disabledText = "Combat", scene = UI_Scene.All),]
        public bool ManeuverRCS = false;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Min obstacle mass", advancedTweakable = true),
            UI_FloatRange(minValue = 0f, maxValue = 100f, stepIncrement = 1f, scene = UI_Scene.All),]
        public float AvoidMass = 0f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Goes up to ", advancedTweakable = true),
            UI_Toggle(enabledText = "eleven", disabledText = "ten", scene = UI_Scene.All), ]
        public bool UpToEleven = false;
        bool toEleven = false;

        const float AttackAngleAtMaxRange = 30f;

        Dictionary<string, float> altMaxValues = new Dictionary<string, float>
        {
            { nameof(MaxSlopeAngle), 90f },
            { nameof(CruiseSpeed), 300f },
            { nameof(MaxSpeed), 400f },
            { nameof(steerMult), 200f },
            { nameof(steerDamping), 100f },
            { nameof(MinEngagementRange), 20000f },
            { nameof(MaxEngagementRange), 30000f },
            { nameof(AvoidMass), 1000000f },
        };
		#endregion

		#region RMB info in editor
		public override string GetInfo()
		{
            // known bug - the game caches the RMB info, changing the variable after checking the info
            // does not update the info. :( No idea how to force an update.
			return @"
<b>Available settings</b>:
<b>Vehicle type</b> - can this vessel operate on land/sea/both
<b>Max slope angle</b> - what is the steepest slope this vessel can negotiate
<b>Cruise speed</b> - the default speed at which it is safe to maneuver
<b>Max speed</b> - the maximum combat speed
<b>Max drift</b> - maximum allowed angle between facing and velocity vector
<b>Moving pitch</b> - the pitch level to maintain when moving at cruise speed
<b>Bank angle</b> - the limit on roll when turning, positive rolls into turns
<b>Steer Factor</b> - higher will make the AI apply more control input for the same desired rotation
<b>Bank angle</b> - higher will make the AI apply more control input when it wants to stop rotation
<b>Attack vector</b> - does the vessel attack from the front or the sides
<b>Min engagement range</b> - AI will try to move away from oponents if closer than this range
<b>Max engagement range</b> - AI will prioritize getting closer over attacking when beyond this range
<b>RCS active</b> - Use RCS during any maneuvers, or only in combat
" + (GameSettings.ADVANCED_TWEAKABLES ?
@"<b>Min obstacle mass</b> - Obstacles of a lower mass than this will be ignored instead of avoided
<b> Goes up to</b> - Increases variable limits, no direct effect on behaviour
" : "");
		}
		#endregion

		#region events

		public override void ActivatePilot()
		{
			base.ActivatePilot();

			pathingMatrix = new AIUtils.TraversabilityMatrix();

            if (!motorControl)
            {
                motorControl = gameObject.AddComponent<BDLandSpeedControl>();
                motorControl.vessel = vessel;
            }
            motorControl.Activate();

            if (BroadsideAttack)
                sideSlipDirection = UnityEngine.Random.Range(0, 2) > 1 ? 1 : -1;

            leftPath = true;
            extendingTarget = null;
            bypassTarget = null;
            collisionDetectionTicker = 6;
        }

        public override void DeactivatePilot()
        {
            base.DeactivatePilot();

            if (motorControl)
                motorControl.Deactivate();
        }

        void Update()
        {
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
                        StartCoroutine(setVar(s.Current, (float)typeof(BDModuleSurfaceAI).GetField(s.Current).GetValue(this)));
                    }
                toEleven = UpToEleven;
            }
        }

        IEnumerator setVar(string name, float value)
        {
            yield return new WaitForFixedUpdate();
            typeof(BDModuleSurfaceAI).GetField(name).SetValue(this, value);
        }

        protected override void OnGUI()
		{
			base.OnGUI();

			if (!pilotEnabled || !vessel.isActiveVessel) return;

			if (!BDArmorySettings.DRAW_DEBUG_LINES) return;
			if (command == PilotCommands.Follow)
			{
				BDGUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, assignedPositionWorld, 2, Color.red);
			}

			BDGUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, vesselTransform.position + targetDirection * 10f, 2, Color.blue);
			BDGUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position + (0.05f * vesselTransform.right), vesselTransform.position + (0.05f * vesselTransform.right), 2, Color.green);

			pathingMatrix.DrawDebug(vessel.CoM, waypoints);
		}

		#endregion

		#region Actual AI Pilot

		protected override void AutoPilot(FlightCtrlState s)
		{
			if (!vessel.Autopilot.Enabled)
				vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, true);

			targetVelocity = 0;
			targetDirection = vesselTransform.up;
            aimingMode = false;
			upDir = VectorUtils.GetUpDirection(vesselTransform.position);
            DebugLine("");

			// check if we should be panicking
			if (!PanicModes())
			{
				// pilot logic figures out what we're supposed to be doing, and sets the base state
				PilotLogic();
				// situational awareness modifies the base as best as it can (evasive mainly)
				Tactical();
			}

			AttitudeControl(s); // move according to our targets
			AdjustThrottle(targetVelocity); // set throttle according to our targets and movement
		}

        void PilotLogic()
        {
            // check for collisions, but not every frame
            if (collisionDetectionTicker == 0)
            {
                collisionDetectionTicker = 20;
                float predictMult = Mathf.Clamp(10 / MaxDrift, 1, 10);

                dodgeVector = null;

                using (var vs = BDATargetManager.LoadedVessels.GetEnumerator())
                    while (vs.MoveNext())
                    {
                        if (vs.Current == null || vs.Current == vessel) continue;
                        if (!vs.Current.LandedOrSplashed || vs.Current.FindPartModuleImplementing<IBDAIControl>()?.commandLeader?.vessel == vessel
                            || vs.Current.GetTotalMass() < AvoidMass)
                            continue;
                        dodgeVector = PredictCollisionWithVessel(vs.Current, 5f * predictMult, 0.5f);
                        if (dodgeVector != null) break;
                    }
            }
            else
                collisionDetectionTicker--;

            // avoid collisions if any are found
            if (dodgeVector != null)
            {
                targetVelocity = PoweredSteering ? MaxSpeed : CruiseSpeed;
                targetDirection = (Vector3)dodgeVector;
                SetStatus($"Avoiding Collision");
                leftPath = true;
                return;
            }

            // if bypass target is no longer relevant, remove it
            if (bypassTarget != null && ((bypassTarget != targetVessel && bypassTarget != commandLeader?.vessel) 
                || (VectorUtils.GetWorldSurfacePostion(bypassTargetPos, vessel.mainBody) - bypassTarget.CoM).sqrMagnitude > 500000))
            {
                bypassTarget = null;
            }

            if (bypassTarget == null)
            {
                // check for enemy targets and engage
                // not checking for guard mode, because if guard mode is off now you can select a target manually and if it is of opposing team, the AI will try to engage while you can man the turrets
                if (weaponManager && targetVessel != null && !BDArmorySettings.PEACE_MODE)
                {
                    leftPath = true;
                    if (collisionDetectionTicker == 5)
                        checkBypass(targetVessel);

                    Vector3 vecToTarget = targetVessel.CoM - vessel.CoM;
                    float distance = vecToTarget.magnitude;
                    // lead the target a bit, where 1km/s is a ballpark estimate of the average bullet velocity
                    float shotSpeed = 1000f;
                    if (weaponManager?.selectedWeapon is ModuleWeapon wep)
                        shotSpeed = wep.bulletVelocity;
                    vecToTarget = targetVessel.PredictPosition(distance / shotSpeed) - vessel.CoM;

                    if (BroadsideAttack)
                    {
                        Vector3 sideVector = Vector3.Cross(vecToTarget, upDir); //find a vector perpendicular to direction to target
                        if (collisionDetectionTicker == 10 
                                && !pathingMatrix.TraversableStraightLine(
                                        VectorUtils.WorldPositionToGeoCoords(vessel.CoM, vessel.mainBody),
                                        VectorUtils.WorldPositionToGeoCoords(vessel.PredictPosition(10), vessel.mainBody),
                                        vessel.mainBody, SurfaceType, MaxSlopeAngle, AvoidMass))
                            sideSlipDirection = -Math.Sign(Vector3.Dot(vesselTransform.up, sideVector)); // switch sides if we're running ashore
                        sideVector *= sideSlipDirection;

                        float sidestep = distance >= MaxEngagementRange ? Mathf.Clamp01((MaxEngagementRange - distance) / (CruiseSpeed * Mathf.Clamp(90 / MaxDrift, 0, 10)) + 1) * AttackAngleAtMaxRange / 90 : // direct to target to attackAngle degrees if over maxrange
                            (distance <= MinEngagementRange ? 1.5f - distance / (MinEngagementRange * 2) : // 90 to 135 degrees if closer than minrange
                            (MaxEngagementRange - distance) / (MaxEngagementRange - MinEngagementRange) * (1 - AttackAngleAtMaxRange / 90) + AttackAngleAtMaxRange / 90); // attackAngle to 90 degrees from maxrange to minrange 
                        targetDirection = Vector3.LerpUnclamped(vecToTarget.normalized, sideVector.normalized, sidestep); // interpolate between the side vector and target direction vector based on sidestep
                        targetVelocity = MaxSpeed;
                        DebugLine($"Broadside attack angle {sidestep}");
                    }
                    else // just point at target and go
                    {
                        if ((targetVessel.horizontalSrfSpeed < 10 || Vector3.Dot(Vector3.ProjectOnPlane(targetVessel.srf_vel_direction, upDir), vessel.up) < 0) //if target is stationary or we're facing in opposite directions
                            && (distance < MinEngagementRange || (distance < (MinEngagementRange * 3 + MaxEngagementRange) / 4 //and too close together
                            && extendingTarget != null && targetVessel != null && extendingTarget == targetVessel)))
                        {
                            extendingTarget = targetVessel;
                            // not sure if this part is very smart, potential for improvement
                            targetDirection = -vecToTarget; //extend
                            targetVelocity = MaxSpeed;
                            SetStatus($"Extending");
                            return;
                        }
                        else
                        {
                            extendingTarget = null;
                            targetDirection = Vector3.ProjectOnPlane(vecToTarget, upDir);
                            if (Vector3.Dot(targetDirection, vesselTransform.up) < 0)
                                targetVelocity = PoweredSteering ? MaxSpeed : 0; // if facing away from target
                            else if (distance >= MaxEngagementRange || distance <= MinEngagementRange)
                                targetVelocity = MaxSpeed;
                            else
                            {
                                targetVelocity = CruiseSpeed / 10 + (MaxSpeed - CruiseSpeed / 10) * (distance - MinEngagementRange) / (MaxEngagementRange - MinEngagementRange); //slow down if inside engagement range to extend shooting opportunities
                                switch (weaponManager?.selectedWeapon?.GetWeaponClass())
                                {
                                    case WeaponClasses.Gun:
                                    case WeaponClasses.DefenseLaser:
                                        var gun = (ModuleWeapon)weaponManager.selectedWeapon;
                                        if ((gun.yawRange == 0 || gun.maxPitch == gun.minPitch) && gun.FiringSolutionVector != null)
                                        {
                                            aimingMode = true;
                                            if (Vector3.Angle((Vector3)gun.FiringSolutionVector, vessel.transform.up) < 20)
                                                targetDirection = (Vector3)gun.FiringSolutionVector;
                                        }
                                        break;
                                    case WeaponClasses.Rocket:
                                        var rocket = (RocketLauncher)weaponManager.selectedWeapon;
                                        if (rocket.yawRange == 0 || rocket.maxPitch == rocket.minPitch)
                                        {
                                            aimingMode = true;
                                            if (Vector3.Angle((Vector3)rocket.FiringSolutionVector, vessel.transform.up) < 20)
                                                targetDirection = (Vector3)rocket.FiringSolutionVector;
                                        }
                                        break;
                                }
                            }
                            targetVelocity = Mathf.Clamp(targetVelocity, PoweredSteering ? CruiseSpeed / 5 : 0, MaxSpeed); // maintain a bit of speed if using powered steering
                        }
                    }
                    SetStatus($"Engaging target");
                    return;
                }

                // follow
                if (command == PilotCommands.Follow)
                {
                    leftPath = true;
                    if (collisionDetectionTicker == 5)
                        checkBypass(commandLeader.vessel);

                    Vector3 targetPosition = GetFormationPosition();
                    Vector3 targetDistance = targetPosition - vesselTransform.position;
                    if (Vector3.Dot(targetDistance, vesselTransform.up) < 0
                        && Vector3.ProjectOnPlane(targetDistance, upDir).sqrMagnitude < 250f * 250f
                        && Vector3.Angle(vesselTransform.up, commandLeader.vessel.srf_velocity) < 0.8f)
                    {
                        targetDirection = Vector3.RotateTowards(Vector3.ProjectOnPlane(commandLeader.vessel.srf_vel_direction, upDir), targetDistance, 0.2f, 0);
                    }
                    else
                    {
                        targetDirection = Vector3.ProjectOnPlane(targetDistance, upDir);
                    }
                    targetVelocity = (float)(commandLeader.vessel.horizontalSrfSpeed + (vesselTransform.position - targetPosition).magnitude / 15);
                    if (Vector3.Dot(targetDirection, vesselTransform.up) < 0 && !PoweredSteering) targetVelocity = 0;
                    SetStatus($"Following");
                    return;
                }
            }

			// goto
            if (leftPath && bypassTarget == null)
            {
                Pathfind(finalPositionGeo);
                leftPath = false;
            }

			const float targetRadius = 250f;
			targetDirection = Vector3.ProjectOnPlane(assignedPositionWorld - vesselTransform.position, upDir);

            if (targetDirection.sqrMagnitude > targetRadius * targetRadius)
			{
                if (bypassTarget != null)
                    targetVelocity = MaxSpeed;
                else if (waypoints.Count > 1)
                    targetVelocity = command == PilotCommands.Attack ? MaxSpeed : CruiseSpeed;
                else
                    targetVelocity = Mathf.Clamp((targetDirection.magnitude - targetRadius / 2) / 5f, 
                    0, command == PilotCommands.Attack ? MaxSpeed : CruiseSpeed);

				if (Vector3.Dot(targetDirection, vesselTransform.up) < 0 && !PoweredSteering) targetVelocity = 0;
				SetStatus(bypassTarget ? "Repositioning" : "Moving");
				return;
			}

            cycleWaypoint();

            SetStatus($"Not doing anything in particular");
			targetDirection = vesselTransform.up;
		}

		void Tactical()
		{
			// enable RCS if we're in combat
			vessel.ActionGroups.SetGroup(KSPActionGroup.RCS, weaponManager && targetVessel && !BDArmorySettings.PEACE_MODE
				&& (weaponManager.selectedWeapon != null || (vessel.CoM - targetVessel.CoM).sqrMagnitude < MaxEngagementRange * MaxEngagementRange) 
                || weaponManager.underFire || weaponManager.missileIsIncoming);

			// if weaponManager thinks we're under fire, do the evasive dance
			if (weaponManager.underFire || weaponManager.missileIsIncoming)
			{
				targetVelocity = MaxSpeed;
				if (weaponManager.underFire || weaponManager.incomingMissileDistance < 2500)
				{
					if (Mathf.Abs(weaveAdjustment) + Time.deltaTime * weaveFactor > weaveLimit) weaveDirection *= -1;
					weaveAdjustment += weaveFactor * weaveDirection * Time.deltaTime;
				}
				else
				{
					weaveAdjustment = 0;
				}
			}
			else
			{
				weaveAdjustment = 0;
			}
			DebugLine($"underFire {weaponManager.underFire}, weaveAdjustment {weaveAdjustment}");
		}

		bool PanicModes()
		{
			if (!vessel.LandedOrSplashed)
			{
				targetVelocity = 0;
				targetDirection = Vector3.ProjectOnPlane(vessel.srf_velocity, upDir);
				SetStatus("Airtime!");
				return true;
			}
			else if (vessel.Landed 
                && !vessel.Splashed // I'm looking at you, Kerbal Konstructs. (When launching directly into water, KK seems to set both vessel.Landed and vessel.Splashed to true.)
                && (SurfaceType & AIUtils.VehicleMovementType.Land) == 0)
			{
				targetVelocity = 0;
				SetStatus("Stranded");
				return true;
			}
            else if (vessel.Splashed && (SurfaceType & AIUtils.VehicleMovementType.Water) == 0)
            {
                targetVelocity = 0;
                SetStatus("Floating");
                return true;
            }
            return false;
		}

		void AdjustThrottle(float targetSpeed)
		{
			targetVelocity = Mathf.Clamp(targetVelocity, 0, MaxSpeed);

			if (float.IsNaN(targetSpeed)) //because yeah, I might have left division by zero in there somewhere
			{
				targetSpeed = CruiseSpeed;
				DebugLine("Target velocity NaN, set to CruiseSpeed.");
			}
			else
				DebugLine($"Target velocity: {targetVelocity}");
            DebugLine($"engine thrust: {speedController.debugThrust}, motor zero: {motorControl.zeroPoint}");

            speedController.targetSpeed = motorControl.targetSpeed = targetSpeed;
            speedController.useBrakes = motorControl.preventNegativeZeroPoint = speedController.debugThrust > 0;
        }

		void AttitudeControl(FlightCtrlState s)
		{
            const float terrainOffset = 5;

			Vector3 yawTarget = Vector3.ProjectOnPlane(targetDirection, vesselTransform.forward);

            // limit "aoa" if we're moving
            float driftMult = 1;
            if (vessel.horizontalSrfSpeed * 10 > CruiseSpeed)
            {
                driftMult = Mathf.Max(Vector3.Angle(vessel.srf_velocity, yawTarget) / MaxDrift, 1);
                yawTarget = Vector3.RotateTowards(vessel.srf_velocity, yawTarget, MaxDrift * Mathf.Deg2Rad, 0);
            }

            float yawError = VectorUtils.SignedAngle(vesselTransform.up, yawTarget, vesselTransform.right) + (aimingMode ? 0 : weaveAdjustment);
            DebugLine($"yaw target: {yawTarget}, yaw error: {yawError}");
            DebugLine($"drift multiplier: {driftMult}");

            Vector3 baseForward = vessel.transform.up * terrainOffset;
            float basePitch = Mathf.Atan2(
                AIUtils.GetTerrainAltitude(vessel.CoM + baseForward, vessel.mainBody, false)
                - AIUtils.GetTerrainAltitude(vessel.CoM - baseForward, vessel.mainBody, false),
                terrainOffset * 2) * Mathf.Rad2Deg;
            float pitchAngle = basePitch + TargetPitch * Mathf.Clamp01((float)vessel.horizontalSrfSpeed / CruiseSpeed);
            if (aimingMode)
                pitchAngle = VectorUtils.SignedAngle(vesselTransform.up, Vector3.ProjectOnPlane(targetDirection, vesselTransform.right), -vesselTransform.forward);
            DebugLine($"terrain fw slope: {basePitch}, target pitch: {pitchAngle}");


			float pitch = 90 - Vector3.Angle(vesselTransform.up, upDir);
			float pitchError = pitchAngle - pitch;

            Vector3 baseLateral = vessel.transform.right * terrainOffset;
            float baseRoll = Mathf.Atan2(
                AIUtils.GetTerrainAltitude(vessel.CoM + baseLateral, vessel.mainBody, false)
                - AIUtils.GetTerrainAltitude(vessel.CoM - baseLateral, vessel.mainBody, false),
                terrainOffset * 2) * Mathf.Rad2Deg;
            float drift = VectorUtils.SignedAngle(vesselTransform.up, Vector3.ProjectOnPlane(vessel.GetSrfVelocity(), upDir), vesselTransform.right);
            float bank = VectorUtils.SignedAngle(-vesselTransform.forward, upDir, -vesselTransform.right);
			float targetRoll = baseRoll + BankAngle * Mathf.Clamp01(drift / MaxDrift) * Mathf.Clamp01((float)vessel.srfSpeed / CruiseSpeed);
			float rollError = targetRoll - bank;
            DebugLine($"terrain sideways slope: {baseRoll}, target roll: {targetRoll}");

            Vector3 localAngVel = vessel.angularVelocity;
			s.roll = steerMult * 0.006f * rollError - 0.4f * steerDamping * -localAngVel.y;
            s.pitch = ((aimingMode ? 0.02f : 0.015f) * steerMult * pitchError) - (steerDamping * -localAngVel.x);
			s.yaw = (((aimingMode ? 0.007f : 0.005f) * steerMult * yawError) - (steerDamping * 0.2f * -localAngVel.z)) * driftMult;
            s.wheelSteer = -(((aimingMode? 0.005f : 0.003f) * steerMult * yawError) - (steerDamping * 0.1f * -localAngVel.z));

            if (ManeuverRCS && (Mathf.Abs(s.roll) >= 1 || Mathf.Abs(s.pitch) >= 1 || Mathf.Abs(s.yaw) >= 1))
                vessel.ActionGroups.SetGroup(KSPActionGroup.RCS, true);
        }

		#endregion

		#region Autopilot helper functions

		public override bool CanEngage()
		{
            if (vessel.Splashed && (SurfaceType & AIUtils.VehicleMovementType.Water) == 0)
                DebugLine(vessel.vesselName + " cannot engage: boat not in water");
            else if (vessel.Landed && (SurfaceType & AIUtils.VehicleMovementType.Land) == 0)
                DebugLine(vessel.vesselName + " cannot engage: vehicle not on land");
            else if (!vessel.LandedOrSplashed)
                DebugLine(vessel.vesselName + " cannot engage: vessel not on surface");
            // the motorControl part fails sometimes, and guard mode then decides not to select a weapon
            // figure out what is wrong with motor control before uncommenting :D
            //else if (speedController.debugThrust + (motorControl?.MaxAccel ?? 0) <= 0)
            //    DebugLine(vessel.vesselName + " cannot engage: no engine power");
            else
                return true;
            return false;
		}

		public override bool IsValidFixedWeaponTarget(Vessel target) 
            => !BroadsideAttack &&
            (((target?.Splashed ?? false) && (SurfaceType & AIUtils.VehicleMovementType.Water) != 0) 
            || ((target?.Landed ?? false) && (SurfaceType & AIUtils.VehicleMovementType.Land) != 0))
            ; //valid if can traverse the same medium and using bow fire

		/// <returns>null if no collision, dodge vector if one detected</returns>
		Vector3? PredictCollisionWithVessel(Vessel v, float maxTime, float interval)
		{
			//evasive will handle avoiding missiles
			if (v == weaponManager.incomingMissileVessel 
                || v.rootPart.FindModuleImplementing<Parts.MissileBase>() != null)
                return null;

			float time = Mathf.Min(0.5f, maxTime);
			while (time < maxTime)
			{
				Vector3 tPos = v.PredictPosition(time);
				Vector3 myPos = vessel.PredictPosition(time);
				if (Vector3.SqrMagnitude(tPos - myPos) < 2500f)
				{
					return Vector3.Dot(tPos - myPos, vesselTransform.right) > 0 ? -vesselTransform.right : vesselTransform.right;
				}

				time = Mathf.MoveTowards(time, maxTime, interval);
			}

			return null;
		}

        void checkBypass(Vessel target)
        {
            if(!pathingMatrix.TraversableStraightLine(
                    VectorUtils.WorldPositionToGeoCoords(vessel.CoM, vessel.mainBody),
                    VectorUtils.WorldPositionToGeoCoords(target.CoM, vessel.mainBody),
                    vessel.mainBody, SurfaceType, MaxSlopeAngle, AvoidMass))
            {
                bypassTarget = target;
                bypassTargetPos = VectorUtils.WorldPositionToGeoCoords(target.CoM, vessel.mainBody);
                waypoints = pathingMatrix.Pathfind(
                    VectorUtils.WorldPositionToGeoCoords(vessel.CoM, vessel.mainBody),
                    VectorUtils.WorldPositionToGeoCoords(target.CoM, vessel.mainBody),
                    vessel.mainBody, SurfaceType, MaxSlopeAngle, AvoidMass);
                if (VectorUtils.GeoDistance(waypoints[waypoints.Count - 1], bypassTargetPos, vessel.mainBody) < 200)
                    waypoints.RemoveAt(waypoints.Count - 1);
                if (waypoints.Count > 0)
                    intermediatePositionGeo = waypoints[0];
                else
                    bypassTarget = null;
            }
        }
        
        private void Pathfind(Vector3 destination)
        {
            waypoints = pathingMatrix.Pathfind(
                                    VectorUtils.WorldPositionToGeoCoords(vessel.CoM, vessel.mainBody),
                                    destination, vessel.mainBody, SurfaceType, MaxSlopeAngle, AvoidMass);
            intermediatePositionGeo = waypoints[0];
        }

        void cycleWaypoint()
        {
            if (waypoints.Count > 1)
            {
                waypoints.RemoveAt(0);
                intermediatePositionGeo = waypoints[0];
            }
            else if (bypassTarget != null)
            {
                waypoints.Clear();
                bypassTarget = null;
                leftPath = true;
            }
        }

		#endregion

		#region WingCommander

		Vector3 GetFormationPosition()
		{
			return commandLeader.vessel.CoM + Quaternion.LookRotation(commandLeader.vessel.up, upDir) * this.GetLocalFormationPosition(commandFollowIndex);
		}
		#endregion
	}
}
