using System;
using BDArmory.Misc;
using BDArmory.UI;
using BDArmory.Core;
using UnityEngine;
using System.Text;

namespace BDArmory.Control
{
	/// <summary>
	/// A base class for implementing AI.
	/// Note: You do not have to use it, it is just for convenience, all the game cares about is that you implement the IBDAIControl interface.
	/// </summary>
	public abstract class BDGenericAIBase : PartModule, IBDAIControl, IBDWMModule
	{
		#region declarations
		public bool pilotEnabled => pilotOn;
        // separate private field for pilot On, because properties cannot be KSPFields
        [KSPField(isPersistant = true)]
        public bool pilotOn;
        protected Vessel activeVessel;

		public MissileFire weaponManager { get; protected set; }

		/// <summary>
		/// The default is BDAirspeedControl. If you want to use something else, just override ActivatePilot  (and, potentially, DeactivatePilot), and make it use something else.
		/// </summary>
		protected BDAirspeedControl speedController;

		protected Transform vesselTransform => vessel.ReferenceTransform;

		protected StringBuilder debugString = new StringBuilder(); 

		protected Vessel targetVessel;

		protected virtual Vector3d assignedPositionGeo { get; set; }

		public Vector3d assignedPositionWorld
		{
			get
			{
				return VectorUtils.GetWorldSurfacePostion(assignedPositionGeo, vessel.mainBody);
			}
			protected set
			{
				assignedPositionGeo = VectorUtils.WorldPositionToGeoCoords(value, vessel.mainBody);
			}
		}

		//wing commander
		public ModuleWingCommander commandLeader { get; protected set; }
		protected PilotCommands command;
		public string currentStatus { get; protected set; } = "Free";
		protected int commandFollowIndex;

		public PilotCommands currentCommand => command;
		public virtual Vector3d commandGPS => assignedPositionGeo;

		#endregion

		public abstract bool CanEngage();
		public abstract bool IsValidFixedWeaponTarget(Vessel target);

		/// <summary>
		/// This will be called every update and should run the autopilot logic.
		/// 
		/// For simple use cases:
		///		1. Engage your target (get in position to engage, shooting is done by guard mode)
		///		2. If no target, check command, and follow it
		///		Do this by setting s.pitch, s.yaw and s.roll.
		///		
		/// For advanced use cases you probably know what you're doing :P
		/// </summary>
		/// <param name="s">current flight control state</param>
		protected abstract void AutoPilot(FlightCtrlState s);

		// A small wrapper to make sure the autopilot does not do anything when it shouldn't
		private void autoPilot(FlightCtrlState s)
		{
			if (!vessel || !vessel.transform || vessel.packed || !vessel.mainBody)
				return;

			debugString.Length = 0;

			// generally other AI and guard mode expects this target to be engaged
			GetGuardTarget(); // get the guard target from weapon manager
			GetNonGuardTarget(); // if guard mode is off, get the UI target
			GetGuardNonTarget(); // pick a target if guard mode is on, but no target is selected, 
								 // though really targeting should be managed by the weaponManager, what if we pick an airplane while having only abrams cannons? :P
								 // (this is another reason why target selection is hardcoded into the base class, so changing this later is less of a mess :) )

			AutoPilot(s);
		}

		#region Pilot on/off

		public virtual void ActivatePilot()
		{
			pilotOn = true;
            if (activeVessel)
                activeVessel.OnFlyByWire -= autoPilot;
            activeVessel = vessel;
            activeVessel.OnFlyByWire += autoPilot;

			if (!speedController)
			{
				speedController = gameObject.AddComponent<BDAirspeedControl>();
				speedController.vessel = vessel;
			}

			speedController.Activate();

			GameEvents.onVesselDestroy.Remove(RemoveAutopilot);
			GameEvents.onVesselDestroy.Add(RemoveAutopilot);

			assignedPositionWorld = vessel.ReferenceTransform.position;

			RefreshPartWindow();
		}

		public virtual void DeactivatePilot()
		{
			pilotOn = false;
            if (activeVessel)
                activeVessel.OnFlyByWire -= autoPilot;
			RefreshPartWindow();

			if (speedController)
			{
				speedController.Deactivate();
			}
		}

		protected void RemoveAutopilot(Vessel v)
		{
			if (v == vessel)
			{
				v.OnFlyByWire -= autoPilot;
			}
		}

		protected void RefreshPartWindow()
		{
			Events["TogglePilot"].guiName = pilotEnabled ? "Deactivate Pilot" : "Activate Pilot";
		}

		[KSPEvent(guiActive = true, guiName = "Toggle Pilot", active = true)]
		public void TogglePilot()
		{
			if (pilotEnabled)
			{
				DeactivatePilot();
			}
			else
			{
				ActivatePilot();
			}
		}

		[KSPAction("Activate Pilot")]
		public void AGActivatePilot(KSPActionParam param) => ActivatePilot();

		[KSPAction("Deactivate Pilot")]
		public void AGDeactivatePilot(KSPActionParam param) => DeactivatePilot();

		[KSPAction("Toggle Pilot")]
		public void AGTogglePilot(KSPActionParam param) => TogglePilot();

        public virtual string Name { get; } = "AI Control";
        public bool Enabled => pilotEnabled;
        public void Toggle() => TogglePilot();
        #endregion

        #region events

