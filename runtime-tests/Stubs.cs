using Character;
using UnityEngine;
using System.Reflection;

namespace UnityEngine
{
    public class Object
    {
        private static int serial;
        public IntPtr Pointer { get; } = (IntPtr)(++serial);
        public int GetInstanceID() => (int)Pointer;
        public bool Destroyed;
        public T TryCast<T>() where T : class => this as T;
        public static Mesh Instantiate(Mesh m) => new() { name=m.name, vertexCount=m.vertexCount, subMeshCount=m.subMeshCount,
            blendShapeCount=m.blendShapeCount, isReadable=m.isReadable, bindposes=(int[])m.bindposes.Clone() };
        public static void Destroy(Object item) => item.Destroyed = true;
    }
    [Flags] public enum HideFlags { None=0, DontUnloadUnusedAsset=32, DontSave=52 }
    public struct Bounds { public int Marker; }
    public class GameObject : Object
    {
        public bool activeInHierarchy = true;
        public SkinnedMeshRenderer[] Renderers = Array.Empty<SkinnedMeshRenderer>();
        public SVSPregnancy.PregnancyHumanController Controller;
        public int Scans;
        public T[] GetComponentsInChildren<T>(bool all) { Scans++; return Renderers.Cast<T>().ToArray(); }
        public Object GetComponent(Type type) => Controller;
    }
    public class Mesh : Object
    {
        public string name="o_body";
        public HideFlags hideFlags;
        public int vertexCount=100, subMeshCount=1, blendShapeCount;
        public bool isReadable=true;
        public int[] bindposes={10,20};
    }
    public class Transform : Object { }
    public class SkinnedMeshRenderer : Object
    {
        public string name="o_body";
        public Mesh sharedMesh;
        public bool enabled=true;
        public GameObject gameObject=new();
        public Bounds localBounds;
        public Transform[] bones={new(),new()};
    }
    public static class Mathf { public static float Clamp01(float f)=>Math.Clamp(f,0,1); }
}
namespace Character
{
    public class HumanBody { public void UpdateShapeBody() {} public SkinnedMeshRenderer rendBody, rendSimpleBody; }
    public class Human : UnityEngine.Object
    {
        public bool disposed,isReloading;
        public bool hiPoly=true;
        public int sex=1;
        public GameObject gameObject=new();
        public HumanBody body=new();
        public void Load(){} public void Reload(){} public void Reload(ref int value){}
        public void ReloadCoordinate(){} public void ReloadHead(){} public void ReloadHair(){}
        public void ReloadSkin(){} public void ReloadMannequin(){} public void LateUpdate(){} public void Dispose(){}
    }
}
namespace Il2CppInterop.Runtime { public static class Il2CppType { public static Type Of<T>()=>typeof(T); } }
namespace HarmonyLib
{
    public class Harmony
    {
        public readonly List<(MethodInfo Original, HarmonyMethod Prefix, HarmonyMethod Postfix)> Patches=new();
        public void Patch(MethodInfo original,HarmonyMethod prefix=null,HarmonyMethod postfix=null)=>Patches.Add((original,prefix,postfix));
    }
    public class HarmonyMethod
    {
        public int priority; public Type Type; public string Name;
        public HarmonyMethod(Type t,string n){Type=t;Name=n;}
    }
    public static class Priority { public const int Last=0; }
    public static class AccessTools
    {
        public static MethodInfo Method(Type type,string name,Type[] parameters=null)=>parameters==null?type.GetMethod(name):type.GetMethod(name,parameters);
        public static List<MethodInfo> GetDeclaredMethods(Type type)=>type.GetMethods(BindingFlags.DeclaredOnly|BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static).ToList();
    }
}
namespace SVSPregnancy
{
    internal static class BodyMeshSelection
    {
        private static readonly HashSet<int> Registered=new();
        internal static readonly string[] PelvisNames={"waist"},SpineNames={"spine"};
        internal static int FindBone(Transform[] bones,string[] names)=>names==PelvisNames?0:1;
        internal static void Clear()=>Registered.Clear();
        internal static void Register(Human h)
        {
            Registered.Clear();
            if(h.body.rendBody!=null)Registered.Add(h.body.rendBody.GetInstanceID());
            if(h.body.rendSimpleBody!=null)Registered.Add(h.body.rendSimpleBody.GetInstanceID());
        }
        internal static bool IsBodyPiece(SkinnedMeshRenderer r)=>r!=null&&TorsoSelectionPolicy.IsBodyPiece(Registered.Contains(r.GetInstanceID()),r.name,r.sharedMesh?.name);
    }
    internal class Flag { internal bool Value; }
    internal class PregnancyPlugin
    {
        internal static readonly PregnancyPlugin _instance=new();
        internal readonly Log Log=new();
        internal static readonly Flag ConfigEnable=new(){Value=true},ConfigLog=new();
    }
    internal class Log
    {
        internal readonly List<string> Errors=new();
        internal void LogInfo(string s){} internal void LogWarning(string s){}
        internal void LogError(string s)=>Errors.Add(s);
    }
    public class PregnancyHumanController : UnityEngine.Object
    {
        public PregnancyCharaController _charactrl; public Action Callback;
        public void AfterNativeUpdate()=>Callback?.Invoke();
    }
    internal static class BellyVertexMorph
    {
        internal static bool Paused,FailApply;
        internal static readonly Dictionary<IntPtr,int> Applies=new(),Updates=new(),Restores=new();
        internal static readonly Dictionary<IntPtr,float> LastRate=new();
        internal static readonly Dictionary<IntPtr,(SkinnedMeshRenderer Renderer,Mesh Mesh,Transform[] Bones,Transform[] Installed,int[] Binds)> Bindings=new();
        internal static int Invalidations;
        internal static void ApplyCore(Human h,int id,float rate)
        {
            BodyChangeRuntime.Events.Add("belly");
            if(!MorphReadiness.TryBeginApply(h))throw new Exception("Mutation outside native completion");
            var renderer=h.body.rendBody;
            if(!MeshLease.Owns(renderer.sharedMesh))throw new Exception("Mutation of unowned source");
            Applies[h.Pointer]=Applies.GetValueOrDefault(h.Pointer)+1; LastRate[h.Pointer]=rate;
            if(!Bindings.ContainsKey(h.Pointer))
            {
                var installed=renderer.bones.Concat(new[]{renderer.bones[0]}).ToArray();
                Bindings[h.Pointer]=(renderer,renderer.sharedMesh,renderer.bones,installed,renderer.sharedMesh.bindposes);
                renderer.bones=installed;
                renderer.sharedMesh.bindposes=renderer.sharedMesh.bindposes.Concat(new[]{99}).ToArray();
            }
            if(FailApply)throw new Exception("simulated partial binding failure");
        }
        internal static void UpdateVirtual(Human h) { BodyChangeRuntime.Events.Add("virtual"); Updates[h.Pointer]=Updates.GetValueOrDefault(h.Pointer)+1; }
        internal static void ForgetHumanCore(Human h)
        {
            Restores[h.Pointer]=Restores.GetValueOrDefault(h.Pointer)+1;
            if(!Bindings.Remove(h.Pointer,out var binding))return;
            binding.Renderer.bones=SkinBoneRestore.Restore(binding.Bones,binding.Installed,binding.Renderer.bones,(a,b)=>a==b);
            binding.Mesh.bindposes=binding.Binds;
        }
        internal static void Invalidate(int id)=>Invalidations++;
    }
}

