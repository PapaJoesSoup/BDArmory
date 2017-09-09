namespace BDArmory.Misc
{
    public abstract class EngageableWeapon : PartModule, IEngageService
    {
        [KSPField(isPersistant = true)]
        public bool engageEnabled = true;
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

        [KSPEvent( guiActive = true, guiActiveEditor = true, guiName = "Disable Engage Options", active = true)]
        public void ToggleEngageOptions()
        {
            
            engageEnabled = !engageEnabled;

            if (engageEnabled == false)
            {
                Events["ToggleEngageOptions"].guiName = "Enable Engage Options";        
            }
            else
            {
                Events["ToggleEngageOptions"].guiName = "Disable Engage Options";
            }
            
            Fields["engageRangeMin"].guiActive = engageEnabled;
            Fields["engageRangeMin"].guiActiveEditor = engageEnabled;
            Fields["engageRangeMax"].guiActive = engageEnabled;
            Fields["engageRangeMax"].guiActiveEditor = engageEnabled;
            Fields["engageAir"].guiActive = engageEnabled;
            Fields["engageAir"].guiActiveEditor = engageEnabled;
            Fields["engageMissile"].guiActive = engageEnabled;
            Fields["engageMissile"].guiActiveEditor = engageEnabled;
            Fields["engageGround"].guiActive = engageEnabled;
            Fields["engageGround"].guiActiveEditor = engageEnabled;
            Fields["engageSLW"].guiActive = engageEnabled;
            Fields["engageSLW"].guiActiveEditor = engageEnabled;

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
            rangeMin.stepIncrement = (max - min) / 100f;
            rangeMin.onFieldChanged = OnRangeUpdated;

            UI_FloatRange rangeMax = (UI_FloatRange)Fields["engageRangeMax"].uiControlEditor;
            rangeMax.minValue = min;
            rangeMax.maxValue = max;
            rangeMax.stepIncrement = (max - min) / 100f;
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