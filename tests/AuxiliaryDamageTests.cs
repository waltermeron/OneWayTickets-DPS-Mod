using System;
using OneWayDps;
using World;
namespace UnityEngine { public static class Debug { public static void LogWarning(object x){Console.WriteLine(x);} } }
namespace World {
    public static class WorldEnum { public enum Faction { player,neutral,opposite } }
    public class WorldContext { public bool in_safe_area,is_in_settlement,is_freeze_time; }
    public struct Attack_Data { public int atk; }
    public struct Dmg_Data { public int dmg; public bool is_heal,is_miss,is_immune; }
    public interface ITarget { WorldEnum.Faction Faction {get;} int hp {get;} void hurt(ITarget source,Attack_Data attack,out Dmg_Data result); }
    public class FakeTarget : ITarget {
        public WorldEnum.Faction faction;public int health=100,calls;public Dmg_Data next;
        public WorldEnum.Faction Faction {get{return faction;}}public int hp {get{return health;}}
        public void hurt(ITarget source,Attack_Data attack,out Dmg_Data result){calls++;result=next;if(!result.is_heal && !result.is_miss && !result.is_immune)health-=result.dmg;}
    }
}
namespace World.Devices { public class Device : FakeTarget {} }
namespace World.Projectiles { public class Projectile : FakeTarget { public ITarget emitter; public AutoCodes.projectile desc; } }
namespace World.Adherents { public class Adherent : FakeTarget { public ITarget _source; public Adherent(){faction=WorldEnum.Faction.neutral;} } }
namespace OneWayDps { public static class DpsOverlay { public static bool Active;public static readonly CombatStats Stats=new CombatStats();public static void Begin(){Active=true;Stats.NewRun();} } }
namespace AutoCodes {public class projectile {public uint projectile_id;}}
class Relic_Thunder_Modify_Variable {}
class AuxiliaryDamageTests {
    static int passed;
    static void Eq(double got,double want,string label){if(double.IsNaN(got)||Math.Abs(got-want)>.00001)throw new Exception(label+": "+got+" != "+want);passed++;}
    static void Yes(bool value,string label){Eq(value?1:0,1,label);}
    static World.Devices.Device Weapon(){return new World.Devices.Device{faction=WorldEnum.Faction.player};}
    static CombatStats Stats {get{return DpsOverlay.Stats;}}
    static void Hit(ITarget source,int damage){var e=new FakeTarget{health=10000,faction=WorldEnum.Faction.opposite,next=new Dmg_Data{dmg=damage}};Dmg_Data result;Hooks.Hurt(e,source,new Attack_Data(),out result);Eq(e.calls,1,"game damage called exactly once");Eq(result.dmg,damage,"game result unchanged");}
    static World.Projectiles.Projectile Shoot(World.Devices.Device weapon,uint id){var prev=DamageAttribution.Enter(weapon,null,1,id);try{var p=new World.Projectiles.Projectile{emitter=weapon,faction=WorldEnum.Faction.player,desc=new AutoCodes.projectile{projectile_id=id}};DamageAttribution.ProjectileReady(p);return p;}finally{DamageAttribution.Exit(prev);}}
    static void Explode(World.Projectiles.Projectile p,int damage){var prev=DamageAttribution.Enter(p,null,3,0);try{Hit(p,damage);}finally{DamageAttribution.Exit(prev);}}
    static void Conserve(){double sum=0,share=0,dps=0;foreach(var e in Stats.Entries)if(e.Visible){sum+=e.Meter.Total;share+=Stats.Share(e);dps+=Stats.GlobalDpsFor(e);}Eq(sum,Stats.Total.Total,"visible rows conserve effective damage");Eq(share,1,"shares sum to100 percent");Eq(dps,Stats.GlobalDps,"source global DPS conserves total");}
    static void Main(){
        Hooks.Begin();var bow=Weapon();var otherBow=Weapon();var quiver=Weapon();var nextQuiver=Weapon();
        Eq(DamageAttribution.AmmoProvided(10015,quiver,bow),10015,"supply return unchanged");
        var arrow=Shoot(bow,10015);Hit(arrow,100);Explode(arrow,40);
        Eq(Stats.Register(bow).Meter.Total,140,"merged bow includes explosion");Yes(!Stats.Register(quiver).Visible,"merged hides auxiliary");Eq(Stats.Total.Total,140,"no duplicate total");Conserve();
        for(int i=0;i<30;i++)Stats.Tick();var sample=Stats.History[0];Eq(sample.Value(Stats.Register(bow).Id),28,"merged historical DPS");
        Stats.SeparateAuxiliary=true;Eq(Stats.Register(bow).Meter.Total,100,"separate bow retains direct hit");Eq(Stats.Register(quiver).Meter.Total,40,"explosion belongs to actual quiver");Yes(Stats.Register(quiver).Visible,"separate displays damaging auxiliary");Eq(sample.Value(Stats.Register(bow).Id),20,"past history switches without reset");Eq(sample.Value(Stats.Register(quiver).Id),8,"past auxiliary curve retained");Conserve();
        Stats.SeparateAuxiliary=false;Eq(sample.Value(Stats.Register(bow).Id),28,"switch back restores past curve");Eq(Stats.ReportedDamage,140,"switch never duplicates raw diagnostic");
        DamageAttribution.AmmoProvided(10015,nextQuiver,bow);var newer=Shoot(bow,10015);Explode(arrow,10);Explode(newer,20);
        Stats.SeparateAuxiliary=true;Eq(Stats.Register(quiver).Meter.Total,50,"old arrow retains original quiver after switch");Eq(Stats.Register(nextQuiver).Meter.Total,20,"new arrow uses new quiver");
        DamageAttribution.AmmoProvided(10015,quiver,otherBow);Explode(Shoot(otherBow,10015),30);Eq(Stats.Register(quiver).Meter.Total,80,"one auxiliary aggregates several weapons");
        Stats.SeparateAuxiliary=false;Eq(Stats.Register(bow).Meter.Total,170,"merged attributes each auxiliary hit to triggering bow");Eq(Stats.Register(otherBow).Meter.Total,30,"second bow remains independent");Conserve();
        Eq(DamageAttribution.AmmoProvided(1,quiver,bow),1,"ordinary ammo return unchanged");Explode(Shoot(bow,10015),5);Stats.SeparateAuxiliary=true;Eq(Stats.Register(quiver).Meter.Total,80,"ordinary ammo clears stale auxiliary");
        DamageAttribution.AmmoProvided(10015,quiver,bow);Explode(Shoot(bow,10001),7);Eq(Stats.Register(quiver).Meter.Total,80,"mismatched projectile never invents provenance");
        var reflected=Shoot(otherBow,10015);reflected.faction=WorldEnum.Faction.opposite;double before=Stats.Total.Total;Explode(reflected,100);Eq(Stats.Total.Total,before,"enemy reflected arrow stays excluded");
        var relic=new object();var prev=DamageAttribution.Enter(relic,bow,4,0);object callback=new object();World.Projectiles.Projectile child;
        try{Hit(bow,11);DamageAttribution.ObjectCreated(callback);child=Shoot(bow,10001);Hit(null,3);Hit(new FakeTarget{faction=WorldEnum.Faction.opposite},999);}finally{DamageAttribution.Exit(prev);}
        Eq(Stats.Register(relic).Meter.Total,14,"relic direct and null-source trusted damage separated");Yes(!DamageAttribution.PlayerContext,"scope restores after callback");
        Hit(child,13);Eq(Stats.Register(relic).Meter.Total,27,"generated projectile retains relic owner after trigger ends");
        prev=DamageAttribution.Enter(callback,null,5,0);try{Hit(null,17);}finally{DamageAttribution.Exit(prev);}Eq(Stats.Register(relic).Meter.Total,44,"delayed callback retains relic provenance");
        var dot=new World.Adherents.Adherent{_source=child};Hit(dot,19);Eq(Stats.Register(relic).Meter.Total,63,"child DOT follows generated projectile");
        Hit(null,50);Eq(Stats.Register(relic).Meter.Total,63,"unowned environment remains excluded");
        var noEmitter=new World.Projectiles.Projectile{faction=WorldEnum.Faction.player,desc=new AutoCodes.projectile{projectile_id=30001}};
        prev=DamageAttribution.Enter(relic,bow,4,0);try{DamageAttribution.ProjectileReady(noEmitter);}finally{DamageAttribution.Exit(prev);}
        object merged,aux;DamageAttribution.Resolve(noEmitter,out merged,out aux);Yes(Object.ReferenceEquals(merged,bow),"generated arrow with no emitter retains triggering weapon");Yes(Object.ReferenceEquals(aux,relic),"generated arrow with no emitter retains auxiliary");
        try{prev=DamageAttribution.Enter(relic,bow,4,0);try{throw new InvalidOperationException();}finally{DamageAttribution.Exit(prev);}}catch(InvalidOperationException){}Yes(!DamageAttribution.PlayerContext,"exception does not leak context");
        var a=new Relic_Thunder_Modify_Variable();var b=new Relic_Thunder_Modify_Variable();prev=DamageAttribution.Enter(a,bow,4,0);try{Hit(null,23);}finally{DamageAttribution.Exit(prev);}object group=null;foreach(var e in Stats.Entries)if(e.Key is SharedDamageSource)group=e.Key;
        prev=DamageAttribution.Enter(b,bow,4,0);try{Hit(null,29);}finally{DamageAttribution.Exit(prev);}Eq(Stats.Register(group).Meter.Total,52,"shared systems grouped instead of falsely assigning one relic");Conserve();
        double running=Stats.Total.Dps;for(int i=0;i<40;i++){Stats.SeparateAuxiliary=!Stats.SeparateAuxiliary;Conserve();Eq(Stats.Total.Dps,running,"repeated switching preserves rolling total");}
        for(int i=0;i<600;i++)Stats.Tick();Eq(Stats.Register(relic).Meter.Dps,0,"auxiliary rolling DPS expires normally");
        Stats.Reset();Eq(Stats.Total.Total,0,"reset clears total");Stats.SeparateAuxiliary=true;Eq(Stats.Register(relic).Meter.Total,0,"reset clears separate bank");Yes(!Stats.Register(relic).Visible,"zero auxiliary hidden");Stats.SeparateAuxiliary=false;Eq(Stats.Register(bow).Meter.Total,0,"reset clears merged bank");
        Hooks.Begin();Hit(child,7);Stats.SeparateAuxiliary=true;Yes(!Stats.Entries.Exists(e=>Object.ReferenceEquals(e.Key,relic)),"new run drops old provenance");
        Console.WriteLine("PASS auxiliary attribution: "+passed+" assertions");
    }
}
