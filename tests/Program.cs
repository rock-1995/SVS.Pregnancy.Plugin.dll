try
{
int passed = 0;
void Check(string name, bool value)
{
    if (!value) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name); passed++;
}
ReleaseDefaultsRegression.Run(Check);
LowerTransitionRegression.Run(Check);
UpperTransitionRegression.Run(Check);
UpperRestrictionsRegression.Run(Check);
ShadingRegression.Run(Check);
ShapeRigRegression.Run(Check);
VirtualAxisRegression.Run(Check);
RenderFixRegression.Run(Check);
BreastExclusionRegression.Run(Check);
SvsAssetRegression.Landmarks(Check);
SkinAttachmentRegression.Run(Check);
if(args.Length>0){SvsAssetRegression.Run(args[0],Check);SkinAttachmentRegression.Asset(args[0],Check);}
Console.WriteLine($"{passed} geometry regression checks passed.");
}
catch(Exception ex){Console.Error.WriteLine(ex);Environment.ExitCode=1;}
