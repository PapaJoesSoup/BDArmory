namespace BahaTurret
{
    public abstract class EngageableWeapon : PartModule, IEngageService
    {
        private bool _engageEnabled = true;
        // Weapon usage settings
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Engage Range Min"),
         UI_FloatRange(minValue = 0f, maxValue = 5000f, stepIncrement = 100f, scene = UI_Scene.Editor)]
        public float engageRangeMin = 0;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Engage Range Max"),
         UI_FloatRange(minValue = 0f, maxValue = 5000f, stepIncrement = 100f, scene = UI_Scene.Editor)]
        public float engageRangeMax = 0;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Engage Air"),
         UI_Toggle(disabledText = "false", enabledText = "true")]
        public bool engageAir = true;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Engage Missile"),
         UI_Toggle(disabledText = "false", enabledText = "true")]
        public bool engageMissile = true;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Engage Ground"),
         UI_Toggle(disabledText = "false", enabledText = "true")]
        public bool engageGround = true;


        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Engage Options", active = true)]
        public void ToggleEngageOptions()
        {
            _engageEnabled = !_engageEnabled;

            Fields["engageRangeMin"].guiActive = _engageEnabled;
            Fields["engageRangeMin"].guiActiveEditor = _engageEnabled;
            Fields["engageRangeMax"].guiActive = _engageEnabled;
            Fields["engageRangeMax"].guiActiveEditor = _engageEnabled;
            Fields["engageAir"].guiActive = _engageEnabled;
            Fields["engageAir"].guiActiveEditor = _engageEnabled;
            Fields["engageMissile"].guiActive = _engageEnabled;
            Fields["engageMissile"].guiActiveEditor = _engageEnabled;
            Fields["engageGround"].guiActive = _engageEnabled;
            Fields["engageGround"].guiActiveEditor = _engageEnabled;
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

            var rangeMin = (UI_FloatRange)this.Fields["engageRangeMin"].uiControlEditor;
            rangeMin.minValue = min;
            rangeMin.maxValue = max;
            rangeMin.onFieldChanged = OnRangeUpdated;

            var rangeMax = (UI_FloatRange)this.Fields["engageRangeMax"].uiControlEditor;
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

    }
}