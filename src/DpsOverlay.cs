using System;
using System.Collections.Generic;
using System.IO;
using Foundations;
using UnityEngine;
using World;
using World.Devices;
namespace OneWayDps {
    public sealed partial class DpsOverlay : MonoBehaviour {
        public static bool Active;
        public static readonly CombatStats Stats=new CombatStats();
        static DpsOverlay instance;
        bool visible=true,renderCompact; int view,rangeIndex; readonly int[] ranges={60,120,30};
        int selected=-1; bool appearanceOpen,preferencesDirty; float nextRefresh,saveAt;
        float vehicleSpeed; bool hasVehicleSpeed;
        int gesture; Vector2 lastPointer;
        HudPreferences preferences=new HudPreferences();
        const float AppearanceHeight=96;
        const string WeaponNotes="点击武器查看单独曲线；同名武器按独立实例统计。\n全局dps为造成伤害开始计时，平均dps为进入战斗开始计时\n实时dps为近5s平均伤害";
        float BodyHeight {get{return panel.height-(appearanceOpen?AppearanceHeight:0);}}
        static readonly string[] sortNames={"累计伤害","实时 DPS","全局 DPS"};
        string SettingsPath {get{return Path.GetFullPath(Path.Combine(Application.dataPath,"..","DPSMod","settings.json"));}}
        Vector2 scroll,legendScroll,relicScroll;
        RelicSnapshot relicSnapshot=new RelicSnapshot();
        Rect panel=new Rect(18,105,760,668);
        GUIStyle normal,small,bold,big,right,nameStyle,button,notes,signature,footerNotes,comparisonValue;
        readonly Dictionary<int,WeaponVisual> visuals=new Dictionary<int,WeaponVisual>();
        readonly List<CombatStats.Entry> rows=new List<CombatStats.Entry>();
        readonly List<object> equipped=new List<object>();
        static readonly Color bg=new Color(.045f,.062f,.09f,.96f), card=new Color(.09f,.12f,.16f,.97f), muted=new Color(.57f,.65f,.73f), ink=new Color(.91f,.95f,.98f), accent=new Color(.32f,.94f,.76f);
        public static void Begin() {
            Stats.NewRun(); Active=true;
            Boot();
            Stats.SeparateAuxiliary=instance.preferences.separateAuxiliary;
            instance.visuals.Clear();instance.selected=-1;instance.nextRefresh=0;
        }
        public static void Boot() {
            if(instance==null) {
                var obj=new GameObject("OneWayDpsOverlay");DontDestroyOnLoad(obj);instance=obj.AddComponent<DpsOverlay>();instance.LoadPreferences();
                Stats.SeparateAuxiliary=instance.preferences.separateAuxiliary;
                Debug.Log("[OneWayDps] Loaded v1.1.1 | B站：-_小西瓜_- | uid:153345791. F1 views; F2 range; F3 visibility; F4 reset; F5 compact; F6 auxiliary sources; F7 history.");
            }
        }
        void Update() {
            RunRecorder.Poll();
            if(preferencesDirty && Time.unscaledTime>=saveAt)SavePreferences();
            hasVehicleSpeed=Active && VehicleSpeed.TryRead(out vehicleSpeed);
            if(!Active || Time.unscaledTime<nextRefresh)return;
            nextRefresh=Time.unscaledTime+1f;
            try {
                DeviceMgr mgr;
                if(Mission.instance.try_get_mgr<DeviceMgr>("DeviceMgr",out mgr) && mgr!=null) {
                    equipped.Clear();
                    foreach(var slot in mgr.slots_device) {
                        var d=slot.slot_device;
                        if(d!=null && d.faction==WorldEnum.Faction.player){var entry=Stats.Register(d);entry.Slot=slot._name;entry.IsAuxiliary=DamageAttribution.IsSupport(d);entry.IsWheel=d.desc!=null && d.desc.device_type!=null && d.desc.device_type.value==Foundations.Excels.Device_Type.EN_Device_Type.Wheel;equipped.Add(d);}
                    }
                }
                if(mgr!=null)Stats.PruneUnseen(equipped);
                foreach(var e in Stats.Entries)visuals[e.Id]=WeaponAssets.Get(e.Key);
            }catch(Exception e){Debug.LogWarning("[OneWayDps] Metadata refresh: "+e.Message);}
            try {relicSnapshot=RelicStats.Read();}catch(Exception e){Debug.LogWarning("[OneWayDps] Relic refresh: "+e.Message);}
        }
        void InitStyle() {
            Font font=Font.CreateDynamicFontFromOSFont(new string[]{"Microsoft YaHei","SimHei","Arial Unicode MS","Arial"},16);
            normal=new GUIStyle(GUI.skin.label);normal.font=font;normal.fontSize=14;normal.normal.textColor=ink;
            small=new GUIStyle(normal);small.fontSize=12;small.normal.textColor=muted;
            bold=new GUIStyle(normal);bold.fontSize=18;bold.fontStyle=FontStyle.Bold;
            big=new GUIStyle(normal);big.fontSize=30;big.fontStyle=FontStyle.Bold;big.normal.textColor=accent;
            right=new GUIStyle(normal);right.alignment=TextAnchor.MiddleRight;
            nameStyle=new GUIStyle(normal);nameStyle.fontStyle=FontStyle.Bold;nameStyle.clipping=TextClipping.Clip;
            button=new GUIStyle(normal);button.alignment=TextAnchor.MiddleCenter;
            notes=new GUIStyle(small);notes.wordWrap=true;
            footerNotes=new GUIStyle(notes);footerNotes.fontSize=14;footerNotes.normal.textColor=ink;
            comparisonValue=new GUIStyle(normal);comparisonValue.fontSize=16;comparisonValue.fontStyle=FontStyle.Bold;
            signature=new GUIStyle(normal);signature.alignment=TextAnchor.UpperRight;signature.fontStyle=FontStyle.Bold;
        }
        void OnGUI() {
            Event input=Event.current;
            if(input.type==EventType.KeyDown) {
                if(input.keyCode==KeyCode.F7){ToggleHistory();ReleaseInput();input.Use();}
                else if(input.keyCode==KeyCode.F1 && Active){view=(view+1)%4;historyOpen=view==3;preferences.dpsOnly=false;ReleaseInput();DirtyPreferences();input.Use();}
                else if(input.keyCode==KeyCode.F3){visible=!visible;ReleaseInput();input.Use();}
                else if(input.keyCode==KeyCode.F4 && Active){Stats.Reset();input.Use();}
                else if(input.keyCode==KeyCode.F2){rangeIndex=(rangeIndex+1)%ranges.Length;input.Use();}
                else if(input.keyCode==KeyCode.F5 && Active && view!=3){preferences.dpsOnly=!preferences.dpsOnly;appearanceOpen=false;visible=true;ReleaseInput();DirtyPreferences();input.Use();}
                else if(input.keyCode==KeyCode.F6){preferences.separateAuxiliary=!preferences.separateAuxiliary;Stats.SeparateAuxiliary=preferences.separateAuxiliary;selected=-1;chartKey=null;ReleaseInput();DirtyPreferences();input.Use();}
            }
            if(view==3)appearanceOpen=false;
            if(!Active && (!historyOpen || !visible)){ReleaseGesture();DrawHistoryLauncher();return;}
            if(!visible)return;if(normal==null)InitStyle();
            rows.Clear();foreach(var entry in Stats.Entries)if(entry.Visible)rows.Add(entry);
            rows.Sort(delegate(CombatStats.Entry a,CombatStats.Entry b){int c=Stats.SortValue(b,preferences.sortMode).CompareTo(Stats.SortValue(a,preferences.sortMode));return c!=0?c:a.Id.CompareTo(b.Id);});
            Matrix4x4 matrix=GUI.matrix;int depth=GUI.depth;Color color=GUI.color;
            try {
                // Position and dimensions never affect the screen's fixed display scale.
                GUI.matrix=Matrix4x4.identity;
                renderCompact=preferences.dpsOnly && view!=3;
                HudLayout.Constrain(preferences,Screen.width,Screen.height,renderCompact);
                float scale=HudLayout.ScreenScale(Screen.width,Screen.height);
                HandleWindowGesture(scale);
                // Smaller windows fit the complete layout without overlapping controls.
                float contentScale=renderCompact?Mathf.Min(1,preferences.width/640f):Mathf.Min(1,Mathf.Min(preferences.width/640f,preferences.height/(appearanceOpen?624f:600f)));
                float renderScale=scale*contentScale;
                panel.width=preferences.width/contentScale;panel.height=HudLayout.DisplayHeight(preferences,renderCompact)/contentScale;
                GUI.color=Color.white;
                GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(renderScale,renderScale,1));GUI.depth=-10000;
                panel.x=preferences.x/renderScale;panel.y=preferences.y/renderScale;
                GUI.Window(198367201,panel,DrawPanel,"",GUIStyle.none);
            }finally{GUI.matrix=matrix;GUI.depth=depth;GUI.color=color;}
        }
        void ReleaseGesture(){if(gesture!=0){GUIUtility.hotControl=0;gesture=0;}}
        void ReleaseInput(){GUIUtility.hotControl=0;gesture=0;}
        void HandleWindowGesture(float scale) {
            int control=GUIUtility.GetControlID(198367202,FocusType.Passive);
            Event e=Event.current;Vector2 mouse=e.mousePosition;
            float contentScale=renderCompact?Mathf.Min(1,preferences.width/640f):Mathf.Min(1,Mathf.Min(preferences.width/640f,preferences.height/(appearanceOpen?624f:600f)));
            float uiScale=scale*contentScale,displayHeight=HudLayout.DisplayHeight(preferences,renderCompact);
            Rect grip=new Rect(preferences.x+preferences.width*scale-30*uiScale,preferences.y+displayHeight*scale-30*uiScale,30*uiScale,30*uiScale);
            Rect title=new Rect(preferences.x,preferences.y+3*uiScale,preferences.width*scale-158*uiScale,44*uiScale);
            if(e.type==EventType.MouseDown && e.button==0 && GUIUtility.hotControl==0) {
                if(grip.Contains(mouse))gesture=2;
                else if(title.Contains(mouse))gesture=1;
                if(gesture!=0){lastPointer=mouse;GUIUtility.hotControl=control;e.Use();}
            } else if(gesture!=0 && GUIUtility.hotControl==control) {
                if(e.type==EventType.MouseDrag) {
                    Vector2 delta=mouse-lastPointer;lastPointer=mouse;
                    if(gesture==1)HudLayout.Move(preferences,preferences.x,preferences.y,delta.x,delta.y,Screen.width,Screen.height,renderCompact);
                    else HudLayout.Resize(preferences,preferences.width,preferences.height,delta.x,delta.y,Screen.width,Screen.height,renderCompact);
                    DirtyPreferences();e.Use();
                } else if(e.type==EventType.MouseUp && e.button==0) {
                    Vector2 delta=mouse-lastPointer;
                    if(gesture==1)HudLayout.Move(preferences,preferences.x,preferences.y,delta.x,delta.y,Screen.width,Screen.height,renderCompact);
                    else HudLayout.Resize(preferences,preferences.width,preferences.height,delta.x,delta.y,Screen.width,Screen.height,renderCompact);
                    ReleaseGesture();DirtyPreferences();e.Use();
                }
            }
        }
        void Fill(Rect r,Color c){Color old=GUI.color;GUI.color=new Color(c.r,c.g,c.b,HudLayout.SurfaceAlpha(c.a,preferences.opacity));GUI.DrawTexture(r,Texture2D.whiteTexture,ScaleMode.StretchToFill,true);GUI.color=old;}
        void Label(Rect r,string s,GUIStyle style,Color c){Color old=style.normal.textColor;style.normal.textColor=c;GUI.Label(r,s,style);style.normal.textColor=old;}
        bool Btn(Rect r,string s,bool on) {
            Fill(r,on?new Color(.12f,.3f,.29f):card);
            if(on)Fill(new Rect(r.x,r.yMax-2,r.width,2),accent);
            return GUI.Button(r,s,button);
        }
        WeaponVisual Visual(CombatStats.Entry e) {WeaponVisual v;return visuals.TryGetValue(e.Id,out v)?v:new WeaponVisual{Name="加载武器信息…"};}
        void DrawPanel(int id) {
            Fill(new Rect(3,5,panel.width,panel.height),new Color(0,0,0,.3f));Fill(new Rect(0,0,panel.width,panel.height),bg);Fill(new Rect(0,0,panel.width,3),accent);
            GUI.Label(new Rect(18,13,120,28),view==3?"历史战绩":"战斗分析",bold);
            GUI.Label(new Rect(142,18,panel.width-298,24),(view==3 && historySelected==""?"最近一局 · ":"")+(Stats.SeparateAuxiliary?"辅助伤害：分别统计":"辅助伤害：合并武器"),small);
            if(Btn(new Rect(panel.width-146,12,130,30),view==3?"关闭历史":appearanceOpen?"收起窗口设置":"窗口设置",appearanceOpen && view!=3)) {
                if(view==3){ToggleHistory();return;}
                if(renderCompact){preferences.dpsOnly=false;appearanceOpen=true;DirtyPreferences();}
                else appearanceOpen=!appearanceOpen;
            }
            if(renderCompact){DrawMetrics();DrawResizeGrip();return;}
            if(appearanceOpen)DrawAppearance();
            float offset=appearanceOpen?AppearanceHeight:0;
            GUI.BeginGroup(new Rect(0,offset,panel.width,panel.height-offset));
            DrawBody();GUI.EndGroup();
            DrawResizeGrip();
        }
        void DrawResizeGrip() {
            float side=renderCompact?16:28;
            Rect r=new Rect(panel.width-side-1,panel.height-side-1,side,side);
            Fill(r,card);Color old=GUI.color;GUI.color=accent;
            for(int line=0;line<3;line++)for(int step=0;step<(renderCompact?7:12)-line*2;step++)
                GUI.DrawTexture(new Rect(r.x+4+line*(renderCompact?3:5)+step,r.yMax-3-step,2,2),Texture2D.whiteTexture);
            GUI.color=old;
        }
        void DrawMetrics() {
            float metricWidth=(panel.width-32)/5;
            Fill(new Rect(16,52,panel.width-32,73),card);
            MetricValue(new Rect(28,58,metricWidth-16,42),Stats.Total.Dps.ToString("N1"),big);
            GUI.Label(new Rect(28,99,metricWidth-16,20),"实时 DPS",small);
            MetricValue(new Rect(28+metricWidth,62,metricWidth-16,26),Stats.GlobalDps.ToString("N1"),bold);GUI.Label(new Rect(28+metricWidth,96,metricWidth-16,22),"全局 DPS",small);
            MetricValue(new Rect(28+metricWidth*2,62,metricWidth-16,26),Stats.Total.Total.ToString("N0"),bold);GUI.Label(new Rect(28+metricWidth*2,96,metricWidth-16,22),"累计伤害",small);
            MetricValue(new Rect(28+metricWidth*3,62,metricWidth-16,26),Stats.AverageDps.ToString("N1"),bold);GUI.Label(new Rect(28+metricWidth*3,96,metricWidth-16,22),"平均 DPS",small);
            MetricValue(new Rect(28+metricWidth*4,62,metricWidth-16,26),hasVehicleSpeed?vehicleSpeed.ToString("N1"):"--",bold);
            GUI.Label(new Rect(28+metricWidth*4,96,metricWidth-16,22),"车速 ("+VehicleSpeed.Unit+")",small);
        }
        void MetricValue(Rect rect,string value,GUIStyle style) {
            int originalSize=style.fontSize;
            try {
                float width=style.CalcSize(new GUIContent(value)).x;
                if(width>rect.width)style.fontSize=Mathf.Max(12,Mathf.FloorToInt(originalSize*(rect.width-4)/width));
                GUI.Label(rect,value,style);
            }finally{style.fontSize=originalSize;}
        }
        void DrawBody() {
            if(view==3)DrawHistoryMetrics();else DrawMetrics();
            if(Active) {
                if(Btn(new Rect(16,138,92,32),"武器明细",view==0)){view=0;historyOpen=false;}
                if(Btn(new Rect(114,138,92,32),"实时曲线",view==1)){view=1;historyOpen=false;}
                if(Btn(new Rect(212,138,104,32),"奇物增益统计",view==2)){view=2;historyOpen=false;}
                if(Btn(new Rect(322,138,104,32),"历史战绩",view==3)){view=3;historyOpen=true;appearanceOpen=false;}
            }
            if(view==1){if(Btn(new Rect(panel.width-178,138,162,32),"最近 "+ranges[rangeIndex]+" 秒",false))rangeIndex=(rangeIndex+1)%ranges.Length;DrawGraph();}
            else if(view==0){if(Btn(new Rect(panel.width-178,138,162,32),"排序："+sortNames[preferences.sortMode]+" ↓",false)){preferences.sortMode=(preferences.sortMode+1)%3;DirtyPreferences();}DrawTable();}
            else if(view==2)DrawRelics();else DrawHistory();
            Fill(new Rect(16,BodyHeight-37,panel.width-32,1),new Color(.2f,.25f,.3f));
            GUI.Label(new Rect(18,BodyHeight-29,panel.width-42,24),"F1 视图  F2 范围  F3 隐藏  F4 本局清零  F5 精简  F6 辅助合并/分开  F7 历史",small);
            if(view!=3)Label(new Rect(panel.width-250,BodyHeight-58,232,21),"战斗时间 "+Stats.Time.ToString("N1")+"s",small,muted);
            Label(new Rect(panel.width-250,BodyHeight-104,232,23),"B站：-_小西瓜_-",signature,accent);
            Label(new Rect(panel.width-250,BodyHeight-81,232,22),"uid:153345791",signature,muted);
        }
        void DrawAppearance() {
            Fill(new Rect(16,52,panel.width-32,88),card);
            GUI.Label(new Rect(28,59,100,24),"窗口透明度",normal);
            float transparency=GUI.HorizontalSlider(new Rect(140,68,panel.width-370,20),1-preferences.opacity,0,.8f);
            GUI.Label(new Rect(panel.width-215,59,60,24),(transparency*100).ToString("0")+"%",normal);
            if(Mathf.Abs((1-transparency)-preferences.opacity)>.0001f) {
                preferences.opacity=1-transparency;DirtyPreferences();
            }
            if(Btn(new Rect(panel.width-142,59,124,28),"恢复默认",false)){preferences.ResetAppearance();HudLayout.Constrain(preferences,Screen.width,Screen.height);DirtyPreferences();}
            if(Btn(new Rect(panel.width-142,101,124,28),"自动适配屏幕",false)) {
                HudLayout.Fit(preferences,Screen.width,Screen.height);DirtyPreferences();
            }
            GUI.Label(new Rect(28,105,panel.width-200,22),"拖拽右下角斜角，分别调整宽高",small);
        }
        void DirtyPreferences() {preferencesDirty=true;saveAt=Time.unscaledTime+.4f;}
        void LoadPreferences() {
            bool loaded=false;
            try {if(File.Exists(SettingsPath)){preferences=JsonUtility.FromJson<HudPreferences>(File.ReadAllText(SettingsPath))??new HudPreferences();loaded=true;}}
            catch(Exception e){Debug.LogWarning("[OneWayDps] Settings load: "+e.Message);preferences=new HudPreferences();}
            if(!loaded)preferences.layoutVersion=2;
            int oldVersion=preferences.layoutVersion;
            HudLayout.Migrate(preferences,Screen.width,Screen.height);
            if(oldVersion<2)DirtyPreferences();
        }
        void SavePreferences() {
            preferencesDirty=false;
            try {preferences.Sanitize();Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));File.WriteAllText(SettingsPath,JsonUtility.ToJson(preferences,true));}
            catch(Exception e){Debug.LogWarning("[OneWayDps] Settings save: "+e.Message);}
        }
        void OnApplicationQuit(){if(preferencesDirty)SavePreferences();RunRecorder.SceneEnding();}
        static string SlotLabel(string slot) {
            switch(slot) {
                case "slot_front":return "前方";
                case "slot_front_top":return "前上";
                case "slot_top":return "顶部";
                case "slot_back_top":return "后上";
                case "slot_back":return "后方";
                case "device_slot_wheel":return "车轮";
                default:return "";
            }
        }
        void DrawTable() {
            float width=panel.width;
            float notesHeight=Mathf.Max(68,footerNotes.CalcHeight(new GUIContent(WeaponNotes),width-280));
            float notesY=BodyHeight-49-notesHeight;
            float comparisonY=notesY-26-54;
            GUI.Label(new Rect(22,182,width-386,25),"伤害来源 / 品质",small);
            Label(new Rect(width-361,182,72,25),"实时 DPS",right,muted);Label(new Rect(width-286,182,76,25),"全局 DPS",right,muted);Label(new Rect(width-206,182,80,25),"总伤害",right,muted);Label(new Rect(width-122,182,76,25),"伤害占比",right,muted);
            Rect view=new Rect(16,213,width-32,Mathf.Max(60,comparisonY-12-213));
            scroll=GUI.BeginScrollView(view,scroll,new Rect(0,0,width-52,Mathf.Max(view.height,rows.Count*66)));
            for(int i=0;i<rows.Count;i++) {
                var e=rows[i];var v=Visual(e);float y=i*66;
                Fill(new Rect(0,y,width-52,61),selected==e.Id?new Color(.12f,.22f,.26f):card);
                Fill(new Rect(0,y,3,61),v.Color);
                Fill(new Rect(12,y+9,44,44),new Color(.035f,.05f,.07f));WeaponAssets.DrawIcon(new Rect(14,y+11,40,40),v.Icon);
                Label(new Rect(68,y+6,width-456,25),v.Name,nameStyle,v.Color);
                GUI.Label(new Rect(68,y+32,width-456,21),v.Quality+(SlotLabel(e.Slot)==""?"":"  ·  "+SlotLabel(e.Slot)),small);
                GUI.Label(new Rect(width-377,y+9,72,25),e.Meter.Dps.ToString("N1"),right);
                GUI.Label(new Rect(width-302,y+9,76,25),Stats.GlobalDpsFor(e).ToString("N1"),right);
                GUI.Label(new Rect(width-222,y+9,80,25),e.Meter.Total.ToString("N0"),right);
                double share=Stats.Share(e);Label(new Rect(width-138,y+9,76,25),(share*100).ToString("F1")+"%",right,v.Color);
                Fill(new Rect(width-374,y+44,310,4),new Color(.04f,.06f,.08f));Fill(new Rect(width-374,y+44,(float)(310*share),4),v.Color);
                if(GUI.Button(new Rect(0,y,width-383,61),"",GUIStyle.none)){selected=e.Id;this.view=1;}
            }
            if(rows.Count==0)GUI.Label(new Rect(22,22,540,56),"等待已装备武器或首次有效命中…",normal);
            GUI.EndScrollView();
            DrawDamageComparison(comparisonY);
            GUI.Label(new Rect(18,notesY,width-280,notesHeight),WeaponNotes,footerNotes);
        }
        void DrawDamageComparison(float y) {
            float cell=(panel.width-32)/3;
            Fill(new Rect(16,y,panel.width-32,54),card);
            string[] names={"飘字累计","有效累计","差额（含溢出）"};
            double[] values={Stats.ReportedDamage,Stats.Total.Total,Stats.DifferenceDamage};
            for(int i=0;i<3;i++) {
                float x=26+cell*i;
                GUI.Label(new Rect(x,y+3,cell-20,22),names[i],normal);
                Label(new Rect(x,y+26,cell-20,25),values[i].ToString("N0"),comparisonValue,i==1?accent:ink);
            }
        }
        float NoteHeight(string text,float width,float minimum) {
            if(String.IsNullOrEmpty(text))return minimum;
            return Mathf.Max(minimum,notes.CalcHeight(new GUIContent(text??""),Mathf.Max(40,width)));
        }
        void DrawRelics() {
            GUI.Label(new Rect(18,182,panel.width-36,36),relicSnapshot.Status,notes);
            float contentWidth=panel.width-52,totalHeight=36+(relicSnapshot.Bonuses.Count==0?30:0);
            string category=null;
            foreach(var bonus in relicSnapshot.Bonuses) {
                if(category!=bonus.Category){totalHeight+=30;category=bonus.Category;}
                totalHeight+=48+NoteHeight(bonus.Sources,contentWidth-28,18)+NoteHeight(bonus.Note,contentWidth-28,0);
            }
            totalHeight+=38;
            foreach(var relic in relicSnapshot.Relics)
                totalHeight+=44+NoteHeight(relic.Description,contentWidth-28,20)+NoteHeight(relic.Details,contentWidth-28,0);
            Rect viewport=new Rect(16,225,panel.width-32,Mathf.Max(80,BodyHeight-355));
            relicScroll=GUI.BeginScrollView(viewport,relicScroll,new Rect(0,0,contentWidth,Mathf.Max(totalHeight,viewport.height)));
            GUI.Label(new Rect(12,0,contentWidth-24,27),"数值增益汇总",bold);
            float y=36;category=null;
            foreach(var bonus in relicSnapshot.Bonuses) {
                if(category!=bonus.Category){category=bonus.Category;Label(new Rect(12,y,contentWidth-24,26),category,normal,accent);y+=30;}
                float sourcesHeight=NoteHeight(bonus.Sources,contentWidth-28,18),noteHeight=NoteHeight(bonus.Note,contentWidth-28,0);
                float height=48+sourcesHeight+noteHeight;
                Fill(new Rect(0,y,contentWidth,height-6),card);
                GUI.Label(new Rect(12,y+6,contentWidth-200,26),bonus.Name,nameStyle);
                Label(new Rect(contentWidth-180,y+6,168,26),bonus.ValueText,right,accent);
                GUI.Label(new Rect(12,y+34,contentWidth-28,sourcesHeight),bonus.Sources,notes);
                if(noteHeight>0)GUI.Label(new Rect(12,y+34+sourcesHeight,contentWidth-28,noteHeight),bonus.Note,notes);
                y+=height;
            }
            if(relicSnapshot.Bonuses.Count==0){GUI.Label(new Rect(12,y,contentWidth-24,27),"暂无可直接汇总的数值增益，条件效果见下方奇物说明。",small);y+=30;}
            GUI.Label(new Rect(12,y,contentWidth-24,30),"持有奇物与条件效果",bold);y+=38;
            foreach(var relic in relicSnapshot.Relics) {
                float descHeight=NoteHeight(relic.Description,contentWidth-28,20),detailsHeight=NoteHeight(relic.Details,contentWidth-28,0);
                float height=44+descHeight+detailsHeight;
                Fill(new Rect(0,y,contentWidth,height-6),card);
                GUI.Label(new Rect(12,y+6,contentWidth-24,27),relic.Name,nameStyle);
                GUI.Label(new Rect(12,y+34,contentWidth-28,descHeight),relic.Description,notes);
                if(detailsHeight>0)GUI.Label(new Rect(12,y+34+descHeight,contentWidth-28,detailsHeight),relic.Details,notes);
                y+=height;
            }
            GUI.EndScrollView();
            GUI.Label(new Rect(18,BodyHeight-111,panel.width-280,67),relicSnapshot.Scope,notes);
        }
        double NiceMax(double v) {
            if(v<=1)return 1;double exp=Math.Pow(10,Math.Floor(Math.Log10(v)));double n=v/exp;
            return (n<=1?1:n<=2?2:n<=5?5:10)*exp;
        }
        void DrawGraph() {
            var tracked=Stats.GraphEntries(selected);
            Rect plot=new Rect(69,221,panel.width-105,Mathf.Max(80,BodyHeight-430));
            double start=Math.Max(0,Stats.Time-ranges[rangeIndex]),end=start+ranges[rangeIndex],max=1;
            foreach(var s in Stats.History)if(s.Time>=start){max=Math.Max(max,s.Total);foreach(var e in tracked)max=Math.Max(max,s.Value(e.Id));}
            max=NiceMax(max*1.12);
            GUI.Label(new Rect(18,183,panel.width-235,25),tracked.Count==1?"DPS / "+DisplayName(tracked[0]):"DPS / 全部伤害来源",small);
            if(Btn(new Rect(panel.width-202,182,186,27),selected>=0?"显示全部来源":"总计 + 全部来源",selected>=0))selected=-1;
            Fill(plot,new Color(.025f,.039f,.058f));
            for(int i=0;i<=4;i++) {
                float y=plot.yMax-plot.height*i/4;
                Fill(new Rect(plot.x,y,plot.width,1),new Color(.16f,.2f,.25f));
                Label(new Rect(8,y-10,53,21),(max*i/4).ToString("0.#"),right,muted);
                float x=plot.x+plot.width*i/4;Fill(new Rect(x,plot.y,1,plot.height),new Color(.12f,.16f,.2f));
                GUI.Label(new Rect(x-14,plot.yMax+5,72,22),(start+(end-start)*i/4).ToString("0.#")+"s",small);
            }
            DrawRaster(plot,start,end,max,tracked);
            Fill(new Rect(20,plot.yMax+42,16,3),accent);GUI.Label(new Rect(42,plot.yMax+34,180,23),"总 DPS",small);
            int columns=Math.Max(1,(int)((panel.width-52)/196));float cellWidth=(panel.width-52)/columns;
            Rect legendView=new Rect(16,plot.yMax+60,panel.width-32,Mathf.Max(28,BodyHeight-115-(plot.yMax+60)));
            legendScroll=GUI.BeginScrollView(legendView,legendScroll,new Rect(0,0,panel.width-52,Mathf.Max(legendView.height,((tracked.Count+columns-1)/columns)*25)));
            for(int i=0;i<tracked.Count;i++) {
                var e=tracked[i];var v=Visual(e);int col=i%columns,row=i/columns;float x=col*cellWidth,y=row*25;
                Fill(new Rect(x+4,y+9,12,3),SeriesColor(e.Id));
                Label(new Rect(x+22,y,cellWidth-28,24),DisplayName(e),small,v.Color);
                if(GUI.Button(new Rect(x,y,cellWidth-4,24),"",GUIStyle.none))selected=e.Id;
            }
            GUI.EndScrollView();
            if(Stats.History.Count==0)GUI.Label(new Rect(plot.x+Mathf.Max(20,(plot.width-350)/2),plot.y+plot.height/2-15,350,30),"战斗开始后生成实时 DPS 曲线",normal);
            Vector2 mouse=Event.current.mousePosition;
            if(plot.Contains(mouse) && Stats.History.Count>0) {
                double t=start+(mouse.x-plot.x)/plot.width*(end-start);CombatStats.Sample nearest=null;double gap=Double.MaxValue;
                foreach(var s in Stats.History)if(Math.Abs(s.Time-t)<gap){nearest=s;gap=Math.Abs(s.Time-t);}
                if(nearest!=null && nearest.Time>=start) {
                    float x=plot.x+(float)((nearest.Time-start)/(end-start))*plot.width;
                    Fill(new Rect(x,plot.y,1,plot.height),new Color(.8f,.9f,1f,.5f));
                    string tip=nearest.Time.ToString("F2")+"s  |  总 DPS "+nearest.Total.ToString("N1");
                    float tipHeight=tracked.Count==1?48:25;
                    Fill(new Rect(plot.x+8,plot.y+8,plot.width-16,tipHeight),new Color(.06f,.1f,.15f,.95f));GUI.Label(new Rect(plot.x+14,plot.y+9,plot.width-28,23),tip,small);
                    if(tracked.Count==1)GUI.Label(new Rect(plot.x+14,plot.y+31,plot.width-28,23),DisplayName(tracked[0])+"  DPS "+nearest.Value(tracked[0].Id).ToString("N1"),small);
                }
            }
        }
        string DisplayName(CombatStats.Entry e) {
            string slot=SlotLabel(e.Slot);return Visual(e).Name+(slot==""?"":" · "+slot);
        }
        static Color SeriesColor(int id) {return Color.HSVToRGB((id*.61803398875f+.12f)%1f,.62f,.96f);}
        Texture2D chartTexture; Color32[] chartPixels; string chartKey;
        void DrawRaster(Rect plot,double start,double end,double max,List<CombatStats.Entry> tracked) {
            double sampledTime=Stats.History.Count>0?Stats.History[Stats.History.Count-1].Time:0;
            string key=sampledTime.ToString("F2")+":"+rangeIndex+":"+max;
            foreach(var e in tracked)key+=":"+e.Id;
            if(chartTexture==null){chartTexture=new Texture2D(535,214,TextureFormat.RGBA32,false);chartTexture.filterMode=FilterMode.Bilinear;chartPixels=new Color32[535*214];}
            if(key!=chartKey) {
                chartKey=key;Array.Clear(chartPixels,0,chartPixels.Length);
                RasterSeries(start,end,max,-1,accent,2.4f);
                for(int i=0;i<tracked.Count;i++)RasterSeries(start,end,max,tracked[i].Id,SeriesColor(tracked[i].Id),1.6f);
                chartTexture.SetPixels32(chartPixels);chartTexture.Apply(false,false);
            }
            GUI.DrawTexture(plot,chartTexture,ScaleMode.StretchToFill,true);
        }
        void RasterSeries(double start,double end,double max,int id,Color color,float width) {
            bool has=false;float oldX=0,oldY=0;
            foreach(var sample in Stats.History) {
                if(sample.Time<start)continue;
                float x=(float)((sample.Time-start)/(end-start))*534;
                float y=(float)((id<0?sample.Total:sample.Value(id))/max)*213;
                if(has)RasterLine(oldX,oldY,x,y,color,width);
                oldX=x;oldY=y;has=true;
            }
        }
        void RasterLine(float ax,float ay,float bx,float by,Color c,float width) {
            int steps=Math.Max(1,(int)Math.Ceiling(Math.Max(Math.Abs(bx-ax),Math.Abs(by-ay))*1.5));
            float radius=width/2;
            for(int step=0;step<=steps;step++) {
                float t=(float)step/steps,x=ax+(bx-ax)*t,y=ay+(by-ay)*t;
                for(int py=(int)Math.Floor(y-radius);py<=(int)Math.Ceiling(y+radius);py++)
                for(int px=(int)Math.Floor(x-radius);px<=(int)Math.Ceiling(x+radius);px++) {
                    if(px<0 || py<0 || px>=535 || py>=214)continue;
                    float dx=px-x,dy=py-y;
                    float alpha=Mathf.Clamp01(radius+.5f-Mathf.Sqrt(dx*dx+dy*dy))*c.a;
                    if(alpha<=0)continue;
                    int i=py*535+px;Color old=chartPixels[i];
                    if(alpha>old.a)chartPixels[i]=new Color(c.r,c.g,c.b,alpha);
                }
            }
        }
        void OnDestroy(){if(preferencesDirty)SavePreferences();if(chartTexture!=null)Destroy(chartTexture);}
    }
}



