using System;
using System.Text;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace BahaTurret
{
    public class RocketLauncher : EngageableWeapon, IBDWeapon
    {
        public bool hasRocket = true;

        [KSPField] public string shortName = string.Empty;

        [KSPField(isPersistant = false)] public string rocketType;

        [KSPField(isPersistant = false)] public string rocketModelPath;

        [KSPField(isPersistant = false)] public float rocketMass;

        [KSPField(isPersistant = false)] public float thrust;

        [KSPField(isPersistant = false)] public float thrustTime;

        [KSPField(isPersistant = false)] public float blastRadius;

        [KSPField(isPersistant = false)] public float blastForce;

        [KSPField] public float blastHeat = -1;

        [KSPField(isPersistant = false)] public bool descendingOrder = true;

        [KSPField(isPersistant = false)] public string explModelPath = "BDArmory/Models/explosion/explosion";

        [KSPField(isPersistant = false)] public string explSoundPath = "BDArmory/Sounds/explode1";

        [KSPField] public float thrustDeviation = 0.10f;

        [KSPField] public float maxTargetingRange = 8000;
        float currentTgtRange = 8000;
        float predictedFlightTime = 1;

        public bool drawAimer = false;

        Vector3 rocketPrediction = Vector3.zero;
        Texture2D aimerTexture;

        Transform[] rockets;

        public AudioSource sfAudioSource;

        //animation
        [KSPField] public string deployAnimationName;
        [KSPField] public float deployAnimationSpeed = 1;
        AnimationState deployAnimState;
        bool hasDeployAnimation = false;
        public bool deployed = false;
        Coroutine deployAnimRoutine;

        public bool readyToFire = true;

        public Vessel legacyGuardTarget = null;
        public float lastAutoFiredTime = 0;
        public float autoRippleRate = 0;
        public float autoFireStartTime = 0;
        public float autoFireDuration = 0;

        //turret
        [KSPField] public int turretID = 0;
        public ModuleTurret turret;
        Vector3 trajectoryOffset = Vector3.zero;
        public MissileFire weaponManager;
        bool targetInTurretView = true;

        public float yawRange
        {
            get { return turret ? turret.yawRange : 0; }
        }

        public float maxPitch
        {
            get { return turret ? turret.maxPitch : 0; }
        }

        public float minPitch
        {
            get { return turret ? turret.minPitch : 0; }
        }

        double lastRocketsLeft = 0;

        //weapon interface
        public Part GetPart()
        {
            return part;
        }

        public string GetShortName()
        {
            return shortName;
        }

        public WeaponClasses GetWeaponClass()
        {
            return WeaponClasses.Rocket;
        }

        public string GetSubLabel()
        {
            return string.Empty;
        }

        public string GetMissileType()
        {
            return string.Empty;
        }


        [KSPAction("Fire")]
        public void AGFire(KSPActionParam param)
        {
            FireRocket();
        }

        [KSPEvent(guiActive = true, guiName = "Fire", active = true)]
        public void GuiFire()
        {
            FireRocket();
        }

        [KSPEvent(guiActive = true, guiName = "Jettison", active = true, guiActiveEditor = false)]
        public void Jettison()
        {
            if (turret)
            {
                return;
            }

            part.decouple(0);
            if (BDArmorySettings.Instance.ActiveWeaponManager != null)
                BDArmorySettings.Instance.ActiveWeaponManager.UpdateList();
        }

        [KSPEvent(guiActive = false, guiName = "Toggle Turret", guiActiveEditor = false)]
        public void ToggleTurret()
        {
            if (deployed)
            {
                DisableTurret();
            }
            else
            {
                EnableTurret();
            }
        }

        public void EnableTurret()
        {
            deployed = true;
            drawAimer = true;
            hasReturned = false;

            if (returnRoutine != null)
            {
                StopCoroutine(returnRoutine);
                returnRoutine = null;
            }

            if (hasDeployAnimation)
            {
                if (deployAnimRoutine != null)
                {
                    StopCoroutine(deployAnimRoutine);
                }
                deployAnimRoutine = StartCoroutine(DeployAnimRoutine(true));
            }
            else
            {
                readyToFire = true;
            }
        }

        public void DisableTurret()
        {
            deployed = false;
            readyToFire = false;
            drawAimer = false;
            hasReturned = false;
            targetInTurretView = false;

            if (returnRoutine != null)
            {
                StopCoroutine(returnRoutine);
            }
            returnRoutine = StartCoroutine(ReturnRoutine());

            if (hasDeployAnimation)
            {
                if (deployAnimRoutine != null)
                {
                    StopCoroutine(deployAnimRoutine);
                }

                deployAnimRoutine = StartCoroutine(DeployAnimRoutine(false));
            }
        }

        bool hasReturned = true;
        Coroutine returnRoutine;

        IEnumerator ReturnRoutine()
        {
            if (deployed)
            {
                hasReturned = false;
                yield break;
            }

            yield return new WaitForSeconds(0.25f);

            while (!turret.ReturnTurret())
            {
                yield return new WaitForFixedUpdate();
            }

            hasReturned = true;
        }

        void SetupAudio()
        {
            sfAudioSource = gameObject.AddComponent<AudioSource>();
            sfAudioSource.minDistance = 1;
            sfAudioSource.maxDistance = 2000;
            sfAudioSource.dopplerLevel = 0;
            sfAudioSource.priority = 230;
            sfAudioSource.spatialBlend = 1;

            UpdateVolume();
            BDArmorySettings.OnVolumeChange += UpdateVolume;
        }

        void UpdateVolume()
        {
            if (sfAudioSource)
            {
                sfAudioSource.volume = BDArmorySettings.BDARMORY_WEAPONS_VOLUME;
            }
        }


        public override void OnStart(PartModule.StartState state)
        {
            // extension for feature_engagementenvelope
            InitializeEngagementRange(0, maxTargetingRange);

            if (HighLogic.LoadedSceneIsFlight)
            {
                part.force_activate();

                aimerTexture = BDArmorySettings.Instance.greenPointCircleTexture;
                // GameDatabase.Instance.GetTexture("BDArmory/Textures/grayCircle", false);


                MakeRocketArray();
                UpdateRocketScales();

                if (shortName == string.Empty)
                {
                    shortName = part.partInfo.title;
                }

                UpdateAudio();
                BDArmorySettings.OnVolumeChange += UpdateAudio;
            }

            if (HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor)
            {
                foreach (var turr in part.FindModulesImplementing<ModuleTurret>())
                {
                    if (turr.turretID == turretID)
                    {
                        turret = turr;
                        targetInTurretView = false;
                        break;
                    }
                }

                if (turret)
                {
                    Events["GuiFire"].guiActive = false;
                    Events["Jettison"].guiActive = false;
                    Actions["AGFire"].active = false;

                    if (HighLogic.LoadedSceneIsFlight)
                    {
                        Events["ToggleTurret"].guiActive = true;
                    }
                }

                if (!string.IsNullOrEmpty(deployAnimationName))
                {
                    deployAnimState = Misc.SetUpSingleAnimation(deployAnimationName, part);
                    hasDeployAnimation = true;

                    readyToFire = false;
                }
            }
            SetupAudio();
        }

        IEnumerator DeployAnimRoutine(bool forward)
        {
            readyToFire = false;
            BDArmorySettings.Instance.UpdateCursorState();

            if (forward)
            {
                while (deployAnimState.normalizedTime < 1)
                {
                    deployAnimState.speed = deployAnimationSpeed;
                    yield return null;
                }

                deployAnimState.normalizedTime = 1;
            }
            else
            {
                while (!hasReturned)
                {
                    deployAnimState.speed = 0;
                    yield return null;
                }

                while (deployAnimState.normalizedTime > 0)
                {
                    deployAnimState.speed = -deployAnimationSpeed;
                    yield return null;
                }

                deployAnimState.normalizedTime = 0;
            }

            deployAnimState.speed = 0;

            readyToFire = deployed;
            BDArmorySettings.Instance.UpdateCursorState();
        }

        void UpdateAudio()
        {
            if (sfAudioSource)
            {
                sfAudioSource.volume = BDArmorySettings.BDARMORY_WEAPONS_VOLUME;
            }
        }

        void OnDestroy()
        {
            BDArmorySettings.OnVolumeChange -= UpdateAudio;
        }


        public override void OnFixedUpdate()
        {
            if (GetRocketResource().amount != lastRocketsLeft)
            {
                UpdateRocketScales();
                lastRocketsLeft = GetRocketResource().amount;
            }

            if (!vessel.IsControllable)
            {
                return;
            }

            SimulateTrajectory();

            currentTgtRange = maxTargetingRange;

            if (deployed && readyToFire && turret)
            {
                Aim();
            }
        }

        public override void OnUpdate()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (readyToFire && deployed)
                {
                    if (returnRoutine != null)
                    {
                        StopCoroutine(returnRoutine);
                        returnRoutine = null;
                    }

                    if (weaponManager && weaponManager.guardMode && weaponManager.selectedWeaponString == GetShortName())
                    {
                        if (Time.time - autoFireStartTime < autoFireDuration)
                        {
                            float fireInterval = 0.5f;
                            if (autoRippleRate > 0) fireInterval = 60f/autoRippleRate;
                            if (Time.time - lastAutoFiredTime > fireInterval)
                            {
                                FireRocket();
                                lastAutoFiredTime = Time.time;
                            }
                        }
                    }
                    else if ((!weaponManager ||
                              (weaponManager.selectedWeaponString == GetShortName() && !weaponManager.guardMode)))
                    {
                        if (BDInputUtils.GetKeyDown(BDInputSettingsFields.WEAP_FIRE_KEY) &&
                            (vessel.isActiveVessel || BDArmorySettings.REMOTE_SHOOTING))
                        {
                            FireRocket();
                        }
                    }
                }
            }
        }

        bool mouseAiming = false;

        void Aim()
        {
            mouseAiming = false;
            if (weaponManager && (weaponManager.slavingTurrets || weaponManager.guardMode))
            {
                SlavedAim();
            }
            else
            {
                if (vessel.isActiveVessel || BDArmorySettings.REMOTE_SHOOTING)
                {
                    MouseAim();
                }
            }
        }

        void SlavedAim()
        {
            Vector3 targetPosition;
            Vector3 targetVel;
            Vector3 targetAccel;
            if (weaponManager.slavingTurrets)
            {
                targetPosition = weaponManager.slavedPosition;
                targetVel = weaponManager.slavedVelocity;
                targetAccel = weaponManager.slavedAcceleration;

                //targetPosition -= vessel.srf_velocity * predictedFlightTime;
            }
            else if (legacyGuardTarget)
            {
                targetPosition = legacyGuardTarget.CoM;
                targetVel = legacyGuardTarget.srf_velocity;
                targetAccel = legacyGuardTarget.acceleration;
            }
            else
            {
                targetInTurretView = false;
                return;
            }

            currentTgtRange = Vector3.Distance(targetPosition, rockets[0].parent.transform.position);


            targetPosition += trajectoryOffset;
            targetPosition += targetVel*predictedFlightTime;
            targetPosition += 0.5f*targetAccel*predictedFlightTime*predictedFlightTime;

            turret.AimToTarget(targetPosition);
            targetInTurretView = turret.TargetInRange(targetPosition, 2, maxTargetingRange);
        }

        void MouseAim()
        {
            mouseAiming = true;
            Vector3 targetPosition;
            //	float maxTargetingRange = 8000;

            float targetDistance;

            //MouseControl
            Vector3 mouseAim = new Vector3(Input.mousePosition.x/Screen.width, Input.mousePosition.y/Screen.height, 0);
            Ray ray = FlightCamera.fetch.mainCamera.ViewportPointToRay(mouseAim);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, maxTargetingRange, 557057))
            {
                targetPosition = hit.point;

                //aim through self vessel if occluding mouseray
                Part p = hit.collider.gameObject.GetComponentInParent<Part>();
                if (p && p.vessel && p.vessel == vessel)
                {
                    targetPosition = ray.direction*maxTargetingRange + FlightCamera.fetch.mainCamera.transform.position;
                }

                targetDistance = Vector3.Distance(hit.point, rockets[0].parent.position);
            }
            else
            {
                targetPosition = (ray.direction*(maxTargetingRange + (FlightCamera.fetch.Distance*0.75f))) +
                                 FlightCamera.fetch.mainCamera.transform.position;
                targetDistance = maxTargetingRange;
            }

            currentTgtRange = targetDistance;

            /*
            float accelDistance = (thrust / rocketMass) * ((thrustTime * thrustTime) / 2);
            float finalV = (thrust / rocketMass) * thrustTime;
            float aveThrustV = finalV / 2;
            float flightTime = targetDistance / finalV;//(accelDistance / aveThrustV) + ((targetDistance - accelDistance) / finalV);
            float gravDrop = 0.5f * (float)FlightGlobals.getGeeForceAtPosition(part.transform.position).magnitude * flightTime * flightTime;
            targetPosition += gravDrop * VectorUtils.GetUpDirection(part.transform.position);
            */

            targetPosition += trajectoryOffset;


            turret.AimToTarget(targetPosition);
            targetInTurretView = turret.TargetInRange(targetPosition, 2, maxTargetingRange);
        }

        public void FireRocket()
        {
            if (!readyToFire) return;
            if (!targetInTurretView) return;

            PartResource rocketResource = GetRocketResource();

            if (rocketResource == null)
            {
                Debug.Log(part.name + " doesn't carry the rocket resource it was meant to");
                return;
            }

            int rocketsLeft = (int) Math.Floor(rocketResource.amount);

            if (rocketsLeft >= 1)
            {
                Transform currentRocketTfm = rockets[rocketsLeft - 1];

                GameObject rocketObj = GameDatabase.Instance.GetModel(rocketModelPath);
                rocketObj =
                    (GameObject) Instantiate(rocketObj, currentRocketTfm.position, currentRocketTfm.parent.rotation);
                rocketObj.transform.rotation = currentRocketTfm.parent.rotation;
                rocketObj.transform.localScale = part.rescaleFactor*Vector3.one;
                currentRocketTfm.localScale = Vector3.zero;
                Rocket rocket = rocketObj.AddComponent<Rocket>();
                rocket.explModelPath = explModelPath;
                rocket.explSoundPath = explSoundPath;
                rocket.spawnTransform = currentRocketTfm;
                rocket.mass = rocketMass;
                rocket.blastForce = blastForce;
                rocket.blastHeat = blastHeat;
                rocket.blastRadius = blastRadius;
                rocket.thrust = thrust;
                rocket.thrustTime = thrustTime;
                rocket.randomThrustDeviation = thrustDeviation;
                if (BDArmorySettings.ALLOW_LEGACY_TARGETING && vessel.targetObject != null)
                {
                    rocket.targetVessel = vessel.targetObject.GetVessel();
                }

                rocket.sourceVessel = vessel;
                rocketObj.SetActive(true);
                rocketObj.transform.SetParent(currentRocketTfm.parent);
                rocket.parentRB = part.rb;

                sfAudioSource.PlayOneShot(GameDatabase.Instance.GetAudioClip("BDArmory/Sounds/launch"));
                rocketResource.amount--;

                lastRocketsLeft = rocketResource.amount;
            }
        }


        void SimulateTrajectory()
        {
            if ((BDArmorySettings.AIM_ASSIST && BDArmorySettings.DRAW_AIMERS && drawAimer && vessel.isActiveVessel) ||
                (weaponManager && weaponManager.guardMode && weaponManager.selectedWeaponString == GetShortName()))
            {
                float simTime = 0;
                Transform fireTransform = rockets[0].parent;
                Vector3 pointingDirection = fireTransform.forward;
                Vector3 simVelocity = part.rb.velocity;
                Vector3 simCurrPos = fireTransform.position + (part.rb.velocity*Time.fixedDeltaTime);
                Vector3 simPrevPos = fireTransform.position + (part.rb.velocity*Time.fixedDeltaTime);
                Vector3 simStartPos = fireTransform.position + (part.rb.velocity*Time.fixedDeltaTime);
                bool simulating = true;
                float simDeltaTime = 0.02f;
                List<Vector3> pointPositions = new List<Vector3>();
                pointPositions.Add(simCurrPos);

                bool slaved = turret && weaponManager && (weaponManager.slavingTurrets || weaponManager.guardMode);
                float atmosMultiplier =
                    Mathf.Clamp01(2.5f*
                                  (float)
                                  FlightGlobals.getAtmDensity(vessel.staticPressurekPa, vessel.externalTemperature,
                                      vessel.mainBody));
                while (simulating)
                {
                    RaycastHit hit;

                    if (simTime > thrustTime)
                    {
                        simDeltaTime = 0.1f;
                    }

                    if (simTime > 0.04f)
                    {
                        simDeltaTime = 0.02f;
                        if (simTime < thrustTime)
                        {
                            simVelocity += thrust/rocketMass*simDeltaTime*pointingDirection;
                        }

                        //rotation (aero stabilize)
                        pointingDirection = Vector3.RotateTowards(pointingDirection,
                            simVelocity + Krakensbane.GetFrameVelocity(),
                            atmosMultiplier*(0.5f*(simTime))*50*simDeltaTime*Mathf.Deg2Rad, 0);
                    }

                    //gravity
                    simVelocity += FlightGlobals.getGeeForceAtPosition(simCurrPos)*simDeltaTime;

                    simCurrPos += simVelocity*simDeltaTime;
                    pointPositions.Add(simCurrPos);
                    if (!mouseAiming && !slaved)
                    {
                        if (simTime > 0.1f &&
                            Physics.Raycast(simPrevPos, simCurrPos - simPrevPos, out hit,
                                Vector3.Distance(simPrevPos, simCurrPos), 557057))
                        {
                            rocketPrediction = hit.point;
                            simulating = false;
                            break;
                        }
                        else if (FlightGlobals.getAltitudeAtPos(simCurrPos) < 0)
                        {
                            rocketPrediction = simCurrPos;
                            simulating = false;
                            break;
                        }
                    }


                    simPrevPos = simCurrPos;

                    if ((simStartPos - simCurrPos).sqrMagnitude > currentTgtRange*currentTgtRange)
                    {
                        rocketPrediction = simStartPos + (simCurrPos - simStartPos).normalized*currentTgtRange;
                        //rocketPrediction = simCurrPos;
                        simulating = false;
                    }
                    simTime += simDeltaTime;
                }

                Vector3 pointingPos = fireTransform.position + (fireTransform.forward*currentTgtRange);
                trajectoryOffset = pointingPos - rocketPrediction;
                predictedFlightTime = simTime;

                if (BDArmorySettings.DRAW_DEBUG_LINES && BDArmorySettings.DRAW_AIMERS)
                {
                    Vector3[] pointsArray = pointPositions.ToArray();
                    if (gameObject.GetComponent<LineRenderer>() == null)
                    {
                        LineRenderer lr = gameObject.AddComponent<LineRenderer>();
                        lr.SetWidth(.1f, .1f);
                        lr.SetVertexCount(pointsArray.Length);
                        for (int i = 0; i < pointsArray.Length; i++)
                        {
                            lr.SetPosition(i, pointsArray[i]);
                        }
                    }
                    else
                    {
                        LineRenderer lr = gameObject.GetComponent<LineRenderer>();
                        lr.enabled = true;
                        lr.SetVertexCount(pointsArray.Length);
                        for (int i = 0; i < pointsArray.Length; i++)
                        {
                            lr.SetPosition(i, pointsArray[i]);
                        }
                    }
                }
                else
                {
                    if (gameObject.GetComponent<LineRenderer>() != null)
                    {
                        gameObject.GetComponent<LineRenderer>().enabled = false;
                    }
                }
            }

            //for straight aimer
            else if (BDArmorySettings.DRAW_AIMERS && drawAimer && vessel.isActiveVessel)
            {
                RaycastHit hit;
                float distance = 2500;
                if (Physics.Raycast(transform.position, transform.forward, out hit, distance, 557057))
                {
                    rocketPrediction = hit.point;
                }
                else
                {
                    rocketPrediction = transform.position + (transform.forward*distance);
                }
            }
        }

        void OnGUI()
        {
            if (drawAimer && vessel.isActiveVessel && BDArmorySettings.DRAW_AIMERS && !MapView.MapIsEnabled)
            {
                float size = 30;

                Vector3 aimPosition = FlightCamera.fetch.mainCamera.WorldToViewportPoint(rocketPrediction);

                Rect drawRect = new Rect(aimPosition.x*Screen.width - (0.5f*size),
                    (1 - aimPosition.y)*Screen.height - (0.5f*size), size, size);
                float cameraAngle = Vector3.Angle(FlightCamera.fetch.GetCameraTransform().forward,
                    rocketPrediction - FlightCamera.fetch.mainCamera.transform.position);
                if (cameraAngle < 90) GUI.DrawTexture(drawRect, aimerTexture);
            }
        }

        void MakeRocketArray()
        {
            Transform rocketsTransform = part.FindModelTransform("rockets");
            int numOfRockets = rocketsTransform.childCount;
            rockets = new Transform[numOfRockets];

            for (int i = 0; i < numOfRockets; i++)
            {
                string rocketName = rocketsTransform.GetChild(i).name;
                int rocketIndex = int.Parse(rocketName.Substring(7)) - 1;
                rockets[rocketIndex] = rocketsTransform.GetChild(i);
            }

            if (!descendingOrder) Array.Reverse(rockets);
        }


        public PartResource GetRocketResource()
        {
            foreach (var res in part.Resources)
            {
                if (res.resourceName == rocketType) return res;
            }

            return null;
        }

        void UpdateRocketScales()
        {
            PartResource rocketResource = GetRocketResource();
            double rocketsLeft = Math.Floor(rocketResource.amount);
            double rocketsMax = rocketResource.maxAmount;
            for (int i = 0; i < rocketsMax; i++)
            {
                if (i < rocketsLeft) rockets[i].localScale = Vector3.one;
                else rockets[i].localScale = Vector3.zero;
            }
        }

        // RMB info in editor
        public override string GetInfo()
        {
            var output = new StringBuilder();
            output.Append(Environment.NewLine);
            output.Append(String.Format("Weapon Type: {0}", "Rocket Launcher"));
            output.Append(Environment.NewLine);
            output.Append(String.Format("Rocket Type: {0}", rocketType));
            output.Append(Environment.NewLine);
            output.Append(String.Format("Max Range: {0} meters", maxTargetingRange));
            output.Append(Environment.NewLine);

            output.Append(String.Format("Blast radius/force/heat: {0}/{1}/{2}", blastRadius, blastForce, blastHeat));
            output.Append(Environment.NewLine);

            return output.ToString();

        }
    }


    public class Rocket : MonoBehaviour
    {
        public Transform spawnTransform;
        public Vessel targetVessel = null;
        public Vessel sourceVessel;
        public Vector3 startVelocity;
        public float mass;
        public float thrust;
        public float thrustTime;
        public float blastRadius;
        public float blastForce;
        public float blastHeat;
        public string explModelPath;
        public string explSoundPath;

        public float randomThrustDeviation = 0.05f;

        public Rigidbody parentRB;

        float startTime;
        public AudioSource audioSource;

        Vector3 prevPosition;
        Vector3 currPosition;


        Vector3 relativePos;

        float stayTime = 0.04f;
        float lifeTime = 10;

        //bool isThrusting = true;

        Rigidbody rb;


        KSPParticleEmitter[] pEmitters;

        float randThrustSeed = 0;

        void Start()
        {
            BDArmorySettings.numberOfParticleEmitters++;

            rb = gameObject.AddComponent<Rigidbody>();
            pEmitters = gameObject.GetComponentsInChildren<KSPParticleEmitter>();

            foreach (var pe in pEmitters)
            {
                if (FlightGlobals.getStaticPressure(transform.position) == 0 && pe.useWorldSpace)
                {
                    pe.emit = false;
                }
                else if (pe.useWorldSpace)
                {
                    BDAGaplessParticleEmitter gpe = pe.gameObject.AddComponent<BDAGaplessParticleEmitter>();
                    gpe.rb = rb;
                    gpe.emit = true;
                }
                else
                {
                    EffectBehaviour.AddParticleEmitter(pe);
                }
            }

            prevPosition = transform.position;
            currPosition = transform.position;
            startTime = Time.time;

            rb.mass = mass;
            rb.isKinematic = true;
            //rigidbody.velocity = startVelocity;
            if (!FlightGlobals.RefFrameIsRotating) rb.useGravity = false;

            rb.useGravity = false;

            randThrustSeed = UnityEngine.Random.Range(0f, 100f);

            SetupAudio();
        }

        void FixedUpdate()
        {
            //floatingOrigin fix
            if (sourceVessel != null &&
                (transform.position - sourceVessel.transform.position - relativePos).sqrMagnitude > 800*800)
            {
                transform.position = sourceVessel.transform.position + relativePos + (rb.velocity*Time.fixedDeltaTime);
            }
            if (sourceVessel != null) relativePos = transform.position - sourceVessel.transform.position;
            //


            if (Time.time - startTime < stayTime && transform.parent != null)
            {
                transform.rotation = transform.parent.rotation;
                transform.position = spawnTransform.position;
                //+(transform.parent.rigidbody.velocity*Time.fixedDeltaTime);
            }
            else
            {
                if (transform.parent != null && parentRB)
                {
                    startVelocity = parentRB.velocity;
                    transform.parent = null;
                    rb.isKinematic = false;
                    rb.velocity = startVelocity;
                }
            }

            if (rb && !rb.isKinematic)
            {
                //physics
                if (FlightGlobals.RefFrameIsRotating)
                {
                    rb.velocity += FlightGlobals.getGeeForceAtPosition(transform.position)*Time.fixedDeltaTime;
                }

                //guidance and attitude stabilisation scales to atmospheric density.
                float atmosMultiplier =
                    Mathf.Clamp01(2.5f*
                                  (float)
                                  FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(transform.position),
                                      FlightGlobals.getExternalTemperature(), FlightGlobals.currentMainBody));

                //model transform. always points prograde
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(rb.velocity + Krakensbane.GetFrameVelocity(), transform.up),
                    atmosMultiplier*(0.5f*(Time.time - startTime))*50*Time.fixedDeltaTime);


                if (Time.time - startTime < thrustTime && Time.time - startTime > stayTime)
                {
                    float random = randomThrustDeviation*(1 - (Mathf.PerlinNoise(4*Time.time, randThrustSeed)*2));
                    float random2 = randomThrustDeviation*(1 - (Mathf.PerlinNoise(randThrustSeed, 4*Time.time)*2));
                    rb.AddRelativeForce(new Vector3(random, random2, thrust));
                }
            }


            if (Time.time - startTime > thrustTime)
            {
                //isThrusting = false;
                foreach (var pEmitter in pEmitters)
                {
                    if (pEmitter.useWorldSpace)
                    {
                        pEmitter.minSize = Mathf.MoveTowards(pEmitter.minSize, 0.1f, 0.05f);
                        pEmitter.maxSize = Mathf.MoveTowards(pEmitter.maxSize, 0.2f, 0.05f);
                    }
                    else
                    {
                        pEmitter.minSize = Mathf.MoveTowards(pEmitter.minSize, 0, 0.1f);
                        pEmitter.maxSize = Mathf.MoveTowards(pEmitter.maxSize, 0, 0.1f);
                        if (pEmitter.maxSize == 0)
                        {
                            pEmitter.emit = false;
                        }
                    }
                }
            }

            if (Time.time - startTime > 0.1f + stayTime)
            {
                currPosition = transform.position;
                float dist = (currPosition - prevPosition).magnitude;
                Ray ray = new Ray(prevPosition, currPosition - prevPosition);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, dist, 557057))
                {
                    Part hitPart = null;
                    try
                    {
                        hitPart = hit.collider.gameObject.GetComponentInParent<Part>();
                    }
                    catch (NullReferenceException)
                    {
                    }


                    if (hitPart == null || (hitPart != null && hitPart.vessel != sourceVessel))
                    {
                        Detonate(hit.point);
                    }
                }
                else if (FlightGlobals.getAltitudeAtPos(transform.position) < 0)
                {
                    Detonate(transform.position);
                }
            }
            else if (FlightGlobals.getAltitudeAtPos(currPosition) <= 0)
            {
                Detonate(currPosition);
            }
            prevPosition = currPosition;

            if (Time.time - startTime > lifeTime)
            {
                Detonate(transform.position);
            }

            //proxy detonation
            if (targetVessel != null &&
                (transform.position - targetVessel.transform.position).sqrMagnitude < 0.5f*blastRadius*blastRadius)
            {
                Detonate(transform.position);
            }
        }

        void Update()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (BDArmorySettings.GameIsPaused)
                {
                    if (audioSource.isPlaying)
                    {
                        audioSource.Stop();
                    }
                }
                else
                {
                    if (!audioSource.isPlaying)
                    {
                        audioSource.Play();
                    }
                }
            }
        }

        void Detonate(Vector3 pos)
        {
            BDArmorySettings.numberOfParticleEmitters--;

            ExplosionFX.CreateExplosion(pos, blastRadius, blastForce, blastHeat, sourceVessel, rb.velocity.normalized,
                explModelPath, explSoundPath);

            foreach (var emitter in pEmitters)
            {
                if (emitter.useWorldSpace)
                {
                    emitter.gameObject.AddComponent<BDAParticleSelfDestruct>();
                    emitter.transform.parent = null;
                }
            }

            GameObject.Destroy(gameObject); //destroy rocket on collision
        }


        void SetupAudio()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.loop = true;
            audioSource.minDistance = 1;
            audioSource.maxDistance = 2000;
            audioSource.dopplerLevel = 0.5f;
            audioSource.volume = 0.9f*BDArmorySettings.BDARMORY_WEAPONS_VOLUME;
            audioSource.pitch = 1f;
            audioSource.priority = 255;
            audioSource.spatialBlend = 1;

            audioSource.clip = GameDatabase.Instance.GetAudioClip("BDArmory/Sounds/rocketLoop");

            UpdateVolume();
            BDArmorySettings.OnVolumeChange += UpdateVolume;
        }

        void UpdateVolume()
        {
            if (this.audioSource)
            {
                audioSource.volume = BDArmorySettings.BDARMORY_WEAPONS_VOLUME;
            }
        }
    }
}