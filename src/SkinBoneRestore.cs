namespace SVSPregnancy;

// During an SVS outfit/skeleton rebuild the renderer can already hold a new
// native prefix, or a completely new bone array. Remove only our own suffix.
internal static class SkinBoneRestore
{
    internal static T[] Restore<T>(T[] original, T[] installed, T[] current, Func<T,T,bool> same)
    {
        if (current == null || installed == null || original == null || current.Length != installed.Length)
            return current;
        for (int i = original.Length; i < installed.Length; i++)
            if (!same(current[i], installed[i])) return current;
        bool unchanged = true;
        for (int i = 0; i < original.Length; i++)
            if (!same(current[i], original[i])) { unchanged = false; break; }
        if (unchanged) return original;
        var native = new T[original.Length];
        Array.Copy(current, native, native.Length);
        return native;
    }
}
