using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Commons;
using Foundations;
using UnityEngine;
using World;
using World.Devices;

namespace OneWayDps {
    public static class RunRecorder {
        static HistoryStore store;static HistorySession session;
        static bool worldReady,dirty,forceNew;static float nextSave;
        static string pendingCheckpoint;
        static readonly Dictionary<object,RunSource> names=new Dictionary<object,RunSource>();
        static readonly RunSource other=new RunSource();
        public static string Notice="";
        public static HistoryDatabase Data {get{Ensure();return session.Data;}}
        static string Now {get{return DateTime.UtcNow.ToString("o");}}
        static void Ensure() {
            if(session!=null)return;
            string path=Path.GetFullPath(Path.Combine(Application.dataPath,"..","DPSMod","history","history.json"));
            store=new HistoryStore(path,HistoryJson.Encode,HistoryJson.Decode);
            session=new HistorySession(store.Load());Notice=store.Notice;
            if(session.Current!=null)session.Suspend(Now);
        }
        public static void Menu(){try{Ensure();DpsOverlay.Boot();}catch(Exception e){Warn(e);}}
        public static void SceneStarting(){worldReady=false;names.Clear();}
        public static void SceneReady() {
            try {
                Ensure();var ctx=WorldContext.instance;
                int ticks=0,index=0;float distance=0;
                Share_DS.instance.try_get_value<int>("game_time",out ticks);Share_DS.instance.try_get_value<int>("scene_index",out index);Share_DS.instance.try_get_value<float>("total_distance",out distance);
                string world=ctx.world_id??"unknown";
                string title=ctx.r_game_world==null?"世界 "+world:Clean(Localization_Utility.get_localization(ctx.r_game_world.world_name)??ctx.r_game_world.world_name);
                var r=session.Start(CommonContext.instance.statistics_play_count+":"+world,title,ticks,distance*2,Now,forceNew);
                r.SceneIndex=index;forceNew=false;worldReady=true;dirty=true;Save();
                if(!String.IsNullOrEmpty(pendingCheckpoint)){session.Checkpoint(pendingCheckpoint);pendingCheckpoint=null;dirty=true;Save();}
            }catch(Exception e){Warn(e);}
        }
        public static void Tick(WorldContext ctx) {
            if(!worldReady || session==null || session.Current==null)return;
            try {bool playing=!ctx.is_freeze_time && !ctx.is_in_settlement;float speed;
                double value=VehicleSpeed.TryToMetersPerSecond(ctx.caravan_velocity.x,out speed)?speed:Double.NaN;
                session.Current.Tick(playing,playing && !ctx.in_safe_area,value);if(playing)dirty=true;
            }catch(Exception e){Warn(e);}
        }
        static RunSource Describe(object key) {
            if(key==null)return other;
            var d=key as Device;var relic=key as World.Relic.Relic;var effect=key as SharedDamageSource;
            RunSource known;if(names.TryGetValue(key,out known) && (d==null || d.desc==null || known.ContentId==d.desc.id))return known;
            known=new RunSource();
            if(d!=null && d.desc!=null){known.ContentId=d.desc.id;known.Key="device:"+d.desc.id;known.Name=Clean(Localization_Utility.get_localization(d.desc.name)??d.desc.name);known.Rank=d.desc.rank;known.Kind=DamageAttribution.IsSupport(d)?"auxiliary":"weapon";}
            else if(relic!=null && relic.desc!=null){known.ContentId=relic.desc.id;known.Key="relic:"+relic.desc.id;known.Name=Clean(Localization_Utility.get_localization(relic.desc.name)??relic.desc.name);known.Rank=relic.desc.rank;known.Kind="relic";}
            else if(effect!=null){known.Key="effect:"+effect.Script;known.Name=effect.Name;known.Kind="effect";}
            names[key]=known;return known;
        }
        static string Clean(string value){return Regex.Replace(value??"其他伤害","<[^>]*>","");}
        public static void RecordHit(object weapon,object auxiliary,double effective,double reported) {
            if(!worldReady || session==null || session.Current==null)return;
            try{session.Current.Add(Describe(weapon),auxiliary==null?null:Describe(auxiliary),effective,reported);dirty=true;}catch(Exception e){Warn(e);}
        }
        static string Fingerprint() {
            var config=Config.current;
            string path=Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath,"..")),config.save_dir_path,config.save_file_path);
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");
        }
        public static void Saved() {try{Ensure();if(session.Current!=null){UpdateNative();session.Checkpoint(Fingerprint());dirty=true;Save();}}catch(Exception e){Warn(e);}}
        public static void Loaded(Dictionary<string,object> data) {
            try {
                Ensure();worldReady=false;names.Clear();pendingCheckpoint=null;object site;
                if(data==null || !data.TryGetValue("site",out site) || (site as string)!="safearea") {session.Suspend(Now);session.Detach();forceNew=true;}
                else {pendingCheckpoint=Fingerprint();bool resumed=session.Resume(pendingCheckpoint,Now);forceNew=!resumed;if(!resumed){session.Suspend(Now);session.Detach();}}
                dirty=true;Save();
            }catch(Exception e){Warn(e);forceNew=true;}
        }
        public static void NewSave(){try{Ensure();session.Finish(RunOutcome.Abandoned,Now);forceNew=true;dirty=true;Save();}catch(Exception e){Warn(e);}}
        static void UpdateNative() {
            var r=session.Current;if(r==null)return;
            int ticks,index;float distance;
            if(Share_DS.instance.try_get_value<int>("game_time",out ticks))r.NativeTicks=ticks;
            if(Share_DS.instance.try_get_value<int>("scene_index",out index))r.SceneIndex=index;
            if(Share_DS.instance.try_get_value<float>("total_distance",out distance))r.NativeDistance=distance*2;
            r.UpdatedUtc=Now;
        }
        public static void Settlement(bool retreat){Finish(retreat?RunOutcome.Retreated:RunOutcome.Defeated);}
        public static void ReturnCamp(){Finish(RunOutcome.Returned);}
        static void Finish(RunOutcome outcome){try{Ensure();UpdateNative();session.Finish(outcome,Now);worldReady=false;dirty=true;Save();}catch(Exception e){Warn(e);}}
        public static void SceneEnding(){try{if(session!=null){UpdateNative();session.Suspend(Now);worldReady=false;dirty=true;Save();}}catch(Exception e){Warn(e);}}
        public static void Poll(){if(dirty && Time.unscaledTime>=nextSave){Save();nextSave=Time.unscaledTime+5;}}
        public static void Save(){try{if(session==null || !dirty)return;UpdateNative();if(store.Save(session.Data))dirty=false;Notice=store.Notice;}catch(Exception e){Warn(e);}}
        static void Warn(Exception e){Notice="历史记录暂时无法保存："+e.Message;Debug.LogWarning("[OneWayDps] History: "+e.Message);}
    }
}
