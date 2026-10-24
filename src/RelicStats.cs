using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using AutoCodes;
using Foundations;
using World.Relic;

namespace OneWayDps {
    public sealed class RelicBonus {
        public string Category="", Name="", ValueText="", Sources="", Note="";
        public double RawValue;
        public int SourceCount;
    }
    public sealed class OwnedRelic {
        public string Name="", Description="", Details="";
        public int Rank;
    }
    public sealed class RelicSnapshot {
        public readonly List<RelicBonus> Bonuses=new List<RelicBonus>();
        public readonly List<OwnedRelic> Relics=new List<OwnedRelic>();
        public string Status="等待局内奇物", Scope="仅汇总当前持有奇物的配置增益，不含武器、角色和科技；条件效果不等于持续生效的最终属性。";
    }
    // B站：-_小西瓜_- / uid:153345791。只读 RelicMgr；不调用 Get/Drop 或修改战斗属性。
    public static class RelicStats {
        sealed class Spec {
            public readonly string Category,Name,Unit;
            public Spec(string category,string name,string unit){Category=category;Name=name;Unit=unit;}
        }
        sealed class Group {
            public RelicBonus Row;
            public string Unit;
            public readonly HashSet<int> Indices=new HashSet<int>();
            public readonly List<string> Names=new List<string>();
        }
        static readonly Dictionary<string,Spec> specs=CreateSpecs();
        static readonly Regex tags=new Regex("<[^>]*>");
        static readonly Regex placeholders=new Regex(@"\{[^{}]*\}");
        static string Localize(string key) {
            if(String.IsNullOrEmpty(key))return "";
            try{return Clean(Commons.Localization_Utility.get_localization(key));}catch{return Clean(key);}
        }
        static string Clean(string value) {
            value=tags.Replace(value??""," ").Replace("\\n","\n");
            return placeholders.Replace(value,"…").Trim();
        }
        public static RelicSnapshot Read() {
            try {
                RelicMgr mgr;
                if(!Mission.instance.try_get_mgr<RelicMgr>("RelicMgr",out mgr) || mgr==null || mgr.relic_list==null)return new RelicSnapshot();
                var descriptions=new List<relic>();var scripts=new List<string>();
                foreach(var item in mgr.relic_list)if(item!=null && item.desc!=null){descriptions.Add(item.desc);scripts.Add(item.GetType().Name);}
                return Build(descriptions,scripts);
            }catch(Exception){return new RelicSnapshot{Status="奇物数据暂不可用，稍后重试"};}
        }
        public static RelicSnapshot FromDescriptions(IList<relic> descriptions) {return Build(descriptions,null);}
        static RelicSnapshot Build(IList<relic> descriptions,IList<string> types) {
            var snapshot=new RelicSnapshot();var groups=new Dictionary<string,Group>();
            if(descriptions==null)return snapshot;
            for(int index=0;index<descriptions.Count;index++) {
                relic d=descriptions[index];if(d==null)continue;
                string name=Localize(d.name);if(name=="")name="未命名奇物";
                string description=Localize(d.description),settings=Localize(d.desc_settings);
                if(settings!="" && settings!=description)description+=(description==""?"":"\n")+settings;
                var owned=new OwnedRelic{Name=name,Description=description,Rank=d.rank};
                snapshot.Relics.Add(owned);
                string script=types==null?d.behaviour_script:types[index];
                if(String.IsNullOrEmpty(script) || script=="Relic")script=d.behaviour_script??"";
                int dot=script.LastIndexOf('.');if(dot>=0)script=script.Substring(dot+1);
                var details=new List<string>();
                string family=script=="Relic_Modify_Variable"?"general":script=="Relic_Blast_Modify_Variable"?"blast":script=="Relic_Dart_Modify_Variable"?"dart":script=="Relic_Thunder_Modify_Variable"?"thunder":"";
                if(family!="" && d.parm_string!=null && d.parm_int!=null) {
                    int count=Math.Min(d.parm_string.Count,d.parm_int.Count);
                    for(int i=0;i<count;i++) {
                        string key=family+":"+d.parm_string[i];Spec spec;
                        if(specs.TryGetValue(key,out spec))Add(groups,index,name,key,spec,d.parm_int[i],details,true,
                            family=="general" && key.EndsWith("_knockback",StringComparison.Ordinal)?"按千分制比例显示；此处为奇物增量。":FamilyNote(family,spec.Unit));
                        else if(family=="thunder" && d.parm_string[i].StartsWith("atk_",StringComparison.Ordinal))
                            Add(groups,index,name,key,new Spec("闪电","闪电附加伤害（"+DamageType(d.parm_string[i].Substring(4))+"）","flat"),d.parm_int[i]/1000,details,true,"按每件配置除以 1000 并取整后加算，保持游戏实现。");
                        // IDs, boolean switches and unknown parameters stay in the localized effect description.
                    }
                }
                switch(script) {
                    case "Relic_Melee_Bigger": Add(groups,index,name,"melee_bigger",new Spec("近战","武器尺寸","percent"),Parameter(d,0,200),details,true,"");break;
                    case "ZoomMeleeRelic": Add(groups,index,name,"melee_scale",new Spec("近战","尺寸系数","percent"),200,details,true,"");break;
                    case "LuckyDogRelic": Add(groups,index,name,"drop_loot_delta",new Spec("掉落与经济","掉落系数","percent"),800,details,true,"");break;
                    case "TitanEnemyRelic":
                        AddFixed(groups,index,name,"enemy_scale", "敌人变化","敌人体型",200,details);
                        AddFixed(groups,index,name,"enemy_mass", "敌人变化","敌人质量",200,details);
                        AddFixed(groups,index,name,"enemy_hp", "敌人变化","敌人生命",200,details);
                        AddFixed(groups,index,name,"enemy_speed", "敌人变化","敌人速度",-200,details);break;
                    case "DifficultModeRelic":
                        AddFixed(groups,index,name,"enemy_scale", "敌人变化","敌人体型",200,details);
                        AddFixed(groups,index,name,"enemy_attack", "敌人变化","敌人攻击",200,details);
                        AddFixed(groups,index,name,"enemy_defense", "敌人变化","敌人防御",-200,details);
                        AddFixed(groups,index,name,"enemy_speed", "敌人变化","敌人速度",200,details);
                        AddFixed(groups,index,name,"drop_loot_delta", "掉落与经济","掉落系数",200,details);break;
                    case "AccelerateRelic": Add(groups,index,name,"initial_speed",new Spec("获取时效果","获取时横向速度","flat"),Parameter(d,0,0),details,false,"获取时一次性施加，不是当前车速。");break;
                    case "Relic_Kill_Recovery":
                        int kills=Parameter(d,0,0);
                        Add(groups,index,name,"kill_recovery:"+kills,new Spec("条件恢复","每 "+kills+" 击杀回复生命","flat"),Parameter(d,1,0),details,false,"达到击杀条件时触发；不表示每秒恢复。");break;
                    case "Relic_Landing_Trigger_Shoot": Add(groups,index,name,"landing_shoot",new Spec("条件触发","落地射击冷却","seconds"),Parameter(d,0,0),details,false,"落地触发，冷却时间不作加法合并。");break;
                    case "Relic_Melee_Assassinate": Add(groups,index,name,"assassinate",new Spec("条件触发","近战暴击处决概率","percent"),Parameter(d,0,0),details,false,"只在近战暴击后判定，精英不适用；独立判定不直接相加。");break;
                    case "Relic_Melee_Coin": Add(groups,index,name,"crit_coin",new Spec("条件触发","近战暴击掉币概率","percent"),Parameter(d,0,0),details,false,"只在近战暴击后判定，独立概率不直接相加。");break;
                }
                owned.Details=details.Count>0?String.Join("；",details.ToArray()):"特殊 / 条件效果见奇物原文，未强行换算为常驻数值。";
            }
            foreach(var group in groups.Values) {
                group.Row.ValueText=Format(group.Row.RawValue,group.Unit);
                group.Row.SourceCount=group.Indices.Count;group.Row.Sources=String.Join("、",group.Names.ToArray());
                snapshot.Bonuses.Add(group.Row);
            }
            snapshot.Bonuses.Sort(delegate(RelicBonus a,RelicBonus b){int c=String.CompareOrdinal(a.Category,b.Category);return c!=0?c:String.CompareOrdinal(a.Name,b.Name);});
            snapshot.Status="持有 "+snapshot.Relics.Count+" 件奇物 · "+snapshot.Bonuses.Count+" 项数值增益";
            return snapshot;
        }
        static string FamilyNote(string family,string unit) {
            if(family=="general")return unit=="raw"?"保留游戏配置单位，具体效果见来源奇物说明。":"当前持有奇物的加算贡献，不含其他来源。";
            return (family=="blast"?"爆炸":family=="dart"?"飞镖":"闪电")+"系统的配置参数；触发条件见来源说明"+(unit=="raw"?"，保留游戏原始单位。":"。");
        }
        static int Parameter(relic d,int index,int fallback){return d.parm_int!=null && d.parm_int.Count>index?d.parm_int[index]:fallback;}
        static string DamageType(string key) {
            switch(key){case "sharp":return "锐器";case "blunt":return "钝击";case "heat":return "热能";case "electric":return "电击";case "acid":return "酸蚀";case "ice":return "寒冷";case "explosion":return "爆炸";case "hp_remove":return "生命移除";default:return "其他";}
        }
        static void AddFixed(Dictionary<string,Group> groups,int index,string source,string key,string category,string name,double value,List<string> details) {
            Add(groups,index,source,key,new Spec(category,name,"percent"),value,details,true,"");
        }
        static void Add(Dictionary<string,Group> groups,int index,string source,string key,Spec spec,double value,List<string> details,bool merge,string note) {
            if(value==0 || Double.IsNaN(value) || Double.IsInfinity(value))return;
            string groupKey=key+(merge?"":"@"+index);Group group;
            if(!groups.TryGetValue(groupKey,out group)){group=new Group{Row=new RelicBonus{Category=spec.Category,Name=spec.Name,Note=note},Unit=spec.Unit};groups.Add(groupKey,group);}
            group.Row.RawValue+=value;
            if(group.Indices.Add(index) && !group.Names.Contains(source))group.Names.Add(source);
            details.Add(spec.Name+" "+Format(value,spec.Unit));
        }
        public static string Format(double value,string unit) {
            double display=unit=="percent"?value/10:unit=="seconds" || unit=="thousandth"?value/1000:value;
            string number=display.ToString("+0.###;-0.###;0",CultureInfo.InvariantCulture);
            return number+(unit=="percent"?"%":unit=="seconds"?" 秒":unit=="raw"?"（配置值）":"");
        }
        static Dictionary<string,Spec> CreateSpecs() {
            var d=new Dictionary<string,Spec>();
            // Verified parallel parm_string/parm_int dispatch in Relic_*_Modify_Variable.Get.
            AddSpecs(d,"general","通用输出","percent","global_atk_pts=伤害加成|global_critical_chance=暴击率|global_critical_dmg_rate=暴击伤害");
            AddSpecs(d,"general","通用输出","flat","global_atk_add=固定攻击");
            AddSpecs(d,"general","通用输出","thousandth","global_armor_piercing=穿甲");
            // Display knockback modifiers with the same per-thousand convention as other relic ratios.
            AddSpecs(d,"general","通用输出","percent","global_knockback=击退");
            AddSpecs(d,"general","防御与耐久","percent","global_hp_pts=装置耐久加成");
            AddSpecs(d,"general","防御与耐久","raw","global_armor_add=护甲加成");
            AddSpecs(d,"general","近战","percent","melee_atk_pts=近战伤害加成|melee_critical_chance=近战暴击率|melee_critical_dmg_rate=近战暴击伤害|melee_sharpness_durability=锋利度耐久|melee_sharpness_add=锋利度上限|melee_sharpness_recover=锋利度恢复");
            AddSpecs(d,"general","近战","flat","melee_atk_add=近战固定攻击");
            AddSpecs(d,"general","近战","thousandth","melee_armor_piercing=近战穿甲");
            AddSpecs(d,"general","近战","percent","melee_knockback=近战击退");
            AddSpecs(d,"general","远程","percent","ranged_atk_pts=远程伤害加成|ranged_critical_chance=远程暴击率|ranged_critical_dmg_rate=远程暴击伤害|ranged_reloading_speedup=装填速度|ranged_capacity=弹匣容量|ranged_ammo_bigger=弹丸尺寸增量|ranged_player_ammo_gravity=弹丸重力系数");
            AddSpecs(d,"general","远程","flat","ranged_atk_add=远程固定攻击|ranged_salvo=齐射数量");
            AddSpecs(d,"general","远程","thousandth","ranged_armor_piercing=远程穿甲");
            AddSpecs(d,"general","远程","percent","ranged_knockback=远程击退");
            AddSpecs(d,"general","远程","raw","ranged_critical_reload=暴击装填");
            AddSpecs(d,"general","车轮与冲撞","flat","wheel_charge_capacity=冲刺储能次数|ram_maneuverability=冲撞操控系数增量");
            AddSpecs(d,"general","车轮与冲撞","percent","wheel_charge_recover=冲刺储能恢复|wheel_tractive_force=动力|ram_stability=冲撞稳定性");
            AddSpecs(d,"general","车轮与冲撞","raw","wheel_charge_effect=冲刺效果|ram_dmg_speed_bonus=冲撞速度伤害加成");
            AddSpecs(d,"general","能量","flat","energy_capacity_add=能量上限");
            AddSpecs(d,"general","能量","raw","energy_recover_add=能量恢复");
            AddSpecs(d,"blast","爆炸","percent","dmg_mod=爆炸伤害加成|radius_mod=爆炸半径|ft_mod=爆炸推力|echo_mod=爆炸每次回响伤害系数|trigger_odds_general=攻击触发爆炸概率|trigger_odds_be_hit=受击触发爆炸概率");
            AddSpecs(d,"blast","爆炸","flat","ts_mod=爆炸命中参数|trigger_on_device_destroyed=装置摧毁触发参数|trigger_click_jump=点击触发跳跃");
            AddSpecs(d,"blast","爆炸","raw","trigger_click_cd=点击触发冷却|trigger_click_speed_up=点击触发加速|speed_up=爆炸加速");
            AddSpecs(d,"dart","飞镖","percent","cc=飞镖暴击率|cr=飞镖暴击伤害|dmg_coef=飞镖伤害系数|trigger_odds_melee=近战触发飞镖概率");
            AddSpecs(d,"dart","飞镖","flat","dmg=飞镖固定伤害|count=飞镖数量|cluster_times=飞镖集束次数|cc_coin=飞镖暴击掉币|kill_coin=飞镖击杀掉币|coin_cost=飞镖金币消耗|trigger_target_die=目标死亡触发飞镖");
            AddSpecs(d,"dart","飞镖","thousandth","speed=飞镖速度增量");
            AddSpecs(d,"dart","飞镖","raw","trigger_process_max=飞镖周期触发阈值|fuse_delay=飞镖引信延迟|ballon_buoyancy=飞镖气球浮力|coin_coef=飞镖金币系数");
            AddSpecs(d,"thunder","闪电","percent","jump_atk_mod=闪电跳跃伤害系数|CC=闪电暴击率|trigger_odds_mod=闪电触发概率加成|trigger_odds_melee=近战触发闪电概率|trigger_odds_projectile=远程触发闪电概率|trigger_odds_be_hit=受击触发闪电概率|trigger_odds_crush=碾压触发闪电概率|trigger_odds_dart=飞镖触发闪电概率|max_jump_dmg_mod=末次跳跃伤害系数");
            AddSpecs(d,"thunder","闪电","flat","jump_count=闪电跳跃次数");
            AddSpecs(d,"thunder","闪电","raw","jump_dis=闪电跳跃距离|jump_cd=闪电跳跃间隔|hp_remove=闪电生命移除|car_speed_up=闪电车速增益|car_windbox_cd=闪电风箱冷却");
            return d;
        }
        static void AddSpecs(Dictionary<string,Spec> d,string family,string category,string unit,string entries) {
            foreach(string item in entries.Split('|')){int split=item.IndexOf('=');d.Add(family+":"+item.Substring(0,split),new Spec(category,item.Substring(split+1),unit));}
        }
    }
}
