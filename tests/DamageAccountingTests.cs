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
namespace World.Projectiles { public class Projectile : FakeTarget { public ITarget emitter; } }
namespace World.Adherents { public class Adherent : FakeTarget { public ITarget _source; public Adherent(){faction=WorldEnum.Faction.neutral;} } }
namespace OneWayDps { public static class DpsOverlay { public static bool Active;public static readonly CombatStats Stats=new CombatStats();public static void Begin(){Active=true;Stats.NewRun();} } }
class DamageAccountingTests {
    static int passed;
    static void Eq(double got,double want,string name){if(double.IsNaN(got) || Math.Abs(got-want)>.000001)throw new Exception(name+": "+got+" != "+want);passed++;}
    static FakeTarget Enemy(int hp,int damage){return new FakeTarget{faction=WorldEnum.Faction.opposite,health=hp,next=new Dmg_Data{dmg=damage}};}
    static void Hit(ITarget source,FakeTarget target){Dmg_Data result;Hooks.Hurt(target,source,new Attack_Data(),out result);Eq(result.dmg,target.next.dmg,"original result retained");Eq(target.calls,1,"original hurt called once");}
    static void Main() {
        var weapon=new World.Devices.Device{faction=WorldEnum.Faction.player};
        Hooks.Begin();var enemy=Enemy(1000,300);Hit(weapon,enemy);
        Eq(DpsOverlay.Stats.Total.Total,300,"ordinary hit counts full popup amount");Eq(DpsOverlay.Stats.ReportedDamage,300,"ordinary popup total");Eq(DpsOverlay.Stats.DifferenceDamage,0,"ordinary hit has no difference");
        Hooks.Begin();enemy=Enemy(40,300);Hit(weapon,enemy);
        Eq(DpsOverlay.Stats.Total.Total,40,"300 popup against40 hp records40 effective");Eq(DpsOverlay.Stats.ReportedDamage,300,"overkill retains popup total");Eq(DpsOverlay.Stats.DifferenceDamage,260,"overkill difference is260");Eq(enemy.hp,-260,"original overkill damage remains unchanged");
        Hooks.Begin();Hit(weapon,Enemy(0,300));Eq(DpsOverlay.Stats.Total.Total,0,"already dead target excluded");Eq(DpsOverlay.Stats.ReportedDamage,0,"dead target does not create raw diagnostics");
        Hooks.Begin();var dot=new World.Adherents.Adherent{_source=weapon};Hit(dot,Enemy(500,120));
        Eq(DpsOverlay.Stats.Total.Total,120,"player owned neutral adherent counts");Eq(DpsOverlay.Stats.Entries.Count,1,"DOT uses one weapon row");Eq(Object.ReferenceEquals(DpsOverlay.Stats.Entries[0].Key,weapon)?1:0,1,"DOT retains source weapon attribution");
        var playerBullet=new World.Projectiles.Projectile{faction=WorldEnum.Faction.player,emitter=weapon};
        Hooks.Begin();var nestedDot=new World.Adherents.Adherent{_source=new World.Adherents.Adherent{_source=playerBullet}};Hit(nestedDot,Enemy(35,100));
        Eq(DpsOverlay.Stats.Total.Total,35,"nested player DOT uses effective damage");Eq(DpsOverlay.Stats.ReportedDamage,100,"nested DOT popup total");Eq(Object.ReferenceEquals(DpsOverlay.Stats.Entries[0].Key,weapon)?1:0,1,"nested DOT resolves projectile weapon");
        Hooks.Begin();var enemySource=new FakeTarget{faction=WorldEnum.Faction.opposite};Hit(new World.Adherents.Adherent{_source=enemySource},Enemy(500,120));
        Eq(DpsOverlay.Stats.Total.Total,0,"enemy owned neutral DOT excluded");Eq(DpsOverlay.Stats.ReportedDamage,0,"enemy DOT diagnostics excluded");
        Hooks.Begin();Hit(new World.Adherents.Adherent(),Enemy(500,120));Hit(null,Enemy(500,120));Eq(DpsOverlay.Stats.ReportedDamage,0,"unowned DOT and environment excluded");
        Hooks.Begin();var reflected=new World.Projectiles.Projectile{faction=WorldEnum.Faction.opposite,emitter=weapon};Hit(reflected,Enemy(500,120));Hit(new World.Adherents.Adherent{_source=reflected},Enemy(500,120));
        Eq(DpsOverlay.Stats.Total.Total,0,"reflected enemy projectile and its DOT excluded despite old player emitter");
        var a=new World.Adherents.Adherent();var b=new World.Adherents.Adherent();a._source=b;b._source=a;
        Eq(Hooks.IsPlayerSource(a)?1:0,0,"cyclic neutral DOT source terminates");
        Hooks.Begin();foreach(int mode in new[]{0,1,2}){enemy=Enemy(500,120);enemy.next=new Dmg_Data{dmg=120,is_miss=mode==0,is_immune=mode==1,is_heal=mode==2};Hit(weapon,enemy);}Eq(DpsOverlay.Stats.ReportedDamage,0,"miss immunity and heal excluded from raw diagnostics");
        Hooks.Begin();Hit(weapon,Enemy(200,300));DpsOverlay.Stats.Reset();Eq(DpsOverlay.Stats.ReportedDamage,0,"reset clears raw total");Eq(DpsOverlay.Stats.DifferenceDamage,0,"reset clears damage difference");
        DpsOverlay.Stats.RecordHit(weapon,40,300);Eq(DpsOverlay.Stats.Total.Total,40,"RecordHit effective sum");Eq(DpsOverlay.Stats.ReportedDamage,300,"RecordHit raw sum");Eq(DpsOverlay.Stats.DifferenceDamage,260,"RecordHit difference sum");
        DpsOverlay.Stats.RecordHit(weapon,10,double.NaN);DpsOverlay.Stats.RecordHit(weapon,10,double.PositiveInfinity);DpsOverlay.Stats.RecordHit(weapon,10,-1);Eq(DpsOverlay.Stats.ReportedDamage,300,"invalid raw values ignored");
        DpsOverlay.Stats.NewRun();Eq(DpsOverlay.Stats.ReportedDamage,0,"new run clears raw diagnostics");
        Console.WriteLine("PASS damage accounting: "+passed+" assertions");
    }
}
