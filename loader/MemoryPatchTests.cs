using System;using System.IO;using System.Linq;using System.Collections.Generic;using OneWayDps.Loading;
class MemoryPatchTests {
    static int Main(string[] args){try{
        string managed=args[0],runtime=args[1],output=args[2];string speaker=args.Length>3?args[3]:null;
        Directory.CreateDirectory(output);var before=Directory.GetFiles(managed,"*.dll").ToDictionary(p=>p,p=>File.ReadAllBytes(p));
        using(var p=MemoryPlan.Prepare(managed,runtime,speaker)){
            if(p.DamageCalls!=74 || p.Scopes!=93 || p.Captures!=25)throw new Exception("Current build coverage changed");
            foreach(var a in p.Modules)a.Value.Write(Path.Combine(output,a.Key));
            File.WriteAllBytes(Path.Combine(output,"OneWayDps.dll"),p.Runtime);
            if(p.Speaker!=null)File.WriteAllBytes(Path.Combine(output,"OneWaySpeaker.dll"),p.Speaker);
            Console.WriteLine("PASS memory patch plan; "+p.DamageCalls+" damage calls; speaker="+(p.Speaker!=null));
        }
        foreach(var f in before)if(!f.Value.SequenceEqual(File.ReadAllBytes(f.Key)))throw new Exception("Source file changed: "+f.Key);
        Console.WriteLine("PASS original Managed DLL bytes unchanged");return 0;
    }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
