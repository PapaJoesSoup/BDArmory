using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
namespace BahaTurret
{
    public class BDArmor
    {
        public float EquivalentThickness { get; private set; }
        public ExplodeMode explodeMode { get; private set; }
        public BDArmor(ConfigNode configNode)
        {
            if (configNode.HasValue("EquivalentThickness"))
                EquivalentThickness = float.Parse(configNode.GetValue("EquivalentThickness"));
            else
                EquivalentThickness = 1;
            if (configNode.HasValue("ExplodeMode"))
                explodeMode = (ExplodeMode)Enum.Parse(typeof(ExplodeMode), configNode.GetValue("ExplodeMode"));
            else
                explodeMode = ExplodeMode.Never;
        }
        public static BDArmor GetArmor(Collider collider)
        {
            var hitPart = Part.FromGO(collider.gameObject);
            if (!hitPart)
                return null;
            var nodes = hitPart.partInfo.partConfig.GetNodes("BDARMOR");
            for (int i = 0; i < nodes.Length; i++)
            {
                var current = nodes[i];
                Transform transform;
                if (current.HasValue("ArmorRootTransform"))
                    transform = hitPart.FindModelTransform(current.GetValue("ArmorRootTransform"));
                else
                    transform = hitPart.partTransform;
                if (!transform)
                {
                    Debug.LogError("Armor Transform not found!");
                    return null;
                }
                if (collider.transform == transform || collider.transform.IsChildOf(transform))
                {
                    return new BDArmor(nodes[i]);
                }
            }
            return null;
        }
        public enum ExplodeMode
        {
            Always,
            Dynamic,
            Never
        }
    }
}
