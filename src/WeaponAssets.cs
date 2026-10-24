using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using World.Devices;
using AutoCodes;
using Commons;
using Addrs;
namespace OneWayDps {
    public sealed class WeaponVisual {
        public string Name="其他伤害", Quality="来源未归属武器";
        public Color Color=new Color(.65f,.73f,.85f);
        public Sprite Icon; public int Rank;
    }
    public static class WeaponAssets {
        static readonly Dictionary<uint,WeaponVisual> cache=new Dictionary<uint,WeaponVisual>();
        static readonly Dictionary<int,Color> colors=new Dictionary<int,Color>();
        static readonly WeaponVisual other=new WeaponVisual();
        public static WeaponVisual Get(object key) {
            var d=key as Device; if(d==null || d.desc==null)return other;
            var desc=d.desc; WeaponVisual v;
            if(cache.TryGetValue(desc.id,out v))return v;
            v=new WeaponVisual(); v.Name=Regex.Replace(Localization_Utility.get_localization(desc.name) ?? desc.name,"<[^>]+>","");
            v.Rank=desc.rank; v.Quality=desc.rank>=100?"特殊品质":"品质 "+desc.rank;
            if(!String.IsNullOrEmpty(desc.icon_photo))Addressable_Utility.try_load_asset<Sprite>(desc.icon_photo,out v.Icon);
            if(!colors.TryGetValue(desc.rank,out v.Color)) {
                device_rank rank; Sprite bar=null;
                if(device_ranks.TryGetValue(desc.rank.ToString(),out rank) && !String.IsNullOrEmpty(rank.img_name_bar))
                    Addressable_Utility.try_load_asset<Sprite>(rank.img_name_bar,out bar);
                v.Color=ReadQualityColor(bar,desc.rank); colors.Add(desc.rank,v.Color);
            }
            cache.Add(desc.id,v);
            Debug.Log("[OneWayDps] Weapon "+desc.id+" "+v.Name+" rank="+v.Rank+" icon="+(v.Icon!=null)+" color="+ColorUtility.ToHtmlStringRGB(v.Color));
            return v;
        }
        static Color ReadQualityColor(Sprite sprite,int rank) {
            Color[] fallback={new Color(.88f,.89f,.9f),new Color(.47f,.9f,.55f),new Color(.38f,.71f,1f),new Color(.8f,.55f,1f),new Color(1f,.73f,.3f),new Color(1f,.4f,.4f)};
            if(sprite==null)return fallback[Mathf.Clamp(rank,0,fallback.Length-1)];
            RenderTexture temp=null; Texture2D copy=null; RenderTexture prev=RenderTexture.active;
            try {
                Rect r=sprite.textureRect; Texture tex=sprite.texture;
                temp=RenderTexture.GetTemporary(32,32,0,RenderTextureFormat.ARGB32);
                Graphics.Blit(tex,temp,new Vector2(r.width/tex.width,r.height/tex.height),new Vector2(r.x/tex.width,r.y/tex.height));
                RenderTexture.active=temp; copy=new Texture2D(32,32,TextureFormat.RGBA32,false);
                copy.ReadPixels(new Rect(0,0,32,32),0,0); copy.Apply();
                var count=new Dictionary<int,int>(); var sum=new Dictionary<int,Vector3>();
                foreach(Color c in copy.GetPixels()) {
                    float h,s,v; Color.RGBToHSV(c,out h,out s,out v);
                    if(c.a<.8f || s<.18f || v<.3f)continue;
                    int bucket=(int)(h*24); int n; count.TryGetValue(bucket,out n);count[bucket]=n+1;
                    Vector3 accum;sum.TryGetValue(bucket,out accum);sum[bucket]=accum+new Vector3(h,s,v);
                }
                int best=-1, max=0;foreach(var kv in count)if(kv.Value>max){best=kv.Key;max=kv.Value;}
                if(best<0)return fallback[Mathf.Clamp(rank,0,fallback.Length-1)];
                Vector3 hsv=sum[best]/max;
                // Preserve the game's quality hue, raising brightness for legibility on the dark HUD.
                return Color.HSVToRGB(hsv.x,Mathf.Clamp(hsv.y,.25f,.8f),Mathf.Max(.92f,hsv.z));
            }catch(Exception e){Debug.LogWarning("[OneWayDps] Quality swatch fallback: "+e.Message);return fallback[Mathf.Clamp(rank,0,fallback.Length-1)];}
            finally {RenderTexture.active=prev;if(temp!=null)RenderTexture.ReleaseTemporary(temp);if(copy!=null)UnityEngine.Object.Destroy(copy);}
        }
        public static void DrawIcon(Rect box,Sprite sprite) {
            if(sprite==null)return;
            try {
                Rect r=sprite.textureRect; Texture t=sprite.texture;
                float scale=Mathf.Min(box.width/r.width,box.height/r.height);
                Rect dest=new Rect(box.x+(box.width-r.width*scale)/2,box.y+(box.height-r.height*scale)/2,r.width*scale,r.height*scale);
                GUI.DrawTextureWithTexCoords(dest,t,new Rect(r.x/t.width,r.y/t.height,r.width/t.width,r.height/t.height),true);
            }catch {GUI.DrawTexture(box,sprite.texture,ScaleMode.ScaleToFit,true);}
        }
    }
}

