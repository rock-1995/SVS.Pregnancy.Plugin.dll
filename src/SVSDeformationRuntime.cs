using Character;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace SVSPregnancy;

// SVS owns pregnancy/rate selection; AL owns shape, attachment and skinning.
// Only this adapter schedules that work and owns its private mesh copies.
internal static class SVSDeformationRuntime
{
    private sealed class Visual
    {
        internal Human Human;
        internal int CharaId;
        internal float Rate;
        internal MeshLease Lease;
        internal bool Failed;
    }

    private static readonly Dictionary<IntPtr, Visual> Visuals = new();
    private static readonly Dictionary<IntPtr, string> Errors = new();
    private static bool _installed;

    internal static void Install(Harmony harmony)
    {
        MorphReadiness.Install(harmony);
        harmony.Patch(AccessTools.Method(typeof(Human), "LateUpdate"),
            postfix: new HarmonyMethod(typeof(SVSDeformationRuntime), nameof(AfterHumanLateUpdate)) { priority = Priority.Last });
        harmony.Patch(AccessTools.Method(typeof(Human), "Dispose"),
            prefix: new HarmonyMethod(typeof(SVSDeformationRuntime), nameof(BeforeHumanDispose)));
        _installed = true;
        PregnancyPlugin._instance.Log.LogInfo("[AL morph port] AL 0.2.26 geometry and virtual skinning; native SVS Human.LateUpdate completion.");
    }

    internal static void Request(Human human, int charaId, float rate)
    {
        if (!_installed || human == null || human.disposed || !float.IsFinite(rate)) return;
        if (rate <= 0) { ForgetHuman(human); return; }
        if (!Visuals.TryGetValue(human.Pointer, out var visual))
            Visuals[human.Pointer] = visual = new Visual { Human = human, CharaId = charaId };
        if (visual.CharaId != charaId)
        {
            ReleaseVisual(human);
            visual.CharaId = charaId;
        }
        float next = Mathf.Clamp01(rate);
        if (visual.Rate != next) visual.Failed = false;
        visual.Rate = next;
    }

    private static void AfterHumanLateUpdate(Human __instance)
    {
        if (__instance == null) return;
        try
        {
            if (__instance.disposed) { ForgetHuman(__instance); return; }
            if (PregnancyPlugin.ConfigEnable?.Value != true || BellyVertexMorph.Paused)
            { ReleaseVisual(__instance); return; }
            if (__instance.sex != 1 || __instance.gameObject == null || !__instance.gameObject.activeInHierarchy) return;
            // Rate selection only queues work. Skip all hierarchy and mesh work
            // for actors with no requested deformation or a latched setup failure.
            var component = __instance.gameObject.GetComponent(Il2CppType.Of<PregnancyHumanController>());
            if (component != null) component.TryCast<PregnancyHumanController>()?.AfterNativeUpdate();
            if (!Visuals.TryGetValue(__instance.Pointer, out var visual) || visual.Failed) return;
            if (!MorphReadiness.EnterNativeBoundary(__instance)) return;
            try
            {
                var observations = MorphReadiness.CurrentMeshes(__instance);
                if (visual.Lease != null)
                {
                    var changes = visual.Lease.CheckChanges(observations);
                    if (changes.Mesh) ReleaseVisual(__instance);
                    else if (changes.Visibility) BellyVertexMorph.Invalidate(visual.CharaId);
                }
                if (visual.Lease == null)
                {
                    visual.Lease = new MeshLease();
                    visual.Lease.Acquire(__instance, observations);
                }
                BellyVertexMorph.ApplyCore(__instance, visual.CharaId, visual.Rate);
                BellyVertexMorph.UpdateVirtual(__instance);
                Errors.Remove(__instance.Pointer);
            }
            finally { MorphReadiness.ExitNativeBoundary(); }
        }
        catch (Exception ex)
        {
            ReleaseVisual(__instance);
            if (Visuals.TryGetValue(__instance.Pointer, out var failed)) failed.Failed = true;
            if (!Errors.TryGetValue(__instance.Pointer, out var old) || old != ex.Message)
                PregnancyPlugin._instance.Log.LogError("[AL morph port] Native update failed; original mesh and weights restored: " + ex);
            Errors[__instance.Pointer] = ex.Message;
        }
    }

    private static void BeforeHumanDispose(Human __instance) => ForgetHuman(__instance);

    // Keep the requested rate through outfit/mesh reloads; the next completed
    // native update captures a fresh baseline before rebuilding the AL shape.
    internal static void ReleaseVisual(Human human)
    {
        if (human == null) return;
        if (!Visuals.TryGetValue(human.Pointer, out var visual)) return;
        BellyVertexMorph.ForgetHumanCore(human);
        visual.Lease?.Dispose();
        visual.Lease = null;
        visual.Failed = false;
    }

    internal static void RetryFailed(int? charaId = null)
    {
        foreach (var visual in Visuals.Values)
            if (charaId == null || visual.CharaId == charaId) visual.Failed = false;
    }

    internal static string FailureStatus(int charaId)
    {
        var visual = Visuals.Values.FirstOrDefault(v => v.Failed && (charaId < 0 || v.CharaId == charaId));
        if (visual == null) return null;
        return "Setup stopped: " + Errors.GetValueOrDefault(visual.Human.Pointer) + " Use Rebuild shape after correcting the character.";
    }

    internal static void ForgetHuman(Human human)
    {
        if (human == null) return;
        ReleaseVisual(human);
        Visuals.Remove(human.Pointer);
        Errors.Remove(human.Pointer);
        MorphReadiness.Forget(human);
    }

    internal static void ReleaseChara(int charaId)
    {
        foreach (var visual in Visuals.Values.Where(v => v.CharaId == charaId).ToArray())
            ForgetHuman(visual.Human);
    }

    internal static void ReleaseAll()
    {
        foreach (var visual in Visuals.Values.ToArray()) ForgetHuman(visual.Human);
        BodyMeshSelection.Clear();
    }
}