        protected virtual void Start()
		{
			if (HighLogic.LoadedSceneIsFlight)
			{
				part.OnJustAboutToBeDestroyed += DeactivatePilot;
				vessel.OnJustAboutToBeDestroyed += DeactivatePilot;
                GameEvents.onVesselWasModified.Add(onVesselWasModified);
				MissileFire.OnToggleTeam += OnToggleTeam;

                activeVessel = vessel;
				UpdateWeaponManager();

				if (pilotEnabled)
				{
					ActivatePilot();
				}
			}

			RefreshPartWindow();
		}

		protected virtual void OnDestroy()
		{
			MissileFire.OnToggleTeam -= OnToggleTeam;
		}

		protected virtual void OnGUI()
		{
			if (!pilotEnabled || !vessel.isActiveVessel) return;
			if (BDArmorySettings.DRAW_DEBUG_LABELS)
			{
				GUI.Label(new Rect(200, Screen.height - 200, 400, 400), $"{vessel.name}: {debugString.ToString()}");
			}
		}

		protected virtual void OnToggleTeam(MissileFire mf, BDArmorySetup.BDATeams team)
		{
			if (mf.vessel == vessel || (commandLeader && commandLeader.vessel == mf.vessel))
			{
				ReleaseCommand();
			}
		}

        protected virtual void onVesselWasModified(Vessel v)
        {
            if (v != activeVessel)
                return;

            if (vessel != activeVessel)
            {
                if (activeVessel)
                    activeVessel.OnJustAboutToBeDestroyed -= DeactivatePilot;
                if (vessel)
                    vessel.OnJustAboutToBeDestroyed += DeactivatePilot;
                if (weaponManager != null && weaponManager.vessel == activeVessel)
                {
                    if (this.Equals(weaponManager.AI))
                        weaponManager.AI = null;
                    UpdateWeaponManager();
                }
            }

            activeVessel = vessel;
        }

		#endregion

		#region utilities
		protected void UpdateWeaponManager()
		{
			weaponManager = vessel.FindPartModuleImplementing<MissileFire>();
			if (weaponManager != null)
				weaponManager.AI = this;
		}

		protected void GetGuardTarget()
		{
			if (weaponManager == null || weaponManager.vessel != vessel)
				UpdateWeaponManager();
			if (weaponManager != null && weaponManager.vessel == vessel)
			{
				if (weaponManager.guardMode && weaponManager.currentTarget != null)
				{
					targetVessel = weaponManager.currentTarget.Vessel;
				}
				else
				{
					targetVessel = null;
				}
				weaponManager.AI = this;
				return;
			}
		}

		/// <summary>
		/// If guard mode is set but no target is selected, pick something
		/// </summary>
		protected virtual void GetGuardNonTarget()
		{
			if (weaponManager && weaponManager.guardMode && !targetVessel)
			{
				TargetInfo potentialTarget = BDATargetManager.GetLeastEngagedTarget(weaponManager);
				if (potentialTarget && potentialTarget.Vessel)
				{
					targetVessel = potentialTarget.Vessel;
				}
			}
		}

		/// <summary>
		/// If guard mode off, and UI target is of the opposing team, set it as target
		/// </summary>
		protected void GetNonGuardTarget()
		{
			if (weaponManager != null && !weaponManager.guardMode)
			{
				if (vessel.targetObject?.GetVessel()?.FindPartModuleImplementing<MissileFire>()?.team == !weaponManager.team)
					targetVessel = (Vessel)vessel.targetObject;
			}
		}

		/// <summary>
		/// Write some text to the debug field (the one on lower left when debug labels are on), followed by a newline.
		/// </summary>
		/// <param name="text">text to write</param>
		protected void DebugLine(string text)
		{
			debugString.Append(text);
			debugString.Append(Environment.NewLine);
		}

		protected void SetStatus(string text)
		{
			currentStatus = text;
			DebugLine(text);
		}
		#endregion

		#region WingCommander

		public virtual void ReleaseCommand()
		{
			if (!vessel || command == PilotCommands.Free) return;
			if (command == PilotCommands.Follow && commandLeader)
			{
				commandLeader = null;
			}
			Debug.Log(vessel.vesselName + " was released from command.");
			command = PilotCommands.Free;

			assignedPositionWorld = vesselTransform.position;
		}

		public virtual void CommandFollow(ModuleWingCommander leader, int followerIndex)
		{
			if (!pilotEnabled) return;
			if (leader == vessel || followerIndex < 0) return;

			Debug.Log(vessel.vesselName + " was commanded to follow.");
			command = PilotCommands.Follow;
			commandLeader = leader;
			commandFollowIndex = followerIndex;
		}

		public virtual void CommandAG(KSPActionGroup ag)
		{
			if (!pilotEnabled) return;
			vessel.ActionGroups.ToggleGroup(ag);
		}

		public virtual void CommandFlyTo(Vector3 gpsCoords)
		{
			if (!pilotEnabled) return;

			Debug.Log(vessel.vesselName + " was commanded to go to.");
			assignedPositionGeo = gpsCoords;
			command = PilotCommands.FlyTo;
		}

		public virtual void CommandAttack(Vector3 gpsCoords)
		{
			if (!pilotEnabled) return;

			Debug.Log(vessel.vesselName + " was commanded to attack.");
			assignedPositionGeo = gpsCoords;
			command = PilotCommands.Attack;
		}

		public virtual void CommandTakeOff()
		{
			ActivatePilot();
		}

		#endregion
	}
}
