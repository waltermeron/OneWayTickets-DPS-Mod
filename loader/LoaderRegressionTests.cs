using System;using System.IO;using System.Linq;using Mono.Cecil;using OneWayDps.Loading;
class LoaderRegressionTests {
    static int Main(string[] args){string temp=null;try{
        temp=Path.Combine(Path.GetFullPath(args[2]),"negative-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
        foreach(var f in Directory.GetFiles(args[0],"*.dll"))File.Copy(f,Path.Combine(temp,Path.GetFileName(f)));
        string world=Path.Combine(temp,"World.dll");byte[] original=File.ReadAllBytes(world);int passed=0;
        using(var p=MemoryPlan.Prepare(temp,args[1],null)){p.Modules["World.dll"].Write(Path.Combine(temp,"duplicate.dll"));}
        File.Copy(Path.Combine(temp,"duplicate.dll"),world,true);
        try{using(var p=MemoryPlan.Prepare(temp,args[1],null)){}throw new Exception("Duplicate hooks accepted");}catch(Exception e){if(!e.Message.Contains("Old static mod hooks"))throw;passed++;}
        File.WriteAllBytes(world,original);
        using(var a=AssemblyDefinition.ReadAssembly(new MemoryStream(original))){MemoryPlan.All(a.MainModule.Types).Single(t=>t.FullName=="World.ITarget").Methods.Single(m=>m.Name=="hurt").Name="renamed_hurt";a.Write(world);}
        try{using(var p=MemoryPlan.Prepare(temp,args[1],null)){}throw new Exception("Missing API accepted");}catch(Exception e){if(!e.Message.Contains("API changed") && !e.Message.Contains("no matching"))throw;passed++;}
        File.WriteAllBytes(world,original);
        using(var a=AssemblyDefinition.ReadAssembly(new MemoryStream(original))){a.MainModule.Mvid=Guid.NewGuid();a.MainModule.Types.Add(new TypeDefinition("FutureUpdate","NewMarker",TypeAttributes.Class|TypeAttributes.Public,a.MainModule.TypeSystem.Object));a.Write(world);}
        using(var p=MemoryPlan.Prepare(temp,args[1],null)){if(!p.Modules["World.dll"].MainModule.Types.Any(t=>t.FullName=="FutureUpdate.NewMarker"))throw new Exception("New update type lost");passed++;}
        Console.WriteLine("PASS loader regression: "+passed+" scenarios; duplicate hooks rejected, changed API rejected, changed MVID/new type accepted and preserved.");return 0;
    }catch(Exception e){Console.Error.WriteLine(e);return 1;}finally{if(temp!=null && Directory.Exists(temp))Directory.Delete(temp,true);}}
}
