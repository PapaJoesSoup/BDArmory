using BDArmory.Core.Extension;
using UnityEngine;

namespace BDArmory.Core.Module
{
    public class HitpointTracker : PartModule
    {
        #region KSP Fields

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Hitpoints"),
        UI_ProgressBar(affectSymCounterparts = UI_Scene.None, controlEnabled = false, scene = UI_Scene.All, maxValue = 100000, minValue = 0, requireFullControl = false)]
        public float Hitpoints;

        [KSPField]
        bool hasPrefabRun = false;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Armor Thickness"),
        UI_FloatRange(minValue = 1f, maxValue = 500f, stepIncrement = 5f, scene = UI_Scene.All)]
        public float Armor = 10f;

        [KSPField(isPersistant = true)]
        public float maxHitPoints = 0f;

        [KSPField(isPersistant = true)]
        public float ArmorThickness = 0f;

        [KSPField(isPersistant = true)]
        public bool ArmorSet;

        [KSPField(isPersistant = true)]
        public string ExplodeMode = "Never";

        [KSPField(isPersistant = true)]
        public bool FireFX = true;

        [KSPField(isPersistant = true)]
        public float FireFXLifeTimeInSeconds = 5f;

        #endregion

        private readonly float hitpointMultiplier = BDArmorySettings.HITPOINT_MULTIPLIER;

        private float previousHitpoints;
        private bool _firstSetup = true;
        private bool _updateHitpoints = false;
        private bool _forceUpdateHitpointsUI = false;
        private const int HpRounding = 100;


        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight) return;

            if (part.partInfo == null)
            {
                // Loading of the prefab from the part config
                _updateHitpoints = true;

            }
            else
            {
                // Loading of the part from a saved craft                
                if (HighLogic.LoadedSceneIsEditor)
                {
                    _updateHitpoints = true;    
                }
                else
                    enabled = false;
            }

        }

        public void SetupPrefab()
        {
            if (part != null)
            {
                var maxHitPoints_ = CalculateTotalHitpoints();

                if (!_forceUpdateHitpointsUI &&  previousHitpoints == maxHitPoints_) return;

                //Add Hitpoints
                UI_ProgressBar damageFieldFlight = (UI_ProgressBar)Fields["Hitpoints"].uiControlFlight;
                damageFieldFlight.maxValue = maxHitPoints_;
                damageFieldFlight.minValue = 0f;

                UI_ProgressBar damageFieldEditor = (UI_ProgressBar)Fields["Hitpoints"].uiControlEditor;
                damageFieldEditor.maxValue = maxHitPoints_;
                damageFieldEditor.minValue = 0f;

                Hitpoints = maxHitPoints_;

                //Add Armor
                UI_FloatRange armorFieldFlight = (UI_FloatRange)Fields["Armor"].uiControlFlight;
                armorFieldFlight.maxValue = 500f;
                armorFieldFlight.minValue = 10;

                UI_FloatRange armorFieldEditor = (UI_FloatRange)Fields["Armor"].uiControlEditor;
                armorFieldEditor.maxValue = 500f;
                armorFieldEditor.minValue = 10f;
                part.RefreshAssociatedWindows();

                if (!ArmorSet) overrideArmorSetFromConfig();

                previousHitpoints = maxHitPoints_;
                hasPrefabRun = true;
            }
            else
            {

                Debug.Log("[BDArmory]: HitpointTracker::OnStart part is null");
            }
        }

        public override void OnStart(StartState state)
        {
            isEnabled = true;

            if (part != null) _updateHitpoints = true;

            if (HighLogic.LoadedSceneIsFlight)
            {
                UI_FloatRange armorField = (UI_FloatRange)Fields["Armor"].uiControlFlight;
                //Once started the max value of the field should be the initial one
                armorField.maxValue = Armor;
                part.RefreshAssociatedWindows();
            }
            GameEvents.onEditorShipModified.Add(ShipModified);
        }



        private void OnDestroy()
        {
            GameEvents.onEditorShipModified.Remove(ShipModified);   
        }

        public void ShipModified(ShipConstruct data)
        {
            _updateHitpoints = true;
        }
        public override void OnUpdate()
        {

            RefreshHitPoints();
        }

        public void Update()
        {

            RefreshHitPoints();
        }

        private void RefreshHitPoints()
        {

            if (_updateHitpoints)
            {
                SetupPrefab();
                _updateHitpoints = false;
                _forceUpdateHitpointsUI = false;
            }
        }

        #region Hitpoints Functions

        public float CalculateTotalHitpoints()
        {
            float hitpoints;

            if (!part.IsMissile())
            {

                var averageSize = part.GetAverageBoundSize();
                var sphereRadius = averageSize * 0.5f;
                var sphereSurface = 4 * Mathf.PI * sphereRadius * sphereRadius;
                var structuralVolume = sphereSurface * 0.1f;

                var density = (part.mass * 1000f) / structuralVolume;
                density = Mathf.Clamp(density, 1000, 10000);
                //Debug.Log("[BDArmory]: Hitpoint Calc" + part.name + " | structuralVolume : " + structuralVolume);
                //Debug.Log("[BDArmory]: Hitpoint Calc"+part.name+" | Density : " + density);
                
                var structuralMass = density * structuralVolume;
                //Debug.Log("[BDArmory]: Hitpoint Calc" + part.name + " | structuralMass : " + structuralMass);
                //3. final calculations 
                hitpoints =  structuralMass * hitpointMultiplier *0.33f;
                hitpoints = Mathf.Round(hitpoints / HpRounding) * HpRounding;

                if (hitpoints <= 0) hitpoints = HpRounding;
            }
            else
            {
                hitpoints = 5;
                Armor = 2;
            }

            //override based on part configuration for custom parts
            if (maxHitPoints != 0)
            {
                hitpoints = maxHitPoints;
            }

            if (hitpoints <= 0) hitpoints = HpRounding;
            return hitpoints;
        }

        public void DestroyPart()
        {
            if (part.mass <= 2f) part.explosionPotential *= 0.85f;

            PartExploderSystem.AddPartToExplode(part);
        }

        public float GetMaxArmor()
        {
            UI_FloatRange armorField = (UI_FloatRange)Fields["Armor"].uiControlEditor;
            return armorField.maxValue;
        }

        public float GetMaxHitpoints()
        {
            UI_ProgressBar hitpointField = (UI_ProgressBar)Fields["Hitpoints"].uiControlEditor;
            return hitpointField.maxValue;
        }

        public bool GetFireFX()
        {
            return FireFX;
        }

        public void SetDamage(float partdamage)
        {
            Hitpoints -= partdamage;

            if (Hitpoints <= 0)
            {
                DestroyPart();
            }
        }

        public void AddDamage(float partdamage)
        {
            if (part.name == "Weapon Manager" || part.name == "BDModulePilotAI") return;

            partdamage = Mathf.Max(partdamage, 0.01f) * -1;
            Hitpoints += partdamage;

            if (Hitpoints <= 0)
            {
                DestroyPart();
            }
        }

        public void AddDamageToKerbal(KerbalEVA kerbal, float damage)
        {
            damage = Mathf.Max(damage, 0.01f) * -1;
            Hitpoints += damage;

            if (Hitpoints <= 0)
            {
                // oh the humanity!
                PartExploderSystem.AddPartToExplode(kerbal.part);
            }
        }

        public void ReduceArmor(float massToReduce)
        {
            Armor -= massToReduce;
            if (Armor < 0)
            {
                Armor = 0;
            }
        }

        public void overrideArmorSetFromConfig(float thickness = 0)
        {
            ArmorSet = true;
            if (ArmorThickness != 0)
            {
                Armor = ArmorThickness;
            }
        }

        #endregion

    }
}