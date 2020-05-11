using BDArmory.Modules;
using UnityEngine;

namespace BDArmory.Guidances
{
    public interface IGuidance
    {
        Vector3 GetDirection(MissileBase missile, Vector3 targetPosition, out double timeToImpact);
    }
}