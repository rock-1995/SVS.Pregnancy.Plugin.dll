using SVSPregnancy;
using Character;
using UnityEngine;

int passed=0;
void Check(string label,bool success) { if(!success)throw new Exception("FAIL: "+label);passed++;Console.WriteLine("PASS: "+label); }
bool Near(double a,double b)=>Math.Abs(a-b)<.000001;
Check("first pregnancy halfway is 0.1 at x=5",Near(BodyChangeMath.Progress(true,.5,0,5),.1));
Check("two births plus half pregnancy is 0.5",Near(BodyChangeMath.Progress(true,.5,2,5),.5));
Check("postpartum retains two fifths",Near(BodyChangeMath.Progress(false,0,2,5),.4));
Check("five births gives exactly full delta",BodyChangeMath.Progress(false,0,5,5)==1);
Check("more births and pregnancy do not exceed full delta",BodyChangeMath.Progress(true,.9,20,5)==1);
Check("x=0 and negative x never divide by zero",BodyChangeMath.Progress(true,0,0,0)==1&&BodyChangeMath.Progress(false,0,0,-5)==1);
Check("int maximum birth count saturates without overflow",BodyChangeMath.Progress(true,1,int.MaxValue,int.MaxValue)==1);
Check("int maximum x preserves a small fractional change",BodyChangeMath.Progress(true,1,0,int.MaxValue)>0);
Check("upper clamp applies after additive change",Near(BodyChangeMath.Add(.9f,.4f,.5,0,100),1));
Check("lower clamp applies after negative change",BodyChangeMath.Add(.2f,-.5f,1,0,100)==0);
Check("negative and above-100 limits are respected",Near(BodyChangeMath.Add(.9f,.4f,1,-200,300),1.3)&&Near(BodyChangeMath.Add(.2f,-.5f,1,-200,300),-.3));
Check("reversed bounds use ordered range",Near(BodyChangeMath.Add(.9f,.4f,1,150,-50),1.3));
Check("unaffected values outside range remain untouched",BodyChangeMath.Add(2,0,1,0,100)==2);
Check("zero progress does not clamp originals",BodyChangeMath.Add(-1,1,0,0,100)==-1);
Check("color caps rather than scales early progress",Near(BodyChangeMath.ColorProgress(.5,.85f),.5));
Check("color caps full progress at 0.85",Near(BodyChangeMath.ColorProgress(1,.85f),.85));
Check("color maximum zero turns color change off",BodyChangeMath.ColorProgress(1,0)==0);

var baseline=BodyChangeValues.Read(HumanData.LoadFixture("o"));
var reference=BodyChangeValues.Read(HumanData.LoadFixture("p"));
var delta=BodyChangeValues.Difference(baseline,reference);
Check("actual cards have 30 changed shape sliders",delta.Shape.Count(x=>x!=0)==30);
Check("actual cards have four changed extra morph controls",delta.Extra.Count(x=>x!=0)==4);
Check("actual card color difference is negative RGB with unchanged alpha",delta.Nipple.r<0&&delta.Nipple.g<0&&delta.Nipple.b<0&&delta.Nipple.a==0);
var complete=baseline.Apply(delta,1,0,150,1);
Check("base plus full difference reconstructs T body and color",complete.Shape.Zip(reference.Shape,(a,b)=>Near(a,b)).All(x=>x)&&complete.Extra.Zip(reference.Extra,(a,b)=>Near(a,b)).All(x=>x));
var capped=baseline.Apply(delta,1,0,100,.85f);
Check("default completed color retains 15 percent of base",Near(capped.Nipple.r,baseline.Nipple.r+delta.Nipple.r*.85)&&Near(capped.Nipple.g,baseline.Nipple.g+delta.Nipple.g*.85)&&Near(capped.Nipple.b,baseline.Nipple.b+delta.Nipple.b*.85));
Check("color cap does not reduce full body changes",capped.Shape.Zip(reference.Shape,(a,b)=>Near(a,Math.Clamp(b,0,1))).All(x=>x)&&capped.Extra.Zip(reference.Extra,(a,b)=>Near(a,Math.Clamp(b,0,1))).All(x=>x));
var changedBody=HumanData.LoadFixture("o");changedBody.shapeValueBody[3]=.4f;
var different=BodyChangeValues.Read(changedBody).Apply(delta,.5,0,100,.85f);
Check("each character receives an additive delta from its own baseline",Near(different.Shape[3],.4+delta.Shape[3]*.5));
Check("face and unrelated body IDs are not part of transfer",changedBody.skinId==17&&different.Shape[0]==baseline.Shape[0]);

