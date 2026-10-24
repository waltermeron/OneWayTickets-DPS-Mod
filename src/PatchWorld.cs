using System;
using System.IO;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
class PatchWorld {
    static IEnumerable<TypeDefinition> All(IEnumerable<TypeDefinition> types) {
        foreach(var t in types) { yield return t; foreach(var n in All(t.NestedTypes)) yield return n; }
    }
    static MethodDefinition Find(AssemblyDefinition a, string type, string method) {
        foreach(var t in All(a.MainModule.Types)) if(t.FullName == type)
            foreach(var m in t.Methods) if(m.Name == method) return m;
        throw new Exception("Required method missing: " + type + "::" + method);
    }
    static void Prefix(MethodDefinition method, MethodReference hook, bool arg) {
        var il = method.Body.GetILProcessor(); var first = method.Body.Instructions[0];
        if(arg) il.InsertBefore(first, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(first, il.Create(OpCodes.Call, hook));
    }
    static int Main(string[] args) {
        try {
            if(args.Length != 3) throw new Exception("Usage: PatchWorld original.dll OneWayDps.dll output.dll");
            var resolver = new DefaultAssemblyResolver(); resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0])));
            var p = new ReaderParameters { AssemblyResolver = resolver };
            using(var game = AssemblyDefinition.ReadAssembly(args[0], p))
            using(var mod = AssemblyDefinition.ReadAssembly(args[1], p)) {
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
                game.Write(args[2]);
                Console.WriteLine("Patched " + count + " damage calls + 3 lifecycle hooks.");
            }
            using(var verify = AssemblyDefinition.ReadAssembly(args[2],p)) {
                int calls = 0, life = 0;
                foreach(var t in All(verify.MainModule.Types)) foreach(var m in t.Methods) if(m.HasBody)
                    foreach(var i in m.Body.Instructions) {
                        var mr = i.Operand as MethodReference;
                        if(mr != null && mr.DeclaringType.FullName == "OneWayDps.Hooks") { if(mr.Name == "Hurt") calls++; else life++; }
                    }
                if(calls < 20 || life != 3) throw new Exception("Written assembly verification failed");
                Console.WriteLine("Written assembly verified: " + calls + " damage calls, " + life + " lifecycle hooks.");
            }
            return 0;
        } catch(Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
