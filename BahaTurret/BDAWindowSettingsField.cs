using System;
using System.Reflection;
using UnityEngine;

namespace BahaTurret
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

			if(!fileNode.HasNode("BDAWindows"))
			{
				fileNode.AddNode("BDAWindows");
			}

			ConfigNode settings = fileNode.GetNode("BDAWindows");

			foreach(FieldInfo field in typeof(BDArmorySettings).GetFields())
			{
				if(!field.IsDefined(typeof(BDAWindowSettingsField), false)) continue;

				settings.SetValue(field.Name, field.GetValue(null).ToString(), true);
			}

			fileNode.Save(BDArmorySettings.settingsConfigURL);
		}

		public static void Load()
		{
			ConfigNode fileNode = ConfigNode.Load(BDArmorySettings.settingsConfigURL);
			if(!fileNode.HasNode("BDAWindows")) return;

			ConfigNode settings = fileNode.GetNode("BDAWindows");

			foreach(FieldInfo field in typeof(BDArmorySettings).GetFields())
			{
				if(!field.IsDefined(typeof(BDAWindowSettingsField), false)) continue;
			  if (!settings.HasValue(field.Name)) continue;

			  object parsedValue = BDArmorySettings.ParseValue(field.FieldType, settings.GetValue(field.Name));
			  if(parsedValue != null)
			  {
			    field.SetValue(null, parsedValue);
			  }
			}
		}
	}
}

