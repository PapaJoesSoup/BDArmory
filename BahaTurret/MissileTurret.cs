using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BahaTurret
{
	public class MissileTurret : PartModule
	{
		[KSPField]
		public string finalTransformName;
		public Transform finalTransform;

		[KSPField]
		public int turretID = 0;

		ModuleTurret turret;

		[KSPField(guiActive = true, guiName = "Turret Enabled")]
		public bool turretEnabled = false;

		[KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Auto-Return"), UI_Toggle(scene = UI_Scene.Editor)]
		public bool autoReturn = true;
		bool hasReturned = true;

		[KSPField]
		public float railLength = 3;

		Coroutine returnRoutine;

		int missileCount = 0;
		MissileLauncher[] missileChildren;
		Transform[] missileTransforms;
		Transform[] missileReferenceTransforms;

		Dictionary<string,Vector3> comOffsets;

		public bool slaved = false;

		public Vector3 slavedTargetPosition;

		bool pausingAfterShot = false;
		float timeFired = 0;
		[KSPField]
		public float firePauseTime = 0.25f;

		ModuleRadar attachedRadar;
		bool hasAttachedRadar = false;
		[KSPField]
		public bool disableRadarYaw = false;
		[KSPField]
		public bool disableRadarPitch = false;

		[KSPField]
		public bool mouseControllable = true;

		//animation
		[KSPField]
		public string deployAnimationName;
		AnimationState deployAnimState;
		bool hasDeployAnimation = false;
		[KSPField]
		public float deployAnimationSpeed = 1;
		bool editorDeployed = false;
		Coroutine deployAnimRoutine;

		//special
		[KSPField]
		public bool activeMissileOnly = false;

		MissileFire wm;
		public MissileFire weaponManager
		{
			get
			{
				if(!wm || wm.vessel != vessel)
				{
					wm = null;
				
					foreach(var mf in vessel.FindPartModulesImplementing<MissileFire>())
					{
						wm = mf;
						break;
					}
				}

				return wm;
			}
		}

		IEnumerator DeployAnimation(bool forward)
		{
			yield return null;

			if(forward)
			{
				while(deployAnimState.normalizedTime < 1)
				{
					deployAnimState.speed = deployAnimationSpeed;
					yield return null;
				}

				deployAnimState.normalizedTime = 1;
			}
			else
			{
				deployAnimState.speed = 0;

				while(pausingAfterShot)
				{
					yield return new WaitForFixedUpdate();
				}

				while(deployAnimState.normalizedTime > 0)
				{
					deployAnimState.speed = -deployAnimationSpeed;
					yield return null;
				}

				deployAnimState.normalizedTime = 0;
			}

			deployAnimState.speed = 0;
		}

		public void EnableTurret()
		{
			if(!HighLogic.LoadedSceneIsFlight)
			{
				return;
			}

			if(returnRoutine!=null)
			{
				StopCoroutine(returnRoutine);
				returnRoutine = null;
			}

			turretEnabled = true;
			hasReturned = false;

			if(hasAttachedRadar)
			{
				attachedRadar.lockingYaw = !disableRadarYaw;
				attachedRadar.lockingPitch = !disableRadarPitch;
			}

			if(!autoReturn)
			{
				Events["ReturnTurret"].guiActive = false;
			}

			if(hasDeployAnimation)
			{
				if(deployAnimRoutine != null)
				{
					StopCoroutine(deployAnimRoutine);
				}

				deployAnimRoutine = StartCoroutine(DeployAnimation(true));
			}
		}
			

		public void DisableTurret()
		{
			turretEnabled = false;

			if(autoReturn)
			{
				hasReturned = true;
				if(returnRoutine != null)
				{
					StopCoroutine(returnRoutine);
				}
				returnRoutine = StartCoroutine(ReturnRoutine());
			}

			if(hasAttachedRadar)
			{
				attachedRadar.lockingYaw = true;
				attachedRadar.lockingPitch = true;
			}

			if(!autoReturn)
			{
				Events["ReturnTurret"].guiActive = true;
			}

			if(hasDeployAnimation)
			{
				if(deployAnimRoutine != null)
				{
					StopCoroutine(deployAnimRoutine);
				}

				deployAnimRoutine = StartCoroutine(DeployAnimation(false));
			}
		}

		[KSPEvent(guiActive = false, guiActiveEditor = false, guiName = "Return Turret")]
		public void ReturnTurret()
		{
			if(!turretEnabled)
			{
				returnRoutine = StartCoroutine(ReturnRoutine());
				hasReturned = true;
			}
		}

		[KSPEvent(guiActive = false, guiActiveEditor = false, guiName = "Toggle Animation")]
		public void EditorToggleAnimation()
		{
			editorDeployed = !editorDeployed;

			if(deployAnimRoutine != null)
			{
				StopCoroutine(deployAnimRoutine);
			}

			deployAnimRoutine = StartCoroutine(DeployAnimation(editorDeployed));
		}

		IEnumerator ReturnRoutine()
		{
			if(turretEnabled)
			{
				hasReturned = false;
				yield break;
			}

			yield return new WaitForSeconds(0.25f);

			while(pausingAfterShot)
			{
				yield return new WaitForFixedUpdate();
			}

			while(!turret.ReturnTurret())
			{
				UpdateMissilePositions();
				yield return new WaitForFixedUpdate();
			}
		}

		public override void OnStart(StartState state)
		{
			base.OnStart(state);

			part.force_activate();

			//setup anim
			if(!string.IsNullOrEmpty(deployAnimationName))
			{
				hasDeployAnimation = true;
				deployAnimState = Misc.SetUpSingleAnimation(deployAnimationName, part);
				if(state == StartState.Editor)
				{
					Events["EditorToggleAnimation"].guiActiveEditor = true;
				}
			}

			if(HighLogic.LoadedSceneIsFlight)
			{
				foreach(var tur in part.FindModulesImplementing<ModuleTurret>())
				{
					if(tur.turretID == turretID)
					{
						turret = tur;
						break;
					}
				}

				attachedRadar = part.FindModuleImplementing<ModuleRadar>();
				if(attachedRadar) hasAttachedRadar = true;

				finalTransform = part.FindModelTransform(finalTransformName);

				UpdateMissileChildren();

				if(!autoReturn)
				{
					Events["ReturnTurret"].guiActive = true;
				}
			}
		}

		public override void OnFixedUpdate()
		{
			base.OnFixedUpdate();



			if(turretEnabled)
			{
				hasReturned = false;

				if(missileCount == 0)
				{
					DisableTurret();
					return;
				}

				Aim();
				UpdateMissilePositions();

				if(!vessel.IsControllable)
				{
					DisableTurret();
				}

			}
			else
			{
				if(Quaternion.FromToRotation(finalTransform.forward, turret.yawTransform.parent.parent.forward) != Quaternion.identity)
				{
					UpdateMissilePositions();
				}

				if(autoReturn && !hasReturned)
				{
					DisableTurret();
				}



			}

			pausingAfterShot = (Time.time - timeFired < firePauseTime);
		}
			

		void Aim()
		{
			UpdateTarget();

			if(slaved)
			{
				SlavedAim();
			}
			else
			{
				if(weaponManager && wm.guardMode)
				{
					return;
				}

				if(mouseControllable && vessel.isActiveVessel)
				{
					MouseAim();
				}
			}
		}

		void UpdateTarget()
		{
			slaved = false;

			if(weaponManager && wm.slavingTurrets && wm.currentMissile)
			{
				slaved = true;
				slavedTargetPosition = MissileGuidance.GetAirToAirFireSolution(wm.currentMissile, wm.slavedPosition, wm.slavedVelocity);
			}
		}



		public void SlavedAim()
		{
			if(pausingAfterShot) return;

			turret.AimToTarget(slavedTargetPosition);
		}


		void MouseAim()
		{
			if(pausingAfterShot) return;

			Vector3 targetPosition;
			float maxTargetingRange = 5000;

			//MouseControl
			Vector3 mouseAim = new Vector3(Input.mousePosition.x/Screen.width, Input.mousePosition.y/Screen.height, 0);
			Ray ray = FlightCamera.fetch.mainCamera.ViewportPointToRay(mouseAim);
			RaycastHit hit;
			if(Physics.Raycast(ray, out hit, maxTargetingRange, 557057))
			{
				targetPosition = hit.point;

				//aim through self vessel if occluding mouseray
				Part p = hit.collider.gameObject.GetComponentInParent<Part>();
				if(p && p.vessel && p.vessel == vessel)
				{
					targetPosition = ray.direction * maxTargetingRange + FlightCamera.fetch.mainCamera.transform.position; 
				}
			}
			else
			{
				targetPosition = (ray.direction * (maxTargetingRange+(FlightCamera.fetch.Distance*0.75f))) + FlightCamera.fetch.mainCamera.transform.position;	
			}

			turret.AimToTarget(targetPosition);
		}

		public void UpdateMissileChildren()
		{
			missileCount = 0;

			//setup com dictionary
			if(comOffsets == null)
			{
				comOffsets = new Dictionary<string, Vector3>();
			}

			//destroy the existing reference transform objects
			if(missileReferenceTransforms != null)
			{
				for(int i = 0; i < missileReferenceTransforms.Length; i++)
				{
					if(missileReferenceTransforms[i])
					{
						GameObject.Destroy(missileReferenceTransforms[i].gameObject);
					}
				}
			}

			List<MissileLauncher> msl = new List<MissileLauncher>();
			List<Transform> mtfl = new List<Transform>();
			List<Transform> mrl = new List<Transform>();

			foreach(var child in part.children)
			{
				if(child.parent != part) continue;

				MissileLauncher ml = child.FindModuleImplementing<MissileLauncher>();

				if(!ml) continue;

				Transform mTf = child.FindModelTransform("missileTransform");
				//fix incorrect hierarchy
				if(!mTf)
				{
					Transform modelTransform = ml.part.partTransform.FindChild("model");

					mTf = new GameObject("missileTransform").transform;
					Transform[] tfchildren = new Transform[modelTransform.childCount];
					for(int i = 0; i < modelTransform.childCount; i++)
					{
						tfchildren[i] = modelTransform.GetChild(i);
					}
					mTf.parent = modelTransform;
					mTf.localPosition = Vector3.zero;
					mTf.localRotation = Quaternion.identity;
					mTf.localScale = Vector3.one;
					for(int i = 0; i < tfchildren.Length; i++)
					{
						Debug.Log("MissileTurret moving transform: " + tfchildren[i].gameObject.name);
						tfchildren[i].parent = mTf;
					}
				}

				if(ml && mTf)
				{
					msl.Add(ml);
					mtfl.Add(mTf);
					Transform mRef = new GameObject().transform;
					mRef.position = mTf.position;
					mRef.rotation = mTf.rotation;
					mRef.parent = finalTransform;
					mrl.Add(mRef);

					ml.missileReferenceTransform = mTf;
					ml.missileTurret = this;

					ml.decoupleForward = true;
					ml.dropTime = 0;

					if(!comOffsets.ContainsKey(ml.part.name))
					{
						comOffsets.Add(ml.part.name, ml.part.CoMOffset);
					}

					missileCount++;
				}
			}

			missileChildren = msl.ToArray();
			missileTransforms = mtfl.ToArray();
			missileReferenceTransforms = mrl.ToArray();
		}

		void UpdateMissilePositions()
		{
			if(missileCount == 0)
			{
				return;
			}

			for(int i = 0; i < missileChildren.Length; i++)
			{
				if(missileTransforms[i] && missileChildren[i] && !missileChildren[i].hasFired)
				{
					missileTransforms[i].position = missileReferenceTransforms[i].position;
					missileTransforms[i].rotation = missileReferenceTransforms[i].rotation;

					Part missilePart = missileChildren[i].part;
					Vector3 newCoMOffset = missilePart.transform.InverseTransformPoint(missileTransforms[i].TransformPoint(comOffsets[missilePart.name]));
					missilePart.CoMOffset = newCoMOffset;
				}
			}
		}


		public void FireMissile(int index)
		{
			if(index < missileCount && missileChildren != null && missileChildren[index] != null)
			{
				PrepMissileForFire(index);
			
				if(weaponManager)
				{
					wm.SendTargetDataToMissile(missileChildren[index]);
				}
				missileChildren[index].FireMissile();
				StartCoroutine(MissileRailRoutine(missileChildren[index]));
				if(wm)
				{
					wm.UpdateList();
				}

				UpdateMissileChildren();

				timeFired = Time.time;
			}
		}

		public void FireMissile(MissileLauncher ml)
		{
			int index = IndexOfMissile(ml);
			if(index >= 0)
			{
				Debug.Log("Firing missile index: " + index);
				FireMissile(index);
			}
			else
			{
				Debug.Log("Tried to fire a missile that doesn't exist or is not attached to the turret.");
			}
		}

		IEnumerator MissileRailRoutine(MissileLauncher ml)
		{
			yield return null;
			Ray ray = new Ray(ml.transform.position, ml.missileReferenceTransform.forward);
			Vector3 localOrigin = turret.pitchTransform.InverseTransformPoint(ray.origin);
			Vector3 localDirection = turret.pitchTransform.InverseTransformDirection(ray.direction);
			float forwardSpeed = ml.decoupleSpeed;
			while(ml && Vector3.SqrMagnitude(ml.transform.position - ray.origin) < railLength*railLength)
			{
				float thrust = ml.timeIndex < ml.boostTime ? ml.thrust : ml.cruiseThrust;
				thrust = ml.timeIndex < ml.boostTime + ml.cruiseTime ? thrust : 0;
				float accel = thrust / ml.part.mass;
				forwardSpeed += accel * Time.fixedDeltaTime;

				ray.origin = turret.pitchTransform.TransformPoint(localOrigin);
				ray.direction = turret.pitchTransform.TransformDirection(localDirection);

				Vector3 projPos = Vector3.Project(ml.vessel.transform.position - ray.origin, ray.direction) + ray.origin;
				Vector3 railVel = part.rb.GetPointVelocity(projPos);
				//Vector3 projVel = Vector3.Project(ml.vessel.srf_velocity-railVel, ray.direction);

				ml.vessel.SetPosition(projPos);
				ml.vessel.SetWorldVelocity(railVel + (forwardSpeed*ray.direction));

				yield return new WaitForFixedUpdate();

				ray.origin = turret.pitchTransform.TransformPoint(localOrigin);
				ray.direction = turret.pitchTransform.TransformDirection(localDirection);
			}


		}


		void PrepMissileForFire(int index)
		{
			Debug.Log("Prepping missile for turret fire.");
			missileTransforms[index].localPosition = Vector3.zero;
			missileTransforms[index].localRotation = Quaternion.identity;
			missileChildren[index].part.partTransform.position = missileReferenceTransforms[index].position;
			missileChildren[index].part.partTransform.rotation = missileReferenceTransforms[index].rotation;

			missileChildren[index].dropTime = 0;
			missileChildren[index].decoupleForward = true;

			missileChildren[index].part.CoMOffset = comOffsets[missileChildren[index].part.name];
		}

		public void PrepMissileForFire(MissileLauncher ml)
		{
			int index = IndexOfMissile(ml);

			if(index >= 0)
			{
				PrepMissileForFire(index);
			}
			else
			{
				Debug.Log("Tried to prep a missile for firing that doesn't exist or is not attached to the turret.");
			}
		}

		private int IndexOfMissile(MissileLauncher ml)
		{
			if(missileCount == 0) return -1;

			for(int i = 0; i < missileCount; i++)
			{
				if(missileChildren[i] && missileChildren[i] == ml)
				{
					return i;
				}
			}

			return -1;
		}

		public bool ContainsMissileOfType(MissileLauncher ml)
		{
			if(!ml) return false;
			if(missileCount == 0) return false;

			for(int i = 0; i < missileCount; i++)
			{
				if((missileChildren[i]) && missileChildren[i].part.name == ml.part.name)
				{
					return true;
				}
			}
			return false;
		}


	}
}

