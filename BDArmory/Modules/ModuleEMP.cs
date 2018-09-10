
namespace BDArmory.Modules
{
    public class ModuleEMP : PartModule
    {
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = false, guiName = "EMP Blast Radius"),
         UI_Label(affectSymCounterparts = UI_Scene.All, controlEnabled = true, scene = UI_Scene.All)]
        public float proximity = 5000;

        public override void OnStart(StartState state)
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                part.force_activate();
                part.OnJustAboutToBeDestroyed += DetonateEMPRoutine;
            }
            base.OnStart(state);
        }

        public void DetonateEMPRoutine()
        {
            foreach (Vessel v in FlightGlobals.Vessels)
            {
                if (!v.HoldPhysics)
                {
                    double targetDistance = Vector3d.Distance(this.vessel.GetWorldPos3D(), v.GetWorldPos3D());

                    if (targetDistance <= proximity)
                    {
                        var count = 0;

                        foreach (Part p in v.parts)
                        {
                            var wmPart = p.FindModuleImplementing<MissileFire>();

                            if (wmPart != null)
                            {
                                count = 1;
                                p.AddModule("ModuleDrainEC");
                            }
                        }

                        if (count == 0)
                        {
                            v.rootPart.AddModule("ModuleDrainEC");
                        }
                    }
                }
            }
        }
    }
}