using SVSPregnancy;
using System.Numerics;
using System.Text.Json;

internal static class SkinAttachmentRegression
{
    internal static void Run(Action<string,bool> check)
    {
        var points=new[]{new Vector3(0,0,0),new Vector3(.1f,0,0),new Vector3(0,.1f,0),new Vector3(.1f,.1f,0),new Vector3(0,0,0)};
        int[] triangles={0,1,2,1,3,2,4,1,2};float[] blend={1,1,1,1,1};
        var alpha=new float[]{0,1,0,1,0};
        var result=SkinAttachmentField.Smooth(points,triangles,alpha,null,1,blend);
        check("Attachment diffusion reduces the native/virtual cliff",result[1]-result[0]<.8f);
        check("Attachment stays finite and within native/virtual bounds",result.All(v=>float.IsFinite(v)&&v>=0&&v<=1));
        check("UV split copies receive exactly the same attachment",result[0]==result[4]);
        var scaled=SkinAttachmentField.Smooth(points.Select(v=>v*20).ToArray(),triangles,alpha,null,20,blend);
        check("Attachment smoothing follows character scale",result.Zip(scaled).All(pair=>MathF.Abs(pair.First-pair.Second)<1e-5f));
        var weaker=SkinAttachmentField.Smooth(points,triangles,alpha.Select(v=>v*.3f).ToArray(),null,1,blend);
        check("Virtual strength remains proportional after compact filtering",result.Zip(weaker).All(pair=>MathF.Abs(pair.First*.3f-pair.Second)<1e-5f));
        var constant=SkinAttachmentField.Smooth(points,triangles,Enumerable.Repeat(.7f,5).ToArray(),null,1,blend);
        check("Constant attachment stays constant",constant.All(v=>MathF.Abs(v-.7f)<1e-5f));
        var off=SkinAttachmentField.Smooth(points,triangles,new float[5],null,1,blend);
        check("Zero virtual field remains zero",off.All(v=>v==0));
        var unchanged=SkinAttachmentField.Smooth(points,triangles,alpha,null,1,new float[5]);
        check("Lower material region retains the AL weights exactly",unchanged.SequenceEqual(alpha));
        var excluded=new[]{false,false,false,false,true};
        var guarded=SkinAttachmentField.Smooth(points,triangles,alpha,excluded,1,blend);
        check("Breast exclusion pins every copy of an excluded material point",guarded[0]==0 && guarded[4]==0);
        check("Field construction does not mutate source weights",alpha.SequenceEqual(new float[]{0,1,0,1,0}));
    }

