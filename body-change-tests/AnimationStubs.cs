// Keep body arguments in their native positions/names: Harmony maps prefixes by
// name, including the bool-leading overload and the two-actor correction paths.
namespace ILLGames.Rigging
{
    using Character;
    public static class MotionIK
    {
        public static void GetCorrectShapeValues(bool isBlend, HumanBody body, int[] shapes) { }
        public static void GetCorrectShapeValues(HumanBody body, float keyFrame, int[] calcBlend) { }
        public static void GetCorrectShapeValuesParam2(HumanBody body1, HumanBody body2, float keyFrame, int[] calcBlend) { }
        public static void GetCorrectShapeValues(HumanBody body1, HumanBody body2, float keyFrame, object info, int mode, ref float ret) { }
        public static void GetCorrectShapeValues(HumanBody body1, HumanBody body2, float keyFrame, int[] calcBlend) { }
        public static void GetCorrectShapeValues(HumanBody body, float keyFrame, object calcBlend) { }
        public static void GetCorrectShapeValues(HumanBody body1, HumanBody body2, float keyFrame, object calcBlend) { }
    }
}
namespace SV.H
{
    public sealed class HScene
    {

        public bool isDisposed, loaded=true;
        public Motion.Base.Controller Controller;
    }
    public sealed class HActor
    {
        public Character.Human Human;
        public float Breast => GetShape(3);
        public float GetShape(int index) => Human.data.Custom.Body.shapeValueBody[index];
    }
}

namespace SV.H.Motion.Base
{
    public class Controller : Character.NativeObject
    {
        public virtual void Update() {}
        public virtual void Dispose() {}
        public virtual void StateChanged(object state) {}
        public HActor[] _actors;
        public int Updates;
        public bool ThrowAfterRead;
        public float[] BreastParameters;
        public virtual void OnClothesChanged()
        {
            Updates++;
            // Model the native path found in Caress00: direct body read, then
            // write the female's chest size to BOTH participant animators.
            // Deliberately bypass HActor.Breast/GetShape and all MotionIK hooks.
            float breast=_actors[1].Human.data.Custom.Body.shapeValueBody[3];
            BreastParameters=new[]{breast,breast};
            if(ThrowAfterRead)throw new InvalidOperationException("native animator parameter failure");
        }
    }
}
namespace SV.H.Motion.Service.Service00 { public class Controller : Base.Controller { public override void Update()=>base.Update(); public override void Dispose()=>base.Dispose(); public override void StateChanged(object state)=>base.StateChanged(state); public override void OnClothesChanged()=>base.OnClothesChanged(); } }
namespace SV.H.Motion.Service.Service01 { public class Controller : Base.Controller { public override void Update()=>base.Update(); public override void Dispose()=>base.Dispose(); public override void StateChanged(object state)=>base.StateChanged(state); public override void OnClothesChanged()=>base.OnClothesChanged(); } }
namespace SV.H.Motion.Service.Service02 { public class Controller : Base.Controller { public override void Update()=>base.Update(); public override void Dispose()=>base.Dispose(); public override void StateChanged(object state)=>base.StateChanged(state); public override void OnClothesChanged()=>base.OnClothesChanged(); } }
namespace SV.H.Motion.Service.Service03 { public class Controller : Base.Controller { public override void Update()=>base.Update(); public override void Dispose()=>base.Dispose(); public override void StateChanged(object state)=>base.StateChanged(state); public override void OnClothesChanged()=>base.OnClothesChanged(); } }
namespace SV.H.Motion.Les00 { public class Controller : Base.Controller { public override void Update()=>base.Update(); public override void StateChanged(object state)=>base.StateChanged(state); } }
namespace SV.H.Motion.Les01 { public class Controller : Base.Controller { public override void Update()=>base.Update(); public override void StateChanged(object state)=>base.StateChanged(state); } }
namespace SV.H.Motion.Les { public class Controller : Base.Controller { public override void OnClothesChanged()=>base.OnClothesChanged(); } }
namespace SV.H.Motion.Standby { public class Controller : Base.Controller { public override void Update()=>base.Update(); public override void StateChanged(object state)=>base.StateChanged(state); public override void OnClothesChanged()=>base.OnClothesChanged(); } }
namespace SV.H.Motion.Insert { public class Controller : Base.Controller { public override void Update()=>base.Update(); } }
namespace SV.H.Motion.Insert.Insert00 { public class Controller : Base.Controller { public override void Dispose()=>base.Dispose(); public override void StateChanged(object state)=>base.StateChanged(state); public override void OnClothesChanged()=>base.OnClothesChanged(); } }
namespace SV.H.Motion.Insert.Insert01 { public class Controller : Base.Controller { public override void Dispose()=>base.Dispose(); public override void StateChanged(object state)=>base.StateChanged(state); public override void OnClothesChanged()=>base.OnClothesChanged(); } }
namespace SV.H.Motion.Caress { public class Controller : Base.Controller { public override void Update()=>base.Update(); } }
namespace SV.H.Motion.Caress.Caress00 { public class Controller : Base.Controller { public override void StateChanged(object state)=>base.StateChanged(state); public override void OnClothesChanged()=>base.OnClothesChanged(); } }
namespace SV.H.Motion.Caress.Caress01 { public class Controller : Base.Controller { public override void StateChanged(object state)=>base.StateChanged(state); public override void OnClothesChanged()=>base.OnClothesChanged(); } }

namespace ILLGames.Unity.Component { public static class Singleton<T> { public static T Instance; } }
namespace HarmonyLib {
 public class Harmony {
  public readonly List<(System.Reflection.MethodInfo Method,HarmonyMethod Prefix,HarmonyMethod Finalizer)> Patches=new();
  public void Patch(System.Reflection.MethodInfo method,HarmonyMethod prefix,HarmonyMethod finalizer=null)=>Patches.Add((method,prefix,finalizer));
 }
 public class HarmonyMethod { public System.Reflection.MethodInfo Method; public HarmonyMethod(Type t,string n)=>Method=t.GetMethod(n,System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic); }
 public static class AccessTools {
  public static List<System.Reflection.MethodInfo> GetDeclaredMethods(Type t)=>t.GetMethods(System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.DeclaredOnly).ToList();
  public static System.Reflection.MethodInfo Method(Type t,string n)=>t.GetMethod(n);
 }
}
