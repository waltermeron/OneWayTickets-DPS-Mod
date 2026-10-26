using System;
using System.Collections.Generic;
namespace OneWayDps {
    // Game simulation ticks at 120 Hz. Pause and safe areas do not advance this clock.
    public sealed class DamageMeter {
        sealed class Hit { public long Tick; public double Damage; }
        readonly Queue<Hit> hits = new Queue<Hit>();
        Hit last;
        long clock, first;
        bool started;
        double rolling;
        public double Total { get; private set; }
        public double Elapsed { get { return started ? (clock - first) / 120.0 : 0; } }
        public double Dps { get { return rolling / 5.0; } }
        public double Average { get { return started ? Total / Math.Max(1.0, Elapsed) : 0; } }
        public void Tick() {
            clock++;
            while(hits.Count > 0 && clock - hits.Peek().Tick >= 600) rolling -= hits.Dequeue().Damage;
            if(hits.Count == 0) rolling = 0;
        }
        public void Add(double damage) {
            if(damage <= 0 || double.IsNaN(damage) || double.IsInfinity(damage)) return;
            if(!started) { started = true; first = clock; }
            Total += damage; rolling += damage;
            // Aggregate hits in the same tick, keeping the queue bounded by 600 ticks.
            if(last != null && last.Tick == clock) last.Damage += damage;
            else { last = new Hit {Tick = clock, Damage = damage}; hits.Enqueue(last); }
        }
        public void Reset() { hits.Clear(); last = null; Total = rolling = 0; clock = first = 0; started = false; }
    }
}