// Reproduce the reported hue inversion using a pink character with the actual O/P pair.
var pink=BodyChangeValues.Read(HumanData.LoadFixture("o"));
pink.Nipple=new UnityEngine.Color(.9f,.7f,.65f,.8f);
var dark=pink.Apply(delta,1,-500,500,.85f);
Check("black P scales all channels without green inversion",Near(dark.Nipple.r,.9*.15)&&Near(dark.Nipple.g,.7*.15)&&Near(dark.Nipple.b,.65*.15)&&dark.Nipple.r>dark.Nipple.g&&dark.Nipple.g>dark.Nipple.b);
Check("color preserves alpha when template alpha is unchanged",dark.Nipple.a==.8f);
var black=pink.Apply(delta,1,0,100,1);
Check("full color progress reaches actual black P",black.Nipple.r==0&&black.Nipple.g==0&&black.Nipple.b==0);
var brownCard=BodyChangeValues.Read(HumanData.LoadFixture("p"));brownCard.Nipple=new UnityEngine.Color(.3f,.2f,.2f,1);
var brownDelta=BodyChangeValues.Difference(baseline,brownCard);
var brown=pink.Apply(brownDelta,1,0,100,1);
Check("nonblack replacement P is the color endpoint",Near(brown.Nipple.r,.3)&&Near(brown.Nipple.g,.2)&&Near(brown.Nipple.b,.2));
var neutral=BodyChangeValues.Difference(baseline,baseline);
Check("identical template colors leave each character's own color untouched",pink.Apply(neutral,1,0,100,1).Nipple==pink.Nipple);

Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory,"SVSPregnancyTemplates"));
// Run production adapter with managed native API doubles; no game process/files.
File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"SVSPregnancyTemplates","o.png"),"native-loader fixture");
File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"SVSPregnancyTemplates","p.png"),"native-loader fixture");
var human=new Human();human.data.Custom.Body=HumanData.LoadFixture("o");
var originalBody=human.data.Custom.Body;
var originalArray=originalBody.shapeValueBody;
var pregnancy=new PregnancyCharaController.PregnancyInfo {IsPregnant=true,Progress=.5};
void Tick()
{
    human.body.LateUpdate();
    BodyChangeRuntime.ApplyAfterBelly(human,pregnancy);
}
Tick();var first=human.body.Applied;
Check("post-belly native shape commit observes changed values",Near(first.Shape[3],baseline.Shape[3]+delta.Shape[3]*.1));
Check("original body and array references restored after tick",ReferenceEquals(originalBody,human.data.Custom.Body)&&ReferenceEquals(originalArray,originalBody.shapeValueBody));
Check("original data contents preserved despite shallow native Copy",BodyChangeValues.Read(originalBody).Same(baseline));
Check("all four native auxiliary setters read the temporary body",human.body.AppliedExtras.SequenceEqual(first.Extra));
Check("native color setter reads the temporary color",human.body.AppliedNipple==first.Nipple);
Check("first post-belly commit applies body values and shape exactly once",human.body.ValueUpdates==1&&human.body.ShapeUpdates==1);

int initialExtras=human.body.ExtraCalls;
int initialCopies=HumanDataBody.Copies;
int initialArrays=Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<float>.Allocations;
for(int i=0;i<100;i++)Tick();
Check("one hundred frames never accumulate the offset",human.body.Applied.Same(first));

Check("one hundred stable frames do not force native shape updates",human.body.ValueUpdates==1&&human.body.ShapeUpdates==1);
Check("one hundred stable frames do not call native auxiliary setters",human.body.ExtraCalls==initialExtras);
Check("one hundred stable frames do not copy native custom body data",HumanDataBody.Copies==initialCopies);
Check("one hundred stable frames do not allocate native shape arrays",Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<float>.Allocations==initialArrays);
human.body.SetUpdateShapeBodyValue();human.body.SetUpdateShapeBody();Tick();
Check("game-requested base refresh is followed by target once after belly",human.body.Applied.Same(first)&&human.body.ValueUpdates==3&&human.body.ShapeUpdates==3);
originalBody.skinId=23;
var liveUnrelated=BodyChangeRuntime.BeginAfterBelly(human,pregnancy);
Check("temporary body preserves current unrelated data",human.data.Custom.Body.skinId==23);
BodyChangeRuntime.End(human,liveUnrelated);
BodyChangeRuntime.NativeShapeUpdated(human.body);
var outer=BodyChangeRuntime.BeginAfterBelly(human,pregnancy);
var nested=BodyChangeRuntime.BeginAfterBelly(human,pregnancy);
Check("reentrant native call does not add twice",nested==null&&BodyChangeValues.Read(human.data.Custom.Body).Same(first));
BodyChangeRuntime.End(human,nested);BodyChangeRuntime.End(human,outer);
Check("outer native scope survives nested finalizer",ReferenceEquals(human.data.Custom.Body,originalBody));

