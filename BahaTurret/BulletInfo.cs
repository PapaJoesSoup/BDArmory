using System;
using System.Collections.Generic;
using UnityEngine;

namespace BahaTurret
{
    public class BulletInfo
    {
        public float positiveCoefficient { get; private set; }
        public FloatCurve penetration { get; private set; }
        public string name { get; private set; }
        public static BulletInfos bullets;
        public BulletInfo(string name, float positiveCoefficient, FloatCurve penetration)
        {
            this.name = name;
            this.positiveCoefficient = positiveCoefficient;
            this.penetration = penetration;
        }

        public static void Load()
        {
            bullets = new BulletInfos();
            var nodes = GameDatabase.Instance.GetConfigs("BULLET");
            for (int i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i].config;
                var penetrationCurve = new FloatCurve();
                penetrationCurve.Load(node.GetNode("penetration"));
                bullets.Add(new BulletInfo(node.GetValue("name"), float.Parse(node.GetValue("positiveCoefficient")), penetrationCurve));
            }
        }
    }
    public class BulletInfos : List<BulletInfo>
    {
        public BulletInfo this[string name]
        {
            get { return Find((value) => { return value.name == name; }); }
        }
    }
}