namespace BahaTurret
{
    public interface IEngageService
    {
        float GetEngagementRangeMax();
        bool GetEngageAirTargets();
        bool GetEngageMissileTargets();
        bool GetEngageGroundTargets();
    }
}