pregnancy.Progress=1;Tick();var beforeBirth=human.body.Applied;
pregnancy.IsPregnant=false;pregnancy.Progress=0;pregnancy._births=1;Tick();
Check("birth preserves body change continuously",human.body.Applied.Same(beforeBirth));
pregnancy._births=5;Tick();Check("five births keep full morphology postpartum",human.body.Applied.Same(capped));
SVSBodyChangeConfig.BodyBirths.Value=10;Tick();
Check("live x changes recalculate using original data",human.body.Applied.Same(baseline.Apply(delta,.5,0,100,.85f)));
SVSBodyChangeConfig.NippleColorMaximum.Value=0;Tick();
Check("live zero color cap restores original color only",human.body.Applied.Nipple==baseline.Nipple&&human.body.Applied.Shape[3]!=baseline.Shape[3]);
SVSBodyChangeConfig.NippleColorMaximum.Value=.85f;SVSBodyChangeConfig.BodyBirths.Value=5;
SVSBodyChangeConfig.BodyLower.Value=-50;SVSBodyChangeConfig.BodyUpper.Value=150;originalBody.shapeValueBody[3]=1.2f;Tick();
Check("changed live source baseline and extended bounds are respected",Near(human.body.Applied.Shape[3],Math.Min(1.5,1.2+delta.Shape[3])));
PregnancyPlugin.ConfigEnable.Value=false;Tick();
Check("disabling restores original rendered values",human.body.Applied.Same(BodyChangeValues.Read(originalBody)));
Check("disabling restores all native auxiliary values and color",human.body.AppliedExtras.SequenceEqual(BodyChangeValues.Read(originalBody).Extra)&&human.body.AppliedNipple==originalBody.nipColor);
PregnancyPlugin.ConfigEnable.Value=true;Tick();
BodyChangeRuntime.ReleaseAll();
Check("world change restores original rendered values",human.body.Applied.Same(BodyChangeValues.Read(originalBody)));
Tick();BodyChangeRuntime.Release(human);human.body=new HumanBody(human);Tick();
Check("body reload reapplies color and auxiliary settings",human.body.ExtraCalls==5);

var anim=BodyChangeRuntime.BeginAnimationRead(human.body);
Check("Animation reads committed visual body",anim!=null && !ReferenceEquals(human.data.Custom.Body,originalBody));
var nestedAnim=BodyChangeRuntime.BeginAnimationRead(human.body);
BodyChangeRuntime.End(human,nestedAnim);
Check("Nested animation read preserves outer scope",nestedAnim==null && !ReferenceEquals(human.data.Custom.Body,originalBody));
BodyChangeRuntime.End(human,anim);
Check("Animation restores original body after read",ReferenceEquals(human.data.Custom.Body,originalBody));
SVSBodyChangeConfig.Preview=true;Tick();
Check("Manual preview restores original body",human.body.Applied.Same(BodyChangeValues.Read(originalBody)));
SVSBodyChangeConfig.Preview=false;Tick();

AnimationContactRegression.Run(Check);
BodyChangeRuntime.ReleaseAll();human.body.ThrowExtra=true;
try {BodyChangeRuntime.BeginAfterBelly(human,pregnancy);throw new Exception("expected failure");}catch(InvalidOperationException){}
Check("native preparation failure cannot leak temporary data",ReferenceEquals(human.data.Custom.Body,originalBody));
human.body.ThrowExtra=false;
var failed=BodyChangeRuntime.BeginAfterBelly(human,pregnancy);
try {throw new InvalidOperationException("native LateUpdate failure");}catch(InvalidOperationException){}
finally {BodyChangeRuntime.End(human,failed);BodyChangeRuntime.End(human,failed);}
Check("native failure restoration is idempotent",ReferenceEquals(human.data.Custom.Body,originalBody));
BodyChangeRuntime.ReleaseAll();
Tick();File.Delete(Path.Combine(AppContext.BaseDirectory,"SVSPregnancyTemplates","p.png"));Time.unscaledTime=3;
Tick();Check("missing replacement card restores original instead of stale changes",human.body.Applied.Same(BodyChangeValues.Read(originalBody)));
Console.WriteLine($"Body change regression: {passed} checks passed.");