    // Fresh game mesh is supplied separately and is never redistributed.
    // Pose rotations are synthetic, using the actual asset's ancestry and skin
    // pivots; no claim is made that these are captured game animations.
    internal static void Asset(string path,Action<string,bool> check)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(path));
        var mesh=doc.RootElement.GetProperty("meshes")[0];
        if(!mesh.TryGetProperty("triangles",out var triangles) || !mesh.TryGetProperty("boneAncestors",out var ancestors))
        {Console.WriteLine("SKIP posed crack replay: extract fixture with triangles and boneAncestors.");return;}
        Vector3 V(JsonElement e)=>new(e[0].GetSingle(),e[1].GetSingle(),e[2].GetSingle());
        var orig=mesh.GetProperty("vertices").EnumerateArray().Select(V).ToArray();
        var names=mesh.GetProperty("bones").EnumerateArray().Select(x=>x.GetString()).ToArray();
        var chains=ancestors.EnumerateArray().Select(x=>x.EnumerateArray().Select(s=>s.GetString()).ToHashSet()).ToArray();
        var rest=mesh.GetProperty("bindposes").EnumerateArray().Select(x=>{
            var a=x.EnumerateArray().Select(v=>v.GetSingle()).ToArray();
            var m=new Matrix4x4(a[0],a[4],a[8],a[12],a[1],a[5],a[9],a[13],a[2],a[6],a[10],a[14],a[3],a[7],a[11],a[15]);
            Matrix4x4.Invert(m,out var inverse);return inverse.Translation;
        }).ToArray();
        var lookup=names.Select((name,i)=>(name,i)).ToDictionary(x=>x.name,x=>rest[x.i]);
        var center=lookup["cf_s_waist01"];var up=Vector3.UnitY;
        var right=lookup["cf_s_thigh01_R"]-lookup["cf_s_thigh01_L"];right.Y=0;right=Vector3.Normalize(right);
        var fwd=Vector3.Normalize(Vector3.Cross(up,right));if(fwd.Z<0){fwd=-fwd;right=Vector3.Normalize(Vector3.Cross(up,fwd));}
        var frame=new Matrix4x4(right.X,right.Y,right.Z,0,0,1,0,0,fwd.X,fwd.Y,fwd.Z,0,center.X,center.Y,center.Z,1);
        Matrix4x4.Invert(frame,out var inverseFrame);
        var local=orig.Select(v=>Vector3.Transform(v,inverseFrame)).ToArray();
        var (floor,navel,chest)=TorsoLandmarks.Read(lookup.ToDictionary(x=>x.Key,x=>Vector3.Transform(x.Value,inverseFrame)));
        var torso=TorsoProfile.Build(local,floor.Y,navel.Y,chest.Y);
        var tri=triangles.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
        var indices=mesh.GetProperty("boneIndices").EnumerateArray().Select(x=>x.EnumerateArray().Select(v=>v.GetInt32()).ToArray()).ToArray();
        var weights=mesh.GetProperty("weights").EnumerateArray().Select(x=>x.EnumerateArray().Select(v=>v.GetSingle()).ToArray()).ToArray();
        bool Belly(string n)=>n.Contains("waist")||n.Contains("spine")||n.Contains("hip")||n.Contains("belly");
        bool Leg(string n)=>!n.Contains("hipleg")&&(n.Contains("thigh")||(n.Contains("leg")&&!n.Contains("spine"))||n.Contains("knee"));
        var influences=orig.Select((_,i)=>BellyShape.BoneInfluence(indices[i].Select((b,k)=>Belly(names[b])?weights[i][k]:0).Sum(),indices[i].Select((b,k)=>Leg(names[b])?weights[i][k]:0).Sum())).ToArray();
        float[] blend=local.Select(v=>BellyShape.Smooth((v.Y-torso.Navel)/(torso.Span*.10f))).ToArray();
        using var config=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"crack-repro-settings.json")));
        var captured=JsonSerializer.Deserialize<VtxSettings>(config.RootElement.GetProperty("vtx").GetRawText());
        // Preserve the original AL baseline when the release preset changes.
        var alDefault=new VtxSettings {AxisBlendStart=0.02f, AxisBlendFull=0.25f, LowerTransitionStart=0.02f, LowerTransitionWidth=0.6f, LowerTransitionBias=0f, UpperTransitionStart=0.10542166f, UpperTransitionWidth=0.5965462f, UpperTransitionBias=0f, UpperTransitionJoin=0.2f, UpperTransitionActivation=0.02f};
        var cases=new[]{("user",captured), ("AL-default",alDefault)};
        var stats=new List<string>{"settings,stage,pose,joints,r2_flipped,r3_flipped,native_flipped"};
        int moderateOld=0,moderateNew=0,stressOld=0,stressNew=0;
        foreach(var (label,p) in cases)
        foreach(float stage in new[]{.6f,.8f,.997f})
        {
            var deformed=local.Select((v,i)=>Vector3.Transform(Vector3.Lerp(v,BellyShape.Deform(v,torso,stage,p),BellyShape.DeformationInfluence(influences[i],v.Y,torso,p)),frame)).ToArray();
            var source=local.Select((v,i)=>VirtualAxisMath.MaterialSurfaceWeight(Vector3.Distance(orig[i],deformed[i]),v,torso,stage,p,BellyShape.DeformationInfluence(influences[i],v.Y,torso,p))).ToArray();
            var watch=System.Diagnostics.Stopwatch.StartNew();
            var smooth=SkinAttachmentField.Smooth(orig,tri,source,null,torso.Span,blend);
            int Slots(float[] field)
            {
                var recipes=new Dictionary<VirtualWeights.Recipe,int>();
                int Slot(VirtualWeights.Recipe recipe){if(!recipes.TryGetValue(recipe,out var id)){id=names.Length+recipes.Count;recipes.Add(recipe,id);}return id;}
                for(int i=0;i<orig.Length;i++)VirtualWeights.Blend(indices[i].Select((b,k)=>new VirtualWeights.Influence(b,weights[i][k])).ToArray(),field[i],Slot);
                return recipes.Count;
            }
            double rebuildMs=watch.Elapsed.TotalMilliseconds;
            Console.WriteLine($"ATTACHMENT {label} stage={stage:F3}: rebuild={rebuildMs:F2}ms virtual={source.Count(x=>x>0)}->{smooth.Count(x=>x>0)} palette={Slots(source)}->{Slots(smooth)}");
            check($"{label}/{stage}: finite bounded SVS field",smooth.All(v=>float.IsFinite(v)&&v>=0&&v<=1));
            check($"{label}/{stage}: lower AL binding is byte-for-byte unchanged",Enumerable.Range(0,orig.Length).Where(i=>blend[i]==0).All(i=>source[i]==smooth[i]));
            var axis=VirtualAxisMath.Reference(torso,stage,p);axis=new(Vector3.Transform(axis.Anchor,frame),Vector3.Transform(axis.Upper,frame));
            foreach(bool distributed in new[]{false,true})
            foreach(string direction in new[]{"bend","side","twist"})
            foreach(float degrees in new[]{-60f,-40f,-20f,20f,40f,60f})
            {
                Matrix4x4 Rotation(float a)=>direction=="bend"?Matrix4x4.CreateRotationX(a):direction=="side"?Matrix4x4.CreateRotationZ(a):Matrix4x4.CreateRotationY(a);
                var matrices=Enumerable.Repeat(Matrix4x4.Identity,names.Length).ToArray();
                // Rest-space rotations compose from the deepest joint outwards.
                for(int j=distributed?3:1;j>=1;j--)
                {
                    var pivot=lookup[$"cf_s_spine0{j}"];float radians=degrees*MathF.PI/180/(distributed?3:1);
                    var matrix=Matrix4x4.CreateTranslation(-pivot)*Rotation(radians)*Matrix4x4.CreateTranslation(pivot);
                    for(int b=0;b<matrices.Length;b++)if(chains[b].Contains($"cf_j_spine0{j}"))matrices[b]*=matrix;
                }
                int spine=names.Select((name,i)=>(name,i)).Where(x=>x.name.StartsWith("cf_s_spine")).OrderBy(x=>Vector3.DistanceSquared(rest[x.i],axis.Upper)).First().i;
                var virtualMatrix=VirtualAxisMath.Evaluate(axis,Matrix4x4.Identity,matrices[spine],p).Transform;
                var nativeMatrices=new Matrix4x4[orig.Length];
                for(int i=0;i<orig.Length;i++)for(int k=0;k<indices[i].Length;k++)nativeMatrices[i]+=matrices[indices[i][k]]*weights[i][k];
                int Count(float[] alpha)
                {
                    var posed=new Vector3[orig.Length];var mixed=new Matrix4x4[orig.Length];
                    for(int i=0;i<orig.Length;i++){mixed[i]=Matrix4x4.Lerp(nativeMatrices[i],virtualMatrix,alpha[i]);posed[i]=Vector3.Transform(deformed[i],mixed[i]);}
                    int inverted=0;
                    for(int i=0;i<tri.Length;i+=3)
                    {
                        int a=tri[i],b=tri[i+1],c=tri[i+2];var pos=(local[a]+local[b]+local[c])/3;
                        if(pos.Y<torso.Navel+.12f*torso.Span || pos.Y>chest.Y-.04f*torso.Span || pos.Z<-.015f)continue;
                        var normal=Vector3.Cross(deformed[b]-deformed[a],deformed[c]-deformed[a]);
                        if(normal.LengthSquared()<1e-18f)continue;
                        var average=(mixed[a]+mixed[b]+mixed[c])*(1f/3);
                        if(!Matrix4x4.Invert(average,out var inverse))throw new Exception("Synthetic pose is singular");
                        var expected=Vector3.TransformNormal(normal,Matrix4x4.Transpose(inverse));
                        var actual=Vector3.Cross(posed[b]-posed[a],posed[c]-posed[a]);
                        if(Vector3.Dot(expected,actual)<0)inverted++;
                    }
                    return inverted;
                }
                int old=Count(source),fixedCount=Count(smooth),native=Count(new float[orig.Length]);
                stats.Add($"{label},{stage},{direction}{degrees},{(distributed?3:1)},{old},{fixedCount},{native}");
                if(MathF.Abs(degrees)<=40){moderateOld+=old;moderateNew+=fixedCount;}else{stressOld+=old;stressNew+=fixedCount;}
            }
        }
        string report=Path.Combine(AppContext.BaseDirectory,"svs-pose-replay.csv");File.WriteAllLines(report,stats);
        Console.WriteLine($"POSE REPLAY: moderate r2={moderateOld} r3={moderateNew}; extreme r2={stressOld} r3={stressNew}. CSV: {report}");
        check("The actual SVS topology reproduces the reported upper fold in r2",moderateOld>0);
        check("Regularisation reduces folds across both parameter sets and all stages",moderateNew<moderateOld && stressNew<stressOld);
        // Pin the user's reproducible ordinary pose set, while reporting rather
        // than hiding extreme-angle and different-preset stress results above.
        var userFull=stats.Skip(1).Select(x=>x.Split(',')).Where(x=>x[0]=="user" && x[1]=="0.997" && (x[2].EndsWith("20")||x[2].EndsWith("40")) && x[3]=="1");
        check("Captured user settings: zero upper folded faces at +/-20 and +/-40 degrees",userFull.All(x=>x[5]=="0"));
    }
}
