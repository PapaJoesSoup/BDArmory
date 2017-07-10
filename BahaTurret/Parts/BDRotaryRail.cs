using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BahaTurret
{
    public class BDRotaryRail : PartModule
    {
        [KSPField] public float maxLength;

        [KSPField] public float maxHeight;

        [KSPField] public int intervals;

        [KSPField] public float rotationSpeed = 360;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Rails")] public float
            numberOfRails = 8;

        float railAngle = 0;

        [KSPField] public float rotationDelay = 0.15f;

        [KSPField(isPersistant = true)] public int railIndex = 0;


        Dictionary<int, int> missileToRailIndex;
        Dictionary<int, int> railToMissileIndex;

        [KSPField(isPersistant = true)] public float currentHeight = 0;

        [KSPField(isPersistant = true)] public float currentLength = 0;


        public int missileCount = 0;
        MissileLauncher[] missileChildren;
        Transform[] missileTransforms;
        Transform[] missileReferenceTransforms;

        Dictionary<Part, Vector3> comOffsets;


        float lengthInterval;
        float heightInterval;

        List<Transform> rotationTransforms;
        List<Transform> heightTransforms;
        List<Transform> lengthTransforms;
        List<Transform> rails;
        int[] railCounts = new int[] {2, 3, 4, 6, 8};

        [KSPField(isPersistant = true)] public float railCountIndex = 4;

        bool rdyToFire = false;

        public bool readyToFire
        {
            get { return rdyToFire; }
        }

        public MissileLauncher nextMissile = null;

        MissileLauncher rdyMissile = null;

        public MissileLauncher readyMissile
        {
            get { return rdyMissile; }
        }

        MissileFire wm;

        public MissileFire weaponManager
        {
            get
            {
                if (!wm || wm.vessel != vessel)
                {
                    wm = null;

                    foreach (var mf in vessel.FindPartModulesImplementing<MissileFire>())
                    {
                        wm = mf;
                        break;
                    }
                }

                return wm;
            }
        }

        [KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "Rails++")]
        public void RailsPlus()
        {
            IncreaseRails(true);
        }

        [KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "Rails--")]
        public void RailsMinus()
        {
            DecreaseRails(true);
        }


        [KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "Height++")]
        public void HeightPlus()
        {
            IncreaseHeight(true);
        }

        [KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "Height--")]
        public void HeightMinus()
        {
            DecreaseHeight(true);
        }

        public void IncreaseHeight(bool updateSym)
        {
            float prevHeight = currentHeight;
            currentHeight = Mathf.Min(currentHeight + heightInterval, maxHeight);

            UpdateChildrenHeight(currentHeight - prevHeight);
            UpdateModelState();

            if (updateSym)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    if (p != part)
                    {
                        p.FindModuleImplementing<BDRotaryRail>().IncreaseHeight(false);
                    }
                }
            }
        }

        public void DecreaseHeight(bool updateSym)
        {
            float prevHeight = currentHeight;
            currentHeight = Mathf.Max(currentHeight - heightInterval, 0);

            UpdateChildrenHeight(currentHeight - prevHeight);
            UpdateModelState();

            if (updateSym)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    if (p != part)
                    {
                        p.FindModuleImplementing<BDRotaryRail>().DecreaseHeight(false);
                    }
                }
            }
        }


        [KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "Length++")]
        public void LengthPlus()
        {
            IncreaseLength(true);
        }

        [KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "Length--")]
        public void LengthMinus()
        {
            DecreaseLength(true);
        }

        public void IncreaseLength(bool updateSym)
        {
            float prevLength = currentLength;
            currentLength = Mathf.Min(currentLength + lengthInterval, maxLength);

            UpdateModelState();

            MoveEndStackNode(currentLength - prevLength);

            UpdateChildrenLength(currentLength - prevLength);

            if (updateSym)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    if (p != part)
                    {
                        p.FindModuleImplementing<BDRotaryRail>().IncreaseLength(false);
                    }
                }
            }
        }

        public void DecreaseLength(bool updateSym)
        {
            float prevLength = currentLength;
            currentLength = Mathf.Max(currentLength - lengthInterval, 0);

            UpdateModelState();

            MoveEndStackNode(currentLength - prevLength);

            UpdateChildrenLength(currentLength - prevLength);

            if (updateSym)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    if (p != part)
                    {
                        p.FindModuleImplementing<BDRotaryRail>().DecreaseLength(false);
                    }
                }
            }
        }


        public void IncreaseRails(bool updateSym)
        {
            railCountIndex = Mathf.Min(railCountIndex + 1, railCounts.Length - 1);
            numberOfRails = railCounts[Mathf.RoundToInt(railCountIndex)];
            UpdateRails(Mathf.RoundToInt(numberOfRails));

            if (updateSym)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    //p.FindModuleImplementing<BDRotaryRail>().IncreaseRails(false);
                    p.FindModuleImplementing<BDRotaryRail>()
                        .SetRailCount(Mathf.RoundToInt(numberOfRails), railCountIndex);
                }
            }
        }

        public void SetRailCount(int railCount, float railCountIndex)
        {
            this.railCountIndex = railCountIndex;
            numberOfRails = railCount;
            UpdateRails(Mathf.RoundToInt(numberOfRails));
        }

        public void DecreaseRails(bool updateSym)
        {
            railCountIndex = Mathf.Max(railCountIndex - 1, 0);
            numberOfRails = railCounts[Mathf.RoundToInt(railCountIndex)];
            UpdateRails(Mathf.RoundToInt(numberOfRails));

            if (updateSym)
            {
                foreach (Part p in part.symmetryCounterparts)
                {
                    //p.FindModuleImplementing<BDRotaryRail>().DecreaseRails(false);
                    p.FindModuleImplementing<BDRotaryRail>()
                        .SetRailCount(Mathf.RoundToInt(numberOfRails), railCountIndex);
                }
            }
        }

        public void MoveEndStackNode(float offset)
        {
            foreach (var node in part.attachNodes)
            {
                if (node.nodeType == AttachNode.NodeType.Stack && node.id.ToLower().Contains("move"))
                {
                    node.position += offset*Vector3.up;
                }
            }
        }

        IEnumerator DelayedMoveStackNode(float offset)
        {
            yield return null;
            MoveEndStackNode(offset);
        }

        void UpdateRails(int railAmount)
        {
            if (rails.Count == 0)
            {
                rails.Add(part.FindModelTransform("railTransform"));
                var extraRails = part.FindModelTransforms("newRail");
                for (int i = 0; i < extraRails.Length; i++)
                {
                    rails.Add(extraRails[i]);
                }
            }

            for (int i = 1; i < rails.Count; i++)
            {
                foreach (var t in rails[i].GetComponentsInChildren<Transform>())
                {
                    t.name = "deleted";
                }
                Destroy(rails[i].gameObject);
            }

            rails.RemoveRange(1, rails.Count - 1);
            lengthTransforms.Clear();
            heightTransforms.Clear();
            rotationTransforms.Clear();

            railAngle = 360f/(float) railAmount;

            for (int i = 1; i < railAmount; i++)
            {
                GameObject newRail = (GameObject) Instantiate(rails[0].gameObject);
                newRail.name = "newRail";
                newRail.transform.parent = rails[0].parent;
                newRail.transform.localPosition = rails[0].localPosition;
                newRail.transform.localRotation =
                    Quaternion.AngleAxis((float) i*railAngle,
                        rails[0].parent.InverseTransformDirection(part.transform.up))*rails[0].localRotation;
                rails.Add(newRail.transform);
            }

            foreach (var t in part.FindModelTransform("rotaryBombBay").GetComponentsInChildren<Transform>())
            {
                switch (t.name)
                {
                    case "lengthTransform":
                        lengthTransforms.Add(t);
                        break;
                    case "heightTransform":
                        heightTransforms.Add(t);
                        break;
                    case "rotationTransform":
                        rotationTransforms.Add(t);
                        break;
                }
            }
        }

        public override void OnStart(StartState state)
        {
            missileToRailIndex = new Dictionary<int, int>();
            railToMissileIndex = new Dictionary<int, int>();

            lengthInterval = maxLength/intervals;
            heightInterval = maxHeight/intervals;


            numberOfRails = railCounts[Mathf.RoundToInt(railCountIndex)];

            rails = new List<Transform>();
            rotationTransforms = new List<Transform>();
            heightTransforms = new List<Transform>();
            lengthTransforms = new List<Transform>();

            UpdateModelState();

            //MoveEndStackNode(currentLength);
            if (HighLogic.LoadedSceneIsEditor)
            {
                StartCoroutine(DelayedMoveStackNode(currentLength));
                //part.AddOnMouseEnter(OnPartEnter);
                //part.AddOnMouseExit(OnPartExit);
                part.OnEditorAttach += OnAttach;
                //previousSymMethod = EditorLogic.fetch.symmetryMethod;

                /*
                foreach(var pSym in part.symmetryCounterparts)
                {
                    var otherRail = pSym.FindModuleImplementing<BDRotaryRail>();
                    if(otherRail.numberOfRails != numberOfRails)
                    {
                        SetRailCount(Mathf.RoundToInt(otherRail.numberOfRails), otherRail.railCountIndex);
                        break;
                    }
                }
                */
            }

            if (HighLogic.LoadedSceneIsFlight)
            {
                UpdateMissileChildren();

                RotateToIndex(railIndex, true);
            }
        }

        void OnAttach()
        {
            UpdateRails(Mathf.RoundToInt(numberOfRails));
        }


        void UpdateChildrenHeight(float offset)
        {
            foreach (Part p in part.children)
            {
                //if(p.parent != part) continue;

                Vector3 direction = p.transform.position - part.transform.position;
                direction = Vector3.ProjectOnPlane(direction, part.transform.up).normalized;

                p.transform.position += direction*offset;
            }
        }

        void UpdateChildrenLength(float offset)
        {
            bool parentInFront =
                Vector3.Dot(part.parent.transform.position - part.transform.position, part.transform.up) > 0;
            if (parentInFront)
            {
                offset = -offset;
            }

            Vector3 direction = part.transform.up;

            if (!parentInFront)
            {
                foreach (Part p in part.children)
                {
                    if (p.FindModuleImplementing<MissileLauncher>() && p.parent == part) continue;

                    p.transform.position += direction*offset;
                }
            }

            if (parentInFront)
            {
                part.transform.position += direction*offset;
            }
        }

        void UpdateModelState()
        {
            UpdateRails(Mathf.RoundToInt(numberOfRails));

            for (int i = 0; i < heightTransforms.Count; i++)
            {
                Vector3 lp = heightTransforms[i].localPosition;
                heightTransforms[i].localPosition = new Vector3(lp.x, -currentHeight, lp.z);
            }

            for (int i = 0; i < lengthTransforms.Count; i++)
            {
                Vector3 lp = lengthTransforms[i].localPosition;
                lengthTransforms[i].localPosition = new Vector3(lp.x, lp.y, currentLength);
            }

            //
        }

        public void RotateToMissile(MissileLauncher ml)
        {
            if (missileCount == 0) return;
            if (!ml) return;

            if (readyMissile == ml) return;

            //rotate to this missile specifically
            for (int i = 0; i < missileChildren.Length; i++)
            {
                if (missileChildren[i] == ml)
                {
                    RotateToIndex(missileToRailIndex[i], false);
                    nextMissile = ml;
                    return;
                }
            }

            //specific missile isnt here, but check if this type exists here

            if (readyMissile && readyMissile.part.name == ml.part.name) return; //current missile is correct type

            //look for missile type
            for (int i = 0; i < missileChildren.Length; i++)
            {
                if (missileChildren[i].GetShortName() == ml.GetShortName())
                {
                    RotateToIndex(missileToRailIndex[i], false);
                    nextMissile = missileChildren[i];
                    return;
                }
            }
        }

        void UpdateIndexDictionary()
        {
            missileToRailIndex.Clear();
            railToMissileIndex.Clear();

            for (int i = 0; i < missileCount; i++)
            {
                float closestSqr = float.MaxValue;
                int rIndex = 0;
                for (int r = 0; r < numberOfRails; r++)
                {
                    Vector3 railPos = Quaternion.AngleAxis((float) r*railAngle, part.transform.up)*
                                      part.transform.forward;
                    railPos += part.transform.position;
                    float sqrDist = (missileChildren[i].transform.position - railPos).sqrMagnitude;
                    if (sqrDist < closestSqr)
                    {
                        rIndex = r;
                        closestSqr = sqrDist;
                    }
                }
                missileToRailIndex.Add(i, rIndex);
                railToMissileIndex.Add(rIndex, i);
                //Debug.Log("Adding to index dictionary: " + i + " : " + rIndex);
            }
        }


        void RotateToIndex(int index, bool instant)
        {
            //Debug.Log("Rotary rail is rotating to index: " + index);

            if (rotationRoutine != null)
            {
                StopCoroutine(rotationRoutine);
            }

            // if(railIndex == index && readyToFire) return;

            if (missileCount > 0)
            {
                if (railToMissileIndex.ContainsKey(index))
                {
                    nextMissile = missileChildren[railToMissileIndex[index]];
                }
            }
            else
            {
                nextMissile = null;
            }

            if (!nextMissile && missileCount > 0)
            {
                RotateToIndex(Mathf.RoundToInt(Mathf.Repeat(index + 1, numberOfRails)), instant);
                return;
            }

            rotationRoutine = StartCoroutine(RotateToIndexRoutine(index, instant));
        }

        Coroutine rotationRoutine;

        IEnumerator RotateToIndexRoutine(int index, bool instant)
        {
            rdyToFire = false;
            rdyMissile = null;
            railIndex = index;

            /*
            MissileLauncher foundMissile = null;

            foreach(var mIndex in missileToRailIndex.Keys)
            {
                if(missileToRailIndex[mIndex] == index)
                {
                    foundMissile = missileChildren[mIndex];
                }
            }
            nextMissile = foundMissile;
            */


            yield return new WaitForSeconds(rotationDelay);

            Quaternion targetRot = Quaternion.Euler(0, 0, (float) index*-railAngle);

            if (instant)
            {
                for (int i = 0; i < rotationTransforms.Count; i++)
                {
                    rotationTransforms[i].localRotation = targetRot;
                }

                UpdateMissilePositions();
                //yield break;
            }
            else
            {
                while (rotationTransforms[0].localRotation != targetRot)
                {
                    for (int i = 0; i < rotationTransforms.Count; i++)
                    {
                        rotationTransforms[i].localRotation =
                            Quaternion.RotateTowards(rotationTransforms[i].localRotation, targetRot,
                                rotationSpeed*Time.fixedDeltaTime);
                    }

                    UpdateMissilePositions();
                    yield return new WaitForFixedUpdate();
                }
            }

            if (nextMissile)
            {
                rdyMissile = nextMissile;
                rdyToFire = true;
                nextMissile = null;

                if (weaponManager)
                {
                    if (wm.weaponIndex > 0 && wm.selectedWeapon.GetPart().name == rdyMissile.part.name)
                    {
                        wm.selectedWeapon = rdyMissile;
                        wm.CurrentMissile = rdyMissile;
                    }
                }
            }
        }

        public void PrepMissileForFire(MissileLauncher ml)
        {
            if (ml != readyMissile)
            {
                //Debug.Log("Rotary rail tried prepping a missile for fire, but it is not in firing position");
                return;
            }

            int index = IndexOfMissile(ml);

            if (index >= 0)
            {
                PrepMissileForFire(index);
            }
            else
            {
                //Debug.Log("Tried to prep a missile for firing that doesn't exist or is not attached to the turret.");
            }
        }

        void PrepMissileForFire(int index)
        {
            //Debug.Log("Prepping missile for rotary rail fire.");
            missileChildren[index].part.CoMOffset = comOffsets[missileChildren[index].part];

            missileTransforms[index].localPosition = Vector3.zero;
            missileTransforms[index].localRotation = Quaternion.identity;
            missileChildren[index].part.partTransform.position = missileReferenceTransforms[index].position;
            missileChildren[index].part.partTransform.rotation = missileReferenceTransforms[index].rotation;

            missileChildren[index].decoupleForward = false;

            missileChildren[index].part.rb.velocity =
                part.rb.GetPointVelocity(missileReferenceTransforms[index].position);

            missileChildren[index].rotaryRail = this;
        }

        public void FireMissile(int missileIndex)
        {
            int nextRailIndex = 0;

            if (!readyToFire)
            {
                return;
            }

            if (missileIndex >= missileChildren.Length)
            {
                return;
            }

            if (missileIndex < missileCount && missileChildren != null && missileChildren[missileIndex] != null)
            {
                if (missileChildren[missileIndex] != readyMissile) return;

                PrepMissileForFire(missileIndex);

                if (weaponManager)
                {
                    wm.SendTargetDataToMissile(missileChildren[missileIndex]);
                }

                string firedMissileName = missileChildren[missileIndex].part.name;

                missileChildren[missileIndex].FireMissile();

                rdyMissile = null;
                rdyToFire = false;
                //StartCoroutine(MissileRailRoutine(missileChildren[missileIndex]));

                nextRailIndex = Mathf.RoundToInt(Mathf.Repeat(missileToRailIndex[missileIndex] + 1, numberOfRails));

                UpdateMissileChildren();

                if (wm)
                {
                    wm.UpdateList();
                }

                if (railToMissileIndex.ContainsKey(nextRailIndex) && railToMissileIndex[nextRailIndex] < missileCount &&
                    missileChildren[railToMissileIndex[nextRailIndex]].part.name == firedMissileName)
                {
                    RotateToIndex(nextRailIndex, false);
                }
            }


            //StartCoroutine(RotateToIndexAtEndOfFrame(nextRailIndex, false));
        }


        IEnumerator RotateToIndexAtEndOfFrame(int index, bool instant)
        {
            yield return new WaitForEndOfFrame();
            RotateToIndex(index, instant);
        }

        public void FireMissile(MissileLauncher ml)
        {
            if (!readyToFire || ml != readyMissile)
            {
                return;
            }


            int index = IndexOfMissile(ml);
            if (index >= 0)
            {
                //Debug.Log("Firing missile index: " + index);
                FireMissile(index);
            }
            else
            {
                //Debug.Log("Tried to fire a missile that doesn't exist or is not attached to the rail.");
            }
        }

        private int IndexOfMissile(MissileLauncher ml)
        {
            if (missileCount == 0) return -1;

            for (int i = 0; i < missileCount; i++)
            {
                if (missileChildren[i] && missileChildren[i] == ml)
                {
                    return i;
                }
            }

            return -1;
        }


        public void UpdateMissileChildren()
        {
            missileCount = 0;

            //setup com dictionary
            if (comOffsets == null)
            {
                comOffsets = new Dictionary<Part, Vector3>();
            }

            //destroy the existing reference transform objects
            if (missileReferenceTransforms != null)
            {
                for (int i = 0; i < missileReferenceTransforms.Length; i++)
                {
                    if (missileReferenceTransforms[i])
                    {
                        GameObject.Destroy(missileReferenceTransforms[i].gameObject);
                    }
                }
            }

            List<MissileLauncher> msl = new List<MissileLauncher>();
            List<Transform> mtfl = new List<Transform>();
            List<Transform> mrl = new List<Transform>();

            foreach (var child in part.children)
            {
                if (child.parent != part) continue;

                MissileLauncher ml = child.FindModuleImplementing<MissileLauncher>();

                if (!ml) continue;

                Transform mTf = child.FindModelTransform("missileTransform");
                //fix incorrect hierarchy
                if (!mTf)
                {
                    Transform modelTransform = ml.part.partTransform.FindChild("model");

                    mTf = new GameObject("missileTransform").transform;
                    Transform[] tfchildren = new Transform[modelTransform.childCount];
                    for (int i = 0; i < modelTransform.childCount; i++)
                    {
                        tfchildren[i] = modelTransform.GetChild(i);
                    }
                    mTf.parent = modelTransform;
                    mTf.localPosition = Vector3.zero;
                    mTf.localRotation = Quaternion.identity;
                    mTf.localScale = Vector3.one;
                    for (int i = 0; i < tfchildren.Length; i++)
                    {
                        //Debug.Log("MissileTurret moving transform: " + tfchildren[i].gameObject.name);
                        tfchildren[i].parent = mTf;
                    }
                }

                if (ml && mTf)
                {
                    msl.Add(ml);
                    mtfl.Add(mTf);
                    Transform mRef = new GameObject().transform;
                    mRef.position = mTf.position;
                    mRef.rotation = mTf.rotation;
                    mRef.parent = rotationTransforms[0];
                    mrl.Add(mRef);

                    ml.MissileReferenceTransform = mTf;
                    ml.rotaryRail = this;

                    ml.decoupleForward = false;
                    //ml.decoupleSpeed = Mathf.Max(ml.decoupleSpeed, 4); //removing clamp as some weapons want greater decouple speeds
                    ml.dropTime = Mathf.Max(ml.dropTime, 0.2f);


                    if (!comOffsets.ContainsKey(ml.part))
                    {
                        comOffsets.Add(ml.part, ml.part.CoMOffset);
                    }

                    //missileCount++;
                }
            }

            missileChildren = msl.ToArray();
            missileCount = missileChildren.Length;
            missileTransforms = mtfl.ToArray();
            missileReferenceTransforms = mrl.ToArray();

            UpdateIndexDictionary();
        }

        /*
        //editor stuff
        void OnPartEnter(Part p)
        {
            if(EditorLogic.SelectedPart && EditorLogic.SelectedPart!=part)
            {
                previousSymMethod = EditorLogic.fetch.symmetryMethod;
                EditorLogic.fetch.symmetryMethod = SymmetryMethod.Radial;
            }
        }
        void OnPartExit(Part p)
        {
            if(EditorLogic.SelectedPart && EditorLogic.SelectedPart != part)
            {
                EditorLogic.fetch.symmetryMethod = previousSymMethod;
            }

            if(EditorLogic.fetch.symmetryMethod == SymmetryMethod.Mirror)
            {
                EditorLogic.fetch.symmetryMode = Mathf.Min(EditorLogic.fetch.symmetryMode, 1);
            }
        }
        SymmetryMethod previousSymMethod;
        */

        //test
        void OnGUI()
        {
            /*
            if(HighLogic.LoadedSceneIsEditor)
            {
                string debugString = "Selected part: " + (EditorLogic.SelectedPart ? EditorLogic.SelectedPart.name : "None");

                debugString += "\nsymmetryMode: " + EditorLogic.fetch.symmetryMode;

                GUI.Label(new Rect(400, 400, 600, 600), debugString);
            }

            */
            /*
            if(HighLogic.LoadedSceneIsFlight)
            {
                if(readyMissile)
                {
                    BDGUIUtils.DrawLineBetweenWorldPositions(readyMissile.missileReferenceTransform.position, readyMissile.missileReferenceTransform.position + readyMissile.missileReferenceTransform.forward, 3, Color.blue);
                    BDGUIUtils.DrawLineBetweenWorldPositions(readyMissile.missileReferenceTransform.position, readyMissile.missileReferenceTransform.position + readyMissile.missileReferenceTransform.up, 3, Color.green);

                }

                for(int i = 0; i < numberOfRails; i++)
                {
                    Vector3 railPos = part.transform.position + (Quaternion.AngleAxis((float)i*railAngle, part.transform.up) * part.transform.forward);
                    Vector2 guiPos;
                    if(BDGUIUtils.WorldToGUIPos(railPos, out guiPos))
                    {
                        GUI.Label(new Rect(guiPos.x, guiPos.y, 20, 20), "R:"+i.ToString());
                    }
                }

                if(missileCount > 0)
                {
                    for(int i = 0; i < missileCount; i++)
                    {
                        MissileLauncher ml = missileChildren[i];
                        Vector2 guiPos;
                        if(BDGUIUtils.WorldToGUIPos(ml.transform.position, out guiPos))
                        {
                            GUI.Label(new Rect(guiPos.x, guiPos.y, 40, 20), "M:"+i.ToString());
                        }
                    }
                }

                string rail2MissileString = "Rail to missile\n";
                if(railToMissileIndex != null)
                {
                    foreach(var r in railToMissileIndex.Keys)
                    {
                        rail2MissileString += "R: " + r + " M: " + railToMissileIndex[r].ToString() +"\n";
                    }
                }
                GUI.Label(new Rect(200, 200, 200, 900), rail2MissileString);

                string missile2railString = "Missile to rail\n";
                if(missileToRailIndex != null)
                {
                    foreach(var m in missileToRailIndex.Keys)
                    {
                        missile2railString += "R: " + missileToRailIndex[m].ToString() + " M: " + m +"\n";
                    }
                }
                GUI.Label(new Rect(500, 200, 200, 900), missile2railString);
            }*/
        }


        void UpdateMissilePositions()
        {
            if (missileCount == 0)
            {
                return;
            }

            for (int i = 0; i < missileChildren.Length; i++)
            {
                if (missileTransforms[i] && missileChildren[i] && !missileChildren[i].HasFired)
                {
                    missileTransforms[i].position = missileReferenceTransforms[i].position;
                    missileTransforms[i].rotation = missileReferenceTransforms[i].rotation;

                    Part missilePart = missileChildren[i].part;
                    Vector3 newCoMOffset =
                        missilePart.transform.InverseTransformPoint(
                            missileTransforms[i].TransformPoint(comOffsets[missilePart]));
                    missilePart.CoMOffset = newCoMOffset;
                }
            }
        }

        /*
        float dropRailLength = 1;
        IEnumerator MissileRailRoutine(MissileLauncher ml)
        {
            yield return null;
            Ray ray = new Ray(ml.transform.position, part.transform.forward);
            Vector3 localOrigin = part.transform.InverseTransformPoint(ray.origin);
            Vector3 localDirection = part.transform.InverseTransformDirection(ray.direction);
            float dropSpeed = ml.decoupleSpeed;
            while(ml && Vector3.SqrMagnitude(ml.transform.position - ray.origin) < dropRailLength * dropRailLength && ml.timeIndex < ml.dropTime)
            {
                
                //float thrust = ml.timeIndex < ml.boostTime ? ml.thrust : ml.cruiseThrust;
                //thrust = ml.timeIndex < ml.boostTime + ml.cruiseTime ? thrust : 0;
                //float accel = thrust / ml.part.mass;
                //dropSpeed += accel * Time.fixedDeltaTime;
                

                ray.origin = part.transform.TransformPoint(localOrigin);
                ray.direction = part.transform.TransformDirection(localDirection);

                Vector3 projPos = Vector3.Project(ml.vessel.transform.position - ray.origin, ray.direction) + ray.origin;
                Vector3 railVel = part.rb.GetPointVelocity(projPos);
                //Vector3 projVel = Vector3.Project(ml.vessel.srf_velocity-railVel, ray.direction);

                ml.vessel.SetPosition(projPos);
                ml.vessel.SetWorldVelocity(railVel + (dropSpeed * ray.direction));

                yield return new WaitForFixedUpdate();

                ray.origin = part.transform.TransformPoint(localOrigin);
                ray.direction = part.transform.TransformDirection(localDirection);
            }
        }
    */
    }
}