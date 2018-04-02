#if DEBUG
// This will only be live in debug builds
using System;
using UnityEngine;
using BDArmory.Core;

namespace BDArmory.UI
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class KrakensbaneDebug : MonoBehaviour
    {
        float lastShift = 0;

        void FixedUpdate()
        {
            if (!FloatingOrigin.Offset.IsZero())
                lastShift = Time.time;
        }

        void OnGUI()
        {
            if (BDArmorySettings.DRAW_DEBUG_LABELS)
            {
                var frameVelocity = Krakensbane.GetFrameVelocityV3f();
                GUI.Label(new Rect(10, 60, 400, 400),
                    $"Frame velocity: {frameVelocity.magnitude} ({frameVelocity}){Environment.NewLine}"
                    + $"Last offset {Time.time - lastShift}s ago{Environment.NewLine}"
                    + $"Local vessel speed: {FlightGlobals.ActiveVessel.rb_velocity.magnitude}, ({FlightGlobals.ActiveVessel.rb_velocity}){Environment.NewLine}"
                    //+ $"Ref frame is {(FlightGlobals.RefFrameIsRotating ? "" : "not ")}rotating{Environment.NewLine}"
                    );
            }
        }
    }
}
#endif
