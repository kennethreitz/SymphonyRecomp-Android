namespace SymphonyRecomp.Android;

// Deploy source through the app's own supported storage APIs, not privileged shell access.
// Existing user-edited source is never overwritten. No mod is enabled by this helper.
internal static class BundledMods
{
    public static void Seed(string modRoot)
    {
        var assembly=typeof(BundledMods).Assembly;
        foreach(string resource in assembly.GetManifestResourceNames().Where(n=>n.StartsWith("BundledMods.CastleGearPlus.",StringComparison.Ordinal)))
        {
            string name=resource["BundledMods.CastleGearPlus.".Length..];
            string folder=Path.Combine(modRoot,"CastleGearPlus");
            Directory.CreateDirectory(folder);
            string path=Path.Combine(folder,name);
            if(File.Exists(path))continue;
            using var input=assembly.GetManifestResourceStream(resource)!;
            using var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);
            input.CopyTo(output);
            Console.WriteLine("[BundledMods] Seeded CastleGearPlus/"+name);
        }
    }
}
