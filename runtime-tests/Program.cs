using SVSPregnancy;
using Character;
using UnityEngine;
using System.Reflection;

try
{
int checks=0;
void Check(string name,bool value){if(!value)throw new Exception("FAIL: "+name);checks++;Console.WriteLine("PASS: "+name);}
Human Create(Mesh shared=null)
{
    var h=new Human();var r=new SkinnedMeshRenderer{sharedMesh=shared??new Mesh(),localBounds=new(){Marker=37}};
    h.gameObject.Renderers=new[]{r};h.body.rendBody=r;return h;
}
void Tick(Human h)=>typeof(SVSDeformationRuntime).GetMethod("AfterHumanLateUpdate",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,new object[]{h});
void Load(Human h,string method)=>typeof(MorphReadiness).GetMethod("BeforeHumanLoad",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,new object[]{h,typeof(Human).GetMethod(method,Type.EmptyTypes)!});
void Dispose(Human h)=>typeof(SVSDeformationRuntime).GetMethod("BeforeHumanDispose",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,new object[]{h});
var harmony=new HarmonyLib.Harmony();SVSDeformationRuntime.Install(harmony);
Check("Native pose completion and disposal hooks installed",harmony.Patches.Any(p=>p.Original.Name=="LateUpdate"&&p.Postfix!=null)&&harmony.Patches.Any(p=>p.Original.Name=="Dispose"&&p.Prefix!=null));
Check("No unsupported by-reference native hook",harmony.Patches.All(p=>p.Original.GetParameters().All(a=>!a.ParameterType.IsByRef)));
var source=new Mesh();var a=Create(source);var b=Create(source);
SVSDeformationRuntime.Request(a,7,.8f);SVSDeformationRuntime.Request(b,7,.4f);
Check("Request does not deform outside native boundary",a.body.rendBody.sharedMesh==source&&BellyVertexMorph.Applies.Count==0);
Tick(a);var cloneA=a.body.rendBody.sharedMesh;
Check("First completed pose uses private mesh",cloneA!=source&&MeshLease.Owns(cloneA)&&BellyVertexMorph.Updates[a.Pointer]==1);
Check("Shared source remains protected and unbound",source.hideFlags.HasFlag(HideFlags.DontUnloadUnusedAsset)&&source.bindposes.Length==2);
Check("Boundary references released after update",!MorphReadiness.TryBeginApply(a));
Tick(b);var cloneB=b.body.rendBody.sharedMesh;
Check("Two humans with same character ID have independent meshes and rates",cloneA!=cloneB&&BellyVertexMorph.LastRate[a.Pointer]==.8f&&BellyVertexMorph.LastRate[b.Pointer]==.4f);
Tick(a);
Check("Unchanged rate keeps mesh and updates pose each native tick",a.body.rendBody.sharedMesh==cloneA&&BellyVertexMorph.Updates[a.Pointer]==2&&a.body.rendBody.bones.Length==3);
var invalidations=BellyVertexMorph.Invalidations;a.body.rendBody.enabled=false;Tick(a);
Check("Visibility invalidates geometry without changing private mesh",BellyVertexMorph.Invalidations==invalidations+1&&a.body.rendBody.sharedMesh==cloneA);
Tick(a);Check("Unchanged visibility does not repeatedly invalidate",BellyVertexMorph.Invalidations==invalidations+1);
a.body.rendBody.enabled=true;Tick(a);
SVSDeformationRuntime.ReleaseVisual(a);
Check("Release restores original mesh and native bones",a.body.rendBody.sharedMesh==source&&a.body.rendBody.bones.Length==2&&cloneA.Destroyed&&!MeshLease.Owns(cloneA));
Check("Second lease keeps shared source protected",source.hideFlags.HasFlag(HideFlags.DontUnloadUnusedAsset));
Tick(a);Check("Requested rate resumes on next completed native update",a.body.rendBody.sharedMesh!=source&&BellyVertexMorph.LastRate[a.Pointer]==.8f);
SVSDeformationRuntime.ReleaseChara(7);
Check("Character reset restores both instances",a.body.rendBody.sharedMesh==source&&b.body.rendBody.sharedMesh==source&&b.body.rendBody.bones.Length==2);
Check("Last lease releases only its own source-protection flag",!source.hideFlags.HasFlag(HideFlags.DontUnloadUnusedAsset));
Tick(a);Check("Reset does not revive previous request",a.body.rendBody.sharedMesh==source);

SVSDeformationRuntime.Request(a,7,.6f);Tick(a);var before=a.body.rendBody.sharedMesh;
Load(a,"ReloadHair");Check("Hair reload does not discard body mesh",a.body.rendBody.sharedMesh==before);
Load(a,"ReloadCoordinate");Check("Coordinate reload restores before native rebuild",a.body.rendBody.sharedMesh==source&&a.body.rendBody.bones.Length==2);
a.isReloading=true;Tick(a);Check("Native reloading actor is not mutated",a.body.rendBody.sharedMesh==source);
a.isReloading=false;Tick(a);Check("Native reload completion rebuilds without a settling delay",a.body.rendBody.sharedMesh!=source);
before=a.body.rendBody.sharedMesh;a.body.rendBody.bones[0]=new Transform();Tick(a);
Check("Native skeleton replacement rebuilds actor lease",before.Destroyed&&a.body.rendBody.sharedMesh!=before);
var stable=a.body.rendBody.sharedMesh;Tick(a);Check("Virtual palette entries do not trigger repeated skeleton rebuilds",a.body.rendBody.sharedMesh==stable);
var replacement=new Mesh();a.body.rendBody.sharedMesh=replacement;Tick(a);
Check("Native mesh replacement becomes new baseline",a.body.rendBody.sharedMesh!=replacement&&MeshLease.OriginalForReadiness(a.body.rendBody.sharedMesh)==replacement);
SVSDeformationRuntime.ReleaseChara(7);Check("Reset restores replacement rather than stale source",a.body.rendBody.sharedMesh==replacement);

var c=Create();var cSource=c.body.rendBody.sharedMesh;c.gameObject.Controller=new(){Callback=()=>SVSDeformationRuntime.Request(c,8,.7f)};
Tick(c);Check("SVS controller selects rate inside completed native callback",BellyVertexMorph.LastRate[c.Pointer]==.7f&&c.body.rendBody.sharedMesh!=cSource);
PregnancyPlugin.ConfigEnable.Value=false;Tick(c);Check("Disabling plugin restores mesh and binding",c.body.rendBody.sharedMesh==cSource&&c.body.rendBody.bones.Length==2);
PregnancyPlugin.ConfigEnable.Value=true;Tick(c);BellyVertexMorph.Paused=true;Tick(c);
Check("Pause restores mesh before skipping updates",c.body.rendBody.sharedMesh==cSource);BellyVertexMorph.Paused=false;
Tick(c);Dispose(c);c.disposed=true;Tick(c);Check("Dispose restores and cannot re-enter morph",c.body.rendBody.sharedMesh==cSource&&c.body.rendBody.bones.Length==2);

var d=Create();var dSource=d.body.rendBody.sharedMesh;SVSDeformationRuntime.Request(d,9,.9f);BellyVertexMorph.FailApply=true;Tick(d);
Check("Partial binding failure restores mesh and bones",d.body.rendBody.sharedMesh==dSource&&d.body.rendBody.bones.Length==2&&!MorphReadiness.TryBeginApply(d));
int errors=PregnancyPlugin._instance.Log.Errors.Count;Tick(d);Check("Repeated identical failure does not flood logs",PregnancyPlugin._instance.Log.Errors.Count==errors);
int failedScans=d.gameObject.Scans,failedApplies=BellyVertexMorph.Applies[d.Pointer];
for(int frame=0;frame<300;frame++){SVSDeformationRuntime.Request(d,9,.9f);Tick(d);}
Check("300 repeated failed frames do not scan, clone or apply again",d.gameObject.Scans==failedScans&&BellyVertexMorph.Applies[d.Pointer]==failedApplies&&d.body.rendBody.sharedMesh==dSource);
Check("Failed setup is visible in the UI status",SVSDeformationRuntime.FailureStatus(9)?.Contains("simulated partial binding failure")==true);
BellyVertexMorph.FailApply=false;SVSDeformationRuntime.RetryFailed(9);Tick(d);Check("Explicit rebuild can recover after failure",d.body.rendBody.sharedMesh!=dSource);
SVSDeformationRuntime.ReleaseVisual(d);BellyVertexMorph.FailApply=true;Tick(d);BellyVertexMorph.FailApply=false;
Load(d,"ReloadCoordinate");Tick(d);Check("Native coordinate reload retries failed setup",d.body.rendBody.sharedMesh!=dSource);
SVSDeformationRuntime.ReleaseAll();Check("Global reset restores all remaining leases",d.body.rendBody.sharedMesh==dSource);
Check("Original local bounds restored",d.body.rendBody.localBounds.Marker==37);
var protectedSource=new Mesh{hideFlags=HideFlags.DontUnloadUnusedAsset};var e=Create(protectedSource);SVSDeformationRuntime.Request(e,10,.5f);Tick(e);SVSDeformationRuntime.ReleaseAll();
Check("Pre-existing source protection survives reset",protectedSource.hideFlags.HasFlag(HideFlags.DontUnloadUnusedAsset));
var originalBones=new[]{new Transform(),new Transform()};
var installedBones=originalBones.Concat(new[]{originalBones[0]}).ToArray();
var replacedBones=new[]{new Transform(),new Transform()};
Check("Complete native bone replacement is preserved",ReferenceEquals(SkinBoneRestore.Restore(originalBones,installedBones,replacedBones,(a,b)=>a==b),replacedBones));
var differentTail=new[]{originalBones[0],originalBones[1],new Transform()};
Check("An unrecognized bone suffix is not removed",ReferenceEquals(SkinBoneRestore.Restore(originalBones,installedBones,differentTail,(a,b)=>a==b),differentTail));
var idle=Create();for(int frame=0;frame<300;frame++)Tick(idle);
Check("300 idle actor updates never scan the mesh hierarchy",idle.gameObject.Scans==0);
Console.WriteLine($"{checks} runtime adapter checks passed (modeled Unity API; no in-game verification).");

}
catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode=1; }
