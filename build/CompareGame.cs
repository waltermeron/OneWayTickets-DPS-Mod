using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
class CompareGame {
    static IEnumerable<TypeDefinition> All(IEnumerable<TypeDefinition> ts){foreach(var t in ts){yield return t;foreach(var n in All(t.NestedTypes))yield return n;}}
    static int Main(string[] args){try{using(var old=AssemblyDefinition.ReadAssembly(args[0]))using(var current=AssemblyDefinition.ReadAssembly(args[1])){
        var before=All(old.MainModule.Types).ToDictionary(t=>t.FullName);var after=All(current.MainModule.Types).ToArray();
        Console.WriteLine("Original MVID: "+old.MainModule.Mvid+"; new MVID: "+current.MainModule.Mvid);
        Console.WriteLine("Type counts: "+before.Count+" -> "+after.Length);
        foreach(var t in after){if(!before.ContainsKey(t.FullName))Console.WriteLine("Added type: "+t.FullName);
            else if(t.FullName.Contains("CaravanHoveringInfo") || t.FullName.Contains("Widget_Blower_Context")){
                Console.WriteLine("FIELDS "+t.FullName+": "+String.Join(", ",t.Fields.Select(f=>f.FullName)));
                foreach(var m in t.Methods.Where(m=>m.HasBody && (m.Name=="Init" || m.Name=="tick"))){Console.WriteLine("IL "+m.FullName);foreach(var i in m.Body.Instructions)Console.WriteLine(i);}
            }
        }
    }return 0;}catch(Exception error){Console.Error.WriteLine(error);return 1;}}
}
