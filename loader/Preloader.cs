using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using BepInEx;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace OneWayDps.Loading {
    // No Unity or game assembly references are allowed in this preloader assembly.
    public sealed class MemoryPlan : IDisposable {
        readonly DefaultAssemblyResolver resolver=new DefaultAssemblyResolver();
        public readonly Dictionary<string,AssemblyDefinition> Modules=new Dictionary<string,AssemblyDefinition>();
        public byte[] Runtime,Speaker;
        public int DamageCalls,Scopes,Captures;
        public static IEnumerable<TypeDefinition> All(IEnumerable<TypeDefinition> ts){foreach(var t in ts){yield return t;foreach(var n in All(t.NestedTypes))yield return n;}}
        AssemblyDefinition Read(string path){return AssemblyDefinition.ReadAssembly(path,new ReaderParameters{AssemblyResolver=resolver,ReadingMode=ReadingMode.Immediate,InMemory=true});}
        static byte[] Bytes(AssemblyDefinition a){using(var stream=new MemoryStream()){a.Write(stream);return stream.ToArray();}}
        static void Clean(AssemblyDefinition a){if(a.MainModule.AssemblyReferences.Any(r=>r.Name=="OneWayDps" || r.Name=="OneWaySpeaker"))throw new Exception("Old static mod hooks found in "+a.Name.Name+". Run DPSMod/Install.ps1 once to migrate; do not stack both loaders.");}
        static string[] Layout(AssemblyDefinition a){return All(a.MainModule.Types).Select(t=>t.FullName+"|"+t.Attributes+"|"+t.BaseType+"|"+String.Join(";",t.Fields.Select(f=>f.FullName+":"+f.Attributes).ToArray())+"|"+String.Join(";",t.Methods.Select(m=>m.FullName).ToArray())).ToArray();}
        static string[] OriginalCalls(AssemblyDefinition a){return All(a.MainModule.Types).SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Where(i=>i.Operand is MethodReference).Select(i=>(MethodReference)i.Operand).Where(r=>!r.DeclaringType.FullName.StartsWith("OneWayDps.")).Select(r=>r.FullName).ToArray();}
        static void Api(AssemblyDefinition a){
            var gameNames=new HashSet<string>(new[]{"World","Commons","AutoCodes","Foundations"});
            foreach(var t in All(a.MainModule.Types))foreach(var m in t.Methods.Where(m=>m.HasBody))foreach(var i in m.Body.Instructions){
                var member=i.Operand as MemberReference;if(member==null || member.DeclaringType==null)continue;
                var scope=member.DeclaringType.Scope as AssemblyNameReference;if(scope==null || !gameNames.Contains(scope.Name))continue;
                var method=member as MethodReference;var field=member as FieldReference;
                if(method!=null && method.Resolve()==null || field!=null && field.Resolve()==null)throw new Exception("Required game API changed: "+member.FullName);
            }
        }
        public static MemoryPlan Prepare(string managed,string runtime,string speaker){
            var plan=new MemoryPlan();try{
                plan.resolver.AddSearchDirectory(managed);plan.resolver.AddSearchDirectory(Path.GetDirectoryName(runtime));
                plan.resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(AssemblyDefinition).Assembly.Location));
                var world=plan.Read(Path.Combine(managed,"World.dll"));plan.Modules.Add("World.dll",world);
                var common=plan.Read(Path.Combine(managed,"Commons.dll"));plan.Modules.Add("Commons.dll",common);
                Clean(world);Clean(common);
                var worldLayout=Layout(world);var commonLayout=Layout(common);var commonCalls=OriginalCalls(common);
                int hurt=All(world.MainModule.Types).SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Count(i=>i.Operand is MethodReference && ((MethodReference)i.Operand).DeclaringType.FullName=="World.ITarget" && ((MethodReference)i.Operand).Name=="hurt");
                using(var mod=plan.Read(runtime)){
                    Api(mod);PatchWorld.ApplyWorld(world,mod);PatchWorld.ApplyCommons(common,mod);
                    if(!worldLayout.SequenceEqual(Layout(world)) || !commonLayout.SequenceEqual(Layout(common)))throw new Exception("Game type/field/method layout changed by patcher");
                    if(!commonCalls.SequenceEqual(OriginalCalls(common)))throw new Exception("Native save call order changed");
                    foreach(var m in All(world.MainModule.Types).SelectMany(t=>t.Methods).Where(m=>m.HasBody))foreach(var i in m.Body.Instructions){var r=i.Operand as MethodReference;if(r==null)continue;if(r.DeclaringType.FullName=="OneWayDps.Hooks" && r.Name=="Hurt")plan.DamageCalls++;if(r.DeclaringType.FullName=="OneWayDps.DamageAttribution"){if(r.Name=="Enter")plan.Scopes++;if(r.Name=="ObjectCreated")plan.Captures++;}}
                    if(hurt!=plan.DamageCalls || hurt<20)throw new Exception("Damage call coverage mismatch");
                    if(speaker!=null){
                        using(var audio=plan.Read(speaker)){
                            Api(audio);var camp=plan.Read(Path.Combine(managed,"Camp.dll"));Clean(camp);plan.Modules.Add("Camp.dll",camp);
                            foreach(var a in plan.Modules.Values)SpeakerMemoryPatch.Apply(a,audio);
                            SpeakerMemoryPatch.Apply(mod,audio);plan.Speaker=Bytes(audio);
                        }
                    }
                    plan.Runtime=Bytes(mod);
                }
                // Round-trip every candidate before publishing any of the module replacements.
                foreach(var key in plan.Modules.Keys.ToArray()){
                    var a=plan.Modules[key];var bytes=Bytes(a);var replacement=AssemblyDefinition.ReadAssembly(new MemoryStream(bytes),new ReaderParameters{AssemblyResolver=plan.resolver,ReadingMode=ReadingMode.Immediate});
                    plan.Modules[key]=replacement;a.Dispose();
                }
                return plan;
            }catch{plan.Dispose();throw;}
        }
        public void Dispose(){foreach(var a in Modules.Values)a.Dispose();Modules.Clear();resolver.Dispose();}
    }
    public static class Patcher {
        static MemoryPlan plan;static bool attempted;static string workingDirectory;
        public static bool Ready {get;private set;}
        public static Assembly RuntimeAssembly {get;private set;}
        public static string Failure {get;private set;}
        static readonly ManualLogSource log=Logger.CreateLogSource("OneWayDps Preloader");
        public static IEnumerable<string> TargetDLLs {get{Initialize();return plan==null?new string[0]:plan.Modules.Keys.ToArray();}}
        public static void Initialize(){
            if(attempted)return;attempted=true;
            try{
                // Unity Mono #20968: byte-loaded assemblies can get an invalid CodeBase
                // when loaded with a non-ASCII working directory. Only change cwd during
                // preload; restore it before the game's startup and native save handling.
                workingDirectory=Environment.CurrentDirectory;
                string safe=Environment.GetFolderPath(Environment.SpecialFolder.System);
                if(!String.IsNullOrEmpty(safe) && safe.All(c=>c<128))Environment.CurrentDirectory=safe;
                string runtime=Path.Combine(Paths.PluginPath,"OneWayDps","OneWayDps.dll");
                string speaker=Path.Combine(Paths.PluginPath,"OneWaySpeaker","OneWaySpeaker.dll");
                // Presence of the optional plugin, rather than legacy folders, controls the speaker layer.
                plan=MemoryPlan.Prepare(Paths.ManagedPath,runtime,File.Exists(speaker)?speaker:null);
                log.LogInfo("v1.3.0 prepared in memory: "+plan.DamageCalls+" damage calls, "+plan.Scopes+" contexts, "+plan.Captures+" captures. Game DLLs are not written.");
            }catch(Exception e){RestoreDirectory();Failure=e.ToString();log.LogError("DPS disabled: compatibility check failed. Game files were not changed. "+Failure);}
        }
        static void RestoreDirectory(){if(workingDirectory!=null){Environment.CurrentDirectory=workingDirectory;workingDirectory=null;}}
        public static void Patch(ref AssemblyDefinition assembly){
            if(plan==null)return;string key=assembly.Name.Name+".dll";
            AssemblyDefinition candidate;if(!plan.Modules.TryGetValue(key,out candidate))return;
            // Refuse to erase a different preloader's earlier changes.
            if(assembly.MainModule.AssemblyReferences.Any(r=>r.Name=="OneWayDps" || r.Name=="OneWaySpeaker"))throw new Exception("Duplicate mod loader: "+key);
            assembly=candidate;
        }
        public static void Finish(){
            if(plan==null){RestoreDirectory();return;}
            try{
                if(plan.Speaker!=null)Assembly.LoadFrom(Path.Combine(Paths.PluginPath,"OneWaySpeaker","OneWaySpeaker.dll"));
                // LoadFrom registers one identity with Mono's normal resolver. Load(byte[])
                // would allow a second physical copy and split the static statistics state.
                string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(plan.Runtime)).Replace("-","");
                string cache=Path.Combine(Paths.BepInExRootPath,"cache","OneWayDps",hash);
                Directory.CreateDirectory(cache);string file=Path.Combine(cache,"OneWayDps.dll");
                if(!File.Exists(file))File.WriteAllBytes(file,plan.Runtime);
                RuntimeAssembly=Assembly.LoadFrom(file);Ready=true;
                log.LogInfo("v1.3.0 runtime assemblies loaded. Optional speaker="+(plan.Speaker!=null));
            }catch(Exception e){Failure=e.ToString();log.LogError("DPS runtime load failed: "+Failure);}finally{RestoreDirectory();}
        }
    }
}
