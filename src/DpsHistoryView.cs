using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace OneWayDps {
    public sealed partial class DpsOverlay {
        bool historyOpen;int beforeHistoryView;
        string historySelected="",historySearch="";
        int historyFilter;Vector2 historyScroll,historyDetailScroll;
        void ToggleHistory() {
            if(view==3 && historyOpen && visible){historyOpen=false;view=beforeHistoryView;}
            else {beforeHistoryView=view==3?0:view;historyOpen=true;view=3;appearanceOpen=false;visible=true;}
        }
        void DrawHistoryLauncher() {
            if(normal==null)InitStyle();
            int depth=GUI.depth;Matrix4x4 matrix=GUI.matrix;GUI.depth=-10000;float scale=HudLayout.ScreenScale(Screen.width,Screen.height);
            try {GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(scale,scale,1));if(Btn(new Rect(18,Screen.height/scale-43,148,31),"历史战绩",false))ToggleHistory();}finally{GUI.depth=depth;GUI.matrix=matrix;}
        }
        RunRecord HistorySelected() {
            var data=RunRecorder.Data;
            foreach(var r in data.Runs)if(r.Id==historySelected)return r;
            return data.Runs.Count>0?data.Runs[data.Runs.Count-1]:null;
        }
        static string Duration(double seconds) {
            long t=(long)Math.Max(0,seconds);return (t/3600).ToString("00")+":"+(t/60%60).ToString("00")+":"+(t%60).ToString("00");
        }
        static string DateLabel(string utc) {
            DateTime value;return DateTime.TryParse(utc,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out value)?value.ToLocalTime().ToString("yyyy-MM-dd HH:mm"):"日期未知";
        }
        static string Outcome(RunRecord r) {
            switch(r.Outcome){case RunOutcome.Active:return "进行中";case RunOutcome.Suspended:return "已暂停";case RunOutcome.Retreated:return "安全撤离";case RunOutcome.Defeated:return "耗尽耐久";case RunOutcome.Returned:return "返回营地";default:return "已放弃";}
        }
        void DrawHistoryMetrics() {
            var r=HistorySelected();float w=(panel.width-32)/5;
            Surface(new Rect(16,52,panel.width-32,73));
            for(int i=1;i<5;i++)Fill(new Rect(16+w*i,63,1,49),rim);
            string[] values={r==null?"0":r.Damage.ToString("N0"),r==null?"0.0":r.GlobalDps.ToString("N1"),r==null?"0.0":r.AverageDps.ToString("N1"),r==null || r.SpeedTicks==0?"--":r.AverageSpeed.ToString("N1"),r==null?"00:00:00":Duration(r.PlaySeconds)};
            string[] labels={"累计有效伤害","全局 DPS","平均 DPS","平均车速 (m/s)",r!=null && r.Partial?"已记录游玩时间":"单局游玩时间"};
            for(int i=0;i<5;i++){MetricValue(new Rect(28+w*i,62,w-16,26),values[i],bold);GUI.Label(new Rect(28+w*i,96,w-16,22),labels[i],small);}
        }
        void DrawHistory() {
            var data=RunRecorder.Data;
            if(historySelected!="") {var r=data.Runs.Find(x=>x.Id==historySelected);if(r!=null){DrawHistoryDetail(r);return;}historySelected="";}
            if(!Active)GUI.Label(new Rect(20,139,panel.width-210,28),"本机历史 · "+data.Runs.Count+" 局 · 最近一局指标见上方",normal);
            GUI.Label(new Rect(20,182,48,24),"查询",small);Surface(new Rect(70,181,panel.width-270,27));
            historySearch=GUI.TextField(new Rect(75,181,panel.width-280,27),historySearch,normal);
            string[] filters={"全部记录","已结束","进行 / 暂停"};
            if(Btn(new Rect(panel.width-178,138,162,32),filters[historyFilter],false)){historyFilter=(historyFilter+1)%3;historyScroll=Vector2.zero;}
            GUI.Label(new Rect(panel.width-188,183,172,23),"日期 / 武器名 / 世界",small);
            var runs=new List<RunRecord>();string query=historySearch.Trim();
            for(int i=data.Runs.Count-1;i>=0;i--){var r=data.Runs[i];if(historyFilter==1 && !r.Closed || historyFilter==2 && r.Closed)continue;
                bool match=query=="" || (DateLabel(r.StartedUtc)+" "+r.WorldName+" "+Outcome(r)).IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0;
                if(!match)foreach(var s in r.Sources)if((s.Name??"").IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0){match=true;break;}
                if(match)runs.Add(r);
            }
            Rect area=new Rect(16,215,panel.width-32,Mathf.Max(65,BodyHeight-343));float contentWidth=panel.width-52;
            historyScroll=GUI.BeginScrollView(area,historyScroll,new Rect(0,0,contentWidth,Mathf.Max(area.height,runs.Count*72)));
            for(int i=0;i<runs.Count;i++){var r=runs[i];float y=i*72;Fill(new Rect(0,y,contentWidth,66),card);
                GUI.Label(new Rect(12,y+5,contentWidth-190,25),DateLabel(r.StartedUtc)+"  "+Outcome(r)+(r.Partial?" · 部分统计":"")+(r.BranchOf!=""?" · 读档分支":""),nameStyle);
                GUI.Label(new Rect(12,y+32,contentWidth-28,25),r.WorldName+" · 时长 "+Duration(r.PlaySeconds)+" · 总伤 "+r.Damage.ToString("N0")+" · 全局 DPS "+r.GlobalDps.ToString("N1"),small);
                Label(new Rect(contentWidth-166,y+5,152,25),"查看武器明细 →",right,accent);
                if(GUI.Button(new Rect(0,y,contentWidth,66),"",GUIStyle.none)){historySelected=r.Id;historyDetailScroll=Vector2.zero;}
            }
            if(runs.Count==0)GUI.Label(new Rect(12,12,contentWidth-24,50),data.Runs.Count==0?"暂无历史记录。v1.1 开始记录，旧版未保存的历局无法补取。":"没有匹配的记录。",notes);
            GUI.EndScrollView();
            GUI.Label(new Rect(20,BodyHeight-116,panel.width-280,75),String.IsNullOrEmpty(RunRecorder.Notice)?"点击一局查看各武器数据；同类同品质按类型汇总。\n历史独立于本局清零；暂停、主菜单和离线时间不计入。\n升级中途接入的存档会标为部分统计。":RunRecorder.Notice,notes);
        }
        void DrawHistoryDetail(RunRecord r) {
            if(Btn(new Rect(16,181,96,28),"← 全部记录",false)){historySelected="";return;}
            GUI.Label(new Rect(122,182,panel.width-144,26),DateLabel(r.StartedUtc)+" · "+Outcome(r)+(r.Partial?" · 从升级后开始统计":""),small);
            float width=panel.width;
            GUI.Label(new Rect(24,218,width-386,25),"武器 / 伤害来源",small);
            Label(new Rect(width-361,218,72,25),"全局 DPS",right,muted);Label(new Rect(width-286,218,76,25),"平均 DPS",right,muted);Label(new Rect(width-206,218,80,25),"总伤害",right,muted);Label(new Rect(width-122,218,76,25),"伤害占比",right,muted);
            var sources=new List<RunSource>();foreach(var s in r.Sources)if(s.Damage(preferences.separateAuxiliary)>0 && (preferences.separateAuxiliary || !s.Auxiliary))sources.Add(s);
            sources.Sort((a,b)=>b.Damage(preferences.separateAuxiliary).CompareTo(a.Damage(preferences.separateAuxiliary)));
            Rect area=new Rect(16,249,width-32,Mathf.Max(65,BodyHeight-377));
            historyDetailScroll=GUI.BeginScrollView(area,historyDetailScroll,new Rect(0,0,width-52,Mathf.Max(area.height,sources.Count*59)));
            for(int i=0;i<sources.Count;i++){var s=sources[i];float y=i*59;double damage=s.Damage(preferences.separateAuxiliary);Fill(new Rect(0,y,width-52,54),card);
                var visual=WeaponAssets.ForHistory(s);WeaponAssets.DrawIcon(new Rect(8,y+9,34,34),visual.Icon);
                Label(new Rect(48,y+5,width-440,25),s.Name,nameStyle,visual.Color);GUI.Label(new Rect(48,y+29,width-440,20),s.Kind=="weapon"?"品质 "+s.Rank:s.Kind=="auxiliary"?"辅助装置":s.Kind=="other"?"其他":"奇物",small);
                Label(new Rect(width-377,y+12,72,25),r.GlobalFor(damage).ToString("N1"),right,ink);Label(new Rect(width-302,y+12,76,25),r.AverageFor(damage).ToString("N1"),right,ink);Label(new Rect(width-222,y+12,80,25),damage.ToString("N0"),right,ink);Label(new Rect(width-138,y+12,76,25),(r.Damage>0?damage/r.Damage:0).ToString("P1"),right,accent);
            }
            if(sources.Count==0)GUI.Label(new Rect(12,12,width-80,40),"该局尚无有效伤害记录。",notes);
            GUI.EndScrollView();
            string note="战斗时间 "+Duration(r.CombatSeconds)+" · 伤害计时 "+Duration(r.DamageSeconds)+"\n平均车速取战斗中水平速度的时间平均，包含停车；暂停与安全区排除。\n全局 DPS 从首次伤害起算，平均 DPS 从本次战斗统计开始起算。";
            GUI.Label(new Rect(20,BodyHeight-116,width-280,77),note,notes);
        }
    }
}
