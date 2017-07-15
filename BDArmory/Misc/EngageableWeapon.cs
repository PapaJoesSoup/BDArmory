namespace BDArmory.Misc
{
    public abstract class EngageableWeapon : PartModule, IEngageService
    {
        public bool EngageEnabled = true;
        // Weapon usage settings
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Engage Range Min"),
         UI_FloatRange(minValue = 0f, maxValue = 5000f, stepIncrement = 100f, scene = UI_Scene.Editor)]
        public float engageRangeMin;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Engage Range Max"),
         UI_FloatRange(minValue = 0f, maxValue = 5000f, stepIncrement = 100f, scene = UI_Scene.Editor)]
        public float engageRangeMax;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Engage Air"),
         UI_Toggle(disabledText = "false", enabledText = "true")]
        public bool engageAir = true;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Engage Missile"),
         UI_Toggle(disabledText = "false", enabledText = "true")]
        public bool engageMissile = true;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Engage Surface"),
         UI_Toggle(disabledText = "false", enabledText = "true")]
        public bool engageGround = true;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Engage SLW"),
        UI_Toggle(disabledText = "false", enabledText = "true")]
        public bool engageSLW = true;

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Disable Engage Options", active = true)]
        public void ToggleEngageOptions()
        {
            
            EngageEnabled = !EngageEnabled;

            if (EngageEnabled == false)
            {
                Events["ToggleEngageOptions"].guiName = "Enable Engage Options";        
            }
            else
            {
                Events["ToggleEngageOptions"].guiName = "Disable Engage Options";
            }
            
            Fields["engageRangeMin"].guiActive = EngageEnabled;
            Fields["engageRangeMin"].guiActiveEditor = EngageEnabled;
            Fields["engageRangeMax"].guiActive = EngageEnabled;
            Fields["engageRangeMax"].guiActiveEditor = EngageEnabled;
            Fields["engageAir"].guiActive = EngageEnabled;
            Fields["engageAir"].guiActiveEditor = EngageEnabled;
            Fields["engageMissile"].guiActive = EngageEnabled;
            Fields["engageMissile"].guiActiveEditor = EngageEnabled;
            Fields["engageGround"].guiActive = EngageEnabled;
            Fields["engageGround"].guiActiveEditor = EngageEnabled;
            Fields["engageSLW"].guiActive = EngageEnabled;
            Fields["engageSLW"].guiActiveEditor = EngageEnabled;

            Misc.RefreshAssociatedWindows(part);
        }
        public void OnRangeUpdated(BaseField field, object obj)
        {
            // ensure max >= min
            if (engageRangeMax < engageRangeMin)
                engageRangeMax = engageRangeMin;
        }

        protected void InitializeEngagementRange(float min, float max)
        {

            UI_FloatRange rangeMin = (UI_FloatRange)Fields["engageRangeMin"].uiControlEditor;
            rangeMin.minValue = min;
            rangeMin.maxValue = max;
            rangeMin.onFieldChanged = OnRangeUpdated;

            UI_FloatRange rangeMax = (UI_FloatRange)Fields["engageRangeMax"].uiControlEditor;
            rangeMax.minValue = min;
            rangeMax.maxValue = max;
            rangeMax.onFieldChanged = OnRangeUpdated;

            if ((engageRangeMin == 0) && (engageRangeMax == 0))
            {
                // no sensible settings yet, set to default
                engageRangeMin = min;
                engageRangeMax = max;
            }
        }
        
        //implementations from Interface
        public float GetEngagementRangeMin()
        {
            return engageRangeMin;
        }

        public float GetEngagementRangeMax()
        {
            return engageRangeMax;
        }

        public bool GetEngageAirTargets()
        {
            return engageAir;
        }

        public bool GetEngageMissileTargets()
        {
            return engageMissile;
        }

        public bool GetEngageGroundTargets()
        {
            return engageGround;
        }
        public bool GetEngageSLWTargets()
        {
            return engageSLW;
        }

    }
}