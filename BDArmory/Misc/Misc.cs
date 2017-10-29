using System;
using System.Collections.Generic;
using System.Reflection;
using BDArmory.Parts;
using BDArmory.Radar;
using BDArmory.UI;
using UniLinq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BDArmory.Misc
{
    public static class Misc
    {
        public static Color ParseColor255(string color)
        {
            Color outputColor = new Color(0, 0, 0, 1);

            string[] strings = color.Split(","[0]);
            for (int i = 0; i < 4; i++)
            {
                outputColor[i] = Single.Parse(strings[i])/255;
            }

            return outputColor;
        }

        public static AnimationState[] SetUpAnimation(string animationName, Part part) //Thanks Majiir!
        {
            List<AnimationState> states = new List<AnimationState>();
            IEnumerator<UnityEngine.Animation> animation = part.FindModelAnimators(animationName).AsEnumerable().GetEnumerator();
            while (animation.MoveNext())
            {
                if (animation.Current == null) continue;
                AnimationState animationState = animation.Current[animationName];
                animationState.speed = 0;
                animationState.enabled = true;
                animationState.wrapMode = WrapMode.ClampForever;
                animation.Current.Blend(animationName);
                states.Add(animationState);
            }
            animation.Dispose();
            return states.ToArray();
        }

        public static AnimationState SetUpSingleAnimation(string animationName, Part part)
        {
            IEnumerator<UnityEngine.Animation> animation = part.FindModelAnimators(animationName).AsEnumerable().GetEnumerator();
            while (animation.MoveNext())
            {
                if (animation.Current == null) continue;
                AnimationState animationState = animation.Current[animationName];
                animationState.speed = 0;
                animationState.enabled = true;
                animationState.wrapMode = WrapMode.ClampForever;
                animation.Current.Blend(animationName);
                return animationState;
            }
            animation.Dispose();
            return null;
        }

        public static bool CheckMouseIsOnGui()
        {

            if (!BDArmorySettings.GAME_UI_ENABLED) return false;

            if (!BDInputSettingsFields.WEAP_FIRE_KEY.inputString.Contains("mouse")) return false;

            Vector3 inverseMousePos = new Vector3(Input.mousePosition.x, Screen.height - Input.mousePosition.y, 0);
            Rect topGui = new Rect(0, 0, Screen.width, 65);


            if (topGui.Contains(inverseMousePos)) return true;
            if (BDArmorySettings.toolbarGuiEnabled && BDArmorySettings.WindowRectToolbar.Contains(inverseMousePos))
                return true;
            if (ModuleTargetingCamera.windowIsOpen && ModuleTargetingCamera.camWindowRect.Contains(inverseMousePos))
                return true;
            if (BDArmorySettings.Instance.ActiveWeaponManager)
            {
                MissileFire wm = BDArmorySettings.Instance.ActiveWeaponManager;

                if (wm.vesselRadarData && wm.vesselRadarData.guiEnabled)
                {
                    if (VesselRadarData.radarWindowRect.Contains(inverseMousePos)) return true;
                    if (wm.vesselRadarData.linkWindowOpen && wm.vesselRadarData.linkWindowRect.Contains(inverseMousePos))
                        return true;
                }
                if (wm.rwr && wm.rwr.rwrEnabled && BDArmorySettings.WindowRectRwr.Contains(inverseMousePos))
                    return true;
                if (wm.wingCommander && wm.wingCommander.showGUI)
                {
                    if (wm.wingCommander.guiWindowRect.Contains(inverseMousePos)) return true;
                    if (wm.wingCommander.showAGWindow && wm.wingCommander.agWindowRect.Contains(inverseMousePos))
                        return true;
                }

                if (extraGUIRects != null)
                {
                    for (int i = 0; i < extraGUIRects.Count; i++)
                    {
                        if (extraGUIRects[i].Contains(inverseMousePos)) return true;
                    }
                }
            }

            return false;
        }

        public static List<Rect> extraGUIRects;

        public static int RegisterGUIRect(Rect rect)
        {
            if (extraGUIRects == null)
            {
                extraGUIRects = new List<Rect>();
            }

            int index = extraGUIRects.Count;
            extraGUIRects.Add(rect);
            return index;
        }

        public static void UpdateGUIRect(Rect rect, int index)
        {
            if (extraGUIRects == null)
            {
                Debug.LogWarning("Trying to update a GUI rect for mouse position check, but Rect list is null.");
            }

            extraGUIRects[index] = rect;
        }

        public static bool MouseIsInRect(Rect rect)
        {
            Vector3 inverseMousePos = new Vector3(Input.mousePosition.x, Screen.height - Input.mousePosition.y, 0);
            return rect.Contains(inverseMousePos);
        }

        //Thanks FlowerChild
        //refreshes part action window
        public static void RefreshAssociatedWindows(Part part)
        {
            IEnumerator<UIPartActionWindow> window = Object.FindObjectsOfType(typeof(UIPartActionWindow)).Cast<UIPartActionWindow>().GetEnumerator();
            while (window.MoveNext())
            {
                if (window.Current == null) continue;
                if (window.Current.part == part)
                {
                    window.Current.displayDirty = true;
                }
            }
            window.Dispose();
        }

        public static Vector3 ProjectOnPlane(Vector3 point, Vector3 planePoint, Vector3 planeNormal)
        {
            planeNormal = planeNormal.normalized;

            Plane plane = new Plane(planeNormal, planePoint);
            float distance = plane.GetDistanceToPoint(point);

            return point - (distance*planeNormal);
        }

        public static float SignedAngle(Vector3 fromDirection, Vector3 toDirection, Vector3 referenceRight)
        {
            float angle = Vector3.Angle(fromDirection, toDirection);
            float sign = Mathf.Sign(Vector3.Dot(toDirection, referenceRight));
            float finalAngle = sign*angle;
            return finalAngle;
        }

        /// <summary>
        /// Parses the string to a curve.
        /// Format: "key:pair,key:pair"
        /// </summary>
        /// <returns>The curve.</returns>
        /// <param name="curveString">Curve string.</param>
        public static FloatCurve ParseCurve(string curveString)
        {
            string[] pairs = curveString.Split(new char[] {','});
            Keyframe[] keys = new Keyframe[pairs.Length];
            for (int p = 0; p < pairs.Length; p++)
            {
                string[] pair = pairs[p].Split(new char[] {':'});
                keys[p] = new Keyframe(float.Parse(pair[0]), float.Parse(pair[1]));
            }

            FloatCurve curve = new FloatCurve(keys);

            return curve;
        }

        public static bool CheckSightLine(Vector3 origin, Vector3 target, float maxDistance, float threshold,
            float startDistance)
        {
            float dist = maxDistance;
            Ray ray = new Ray(origin, target - origin);
            ray.origin += ray.direction*startDistance;
            RaycastHit rayHit;
            if (Physics.Raycast(ray, out rayHit, dist, 688129))
            {
                if ((target - rayHit.point).sqrMagnitude < threshold*threshold)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            return false;
        }

        public static bool CheckSightLineExactDistance(Vector3 origin, Vector3 target, float maxDistance,
            float threshold, float startDistance)
        {
            float dist = maxDistance;
            Ray ray = new Ray(origin, target - origin);
            ray.origin += ray.direction*startDistance;
            RaycastHit rayHit;

            if (Physics.Raycast(ray, out rayHit, dist, 688129))
            {
                if ((target - rayHit.point).sqrMagnitude < threshold*threshold)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            return true;
        }


        public static float[] ParseToFloatArray(string floatString)
        {
            string[] floatStrings = floatString.Split(new char[] {','});
            float[] floatArray = new float[floatStrings.Length];
            for (int i = 0; i < floatStrings.Length; i++)
            {
                floatArray[i] = float.Parse(floatStrings[i]);
            }

            return floatArray;
        }

        public static string FormattedGeoPos(Vector3d geoPos, bool altitude)
        {
            string finalString = string.Empty;
            //lat
            double lat = geoPos.x;
            double latSign = Math.Sign(lat);
            double latMajor = latSign*Math.Floor(Math.Abs(lat));
            double latMinor = 100*(Math.Abs(lat) - Math.Abs(latMajor));
            string latString = latMajor.ToString("0") + " " + latMinor.ToString("0.000");
            finalString += "N:" + latString;


            //longi
            double longi = geoPos.y;
            double longiSign = Math.Sign(longi);
            double longiMajor = longiSign*Math.Floor(Math.Abs(longi));
            double longiMinor = 100*(Math.Abs(longi) - Math.Abs(longiMajor));
            string longiString = longiMajor.ToString("0") + " " + longiMinor.ToString("0.000");
            finalString += " E:" + longiString;

            if (altitude)
            {
                finalString += " ASL:" + geoPos.z.ToString("0.000");
            }

            return finalString;
        }

        public static string FormattedGeoPosShort(Vector3d geoPos, bool altitude)
        {
            string finalString = string.Empty;
            //lat
            double lat = geoPos.x;
            double latSign = Math.Sign(lat);
            double latMajor = latSign*Math.Floor(Math.Abs(lat));
            double latMinor = 100*(Math.Abs(lat) - Math.Abs(latMajor));
            string latString = latMajor.ToString("0") + " " + latMinor.ToString("0");
            finalString += "N:" + latString;


            //longi
            double longi = geoPos.y;
            double longiSign = Math.Sign(longi);
            double longiMajor = longiSign*Math.Floor(Math.Abs(longi));
            double longiMinor = 100*(Math.Abs(longi) - Math.Abs(longiMajor));
            string longiString = longiMajor.ToString("0") + " " + longiMinor.ToString("0");
            finalString += " E:" + longiString;

            if (altitude)
            {
                finalString += " ASL:" + geoPos.z.ToString("0");
            }

            return finalString;
        }


        public static KeyBinding AGEnumToKeybinding(KSPActionGroup group)
        {
            string groupName = group.ToString();
            if (groupName.Contains("Custom"))
            {
                groupName = groupName.Substring(6);
                int customNumber = int.Parse(groupName);
                groupName = "CustomActionGroup" + customNumber;
            }
            else
            {
                return null;
            }

            FieldInfo field = typeof(GameSettings).GetField(groupName);
            return (KeyBinding) field.GetValue(null);
        }
    }
}