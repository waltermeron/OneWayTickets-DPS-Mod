using System;
using System.Runtime.CompilerServices;
using World;
using World.Devices;
using World.Projectiles;

namespace OneWayDps {
    public sealed class SharedDamageSource {
        public readonly string Name,Script;
        internal SharedDamageSource(string name,string script){Name=name;Script=script;}
    }
    // These tags are observation only. The game's emitter, attack data and callbacks stay intact.
    public static class DamageAttribution {
        sealed class Frame {
            public object Auxiliary;public Device Weapon;public bool Player,ExtraOnly;
            public Device Ammo;public uint ProjectileId;
            public Frame Direct,Explosion;
        }
        sealed class Load {public Device Quiver;public uint ProjectileId;}
        static ConditionalWeakTable<object,Frame> tags=new ConditionalWeakTable<object,Frame>();
        static ConditionalWeakTable<Device,Load> loads=new ConditionalWeakTable<Device,Load>();
        [ThreadStatic] static Frame current;
        static readonly SharedDamageSource blast=new SharedDamageSource("奇物：秘能爆炸","Relic_Blast_Modify_Variable"),
            thunder=new SharedDamageSource("奇物：闪电","Relic_Thunder_Modify_Variable"),
            dart=new SharedDamageSource("奇物：飞箭","Relic_Dart_Modify_Variable");
        public static void NewRun(){tags=new ConditionalWeakTable<object,Frame>();loads=new ConditionalWeakTable<Device,Load>();current=null;}
        public static bool IsSupport(Device device){return device!=null && (device.GetType().Name=="DScript_Special_Quiver" || device.GetType().Name=="DScript_Backup_Ammo");}
        static Frame Copy(Frame f){return f==null?new Frame():new Frame{Auxiliary=f.Auxiliary,Weapon=f.Weapon,Player=f.Player,ExtraOnly=f.ExtraOnly,Ammo=f.Ammo,ProjectileId=f.ProjectileId};}
        static Frame Find(ITarget source) {
            for(int i=0;i<16 && source!=null;i++) {
                Frame f;if(tags.TryGetValue(source,out f) && !f.ExtraOnly)return f;
                var p=source as Projectile;if(p!=null){source=p.emitter;continue;}
                source=Hooks.AdherentSource(source);
            }
            return null;
        }
        public static uint AmmoProvided(uint result,Device quiver,Device weapon) {
            try {if(weapon!=null){loads.Remove(weapon);if(result>1 && quiver!=null)loads.Add(weapon,new Load{Quiver=quiver,ProjectileId=result});}}catch(Exception){}
            return result;
        }
        // kind: 1 shooting; 2 projectile action; 3 projectile explosion; 4 relic trigger; 5 delayed object/callback.
        public static object Enter(object owner,ITarget trigger,int kind,uint projectileId) {
            Frame previous=current;
            try {
                var f=previous;
                if(kind==1) {
                    f=new Frame{Weapon=owner as Device,Player=Hooks.IsPlayerSource(owner as ITarget),ProjectileId=projectileId};
                    if(previous!=null && previous.Auxiliary!=null && previous.Player)f.Auxiliary=previous.Auxiliary;
                    Load load;if(f.Weapon!=null && loads.TryGetValue(f.Weapon,out load) && load.ProjectileId==projectileId)f.Ammo=load.Quiver;
                } else if(kind==2 || kind==3) {
                    var p=owner as Projectile;Frame stored;
                    if(p!=null && tags.TryGetValue(p,out stored))f=kind==3?(stored.Explosion??stored):(stored.Direct??stored);
                    else f=new Frame{Weapon=Hooks.ResolveWeapon(p),Player=Hooks.IsPlayerSource(p)};
                    if(f.Player!=Hooks.IsPlayerSource(p)){f=Copy(f);f.Player=Hooks.IsPlayerSource(p);}
                } else if(kind==4) {
                    f=new Frame{Auxiliary=owner,Weapon=previous==null?null:previous.Weapon,Player=true};
                    if(trigger!=null && Hooks.IsPlayerSource(trigger)){var w=Hooks.ResolveWeapon(trigger);if(w!=null)f.Weapon=w;}
                    string name=owner==null?"":owner.GetType().Name;
                    if(name==blast.Script)f.Auxiliary=blast;else if(name==thunder.Script)f.Auxiliary=thunder;else if(name==dart.Script)f.Auxiliary=dart;
                } else if(kind==5) {Frame stored;if(owner!=null && tags.TryGetValue(owner,out stored))f=stored;}
                current=f;
            }catch(Exception){current=previous;}
            return previous;
        }
        public static void Exit(object previous){current=previous as Frame;}
        public static void ObjectCreated(object value) {
            try{if(value!=null && current!=null && current.Auxiliary!=null){tags.Remove(value);tags.Add(value,Copy(current));}}catch(Exception){}
        }
        public static void ProjectileReady(Projectile p) {
            try {
                if(p==null)return;tags.Remove(p);
                if(!DpsOverlay.Active || current==null || !Hooks.IsPlayerSource(p))return;
                Frame f=Copy(current);f.Weapon=Hooks.ResolveWeapon(p)??f.Weapon;
                if(f.Ammo!=null && p.desc!=null && p.desc.projectile_id==f.ProjectileId){if(f.Auxiliary==null){f.Auxiliary=f.Ammo;f.ExtraOnly=true;}}
                else f.Ammo=null;
                if(f.ExtraOnly)f.Direct=new Frame{Weapon=f.Weapon,Player=f.Player};
                if(f.Ammo!=null){f.Explosion=Copy(f);f.Explosion.Auxiliary=f.Ammo;f.Explosion.ExtraOnly=false;}
                tags.Add(p,f);
            }catch(Exception){}
        }
        public static bool PlayerContext {get{return current!=null && current.Player && current.Auxiliary!=null;}}
        public static void Resolve(ITarget source,out object weapon,out object auxiliary) {
            weapon=Hooks.ResolveWeapon(source);auxiliary=null;
            Frame f=current!=null && current.Auxiliary!=null?current:Find(source);
            if(f!=null && f.Player){weapon=f.Weapon??weapon;auxiliary=f.Auxiliary;}
        }
    }
}
