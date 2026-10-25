using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
class PatchTests {
    static IEnumerable<TypeDefinition> All(IEnumerable<TypeDefinition> types){foreach(var t in types){yield return t;foreach(var n in All(t.NestedTypes))yield return n;}}
    static void Check(bool okay,string name){if(!okay)throw new Exception(name);}
    static string[] Calls(MethodDefinition m){return m.Body.Instructions.Where(i=>i.Operand is MethodReference).Select(i=>(MethodReference)i.Operand)
        .Where(r=>r.DeclaringType.FullName!="OneWayDps.RunRecorder" && r.DeclaringType.FullName!="OneWayDps.DamageAttribution" && !(r.DeclaringType.FullName=="OneWayDps.Hooks" && r.Name!="Hurt"))
        .Select(r=>(r.Name=="hurt" && r.DeclaringType.FullName=="World.ITarget") || (r.Name=="Hurt" && r.DeclaringType.FullName=="OneWayDps.Hooks")?"[original hurt]":r.FullName).ToArray();}
    static int Main(string[] args){try{Run(args);return 0;}catch(Exception error){Console.Error.WriteLine(error.Message);return 1;}}
    static void Run(string[] args){using(var original=AssemblyDefinition.ReadAssembly(args[0]))using(var patched=AssemblyDefinition.ReadAssembly(args[1])){
        var old=All(original.MainModule.Types).SelectMany(t=>t.Methods).ToDictionary(m=>m.FullName);
        var oldTypes=All(original.MainModule.Types).ToArray();var newTypes=All(patched.MainModule.Types).ToArray();
        Check(oldTypes.Select(t=>t.FullName).SequenceEqual(newTypes.Select(t=>t.FullName)),"all game types retained, including scene components");
        foreach(var t in oldTypes){var current=newTypes.First(x=>x.FullName==t.FullName);Check(t.Attributes==current.Attributes && (t.BaseType==null?null:t.BaseType.FullName)==(current.BaseType==null?null:current.BaseType.FullName),"game type layout preserved: "+t.FullName);
            Check(t.Fields.Select(f=>f.FullName+":"+f.Attributes).SequenceEqual(current.Fields.Select(f=>f.FullName+":"+f.Attributes)),"all game fields preserved: "+t.FullName);}
        var methods=All(patched.MainModule.Types).SelectMany(t=>t.Methods).ToArray();Check(old.Count==methods.Length,"game method count unchanged");
        int scopes=0,captures=0,hurt=0,life=0,supply=0,ready=0,history=0;
        foreach(var m in methods)if(m.HasBody){var before=old[m.FullName];Check(Calls(before).SequenceEqual(Calls(m)),"original call order retained: "+m.FullName);
            Check(before.Body.Instructions.Where(i=>i.Operand is FieldReference).Select(i=>i.OpCode.Code+":"+i.Operand).SequenceEqual(m.Body.Instructions.Where(i=>i.Operand is FieldReference).Select(i=>i.OpCode.Code+":"+i.Operand)),"original field accesses retained: "+m.FullName);
            var body=m.Body;var all=new HashSet<Instruction>(body.Instructions);int enter=0,exit=0;
            foreach(var i in body.Instructions){var target=i.Operand as Instruction;if(target!=null)Check(all.Contains(target),"valid branch target");var targets=i.Operand as Instruction[];if(targets!=null)foreach(var t in targets)Check(all.Contains(t),"valid switch target");
                var call=i.Operand as MethodReference;if(call==null)continue;
                if(call.DeclaringType.FullName=="World.ITarget" && call.Name=="hurt")throw new Exception("unhooked hurt");
                if(call.DeclaringType.FullName=="OneWayDps.Hooks"){if(call.Name=="Hurt")hurt++;else life++;}
                if(call.DeclaringType.FullName=="OneWayDps.RunRecorder")history++;
                if(call.DeclaringType.FullName!="OneWayDps.DamageAttribution")continue;
                switch(call.Name){case "Enter":enter++;break;case "Exit":exit++;break;case "ObjectCreated":captures++;break;case "AmmoProvided":supply++;break;case "ProjectileReady":ready++;break;}
            }
            if(enter>0){Check(enter==1 && exit==1,"balanced context: "+m.FullName);scopes++;
                var h=body.ExceptionHandlers.Last();Check(h.HandlerType==ExceptionHandlerType.Finally,"context restored by finally");
                int start=body.Instructions.IndexOf(h.TryStart),end=body.Instructions.IndexOf(h.TryEnd);
                Check(!body.Instructions.Skip(start).Take(end-start).Any(i=>i.OpCode==OpCodes.Ret),"all returns leave protected region");
            }
            foreach(var h in body.ExceptionHandlers){Check(h.TryStart!=null && h.HandlerStart!=null && all.Contains(h.TryStart) && all.Contains(h.HandlerStart),"valid handler starts");Check(h.TryEnd==null || all.Contains(h.TryEnd),"valid try end");Check(h.HandlerEnd==null || all.Contains(h.HandlerEnd),"valid handler end");}
        }
        Check(hurt==74 && life==3,"all original74 damage and3 lifecycle hooks");Check(scopes==93 && captures==25,"expected v0.8.0.3 attribution instrumentation");Check(supply==4 && ready==1,"ammo returns and projectile initialization hooked");
        Check(patched.MainModule.AssemblyReferences.Count(r=>r.Name=="OneWayDps")==1,"single mod assembly binding");
        Check(history==3,"three world history observers");
        Console.WriteLine("PASS game patch: original calls/field accesses preserved; "+hurt+" damage hooks, "+scopes+" balanced scopes, "+captures+" captures");
    }
    using(var original=AssemblyDefinition.ReadAssembly(args[2]))using(var patched=AssemblyDefinition.ReadAssembly(args[3])){
        var before=All(original.MainModule.Types).SelectMany(t=>t.Methods).ToDictionary(m=>m.FullName);int observed=0;
        foreach(var m in All(patched.MainModule.Types).SelectMany(t=>t.Methods))if(m.HasBody){Check(Calls(before[m.FullName]).SequenceEqual(Calls(m)),"Commons original calls retained: "+m.FullName);foreach(var i in m.Body.Instructions){var call=i.Operand as MethodReference;if(call!=null && call.DeclaringType.FullName=="OneWayDps.RunRecorder")observed++;}}
        Check(observed==4,"four Commons history observers");Console.WriteLine("PASS history lifecycle: 3 World + 4 Commons observers; original save calls preserved");
    }}
}
