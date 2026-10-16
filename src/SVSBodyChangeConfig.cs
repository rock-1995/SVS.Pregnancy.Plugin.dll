using BepInEx.Configuration;
using Character;
using Il2CppInterop.Runtime;

namespace SVSPregnancy;

internal static class SVSBodyChangeConfig
{
    internal static ConfigEntry<int> BodyBirths, BodyLower, BodyUpper;
    internal static ConfigEntry<float> NippleColorMaximum;

    internal static void Bind(ConfigFile config)
    {
        const string section = "Pregnancy body changes";
        BodyBirths = config.Bind(section, "Births for full change", 5,
            "Body change = (p.png - o.png) * min(1, (births + pregnancy progress) / this number). Postpartum changes persist. Values <= 0 apply the full difference immediately. Templates are in plugins/SVSPregnancyTemplates beside the DLL.");
        BodyLower = config.Bind(section, "Minimum slider value", 0,
            "Lower limit in character creator units (100 = normalized 1). Applies only to changed channels. Negative values allowed; reversed bounds are ordered for calculation.");
        BodyUpper = config.Bind(section, "Maximum slider value", 100,
            "Upper limit in character creator units. Values above 100 are allowed. Adds the template difference to each character's original body.");
        NippleColorMaximum = config.Bind(section, "Nipple color progress maximum", .45070422f,
            new ConfigDescription("Blend each character's original nipple color toward p.png. Color progress is capped separately from body progress; a black target darkens without changing hue. 0 disables color changes.", new AcceptableValueRange<float>(0, 1)));
    }

    internal static PregnancyHumanController Controller(Human human)
    {
        if (human?.gameObject == null) return null;
        return human.gameObject.GetComponent(Il2CppType.Of<PregnancyHumanController>())?.TryCast<PregnancyHumanController>();
    }

    internal static bool ManualPreviewActive(Human human)
    {
        if (BellyVertexMorph.Paused) return true;
        if (!BellyVertexMorph.ForceApplyEnabled) return false;
        return BellyVertexMorph.ForceApplyCharaId < 0 ||
            Controller(human)?._charaId == BellyVertexMorph.ForceApplyCharaId;
    }

    internal static bool HasContribution(PregnancyCharaController.PregnancyInfo info) =>
        info != null && BodyChangeMath.Progress(info.IsPregnant, info.Progress, info._births, BodyBirths.Value) > 0;
}
