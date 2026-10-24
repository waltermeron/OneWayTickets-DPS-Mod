using System;
using System.Collections.Generic;
using OneWayDps;
namespace AutoCodes {public class relic {public uint id;public string name,description,desc_settings,behaviour_script;public int rank;public List<string> parm_string;public List<int> parm_int;}}
namespace Commons {public static class Localization_Utility {public static string get_localization(string key){return key;}}}
namespace World.Relic {public class Relic {public AutoCodes.relic desc;}public class RelicMgr {public List<Relic> relic_list=new List<Relic>();}}
namespace Foundations {public class Mission {public static Mission instance=new Mission();public World.Relic.RelicMgr Relics;public bool try_get_mgr<T>(string key,out T value){value=Relics==null?default(T):(T)(object)Relics;return Relics!=null;}}}
class RelicStatsTests {
 static int passed;
 static void Eq(bool truth,string name){if(!truth)throw new Exception(name);passed++;}
 static AutoCodes.relic R(string name,string script,string[] keys,int[] values){return new AutoCodes.relic{name=name,description="<color=green>效果</color>",behaviour_script=script,parm_string=new List<string>(keys),parm_int=new List<int>(values)};}
 static RelicBonus Find(RelicSnapshot s,string name){foreach(var b in s.Bonuses)if(b.Name==name)return b;throw new Exception("Missing bonus "+name);}
 static void Main() {
  var a=R("奇物甲","Relic_Modify_Variable",new[]{"global_atk_pts","global_critical_chance","global_atk_add"},new[]{100,50,12});
  var b=R("奇物乙","Relic_Modify_Variable",new[]{"global_atk_pts","global_critical_chance","ranged_capacity"},new[]{200,100,250});
  var s=RelicStats.FromDescriptions(new List<AutoCodes.relic>{a,b});
  Eq(s.Relics.Count==2,"owned list count");Eq(s.Bonuses.Count==4,"matching keys merged");
  var atk=Find(s,"伤害加成");Eq(atk.RawValue==300,"additive raw contributions");Eq(atk.ValueText=="+30%","per-thousand converted to percent");Eq(atk.SourceCount==2,"source count");Eq(atk.Sources.Contains("奇物甲") && atk.Sources.Contains("奇物乙"),"source names retained");
  Eq(Find(s,"暴击率").ValueText=="+15%","critical chance percent");Eq(Find(s,"固定攻击").ValueText=="+12","flat attack not scaled");Eq(Find(s,"弹匣容量").ValueText=="+25%","ammo capacity percent");
  Eq(s.Relics[0].Description=="效果","markup cleaned");Eq(s.Relics[0].Details.Contains("伤害加成 +10%"),"per relic detail retained");Eq(s.Scope.Contains("不含武器"),"scope excludes nonrelic sources");
  Eq(RelicStats.FromDescriptions(null).Relics.Count==0,"null collection");Eq(RelicStats.FromDescriptions(new List<AutoCodes.relic>{null}).Bonuses.Count==0,"null record ignored");
  var negative=R("代价","Relic_Modify_Variable",new[]{"global_atk_pts","global_atk_add"},new[]{-50,0});s=RelicStats.FromDescriptions(new[]{negative});Eq(s.Bonuses.Count==1,"zero contribution omitted");Eq(s.Bonuses[0].ValueText=="-5%","negative modifier preserved");
  var labels=R("比例与术语","Relic_Modify_Variable",new[]{"global_knockback","melee_knockback","ranged_knockback","global_hp_pts","global_armor_add","wheel_tractive_force"},new[]{150,-250,300,450,12,200});
  s=RelicStats.FromDescriptions(new[]{labels});
  Eq(Find(s,"击退").ValueText=="+15%","knockback150 displays percentage");Eq(Find(s,"近战击退").ValueText=="-25%","negative melee knockback percentage");Eq(Find(s,"远程击退").ValueText=="+30%","ranged knockback percentage");
  Eq(Find(s,"击退").Note.Contains("奇物增量"),"knockback percentage scope remains explicit");
  Eq(Find(s,"装置耐久加成").Category=="防御与耐久","device durability terminology");Eq(Find(s,"装置耐久加成").ValueText=="+45%","durability percentage remains unchanged");Eq(Find(s,"护甲加成").Category=="防御与耐久","armor grouped with durability");
  Eq(Find(s,"动力").ValueText=="+20%","tractive force label matches game power");Eq(Find(s,"护甲加成").ValueText=="+12（配置值）","uncertain armor unit is not forced to percent");
  Eq(s.Relics[0].Details.Contains("击退 +15%") && s.Relics[0].Details.Contains("动力 +20%"),"per relic details share percent and game terms");
  var unknown=R("特殊","Relic_Empty",new[]{"anything"},new[]{999});unknown.description="穿过墙壁 {0}";s=RelicStats.FromDescriptions(new[]{unknown});Eq(s.Bonuses.Count==0,"unknown parameter not invented");Eq(s.Relics[0].Description.Contains("穿过墙壁"),"special original effect retained");Eq(!s.Relics[0].Description.Contains("{0}"),"unresolved placeholder hidden");
  var special=R("大一点","Relic_Melee_Bigger",new string[0],new int[0]);s=RelicStats.FromDescriptions(new[]{special});Eq(Find(s,"武器尺寸").ValueText=="+20%","code default size value");
  special=R("落地","Relic_Landing_Trigger_Shoot",new string[0],new[]{1500});s=RelicStats.FromDescriptions(new[]{special});Eq(s.Bonuses[0].ValueText=="+1.5 秒","milliseconds cooldown");
  var proc=R("处决甲","Relic_Melee_Assassinate",new string[0],new[]{100});var proc2=R("处决乙","Relic_Melee_Assassinate",new string[0],new[]{200});s=RelicStats.FromDescriptions(new[]{proc,proc2});Eq(s.Bonuses.Count==2,"independent proc probabilities not summed");Eq(s.Bonuses[0].Note.Contains("精英不适用"),"proc conditions preserved");
  var thunder=R("闪电","Relic_Thunder_Modify_Variable",new[]{"jump_count","atk_sharp","if_repeatable","jump_dis"},new[]{2,1999,1,500});s=RelicStats.FromDescriptions(new[]{thunder});Eq(s.Bonuses.Count==3,"boolean effect not treated as numeric gain");Eq(Find(s,"闪电附加伤害（锐器）").RawValue==1,"thunder damage truncates per game");Eq(Find(s,"闪电跳跃距离").ValueText=="+500（配置值）","uncertain units kept explicit");
  var blast=R("爆炸","Relic_Blast_Modify_Variable",new[]{"dmg_mod","echo_mod","trigger_odds_general"},new[]{300,500,100});s=RelicStats.FromDescriptions(new[]{blast});Eq(Find(s,"爆炸伤害加成").ValueText=="+30%","blast damage modifier");Eq(Find(s,"爆炸每次回响伤害系数").ValueText=="+50%","blast echo per-thousand");
  var dart=R("飞镖","Relic_Dart_Modify_Variable",new[]{"dmg","speed","cc","new_projectile_id"},new[]{20,1500,100,12345});s=RelicStats.FromDescriptions(new[]{dart});Eq(s.Bonuses.Count==3,"projectile identifier is not a gain");Eq(Find(s,"飞镖速度增量").ValueText=="+1.5","dart speed thousandth conversion");
  var duplicate=R("重复","Relic_Modify_Variable",new[]{"global_atk_pts","global_atk_pts"},new[]{100,100});s=RelicStats.FromDescriptions(new[]{duplicate,duplicate});Eq(s.Bonuses[0].RawValue==400,"duplicate contributions included");Eq(s.Bonuses[0].SourceCount==2,"same instance contributes source once per group");
  Eq(RelicStats.Format(1000,"percent")=="+100%","percentage formula");Eq(RelicStats.Format(-250,"percent")=="-25%","negative percentage formula");
  Foundations.Mission.instance.Relics=null;Eq(RelicStats.Read().Relics.Count==0,"unavailable manager safe");
  Foundations.Mission.instance.Relics=new World.Relic.RelicMgr();Foundations.Mission.instance.Relics.relic_list.Add(new World.Relic.Relic{desc=a});s=RelicStats.Read();Eq(s.Relics.Count==1 && s.Bonuses.Count==3,"read current manager list");
  Console.WriteLine("PASS: "+passed+" relic assertions");
 }
}
