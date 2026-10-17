using System.Numerics;
using System.Text.Json;
using SVSPregnancy;

internal static class LowerBoundaryArcRegression
{
    internal static void Run(Action<string,bool> check)
    {
        var t=new TorsoProfile {PelvicFloor=0,Pubis=.1f,Navel=1.1f,Ribs=2.7f,SampleMin=0,SampleMax=3,
            Front=Enumerable.Repeat(.5f,21).ToArray(),Back=Enumerable.Repeat(-.5f,21).ToArray(),Width=Enumerable.Repeat(.9f,21).ToArray()};
        foreach(float old in new[]{0f,.23f,.8f,1.7f})
        {
            var legacy=JsonSerializer.Deserialize<VtxSettings>(FormattableString.Invariant($"{{\"heightBoundaryArc\":{old}}}"))!;
            check($"Old preset arc {old} is inherited by the lower boundary",legacy.LowerHeightBoundaryArc==old);
        }
        var p=new VtxSettings {HeightBoundaryArc=.8f,LowerHeightBoundaryArc=.8f,SagStrength=1.0625f,BellySag=0,NavelEversion=0,NavelProportion=0};
        float center=HeightBoundary.Center(t,p),bottom=center-.5f,top=center+.5f;
        float upper=HeightBoundary.Height(top,1,t,p),lower=HeightBoundary.Height(bottom,1,t,p);
        p.LowerHeightBoundaryArc=0;
        check("Lower arc zero gives a straight lower boundary",HeightBoundary.Height(bottom,1,t,p)==bottom);
        check("Changing lower curvature leaves upper contours unchanged",HeightBoundary.Height(top,1,t,p)==upper);
        p.LowerHeightBoundaryArc=.8f;p.HeightBoundaryArc=0;
        check("Upper arc zero gives a straight upper boundary",HeightBoundary.Height(top,1,t,p)==top);
        check("Changing upper curvature leaves lower contours unchanged",HeightBoundary.Height(bottom,1,t,p)==lower);
        var roundtrip=JsonSerializer.Deserialize<VtxSettings>(JsonSerializer.Serialize(p))!;
        check("Both boundary curvatures survive JSON independently",roundtrip.HeightBoundaryArc==0&&roundtrip.LowerHeightBoundaryArc==.8f);
        var reordered=JsonSerializer.Deserialize<VtxSettings>("{\"lowerHeightBoundaryArc\":0,\"heightBoundaryArc\":1.2}")!;
        check("Explicit lower zero survives either JSON property order",reordered.LowerHeightBoundaryArc==0&&reordered.HeightBoundaryArc==1.2f);

        foreach(var pair in new[]{(Upper:0f,Lower:.8f),(.8f,0f),(.8f,1f),(.2f,2f)})
        {
            p.HeightBoundaryArc=pair.Item1;p.LowerHeightBoundaryArc=pair.Item2;
            bool inverse=true,monotonic=true,symmetric=true,joined=true;
            foreach(float angle in new[]{-1.4f,-.7f,0,.7f,1.4f})
            {
                float previous=float.NegativeInfinity;
                for(int i=-100;i<=100;i++)
                {
                    float level=center+i*.01f;
                    float height=HeightBoundary.Height(level,angle,t,p);
                    inverse &= MathF.Abs(HeightBoundary.Coordinate(height,angle,center,p.HeightBoundaryArc,p.LowerHeightBoundaryArc)-level)<1e-5f;
                    monotonic &= height>previous;previous=height;
                    symmetric &= height==HeightBoundary.Height(level,-angle,t,p);
                }
                joined &= HeightBoundary.Height(center,angle,t,p)==center &&
                    MathF.Abs(HeightBoundary.Height(center-1e-5f,angle,t,p)-center)<1.1e-5f &&
                    MathF.Abs(HeightBoundary.Height(center+1e-5f,angle,t,p)-center)<1.1e-5f;
            }
            check($"Split arcs {pair}: invertible, ordered, symmetric and joined",inverse&&monotonic&&symmetric&&joined);
            foreach(float stage in new[]{.4f,1f})
            foreach(float sag in new[]{-1.0625f,1.0625f})
            {
                p.SagStrength=sag;bool finite=true,ordered=true;
                foreach(float x in new[]{-.5f,0,.5f})
                {
                    float previous=float.NegativeInfinity;
                    for(int row=0;row<=300;row++)
                    {
                        var original=new Vector3(x,.01f+row*.009f,.5f);
                        var changed=BellyShape.Deform(original,t,stage,p);
                        finite &= float.IsFinite(changed.X+changed.Y+changed.Z+BellyShape.ClothingFootprint(original,t,stage,p));
                        ordered &= changed.Y>previous;previous=changed.Y;
                    }
                }
                check($"Split arcs {pair}, stage {stage}, slide {sag}: finite skin/cloth and no reversed vertical rows",finite&&ordered);
            }
        }
        p.HeightBoundaryArc=.8f;p.LowerHeightBoundaryArc=0;p.SagStrength=1.0625f;
        var sample=new Vector3(.4f,.6f,.5f);
        var before=BellyShape.Deform(sample,t,1,p);
        int builds=BellyShape.FieldBuildCount;
        p.LowerHeightBoundaryArc=1;
        var after=BellyShape.Deform(sample,t,1,p);
        check("Changing only lower curvature rebuilds the cached shape once",BellyShape.FieldBuildCount==builds+1);
        check("Lower curvature changes the deformed lower surface",Vector3.Distance(before,after)>1e-4f);
        for(int i=0;i<1000;i++)BellyShape.Deform(sample,t,1,p);
        check("Repeated split-arc sampling reuses the field",BellyShape.FieldBuildCount==builds+1);
    }
}
