using System.Numerics;

namespace SVSPregnancy;

// Regularise the AL attachment field on the rest surface, once per rebuild.
// A displacement threshold can jump from native to virtual within one SVS
// triangle. Filtering only normals cannot repair the resulting posed fold.
internal static class SkinAttachmentField
{
    internal static float[] Smooth(Vector3[] points, int[] triangles,
        float[] source, bool[] excluded, float span, float[] upperBlend)
    {
        int n=points.Length;
        if(n==0 || source.Length!=n || upperBlend.Length!=n || !(span>0) || triangles==null || triangles.Length<3 || !source.Any(v=>v>0))return source;
        // Work on the quotient mesh: UV/normal splits are one material point.
        // The shading weld map uses a fixed millimetre tolerance; that is too
        // coarse for geometry on SVS's smaller rig. Use near-identical positions.
        float precision=1/MathF.Max(span*1e-6f,1e-8f);
        var ids=new Dictionary<(int,int,int),int>();var group=new int[n];
        var positions=new List<Vector3>();var values=new List<float>();var pinned=new List<bool>();var blend=new List<float>();
        for(int i=0;i<n;i++)
        {
            var point=points[i];
            var key=((int)MathF.Round(point.X*precision),(int)MathF.Round(point.Y*precision),(int)MathF.Round(point.Z*precision));
            if(!ids.TryGetValue(key,out int g))
            {g=positions.Count;ids.Add(key,g);positions.Add(points[i]);values.Add(Math.Clamp(source[i],0,1));pinned.Add(false);blend.Add(upperBlend[i]);}
            group[i]=g;
            pinned[g]|=excluded!=null && i<excluded.Length && excluded[i];
        }
        int count=positions.Count;
        var edges=new HashSet<(int,int)>();
        void Edge(int i,int j)
        {
            if(i<0 || j<0 || i>=n || j>=n)return;
            int a=group[i],b=group[j];if(a==b)return;
            edges.Add(a<b?(a,b):(b,a));
        }
        for(int i=0;i+2<triangles.Length;i+=3)
        {Edge(triangles[i],triangles[i+1]);Edge(triangles[i+1],triangles[i+2]);Edge(triangles[i+2],triangles[i]);}
        var links=new List<(int A,int B,float Weight)>();var total=new float[count];var degree=new int[count];
        float minDistance=span*.003f,radius=span*.18f;
        foreach(var (a,b) in edges)
        {
            float distanceSquared=Vector3.DistanceSquared(positions[a],positions[b]);
            float weight=1/MathF.Max(minDistance*minDistance,distanceSquared);
            links.Add((a,b,weight));total[a]+=weight;total[b]+=weight;degree[a]++;degree[b]++;
        }
        var original=values.ToArray();var current=(float[])original.Clone();var next=new float[count];var sums=new float[count];
        var coupling=new float[count];
        for(int i=0;i<count;i++)
        {
            if(pinned[i])current[i]=original[i]=0;
            coupling[i]=radius*radius*total[i]/Math.Max(1,degree[i]);
        }
        // Positive weights form a convex screened diffusion: values stay in
        // [0,1], and the original AL field remains the data term at every node.
        for(int pass=0;pass<96;pass++)
        {
            Array.Clear(sums,0,count);
            foreach(var (a,b,weight) in links){sums[a]+=current[b]*weight;sums[b]+=current[a]*weight;}
            float change=0;
            for(int i=0;i<count;i++)
            {
                float value=pinned[i]?0:total[i]>0?(original[i]+coupling[i]*sums[i]/total[i])/(1+coupling[i]):original[i];
                next[i]=value;change=MathF.Max(change,MathF.Abs(value-current[i]));
            }
            (current,next)=(next,current);
            if(change<1e-5f)break;
        }
        // Compact the diffusion tails with a continuous soft threshold. Keeping
        // tiny weights across the entire upper body would create hundreds of
        // unnecessary fused palette matrices every frame. The threshold is
        // relative, so the user's virtual strength still scales the whole field.
        float peak=original.Length==0?0:original.Max(),tail=peak*.01f;
        var result=new float[n];
        for(int i=0;i<n;i++)
        {
            int g=group[i];float compact=peak>0?MathF.Max(0,current[g]-tail)/(1-.01f):0;
            float value=original[g]+(compact-original[g])*Math.Clamp(blend[g],0,1);
            if(upperBlend[i]<=0 && !pinned[g]){result[i]=source[i];continue;}
            // Discard only negligible diffusion tails (less than 0.001% bone
            // influence), so distant limbs do not acquire virtual palette slots.
            result[i]=pinned[g] || value<1e-5f?0:Math.Clamp(value,0,1);
        }
        return result;
    }
}
