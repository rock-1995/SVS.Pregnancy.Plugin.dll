using System.Numerics;

namespace SVSPregnancy;

// Exact nearest vertex lookup in rest space. Stable index tie-breaking preserves
// the previous linear scan's result; no approximation or distance cutoff.
internal sealed class NearestVertexIndex
{
    private readonly Vector3[] _points;
    private readonly Node[] _nodes;
    private readonly int _root;
    private readonly record struct Node(int Point, int Left, int Right, int Axis);
    internal NearestVertexIndex(Vector3[] points)
    {
        _points = points;
        _nodes = new Node[points.Length];
        var order = Enumerable.Range(0, points.Length).ToArray();
        _root = Build(order, 0, order.Length, 0);
    }
    private static float Coordinate(Vector3 p, int axis) => axis == 0 ? p.X : axis == 1 ? p.Y : p.Z;
    private int Build(int[] order, int start, int length, int depth)
    {
        if (length == 0) return -1;
        int axis = depth % 3;
        Array.Sort(order, start, length, Comparer<int>.Create((a, b) =>
        {
            int c = Coordinate(_points[a], axis).CompareTo(Coordinate(_points[b], axis));
            return c == 0 ? a.CompareTo(b) : c;
        }));
        int middle = start + length / 2;
        int left = Build(order, start, middle - start, depth + 1);
        int right = Build(order, middle + 1, start + length - middle - 1, depth + 1);
        _nodes[middle] = new Node(order[middle], left, right, axis);
        return middle;
    }
    internal int Find(Vector3 point) => Find(point, out _);
    internal int Find(Vector3 point, out int visited)
    {
        int best = -1;
        float distance = float.PositiveInfinity;
        visited = 0;
        Search(_root, point, ref best, ref distance, ref visited);
        return best;
    }
    private void Search(int node, Vector3 point, ref int best, ref float distance, ref int visited)
    {
        if (node < 0) return;
        visited++;
        var item = _nodes[node];
        var original = _points[item.Point];
        float x = original.X - point.X, y = original.Y - point.Y, z = original.Z - point.Z;
        float squared = x * x + y * y + z * z;
        if (squared < distance || (squared == distance && (best < 0 || item.Point < best)))
        { distance = squared; best = item.Point; }
        float split = Coordinate(point, item.Axis) - Coordinate(original, item.Axis);
        int near = split <= 0 ? item.Left : item.Right;
        int far = split <= 0 ? item.Right : item.Left;
        Search(near, point, ref best, ref distance, ref visited);
        if (split * split <= distance) Search(far, point, ref best, ref distance, ref visited);
    }
}
