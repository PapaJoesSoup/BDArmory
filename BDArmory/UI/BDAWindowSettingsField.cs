using System;
using System.Collections.Generic;
using System.Reflection;
using UniLinq;

namespace BDArmory.UI
{
    [AttributeUsage(AttributeTargets.Field)]
    public class BDAWindowSettingsField : Attribute
    {
        public BDAWindowSettingsField()
        {
        }

        public static void Save()
        {
            ConfigNode fileNode = ConfigNode.Load(BDArmorySettings.settingsConfigURL);

            if (!fileNode.HasNode("BDAWindows"))
            {
                fileNode.AddNode("BDAWindows");
            }

            ConfigNode settings = fileNode.GetNode("BDAWindows");

            IEnumerator<FieldInfo> field = typeof(BDArmorySettings).GetFields().AsEnumerable().GetEnumerator();
            while (field.MoveNext())
            {
                if (field.Current == null) continue;
                if (!field.Current.IsDefined(typeof(BDAWindowSettingsField), false)) continue;

                settings.SetValue(field.Current.Name, field.Current.GetValue(null).ToString(), true);
            }
            field.Dispose();
            fileNode.Save(BDArmorySettings.settingsConfigURL);
        }

        public static void Load()
        {
            ConfigNode fileNode = ConfigNode.Load(BDArmorySettings.settingsConfigURL);
            if (!fileNode.HasNode("BDAWindows")) return;

            ConfigNode settings = fileNode.GetNode("BDAWindows");

            IEnumerator<FieldInfo> field = typeof(BDArmorySettings).GetFields().AsEnumerable().GetEnumerator();
            while (field.MoveNext())
            {
                if (field.Current == null) continue;
                if (!field.Current.IsDefined(typeof(BDAWindowSettingsField), false)) continue;
                if (!settings.HasValue(field.Current.Name)) continue;

                object parsedValue = BDArmorySettings.ParseValue(field.Current.FieldType, settings.GetValue(field.Current.Name));
                if (parsedValue != null)
                {
                    field.Current.SetValue(null, parsedValue);
                }
            }
            field.Dispose();
        }
    }
}