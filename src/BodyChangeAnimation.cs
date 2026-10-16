using Character;
using HarmonyLib;
using ILLGames.Rigging;

namespace SVSPregnancy;

// Patch the callers, not HumanBody.GetShapeBodyValue: native IK uses an inlined
// copy of that getter. Leave the game's contact offsets, masks and covers intact.
internal static class BodyChangeAnimation
{
    private static readonly Dictionary<IntPtr, SV.H.Motion.Base.Controller> Controllers = new();
    // Audited against SVS's native implementations. Do not scan all overrides:
    // Standby, Les and Service02/03 include a shared native RET stub. Detouring
    // that address more than once recurses and also intercepts unrelated methods.
    // Caress00/01 StateChanged also share one entry; they only dispatch voice.
    // Generated wrappers also make abstract native methods look concrete.
    private static readonly (Type Type, string Names)[] MotionTargets =
    {
        (typeof(SV.H.Motion.Base.Controller), "Update Dispose"),
        (typeof(SV.H.Motion.Service.Service00.Controller), "Update Dispose StateChanged OnClothesChanged"),
        (typeof(SV.H.Motion.Service.Service01.Controller), "Update Dispose StateChanged OnClothesChanged"),
        (typeof(SV.H.Motion.Service.Service02.Controller), "Update Dispose StateChanged"),
        (typeof(SV.H.Motion.Service.Service03.Controller), "Update Dispose StateChanged"),
        (typeof(SV.H.Motion.Les00.Controller), "Update StateChanged"),
        (typeof(SV.H.Motion.Les01.Controller), "Update"),
        (typeof(SV.H.Motion.Insert.Controller), "Update"),
        (typeof(SV.H.Motion.Insert.Insert00.Controller), "Dispose StateChanged OnClothesChanged"),
        (typeof(SV.H.Motion.Insert.Insert01.Controller), "Dispose StateChanged OnClothesChanged"),
        (typeof(SV.H.Motion.Caress.Controller), "Update"),
        (typeof(SV.H.Motion.Caress.Caress00.Controller), "OnClothesChanged"),
        (typeof(SV.H.Motion.Caress.Caress01.Controller), "OnClothesChanged"),
    };
    internal static System.Reflection.MethodInfo[] GetMotionMethods() => MotionTargets
        .SelectMany(target => target.Names.Split(' ').Select(name =>
            AccessTools.GetDeclaredMethods(target.Type).Single(method => method.Name == name)))
        .ToArray();
    internal static void Install(Harmony harmony)
    {
        foreach (var method in AccessTools.GetDeclaredMethods(typeof(MotionIK)))
        {
            if (method.Name != "GetCorrectShapeValues" && method.Name != "GetCorrectShapeValuesParam2") continue;
            int bodies = method.GetParameters().Count(p => p.ParameterType == typeof(HumanBody));
            if (bodies != 1 && bodies != 2) throw new InvalidOperationException("Unknown native IK shape signature: " + method);
            harmony.Patch(method,
                prefix: new HarmonyMethod(typeof(BodyChangeAnimation), bodies == 1 ? nameof(BeforeSingle) : nameof(BeforePair)),
                finalizer: new HarmonyMethod(typeof(BodyChangeAnimation), nameof(AfterRead)));
        }
        // Scene motion correction also asks its actor for breast/shape values.
        foreach (string name in new[] { "get_Breast" })
            harmony.Patch(AccessTools.Method(typeof(SV.H.HActor), name),
                prefix: new HarmonyMethod(typeof(BodyChangeAnimation), nameof(BeforeActor)),
                finalizer: new HarmonyMethod(typeof(BodyChangeAnimation), nameof(AfterRead)));
        var methods = GetMotionMethods();
        foreach (var method in methods)
        {
            if (method.Name == "OnClothesChanged" || method.Name == "StateChanged")
                harmony.Patch(method,
                    prefix: new HarmonyMethod(typeof(BodyChangeAnimation), nameof(BeforeAnimator)),
                    finalizer: new HarmonyMethod(typeof(BodyChangeAnimation), nameof(AfterAnimator)));
            else if (method.Name == "Update")
                harmony.Patch(method, prefix: new HarmonyMethod(typeof(BodyChangeAnimation), nameof(TrackController)));
            else if (method.Name == "Dispose")
                harmony.Patch(method, prefix: new HarmonyMethod(typeof(BodyChangeAnimation), nameof(ForgetController)));
        }
        PregnancyPlugin._instance.Log.LogInfo("[Body changes] Installed SVS MotionIK and actor reads; 29 audited motion hooks, shared stubs and abstract methods excluded.");
    }

