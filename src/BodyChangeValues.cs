using Character;
using UnityEngine;

namespace SVSPregnancy;

// Only continuous body morph controls and nipple color participate. Face, clothes,
// IDs, names and other card metadata are never transferred to a live character.
internal sealed class BodyChangeValues
{
    internal float[] Shape;
    internal float[] Extra;
    internal Color Nipple;
    internal Color NippleTarget;
    internal static readonly string[] ExtraNames = {
        "bustSoftness", "bustWeight", "areolaSize", "bustSoftness2"
    };

    internal static BodyChangeValues Read(HumanDataBody b) => new()
    {
        Shape = b.shapeValueBody.ToArray(),
        Extra = new[] { b.bustSoftness, b.bustWeight, b.areolaSize, b.bustSoftness2 },
        Nipple = b.nipColor
    };

    internal void Validate()
    {
        if (Shape == null || Shape.Length != (int)ChaFileDefine.BodyShapeIdx.HandW + 1 ||
            Shape.Any(x => !float.IsFinite(x)) || Extra.Any(x => !float.IsFinite(x)) ||
            Enumerable.Range(0, 4).Any(i => !float.IsFinite(Nipple[i])))
            throw new InvalidDataException("Invalid body morph values in template card.");
    }

    internal static BodyChangeValues Difference(BodyChangeValues baseline, BodyChangeValues reference)
    {
        baseline.Validate(); reference.Validate();
        return new BodyChangeValues {
            Shape = reference.Shape.Zip(baseline.Shape, (r, b) => r - b).ToArray(),
            Extra = reference.Extra.Zip(baseline.Extra, (r, b) => r - b).ToArray(),
            Nipple = reference.Nipple - baseline.Nipple,
            NippleTarget = reference.Nipple
        };
    }

    internal BodyChangeValues Apply(BodyChangeValues delta, double progress, int lower, int upper, float colorMaximum)
    {
        var result = new BodyChangeValues {
            Shape = Shape.Select((v, i) => BodyChangeMath.Add(v, delta.Shape[i], progress, lower, upper)).ToArray(),
            Extra = Extra.Select((v, i) => BodyChangeMath.Add(v, delta.Extra[i], progress, lower, upper)).ToArray(),
            Nipple = Nipple
        };
        double colorProgress = BodyChangeMath.ColorProgress(progress, colorMaximum);
        if (delta.Nipple.r != 0 || delta.Nipple.g != 0 || delta.Nipple.b != 0)
            for (int i = 0; i < 3; i++)
                result.Nipple[i] = BodyChangeMath.BlendColor(Nipple[i], delta.NippleTarget[i], colorProgress);
        if (delta.Nipple.a != 0)
            result.Nipple.a = BodyChangeMath.BlendColor(Nipple.a, delta.NippleTarget.a, colorProgress);
        return result;
    }

    internal bool Same(BodyChangeValues other) => other != null &&
        Shape.SequenceEqual(other.Shape) && Extra.SequenceEqual(other.Extra) &&
        Enumerable.Range(0, 4).All(i => Nipple[i] == other.Nipple[i]);

    internal void Write(HumanDataBody b)
    {
        // Own this array even if a future version of the game's Copy becomes shallow.
        b.shapeValueBody = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<float>(Shape);
        b.bustSoftness = Extra[0]; b.bustWeight = Extra[1]; b.areolaSize = Extra[2];
        b.bustSoftness2 = Extra[3]; b.nipColor = Nipple;
    }
}
