using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
class PatchWorld {
    static IEnumerable<TypeDefinition> All(IEnumerable<TypeDefinition> types) {
        foreach(var t in types) { yield return t; foreach(var n in All(t.NestedTypes)) yield return n; }
    }
    static MethodDefinition Find(AssemblyDefinition a, string type, string method) {
        foreach(var t in All(a.MainModule.Types)) if(t.FullName == type) {
            var matches=t.Methods.Where(m=>m.Name==method && m.HasBody).ToArray();
            if(matches.Length==1)return matches[0];
            throw new Exception("Required method missing or ambiguous: "+type+"::"+method);
        }
        throw new Exception("Required method missing: " + type + "::" + method);
    }
    static void Prefix(MethodDefinition method, MethodReference hook, bool arg) {
        var il = method.Body.GetILProcessor(); var first = method.Body.Instructions[0];
        if(arg) il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(first, il.Create(OpCodes.Call, hook));
    }
    static void After(MethodDefinition method,MethodReference hook,int parameter) {
        var il=method.Body.GetILProcessor();
        foreach(var ret in method.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ret).ToArray()) {
            if(parameter>=0){ret.OpCode=OpCodes.Ldarg;ret.Operand=method.Parameters[parameter];var call=il.Create(OpCodes.Call,hook);il.InsertAfter(ret,call);il.InsertAfter(call,il.Create(OpCodes.Ret));}
            else{ret.OpCode=OpCodes.Call;ret.Operand=hook;il.InsertAfter(ret,il.Create(OpCodes.Ret));}
        }
        Widen(method);
    }
    static void Widen(MethodDefinition method) {
        foreach(var i in method.Body.Instructions) {
            switch(i.OpCode.Code) {
                case Code.Br_S:i.OpCode=OpCodes.Br;break;case Code.Brfalse_S:i.OpCode=OpCodes.Brfalse;break;case Code.Brtrue_S:i.OpCode=OpCodes.Brtrue;break;
                case Code.Beq_S:i.OpCode=OpCodes.Beq;break;case Code.Bge_S:i.OpCode=OpCodes.Bge;break;case Code.Bge_Un_S:i.OpCode=OpCodes.Bge_Un;break;
                case Code.Bgt_S:i.OpCode=OpCodes.Bgt;break;case Code.Bgt_Un_S:i.OpCode=OpCodes.Bgt_Un;break;case Code.Ble_S:i.OpCode=OpCodes.Ble;break;
                case Code.Ble_Un_S:i.OpCode=OpCodes.Ble_Un;break;case Code.Blt_S:i.OpCode=OpCodes.Blt;break;case Code.Blt_Un_S:i.OpCode=OpCodes.Blt_Un;break;
                case Code.Bne_Un_S:i.OpCode=OpCodes.Bne_Un;break;case Code.Leave_S:i.OpCode=OpCodes.Leave;break;
            }
        }
    }
    static void Scope(MethodDefinition m,MethodReference enter,MethodReference exit,int kind) {
        var body=m.Body;body.InitLocals=true;var il=body.GetILProcessor();var first=body.Instructions[0];
        var scope=new VariableDefinition(m.Module.TypeSystem.Object);body.Variables.Add(scope);
        VariableDefinition result=null;if(m.ReturnType.MetadataType!=MetadataType.Void){result=new VariableDefinition(m.ReturnType);body.Variables.Add(result);}
        var end=il.Create(OpCodes.Nop);var returns=body.Instructions.Where(i=>i.OpCode==OpCodes.Ret).ToArray();
        foreach(var i in returns){if(result!=null){i.OpCode=OpCodes.Stloc;i.Operand=result;il.InsertAfter(i,il.Create(OpCodes.Leave,end));}else{i.OpCode=OpCodes.Leave;i.Operand=end;}}
        var finalStart=il.Create(OpCodes.Ldloc,scope);
        foreach(var h in body.ExceptionHandlers){if(h.TryEnd==null)h.TryEnd=finalStart;if(h.HandlerEnd==null)h.HandlerEnd=finalStart;}
        il.Append(finalStart);il.Append(il.Create(OpCodes.Call,exit));il.Append(il.Create(OpCodes.Endfinally));il.Append(end);
        if(result!=null)il.Append(il.Create(OpCodes.Ldloc,result));il.Append(il.Create(OpCodes.Ret));
        body.ExceptionHandlers.Add(new ExceptionHandler(ExceptionHandlerType.Finally){TryStart=first,TryEnd=finalStart,HandlerStart=finalStart,HandlerEnd=end});
        il.InsertBefore(first,il.Create(OpCodes.Ldarg_0));
        var trigger=kind==4?m.Parameters.FirstOrDefault(p=>p.ParameterType.FullName=="World.ITarget" || p.ParameterType.FullName=="World.Projectiles.Projectile" || p.ParameterType.FullName=="World.Devices.Device"):null;
        il.InsertBefore(first,trigger==null?il.Create(OpCodes.Ldnull):il.Create(OpCodes.Ldarg,trigger));
        il.InsertBefore(first,il.Create(OpCodes.Ldc_I4,kind));
        il.InsertBefore(first,kind==1?il.Create(OpCodes.Ldarg,m.Parameters[2]):il.Create(OpCodes.Ldc_I4_0));
        il.InsertBefore(first,il.Create(OpCodes.Call,enter));il.InsertBefore(first,il.Create(OpCodes.Stloc,scope));
        Widen(m);
    }
    static int Attribution(AssemblyDefinition game,AssemblyDefinition mod) {
        var module=game.MainModule;
        var enter=module.ImportReference(Find(mod,"OneWayDps.DamageAttribution","Enter"));var exit=module.ImportReference(Find(mod,"OneWayDps.DamageAttribution","Exit"));
        var created=module.ImportReference(Find(mod,"OneWayDps.DamageAttribution","ObjectCreated"));var ready=module.ImportReference(Find(mod,"OneWayDps.DamageAttribution","ProjectileReady"));
        var supplied=module.ImportReference(Find(mod,"OneWayDps.DamageAttribution","AmmoProvided"));
        var types=All(module.Types).ToArray();int scopes=0,captures=0;
        var delayed=new HashSet<string>(types.Where(t=>!t.IsValueType &&
            (t.FullName=="World.Adherents.Adherent" || t.FullName=="World.Helpers.Thunder_Helper/Thunder" ||
            (t.IsNested && t.FullName.Contains("<>c__DisplayClass") &&
            (t.FullName.StartsWith("World.Relic.Relics.") || t.FullName.StartsWith("World.Projectiles.") ||
             t.FullName.StartsWith("World.BattleUtility/") || t.FullName.StartsWith("World.Enemys.Enemy_Buff_Helper/")))))
            .Select(t=>t.FullName));
        string[] relicMethods={"division_projectile","add_event","melee_arrow_chasing_event","arrow_period","melee_assassinate_event","melee_ignite_event","knock_off","immediatly_shoot","do_shoot","dart_melee","trigger_process","trigger_target_die","blast_hit","blast_hit_back","blast_device_destroyed","blast_click","thunder_dart","thunder_projectile","thunder_melee","thunder_hit_back","thunder_crush"};
        foreach(var t in types)foreach(var m in t.Methods.Where(m=>m.HasBody)) {
            var il=m.Body.GetILProcessor();
            // Capture provenance on compiler generated closures and delayed effect objects.
            foreach(var i in m.Body.Instructions.ToArray())if(i.OpCode==OpCodes.Newobj){var ctor=i.Operand as MethodReference;if(ctor!=null && delayed.Contains(ctor.DeclaringType.FullName)){var dup=il.Create(OpCodes.Dup);il.InsertAfter(i,dup);il.InsertAfter(dup,il.Create(OpCodes.Call,created));captures++;}}
            if(t.FullName=="World.Devices.Device_AI.DScript_Special_Quiver" && m.Name=="afford_ammo") {
                foreach(var i in m.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ret).ToArray()) {
                    // Return values already on the stack are passed through unchanged.
                    i.OpCode=OpCodes.Ldarg_0;i.Operand=null;var a=il.Create(OpCodes.Ldarg_1);il.InsertAfter(i,a);var call=il.Create(OpCodes.Call,supplied);il.InsertAfter(a,call);il.InsertAfter(call,il.Create(OpCodes.Ret));
                }
            }
            if(t.FullName=="World.Projectiles.Projectile" && m.Name=="Init")foreach(var i in m.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ret).ToArray()) {
                i.OpCode=OpCodes.Ldarg_0;i.Operand=null;var call=il.Create(OpCodes.Call,ready);il.InsertAfter(i,call);il.InsertAfter(call,il.Create(OpCodes.Ret));
            }
            if(!m.IsStatic && !m.IsConstructor) {
                int kind=0;
                if(t.Namespace=="World.Devices.Device_AI" && m.Name=="single_shoot" && m.Parameters.Count==3)kind=1;
                else if((t.FullName=="World.Projectiles.Projectile" || t.FullName=="World.Projectiles.DartProjectile") && m.Name=="projectile_explode")kind=3;
                else if((t.FullName=="World.Projectiles.Projectile" || t.FullName=="World.Projectiles.DartProjectile") && new[]{"tick","hit_enemy","arrow_hit_enemy","bullet_hit_enemy","destroy_hit_enemy","hit_ground","movement_in_object","dart_explode"}.Contains(m.Name))kind=2;
                else if(t.Namespace=="World.Relic.Relics" && (relicMethods.Contains(m.Name) || (m.Name=="Tick" && t.Name=="Relic_Dart_Modify_Variable")))kind=4;
                else if(delayed.Contains(t.FullName) && !m.IsGetter && !m.IsSetter)kind=5;
                if(kind!=0){Scope(m,enter,exit,kind);scopes++;}
            }
            Widen(m);
        }
        if(scopes<25 || captures<15)throw new Exception("Unexpected attribution instrumentation counts");
        Console.WriteLine("Attribution: "+scopes+" exception-safe scopes, "+captures+" delayed-object capture sites.");return scopes;
    }
    public static void ApplyWorld(AssemblyDefinition game,AssemblyDefinition mod) {
        var damageInterface=All(game.MainModule.Types).Single(t=>t.FullName=="World.ITarget").Methods.Single(m=>m.Name=="hurt");
        if(damageInterface.ReturnType.FullName!="System.Void" || damageInterface.Parameters.Count!=3 || damageInterface.Parameters[0].ParameterType.FullName!="World.ITarget" || damageInterface.Parameters[1].ParameterType.FullName!="World.Attack_Data" || damageInterface.Parameters[2].ParameterType.FullName!="World.Dmg_Data&")throw new Exception("Damage interface signature changed");
        var endMethod=Find(game,"World.WorldSceneRoot","SetSettlement");
        if(endMethod.Parameters.Count!=1 || endMethod.Parameters[0].ParameterType.FullName!="System.Boolean")throw new Exception("Settlement signature changed");
                foreach(var reference in game.MainModule.AssemblyReferences) if(reference.Name == "OneWayDps") throw new Exception("Already patched");
                var module = game.MainModule;
                var hurt = module.ImportReference(Find(mod, "OneWayDps.Hooks", "Hurt"));
                int count = 0;
                foreach(var t in All(module.Types)) foreach(var m in t.Methods) if(m.HasBody)
                    foreach(var i in m.Body.Instructions) {
                        var callee = i.Operand as MethodReference;
                        if(i.OpCode == OpCodes.Callvirt && callee != null && callee.DeclaringType.FullName == "World.ITarget" && callee.Name == "hurt" && callee.Parameters.Count == 3) {
                            i.OpCode = OpCodes.Call; i.Operand = hurt; count++;
                        }
                    }
                if(count < 20) throw new Exception("Unexpected damage interface: only " + count + " call sites");
                Prefix(Find(game,"World.WorldSceneRoot","on_init"), module.ImportReference(Find(mod,"OneWayDps.Hooks","Begin")),false);
                Prefix(Find(game,"World.WorldSceneRoot","on_fini"), module.ImportReference(Find(mod,"OneWayDps.Hooks","End")),false);
                Prefix(Find(game,"World.WorldContext","ctx_tick_start"), module.ImportReference(Find(mod,"OneWayDps.Hooks","Tick")),true);
                Attribution(game,mod);
                After(Find(game,"World.WorldSceneRoot","on_init"),module.ImportReference(Find(mod,"OneWayDps.RunRecorder","SceneReady")),-1);
                Prefix(Find(game,"World.WorldSceneRoot","back_campScene"),module.ImportReference(Find(mod,"OneWayDps.RunRecorder","ReturnCamp")),false);
                var settlement=Find(game,"World.WorldSceneRoot","SetSettlement");var si=settlement.Body.GetILProcessor();var sf=settlement.Body.Instructions[0];
                si.InsertBefore(sf,si.Create(OpCodes.Ldarg,settlement.Parameters[0]));si.InsertBefore(sf,si.Create(OpCodes.Call,module.ImportReference(Find(mod,"OneWayDps.RunRecorder","Settlement"))));Widen(settlement);

    }
    public static void ApplyCommons(AssemblyDefinition common,AssemblyDefinition mod) {
        var loaded=Find(common,"Commons.SL_Helper","load_data");
        if(loaded.Parameters.Count!=1 || loaded.Parameters[0].ParameterType.FullName!="System.Collections.Generic.Dictionary`2<System.String,System.Object>")throw new Exception("Native save-load signature changed");
                if(common.MainModule.AssemblyReferences.Any(r=>r.Name=="OneWayDps"))throw new Exception("Commons is already patched");
                var module=common.MainModule;
                Prefix(Find(common,"Commons.SL_Helper","load_game"),module.ImportReference(Find(mod,"OneWayDps.RunRecorder","Menu")),false);
                After(Find(common,"Commons.SL_Helper","load_data"),module.ImportReference(Find(mod,"OneWayDps.RunRecorder","Loaded")),0);
                After(Find(common,"Commons.SL_Helper","save_game_safearea"),module.ImportReference(Find(mod,"OneWayDps.RunRecorder","Saved")),-1);
                After(Find(common,"Commons.SL_Helper","new_save"),module.ImportReference(Find(mod,"OneWayDps.RunRecorder","NewSave")),-1);

    }
    static int Main(string[] args) {
        try {
            if(args.Length!=5)throw new Exception("Usage: PatchWorld originalWorld mod outputWorld originalCommons outputCommons");
            using(var mod=AssemblyDefinition.ReadAssembly(args[1])) {
                using(var game=AssemblyDefinition.ReadAssembly(args[0])){ApplyWorld(game,mod);game.Write(args[2]);}
                using(var common=AssemblyDefinition.ReadAssembly(args[3])){ApplyCommons(common,mod);common.Write(args[4]);}
            }
            Console.WriteLine("DPS hooks generated; original game code retained.");return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
