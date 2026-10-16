using System.Numerics;
using System.Diagnostics;
using SVSPregnancy;
int checks=0;
void Check(string label,bool ok){if(!ok)throw new Exception(label);checks++;}
int Linear(Vector3[] points,Vector3 p){float best=float.PositiveInfinity;int index=-1;for(int i=0;i<points.Length;i++){float x=points[i].X-p.X,y=points[i].Y-p.Y,z=points[i].Z-p.Z;float d=x*x+y*y+z*z;if(d<best){best=d;index=i;}}return index;}
var random=new Random(1632);
Vector3 Next()=>new((float)random.NextDouble()*.6f-.3f,(float)random.NextDouble()*1.7f,(float)random.NextDouble()*.4f-.2f);
foreach(int count in new[]{0,1,2,7,100,10597}){
 var points=Enumerable.Range(0,count).Select(_=>Next()).ToArray();
 var tree=new NearestVertexIndex(points);
 for(int i=0;i<200;i++){var q=Next();Check("Exact nearest mapping for "+count,tree.Find(q)==Linear(points,q));}
 for(int i=0;i<Math.Min(count,100);i++)Check("Coincident vertex",tree.Find(points[i])==Linear(points,points[i]));
}
foreach(var points in new[]{new[]{Vector3.Zero,Vector3.Zero,Vector3.UnitX},new[]{-Vector3.UnitX,Vector3.UnitX},Enumerable.Repeat(Vector3.Zero,100).ToArray(),Enumerable.Range(0,101).Select(i=>new Vector3(0,i*.01f,0)).ToArray()}){
 var tree=new NearestVertexIndex(points);foreach(var q in new[]{Vector3.Zero,Vector3.One,new Vector3(0,.505f,0)})Check("Duplicate/tie/degenerate geometry",tree.Find(q)==Linear(points,q));
}
var body=Enumerable.Range(0,10597).Select(_=>Next()).ToArray();
var overlay=Enumerable.Range(0,2000).Select(_=>Next()).ToArray();
var watch=Stopwatch.StartNew();var spatial=new NearestVertexIndex(body);double build=watch.Elapsed.TotalMilliseconds;
watch.Restart();int[] expected=overlay.Select(p=>Linear(body,p)).ToArray();double brute=watch.Elapsed.TotalMilliseconds;
watch.Restart();long visits=0;for(int i=0;i<overlay.Length;i++){int actual=spatial.Find(overlay[i],out int visited);visits+=visited;Check("Representative mesh mapping exact",actual==expected[i]);}double indexed=watch.Elapsed.TotalMilliseconds;
Check("Representative search eliminates at least 90 percent of pair comparisons",visits<(long)body.Length*overlay.Length/10);
Console.WriteLine($"{checks} exact-search checks passed.");
Console.WriteLine($"Representative body={body.Length}, overlays={overlay.Length}: linear comparisons={(long)body.Length*overlay.Length:N0}, index comparisons={visits:N0}; build={build:F2} ms, query={indexed:F2} ms, managed linear reference={brute:F2} ms.");
Console.WriteLine("Synthetic managed benchmark; excludes native bridge overhead and is not an in-game loading-time measurement.");
