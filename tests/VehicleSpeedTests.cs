using System;
using OneWayDps;

namespace UnityEngine {
    public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}}
}
namespace World {
    public sealed class WorldContext {public UnityEngine.Vector2 caravan_velocity;}
}
namespace World.Caravans {
    public sealed class Caravan {}
    public sealed class CaravanMgr {public Caravan cell;public World.WorldContext ctx;}
}
namespace Foundations {
    public sealed class Mission {
        public static Mission instance;
        public object Manager;
        public string LastKey;
        public bool Throw;
        public bool try_get_mgr<T>(string key,out T value) where T:class {
            LastKey=key;
            if(Throw)throw new InvalidOperationException("Unavailable mission");
            value=Manager as T;
            return value!=null;
        }
    }
}
class VehicleSpeedTests {
    static int passed;
    static void Check(bool condition,string name){if(!condition)throw new Exception(name);passed++;}
    static void Main() {
        float speed;
        Check(VehicleSpeed.TryNormalizeHorizontal(0,out speed) && speed==0,"stationary is valid zero");
        Check(VehicleSpeed.TryNormalizeHorizontal(12.5f,out speed) && speed==12.5f,"forward speed");
        Check(VehicleSpeed.TryNormalizeHorizontal(-12.5f,out speed) && speed==12.5f,"reverse speed is absolute");
        Check(!VehicleSpeed.TryNormalizeHorizontal(float.NaN,out speed) && speed==0,"NaN unavailable");
        Check(!VehicleSpeed.TryNormalizeHorizontal(float.PositiveInfinity,out speed) && speed==0,"positive infinity unavailable");
        Check(!VehicleSpeed.TryNormalizeHorizontal(float.NegativeInfinity,out speed) && speed==0,"negative infinity unavailable");
        Check(VehicleSpeed.TryNormalizeHorizontal(float.MinValue,out speed) && speed==float.MaxValue,"finite extreme does not overflow");
        Check(VehicleSpeed.TryToMetersPerSecond(0,out speed) && speed==0,"stationary meters per second");
        Check(VehicleSpeed.TryToMetersPerSecond(12.5f,out speed) && speed==25,"meter display uses verified double distance scale");
        Check(VehicleSpeed.TryToMetersPerSecond(-7,out speed) && speed==14,"reverse converted to positive meters per second");
        Check(!VehicleSpeed.TryToMetersPerSecond(float.NaN,out speed) && speed==0,"NaN conversion unavailable");
        Check(!VehicleSpeed.TryToMetersPerSecond(float.PositiveInfinity,out speed) && speed==0,"infinite conversion unavailable");
        Check(!VehicleSpeed.TryToMetersPerSecond(float.MaxValue,out speed) && speed==0,"conversion overflow unavailable");
        Check(VehicleSpeed.TryToMetersPerSecond(float.MaxValue/2,out speed) && speed==float.MaxValue,"largest representable conversion remains valid");
        Foundations.Mission.instance=null;
        Check(!VehicleSpeed.TryRead(out speed) && speed==0,"missing mission unavailable");
        var mission=new Foundations.Mission();Foundations.Mission.instance=mission;
        Check(!VehicleSpeed.TryRead(out speed) && speed==0,"missing caravan manager unavailable");
        Check(mission.LastKey=="CaravanMgr","uses verified manager name");
        var manager=new World.Caravans.CaravanMgr();mission.Manager=manager;
        Check(!VehicleSpeed.TryRead(out speed),"uninitialized caravan unavailable");
        manager.cell=new World.Caravans.Caravan();
        Check(!VehicleSpeed.TryRead(out speed),"missing context unavailable");
        manager.ctx=new World.WorldContext();manager.ctx.caravan_velocity=new UnityEngine.Vector2(18,900);
        Check(VehicleSpeed.TryRead(out speed) && speed==36,"vertical motion excluded and meter scale applied");
        manager.ctx.caravan_velocity=new UnityEngine.Vector2(-7,-500);
        Check(VehicleSpeed.TryRead(out speed) && speed==14,"reads fresh current meters per second each call");
        manager.ctx.caravan_velocity=new UnityEngine.Vector2(0,10);
        Check(VehicleSpeed.TryRead(out speed) && speed==0,"stationary horizontal during jump remains zero");
        manager.ctx.caravan_velocity=new UnityEngine.Vector2(float.NaN,0);
        Check(!VehicleSpeed.TryRead(out speed) && speed==0,"invalid live velocity unavailable");
        mission.Throw=true;
        Check(!VehicleSpeed.TryRead(out speed) && speed==0,"scene transition failure safe");
        Console.WriteLine("PASS vehicle speed: "+passed+" assertions");
    }
}
