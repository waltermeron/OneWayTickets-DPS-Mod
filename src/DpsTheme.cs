using System.Collections.Generic;
using UnityEngine;

namespace OneWayDps {
    public sealed partial class DpsOverlay {
        // Procedural UI chrome only. Weapon sprites and their quality colors are untouched.
        static readonly Color bg=new Color(.13f,.085f,.064f,.97f),
            card=new Color(.19f,.127f,.094f,.97f),
            muted=new Color(.73f,.65f,.53f), ink=new Color(.94f,.87f,.73f),
            accent=new Color(.96f,.62f,.19f), rim=new Color(.37f,.26f,.18f),
            edge=new Color(.065f,.043f,.033f), recess=new Color(.095f,.066f,.049f),
            selectedCard=new Color(.28f,.18f,.095f), grid=new Color(.26f,.19f,.135f);
        readonly List<Texture2D> themeTextures=new List<Texture2D>();
        GUISkin gameSkin;
        GUIStyle selectedButton,buttonLabel;

        Texture2D FrameTexture(Color body,Color border,int arrow) {
            const int size=12;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
            texture.name="OneWayDps UI frame";texture.filterMode=FilterMode.Point;
            texture.wrapMode=TextureWrapMode.Clamp;texture.hideFlags=HideFlags.HideAndDontSave;
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++) {
                bool corner=(x<1 || x>size-2) && (y<1 || y>size-2);
                Color color=body;
                if(corner)color=Color.clear;
                else if(x==0 || y==0 || x==size-1 || y==size-1)color=edge;
                else if(x==1 || y==1 || x==size-2 || y==size-2)color=border;
                else if(y==size-3)color=Color.Lerp(body,ink,.12f);
                else if(y==2)color=Color.Lerp(body,edge,.5f);
                pixels[y*size+x]=color;
            }
            // Small scrollbar arrows retain the native controls, using the same palette.
            if(arrow!=0)for(int row=0;row<3;row++)for(int offset=-row;offset<=row;offset++) {
                int x=5+offset,y=arrow==1?7-row:arrow==2?4+row:5+offset;
                if(arrow==3 || arrow==4){x=arrow==3?4+row:7-row;y=5+offset;}
                pixels[y*size+x]=muted;
            }
            texture.SetPixels(pixels);texture.Apply(false,true);themeTextures.Add(texture);return texture;
        }
        void SetFrames(GUIStyle style,Texture2D normalFrame,Texture2D hover,Texture2D pressed) {
            style.border=new RectOffset(4,4,4,4);
            style.normal.background=normalFrame;style.hover.background=hover;
            style.active.background=pressed;style.focused.background=hover;
            style.onNormal.background=pressed;style.onHover.background=hover;
            style.onActive.background=pressed;style.onFocused.background=hover;
        }
        void InitTheme() {
            var normalFrame=FrameTexture(new Color(.29f,.19f,.12f),rim,0);
            var hover=FrameTexture(new Color(.36f,.235f,.12f),new Color(.65f,.40f,.17f),0);
            var pressed=FrameTexture(new Color(.36f,.215f,.08f),accent,0);
            button=new GUIStyle(GUIStyle.none);SetFrames(button,normalFrame,hover,pressed);
            selectedButton=new GUIStyle(button);SetFrames(selectedButton,pressed,hover,pressed);
            buttonLabel=new GUIStyle(normal);buttonLabel.alignment=TextAnchor.MiddleCenter;
            buttonLabel.clipping=TextClipping.Clip;
            gameSkin=Instantiate(GUI.skin);gameSkin.name="OneWayDps warm pixel UI";
            gameSkin.hideFlags=HideFlags.HideAndDontSave;
            var track=FrameTexture(recess,rim,0);
            foreach(var style in new[]{gameSkin.verticalScrollbar,gameSkin.horizontalScrollbar})SetFrames(style,track,track,track);
            foreach(var style in new[]{gameSkin.verticalScrollbarThumb,gameSkin.horizontalScrollbarThumb})SetFrames(style,normalFrame,hover,pressed);
            gameSkin.verticalScrollbar.fixedWidth=14;gameSkin.verticalScrollbarThumb.fixedWidth=14;
            gameSkin.horizontalScrollbar.fixedHeight=14;gameSkin.horizontalScrollbarThumb.fixedHeight=14;
            GUIStyle[] arrows={gameSkin.verticalScrollbarUpButton,gameSkin.verticalScrollbarDownButton,gameSkin.horizontalScrollbarLeftButton,gameSkin.horizontalScrollbarRightButton};
            for(int i=0;i<arrows.Length;i++) {
                var glyph=FrameTexture(card,rim,i+1);SetFrames(arrows[i],glyph,glyph,glyph);
                arrows[i].fixedWidth=14;arrows[i].fixedHeight=14;
            }
            SetFrames(gameSkin.horizontalSlider,track,track,track);
            gameSkin.horizontalSlider.fixedHeight=8;
            SetFrames(gameSkin.horizontalSliderThumb,normalFrame,hover,pressed);
            gameSkin.horizontalSliderThumb.fixedWidth=14;gameSkin.horizontalSliderThumb.fixedHeight=18;
            gameSkin.horizontalSliderThumb.margin=new RectOffset(0,0,-5,0);
            gameSkin.settings.selectionColor=new Color(.67f,.39f,.12f,.6f);
            gameSkin.settings.cursorColor=ink;
        }
        void PixelPanel(Rect r) {
            // Borders occupy strips, rather than stacking opaque full-panel fills.
            // This preserves the existing transparency control in the panel interior.
            Fill(new Rect(r.x+4,r.y,r.width-8,2),edge);
            Fill(new Rect(r.x+4,r.yMax-2,r.width-8,2),edge);
            Fill(new Rect(r.x,r.y+4,2,r.height-8),edge);
            Fill(new Rect(r.xMax-2,r.y+4,2,r.height-8),edge);
            foreach(float x in new[]{r.x+2,r.xMax-4})foreach(float y in new[]{r.y+2,r.yMax-4})Fill(new Rect(x,y,2,2),edge);
            Fill(new Rect(r.x+4,r.y+2,r.width-8,3),rim);
            Fill(new Rect(r.x+4,r.yMax-5,r.width-8,3),rim);
            Fill(new Rect(r.x+2,r.y+4,3,r.height-8),rim);
            Fill(new Rect(r.xMax-5,r.y+4,3,r.height-8),rim);
            Fill(new Rect(r.x+5,r.y+5,r.width-10,40),card);
            Fill(new Rect(r.x+5,r.y+45,r.width-10,r.height-50),bg);
            Fill(new Rect(r.x+5,r.y+5,r.width-10,2),new Color(.47f,.33f,.23f));
            Fill(new Rect(r.x+7,r.y+46,r.width-14,1),rim);
        }
        void Surface(Rect r) {
            Fill(new Rect(r.x+1,r.y+1,r.width-2,r.height-2),card);
            Fill(new Rect(r.x,r.y,r.width,1),rim);Fill(new Rect(r.x,r.yMax-1,r.width,1),rim);
            Fill(new Rect(r.x,r.y+1,1,r.height-2),rim);Fill(new Rect(r.xMax-1,r.y+1,1,r.height-2),rim);
            Fill(new Rect(r.x+2,r.y+2,r.width-4,1),new Color(.24f,.17f,.12f));
        }
        void DisposeTheme() {
            if(gameSkin!=null)Destroy(gameSkin);
            foreach(var texture in themeTextures)if(texture!=null)Destroy(texture);
            themeTextures.Clear();
        }
    }
}
