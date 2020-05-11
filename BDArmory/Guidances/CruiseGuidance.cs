using System;
using BDArmory.Core.Extension;
using BDArmory.Misc;
using BDArmory.Modules;
using UnityEngine;

namespace BDArmory.Guidances
{
    public enum GuidanceState
    {
        Ascending,
        Cruising,
        Descending,
        Terminal
    }

    public enum PitchDecision
    {
        Ascent,
        Descent,
        Hold,
        EmergencyAscent
    }

    public enum ThrottleDecision
    {
        Increase,
        Decrease,
        Hold
    }

    public class CruiseGuidance : IGuidance
    {
        private readonly MissileBase _missile;
        private Vector3 _startPoint;
        private double _originalDistance = float.MinValue;
        private float _pitchAngle;
        private double _futureAltitude;
        private double _futureSpeed;
        private double _horizontalAcceleration;

        private float _lastDataRead;
        private double _lastHorizontalSpeed;
        private double _lastSpeedDelta;
        private double _lastVerticalSpeed;

        private double _verticalAcceleration;

        private Vector3 planarDirectionToTarget;
        private Vector3 upDirection;

        public CruiseGuidance(MissileBase missile)
        {
            _missile = missile;
        }

        public ThrottleDecision ThrottleDecision { get; set; }
        public PitchDecision PitchDecision { get; set; }

        public GuidanceState GuidanceState { get; set; }

        public Vector3 GetDirection(MissileBase missile, Vector3 targetPosition, out double timeToImpact)
        {
            //set up
            if (_originalDistance == float.MinValue)
            {
                _startPoint = missile.vessel.CoM;
                _originalDistance = Vector3.Distance(targetPosition, missile.vessel.CoM);
            }

            var surfaceDistanceVector = Vector3
                .Project((missile.vessel.CoM - _startPoint), (targetPosition - _startPoint).normalized);

            var pendingDistance = _originalDistance - surfaceDistanceVector.magnitude;

            timeToImpact = pendingDistance / missile.vessel.horizontalSrfSpeed;


            if (_missile.TimeIndex < 1)
                return _missile.vessel.CoM + _missile.vessel.Velocity() * 10;

            upDirection = VectorUtils.GetUpDirection(_missile.vessel.CoM);

            planarDirectionToTarget =
                Vector3.ProjectOnPlane(targetPosition - _missile.vessel.CoM, upDirection).normalized;

            // Ascending
            _missile.debugString.Append("State=" + GuidanceState);
            _missile.debugString.Append(Environment.NewLine);

            var missileAltitude = GetCurrentAltitude(_missile.vessel);
            _missile.debugString.Append("Altitude=" + missileAltitude);
            _missile.debugString.Append(Environment.NewLine);

            _missile.debugString.Append("Apoapsis=" + _missile.vessel.orbit.ApA);
            _missile.debugString.Append(Environment.NewLine);

            _missile.debugString.Append("Future Altitude=" + _futureAltitude);
            _missile.debugString.Append(Environment.NewLine);

            _missile.debugString.Append("Pitch angle=" + _pitchAngle);
            _missile.debugString.Append(Environment.NewLine);

            _missile.debugString.Append("Pitch decision=" + PitchDecision);
            _missile.debugString.Append(Environment.NewLine);

            _missile.debugString.Append("lastVerticalSpeed=" + _lastVerticalSpeed);
            _missile.debugString.Append(Environment.NewLine);

            _missile.debugString.Append("verticalAcceleration=" + _verticalAcceleration);
            _missile.debugString.Append(Environment.NewLine);

            GetTelemetryData();

            switch (GuidanceState)
            {
                case GuidanceState.Ascending:
                    UpdateThrottle();

                    if (MissileWillReachAltitude(missileAltitude))
                    {
                        _pitchAngle = 0;
                        GuidanceState = GuidanceState.Cruising;

                        break;
                    }

                    CheckIfTerminal(missileAltitude, targetPosition, upDirection);

                    return _missile.vessel.CoM + (planarDirectionToTarget.normalized + upDirection.normalized) * 10f;

                case GuidanceState.Cruising:

                    CheckIfTerminal(missileAltitude, targetPosition, upDirection);
                    //Altitude control
                    UpdatePitch(missileAltitude);
                    UpdateThrottle();

                    return _missile.vessel.CoM + 10 * planarDirectionToTarget.normalized + _pitchAngle * upDirection;

                case GuidanceState.Terminal:

                    _missile.debugString.Append($"Descending");
                    _missile.debugString.Append(Environment.NewLine);

                    _missile.Throttle = Mathf.Clamp((float)(_missile.vessel.atmDensity * 10f), 0.01f, 1f);

                    if (_missile is BDModularGuidance)
                        if (_missile.vessel.InVacuum())
                            return _missile.vessel.CoM + _missile.vessel.Velocity() * 10;

                    return MissileGuidance.GetAirToGroundTarget(targetPosition, _missile.vessel, 1.85f);
            }

            return _missile.vessel.CoM + _missile.vessel.Velocity() * 10;
        }

