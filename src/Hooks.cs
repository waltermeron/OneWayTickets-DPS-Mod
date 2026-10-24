using System;
using System.Reflection;
using UnityEngine;
using World;
using World.Devices;
using World.Projectiles;
namespace OneWayDps {
    public static class Hooks {
        static bool warned;
        static readonly FieldInfo adherentSource=typeof(World.Adherents.Adherent).GetField("_source",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        public static void Begin() {try{DpsOverlay.Begin();}catch(Exception e){Warn(e);}}
        public static void End() {DpsOverlay.Active=false;}
        public static void Tick(WorldContext ctx) {
            if(DpsOverlay.Active && !ctx.in_safe_area && !ctx.is_in_settlement && !ctx.is_freeze_time) DpsOverlay.Stats.Tick();
        }
        static void Warn(Exception e) {if(!warned){warned=true;Debug.LogWarning("[OneWayDps] "+e);}}
        public static Device ResolveWeapon(ITarget source) {
            for(int i=0;i<16 && source!=null;i++) {
                var d=source as Device; if(d!=null)return d;
                var p=source as Projectile; if(p!=null){source=p.emitter;continue;}
                var a=source as World.Adherents.Adherent;
                if(a!=null && adherentSource!=null){source=adherentSource.GetValue(a) as ITarget;continue;}
                break;
            }
            return null;
        }
        public static bool IsPlayerSource(ITarget source) {
            for(int i=0;i<16 && source!=null;i++) {
                // Adherent.Faction is always neutral in the game; its _source owns the DOT.
                var a=source as World.Adherents.Adherent;
                if(a!=null && adherentSource!=null){source=adherentSource.GetValue(a) as ITarget;continue;}
                // A projectile's explicit faction remains authoritative after reflection.
                return source.Faction==WorldEnum.Faction.player;
            }
            return false;
        }
        public static void Hurt(ITarget target,ITarget source,Attack_Data attack,out Dmg_Data result) {
            bool eligible=false; int before=0; Device weapon=null;
            try {
                eligible=DpsOverlay.Active && source!=null && target!=null && IsPlayerSource(source) && target.Faction==WorldEnum.Faction.opposite;
                if(eligible){before=Math.Max(0,target.hp);eligible=before>0;if(eligible)weapon=ResolveWeapon(source);}
            }catch(Exception e){Warn(e);}
            target.hurt(source,attack,out result);
            try {
                if(eligible && !result.is_heal && !result.is_miss && !result.is_immune && result.dmg>0) {
                    double loss=Math.Max(0.0,(double)before-Math.Max(0,target.hp));
                    DpsOverlay.Stats.RecordHit(weapon,Math.Min(loss,result.dmg),result.dmg);
                }
            }catch(Exception e){Warn(e);}
        }
    }
}
