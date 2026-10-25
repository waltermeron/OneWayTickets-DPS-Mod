using System;
using OneWayDps;
using World;
namespace UnityEngine { public static class Debug { public static void LogWarning(object x) { Console.WriteLine(x); } } }
namespace World {
 public static class WorldEnum { public enum Faction { player, neutral, opposite } }
 public class WorldContext { public bool in_safe_area, is_in_settlement, is_freeze_time; }
 public struct Attack_Data { public int atk; }
 public struct Dmg_Data { public int dmg; public bool is_heal, is_miss, is_immune; }
 public interface ITarget { WorldEnum.Faction Faction {get;} int hp {get;} void hurt(ITarget source, Attack_Data attack, out Dmg_Data result); }
 public class FakeTarget : ITarget {
  public WorldEnum.Faction faction; public int health = 100; public int calls; public Dmg_Data next;
  public WorldEnum.Faction Faction {get{return faction;}} public int hp {get{return health;}}
  public void hurt(ITarget source, Attack_Data attack, out Dmg_Data result) {calls++; result=next; if(!next.is_miss && !next.is_immune) health-=next.dmg;}
 }
}
namespace OneWayDps {
 public static class DpsOverlay { public static bool Active; public static readonly CombatStats Stats=new CombatStats(); public static DamageMeter Meter {get{return Stats.Total;}} public static void Begin(){ Active=true; Stats.NewRun(); } }
}
namespace World.Devices { public class Device : World.FakeTarget {} }
namespace World.Projectiles { public class Projectile : World.FakeTarget { public World.ITarget emitter; public AutoCodes.projectile desc; } }
namespace World.Adherents { public class Adherent : World.FakeTarget { public World.ITarget _source; } }
class Tests {
 static int passed;
 static void Eq(double got,double want,string name) {if(double.IsNaN(got) || Math.Abs(got-want)>0.00001){Console.WriteLine("FAIL: "+name+": "+got+" != "+want);throw new Exception(name+": "+got+" != "+want);}passed++;}
 static void Main() {
  var m=new DamageMeter(); m.Add(100); Eq(m.Dps,20,"fixed 5s window"); Eq(m.Average,100,"startup average");
  for(int i=0;i<599;i++)m.Tick(); Eq(m.Dps,20,"inside window"); m.Tick(); Eq(m.Dps,0,"expires at 5s"); Eq(m.Total,100,"total retained"); Eq(m.Average,20,"average time");
  m.Reset(); Eq(m.Total,0,"reset total"); Eq(m.Elapsed,0,"reset clock");
  m.Add(double.NaN); m.Add(double.PositiveInfinity); m.Add(-1); Eq(m.Total,0,"invalid hits");
  for(int i=0;i<10000;i++)m.Add(1); Eq(m.Total,10000,"same tick burst"); for(int i=0;i<600;i++)m.Tick(); Eq(m.Dps,0,"burst expiration");
  Hooks.Begin(); var ctx=new WorldContext(); DpsOverlay.Meter.Add(1);
  ctx.is_freeze_time=true; Hooks.Tick(ctx); Eq(DpsOverlay.Meter.Elapsed,0,"pause clock");
  ctx.is_freeze_time=false; ctx.in_safe_area=true; Hooks.Tick(ctx); Eq(DpsOverlay.Meter.Elapsed,0,"safe area clock");
  ctx.in_safe_area=false; ctx.is_in_settlement=true; Hooks.Tick(ctx); Eq(DpsOverlay.Meter.Elapsed,0,"settlement clock");
  ctx.is_in_settlement=false; Hooks.Tick(ctx); Eq(DpsOverlay.Meter.Elapsed,1/120.0,"simulation clock");
  var player=new FakeTarget{faction=WorldEnum.Faction.player};
  var enemy=new FakeTarget{faction=WorldEnum.Faction.opposite,next=new Dmg_Data{dmg=250}};
  Dmg_Data result; Hooks.Begin(); Hooks.Hurt(enemy,player,new Attack_Data(),out result);
  Eq(DpsOverlay.Meter.Total,100,"overkill clipped"); Eq(enemy.calls,1,"original called once"); Eq(result.dmg,250,"result unchanged"); Eq(enemy.hp,-150,"original damage unchanged");
  Hooks.Hurt(enemy,player,new Attack_Data(),out result); Eq(DpsOverlay.Meter.Total,100,"dead target excluded");
  foreach(int mode in new int[]{0,1,2}) {enemy=new FakeTarget{faction=WorldEnum.Faction.opposite,next=new Dmg_Data{dmg=10,is_miss=mode==0,is_immune=mode==1,is_heal=mode==2}}; Hooks.Hurt(enemy,player,new Attack_Data(),out result);}
  Eq(DpsOverlay.Meter.Total,100,"miss immune heal excluded");
  player.next=new Dmg_Data{dmg=20}; Hooks.Hurt(player,enemy,new Attack_Data(),out result); Eq(DpsOverlay.Meter.Total,100,"incoming excluded");
  enemy=new FakeTarget{faction=WorldEnum.Faction.opposite,next=new Dmg_Data{dmg=10}}; Hooks.Hurt(enemy,null,new Attack_Data(),out result); Eq(DpsOverlay.Meter.Total,100,"environment excluded");
  Hooks.End(); Hooks.Hurt(enemy,player,new Attack_Data(),out result); Eq(DpsOverlay.Meter.Total,100,"outside run excluded");
  Hooks.Begin(); Eq(DpsOverlay.Meter.Total,0,"new run resets");
  var weapon=new World.Devices.Device{faction=WorldEnum.Faction.player};
  var bullet=new World.Projectiles.Projectile{faction=WorldEnum.Faction.player,emitter=weapon};
  var subBullet=new World.Projectiles.Projectile{faction=WorldEnum.Faction.player,emitter=bullet};
  Eq(Object.ReferenceEquals(Hooks.ResolveWeapon(subBullet),weapon)?1:0,1,"recursive projectile attribution");
  var dot=new World.Adherents.Adherent{faction=WorldEnum.Faction.player,_source=bullet};
  Eq(Object.ReferenceEquals(Hooks.ResolveWeapon(dot),weapon)?1:0,1,"adherent attribution");
  bullet.emitter=bullet; Eq(Hooks.ResolveWeapon(bullet)==null?1:0,1,"cyclic source terminates");
  var stats=new CombatStats(); var w1=new object();var w2=new object();
  var e1=stats.Register(w1);var e2=stats.Register(w2);
  stats.Add(w1,120);stats.Add(w2,80);stats.Add(null,50);
  Eq(stats.Entries.Count,3,"distinct instances and other");Eq(stats.Register(w1).Id,e1.Id,"stable weapon identity");
  Eq(e1.Meter.Dps,24,"weapon rolling dps");Eq(e2.Meter.Total,80,"weapon total");
  double totalShares=0, totalDamage=0;foreach(var entry in stats.Entries){totalShares+=stats.Share(entry);totalDamage+=entry.Meter.Total;}
  Eq(totalShares,1,"shares sum to 100 percent");Eq(totalDamage,stats.Total.Total,"row totals conserve total");
  for(int i=0;i<30;i++)stats.Tick();Eq(stats.History.Count,1,"4Hz sample cadence");
  Eq(stats.History[0].Time,.25,"sample timestamp");Eq(stats.History[0].Value(e1.Id),24,"per weapon sample");Eq(stats.History[0].Total,50,"total sample");
  Eq(stats.History[0].Value(999),0,"unregistered series has zero history");
  for(int i=0;i<600;i++)stats.Tick();Eq(e1.Meter.Dps,0,"per weapon window expiration");
  for(int i=0;i<15000;i++)stats.Tick();Eq(stats.History.Count,481,"history bounded at 120 seconds");
  stats.Reset();Eq(stats.Total.Total,0,"reset combat total");Eq(e1.Meter.Total,0,"reset weapon total");Eq(stats.History.Count,0,"reset clears chart");Eq(stats.Entries.Count,3,"reset retains equipped rows");
  var unused=new object();stats.Register(unused);stats.Add(w1,1);stats.PruneUnseen(new System.Collections.Generic.List<object>{w2});Eq(stats.Entries.Count,2,"prune stale zero damage instances but retain history");
  stats.NewRun();Eq(stats.Entries.Count,0,"new run clears stale weapon instances");
  var global=new CombatStats();Eq(global.GlobalDps,0,"global dps initially zero");
  for(int i=0;i<240;i++)global.Tick();var globalWeapon=global.Register(new object());global.Add(globalWeapon.Key,120);
  Eq(global.GlobalDps,120,"global begins at first hit");Eq(global.GlobalDpsFor(globalWeapon),120,"per weapon common first-hit denominator");
  Eq(global.AverageDps,60,"average includes time before first hit");
  Eq(global.Total.Average,global.GlobalDps,"global uses first-hit meter");
  for(int i=0;i<240;i++)global.Tick();Eq(global.GlobalDps,60,"global denominator progresses after first hit");Eq(global.AverageDps,30,"average uses full combat clock");
  Eq(global.SortValue(globalWeapon,0),120,"sort by total damage");Eq(global.SortValue(globalWeapon,1),24,"sort by realtime dps");Eq(global.SortValue(globalWeapon,2),60,"sort by global dps");
  var wheel=global.Register(new object());wheel.IsWheel=true;Eq(wheel.Visible?1:0,0,"zero damage wheel hidden");
  var normal=global.Register(new object());Eq(normal.Visible?1:0,1,"zero damage nonwheel remains visible");
  global.Add(wheel.Key,20);Eq(wheel.Visible?1:0,1,"wheel becomes visible after damage");
  for(int i=0;i<8;i++)global.Add(new object(),i+1);global.Add(null,10);
  Eq(global.GraphEntries(-1).Count,11,"graph includes all damaging sources beyond five including other");
  Eq(global.GraphEntries(wheel.Id).Count,1,"single source graph filter");Eq(global.GraphEntries(normal.Id).Count,0,"graph omits zero damage sources");
  double combined=0;foreach(var e in global.Entries)combined+=global.GlobalDpsFor(e);Eq(combined,global.GlobalDps,"per source global dps conserves total");
  global.Reset();Eq(global.GlobalDps,0,"reset clears global dps");Eq(wheel.Visible?1:0,0,"reset hides zero damage wheel again");
  var preferences=new HudPreferences();preferences.opacity=-1;preferences.size=5;preferences.x=float.NaN;preferences.y=float.PositiveInfinity;preferences.sortMode=8;preferences.Sanitize();
  Eq(preferences.opacity,.2f,"opacity minimum remains visible");Eq(preferences.size,1.5f,"window scale upper bound");Eq(preferences.x,18,"invalid window x restored");Eq(preferences.y,105,"invalid window y restored");Eq(preferences.sortMode,2,"sort mode constrained");
  preferences.opacity=.6f;preferences.size=.8f;preferences.width=1200;preferences.height=900;preferences.ResetAppearance();Eq(preferences.opacity,1,"reset opacity");Eq(preferences.size,1,"reset legacy scale");Eq(preferences.width,760,"reset width");Eq(preferences.height,668,"reset height");
  preferences.opacity=float.NaN;preferences.size=float.NaN;preferences.Sanitize();Eq(preferences.opacity,1,"invalid opacity defaults");Eq(preferences.size,1,"invalid scale defaults");
  var layout=new HudPreferences{x=30,y=60,width=760,height=668,layoutVersion=2};
  float scale=HudLayout.ScreenScale(1920,1080);Eq(scale,1,"1080p uses fixed display scale");
  HudLayout.Resize(layout,760,668,200,0,1920,1080);Eq(layout.width,960,"horizontal resize increases width");Eq(layout.height,668,"horizontal resize leaves height alone");Eq(layout.x,30,"resize preserves left anchor");Eq(layout.y,60,"resize preserves top anchor");
  HudLayout.Resize(layout,960,668,0,150,1920,1080);Eq(layout.width,960,"vertical resize leaves width alone");Eq(layout.height,818,"vertical resize increases height");
  HudLayout.Resize(layout,960,818,-5000,-5000,1920,1080);Eq(layout.width,428,"resize reduced minimum width");Eq(layout.height,400,"resize reduced minimum height");
  HudLayout.Resize(layout,428,400,10000,10000,1920,1080);Eq(layout.x+layout.width*scale,1912,"resize stops at right margin");Eq(layout.y+layout.height*scale,1072,"resize stops at bottom margin");
  layout.width=760;layout.height=668;HudLayout.Move(layout,30,60,0,10000,1920,1080);float bottom=layout.y;
  Eq(bottom,404,"moving down clamps at bottom");Eq(layout.width,760,"bottom clamp does not change width");Eq(layout.height,668,"bottom clamp does not change height");
  HudLayout.Move(layout,layout.x,bottom,0,-100,1920,1080);Eq(layout.y,304,"window leaves bottom immediately when dragged upward");Eq(layout.height,668,"upward movement does not require resizing");Eq(HudLayout.ScreenScale(1920,1080),scale,"position never changes display scale");
  HudLayout.Move(layout,30,304,-10000,-10000,1920,1080);Eq(layout.x,8,"move clamps at left");Eq(layout.y,8,"move clamps at top");
  HudLayout.Fit(layout,1920,1080);Eq(layout.x,8,"fit moves to visible left margin");Eq(layout.y,8,"fit moves to visible top margin");Eq(layout.width,1400,"fit caps overly wide display");Eq(layout.height,1000,"fit caps overly tall display");
  HudLayout.Fit(layout,800,600);scale=HudLayout.ScreenScale(800,600);Eq(layout.width*scale<=784.001?1:0,1,"fit respects narrow screen width");Eq(layout.height*scale<=584.001?1:0,1,"fit respects short screen height");
  layout.x=10000;layout.y=10000;layout.width=5000;layout.height=5000;HudLayout.Constrain(layout,1280,720);scale=HudLayout.ScreenScale(1280,720);
  Eq(layout.x+layout.width*scale<=1272.001?1:0,1,"offscreen anchor and width recover");Eq(layout.y+layout.height*scale<=712.001?1:0,1,"offscreen anchor and height recover");
  var legacy=new HudPreferences{x=18,y=105,size=.8f,layoutVersion=1};HudLayout.Migrate(legacy,1920,1080);Eq(legacy.layoutVersion,2,"legacy layout version migrates");Eq(legacy.x,18,"pixel migration preserves x");Eq(legacy.y,105,"pixel migration preserves y");Eq(legacy.width,512,"legacy narrow size follows saved scale");Eq(Math.Abs(legacy.height-534.4)<.001?1:0,1,"legacy height follows saved scale");
  legacy=new HudPreferences{x=100,y=100,size=.8f,layoutVersion=0};HudLayout.Migrate(legacy,1920,1080);Eq(legacy.x,80,"scaled coordinate migration converts x to pixels");Eq(legacy.y,80,"scaled coordinate migration converts y to pixels");
  legacy.width=900;legacy.height=750;HudLayout.Migrate(legacy,1920,1080);Eq(legacy.width,900,"new width is stable across repeated migration");Eq(legacy.height,750,"new height is stable across repeated migration");
  preferences.width=float.NaN;preferences.height=float.PositiveInfinity;preferences.Sanitize();Eq(preferences.width,760,"invalid width defaults");Eq(preferences.height,668,"invalid height defaults");
  preferences.width=1;preferences.height=1;preferences.Sanitize();Eq(preferences.width,428,"sanitize uses reduced minimum width");Eq(preferences.height,400,"sanitize uses reduced minimum height");
  var compact=new HudPreferences{x=20,y=20,width=760,height=900,dpsOnly=true,layoutVersion=2};
  Eq(HudLayout.DisplayHeight(compact,true),140,"wide compact mode uses 140 logical height");Eq(HudLayout.DisplayHeight(compact,false),900,"normal display height remains stored full height");
  HudLayout.Move(compact,20,20,0,10000,1920,1080,true);Eq(compact.y,932,"compact bottom bound uses displayed height");Eq(compact.height,900,"compact movement preserves full height");
  HudLayout.Move(compact,compact.x,compact.y,0,-80,1920,1080,true);Eq(compact.y,852,"compact leaves bottom immediately when dragged upward");
  HudLayout.Resize(compact,760,900,200,-1000,1920,1080,true);Eq(compact.width,960,"compact width remains resizable");Eq(compact.height,900,"compact resize ignores vertical delta");
  HudLayout.Resize(compact,960,900,-10000,10000,1920,1080,true);Eq(compact.width,428,"compact width obeys reduced minimum");Eq(compact.height,900,"compact minimum resize preserves full height");
  Eq(HudLayout.DisplayHeight(compact,true),93.625,"narrow compact height follows proportional content scale");
  HudLayout.Move(compact,compact.x,compact.y,0,10000,1920,1080,true);Eq(compact.y,978.375,"narrow compact bottom bound follows visible frame");
  HudLayout.Resize(compact,428,900,2000,0,1920,1080,true);Eq(compact.width,428,"compact proportional growth respects bottom boundary");Eq(compact.y,978.375,"compact resize retains top anchor at bottom");
  HudLayout.Constrain(compact,1920,1080,false);Eq(compact.y,172,"returning to full view moves window inside full-height bounds");Eq(compact.height,900,"returning to full view restores saved height");
  compact.width=760;compact.height=1200;compact.y=500;HudLayout.Constrain(compact,1280,720,true);Eq(compact.height,1200,"compact screen clamp preserves full height beyond current screen");
  HudLayout.Fit(compact,1280,720);Eq(compact.height,1200,"compact fit preserves full height");Eq(compact.dpsOnly?1:0,1,"compact flag survives fit");
  compact.y=10000;HudLayout.Migrate(compact,1920,1080);Eq(compact.y,932,"saved compact mode migrates against compact bounds");Eq(compact.height,1200,"compact migration preserves full height");
  compact.ResetAppearance();Eq(compact.dpsOnly?1:0,1,"appearance reset preserves chosen display mode");Eq(compact.height,668,"appearance reset restores full height default");
  Eq(HudLayout.SurfaceAlpha(.96f,1),.96f,"opaque background alpha");Eq(HudLayout.SurfaceAlpha(.96f,.2f),.192f,"transparent background alpha applied directly");
  Eq(HudLayout.SurfaceAlpha(1,0),0,"zero opacity draws no background");Eq(HudLayout.SurfaceAlpha(2,2),1,"surface alpha bounds");
  Console.WriteLine("PASS: "+passed+" assertions");
 }
}


namespace AutoCodes { public class projectile { public uint projectile_id; } }