        private double CalculateFreeFallTime(double missileAltitude)
        {
            double vi = -_missile.vessel.verticalSpeed;
            double a = 9.80665f;
            double d = missileAltitude;

            double time1 = (-vi + Math.Sqrt(Math.Pow(vi, 2) - 4 * (0.5f * a) * (-d))) / a;
            double time2 = (-vi - Math.Sqrt(Math.Pow(vi, 2) - 4 * (0.5f * a) * (-d))) / a;

            return Math.Max(time1, time2);
        }

        private float GetProperDescentRatio(double missileAltitude)
        {
            float altitudePercentage = Mathf.Clamp01((float)(missileAltitude / 1000f));

            return Mathf.Lerp(-1f, 1.85f, altitudePercentage);
        }

        private void GetTelemetryData()
        {
            _lastDataRead = Time.time;

            _verticalAcceleration = (_missile.vessel.verticalSpeed - _lastVerticalSpeed);
            _lastVerticalSpeed = _missile.vessel.verticalSpeed;

            _horizontalAcceleration = (_missile.vessel.horizontalSrfSpeed - _lastHorizontalSpeed);
            _lastHorizontalSpeed = _missile.vessel.horizontalSrfSpeed;
        }

        private bool CheckIfTerminal(double altitude, Vector3 targetPosition, Vector3 upDirection)
        {
            Vector3 surfacePos = this._missile.vessel.transform.position +
                                 Vector3.Project(targetPosition - this._missile.vessel.transform.position, -upDirection);

            float distanceToTarget = Vector3.Distance(surfacePos, targetPosition);

            _missile.debugString.Append($"Distance to target" + distanceToTarget);
            _missile.debugString.Append(Environment.NewLine);
            double freefallTime = CalculateFreeFallTime(altitude);

            _missile.debugString.Append($"freefallTime" + freefallTime);
            _missile.debugString.Append(Environment.NewLine);

            if (distanceToTarget < (freefallTime * _missile.vessel.horizontalSrfSpeed))
            {
                GuidanceState = GuidanceState.Terminal;
                return true;
            }
            return false;
        }

        private void UpdateThrottle()
        {
            MakeDecisionAboutThrottle(_missile);
        }

        private void UpdatePitch(double missileAltitude)
        {
            MakeDecisionAboutPitch(_missile, missileAltitude);
        }

        private double GetCurrentAltitude(Vessel missileVessel)
        {
            var currentRadarAlt = MissileGuidance.GetRadarAltitude(missileVessel);
            return currentRadarAlt;
        }

        private double GetCurrentAltitudeAtPosition(Vector3 position)
        {
            var currentRadarAlt = MissileGuidance.GetRadarAltitudeAtPos(position);

            return currentRadarAlt;
        }

