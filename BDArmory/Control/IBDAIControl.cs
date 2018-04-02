using UnityEngine;

namespace BDArmory.Control
{
	public interface IBDAIControl
	{
		#region PartModule
		Vessel vessel { get; }
		Transform transform { get; }
		#endregion

		/// <summary>
		/// The weapon manager the AI connects to.
		/// </summary>
		MissileFire weaponManager { get; }

		void ActivatePilot();
		void DeactivatePilot();
		void TogglePilot();

		bool pilotEnabled { get; }

		/// <summary>
		/// A function to check if AI could possibly fire at the target with forward looking fixed weapons.
		/// E.g. ships won't be able to fire at airborne targets, hot air baloons might never return true, etc.
		/// </summary>
		/// <param name="target">Vessel to be checked</param>
		/// <returns>true if the AI thinks it might eventually fire on the target with direct fire weapons, false otherwise</returns>
        /// <remarks>Guard mode uses this to check if fixed weapons are viable when selecting weapons.</remarks>
		bool IsValidFixedWeaponTarget(Vessel target);

		/// <summary>
		/// Check if AI is combat-capable.
		/// E.g. dogfight competition mode checks this before starting the competition.
		/// </summary>
		/// <returns>true if AI is ready for combat</returns>
        /// <remarks>Mainly use this to check for obvious user errors such as forgetting to stage engines.</remarks>
		bool CanEngage();

		#region WingCommander
		string currentStatus { get; }

		void ReleaseCommand();
		void CommandFollow(ModuleWingCommander leader, int followerIndex);
		void CommandAG(KSPActionGroup ag);
		void CommandFlyTo(Vector3 gpsCoords);
		void CommandAttack(Vector3 gpsCoords);
		void CommandTakeOff();

		Vector3d commandGPS { get; }
		PilotCommands currentCommand { get; }
		ModuleWingCommander commandLeader { get; }
		#endregion
	}

	public enum PilotCommands { Free, Attack, Follow, FlyTo }
}
