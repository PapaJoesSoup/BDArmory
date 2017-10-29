using BDArmory.Misc;
using BDArmory.UI;
using UnityEngine;

namespace BDArmory
{
    public class ModuleTurret : PartModule
    {
        [KSPField] public int turretID = 0;


        [KSPField] public string pitchTransformName = "pitchTransform";
        public Transform pitchTransform;

        [KSPField] public string yawTransformName = "yawTransform";
        public Transform yawTransform;


        Transform referenceTransform; //set this to gun's fireTransform

        [KSPField] public float pitchSpeedDPS;
        [KSPField] public float yawSpeedDPS;


        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Max Pitch"),
         UI_FloatRange(minValue = 0f, maxValue = 60f, stepIncrement = 1f, scene = UI_Scene.All)] public float maxPitch;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Min Pitch"),
         UI_FloatRange(minValue = 1f, maxValue = 0f, stepIncrement = 1f, scene = UI_Scene.All)] public float minPitch;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Yaw Range"),
         UI_FloatRange(minValue = 1f, maxValue = 60f, stepIncrement = 1f, scene = UI_Scene.All)] public float yawRange;

        [KSPField(isPersistant = true)] public float minPitchLimit = 400;
        [KSPField(isPersistant = true)] public float maxPitchLimit = 400;
        [KSPField(isPersistant = true)] public float yawRangeLimit = 400;

        [KSPField] public bool smoothRotation = false;
        [KSPField] public float smoothMultiplier = 10;

        float pitchTargetOffset;
        float yawTargetOffset;

        //sfx
        [KSPField] public string audioPath;
        [KSPField] public float maxAudioPitch = 0.5f;
        [KSPField] public float minAudioPitch = 0f;
        [KSPField] public float maxVolume = 1;
        [KSPField] public float minVolume = 0;

        AudioClip soundClip;
        AudioSource audioSource;
        bool hasAudio;
        float audioRotationRate;
        float targetAudioRotationRate;
        Vector3 lastTurretDirection;
        float maxAudioRotRate;


        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            SetupTweakables();


            pitchTransform = part.FindModelTransform(pitchTransformName);
            yawTransform = part.FindModelTransform(yawTransformName);

            if (!pitchTransform)
            {
                Debug.LogWarning(part.partInfo.title + " has no pitchTransform");
            }

            if (!yawTransform)
            {
                Debug.LogWarning(part.partInfo.title + " has no yawTransform");
            }

            if (!referenceTransform)
            {
                SetReferenceTransform(pitchTransform);
            }

            if (!string.IsNullOrEmpty(audioPath) && (yawSpeedDPS != 0 || pitchSpeedDPS != 0))
            {
                soundClip = GameDatabase.Instance.GetAudioClip(audioPath);

                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.clip = soundClip;
                audioSource.loop = true;
                audioSource.dopplerLevel = 0;
                audioSource.minDistance = .5f;
                audioSource.maxDistance = 150;
                audioSource.Play();
                audioSource.volume = 0;
                audioSource.pitch = 0;
                audioSource.priority = 9999;
                audioSource.spatialBlend = 1;

                lastTurretDirection = yawTransform.parent.InverseTransformDirection(pitchTransform.forward);

                maxAudioRotRate = Mathf.Min(yawSpeedDPS, pitchSpeedDPS);

                hasAudio = true;
            }
        }

        void FixedUpdate()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (hasAudio)
                {
                    audioRotationRate = Mathf.Lerp(audioRotationRate, targetAudioRotationRate, 20*Time.fixedDeltaTime);
                    audioRotationRate = Mathf.Clamp01(audioRotationRate);


                    if (audioRotationRate < 0.05f)
                    {
                        audioSource.volume = 0;
                    }
                    else
                    {
                        audioSource.volume = Mathf.Clamp(2f*audioRotationRate,
                            minVolume*BDArmorySettings.BDARMORY_WEAPONS_VOLUME,
                            maxVolume*BDArmorySettings.BDARMORY_WEAPONS_VOLUME);
                        audioSource.pitch = Mathf.Clamp(audioRotationRate, minAudioPitch, maxAudioPitch);
                    }


                    Vector3 tDir = yawTransform.parent.InverseTransformDirection(pitchTransform.forward);
                    float angle = Vector3.Angle(tDir, lastTurretDirection);
                    float rate = Mathf.Clamp01((angle/Time.fixedDeltaTime)/maxAudioRotRate);
                    lastTurretDirection = tDir;

                    targetAudioRotationRate = rate;
                }
            }
        }

        void Update()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (hasAudio)
                {
                    if (!BDArmorySettings.GameIsPaused && audioRotationRate > 0.05f)
                    {
                        if (!audioSource.isPlaying) audioSource.Play();
                    }
                    else
                    {
                        if (audioSource.isPlaying)
                        {
                            audioSource.Stop();
                        }
                    }
                }
            }
        }


        public void AimToTarget(Vector3 targetPosition, bool pitch = true, bool yaw = true)
        {
            if (!yawTransform)
            {
                return;
            }

            float deltaTime = Time.fixedDeltaTime;

            Vector3 localTargetYaw =
                yawTransform.parent.InverseTransformPoint(targetPosition - (yawTargetOffset*pitchTransform.right));
            Vector3 targetYaw = Vector3.ProjectOnPlane(localTargetYaw, Vector3.up);
            float targetYawAngle = VectorUtils.SignedAngle(Vector3.forward, targetYaw, Vector3.right);
            targetYawAngle = Mathf.Clamp(targetYawAngle, -yawRange/2, yawRange/2);

            Quaternion currYawRot = yawTransform.localRotation;
            yawTransform.localRotation = Quaternion.Euler(0, targetYawAngle, 0);
            Vector3 localTargetPitch =
                pitchTransform.parent.InverseTransformPoint(targetPosition - (pitchTargetOffset*pitchTransform.up));
            yawTransform.localRotation = currYawRot;
            localTargetPitch.z = Mathf.Abs(localTargetPitch.z); //prevents from aiming wonky if target is behind
            Vector3 targetPitch = Vector3.ProjectOnPlane(localTargetPitch, Vector3.right);
            float targetPitchAngle = VectorUtils.SignedAngle(Vector3.forward, targetPitch, Vector3.up);
            targetPitchAngle = Mathf.Clamp(targetPitchAngle, minPitch, maxPitch);

            float yawOffset = Vector3.Angle(yawTransform.parent.InverseTransformDirection(yawTransform.forward),
                targetYaw);
            float currentYawSign = Mathf.Sign(Vector3.Dot(yawTransform.localRotation*Vector3.forward, Vector3.right));
            float pitchOffset = Vector3.Angle(pitchTransform.parent.InverseTransformDirection(pitchTransform.forward),
                targetPitch);

            float linPitchMult = yawOffset > 0 ? Mathf.Clamp01((pitchOffset/yawOffset)*(yawSpeedDPS/pitchSpeedDPS)) : 1;
            float linYawMult = pitchOffset > 0 ? Mathf.Clamp01((yawOffset/pitchOffset)*(pitchSpeedDPS/yawSpeedDPS)) : 1;

            float yawSpeed;
            float pitchSpeed;
            if (smoothRotation)
            {
                yawSpeed = Mathf.Clamp(yawOffset*smoothMultiplier, 1f, yawSpeedDPS)*deltaTime;
                pitchSpeed = Mathf.Clamp(pitchOffset*smoothMultiplier, 1f, pitchSpeedDPS)*deltaTime;
            }
            else
            {
                yawSpeed = yawSpeedDPS*deltaTime;
                pitchSpeed = pitchSpeedDPS*deltaTime;
            }

            yawSpeed *= linYawMult;
            pitchSpeed *= linPitchMult;

            if (yawRange < 360 && Mathf.Abs(targetYawAngle) > 90 && currentYawSign != Mathf.Sign(targetYawAngle))
            {
                targetYawAngle = 5*Mathf.Sign(targetYawAngle);
            }

            if (yaw)
                yawTransform.localRotation = Quaternion.RotateTowards(yawTransform.localRotation,
                    Quaternion.Euler(0, targetYawAngle, 0), yawSpeed);
            if (pitch)
                pitchTransform.localRotation = Quaternion.RotateTowards(pitchTransform.localRotation,
                    Quaternion.Euler(-targetPitchAngle, 0, 0), pitchSpeed);
        }

        public bool ReturnTurret()
        {
            if (!yawTransform)
            {
                return false;
            }

            float deltaTime = Time.fixedDeltaTime;

            float yawOffset = Vector3.Angle(yawTransform.forward, yawTransform.parent.forward);
            float pitchOffset = Vector3.Angle(pitchTransform.forward, yawTransform.forward);

            float yawSpeed;
            float pitchSpeed;

            if (smoothRotation)
            {
                yawSpeed = Mathf.Clamp(yawOffset*smoothMultiplier, 1f, yawSpeedDPS)*deltaTime;
                pitchSpeed = Mathf.Clamp(pitchOffset*smoothMultiplier, 1f, pitchSpeedDPS)*deltaTime;
            }
            else
            {
                yawSpeed = yawSpeedDPS*deltaTime;
                pitchSpeed = pitchSpeedDPS*deltaTime;
            }

            float linPitchMult = yawOffset > 0 ? Mathf.Clamp01((pitchOffset/yawOffset)*(yawSpeedDPS/pitchSpeedDPS)) : 1;
            float linYawMult = pitchOffset > 0 ? Mathf.Clamp01((yawOffset/pitchOffset)*(pitchSpeedDPS/yawSpeedDPS)) : 1;

            yawSpeed *= linYawMult;
            pitchSpeed *= linPitchMult;

            yawTransform.localRotation = Quaternion.RotateTowards(yawTransform.localRotation, Quaternion.identity,
                yawSpeed);
            pitchTransform.localRotation = Quaternion.RotateTowards(pitchTransform.localRotation, Quaternion.identity,
                pitchSpeed);

            if (yawTransform.localRotation == Quaternion.identity && pitchTransform.localRotation == Quaternion.identity)
            {
                return true;
            }
            else
            {
                return false;
            }
        }


        public bool TargetInRange(Vector3 targetPosition, float thresholdDegrees, float maxDistance)
        {
            if (!pitchTransform)
            {
                return false;
            }
            bool withinView = Vector3.Angle(targetPosition - pitchTransform.position, pitchTransform.forward) <
                              thresholdDegrees;
            bool withinDistance = (targetPosition - pitchTransform.position).sqrMagnitude < maxDistance*maxDistance;
            return (withinView && withinDistance);
        }

        public void SetReferenceTransform(Transform t)
        {
            referenceTransform = t;
            pitchTargetOffset = pitchTransform.InverseTransformPoint(referenceTransform.position).y;
            yawTargetOffset = yawTransform.InverseTransformPoint(referenceTransform.position).x;
        }

        void SetupTweakables()
        {
            UI_FloatRange minPitchRange = (UI_FloatRange) Fields["minPitch"].uiControlEditor;
            if (minPitchLimit > 90)
            {
                minPitchLimit = minPitch;
            }
            if (minPitchLimit == 0)
            {
                Fields["minPitch"].guiActiveEditor = false;
            }
            minPitchRange.minValue = minPitchLimit;
            minPitchRange.maxValue = 0;

            UI_FloatRange maxPitchRange = (UI_FloatRange) Fields["maxPitch"].uiControlEditor;
            if (maxPitchLimit > 90)
            {
                maxPitchLimit = maxPitch;
            }
            if (maxPitchLimit == 0)
            {
                Fields["maxPitch"].guiActiveEditor = false;
            }
            maxPitchRange.maxValue = maxPitchLimit;
            maxPitchRange.minValue = 0;

            UI_FloatRange yawRangeEd = (UI_FloatRange) Fields["yawRange"].uiControlEditor;
            if (yawRangeLimit > 360)
            {
                yawRangeLimit = yawRange;
            }

            if (yawRangeLimit == 0)
            {
                Fields["yawRange"].guiActiveEditor = false;
            }
            else if (yawRangeLimit < 0)
            {
                yawRangeEd.minValue = 0;
                yawRangeEd.maxValue = 360;

                if (yawRange < 0) yawRange = 360;
            }
            else
            {
                yawRangeEd.minValue = 0;
                yawRangeEd.maxValue = yawRangeLimit;
            }
        }
    }
}