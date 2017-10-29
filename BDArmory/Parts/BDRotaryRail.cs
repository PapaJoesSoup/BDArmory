using System.Collections;
using System.Collections.Generic;
using UniLinq;
using UnityEngine;

namespace BDArmory.Parts
{
    public class BDRotaryRail : PartModule
    {
        [KSPField] public float maxLength;

        [KSPField] public float maxHeight;

        [KSPField] public int intervals;

        [KSPField] public float rotationSpeed = 360;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = "Rails")] public float
            numberOfRails = 8;

        float railAngle;

        [KSPField] public float rotationDelay = 0.15f;

        [KSPField(isPersistant = true)] public int railIndex;


        Dictionary<int, int> missileToRailIndex;
        Dictionary<int, int> railToMissileIndex;

        [KSPField(isPersistant = true)] public float currentHeight;

        [KSPField(isPersistant = true)] public float currentLength;


        public int missileCount;
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

        bool rdyToFire;

        public bool readyToFire
        {
            get { return rdyToFire; }
        }

        public MissileLauncher nextMissile;

        MissileLauncher rdyMissile;

        public MissileLauncher readyMissile
        {
            get { return rdyMissile; }
        }

        MissileFire wm;

        public MissileFire weaponManager
        {
            get
            {
                if (wm && wm.vessel == vessel) return wm;
                wm = null;
                List<MissileFire>.Enumerator mf = vessel.FindPartModulesImplementing<MissileFire>().GetEnumerator();
                while (mf.MoveNext())
                {
                    if (mf.Current == null) continue;
                    wm = mf.Current;
                    break;
                }
                mf.Dispose();
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

            if (!updateSym) return;
            List<Part>.Enumerator p = part.symmetryCounterparts.GetEnumerator();
            while (p.MoveNext())
            {
                if (p.Current == null) continue;
                if (p.Current != part)
                {
                    p.Current.FindModuleImplementing<BDRotaryRail>().IncreaseHeight(false);
                }
            }
            p.Dispose();
        }

        public void DecreaseHeight(bool updateSym)
        {
            float prevHeight = currentHeight;
            currentHeight = Mathf.Max(currentHeight - heightInterval, 0);

            UpdateChildrenHeight(currentHeight - prevHeight);
            UpdateModelState();

            if (!updateSym) return;
            List<Part>.Enumerator p = part.symmetryCounterparts.GetEnumerator();
            while (p.MoveNext())
            {
                if (p.Current == null) continue;
                if (p.Current != part)
                {
                    p.Current.FindModuleImplementing<BDRotaryRail>().DecreaseHeight(false);
                }
            }
            p.Dispose();
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

            if (!updateSym) return;
            List<Part>.Enumerator p = part.symmetryCounterparts.GetEnumerator();
            while (p.MoveNext())
            {
                if (p.Current == null) continue;
                if (p.Current != part)
                {
                    p.Current.FindModuleImplementing<BDRotaryRail>().IncreaseLength(false);
                }
            }
            p.Dispose();
        }

        public void DecreaseLength(bool updateSym)
        {
            float prevLength = currentLength;
            currentLength = Mathf.Max(currentLength - lengthInterval, 0);

            UpdateModelState();

            MoveEndStackNode(currentLength - prevLength);

            UpdateChildrenLength(currentLength - prevLength);

            if (!updateSym) return;
            List<Part>.Enumerator p = part.symmetryCounterparts.GetEnumerator();
            while (p.MoveNext())
            {
                if (p.Current == null) continue;
                if (p.Current != part)
                {
                    p.Current.FindModuleImplementing<BDRotaryRail>().DecreaseLength(false);
                }
            }
            p.Dispose();
        }


        public void IncreaseRails(bool updateSym)
        {
            railCountIndex = Mathf.Min(railCountIndex + 1, railCounts.Length - 1);
            numberOfRails = railCounts[Mathf.RoundToInt(railCountIndex)];
            UpdateRails(Mathf.RoundToInt(numberOfRails));

            if (!updateSym) return;
            List<Part>.Enumerator p = part.symmetryCounterparts.GetEnumerator();
            while (p.MoveNext())
            {
                if (p.Current == null) continue;
                p.Current.FindModuleImplementing<BDRotaryRail>().SetRailCount(Mathf.RoundToInt(numberOfRails), railCountIndex);
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

            if (!updateSym) return;
            List<Part>.Enumerator p = part.symmetryCounterparts.GetEnumerator();
            while (p.MoveNext())
            {
                if (p.Current == null) continue;
                p.Current.FindModuleImplementing<BDRotaryRail>().SetRailCount(Mathf.RoundToInt(numberOfRails), railCountIndex);
            }
        }

        public void MoveEndStackNode(float offset)
        {
            List<AttachNode>.Enumerator node = part.attachNodes.GetEnumerator();
            while (node.MoveNext())
            {
                if (node.Current == null) continue;
                if (node.Current.nodeType == AttachNode.NodeType.Stack && node.Current.id.ToLower().Contains("move"))
                {
                    node.Current.position += offset*Vector3.up;
                }
            }
            node.Dispose();
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
                IEnumerator<Transform> t = part.FindModelTransforms("newRail").AsEnumerable().GetEnumerator();
                while (t.MoveNext())
                {
                    if (t.Current == null) continue;
                    rails.Add(t.Current);
                }
                t.Dispose();
            }

            for (int i = 1; i < rails.Count; i++)
            {
                IEnumerator<Transform> t = rails[i].GetComponentsInChildren<Transform>().AsEnumerable().GetEnumerator();
                while (t.MoveNext())
                {
                    if (t.Current == null) continue;
                    t.Current.name = "deleted";
                }
                t.Dispose();
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

            IEnumerator<Transform> mt = part.FindModelTransform("rotaryBombBay").GetComponentsInChildren<Transform>().AsEnumerable().GetEnumerator();
            while (mt.MoveNext())
            {
                if (mt.Current == null) continue;
                switch (mt.Current.name)
                {
                    case "lengthTransform":
                        lengthTransforms.Add(mt.Current);
                        break;
                    case "heightTransform":
                        heightTransforms.Add(mt.Current);
                        break;
                    case "rotationTransform":
                        rotationTransforms.Add(mt.Current);
                        break;
                }
            }
            mt.Dispose();
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
                part.OnEditorAttach += OnAttach;
            }

            if (!HighLogic.LoadedSceneIsFlight) return;
            UpdateMissileChildren();
            RotateToIndex(railIndex, true);
        }

        void OnAttach()
        {
            UpdateRails(Mathf.RoundToInt(numberOfRails));
        }


        void UpdateChildrenHeight(float offset)
        {
            List<Part>.Enumerator p = part.children.GetEnumerator();
            while (p.MoveNext())
            {
                if (p.Current == null) continue;
                Vector3 direction = p.Current.transform.position - part.transform.position;
                direction = Vector3.ProjectOnPlane(direction, part.transform.up).normalized;

                p.Current.transform.position += direction*offset;
            }
            p.Dispose();
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
                List<Part>.Enumerator p = part.children.GetEnumerator();
                while (p.MoveNext())
                {
                    if (p.Current == null) continue;
                    if (p.Current.FindModuleImplementing<MissileLauncher>() && p.Current.parent == part) continue;

                    p.Current.transform.position += direction*offset;
                }
                p.Dispose();
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
                if (missileChildren[i] != ml) continue;
                RotateToIndex(missileToRailIndex[i], false);
                nextMissile = ml;
                return;
            }

            //specific missile isnt here, but check if this type exists here

            if (readyMissile && readyMissile.part.name == ml.part.name) return; //current missile is correct type

            //look for missile type
            for (int i = 0; i < missileChildren.Length; i++)
            {
                if (missileChildren[i].GetShortName() != ml.GetShortName()) continue;
                RotateToIndex(missileToRailIndex[i], false);
                nextMissile = missileChildren[i];
                return;
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

            yield return new WaitForSeconds(rotationDelay);

            Quaternion targetRot = Quaternion.Euler(0, 0, (float) index*-railAngle);

            if (instant)
            {
                for (int i = 0; i < rotationTransforms.Count; i++)
                {
                    rotationTransforms[i].localRotation = targetRot;
                }

                UpdateMissilePositions();
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
                        Destroy(missileReferenceTransforms[i].gameObject);
                    }
                }
            }

            List<MissileLauncher> msl = new List<MissileLauncher>();
            List<Transform> mtfl = new List<Transform>();
            List<Transform> mrl = new List<Transform>();
            List<Part>.Enumerator child = part.children.GetEnumerator();
            while (child.MoveNext())
            {
                if (child.Current == null) continue;
                if (child.Current.parent != part) continue;

                MissileLauncher ml = child.Current.FindModuleImplementing<MissileLauncher>();

                if (!ml) continue;

                Transform mTf = child.Current.FindModelTransform("missileTransform");
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
                    IEnumerator<Transform> t = tfchildren.AsEnumerable().GetEnumerator();
                    while (t.MoveNext())
                    {
                        if (t.Current == null) continue;
                        //Debug.Log("MissileTurret moving transform: " + tfchildren[i].gameObject.name);
                        t.Current.parent = mTf;
                    }
                    t.Dispose();
                }

                if (!mTf) continue;
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
            }
            child.Dispose();

            missileChildren = msl.ToArray();
            missileCount = missileChildren.Length;
            missileTransforms = mtfl.ToArray();
            missileReferenceTransforms = mrl.ToArray();

            UpdateIndexDictionary();
        }


        void UpdateMissilePositions()
        {
            if (missileCount == 0)
            {
                return;
            }

            for (int i = 0; i < missileChildren.Length; i++)
            {
                if (!missileTransforms[i] || !missileChildren[i] || missileChildren[i].HasFired) continue;
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
}