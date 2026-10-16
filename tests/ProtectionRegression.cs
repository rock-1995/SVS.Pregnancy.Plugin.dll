using SVSPregnancy;
using System.Numerics;

internal static class ProtectionRegression
{
    internal static void Run(Action<string, bool> check)
    {
        var t = new TorsoProfile { PelvicFloor=-1, Pubis=-.8f, Navel=0, Ribs=1.5f,
            SampleMin=-1, SampleMax=1.5f, Front=new[]{.5f,.5f,.5f}, Back=new[]{-.5f,-.5f,-.5f}, Width=new[]{.6f,.6f,.6f} };
        var p = new VtxSettings { HeightBoundaryArc=0 };
        check("Zero arc retains physical height", Enumerable.Range(-20,41).All(i=>HeightBoundary.Coordinate(new(.4f,i/10f,.5f),t,p)==i/10f));
        p.HeightBoundaryArc=.8f;
        float center=HeightBoundary.Center(t,p), top=center+.6f, bottom=center-.6f;
        check("Upper boundary arches upward at the center",HeightBoundary.Height(top,0,t,p)>HeightBoundary.Height(top,1,t,p));
        check("Lower boundary dips downward at the center",HeightBoundary.Height(bottom,0,t,p)<HeightBoundary.Height(bottom,1,t,p));
        bool inverse=true,mirror=true;
        foreach(float angle in new[]{-1.5f,-.7f,0,.7f,1.5f})
        foreach(float level in new[]{bottom,center,top}) {
            float y=HeightBoundary.Height(level,angle,t,p);
            inverse &= MathF.Abs(HeightBoundary.Coordinate(y,angle,center,.8f)-level)<1e-6f;
            mirror &= y==HeightBoundary.Height(level,-angle,t,p);
        }
        check("Curved material coordinates are invertible and symmetric",inverse&&mirror);
        foreach(float stage in new[]{0f,.2f,.5f,.8f,1f}) BellyShape.Growth(t,stage,p);
        check("Boundary center stays fixed across pregnancy stages",HeightBoundary.Center(t,p)==center);
        p.HeightBoundaryArc=0;p.ThighGuardSpeed=0;p.InnerThighGuardStrength=0;p.BottomGuardStrength=0;p.LowerBreastGuardWidth=0;
        var v=new[]{new Vector3(0,.5f,.5f),new Vector3(.1f,.5f,.5f),new Vector3(.2f,.5f,.5f)};
        var keep=BodyProtection.Build(v,Array.Empty<int>(),null,null,null,null,t,p,new[]{0f,.3f,1f});
        check("Breast guard continuously follows actual physics weights",MathF.Abs(keep[0]-1)<1e-6f&&MathF.Abs(keep[1]-.7f)<1e-6f&&keep[2]==0);
        p.BreastExclusionEnabled=false;
        check("Breast guard switch restores full belly ownership",BodyProtection.Build(v,Array.Empty<int>(),null,null,null,null,t,p,new[]{0f,.3f,1f}).All(x=>x==1));
        p.BreastExclusionEnabled=true;
        var weld=BodyProtection.Build(v,Array.Empty<int>(),new[]{0,1,1},null,null,null,t,p,new[]{0f,.3f,1f});
        check("Split vertices share full protection without a seam",weld[1]==0&&weld[2]==0);
        p.InnerThighGuardStrength=1;
        check("Thigh bone protection responds below the waist",BodyProtection.ThighRestore(new(.4f,-.5f,.3f),t,.25f,p)==1);
        check("Thigh bone protection leaves upper abdomen free",BodyProtection.ThighRestore(new(.4f,.5f,.3f),t,1,p)==0);
        p.BottomGuardStrength=1;
        check("Pubic skin can be fully protected",BodyProtection.BottomRestore(new(0,-.8f,.3f),t,1,p)==1);
        check("Bottom guard does not cover the upper abdomen",BodyProtection.BottomRestore(new(0,.5f,.3f),t,1,p)==0);
        check("SVS thigh/pubic bones are recognized",BodyProtection.ThighBone("cf_s_thigh01_L")&&BodyProtection.PubicBone("cf_s_kokan"));
    }
}