    internal static void TrackController(SV.H.Motion.Base.Controller __instance)
    { if (__instance != null) Controllers[__instance.Pointer] = __instance; }
    internal static void ForgetController(SV.H.Motion.Base.Controller __instance)
    { if (__instance != null) Controllers.Remove(__instance.Pointer); }

    internal sealed class AnimatorReadScope : IDisposable
    {
        private readonly List<ReadScope> _scopes = new();
        internal static AnimatorReadScope Begin(SV.H.Motion.Base.Controller controller)
        {
            var actors = controller?._actors;
            AnimatorReadScope result = null;
            try
            {
                if (actors != null) foreach (var actor in actors)
                {
                    var scope = ReadScope.Begin(actor?.Human?.body, null);
                    if (scope == null) continue;
                    result ??= new AnimatorReadScope();
                    result._scopes.Add(scope);
                }
                return result;
            }
            catch { result?.Dispose(); throw; }
        }
        public void Dispose()
        {
            // Dispose every actor even if a native object was destroyed mid-call.
            Exception failure = null;
            for (int i = _scopes.Count - 1; i >= 0; i--)
                try { _scopes[i].Dispose(); } catch (Exception ex) { failure ??= ex; }
            _scopes.Clear();
            if (failure != null) throw failure;
        }
    }

    // A live F8 progress change can happen after this motion's parameters were
    // initialized. Refresh only the native parameter calculation, once per body
    // change. The next ordinary Animator evaluation consumes the new values.
    internal static bool RefreshAnimatorParameters(Human human)
    {
        var scene = ILLGames.Unity.Component.Singleton<SV.H.HScene>.Instance;
        if (scene == null || scene.isDisposed || !scene.loaded) return false;
        bool refreshed = false;
        foreach (var controller in Controllers.Values.ToArray())
        {
            var actors = controller?._actors;
            if (actors == null || !actors.Any(a => a?.Human?.Pointer == human?.Pointer)) continue;
            // SVS refreshes breast/cover animator parameters through its native
            // clothing-change callback; it has no AL UpdateAnimeParam method.
            using (AnimatorReadScope.Begin(controller)) controller.OnClothesChanged();
            refreshed = true;
        }
        return refreshed;
    }

    internal static void BeforeAnimator(SV.H.Motion.Base.Controller __instance, out AnimatorReadScope __state)
        { TrackController(__instance); __state = AnimatorReadScope.Begin(__instance); }
    internal static Exception AfterAnimator(Exception __exception, AnimatorReadScope __state)
    {
        __state?.Dispose();
        return __exception;
    }

    internal sealed class ReadScope : IDisposable
    {
        private readonly Human _first, _second;
        private BodyChangeRuntime.Scope _firstScope, _secondScope;
        private ReadScope(Human first, BodyChangeRuntime.Scope firstScope, Human second, BodyChangeRuntime.Scope secondScope)
        {
            _first = first; _firstScope = firstScope;
            _second = second; _secondScope = secondScope;
        }
        internal static ReadScope Begin(HumanBody first, HumanBody second)
        {
            var firstHuman = first?.human;
            var secondHuman = second?.human;
            BodyChangeRuntime.Scope firstScope = null, secondScope = null;
            try
            {
                firstScope = BodyChangeRuntime.BeginAnimationRead(first);
                secondScope = BodyChangeRuntime.BeginAnimationRead(second);
                return firstScope == null && secondScope == null ? null : new ReadScope(firstHuman, firstScope, secondHuman, secondScope);
            }
            catch
            {
                try { BodyChangeRuntime.End(secondHuman, secondScope); }
                finally { BodyChangeRuntime.End(firstHuman, firstScope); }
                throw;
            }
        }
        public void Dispose()
        {
            try { BodyChangeRuntime.End(_second, _secondScope); }
            finally
            {
                BodyChangeRuntime.End(_first, _firstScope);
                _firstScope = _secondScope = null;
            }
        }
    }

    internal static void BeforeSingle(HumanBody body, out ReadScope __state) => __state = ReadScope.Begin(body, null);
    internal static void BeforePair(HumanBody body1, HumanBody body2, out ReadScope __state) => __state = ReadScope.Begin(body1, body2);
    internal static void BeforeActor(SV.H.HActor __instance, out ReadScope __state) => __state = ReadScope.Begin(__instance?.Human?.body, null);
    internal static Exception AfterRead(Exception __exception, ReadScope __state)
    {
        __state?.Dispose();
        return __exception;
    }
}
