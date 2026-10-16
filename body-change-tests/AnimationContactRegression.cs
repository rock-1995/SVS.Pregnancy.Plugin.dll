using Character;
using HarmonyLib;
namespace SVSPregnancy;

internal static class AnimationContactRegression
{
    internal static void Run(Action<string,bool> check)
    {
        var first=new Human();first.data.Custom.Body=HumanData.LoadFixture("o");
        var second=new Human();second.data.Custom.Body=HumanData.LoadFixture("o");
        second.data.Custom.Body.shapeValueBody[3]=.3f;
        var savedFirst=first.data.Custom.Body;var savedSecond=second.data.Custom.Body;
        var original=BodyChangeValues.Read(savedFirst);
        var firstArray=savedFirst.shapeValueBody;
        var pregnant=new PregnancyCharaController.PregnancyInfo{IsPregnant=true,Progress=.7};
        var postpartum=new PregnancyCharaController.PregnancyInfo{_births=3};
        BodyChangeRuntime.ApplyAfterBelly(first,pregnant);
        BodyChangeRuntime.ApplyAfterBelly(second,postpartum);
        float target=first.body.Applied.Shape[3],targetSecond=second.body.Applied.Shape[3];
        // Native correction interpolates authored min/medium/max contact offsets.
        float Contact(float shape)=>shape<.5f ? -.08f+shape*2*.08f : (shape-.5f)*2*.13f;
        var actor=new SV.H.HActor{Human=first};
        check("old card-only IK path disagrees with the rendered chest",Math.Abs(Contact(actor.Breast)-Contact(target))>1e-6);
        int updates=first.body.ShapeUpdates+second.body.ShapeUpdates;
        int values=first.body.ValueUpdates+second.body.ValueUpdates;
        int extras=first.body.ExtraCalls+second.body.ExtraCalls;
        BodyChangeAnimation.BeforeSingle(first.body,out var one);
        check("hand contact uses the committed visual chest size",Contact(actor.Breast)==Contact(target));
        check("all native IK morph inputs match the committed visual body",BodyChangeValues.Read(first.data.Custom.Body).Same(first.body.Applied));
        check("IK read leaves original card contents untouched",BodyChangeValues.Read(savedFirst).Same(original)&&ReferenceEquals(firstArray,savedFirst.shapeValueBody));
        BodyChangeAnimation.BeforePair(first.body,second.body,out var nested);
        check("nested two-actor IK sees both current bodies",actor.Breast==target&&second.data.Custom.Body.shapeValueBody[3]==targetSecond);
        BodyChangeAnimation.AfterRead(null,nested);
        check("nested finalizer preserves outer scope and restores partner",actor.Breast==target&&ReferenceEquals(second.data.Custom.Body,savedSecond));
        var failure=new InvalidOperationException("native contact failure");
        check("IK exception propagates without losing card restoration",ReferenceEquals(BodyChangeAnimation.AfterRead(failure,one),failure)&&ReferenceEquals(first.data.Custom.Body,savedFirst));
        BodyChangeAnimation.AfterRead(null,one);
        check("IK finalizer is idempotent",ReferenceEquals(first.data.Custom.Body,savedFirst));
        BodyChangeAnimation.BeforePair(first.body,first.body,out var duplicate);
        check("same actor on both sides never applies body delta twice",actor.Breast==target);
        BodyChangeAnimation.AfterRead(null,duplicate);
        check("same-actor pair restores original reference",ReferenceEquals(first.data.Custom.Body,savedFirst));
        BodyChangeAnimation.BeforeActor(actor,out var sceneRead);
        check("scene breast and shape reads use current visual parameters",actor.Breast==target&&actor.GetShape(3)==target);
        BodyChangeAnimation.AfterRead(null,sceneRead);
        check("IK reads do not run shape updates or physics setters",updates==first.body.ShapeUpdates+second.body.ShapeUpdates&&values==first.body.ValueUpdates+second.body.ValueUpdates&&extras==first.body.ExtraCalls+second.body.ExtraCalls);
        check("animation leaves no pending body rebuild",!first.body._updateShapeBody&&!first.body._updateShapeBodyValue);

        BodyChangeRuntime.NativeShapeUpdated(first.body); // Force a real visual commit.
        var own=BodyChangeRuntime.BeginAfterBelly(first,pregnant);
        BodyChangeAnimation.BeforeSingle(first.body,out var duringCommit);
        BodyChangeAnimation.AfterRead(null,duringCommit);
        check("IK called inside visual commit preserves the outer body scope",actor.Breast==target);
        BodyChangeRuntime.End(first,own);

        foreach(var flag in new[]{"disabled","preview","reload","disposed","inactive","lowpoly","male"})
        {
            PregnancyPlugin.ConfigEnable.Value=flag!="disabled";
            SVSBodyChangeConfig.Preview=flag=="preview";
            first.isReloading=flag=="reload";first.disposed=flag=="disposed";
            first.gameObject.activeInHierarchy=flag!="inactive";first.hiPoly=flag!="lowpoly";first.sex=flag=="male"?0:1;
            BodyChangeAnimation.BeforeSingle(first.body,out var skipped);
            check("IK scope skips "+flag,ReferenceEquals(first.data.Custom.Body,savedFirst));
            BodyChangeAnimation.AfterRead(null,skipped);
        }
        PregnancyPlugin.ConfigEnable.Value=true;SVSBodyChangeConfig.Preview=false;
        first.isReloading=first.disposed=false;first.gameObject.activeInHierarchy=first.hiPoly=true;first.sex=1;
        var stale=first.body;first.body=new HumanBody(first);
        BodyChangeAnimation.BeforeSingle(stale,out var staleRead);
        check("stale skeleton cannot activate old IK body",ReferenceEquals(first.data.Custom.Body,savedFirst));
        BodyChangeAnimation.AfterRead(null,staleRead);first.body=stale;

        BodyChangeAnimation.BeforeSingle(first.body,out var reload);
        var replacement=HumanData.LoadFixture("p");first.data.Custom.Body=replacement;
        BodyChangeAnimation.AfterRead(null,reload);
        check("native body replacement during IK survives finalization",ReferenceEquals(first.data.Custom.Body,replacement));
        first.data.Custom.Body=savedFirst;
        BodyChangeRuntime.Release(first);BodyChangeRuntime.Release(second);
        BodyChangeAnimation.BeforePair(first.body,second.body,out var released);
        check("released actors cannot use stale visual IK parameters",ReferenceEquals(first.data.Custom.Body,savedFirst)&&ReferenceEquals(second.data.Custom.Body,savedSecond));
        BodyChangeAnimation.AfterRead(null,released);

        var harmony=new Harmony();BodyChangeAnimation.Install(harmony);
        var motionMethods=BodyChangeAnimation.GetMotionMethods();
        check("Audited native motion registrations cover 29 distinct methods",motionMethods.Length==29&&motionMethods.Distinct().Count()==29);
        check("Native abstract methods are not hooked",!motionMethods.Any(m=>m.DeclaringType==typeof(SV.H.Motion.Base.Controller)&&m.Name is "StateChanged" or "OnClothesChanged"));
        check("Shared native empty callbacks are not hooked",!motionMethods.Any(m=>m.DeclaringType==typeof(SV.H.Motion.Standby.Controller)||m.DeclaringType==typeof(SV.H.Motion.Les.Controller)||(m.Name=="OnClothesChanged"&&(m.DeclaringType==typeof(SV.H.Motion.Service.Service02.Controller)||m.DeclaringType==typeof(SV.H.Motion.Service.Service03.Controller)))||(m.Name=="StateChanged"&&m.DeclaringType==typeof(SV.H.Motion.Les01.Controller))));
        check("All seven native IK signatures are scoped",harmony.Patches.Count(p=>p.Method.Name.StartsWith("GetCorrectShape"))==7);
        foreach(var patch in harmony.Patches)
        {
            var names=patch.Method.GetParameters().Select(p=>p.Name).ToHashSet();
            check("prefix argument mapping: "+patch.Method,patch.Prefix.Method.GetParameters().All(p=>p.Name.StartsWith("__")||names.Contains(p.Name)));
            if(patch.Method.Name=="Update"||patch.Method.Name=="Dispose")continue;
            string finalizer=patch.Method.Name=="OnClothesChanged"||patch.Method.Name=="StateChanged"?nameof(BodyChangeAnimation.AfterAnimator):nameof(BodyChangeAnimation.AfterRead);
            check("exception finalizer installed: "+patch.Method,patch.Finalizer?.Method?.Name==finalizer);
        }

        var male=new Human {sex=0};
        var native=new SV.H.Motion.Caress.Caress00.Controller {_actors=new[]{new SV.H.HActor{Human=male},new SV.H.HActor{Human=first}}};
        ILLGames.Unity.Component.Singleton<SV.H.HScene>.Instance=new();
        BodyChangeAnimation.TrackController(native);
        BodyChangeRuntime.ApplyAfterBelly(first,pregnant);
        check("Native cover refresh reads committed chest for both actors",native.BreastParameters.All(x=>x==first.body.Applied.Shape[3]));
        check("Native cover refresh restores original card",ReferenceEquals(first.data.Custom.Body,savedFirst));
        BodyChangeAnimation.BeforeAnimator(native,out var animatorScope);
        var nativeFailure=new InvalidOperationException("native failure");
        check("Animator finalizer preserves native exception",ReferenceEquals(BodyChangeAnimation.AfterAnimator(nativeFailure,animatorScope),nativeFailure));
        check("Animator failure restores both actors",ReferenceEquals(first.data.Custom.Body,savedFirst));
        BodyChangeAnimation.ForgetController(native);
        check("Disposed motion controller is no longer refreshed",!BodyChangeAnimation.RefreshAnimatorParameters(first));
        ILLGames.Unity.Component.Singleton<SV.H.HScene>.Instance=null;
        BodyChangeRuntime.ReleaseAll();

    }
}

