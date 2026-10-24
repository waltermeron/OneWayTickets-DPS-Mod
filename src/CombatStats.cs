using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
namespace OneWayDps {
    public sealed class CombatStats {
        sealed class RefComparer : IEqualityComparer<object> {
            public new bool Equals(object a, object b) { return Object.ReferenceEquals(a,b); }
            public int GetHashCode(object a) { return RuntimeHelpers.GetHashCode(a); }
        }
        public sealed class Entry {
            public readonly int Id; public readonly object Key; public string Slot = ""; public bool IsWheel;
            public bool Visible {get{return !IsWheel || Meter.Total>0;}}
            public readonly DamageMeter Meter = new DamageMeter();
            public Entry(int id, object key) { Id=id; Key=key; }
        }
        public sealed class Sample {
            public double Time, Total;
            public readonly Dictionary<int,double> Weapons = new Dictionary<int,double>();
            public double Value(int id) { double v; return Weapons.TryGetValue(id,out v) ? v : 0; }
        }
        readonly Dictionary<object,Entry> byKey = new Dictionary<object,Entry>(new RefComparer());
        public readonly List<Entry> Entries = new List<Entry>();
        public readonly List<Sample> History = new List<Sample>();
        public readonly DamageMeter Total = new DamageMeter();
        public double ReportedDamage {get;private set;}
        public double DifferenceDamage {get{return Math.Max(0,ReportedDamage-Total.Total);}}
        static readonly object other = new object();
        long ticks; int serial;
        public double Time {get{return ticks/120.0;}}
        public double GlobalDps {get{return Total.Average;}}
        public double AverageDps {get{return Total.Total/Math.Max(1.0,Time);}}
        public double GlobalDpsFor(Entry e) {return e.Meter.Total/Math.Max(1.0,Total.Elapsed);}
        public double SortValue(Entry e,int mode) {return mode==1?e.Meter.Dps:mode==2?GlobalDpsFor(e):e.Meter.Total;}
        public List<Entry> GraphEntries(int selectedId) {
            var entries=new List<Entry>();
            foreach(var e in Entries)if(e.Meter.Total>0 && (selectedId<0 || e.Id==selectedId))entries.Add(e);
            return entries;
        }
        public Entry Register(object key) {
            key=key ?? other; Entry entry;
            if(!byKey.TryGetValue(key,out entry)) { entry=new Entry(++serial,key); byKey.Add(key,entry); Entries.Add(entry); }
            return entry;
        }
        public void PruneUnseen(IList<object> equipped) {
            for(int i=Entries.Count-1;i>=0;i--) {
                Entry e=Entries[i];if(e.Meter.Total>0)continue;
                bool found=false;foreach(object key in equipped)if(Object.ReferenceEquals(key,e.Key)){found=true;break;}
                if(!found){byKey.Remove(e.Key);Entries.RemoveAt(i);}
            }
        }
        public void Add(object key, double damage) {
            if(damage<=0 || Double.IsNaN(damage) || Double.IsInfinity(damage)) return;
            Register(key).Meter.Add(damage); Total.Add(damage);
        }
        public void RecordHit(object key,double effective,double reported) {
            if(reported<=0 || Double.IsNaN(reported) || Double.IsInfinity(reported))return;
            ReportedDamage+=reported;
            Add(key,Math.Min(Math.Max(0,effective),reported));
        }
        public double Share(Entry e) {return Total.Total>0 ? e.Meter.Total/Total.Total : 0;}
        public void Tick() {
            ticks++; Total.Tick(); foreach(var e in Entries) e.Meter.Tick();
            if(ticks%30==0) {
                var s=new Sample {Time=Time,Total=Total.Dps};
                foreach(var e in Entries) s.Weapons.Add(e.Id,e.Meter.Dps);
                History.Add(s); if(History.Count>481) History.RemoveAt(0);
            }
        }
        public void Reset() {
            Total.Reset(); foreach(var e in Entries) e.Meter.Reset(); ticks=0; History.Clear();ReportedDamage=0;
        }
        public void NewRun() {Reset(); Entries.Clear(); byKey.Clear(); serial=0;}
    }
}

