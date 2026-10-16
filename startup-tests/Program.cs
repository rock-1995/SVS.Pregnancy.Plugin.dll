using System.Reflection;
using System.Runtime.Loader;
using System.Reflection.PortableExecutable;
if(args.Length!=2)throw new ArgumentException("Pass plugin DLL and SVS game directory.");
var game=Path.GetFullPath(args[1]);
var interop=Path.Combine(game,"BepInEx","SamabakeScramble","interop");
var folders=new[]{Path.GetDirectoryName(Path.GetFullPath(args[0])),Path.Combine(game,"BepInEx","core"),interop};
AssemblyLoadContext.Default.Resolving+=(_,name)=>{
 foreach(var folder in folders){var path=Path.Combine(folder,name.Name+".dll");if(File.Exists(path))return AssemblyLoadContext.Default.LoadFromAssemblyPath(path);}return null;
};
var plugin=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[0]));
var animation=plugin.GetType("SVSPregnancy.BodyChangeAnimation",true);
var selected=(MethodInfo[])animation.GetMethod("GetMotionMethods",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
var native=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(interop,"Assembly-CSharp.dll"));
using var reader=new BinaryReader(File.OpenRead(Path.Combine(interop,"MethodAddressToToken.db")));
if(reader.ReadInt32()!=1297370453||reader.ReadInt32()!=1)throw new Exception("Unsupported address database");
int assemblyCount=reader.ReadInt32(),methodCount=reader.ReadInt32(),dataOffset=reader.ReadInt32();
var assemblies=Enumerable.Range(0,assemblyCount).Select(_=>reader.ReadString()).ToArray();
int gameIndex=Array.FindIndex(assemblies,n=>new AssemblyName(n).Name=="Assembly-CSharp");
reader.BaseStream.Position=dataOffset;
var addresses=Enumerable.Range(0,methodCount).Select(_=>reader.ReadInt64()).ToArray();
var mapping=new Dictionary<int,long>();var counts=addresses.GroupBy(a=>a).ToDictionary(g=>g.Key,g=>g.Count());
for(int i=0;i<methodCount;i++){int token=reader.ReadInt32(),assemblyIndex=reader.ReadInt32();if(assemblyIndex==gameIndex)mapping[token]=addresses[i];}
var gameBytes=File.ReadAllBytes(Path.Combine(game,"GameAssembly.dll"));
using var pe=new PEReader(new MemoryStream(gameBytes));
byte[] Code(long rva){var section=pe.PEHeaders.SectionHeaders.Single(s=>rva>=s.VirtualAddress&&rva<s.VirtualAddress+s.VirtualSize);return gameBytes.AsSpan((int)(rva-section.VirtualAddress+section.PointerToRawData),12).ToArray();}
int checks=0;
void Check(string label,bool ok){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS: "+label);}
long Address(MethodInfo method)=>mapping.TryGetValue(method.MetadataToken,out var address)?address:throw new Exception("Native mapping missing: "+method);
var empty=native.GetType("SV.H.Motion.Standby.Controller").GetMethod("Update",BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly);
var les=native.GetType("SV.H.Motion.Les.Controller").GetMethod("OnClothesChanged",BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly);
var emptyAddress=Address(empty);
Check("Crash callback and standby share the same native entry",Address(les)==emptyAddress);
Check("Crash callback is a native RET stub",Code(emptyAddress)[0] is 0xC2 or 0xC3);
Console.WriteLine($"Shared RET: RVA=0x{emptyAddress:X}, database aliases={counts[emptyAddress]}");
Check("29 motion methods selected from the real SVS assembly",selected.Length==29);
var selectedAddresses=new HashSet<long>();
foreach(var method in selected){
 long addr=Address(method);var code=Code(addr);
 var fieldNames=method.DeclaringType.GetFields(BindingFlags.NonPublic|BindingFlags.Static).Select(f=>f.Name);
 Console.WriteLine($"Inspect {method.DeclaringType.FullName}::{method.Name}, RVA=0x{addr:X}, aliases={counts[addr]}, bytes={Convert.ToHexString(code)}");
 if(counts[addr]>1)foreach(var alias in mapping.Where(kv=>kv.Value==addr))Console.WriteLine("Alias: "+native.ManifestModule.ResolveMethod(alias.Key).DeclaringType+"::"+native.ManifestModule.ResolveMethod(alias.Key).Name);
 Check(method.DeclaringType.FullName+"::"+method.Name+" has a unique, non-empty native body",addr!=emptyAddress&&code[0] is not (0xC2 or 0xC3)&&selectedAddresses.Add(addr)&&counts[addr]==1);
 Console.WriteLine($"  RVA=0x{addr:X}, bytes={Convert.ToHexString(code)}");
 var attr=method.GetCustomAttributesData().FirstOrDefault(a=>a.AttributeType.Name=="CachedScanResultsAttribute");
 Check("Native implementation metadata exists: "+method.DeclaringType.FullName+"::"+method.Name,attr!=null);
}
// Native callback selection must not change the visual and contact update paths.
Check("Every body-changing cover callback is retained",selected.Count(m=>m.Name=="OnClothesChanged")==6);
var additional=native.GetType("ILLGames.Rigging.MotionIK").GetMethods(BindingFlags.Public|BindingFlags.Static|BindingFlags.DeclaredOnly).Where(m=>m.Name is "GetCorrectShapeValues" or "GetCorrectShapeValuesParam2")
 .Concat(new[]{native.GetType("SV.H.HActor").GetMethod("get_Breast"),native.GetType("Character.HumanBody").GetMethod("UpdateShapeBody",Type.EmptyTypes)}).ToArray();
Check("All nine other newly patched native entries audited",additional.Length==9);
foreach(var method in additional){long addr=Address(method);Check("Unique original entry: "+method.DeclaringType+"::"+method.Name,counts[addr]==1&&selectedAddresses.Add(addr)&&Code(addr)[0] is not (0xC2 or 0xC3));}
Console.WriteLine($"{checks} startup checks passed against installed SVS interop and GameAssembly; no native code executed.");


