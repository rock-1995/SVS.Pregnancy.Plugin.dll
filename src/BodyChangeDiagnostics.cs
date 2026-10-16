using Character;
using UnityEngine;

namespace SVSPregnancy;

// One readback per loaded body/template pair, not per frame. This captures the
// actual native material path without creating materials or changing render state.
internal static class BodyChangeDiagnostics
{
    internal static void Trace(HumanBody body, BodyChangeValues original,
        BodyChangeValues target, BodyChangeValues delta, double progress)
    {
        try
        {
            double amount = BodyChangeMath.ColorProgress(progress, SVSBodyChangeConfig.NippleColorMaximum.Value);
            var legacy = original.Nipple;
            for (int i = 0; i < 4; i++)
                legacy[i] = BodyChangeMath.Add(original.Nipple[i], delta.Nipple[i], amount,
                    SVSBodyChangeConfig.BodyLower.Value, SVSBodyChangeConfig.BodyUpper.Value);
            int property = ChaShader.Body.sNipColorID;
            string Describe(Material material)
            {
                if (material == null) return "missing";
                string color = material.HasProperty(property) ? Format(material.GetColor(property)) : "no-color-property";
                return $"{material.Pointer.ToInt64():X}:{material.name}/{material.shader?.name}={color}";
            }
            var rendered = body.rendBody?.sharedMaterials;
            string renderColors = rendered == null ? "missing" : string.Join("; ", rendered.Select(Describe));
            PregnancyPlugin._instance.Log.LogInfo(
                $"[Body color check] body={body.Pointer.ToInt64():X} progress={amount:F4} " +
                $"original={Format(original.Nipple)} P={Format(delta.NippleTarget)} " +
                $"old-additive={Format(legacy)} applied={Format(target.Nipple)} " +
                $"custom={Describe(body.customMatBody)} rendered=[{renderColors}]");
        }
        catch (Exception ex)
        {
            PregnancyPlugin._instance.Log.LogWarning("[Body color check] Readback unavailable: " + ex.Message);
        }
    }

    private static string Format(Color c) => FormattableString.Invariant($"({c.r:F5},{c.g:F5},{c.b:F5},{c.a:F5})");
}
