using System;
namespace OneWayDps {
    [Serializable]
    public sealed class HudPreferences {
        public float opacity=1f, size=1f, x=18f, y=105f;
        public float width=760f, height=668f;
        public bool dpsOnly;
        public int sortMode;
        public int layoutVersion;
        static float Clamp(float value,float low,float high,float fallback) {
            if(float.IsNaN(value) || float.IsInfinity(value))return fallback;
            return Math.Max(low,Math.Min(high,value));
        }
        public void Sanitize() {
            opacity=Clamp(opacity,.2f,1f,1f);size=Clamp(size,.65f,1.5f,1f);
            x=Clamp(x,0,10000,18);y=Clamp(y,0,10000,105);
            width=Clamp(width,HudLayout.MinWidth,5000,760);height=Clamp(height,HudLayout.MinHeight,5000,668);
            sortMode=Math.Max(0,Math.Min(2,sortMode));
        }
        public void ResetAppearance() {opacity=1f;size=1f;width=760;height=668;}
    }
    // B站：-_小西瓜_- / uid:153345791。位置使用屏幕像素，右下角拖拽独立调整宽高。
    public static class HudLayout {
        public const float MinWidth=428f, MinHeight=400f, CompactHeight=140f, Margin=8f;
        static float Clamp(float value,float low,float high){return Math.Max(low,Math.Min(high,value));}
        public static float DisplayHeight(HudPreferences p,bool compact){return compact?CompactHeight*Math.Min(1,p.width/640f):p.height;}
        public static float BaseScale(float screenHeight){return Clamp(screenHeight/1080f,.65f,2f);}
        public static float ScreenScale(float screenWidth,float screenHeight) {
            return Math.Max(.01f,Math.Min(BaseScale(screenHeight),Math.Min((screenWidth-Margin*2)/MinWidth,(screenHeight-Margin*2)/MinHeight)));
        }
        public static void Constrain(HudPreferences p,float screenWidth,float screenHeight) {
            Constrain(p,screenWidth,screenHeight,false);
        }
        public static void Constrain(HudPreferences p,float screenWidth,float screenHeight,bool compact) {
            p.Sanitize();
            float scale=ScreenScale(screenWidth,screenHeight);
            p.width=Clamp(p.width,MinWidth,Math.Max(MinWidth,(screenWidth-Margin*2)/scale));
            if(!compact)p.height=Clamp(p.height,MinHeight,Math.Max(MinHeight,(screenHeight-Margin*2)/scale));
            p.x=Clamp(p.x,Margin,Math.Max(Margin,screenWidth-Margin-p.width*scale));
            p.y=Clamp(p.y,Margin,Math.Max(Margin,screenHeight-Margin-DisplayHeight(p,compact)*scale));
        }
        public static void Move(HudPreferences p,float startX,float startY,float deltaScreenX,float deltaScreenY,float screenWidth,float screenHeight) {
            Move(p,startX,startY,deltaScreenX,deltaScreenY,screenWidth,screenHeight,false);
        }
        public static void Move(HudPreferences p,float startX,float startY,float deltaScreenX,float deltaScreenY,float screenWidth,float screenHeight,bool compact) {
            p.x=startX+deltaScreenX;p.y=startY+deltaScreenY;
            Constrain(p,screenWidth,screenHeight,compact);
        }
        public static void Resize(HudPreferences p,float startWidth,float startHeight,float deltaScreenX,float deltaScreenY,float screenWidth,float screenHeight) {
            Resize(p,startWidth,startHeight,deltaScreenX,deltaScreenY,screenWidth,screenHeight,false);
        }
        public static void Resize(HudPreferences p,float startWidth,float startHeight,float deltaScreenX,float deltaScreenY,float screenWidth,float screenHeight,bool compact) {
            float scale=ScreenScale(screenWidth,screenHeight);
            float maximumWidth=Math.Max(MinWidth,(screenWidth-Margin-p.x)/scale);
            float maximumHeight=Math.Max(MinHeight,(screenHeight-Margin-p.y)/scale);
            if(compact) {
                float availableHeight=(screenHeight-Margin-p.y)/scale;
                if(availableHeight<CompactHeight)maximumWidth=Math.Min(maximumWidth,Math.Max(MinWidth,640*availableHeight/CompactHeight));
            }
            p.width=Clamp(startWidth+deltaScreenX/scale,MinWidth,maximumWidth);
            if(!compact)p.height=Clamp(startHeight+deltaScreenY/scale,MinHeight,maximumHeight);
        }
        public static void Fit(HudPreferences p,float screenWidth,float screenHeight) {
            float scale=ScreenScale(screenWidth,screenHeight);
            p.x=Margin;p.y=Margin;
            p.width=Math.Min(1400,(screenWidth-Margin*2)/scale);
            if(!p.dpsOnly)p.height=Math.Min(1000,(screenHeight-Margin*2)/scale);
            Constrain(p,screenWidth,screenHeight,p.dpsOnly);
        }
        public static void Migrate(HudPreferences p,float screenWidth,float screenHeight) {
            p.Sanitize();
            if(p.layoutVersion<2) {
                if(p.layoutVersion<1) {
                    float oldScale=Math.Min(BaseScale(screenHeight)*p.size,Math.Min((screenWidth-16)/640f,(screenHeight-16)/668f));
                    p.x*=oldScale;p.y*=oldScale;
                }
                p.width=640*p.size;p.height=668*p.size;p.layoutVersion=2;
            }
            Constrain(p,screenWidth,screenHeight,p.dpsOnly);
        }
        public static float SurfaceAlpha(float colorAlpha,float opacity){return Clamp(colorAlpha,0,1)*Clamp(opacity,0,1);}
    }
}