namespace SVSPregnancy {
 public class PregnancyCharaController { public PregnancyInfo _pregnancyInfo; public class PregnancyInfo {} }
 internal static class SVSBodyChangeConfig {
  internal static bool Preview,Contribution;
  internal static PregnancyHumanController Controller(Character.Human h)=>h.gameObject.Controller;
  internal static bool ManualPreviewActive(Character.Human h)=>Preview;
  internal static bool HasContribution(PregnancyCharaController.PregnancyInfo i)=>i!=null && Contribution;
 }
 internal static class BodyChangeRuntime {
  internal static List<string> Events=new();
  internal static HashSet<IntPtr> Active=new();
  internal static void NativeShapeUpdated(Character.HumanBody h) {}
  internal static void ApplyAfterBelly(Character.Human h,PregnancyCharaController.PregnancyInfo info) {
   Events.Add("body"); if (SVSBodyChangeConfig.HasContribution(info) && !SVSBodyChangeConfig.Preview) Active.Add(h.Pointer);else Release(h);
  }
  internal static void Release(Character.Human h) { Active.Remove(h.Pointer); }
  internal static void ReleaseAll() { Active.Clear(); }
 }
 internal static class BodyChangeAnimation { internal static void Install(HarmonyLib.Harmony h) {} }
}
