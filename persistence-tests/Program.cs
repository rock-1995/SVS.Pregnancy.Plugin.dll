using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

if(args.Length!=2)throw new ArgumentException("Pass the built plugin DLL and the SVS game directory.");
var folders=new[]{Path.GetDirectoryName(Path.GetFullPath(args[0])),Path.Combine(args[1],"BepInEx","core"),Path.Combine(args[1],"BepInEx","SamabakeScramble","interop")};
AssemblyLoadContext.Default.Resolving+=(_,name)=>{
    foreach(var folder in folders) {var path=Path.Combine(folder,name.Name+".dll");if(File.Exists(path))return AssemblyLoadContext.Default.LoadFromAssemblyPath(path);}
    return null;
};
var assembly=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[0]));
var entry=assembly.GetType("SVSPregnancy.PregnancyPersistenceManager+PregEntry",true);
int count=0;
void Check(string message,bool ok){if(!ok)throw new Exception(message);count++;Console.WriteLine("PASS: "+message);}
var old=JsonSerializer.Deserialize("{\"charaId\":2,\"day\":80,\"maxDays\":280,\"cooldown\":0}",entry);
Check("Compiled save DTO loads old saves with zero births",(int)entry.GetProperty("Births").GetValue(old)==0);
entry.GetProperty("Births").SetValue(old,4);
string serialized=JsonSerializer.Serialize(old,entry);
Check("Compiled save DTO writes stable births key",serialized.Contains("\"births\":4"));
var restored=JsonSerializer.Deserialize(serialized,entry);
Check("Birth count survives serialized round trip",(int)entry.GetProperty("Births").GetValue(restored)==4);
Check("Existing pregnancy day survives with new count",(int)entry.GetProperty("Day").GetValue(restored)==80);
var info=assembly.GetType("SVSPregnancy.PregnancyCharaController+PregnancyInfo",true);
var state=Activator.CreateInstance(info,new object[]{1});
info.GetField("_births").SetValue(state,3);
info.GetField("_day").SetValue(state,140);
Check("Compiled progress uses normalized SVS pregnancy days",(double)info.GetProperty("Progress").GetValue(state)==.5);
info.GetMethod("Reset",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(state,null);
Check("Clearing pregnancy preserves accumulated births",(int)info.GetField("_births").GetValue(state)==3&&(int)info.GetField("_day").GetValue(state)==-1);
Check("Postpartum current-pregnancy contribution is zero",(double)info.GetProperty("Progress").GetValue(state)==0);
Console.WriteLine($"{count} compiled persistence checks passed; no native game calls.");