        private bool CalculateFutureCollision(float predictionTime)
        {
            var terrainRay = new Ray(this._missile.vessel.CoM, this._missile.vessel.Velocity());
            RaycastHit hit;
            return Physics.Raycast(terrainRay, out hit, (float)(this._missile.vessel.srfSpeed * predictionTime), (1 << 15) | (1 << 17));
        }

        private void MakeDecisionAboutThrottle(MissileBase missile)
        {
            const double maxError = 10;
            _futureSpeed = CalculateFutureSpeed();

            var currentSpeedDelta = missile.vessel.horizontalSrfSpeed - _missile.CruiseSpeed;

            if (_futureSpeed > missile.CruiseSpeed)
                ThrottleDecision = ThrottleDecision.Decrease;
            else if (Math.Abs(_futureSpeed - _missile.CruiseSpeed) < maxError)
                ThrottleDecision = ThrottleDecision.Hold;
            else
                ThrottleDecision = ThrottleDecision.Increase;

            switch (ThrottleDecision)
            {
                case ThrottleDecision.Increase:
                    missile.Throttle = Mathf.Clamp(missile.Throttle + 0.001f, 0, 1f);
                    break;

                case ThrottleDecision.Decrease:
                    missile.Throttle = Mathf.Clamp(missile.Throttle - 0.001f, 0, 1f);
                    break;

                case ThrottleDecision.Hold:
                    break;
            }

            _lastSpeedDelta = currentSpeedDelta;
        }

        private void MakeDecisionAboutPitch(MissileBase missile, double missileAltitude)
        {
            _futureAltitude = CalculateFutureAltitude(_missile.CruisePredictionTime);

            PitchDecision futureDecision;

            if (this.GuidanceState != GuidanceState.Terminal &&
                (missileAltitude < 4d || CalculateFutureAltitude(1f) < 4d))
            {
                futureDecision = PitchDecision.EmergencyAscent;
            }
            else if (this.GuidanceState != GuidanceState.Terminal && CalculateFutureCollision(_missile.CruisePredictionTime))
            {
                futureDecision = PitchDecision.EmergencyAscent;
            }
            else if (_futureAltitude < missile.CruiseAltitude || missileAltitude < missile.CruiseAltitude)
            {
                futureDecision = PitchDecision.Ascent;
            }
            else if (_futureAltitude > missile.CruiseAltitude || missileAltitude > missile.CruiseAltitude)
            {
                futureDecision = PitchDecision.Descent;
            }
            else
            {
                futureDecision = PitchDecision.Hold;
            }

            switch (futureDecision)
            {
                case PitchDecision.EmergencyAscent:
                    if (PitchDecision == futureDecision)
                    {
                        _pitchAngle = Mathf.Clamp(_pitchAngle + 1f, 1.5f, 100f);
                    }
                    else
                    {
                        _pitchAngle = 1.5f;
                    }
                    break;

                case PitchDecision.Ascent:
                    _pitchAngle = Mathf.Clamp(_pitchAngle + 0.0055f, -1.5f, 1.5f);
                    break;

                case PitchDecision.Descent:
                    _pitchAngle = Mathf.Clamp(_pitchAngle - 0.0025f, -1.5f, 1.5f);
                    break;

                case PitchDecision.Hold:
                    break;
            }

            PitchDecision = futureDecision;
        }

        private double CalculateFutureAltitude(float predictionTime)
        {
            Vector3 futurePosition = _missile.vessel.CoM + _missile.vessel.Velocity() * predictionTime
                + 0.5f * _missile.vessel.acceleration_immediate * Math.Pow(predictionTime, 2);

            return GetCurrentAltitudeAtPosition(futurePosition);
        }

        private double CalculateFutureSpeed()
        {
            return _missile.vessel.horizontalSrfSpeed + (_horizontalAcceleration / Time.fixedDeltaTime) * _missile.CruisePredictionTime;
        }

        private bool MissileWillReachAltitude(double currentAltitude)
        {
            return _missile.vessel.orbit.ApA > _missile.CruiseAltitude;
        }
    }
}
