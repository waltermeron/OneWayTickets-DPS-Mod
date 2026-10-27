using System;
using System.Linq;
using System.Reflection;
using BepInEx;
namespace OneWayDps.Loading {
    [BepInPlugin("suika.oneway.dps","One Way Tickets DPS Visualizer","1.3.0")]
    public sealed class Plugin : BaseUnityPlugin {
        void Awake(){
            if(!Patcher.Ready){Logger.LogError("DPS is disabled. See OneWayDps Preloader errors in BepInEx/LogOutput.log. "+Patcher.Failure);return;}
            try{
                int copies=AppDomain.CurrentDomain.GetAssemblies().Count(a=>a.FullName.StartsWith("OneWayDps,"));
                if(copies!=1)throw new Exception("Expected one DPS runtime assembly, found "+copies);
                // The original menu/save hooks boot the HUD after the game finishes
                // startup; do not create a second early HUD during the Unity bootstrap.
                Logger.LogInfo("DPS v1.3.0 enabled. Existing DPSMod/settings.json and history are preserved.");
            }catch(Exception e){Logger.LogError("DPS bootstrap failed: "+e);}
        }
    }
}
