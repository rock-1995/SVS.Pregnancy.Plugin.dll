using SVSPregnancy;
using System.Numerics;
using System.Text.Json;

internal static class SvsAssetRegression
{
    internal static void Landmarks(Action<string,bool> check)
    {
        var bones=new Dictionary<string,Vector3>{["cf_j_kokan"]=new(0,.83f,0),["cf_s_waist01"]=new(0,1.01f,0),["cf_s_spine03"]=new(0,1.21f,0)};
        var result=TorsoLandmarks.Read(bones);
        check("SVS cf_j_kokan supplies the pelvic landmark absent in AL naming",result.Floor==bones["cf_j_kokan"]);
        bones["cf_s_kokanskin"]=new(0,.84f,0);
        check("AL pelvic landmark retains priority when present",TorsoLandmarks.Read(bones).Floor==bones["cf_s_kokanskin"]);
        bones.Remove("cf_j_kokan");bones.Remove("cf_s_kokanskin");bool rejected=false;
        try{TorsoLandmarks.Read(bones);}catch(InvalidOperationException ex){rejected=ex.Message.Contains("pelvis");}
        check("Missing pelvis reports the exact unsupported landmark",rejected);
        bones["cf_j_kokan"]=new(0,1.5f,0);rejected=false;
        try{TorsoLandmarks.Read(bones);}catch(InvalidOperationException){rejected=true;}
        check("Inverted anatomical landmark order is rejected",rejected);
    }

    // Input is freshly extracted from the installed SVS asset, not an invented
    // AL torso. The game mesh is kept outside the redistributable source ZIP.
    internal static void Run(string path,Action<string,bool> check)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(path));
        Console.WriteLine("SVS asset SHA256: "+doc.RootElement.GetProperty("sha256").GetString());
        foreach(var mesh in doc.RootElement.GetProperty("meshes").EnumerateArray())
        {
            var names=mesh.GetProperty("bones").EnumerateArray().Select(x=>x.GetString()).ToArray();
            var binds=mesh.GetProperty("bindposes").EnumerateArray().Select(ReadUnityMatrix).ToArray();
            var rest=binds.Select(b=>{if(!Matrix4x4.Invert(b,out var r))throw new Exception("Singular asset bindpose");return r.Translation;}).ToArray();
            var lookup=names.Select((name,i)=>(name,i)).ToDictionary(x=>x.name,x=>rest[x.i]);
            check("Actual SVS body reproduces the missing AL pelvic name",!lookup.ContainsKey("cf_s_kokanskin")&&lookup.ContainsKey("cf_j_kokan"));
            check("Actual SVS body uses 103 source bones",names.Length==103);
            var center=lookup["cf_s_waist01"];var up=Vector3.Normalize(lookup["cf_s_spine01"]-center);
            check("Actual SVS spine01 reproduces the reversed short-bone axis",Vector3.Dot(up,lookup["cf_s_spine03"]-center)<0);
            up=TorsoLandmarks.OrientUp(up,center,lookup["cf_s_spine03"]);
            check("SVS frame points toward the upper torso",Vector3.Dot(up,Vector3.UnitY)>.98f);
            if(Vector3.Dot(up,Vector3.UnitY)>.98f)up=Vector3.UnitY;
            var right=lookup["cf_s_thigh01_R"]-lookup["cf_s_thigh01_L"];right=Vector3.Normalize(right-up*Vector3.Dot(right,up));
            var fwd=Vector3.Normalize(Vector3.Cross(up,right));
            if(Vector3.Dot(fwd,Vector3.UnitZ)<0){fwd=-fwd;right=Vector3.Normalize(Vector3.Cross(up,fwd));}
            Vector3 Local(Vector3 v){var d=v-center;return new(Vector3.Dot(d,right),Vector3.Dot(d,up),Vector3.Dot(d,fwd));}
            var points=mesh.GetProperty("vertices").EnumerateArray().Select(x=>Local(ReadVector(x))).ToArray();
            var (floor,waist,chest)=TorsoLandmarks.Read(lookup.ToDictionary(x=>x.Key,x=>Local(x.Value)));
            var torso=TorsoProfile.Build(points,floor.Y,waist.Y,chest.Y);var settings=new VtxSettings();
            check("Actual SVS rest landmarks calibrate a valid torso",torso.Span>0&&float.IsFinite(torso.Span));
            check("Actual SVS body has the 10597 vertices reported in failed game log",points.Length==10597);
            foreach(float stage in new[]{0f,.25f,.5f,1f})
            {
                var targets=points.Select(v=>BellyShape.Deform(v,torso,stage,settings)).ToArray();
                var delta=points.Select((v,i)=>Vector3.Distance(v,targets[i])).ToArray();
                check($"SVS asset stage {stage}: no NaN or infinity",targets.All(v=>float.IsFinite(v.X+v.Y+v.Z)));
                check($"SVS asset stage {stage}: expected nonzero geometry",stage==0?delta.All(x=>x==0):delta.Count(x=>x>1e-6f)>100);
                var alpha=points.Select((v,i)=>VirtualAxisMath.MaterialSurfaceWeight(delta[i],v,torso,stage,settings)).ToArray();
                check($"SVS asset stage {stage}: bounded virtual weights",alpha.All(x=>float.IsFinite(x)&&x>=0&&x<=1));
                if(stage>=.5f)check($"SVS asset stage {stage}: vertices enter virtual binding",alpha.Count(x=>x>1e-6f)>100);
                Console.WriteLine($"SVS ASSET stage={stage:F2} vertices={points.Length} moved={delta.Count(x=>x>1e-6f)} maxDelta={delta.Max():F6} virtual={alpha.Count(x=>x>1e-6f)} floor={floor.Y:F6} waist={waist.Y:F6} chest={chest.Y:F6}");
            }
        }
    }
    private static Vector3 ReadVector(JsonElement x)=>new(x[0].GetSingle(),x[1].GetSingle(),x[2].GetSingle());
    private static Matrix4x4 ReadUnityMatrix(JsonElement m)=>new(m[0].GetSingle(),m[4].GetSingle(),m[8].GetSingle(),m[12].GetSingle(),m[1].GetSingle(),m[5].GetSingle(),m[9].GetSingle(),m[13].GetSingle(),m[2].GetSingle(),m[6].GetSingle(),m[10].GetSingle(),m[14].GetSingle(),m[3].GetSingle(),m[7].GetSingle(),m[11].GetSingle(),m[15].GetSingle());
}
