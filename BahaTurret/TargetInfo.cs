using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace BahaTurret
{
	public class TargetInfo : MonoBehaviour
	{
		public BDArmorySettings.BDATeams team;
		public bool isMissile = false;
		public MissileBase MissileBaseModule = null;
		public MissileFire weaponManager;
        List<MissileFire> friendliesEngaging;
        public float detectedTime;
        Coroutine lifeRoutine;
        Coroutine massRoutine;

        public bool isLanded
		{
			get
			{
                //if(!vessel) return false;
                //return vessel.Landed;
                if (!vessel) return false;
                if (
                    (vessel.situation == Vessel.Situations.LANDED ||
                    vessel.situation == Vessel.Situations.SPLASHED) && // Boats should be included 
                    !isUnderwater //refrain from shooting subs with missiles
                    )
                {
                    
                    return true;
                }
                
                else
                    return false;
            }
		}

        public bool isFlying
        {
            get
            {
                if (!vessel) return false;
                if (vessel.situation == Vessel.Situations.FLYING ) return true;
                else
                    return false;
            }

        }

        public bool isUnderwater
        {
            get
            {
                if (!vessel) return false;
                if (vessel.altitude < -20) //some boats sit slightly underwater, this is only for submersibles
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
        
        public bool isSplashed
        {
            get
            {
                if (!vessel) return false;
                if (vessel.situation == Vessel.Situations.SPLASHED) return true;
                else
                    return false;
            }
        }

        public Vector3 velocity
		{
			get
			{
				if(!vessel) return Vector3.zero;
				return vessel.srf_velocity;
			}
		}

		public Vector3 position
        {
            get
            {
                return vessel.vesselTransform.position;
            }
        }

		private Vessel vessel;

		public Vessel Vessel
		{
			get
			{
				if(!vessel)
				{
					vessel = GetComponent<Vessel>();
				}

				return vessel;
			}
			set
			{
				vessel = value;
			}
		}

		public bool isThreat
		{
			get
			{
				if(!Vessel)
				{
					return false;
				}

				if(isMissile && MissileBaseModule && !MissileBaseModule.HasMissed)
				{
					return true;
				}
				else if(weaponManager && weaponManager.vessel.IsControllable)
				{
					return true;
				}

				return false;
			}
		}		
        
		void Awake()
		{
			if(!vessel)
			{
				vessel = GetComponent<Vessel>();
			}
			if(!vessel)
			{
				Debug.Log ("[BDArmory]:TargetInfo was added to a non-vessel");
				Destroy (this);
				return;
			}

			//destroy this if a target info is already attached to the vessel
			foreach(var otherInfo in vessel.gameObject.GetComponents<TargetInfo>())
			{
				if(otherInfo != this)
				{
					Destroy(this);
					return;
				}
			}

			team = BDArmorySettings.BDATeams.None;

			bool foundMf = false;
			foreach(var mf in vessel.FindPartModulesImplementing<MissileFire>())
			{
				foundMf = true;
				team = BDATargetManager.BoolToTeam(mf.team);
				weaponManager = mf;
				break;
			}
			if(!foundMf)
			{
				foreach(var ml in vessel.FindPartModulesImplementing<MissileBase>())
				{
					isMissile = true;
					MissileBaseModule = ml;
					team = BDATargetManager.BoolToTeam(ml.Team);
					break;
				}
			}
				
			if(team != BDArmorySettings.BDATeams.None)
			{
				if(!BDATargetManager.TargetDatabase[BDATargetManager.OtherTeam(team)].Contains(this))
				{
					BDATargetManager.TargetDatabase[BDATargetManager.OtherTeam(team)].Add(this);
				}
			}

			friendliesEngaging = new List<MissileFire>();

			vessel.OnJustAboutToBeDestroyed += AboutToBeDestroyed;
			lifeRoutine = StartCoroutine(LifetimeRoutine());
			//add delegate to peace enable event
			BDArmorySettings.OnPeaceEnabled += OnPeaceEnabled;

			if(!isMissile && team != BDArmorySettings.BDATeams.None)
			{
				massRoutine = StartCoroutine(MassRoutine());
			}
		}

		void OnPeaceEnabled()
		{
			if(lifeRoutine != null)
			{
				StopCoroutine(lifeRoutine);
			}
			if(massRoutine != null)
			{
				StopCoroutine(massRoutine);
			}

			Destroy(this);
		}

		void OnDestroy()
		{
			//remove delegate from peace enable event
			BDArmorySettings.OnPeaceEnabled -= OnPeaceEnabled;
		}
	
		IEnumerator LifetimeRoutine()
		{
			detectedTime = Time.time;
			while(Time.time - detectedTime < 60 && enabled)
			{
				yield return null;
			}
			if(massRoutine != null)
			{
				StopCoroutine(massRoutine);
			}
			Destroy(this);
		}
		
		IEnumerator MassRoutine()
		{
			float startMass = vessel.GetTotalMass();
			while(enabled)
			{
				if(vessel.GetTotalMass() < startMass / 4)
				{
					if(lifeRoutine != null)
					{
						StopCoroutine(lifeRoutine);
					}

					RemoveFromDatabases();
					yield break;
				}
				yield return new WaitForSeconds(1);
			}
		}

		void Update()
		{
			if(!vessel)
			{
				AboutToBeDestroyed();
			}
			else
			{
				if((vessel.vesselType == VesselType.Debris) && (weaponManager == null))
				{
					RemoveFromDatabases();
					team = BDArmorySettings.BDATeams.None;
				}
			}
		}

		public int numFriendliesEngaging
		{
			get
			{
				if(friendliesEngaging == null)
				{
					return 0;
				}
				friendliesEngaging.RemoveAll(item => item == null);
				return friendliesEngaging.Count;
			}
		}

		public void Engage(MissileFire mf)
		{
			/*
			if(!hasStarted)
			{
				Start();
			}
			*/
			if(!friendliesEngaging.Contains(mf))
			{
				friendliesEngaging.Add(mf);
			}
		}

		public void Disengage(MissileFire mf)
		{
			friendliesEngaging.Remove(mf);
		}
		
		void AboutToBeDestroyed()
		{
			RemoveFromDatabases();
			Destroy(this);
		}

		public bool IsCloser(TargetInfo otherTarget, MissileFire myMf)
		{
			float thisSqrDist = (position-myMf.transform.position).sqrMagnitude;
			float otherSqrDist = (otherTarget.position-myMf.transform.position).sqrMagnitude;
			return thisSqrDist < otherSqrDist;
		}

		public void RemoveFromDatabases()
		{
			BDATargetManager.TargetDatabase[BDArmorySettings.BDATeams.A].Remove(this);
			BDATargetManager.TargetDatabase[BDArmorySettings.BDATeams.B].Remove(this);
		}
	}
}

