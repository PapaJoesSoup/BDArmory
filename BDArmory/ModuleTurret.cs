using BDArmory.Core;
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
         UI_FloatRange(minValue = 0f, maxValue = 60f, stepIncrement = 1f, scene = UI_Scene.All)]
        public float maxPitch;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Min Pitch"),
         UI_FloatRange(minValue = 1f, maxValue = 0f, stepIncrement = 1f, scene = UI_Scene.All)]
        public float minPitch;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Yaw Range"),
         UI_FloatRange(minValue = 1f, maxValue = 60f, stepIncrement = 1f, scene = UI_Scene.All)]
        public float yawRange;

        [KSPField(isPersistant = true)] public float minPitchLimit = 400;
        [KSPField(isPersistant = true)] public float maxPitchLimit = 400;
        [KSPField(isPersistant = true)] public float yawRangeLimit = 400;

        [KSPField] public bool smoothRotation = false;
        [KSPField] public float smoothMultiplier = 10;

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
                    if (!BDArmorySetup.GameIsPaused && audioRotationRate > 0.05f)
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
            AimInDirection(targetPosition - referenceTransform.position, pitch, yaw);
        }

        public void AimInDirection(Vector3 targetDirection, bool pitch = true, bool yaw = true)
        {
            if (!yawTransform)
            {
                return;
            }

            float deltaTime = Time.fixedDeltaTime;

            Vector3 yawNormal = yawTransform.up;
            Vector3 yawComponent = Vector3.ProjectOnPlane(targetDirection, yawNormal);
            Vector3 pitchNormal = Vector3.Cross(yawComponent, yawNormal);
            Vector3 pitchComponent = Vector3.ProjectOnPlane(targetDirection, pitchNormal);

            float currentYaw = yawTransform.localEulerAngles.y;
            float yawError = VectorUtils.SignedAngleDP(
                Vector3.ProjectOnPlane(referenceTransform.forward, yawNormal), 
                yawComponent, 
                Vector3.Cross(yawNormal, referenceTransform.forward));
            float yawOffset = Mathf.Abs(yawError);
            float targetYawAngle = Mathf.Clamp((currentYaw + yawError + 180f) % 360f - 180f, -yawRange / 2, yawRange / 2); // clamped target yaw

            float currentPitch = -((pitchTransform.localEulerAngles.x + 180f) % 360f - 180f); // from current rotation transform
            float realPitch = 90f - (float)Vector3d.Angle(yawNormal, referenceTransform.forward);
            float targetPitchAngle = 90f - (float)Vector3d.Angle(yawNormal, pitchComponent) + (realPitch - currentPitch); // simple angle from yaw normal
            float pitchOffset = Mathf.Abs(targetPitchAngle - currentPitch);
            targetPitchAngle = Mathf.Clamp(targetPitchAngle, minPitch, maxPitch); // clamp pitch

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

            if (yawRange < 360 && Mathf.Abs(targetYawAngle) > 90 && Mathf.Sign(currentYaw) != Mathf.Sign(targetYawAngle))
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