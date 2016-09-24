using System;
using System.Reflection;
using UnityEngine;


namespace BahaTurret
{
	public class BDInputSettingsFields
	{
		//MAIN
		public static BDInputInfo WEAP_FIRE_KEY = new BDInputInfo("mouse 0", "Fire");
		//TGP
		public static BDInputInfo TGP_SLEW_RIGHT = new BDInputInfo("Slew Right");
		public static BDInputInfo TGP_SLEW_LEFT = new BDInputInfo("Slew Left");
		public static BDInputInfo TGP_SLEW_UP = new BDInputInfo("Slew Up");
		public static BDInputInfo TGP_SLEW_DOWN = new BDInputInfo("Slew Down");
		public static BDInputInfo TGP_LOCK = new BDInputInfo("Lock/Unlock");
		public static BDInputInfo TGP_IN = new BDInputInfo("Zoom In");
		public static BDInputInfo TGP_OUT = new BDInputInfo("Zoom Out");
		public static BDInputInfo TGP_RADAR = new BDInputInfo("To Radar");
		public static BDInputInfo TGP_SEND_GPS = new BDInputInfo("Send GPS");
		public static BDInputInfo TGP_TO_GPS = new BDInputInfo("Slave to GPS");
		public static BDInputInfo TGP_TURRETS = new BDInputInfo("Slave Turrets");
		public static BDInputInfo TGP_COM = new BDInputInfo("CoM-Track");
		public static BDInputInfo TGP_NV = new BDInputInfo("Toggle NV");
		public static BDInputInfo TGP_RESET = new BDInputInfo("Reset");

		//RADAR
		public static BDInputInfo RADAR_LOCK = new BDInputInfo("Lock/Unlock");
		public static BDInputInfo RADAR_CYCLE_LOCK = new BDInputInfo("Cycle Lock");
		public static BDInputInfo RADAR_SLEW_RIGHT = new BDInputInfo("Slew Right");
		public static BDInputInfo RADAR_SLEW_LEFT = new BDInputInfo("Slew Left");
		public static BDInputInfo RADAR_SLEW_UP = new BDInputInfo("Slew Up");
		public static BDInputInfo RADAR_SLEW_DOWN = new BDInputInfo("Slew Down");
		public static BDInputInfo RADAR_SCAN_MODE = new BDInputInfo("Scan Mode");
		public static BDInputInfo RADAR_TURRETS = new BDInputInfo("Slave Turrets");
		public static BDInputInfo RADAR_RANGE_UP = new BDInputInfo("Range +");
		public static BDInputInfo RADAR_RANGE_DN = new BDInputInfo("Range -");


		public static void SaveSettings()
		{
			ConfigNode fileNode = ConfigNode.Load(BDArmorySettings.settingsConfigURL);
			if(!fileNode.HasNode("BDAInputSettings"))
			{
				fileNode.AddNode("BDAInputSettings");
			}

			ConfigNode cfg = fileNode.GetNode("BDAInputSettings");

			FieldInfo[] fields = typeof(BDInputSettingsFields).GetFields();
			for(int i = 0; i < fields.Length; i++)
			{
				string fieldName = fields[i].Name;
				string valueString = ((BDInputInfo)fields[i].GetValue(null)).inputString;
				cfg.SetValue(fieldName, valueString, true);
			}

			fileNode.Save(BDArmorySettings.settingsConfigURL);
		}

		public static void LoadSettings()
		{
			ConfigNode fileNode = ConfigNode.Load(BDArmorySettings.settingsConfigURL);
			if(!fileNode.HasNode("BDAInputSettings"))
			{
				fileNode.AddNode("BDAInputSettings");
			}

			ConfigNode cfg = fileNode.GetNode("BDAInputSettings");

			FieldInfo[] fields = typeof(BDInputSettingsFields).GetFields();
			for(int i = 0; i < fields.Length; i++)
			{
				string fieldName = fields[i].Name;
			  if (!cfg.HasValue(fieldName)) continue;
			  BDInputInfo orig = (BDInputInfo)fields[i].GetValue(null);
			  BDInputInfo loaded = new BDInputInfo(cfg.GetValue(fieldName), orig.description);
			  fields[i].SetValue(null, loaded);
			}

			fileNode.Save(BDArmorySettings.settingsConfigURL);
		}
	}
}

