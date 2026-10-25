using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OneWayDps {
    public enum RunOutcome { Active, Suspended, Retreated, Defeated, Returned, Abandoned }
    [Serializable] public sealed class RunSource {
        public string Key="other",Name="其他伤害",Kind="other";
        public uint ContentId;public int Rank;
        public double MergedDamage,SeparateDamage;
        public bool Auxiliary {get{return Kind=="auxiliary" || Kind=="relic" || Kind=="effect";}}
        public double Damage(bool separate){return separate?SeparateDamage:MergedDamage;}
        public RunSource Copy(){return (RunSource)MemberwiseClone();}
    }
    [Serializable] public sealed class RunRecord {
        public string Id="",WorldKey="",WorldName="",StartedUtc="",UpdatedUtc="",EndedUtc="",BranchOf="";
        public RunOutcome Outcome;public bool Partial;
        public long PlayTicks,CombatTicks,SpeedTicks,FirstDamageTick=-1;
        public int NativeTicks,SceneIndex;
        public double Damage,ReportedDamage,SpeedDistance,PeakSpeed,NativeDistance;
        public List<RunSource> Sources=new List<RunSource>();
        public bool Closed {get{return Outcome!=RunOutcome.Active && Outcome!=RunOutcome.Suspended;}}
        public double PlaySeconds {get{return PlayTicks/120.0;}}
        public double CombatSeconds {get{return CombatTicks/120.0;}}
        public double DamageSeconds {get{return Damage>0?Math.Max(0,(CombatTicks-FirstDamageTick)/120.0):0;}}
        public double AverageSpeed {get{return SpeedTicks>0?SpeedDistance/(SpeedTicks/120.0):0;}}
        public double GlobalDps {get{return GlobalFor(Damage);}}
        public double AverageDps {get{return AverageFor(Damage);}}
        public double GlobalFor(double damage){return damage/Math.Max(1,DamageSeconds);}
        public double AverageFor(double damage){return damage/Math.Max(1,CombatSeconds);}
        public RunSource Source(RunSource info) {
            foreach(var source in Sources)if(source.Key==info.Key)return source;
            var result=info.Copy();result.MergedDamage=result.SeparateDamage=0;Sources.Add(result);return result;
        }
        public void Add(RunSource weapon,RunSource auxiliary,double effective,double reported) {
            if(!Finite(reported) || reported<=0)return;
            ReportedDamage+=reported;
            if(!Finite(effective) || effective<=0)return;
            double damage=Math.Min(effective,reported);
            if(Damage==0)FirstDamageTick=CombatTicks;
            Damage+=damage;Source(weapon).MergedDamage+=damage;Source(auxiliary??weapon).SeparateDamage+=damage;
        }
        public void Tick(bool playing,bool fighting,double speed) {
            if(Closed || Outcome!=RunOutcome.Active)return;
            if(playing)PlayTicks++;
            if(!playing || !fighting)return;
            CombatTicks++;
            if(Finite(speed) && speed>=0){SpeedTicks++;SpeedDistance+=speed/120;PeakSpeed=Math.Max(PeakSpeed,speed);}
        }
        public RunRecord Copy() {
            var copy=(RunRecord)MemberwiseClone();copy.Sources=new List<RunSource>();foreach(var s in Sources)copy.Sources.Add(s.Copy());return copy;
        }
        public static bool Finite(double value){return !Double.IsNaN(value) && !Double.IsInfinity(value);}
    }
    [Serializable] public sealed class RunCheckpoint {public string Fingerprint="";public RunRecord Snapshot;}
    [Serializable] public sealed class HistoryDatabase {
        public int Schema=1;public string ActiveRunId="";
        public List<RunRecord> Runs=new List<RunRecord>();
        public List<RunCheckpoint> Checkpoints=new List<RunCheckpoint>();
    }
    public sealed class HistorySession {
        public readonly HistoryDatabase Data;
        RunRecord cached;
        public HistorySession(HistoryDatabase data){Data=data??new HistoryDatabase();}
        public RunRecord Current {get{if(String.IsNullOrEmpty(Data.ActiveRunId))return null;if(cached!=null && cached.Id==Data.ActiveRunId && !cached.Closed)return cached;foreach(var r in Data.Runs)if(r.Id==Data.ActiveRunId && !r.Closed){cached=r;return r;}return null;}}
        public RunRecord Start(string key,string world,int nativeTicks,double nativeDistance,string utc,bool forceNew) {
            var current=Current;
            if(!forceNew && current!=null && current.WorldKey==key){current.Outcome=RunOutcome.Active;current.UpdatedUtc=utc;return current;}
            if(current!=null){current.Outcome=RunOutcome.Suspended;current.UpdatedUtc=utc;}
            current=new RunRecord{Id=Guid.NewGuid().ToString("N"),WorldKey=key,WorldName=world,StartedUtc=utc,UpdatedUtc=utc,Partial=nativeTicks>0,NativeTicks=nativeTicks,NativeDistance=nativeDistance};
            Data.Runs.Add(current);Data.ActiveRunId=current.Id;return current;
        }
        public void Suspend(string utc){var r=Current;if(r!=null){r.Outcome=RunOutcome.Suspended;r.UpdatedUtc=utc;}}
        public void Finish(RunOutcome outcome,string utc){var r=Current;if(r==null)return;r.Outcome=outcome;r.EndedUtc=r.UpdatedUtc=utc;Data.ActiveRunId="";}
        public void Detach(){Data.ActiveRunId="";}
        public void Checkpoint(string fingerprint) {
            var r=Current;if(r==null || String.IsNullOrEmpty(fingerprint))return;
            Data.Checkpoints.RemoveAll(c=>c.Fingerprint==fingerprint);
            Data.Checkpoints.Add(new RunCheckpoint{Fingerprint=fingerprint,Snapshot=r.Copy()});
            // Keep two snapshots per run: the game's primary and automatic backup save.
            int count=0;for(int i=Data.Checkpoints.Count-1;i>=0;i--)if(Data.Checkpoints[i].Snapshot.Id==r.Id && ++count>2)Data.Checkpoints.RemoveAt(i);
        }
        public bool Resume(string fingerprint,string utc) {
            RunCheckpoint match=null;foreach(var c in Data.Checkpoints)if(c.Fingerprint==fingerprint)match=c;
            if(match==null)return false;
            Suspend(utc);
            var snapshot=match.Snapshot.Copy();var old=Data.Runs.Find(r=>r.Id==snapshot.Id);
            if(old!=null && old.Closed){snapshot.BranchOf=snapshot.Id;snapshot.Id=Guid.NewGuid().ToString("N");snapshot.EndedUtc="";Data.Runs.Add(snapshot);}
            else if(old!=null)Data.Runs[Data.Runs.IndexOf(old)]=snapshot;
            else Data.Runs.Add(snapshot);
            snapshot.Outcome=RunOutcome.Suspended;snapshot.UpdatedUtc=utc;Data.ActiveRunId=snapshot.Id;cached=snapshot;return true;
        }
    }
    public sealed class HistoryStore {
        readonly string path;readonly Func<HistoryDatabase,string> encode;readonly Func<string,HistoryDatabase> decode;
        bool badPrimary;public bool ReadOnly {get;private set;}public string Notice="";
        public HistoryStore(string file,Func<HistoryDatabase,string> writer,Func<string,HistoryDatabase> reader){path=file;encode=writer;decode=reader;}
        static void Validate(HistoryDatabase d) {
            if(d==null || d.Runs==null || d.Checkpoints==null)throw new InvalidDataException("Invalid history document");
            if(d.Schema!=1)throw new NotSupportedException("Unsupported history schema");
            var ids=new HashSet<string>();
            foreach(var r in d.Runs){if(r==null || String.IsNullOrEmpty(r.Id) || !ids.Add(r.Id) || r.Sources==null || r.CombatTicks<0 || r.PlayTicks<0 || r.SpeedTicks<0 || !RunRecord.Finite(r.Damage) || !RunRecord.Finite(r.SpeedDistance))throw new InvalidDataException("Invalid history record");foreach(var s in r.Sources)if(s==null || s.Key==null || !RunRecord.Finite(s.MergedDamage) || !RunRecord.Finite(s.SeparateDamage))throw new InvalidDataException("Invalid damage source");}
            foreach(var c in d.Checkpoints)if(c==null || c.Snapshot==null || c.Snapshot.Sources==null)throw new InvalidDataException("Invalid history checkpoint");
        }
        public HistoryDatabase Load() {
            if(!File.Exists(path) && !File.Exists(path+".bak"))return new HistoryDatabase();
            try{var d=decode(File.ReadAllText(path));Validate(d);return d;}
            catch(NotSupportedException){ReadOnly=true;Notice="历史数据来自其他版本，已保护原文件。";return new HistoryDatabase();}
            catch(Exception){badPrimary=true;}
            try{var d=decode(File.ReadAllText(path+".bak"));Validate(d);Notice="历史主文件异常，已从备份恢复。";return d;}
            catch(NotSupportedException){ReadOnly=true;Notice="历史备份来自其他版本，已保护原文件。";return new HistoryDatabase();}
            catch(Exception){Notice="历史文件无法读取；原文件会保留为异常副本。";return new HistoryDatabase();}
        }
        public bool Save(HistoryDatabase data) {
            if(ReadOnly)return false;
            Validate(data);Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp=path+".tmp-"+Guid.NewGuid().ToString("N");
            try {
                byte[] bytes=new UTF8Encoding(false).GetBytes(encode(data));
                using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                if(badPrimary && File.Exists(path)){File.Move(path,path+".corrupt-"+Guid.NewGuid().ToString("N"));badPrimary=false;}
                if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);
                return true;
            }finally{if(File.Exists(temp))File.Delete(temp);}
        }
    }
}
