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
        public float blastRadius { get; private set; }
        public float blastPower { get; private set; }
        public float blastHeat { get; private set; }
        public float outerArmorThickness { get; private set; }
        public string explModelPath { get; private set; }
        public string explSoundPath { get; private set; }

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

            if (configNode.HasValue("blastRadius"))
                blastRadius = float.Parse(configNode.GetValue("blastRadius"));
            else
                blastRadius = 1;

            if (configNode.HasValue("blastPower"))
                blastPower = float.Parse(configNode.GetValue("blastPower"));
            else
                blastPower = 1;

            if (configNode.HasValue("blastHeat"))
                blastHeat = float.Parse(configNode.GetValue("blastHeat"));
            else
                blastHeat = 1;

            if (configNode.HasValue("outerArmorThickness"))
                outerArmorThickness = float.Parse(configNode.GetValue("outerArmorThickness"));
            else
                outerArmorThickness = float.MaxValue;

            if (configNode.HasValue("explModelPath"))
                explModelPath = configNode.GetValue("explModelPath");
            else
                explModelPath = "BDArmory/Models/explosion/explosionLarge";

            if (configNode.HasValue("explSoundPath"))
                explSoundPath = configNode.GetValue("explSoundPath");
            else
                explSoundPath = "BDArmory/Sounds/explode1";
        }
        public void CreateExplosion(Part part)
        {
            float explodeScale = 0;
            for (int i = 0; i < part.Resources.Count; i++)
            {
                var current = part.Resources[i];
                switch (current.resourceName)
                {
                    case "LiquidFuel":
                        explodeScale += (float)current.amount;
                        break;
                    case "Oxidizer":
                        explodeScale += (float)current.amount;
                        break;
                }
            }
            explodeScale /= 100;
            part.explode();
            ExplosionFX.CreateExplosion(part.partTransform.position, explodeScale * blastRadius, explodeScale * blastPower * 2, explodeScale * blastHeat, part.vessel, FlightGlobals.upAxis, explModelPath, explSoundPath);
        }
        public static BDArmor GetArmor(Collider collider, Part hitPart)
        {
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
