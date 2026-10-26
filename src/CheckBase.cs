using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
// A missing reference to these two mods is a compatibility guard, not proof of Steam provenance.
class CheckBase {
    static IEnumerable<TypeDefinition> All(IEnumerable<TypeDefinition> types) {
        foreach(var t in types){yield return t;foreach(var n in All(t.NestedTypes))yield return n;}
    }
    static int Main(string[] args) {
        try {
            foreach(var path in args)using(var a=AssemblyDefinition.ReadAssembly(path)) {
                if(a.MainModule.AssemblyReferences.Any(r=>r.Name=="OneWayDps" || r.Name=="OneWaySpeaker"))
                    throw new Exception("Candidate still contains mod hooks: "+path+". Verify Steam files first.");
                foreach(var t in All(a.MainModule.Types))foreach(var m in t.Methods.Where(m=>m.HasBody))
                    foreach(var i in m.Body.Instructions) {
                        var member=i.Operand as MemberReference;
                        if(member!=null && (member.FullName.Contains("OneWayDps.") || member.FullName.Contains("OneWaySpeaker.")))
                            throw new Exception("Candidate contains mod calls: "+path);
                    }
                Console.WriteLine("Clean hook base: "+a.Name.Name+" MVID="+a.MainModule.Mvid);
            }
            return 0;
        } catch(Exception e){Console.Error.WriteLine(e.Message);return 1;}
    }
}
