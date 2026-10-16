using System.Text.Json;
using SVSPregnancy;

int checks=0;
void Check(string name,bool ok){if(!ok)throw new Exception(name);checks++;Console.WriteLine("PASS: "+name);}
string dir=BepInEx.Paths.ConfigPath;
Directory.CreateDirectory(dir);
string path=Path.Combine(dir,"SVSPregnancy_belly.json");
try
{
    File.WriteAllText(path,"""{"version":5,"startDay":57,"vtx":{"sagStrength":0.37,"radiusFront":0.83}}""");
    BellyDeformSettings.Load();
    Check("Old sag and unrelated saved settings retained",BellyDeformSettings.Vtx.SagStrength==.37f&&BellyDeformSettings.Vtx.RadiusFront==.83f&&BellyDeformSettings.StartDay==57);
    Check("Missing new sag receives COM3D2 default",BellyDeformSettings.Vtx.BellySag==2.5f);
    var p=BellyDeformSettings.Vtx;
    p.SagStrength=1.31f;p.BellySag=.42f;
    string before=File.ReadAllText(path);
    BellyDeformSettings.SetLive(48,p);
    Check("Live preview uses independent sag controls",BellyDeformSettings.Vtx.SagStrength==1.31f&&BellyDeformSettings.Vtx.BellySag==.42f);
    Check("Live preview does not overwrite saved settings",File.ReadAllText(path)==before);
    BellyDeformSettings.Save(48,p);
    using(var json=JsonDocument.Parse(File.ReadAllText(path)))
    {
        var vtx=json.RootElement.GetProperty("vtx");
        Check("Both values serialize under distinct stable keys",vtx.GetProperty("sagStrength").GetSingle()==1.31f&&vtx.GetProperty("bellySag").GetSingle()==.42f);
    }
    BellyDeformSettings.ResetToDefaults();BellyDeformSettings.Load();
    Check("Both controls survive save/load independently",BellyDeformSettings.Vtx.SagStrength==1.31f&&BellyDeformSettings.Vtx.BellySag==.42f&&BellyDeformSettings.StartDay==48);
    p=BellyDeformSettings.Vtx;p.BellySag=0;BellyDeformSettings.Save(48,p);
    BellyDeformSettings.ResetToDefaults();BellyDeformSettings.Load();
    Check("Explicitly saved new sag zero stays off",BellyDeformSettings.Vtx.BellySag==0&&BellyDeformSettings.Vtx.SagStrength==1.31f);
    BellyDeformSettings.ResetToDefaults();
    Check("Reset preserves r4 legacy default and initializes new sag",BellyDeformSettings.Vtx.SagStrength==1.0625f&&BellyDeformSettings.Vtx.BellySag==2.5f);
    Console.WriteLine($"{checks} settings integration checks passed (production store, isolated temporary config).");
}
finally {File.Delete(path);Directory.Delete(dir);}

namespace BepInEx
{
    public static class Paths {public static readonly string ConfigPath=Path.Combine(AppContext.BaseDirectory,"test-config-"+Guid.NewGuid().ToString("N"));}
}
namespace BepInEx.Logging
{
    public static class Logger {public static LogSource CreateLogSource(string name)=>new();}
    public sealed class LogSource
    {
        public void LogWarning(object value)=>Console.WriteLine(value);
        public void LogError(object value)=>throw new Exception(value.ToString());
    }
}
