using System;using System.Linq;using System.Collections.Generic;using Mono.Cecil;using Mono.Cecil.Cil;
// Optional speaker v1.3.0 compatibility: the same hooks, applied in memory only.
static class SpeakerMemoryPatch {
 static IEnumerable<TypeDefinition> All(IEnumerable<TypeDefinition> ts){foreach(var t in ts){yield return t;foreach(var n in All(t.NestedTypes))yield return n;}}
 static MethodDefinition Find(AssemblyDefinition a,string t,string m){return All(a.MainModule.Types).Single(x=>x.FullName==t).Methods.Single(x=>x.Name==m);}
 static void Before(MethodDefinition m,MethodReference h,bool self){var il=m.Body.GetILProcessor();var first=m.Body.Instructions[0];if(self)il.InsertBefore(first,il.Create(OpCodes.Ldarg_0));il.InsertBefore(first,il.Create(OpCodes.Call,h));}
 static void After(MethodDefinition m,MethodReference h){var il=m.Body.GetILProcessor();foreach(var ret in m.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ret).ToArray()){ret.OpCode=OpCodes.Ldarg_0;ret.Operand=null;var c=il.Create(OpCodes.Call,h);il.InsertAfter(ret,c);il.InsertAfter(c,il.Create(OpCodes.Ret));}Widen(m);}
 static void MapReturn(MethodDefinition m,MethodReference h){var il=m.Body.GetILProcessor();foreach(var ret in m.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ret).ToArray()){ret.OpCode=OpCodes.Call;ret.Operand=h;il.InsertAfter(ret,il.Create(OpCodes.Ret));}Widen(m);}
 static void Widen(MethodDefinition m){foreach(var i in m.Body.Instructions){switch(i.OpCode.Code){case Code.Br_S:i.OpCode=OpCodes.Br;break;case Code.Brfalse_S:i.OpCode=OpCodes.Brfalse;break;case Code.Brtrue_S:i.OpCode=OpCodes.Brtrue;break;case Code.Beq_S:i.OpCode=OpCodes.Beq;break;case Code.Bne_Un_S:i.OpCode=OpCodes.Bne_Un;break;case Code.Bge_S:i.OpCode=OpCodes.Bge;break;case Code.Bge_Un_S:i.OpCode=OpCodes.Bge_Un;break;case Code.Bgt_S:i.OpCode=OpCodes.Bgt;break;case Code.Bgt_Un_S:i.OpCode=OpCodes.Bgt_Un;break;case Code.Ble_S:i.OpCode=OpCodes.Ble;break;case Code.Ble_Un_S:i.OpCode=OpCodes.Ble_Un;break;case Code.Blt_S:i.OpCode=OpCodes.Blt;break;case Code.Blt_Un_S:i.OpCode=OpCodes.Blt_Un;break;case Code.Leave_S:i.OpCode=OpCodes.Leave;break;}}}

 public static void Apply(AssemblyDefinition game,AssemblyDefinition mod) {
    if(game.MainModule.AssemblyReferences.Any(r=>r.Name=="OneWaySpeaker"))throw new Exception("Speaker already statically patched. Migrate to clean game files first.");
    string name=game.Name.Name+".dll";
    var module=game.MainModule;Func<string,MethodReference> hook=n=>module.ImportReference(Find(mod,"OneWaySpeaker.Hooks",n));int sprites=0;
    foreach(var t in All(module.Types))foreach(var m in t.Methods.Where(x=>x.HasBody)){
     foreach(var i in m.Body.Instructions){var call=i.Operand as GenericInstanceMethod;if(call!=null && call.DeclaringType.FullName=="Addrs.Addressable_Utility" && call.Name=="try_load_asset" && call.GenericArguments.Count==1 && call.GenericArguments[0].FullName=="UnityEngine.Sprite"){i.OpCode=OpCodes.Call;i.Operand=hook("LoadSprite");sprites++;}}
    }
    if(name=="OneWayDps.dll")Before(Find(game,"OneWayDps.DpsOverlay","Boot"),hook("DiagnosticBoot"),false);
    if(name=="Commons.dll"){
     MapReturn(Find(game,"Commons.Localization_Utility","get_localization"),hook("Localize"));
     After(Find(game,"Commons.AudioSystem","Update"),hook("AudioUpdated"));
    }
    if(name=="World.dll"){
     After(Find(game,"World.WorldRawImage","LateUpdate"),hook("FitPreview"));
     After(Find(game,"World.Devices.Equip.EquipmentMgrView","OnEnable"),hook("FitPreview"));
     After(Find(game,"World.Devices.DeviceUiViews.DeviceUiView","attach"),hook("AttachUi"));
     Before(Find(game,"World.SafeArea.SafeAreaView","Init"),hook("EnterRest"),false);
     Before(Find(game,"World.WorldSceneRoot","ExitSafeArea"),hook("LeaveRest"),false);
     Before(Find(game,"World.WorldSceneRoot","on_init"),hook("Begin"),false);
     foreach(var n in new[]{"on_fini","back_campScene","back_initScene","QuitGame","SetSettlement"})Before(Find(game,"World.WorldSceneRoot",n),hook("End"),false);
     var inputMethod=Find(game,"World.Input.WorldInputController","OnLeftClickPerformedHandler");var il=inputMethod.Body.GetILProcessor();var invoke=inputMethod.Body.Instructions.Single(i=>i.Operand is MethodReference && ((MethodReference)i.Operand).FullName=="System.Void System.Action::Invoke()");
     il.InsertBefore(invoke,il.Create(OpCodes.Call,hook("Attack")));Widen(inputMethod);
     foreach(var type in All(module.Types).Where(t=>t.FullName.StartsWith("World.Devices.DeviceUiViews.DevicePanelAttachment_Trigger")))foreach(var m in type.Methods.Where(m=>m.Name=="OnPointerDown" && m.HasBody))Before(m,hook("PanelAttack"),true);
     foreach(var type in All(module.Types).Where(t=>t.FullName=="World.Devices.DeviceViews.DeviceView" || t.FullName=="World.Devices.DeviceViews.DeviceView_Spine"))foreach(var m in type.Methods.Where(m=>m.Name=="attach"))After(m,hook("Attach"));
    }

 }
}