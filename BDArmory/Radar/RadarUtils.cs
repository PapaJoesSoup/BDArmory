using BDArmory.Core.Extension;
using BDArmory.CounterMeasure;
using BDArmory.Misc;
using BDArmory.Parts;
using BDArmory.Shaders;
using BDArmory.UI;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BDArmory.Radar
{
	public static class RadarUtils
	{
        private static bool rcsSetupCompleted = false;
        private static int radarResolution = 128;

        private static RenderTexture rcsRenderingFrontal;
        private static RenderTexture rcsRenderingLateral;
        private static RenderTexture rcsRenderingVentral;
        private static Camera radarCam;

        private static Texture2D drawTextureFrontal;
        public static Texture2D GetTextureFrontal { get { return drawTextureFrontal; } }
        private static Texture2D drawTextureLateral;
        public static Texture2D GetTextureLateral { get { return drawTextureLateral; } }
        private static Texture2D drawTextureVentral;
        public static Texture2D GetTextureVentral { get { return drawTextureVentral; } }
        // additional anti-exploit 45° offset renderings
        private static Texture2D drawTextureFrontal45;
        public static Texture2D GetTextureFrontal45 { get { return drawTextureFrontal45; } }
        private static Texture2D drawTextureLateral45;
        public static Texture2D GetTextureLateral45 { get { return drawTextureLateral45; } }
        private static Texture2D drawTextureVentral45;
        public static Texture2D GetTextureVentral45 { get { return drawTextureVentral45; } }

        internal static float rcsFrontal;             // internal so that editor analysis window has access to the details
        internal static float rcsLateral;             // dito
        internal static float rcsVentral;             // dito
        internal static float rcsFrontal45;             // dito
        internal static float rcsLateral45;             // dito
        internal static float rcsVentral45;             // dito
        internal static float rcsTotal;               // dito

        internal const float RCS_NORMALIZATION_FACTOR = 16.0f;       //IMPORTANT FOR RCS CALCULATION! DO NOT CHANGE! (1x1 structural panel frontally facing should yield 1 m^2 of rcs!)
        internal const float RCS_MISSILES = 999f;                    //default rcs value for missiles if not configured in the part config
        internal const float RWR_PING_RANGE_FACTOR = 2.0f;
        internal const float RADAR_IGNORE_DISTANCE_SQR = 100f;
        internal const float ACTIVE_MISSILE_PING_PERISTS_TIME = 0.2f;
        internal const float MISSILE_DEFAULT_LOCKABLE_RCS = 5f;


        /// <summary>
        /// Get a vessel radar siganture, including all modifiers (ECM, stealth, ...)
        /// </summary>
        public static TargetInfo GetVesselRadarSignature(Vessel v)
        {
            //1. baseSig = GetVesselRadarCrossSection
            TargetInfo ti = GetVesselRadarCrossSection(v);

            //2. modifiedSig = GetVesselModifiedSignature(baseSig)    //ECM-jammers with rcs reduction effect; other rcs reductions (stealth)
            float modifiedSig = GetVesselModifiedSignature(v, ti);

            return ti;
        }


        /// <summary>
        /// Internal method: get a vessel base radar signature
        /// </summary>
        private static TargetInfo GetVesselRadarCrossSection(Vessel v)
        {
            //read vesseltargetinfo, or render against radar cameras    
            TargetInfo ti = v.gameObject.GetComponent<TargetInfo>();

            if (ti == null)
            {
                // add targetinfo to vessel
                ti = v.gameObject.AddComponent<TargetInfo>();
            }

            if (ti.isMissile)
            {
                // missile handling: get signature from missile config, unless it is radaractive, then use old legacy special handling.
                // LEGACY special handling missile: should always be detected, hence signature is set to maximum
                MissileBase missile = ti.MissileBaseModule;
                if (missile != null)
                {
                    if (missile.ActiveRadar)
                        ti.radarBaseSignature = RCS_MISSILES;
                    else
                        ti.radarBaseSignature = missile.missileRadarCrossSection;

                    ti.radarBaseSignatureNeedsUpdate = false;
                }
            }

            if (ti.radarBaseSignature == -1 || ti.radarBaseSignatureNeedsUpdate)
            {
                // is it just some debris? then dont bother doing a real rcs rendering and just fake it with the parts mass
                if (v.vesselType == VesselType.Debris && !v.IsControllable)
                {
                    ti.radarBaseSignature = v.GetTotalMass();
                }
                else
                {
                    // perform radar rendering to obtain base cross section
                    ti.radarBaseSignature = RenderVesselRadarSnapshot(v, v.transform);

                }

                ti.radarBaseSignatureNeedsUpdate = false;
                ti.alreadyScheduledRCSUpdate = false;
            }

            return ti;
        }


        /// <summary>
        /// Internal method: get a vessels siganture modifiers (ecm, stealth, ...)
        /// </summary>
        private static float GetVesselModifiedSignature(Vessel v, TargetInfo ti)
        {
            ti.radarModifiedSignature = ti.radarBaseSignature;
            ti.radarLockbreakFactor = 1;

            // 
            // read vessel ecminfo for active jammers and calculate effects:
            VesselECMJInfo vesseljammer = v.gameObject.GetComponent<VesselECMJInfo>();
            if (vesseljammer)
            {
                //1) read vessel ecminfo for jammers with RCS reduction effect and multiply factor
                ti.radarModifiedSignature *= vesseljammer.rcsReductionFactor;

                //2) increase in detectability relative to jammerstrength and vessel rcs signature:
                // rcs_factor = jammerStrength / modifiedSig / 100 + 1.0f
                ti.radarModifiedSignature *= (((vesseljammer.jammerStrength / ti.radarModifiedSignature) / 100) + 1.0f);

                //3) garbling due to overly strong jamming signals relative to jammer's strength in relation to vessel rcs signature:
                // jammingDistance =  (jammerstrength / baseSig / 100 + 1.0) x js
                ti.radarJammingDistance = ((vesseljammer.jammerStrength / ti.radarBaseSignature / 100) + 1.0f) * vesseljammer.jammerStrength;

                //4) lockbreaking strength relative to jammer's lockbreak strength in relation to vessel rcs signature:
                // lockbreak_factor = baseSig/modifiedSig x (1 – lopckBreakStrength/baseSig/100)                
                ti.radarLockbreakFactor = (ti.radarBaseSignature / ti.radarModifiedSignature) * (1 - (vesseljammer.lockBreakStrength / ti.radarBaseSignature / 100));
            }

            return ti.radarModifiedSignature;
        }


        /// <summary>
        /// Get vessel chaff factor
        /// </summary>
        public static float GetVesselChaffFactor(Vessel v)
        {
            float chaffFactor = 1.0f;

            // read vessel ecminfo for active lockbreaking jammers
            VesselChaffInfo vci = v.gameObject.GetComponent<VesselChaffInfo>();

            if (vci)
            {
                // lockbreaking strength relative to jammer's lockbreak strength in relation to vessel rcs signature:
                // lockbreak_factor = baseSig/modifiedSig x (1 – lopckBreakStrength/baseSig/100)
                chaffFactor = vci.GetChaffMultiplier();
            }

            return chaffFactor;
        }


        /// <summary>
        /// Get a vessel ecm jamming area (in m) where radar display garbling occurs
        /// </summary>
        public static float GetVesselECMJammingDistance(Vessel v)
        {
            float jammingDistance = 0f;

            if (v == null)
                return jammingDistance;

            jammingDistance = GetVesselRadarCrossSection(v).radarJammingDistance;

            return jammingDistance;
        }


        /// <summary>
        /// Internal method: do the actual radar snapshot rendering from 3 sides and store it in a vesseltargetinfo attached to the vessel
        /// 
        /// Note: Transform t is passed separatedly (instead of using v.transform), as the method need to be called from the editor
        ///         and there we dont have a VESSEL, only a SHIPCONSTRUCT, so the EditorRcSWindow passes the transform separately.
        /// </summary>
        /// <param name="inEditorZoom">when true, we try to make the rendered vessel fill the rendertexture completely, for a better detailed view. This does skew the computed cross section, so it is only for a good visual in editor!</param>
        public static float RenderVesselRadarSnapshot(Vessel v, Transform t, bool inEditorZoom = false)
        {
            const float radarDistance = 1000f;
            const float radarFOV = 2.0f;
            Vector3 presentationPosition = -t.forward * radarDistance;

            SetupResources();

            //move vessel up for clear rendering shot (only if outside editor and thus vessel is a real vessel)
            if (HighLogic.LoadedSceneIsFlight)
                v.SetPosition(v.transform.position + presentationPosition);

            Bounds vesselbounds = CalcVesselBounds(v, t);
            if (BDArmorySettings.DRAW_DEBUG_LABELS)
            {
                if (HighLogic.LoadedSceneIsFlight)
                    Debug.Log($"[BDArmory]: Rendering radar snapshot of vessel {v.name}, type {v.vesselType}");
                else
                    Debug.Log("[BDArmory]: Rendering radar snapshot of vessel");
                Debug.Log("[BDArmory]: - bounds: " + vesselbounds.ToString());
                //Debug.Log("[BDArmory]: - size: " + vesselbounds.size + ", magnitude: " + vesselbounds.size.magnitude);
            }

            if (vesselbounds.size.sqrMagnitude == 0f)
            {
                // SAVE US THE RENDERING, result will be zero anyway...
                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                {
                    Debug.Log("[BDArmory]: - rcs is zero.");
                }

                // revert presentation (only if outside editor and thus vessel is a real vessel)
                if (HighLogic.LoadedSceneIsFlight)
                    v.SetPosition(v.transform.position - presentationPosition);

                return 0f;
            }

            // pass1: frontal
            RenderSinglePass(t, inEditorZoom, t.up, vesselbounds, radarDistance, radarFOV, rcsRenderingFrontal, drawTextureFrontal);
            // pass2: lateral
            RenderSinglePass(t, inEditorZoom, t.right, vesselbounds, radarDistance, radarFOV, rcsRenderingLateral, drawTextureLateral);
            // pass3: Ventral
            RenderSinglePass(t, inEditorZoom, t.forward, vesselbounds, radarDistance, radarFOV, rcsRenderingVentral, drawTextureVentral);

            //additional 45° offset renderings:
            RenderSinglePass(t, inEditorZoom, (t.up+t.right), vesselbounds, radarDistance, radarFOV, rcsRenderingFrontal, drawTextureFrontal45);
            RenderSinglePass(t, inEditorZoom, (t.right+t.forward), vesselbounds, radarDistance, radarFOV, rcsRenderingLateral, drawTextureLateral45);
            RenderSinglePass(t, inEditorZoom, (t.forward-t.up), vesselbounds, radarDistance, radarFOV, rcsRenderingVentral, drawTextureVentral45);


            // revert presentation (only if outside editor and thus vessel is a real vessel)
            if (HighLogic.LoadedSceneIsFlight)
                v.SetPosition(v.transform.position - presentationPosition);

            // Count pixel colors to determine radar returns (only for normal non-zoomed rendering!)
            if (!inEditorZoom)
            {
                rcsFrontal = 0;
                rcsLateral = 0;
                rcsVentral = 0;
                rcsFrontal45 = 0;
                rcsLateral45 = 0;
                rcsVentral45 = 0;

                for (int x = 0; x < radarResolution; x++)
                {
                    for (int y = 0; y < radarResolution; y++)
                    {
                        rcsFrontal += drawTextureFrontal.GetPixel(x, y).maxColorComponent;
                        rcsLateral += drawTextureLateral.GetPixel(x, y).maxColorComponent;
                        rcsVentral += drawTextureVentral.GetPixel(x, y).maxColorComponent;

                        rcsFrontal45 += drawTextureFrontal45.GetPixel(x, y).maxColorComponent;
                        rcsLateral45 += drawTextureLateral45.GetPixel(x, y).maxColorComponent;
                        rcsVentral45 += drawTextureVentral45.GetPixel(x, y).maxColorComponent;
                    }
                }

                // normalize rcs value, so that the structural 1x1 panel facing the radar exactly gives a return of 1 m^2:
                rcsFrontal /= RCS_NORMALIZATION_FACTOR;
                rcsLateral /= RCS_NORMALIZATION_FACTOR;
                rcsVentral /= RCS_NORMALIZATION_FACTOR;

                rcsFrontal45 /= RCS_NORMALIZATION_FACTOR;
                rcsLateral45 /= RCS_NORMALIZATION_FACTOR;
                rcsVentral45 /= RCS_NORMALIZATION_FACTOR;

                rcsTotal = (Mathf.Max(rcsFrontal, rcsFrontal45) + Mathf.Max(rcsLateral, rcsLateral45) + Mathf.Max(rcsVentral, rcsVentral45)) / 3f;
                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                {
                    Debug.Log($"[BDArmory]: - Vessel rcs is (frontal/lateral/ventral), (frontal45/lateral45/ventral45): {rcsFrontal}/{rcsLateral}/{rcsVentral}, {rcsFrontal45}/{rcsLateral45}/{rcsVentral45} = rcsTotal: {rcsTotal}");
                }
            }

            return rcsTotal;
        }


        /// <summary>
        /// Internal helpder method
        /// </summary>
        private static void RenderSinglePass(Transform t, bool inEditorZoom, Vector3 cameraDirection, Bounds vesselbounds, float radarDistance, float radarFOV, RenderTexture rcsRendering, Texture2D rcsTexture)
        {
            // Render one snapshop pass:
            // setup camera FOV
            radarCam.transform.position = vesselbounds.center + cameraDirection * radarDistance;
            radarCam.transform.LookAt(vesselbounds.center, -t.forward);
            float distanceToShip = Vector3.Distance(radarCam.transform.position, vesselbounds.center);
            radarCam.nearClipPlane = distanceToShip - 200;
            radarCam.farClipPlane = distanceToShip + 200;
            if (inEditorZoom)
                radarCam.fieldOfView = Mathf.Atan(vesselbounds.size.magnitude / distanceToShip) * 180 / Mathf.PI;
            else
                radarCam.fieldOfView = radarFOV;
            // setup rendertexture
            radarCam.targetTexture = rcsRendering;
            RenderTexture.active = rcsRendering;
            Shader.SetGlobalVector("_LIGHTDIR", -cameraDirection);
            radarCam.RenderWithShader(BDAShaderLoader.RCSShader, string.Empty);
            rcsTexture.ReadPixels(new Rect(0, 0, radarResolution, radarResolution), 0, 0);
            rcsTexture.Apply();
        }


        /// <summary>
        /// Internal method: get a vessel's bounds
        /// Method implemention adapted from kronal vessel viewer
        /// </summary>
        private static Bounds CalcVesselBounds(Vessel v, Transform t)
        {
            Bounds result = new Bounds(t.position, Vector3.zero);

            List<Part>.Enumerator vp = v.Parts.GetEnumerator();
            while (vp.MoveNext())
            {
                if (vp.Current.collider && !vp.Current.Modules.Contains("LaunchClamp"))
                {
                    result.Encapsulate(vp.Current.collider.bounds);
                }
            }
            vp.Dispose();

            return result;
        }


        /// <summary>
        /// Internal method: get a vessel's size (based on it's bounds)
        /// Method implemention adapted from kronal vessel viewer
        /// </summary>
        private static Vector3 GetVesselSize(Vessel v, Transform t)
        {
            return CalcVesselBounds(v, t).size;
        }


        /// <summary>
        /// Initialization of required resources. Necessary once per scene.
        /// </summary>
        public static void SetupResources()
		{
            if (!rcsSetupCompleted)
            {
                //set up rendertargets and textures
                rcsRenderingFrontal = new RenderTexture(radarResolution, radarResolution, 16);
                rcsRenderingLateral = new RenderTexture(radarResolution, radarResolution, 16);
                rcsRenderingVentral = new RenderTexture(radarResolution, radarResolution, 16);
                drawTextureFrontal = new Texture2D(radarResolution, radarResolution, TextureFormat.RGB24, false);
                drawTextureLateral = new Texture2D(radarResolution, radarResolution, TextureFormat.RGB24, false);
                drawTextureVentral = new Texture2D(radarResolution, radarResolution, TextureFormat.RGB24, false);
                drawTextureFrontal45 = new Texture2D(radarResolution, radarResolution, TextureFormat.RGB24, false);
                drawTextureLateral45 = new Texture2D(radarResolution, radarResolution, TextureFormat.RGB24, false);
                drawTextureVentral45 = new Texture2D(radarResolution, radarResolution, TextureFormat.RGB24, false);

                rcsSetupCompleted = true;
            }

            if (radarCam == null)
            {
                //set up camera
                radarCam = (new GameObject("RadarCamera")).AddComponent<Camera>();
                radarCam.enabled = false;
                radarCam.clearFlags = CameraClearFlags.SolidColor;
                radarCam.backgroundColor = Color.black;
                radarCam.cullingMask = 1 << 0;   // only layer 0 active, see: http://wiki.kerbalspaceprogram.com/wiki/API:Layers
            }
        }


        /// <summary>
        /// Release of acquired resources. Necessary once at end of scene.
        /// </summary>
        public static void CleanupResources()
        {
            if (rcsSetupCompleted)
            {
                RenderTexture.Destroy(rcsRenderingFrontal);
                RenderTexture.Destroy(rcsRenderingLateral);
                RenderTexture.Destroy(rcsRenderingVentral);
                Texture2D.Destroy(drawTextureFrontal);
                Texture2D.Destroy(drawTextureLateral);
                Texture2D.Destroy(drawTextureVentral);
                Texture2D.Destroy(drawTextureFrontal45);
                Texture2D.Destroy(drawTextureLateral45);
                Texture2D.Destroy(drawTextureVentral45);
                GameObject.Destroy(radarCam);
                rcsSetupCompleted = false;
            }
        }


        /// <summary>
        /// Determine for a vesselposition relative to the radar position how much effect the ground clutter factor will have.
        /// </summary>
        public static float GetRadarGroundClutterModifier(ModuleRadar radar, Transform referenceTransform, Vector3 position, Vector3 vesselposition, TargetInfo ti)
        {
            Vector3 upVector = referenceTransform.up;

            //ground clutter factor when looking down:
            Vector3 targetDirection = (vesselposition - position);
            float angleFromUp = Vector3.Angle(targetDirection, upVector);
            float lookDownAngle = angleFromUp - 90; // result range: -90 .. +90
            Mathf.Clamp(lookDownAngle, 0, 90);      // result range:   0 .. +90

            float groundClutterMutiplier = Mathf.Lerp(1, radar.radarGroundClutterFactor, (lookDownAngle / 90));

            //additional ground clutter factor when target is landed/splashed:
            if (ti.isLanded || ti.isSplashed)
                groundClutterMutiplier *= radar.radarGroundClutterFactor;

            return groundClutterMutiplier;
        }


        /// <summary>
        /// Special scanning method that needs to be set manually on the radar: perform fixed boresight scan with locked fov.
        /// Called from ModuleRadar, which will then attempt to immediately lock onto the detected targets.
        /// Uses detectionCurve for rcs evaluation.
        /// </summary>
        //was: public static void UpdateRadarLock(Ray ray, float fov, float minSignature, ref TargetSignatureData[] dataArray, float dataPersistTime, bool pingRWR, RadarWarningReceiver.RWRThreatTypes rwrType, bool radarSnapshot)
        public static bool RadarUpdateScanBoresight(Ray ray, float fov, ref TargetSignatureData[] dataArray, float dataPersistTime, ModuleRadar radar)
        {
            int dataIndex = 0;
            bool hasLocked = false;

            // guard clauses
            if (!radar)
                return false;

            List<Vessel>.Enumerator loadedvessels = BDATargetManager.LoadedVessels.GetEnumerator();
            while (loadedvessels.MoveNext())
            {
                // ignore null, unloaded
                if (loadedvessels.Current == null) continue;
                if (!loadedvessels.Current.loaded) continue;

                // ignore self, ignore behind ray
                Vector3 vectorToTarget = (loadedvessels.Current.transform.position - ray.origin);
                if (((vectorToTarget).sqrMagnitude < RADAR_IGNORE_DISTANCE_SQR) ||
                     (Vector3.Dot(vectorToTarget, ray.direction) < 0))
                    continue;

                if (Vector3.Angle(loadedvessels.Current.CoM - ray.origin, ray.direction) < fov / 2f)
                {
                    // ignore when blocked by terrain
                    if (TerrainCheck(ray.origin, loadedvessels.Current.transform.position))
                        continue;

                    // get vessel's radar signature
                    TargetInfo ti = GetVesselRadarSignature(loadedvessels.Current);
                    float signature = ti.radarModifiedSignature;
                    signature *= GetRadarGroundClutterModifier(radar, radar.referenceTransform, ray.origin, loadedvessels.Current.CoM, ti);
                    // no ecm lockbreak factor here
                    // no chaff factor here

                    // evaluate range
                    float distance = (loadedvessels.Current.CoM - ray.origin).magnitude / 1000f;                                      //TODO: Performance! better if we could switch to sqrMagnitude...
                    if (distance > radar.radarMinDistanceDetect && distance < radar.radarMaxDistanceDetect)
                    {
                        //evaluate if we can detect such a signature at that range
                        float minDetectSig = radar.radarDetectionCurve.Evaluate(distance);

                        if (signature > minDetectSig)
                        {
                            // detected by radar
                            // fill attempted locks array for locking later:
                            while (dataIndex < dataArray.Length - 1)
                            {
                                if (!dataArray[dataIndex].exists || (dataArray[dataIndex].exists && (Time.time - dataArray[dataIndex].timeAcquired) > dataPersistTime))
                                {
                                    break;
                                }
                                dataIndex++;
                            }

                            if (dataIndex < dataArray.Length)
                            {
                                dataArray[dataIndex] = new TargetSignatureData(loadedvessels.Current, signature);
                                dataIndex++;
                                hasLocked = true;
                            }
                        }
                    }

                    //  our radar ping can be received at a higher range than we can detect, according to RWR range ping factor:
                    if (distance < radar.radarMaxDistanceDetect * RWR_PING_RANGE_FACTOR)
                        RadarWarningReceiver.PingRWR(loadedvessels.Current, ray.origin, radar.rwrType, radar.signalPersistTimeForRwr);
                }

            }
            loadedvessels.Dispose();

            return hasLocked;
        }


        /// <summary>
        /// Special scanning method for missiles with active radar homing.
        /// Called from MissileBase / MissileLauncher, which will then attempt to immediately lock onto the detected targets.
        /// Uses the missiles locktrackCurve for rcs evaluation.
        /// </summary>
        //was: UpdateRadarLock(ray, maxOffBoresight, activeRadarMinThresh, ref scannedTargets, 0.4f, true, RadarWarningReceiver.RWRThreatTypes.MissileLock, true);
        public static bool RadarUpdateMissileLock(Ray ray, float fov, ref TargetSignatureData[] dataArray, float dataPersistTime, MissileBase missile)
        {
            int dataIndex = 0;
            bool hasLocked = false;

            // guard clauses
            if (!missile)
                return false;

            List<Vessel>.Enumerator loadedvessels = BDATargetManager.LoadedVessels.GetEnumerator();
            while (loadedvessels.MoveNext())
            {
                // ignore null, unloaded
                if (loadedvessels.Current == null) continue;
                if (!loadedvessels.Current.loaded) continue;

                // ignore self, ignore behind ray
                Vector3 vectorToTarget = (loadedvessels.Current.transform.position - ray.origin);
                if (((vectorToTarget).sqrMagnitude < RADAR_IGNORE_DISTANCE_SQR) ||
                     (Vector3.Dot(vectorToTarget, ray.direction) < 0))
                    continue;

                if (Vector3.Angle(loadedvessels.Current.CoM - ray.origin, ray.direction) < fov / 2f)
                {
                    // ignore when blocked by terrain
                    if (TerrainCheck(ray.origin, loadedvessels.Current.transform.position))
                        continue;

                    // get vessel's radar signature
                    TargetInfo ti = GetVesselRadarSignature(loadedvessels.Current);
                    float signature = ti.radarModifiedSignature;
                    // no ground clutter modifier for missiles
                    signature *= ti.radarLockbreakFactor;    //multiply lockbreak factor from active ecm
                    //do not multiply chaff factor here

                    // evaluate range
                    float distance = (loadedvessels.Current.CoM - ray.origin).magnitude;                                      //TODO: Performance! better if we could switch to sqrMagnitude...
                    if (distance < missile.activeRadarRange)
                    {
                        //evaluate if we can detect such a signature at that range
                        float minDetectSig = missile.activeRadarLockTrackCurve.Evaluate(distance/1000f);

                        if (signature > minDetectSig)
                        {
                            // detected by radar
                            // fill attempted locks array for locking later:
                            while (dataIndex < dataArray.Length - 1)
                            {
                                if (!dataArray[dataIndex].exists || (dataArray[dataIndex].exists && (Time.time - dataArray[dataIndex].timeAcquired) > dataPersistTime))
                                {
                                    break;
                                }
                                dataIndex++;
                            }

                            if (dataIndex < dataArray.Length)
                            {
                                dataArray[dataIndex] = new TargetSignatureData(loadedvessels.Current, signature);
                                dataIndex++;
                                hasLocked = true;
                            }
                        }
                    }

                    //  our radar ping can be received at a higher range than we can detect, according to RWR range ping factor:
                    if (distance < missile.activeRadarRange * RWR_PING_RANGE_FACTOR)
                    {
                        if (missile.GetWeaponClass() == WeaponClasses.SLW)
                            RadarWarningReceiver.PingRWR(loadedvessels.Current, ray.origin, RadarWarningReceiver.RWRThreatTypes.TorpedoLock, ACTIVE_MISSILE_PING_PERISTS_TIME);

                        else
                            RadarWarningReceiver.PingRWR(loadedvessels.Current, ray.origin, RadarWarningReceiver.RWRThreatTypes.MissileLock, ACTIVE_MISSILE_PING_PERISTS_TIME);
                    }
                }

            }
            loadedvessels.Dispose();

            return hasLocked;
        }


        /// <summary>
        /// Main scanning and locking method called from ModuleRadar.
        /// scanning both for omnidirectional and boresight scans.
        /// Uses detectionCurve OR locktrackCurve for rcs evaluation, depending on wether modeTryLock is true or false.
        /// </summary>
        /// <param name="modeTryLock">true: track/lock target; false: scan only</param>
        /// <param name="dataArray">relevant only for modeTryLock=true</param>
        /// <param name="dataPersistTime">optional, relevant only for modeTryLock=true</param>
        /// <returns></returns>
        //was: public static void UpdateRadarLock(MissileFire myWpnManager, float directionAngle, Transform referenceTransform, float fov, Vector3 position, float minSignature, ref TargetSignatureData[] dataArray, float dataPersistTime, bool pingRWR, RadarWarningReceiver.RWRThreatTypes rwrType, bool radarSnapshot)
        public static bool RadarUpdateScanLock(MissileFire myWpnManager, float directionAngle, Transform referenceTransform, float fov, Vector3 position, ModuleRadar radar, bool modeTryLock, ref TargetSignatureData[] dataArray, float dataPersistTime = 0f)
        {
            Vector3 forwardVector = referenceTransform.forward;
            Vector3 upVector = referenceTransform.up;
            Vector3 lookDirection = Quaternion.AngleAxis(directionAngle, upVector) * forwardVector;
            int dataIndex = 0;
            bool hasLocked = false;

            // guard clauses
            if (!myWpnManager || !myWpnManager.vessel || !radar)
                return false;

            List<Vessel>.Enumerator loadedvessels = BDATargetManager.LoadedVessels.GetEnumerator();
            while (loadedvessels.MoveNext())
            {
                // ignore null, unloaded and self
                if (loadedvessels.Current == null) continue;
                if (!loadedvessels.Current.loaded) continue;
                if (loadedvessels.Current == myWpnManager.vessel) continue;

                // ignore too close ones
                if ((loadedvessels.Current.transform.position - position).sqrMagnitude < RADAR_IGNORE_DISTANCE_SQR)
                    continue;

                Vector3 vesselDirection = Vector3.ProjectOnPlane(loadedvessels.Current.CoM - position, upVector);
                if (Vector3.Angle(vesselDirection, lookDirection) < fov / 2f)
                {
                    // ignore when blocked by terrain
                    if (TerrainCheck(referenceTransform.position, loadedvessels.Current.transform.position))
                        continue;

                    // get vessel's radar signature
                    TargetInfo ti = GetVesselRadarSignature(loadedvessels.Current);
                    float signature = ti.radarModifiedSignature;
                    //do not multiply chaff factor here
                    signature *= GetRadarGroundClutterModifier(radar, referenceTransform, position, loadedvessels.Current.CoM, ti);

                    // evaluate range
                    float distance = (loadedvessels.Current.CoM - position).magnitude / 1000f;                                      //TODO: Performance! better if we could switch to sqrMagnitude...

                    if (modeTryLock)    // LOCK/TRACK TARGET:
                    {
                        //evaluate if we can lock/track such a signature at that range
                        if (distance > radar.radarMinDistanceLockTrack && distance < radar.radarMaxDistanceLockTrack)
                        {
                            //evaluate if we can lock/track such a signature at that range
                            float minLockSig = radar.radarLockTrackCurve.Evaluate(distance);
                            signature *= ti.radarLockbreakFactor;    //multiply lockbreak factor from active ecm
                            //do not multiply chaff factor here

                            if (signature > minLockSig)
                            {
                                // detected by radar
                                if (myWpnManager != null)
                                {
                                    BDATargetManager.ReportVessel(loadedvessels.Current, myWpnManager);
                                }

                                // fill attempted locks array for locking later:
                                while (dataIndex < dataArray.Length - 1)
                                {
                                    if (!dataArray[dataIndex].exists || (dataArray[dataIndex].exists && (Time.time - dataArray[dataIndex].timeAcquired) > dataPersistTime))
                                    {
                                        break;
                                    }
                                    dataIndex++;
                                }

                                if (dataIndex < dataArray.Length)
                                {
                                    dataArray[dataIndex] = new TargetSignatureData(loadedvessels.Current, signature);
                                    dataIndex++;
                                    hasLocked = true;
                                }

                            }
                        }

                        //  our radar ping can be received at a higher range than we can lock/track, according to RWR range ping factor:
                        if (distance < radar.radarMaxDistanceLockTrack * RWR_PING_RANGE_FACTOR)
                            RadarWarningReceiver.PingRWR(loadedvessels.Current, position, radar.rwrType, radar.signalPersistTimeForRwr);

                    }
                    else   // SCAN/DETECT TARGETS:
                    {
                        //evaluate if we can detect such a signature at that range
                        if (distance > radar.radarMinDistanceDetect && distance < radar.radarMaxDistanceDetect)
                        {
                            //evaluate if we can detect or lock such a signature at that range
                            float minDetectSig = radar.radarDetectionCurve.Evaluate(distance);
                            //do not consider lockbreak factor from active ecm here!
                            //do not consider chaff here

                            if (signature > minDetectSig)
                            {
                                // detected by radar
                                if (myWpnManager != null)
                                {
                                    BDATargetManager.ReportVessel(loadedvessels.Current, myWpnManager);
                                }

                                // report scanned targets only
                                radar.ReceiveContactData(new TargetSignatureData(loadedvessels.Current, signature), false);
                            }
                        }

                        //  our radar ping can be received at a higher range than we can detect, according to RWR range ping factor:
                        if (distance < radar.radarMaxDistanceDetect * RWR_PING_RANGE_FACTOR)
                            RadarWarningReceiver.PingRWR(loadedvessels.Current, position, radar.rwrType, radar.signalPersistTimeForRwr);
                    }
                    
                }


            }
            loadedvessels.Dispose();

            return hasLocked;
        }


        /// <summary>
        /// Update a lock on a tracked target.
        /// Uses locktrackCurve for rcs evaluation.
        /// </summary>
        //was: public static void UpdateRadarLock(Ray ray, Vector3 predictedPos, float fov, float minSignature, ModuleRadar radar, bool pingRWR, bool radarSnapshot, float dataPersistTime, bool locked, int lockIndex, Vessel lockedVessel)
        public static bool RadarUpdateLockTrack(Ray ray, Vector3 predictedPos, float fov, ModuleRadar radar, float dataPersistTime, bool locked, int lockIndex, Vessel lockedVessel)
        {
            float closestSqrDist = 1000f;

            // guard clauses
            if (!radar)
                return false;

            // first: re-acquire lock if temporarily lost
            if (!lockedVessel)
            {
                List<Vessel>.Enumerator loadedvessels = BDATargetManager.LoadedVessels.GetEnumerator();
                while (loadedvessels.MoveNext())
                {
                    // ignore null, unloaded
                    if (loadedvessels.Current == null) continue;
                    if (!loadedvessels.Current.loaded) continue;

                    // ignore self, ignore behind ray
                    Vector3 vectorToTarget = (loadedvessels.Current.transform.position - ray.origin);
                    if (((vectorToTarget).sqrMagnitude < RADAR_IGNORE_DISTANCE_SQR) ||
                         (Vector3.Dot(vectorToTarget, ray.direction) < 0))
                        continue;

                    if (Vector3.Angle(loadedvessels.Current.CoM - ray.origin, ray.direction) < fov/2)
                    {
                        float sqrDist = Vector3.SqrMagnitude(loadedvessels.Current.CoM - predictedPos);
                        if (sqrDist < closestSqrDist)
                        {
                            // best candidate so far, take it
                            closestSqrDist = sqrDist;
                            lockedVessel = loadedvessels.Current;
                        }
                    }

                }
                loadedvessels.Dispose();
            }

            // second: track that lock
            if (lockedVessel)
            {
                // blocked by terrain?
                if (TerrainCheck(ray.origin, lockedVessel.transform.position))
                {
                    radar.UnlockTargetAt(lockIndex, true);
                    return false;
                }

                // get vessel's radar signature
                TargetInfo ti = GetVesselRadarSignature(lockedVessel);
                float signature = ti.radarModifiedSignature;
                signature *= GetRadarGroundClutterModifier(radar, radar.referenceTransform, ray.origin, lockedVessel.CoM, ti);
                signature *= ti.radarLockbreakFactor;    //multiply lockbreak factor from active ecm
                //do not multiply chaff factor here

                // evaluate range
                float distance = (lockedVessel.CoM - ray.origin).magnitude / 1000f;                                      //TODO: Performance! better if we could switch to sqrMagnitude...
                if (distance > radar.radarMinDistanceLockTrack && distance < radar.radarMaxDistanceLockTrack)
                {
                    //evaluate if we can detect such a signature at that range
                    float minTrackSig = radar.radarLockTrackCurve.Evaluate(distance);

                    if (signature > minTrackSig)
                    {
                        // can be tracked
                        radar.ReceiveContactData(new TargetSignatureData(lockedVessel, signature), locked);
                    }
                    else
                    {
                        // cannot track, so unlock it
                        radar.UnlockTargetAt(lockIndex, true);
                        return false;
                    }
                }

                //  our radar ping can be received at a higher range than we can detect, according to RWR range ping factor:
                if (distance < radar.radarMaxDistanceLockTrack * RWR_PING_RANGE_FACTOR)
                    RadarWarningReceiver.PingRWR(lockedVessel, ray.origin, radar.rwrType, ACTIVE_MISSILE_PING_PERISTS_TIME);

                return true;
            }
            else
            {
                // nothing tracked/locked at this index
                radar.UnlockTargetAt(lockIndex, true);
            }

            return false;
        }


        /// <summary>
        /// Scans for targets in direction with field of view.
        /// (Visual Target acquisition)
        /// </summary>
        public static Vector3 GuardScanInDirection(MissileFire myWpnManager, float directionAngle, Transform referenceTransform, float fov, out ViewScanResults results, float maxDistance)
		{
			fov *= 1.1f;
            results = new ViewScanResults
            {
                foundMissile = false,
                foundHeatMissile = false,
                foundRadarMissile = false,
                foundAGM = false,
                firingAtMe = false,
                missileThreatDistance = float.MaxValue,
                threatVessel = null,
                threatWeaponManager = null
            };

            if (!myWpnManager || !referenceTransform)
			{
				return Vector3.zero;
			}

			Vector3 position = referenceTransform.position;
			//Vector3d geoPos = VectorUtils.WorldPositionToGeoCoords(position, FlightGlobals.currentMainBody);
			Vector3 forwardVector = referenceTransform.forward;
			Vector3 upVector = referenceTransform.up;
			Vector3 lookDirection = Quaternion.AngleAxis(directionAngle, upVector) * forwardVector;


            List<Vessel>.Enumerator loadedvessels = BDATargetManager.LoadedVessels.GetEnumerator();
            while (loadedvessels.MoveNext())
            {
				if(loadedvessels.Current == null) continue;

				if(loadedvessels.Current.loaded)
				{
					if(loadedvessels.Current == myWpnManager.vessel) continue; //ignore self

					Vector3 vesselProjectedDirection = Vector3.ProjectOnPlane(loadedvessels.Current.transform.position-position, upVector);
					Vector3 vesselDirection = loadedvessels.Current.transform.position - position;

					if(Vector3.Dot(vesselDirection, lookDirection) < 0) continue;   //ignore behind

					float vesselDistance = (loadedvessels.Current.transform.position - position).sqrMagnitude;
					if (vesselDistance < maxDistance*maxDistance && Vector3.Angle(vesselProjectedDirection, lookDirection) < fov / 2 && Vector3.Angle(loadedvessels.Current.transform.position-position, -myWpnManager.transform.forward) < myWpnManager.guardAngle/2)
					{
						//Debug.Log("Found vessel: " + vessel.vesselName);
						if (TerrainCheck(referenceTransform.position, loadedvessels.Current.transform.position))
                                continue; //blocked by terrain

						BDATargetManager.ReportVessel(loadedvessels.Current, myWpnManager);

						TargetInfo tInfo;
						if((tInfo = loadedvessels.Current.gameObject.GetComponent<TargetInfo>()))
						{
							if(tInfo.isMissile)
							{
								MissileBase missileBase;
								if(missileBase = tInfo.MissileBaseModule)
								{
									results.foundMissile = true;
									results.threatVessel = missileBase.vessel;
									Vector3 vectorFromMissile = myWpnManager.vessel.CoM - missileBase.part.transform.position;
									Vector3 relV = missileBase.vessel.Velocity() - myWpnManager.vessel.Velocity();
									bool approaching = Vector3.Dot(relV, vectorFromMissile) > 0;
									if(missileBase.HasFired && missileBase.TimeIndex > 1 && approaching && (missileBase.TargetPosition - (myWpnManager.vessel.CoM + (myWpnManager.vessel.Velocity() * Time.fixedDeltaTime))).sqrMagnitude < 3600)
									{
										if(missileBase.TargetingMode == MissileBase.TargetingModes.Heat)
										{
											results.foundHeatMissile = true;
											results.missileThreatDistance = Mathf.Min(results.missileThreatDistance, Vector3.Distance(missileBase.part.transform.position, myWpnManager.part.transform.position));
											results.threatPosition = missileBase.transform.position;
											break;
										}
										else if(missileBase.TargetingMode == MissileBase.TargetingModes.Radar)
										{
											results.foundRadarMissile = true;
											results.missileThreatDistance = Mathf.Min(results.missileThreatDistance, Vector3.Distance(missileBase.part.transform.position, myWpnManager.part.transform.position));
											results.threatPosition = missileBase.transform.position;
										}
										else if(missileBase.TargetingMode == MissileBase.TargetingModes.Laser)
										{
											results.foundAGM = true;
											results.missileThreatDistance = Mathf.Min(results.missileThreatDistance, Vector3.Distance(missileBase.part.transform.position, myWpnManager.part.transform.position));
											break;
										}
									}
									else
									{
										break;
									}
								}
							}
							else
							{
                                List<ModuleWeapon>.Enumerator weapon = loadedvessels.Current.FindPartModulesImplementing<ModuleWeapon>().GetEnumerator();
                                while (weapon.MoveNext())
                                {
									if(!weapon.Current.recentlyFiring) continue;
									if(Vector3.Dot(weapon.Current.fireTransforms[0].forward, vesselDirection) > 0) continue;

									if(Vector3.Angle(weapon.Current.fireTransforms[0].forward, -vesselDirection) < 6500 / vesselDistance && (!results.firingAtMe || (weapon.Current.vessel.ReferenceTransform.position - position).sqrMagnitude < (results.threatPosition - position).sqrMagnitude))
									{
										results.firingAtMe = true;
										results.threatPosition = weapon.Current.vessel.transform.position;
                                        results.threatVessel = weapon.Current.vessel;
                                        results.threatWeaponManager = weapon.Current.weaponManager;
                                        break;
									}
								}
							}
						}
					}
				}
			}

            loadedvessels.Dispose();
            return lookDirection;
		}


        /// <summary>
        /// Helper method: check if line intersects terrain
        /// </summary>
		public static bool TerrainCheck(Vector3 start, Vector3 end)
		{
		    if (!BDArmorySettings.IGNORE_TERRAIN_CHECK)
		    {
		        return Physics.Linecast(start, end, 1 << 15);
		    }
	
		    return false;
		    
		}


        /// <summary>
        /// Helper method: map a position onto the radar display
        /// </summary>
        public static Vector2 WorldToRadar(Vector3 worldPosition, Transform referenceTransform, Rect radarRect, float maxDistance)
		{
			float scale = maxDistance/(radarRect.height/2);
			Vector3 localPosition = referenceTransform.InverseTransformPoint(worldPosition);
			localPosition.y = 0;
			Vector2 radarPos = new Vector2((radarRect.width/2)+(localPosition.x/scale), ((radarRect.height/2)-(localPosition.z/scale)));
			return radarPos;
		}


        /// <summary>
        /// Helper method: map a position onto the radar display (for non-onmi radars)
        /// </summary>
        public static Vector2 WorldToRadarRadial(Vector3 worldPosition, Transform referenceTransform, Rect radarRect, float maxDistance, float maxAngle)
		{
			float scale = maxDistance/(radarRect.height);
			Vector3 localPosition = referenceTransform.InverseTransformPoint(worldPosition);
			localPosition.y = 0;
			float angle = Vector3.Angle(localPosition, Vector3.forward);
			if(localPosition.x < 0) angle = -angle;
			float xPos = (radarRect.width/2) + ((angle/maxAngle)*radarRect.width/2);
			float yPos = radarRect.height - (new Vector2 (localPosition.x, localPosition.z)).magnitude / scale;
			Vector2 radarPos = new Vector2(xPos, yPos);
			return radarPos;
		}


    }
}

