using System;
using System.IO;
using System.Web.Script.Serialization;
using OneWayDps;
class RunHistoryTests {
    static int passed;
    static void Eq(double got,double want,string name){if(Double.IsNaN(got)||Math.Abs(got-want)>.00001)throw new Exception(name+": "+got+" != "+want);passed++;}
    static void Yes(bool okay,string name){if(!okay)throw new Exception(name);passed++;}
    static RunSource Bow(){return new RunSource{Key="device:1",Name="短弓",ContentId=1,Kind="weapon"};}
    static void Tick(RunRecord r,int count,bool playing,bool combat,double speed){for(int i=0;i<count;i++)r.Tick(playing,combat,speed);}
    static double Sum(RunRecord r,bool separate){double n=0;foreach(var s in r.Sources)if(separate||!s.Auxiliary)n+=s.Damage(separate);return n;}
    static HistoryStore Store(string path){var serializer=new JavaScriptSerializer{MaxJsonLength=10000000};return new HistoryStore(path,d=>serializer.Serialize(d),s=>serializer.Deserialize<HistoryDatabase>(s));}
    static void Main(){
        var db=new HistoryDatabase();var session=new HistorySession(db);var r=session.Start("0:a","荒原",0,0,"2026-10-25T00:00:00Z",false);var bow=Bow();var aux=new RunSource{Key="device:2",Name="爆炸箭袋",Kind="auxiliary"};
        Eq(db.Runs.Count,1,"first run recorded");Yes(!r.Partial,"new expedition fully covered");
        Tick(r,120,true,false,999);Eq(r.PlaySeconds,1,"safe area counted in play time");Eq(r.CombatSeconds,0,"safe area excluded from combat");Eq(r.SpeedTicks,0,"safe area excluded from speed average");
        Tick(r,240,true,true,10);r.Add(bow,null,120,300);Eq(r.Damage,120,"effective damage retained");Eq(r.ReportedDamage,300,"reported total retained");Eq(r.GlobalDps,120,"first damage minimum one-second denominator");Eq(r.AverageDps,60,"average includes pre-damage combat");
        Tick(r,240,true,true,20);Eq(r.AverageSpeed,15,"speed average weighted by simulation time");Eq(r.PeakSpeed,20,"peak speed");Eq(r.GlobalDps,60,"global starts at first hit");Eq(r.AverageDps,30,"average starts at combat begin");
        r.Add(bow,aux,30,60);Eq(Sum(r,false),150,"merged totals conserve damage");Eq(Sum(r,true),150,"separate totals conserve damage");Eq(r.Source(bow).MergedDamage,150,"bow plus auxiliary merged");Eq(r.Source(bow).SeparateDamage,120,"bow separate direct damage");Eq(r.Source(aux).SeparateDamage,30,"quiver separate damage");Eq(r.GlobalFor(30),15,"source global denominator shared");Eq(r.AverageFor(30),7.5,"source average denominator shared");
        Tick(r,120,false,false,1000);Eq(r.PlaySeconds,5,"pause excluded from play time");Eq(r.CombatSeconds,4,"pause excluded from combat time");
        Tick(r,120,true,true,0);Eq(r.AverageSpeed,12,"stationary time included");Tick(r,120,true,true,Double.NaN);Eq(r.AverageSpeed,12,"missing speed excluded without fake zero");Eq(r.CombatSeconds,6,"missing speed does not stop combat clock");
        r.Add(bow,null,Double.NaN,Double.NaN);r.Add(bow,null,-10,-2);Eq(r.Damage,150,"invalid damage ignored");
        var copy=r.Copy();copy.Sources[0].MergedDamage+=100;Eq(r.Sources[0].MergedDamage,150,"snapshot sources deeply copied");
        string id=r.Id;session.Checkpoint("save-A");r.Add(bow,null,50,50);Tick(r,120,true,true,30);Yes(session.Resume("save-A","later"),"known save resumes");r=session.Current;Yes(r.Id==id,"same save retains run id");Eq(r.Damage,150,"load rolls back uncommitted damage");Eq(r.CombatSeconds,6,"load rolls back uncommitted time");Eq(db.Runs.Count,1,"reload never duplicates run");
        Yes(Object.ReferenceEquals(session.Start("0:a","荒原",720,100,"again",false),r),"world resume reuses current run");
        r.Add(bow,null,10,10);Eq(r.Damage,160,"replay records once after rollback");
        session.Checkpoint("save-B");session.Checkpoint("save-C");Eq(db.Checkpoints.Count,2,"latest primary and backup checkpoints retained");Yes(!session.Resume("save-A","old"),"unknown older save not guessed");
        session.Finish(RunOutcome.Defeated,"ended");Yes(r.Closed && session.Current==null,"settlement closes run");double completedDamage=r.Damage;Tick(r,120,true,true,999);Eq(r.Damage,completedDamage,"completed record stable");
        Yes(session.Resume("save-C","branch"),"completed historical save can load");var branch=session.Current;Yes(branch.Id!=id && branch.BranchOf==id,"completed save creates explicit branch");Eq(db.Runs.Count,2,"completed run preserved beside branch");Eq(r.Damage,completedDamage,"branch never rewrites finished run");
        var live=new CombatStats();live.Add(new object(),10);live.Reset();Eq(branch.Damage,160,"live F4 reset independent from history");
        var same=branch.Id;session.Suspend("quit");Yes(!branch.Closed && branch.Outcome==RunOutcome.Suspended,"quit suspends without finalizing");Yes(session.Start("0:a","荒原",720,100,"continue",false).Id==same,"same expedition continues after suspend");
        var another=session.Start("1:b","沙泽",800,20,"new",true);Yes(another.Partial,"mid-run upgrade marked partial");Yes(another.Id!=same,"new run unique id");Eq(db.Runs.Count,3,"all runs retained");
        string folder=Path.Combine(Path.GetTempPath(),"OneWayDps-HistoryTests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);string path=Path.Combine(folder,"history.json");
        try {
            var store=Store(path);Eq(store.Load().Runs.Count,0,"fresh history starts empty");Yes(store.Save(db),"first atomic save");var loaded=Store(path).Load();Eq(loaded.Runs.Count,3,"all runs survive restart");Yes(loaded.Runs[0].Sources[0].Name=="短弓","Chinese source names survive storage");Eq(loaded.Runs[0].Damage,160,"damage survives restart");
            var restored=new HistorySession(loaded);Yes(restored.Current.Id==another.Id,"active run restored from disk");
            another.Add(bow,null,5,5);store.Save(db);Yes(File.Exists(path+".bak"),"automatic previous version backup exists");
            File.WriteAllText(path,"{broken");var recovery=Store(path);var recovered=recovery.Load();Eq(recovered.Runs.Count,3,"corrupt primary recovers backup");Yes(recovery.Notice.Contains("备份"),"recovery reported");Yes(recovery.Save(recovered),"healthy history saved after recovery");Eq(Directory.GetFiles(folder,"*.corrupt-*").Length,1,"corrupt original preserved");
            File.Delete(path);var backupOnly=Store(path).Load();Eq(backupOnly.Runs.Count,3,"missing primary can recover backup");
            File.WriteAllText(path,"{\"Schema\":2,\"Runs\":[],\"Checkpoints\":[]}");var future=Store(path);future.Load();Yes(future.ReadOnly,"future schema protected");Yes(!future.Save(db),"future schema never overwritten");Yes(File.ReadAllText(path).Contains("\"Schema\":2"),"future content intact");
            File.Delete(path);File.Delete(path+".bak");store=Store(path);store.Save(db);string before=File.ReadAllText(path);var failing=new HistoryStore(path,d=>{throw new IOException("simulated serialization failure");},s=>null);bool failed=false;try{failing.Save(db);}catch(IOException){failed=true;}Yes(failed,"write error propagates");Yes(File.ReadAllText(path)==before,"failed write preserves existing file");Eq(Directory.GetFiles(folder,"*.tmp-*").Length,0,"temporary files cleaned");
        } finally {foreach(var file in Directory.GetFiles(folder))File.Delete(file);Directory.Delete(folder,false);}
        Console.WriteLine("PASS run history: "+passed+" assertions");
    }
}
