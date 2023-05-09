using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Barotrauma;

public class PublicizedBinariesResolver
{
    private List<string> searchRoots;
    private const string PublicizedDirName = "Publicized";
    
    public PublicizedBinariesResolver()
    {
        this.searchRoots = new List<string>(2);

        var luaForBarotraumaPackage = LuaCsSetup.GetPackage(LuaCsSetup.LuaForBarotraumaId);
        this.searchRoots.Add(Path.Combine(luaForBarotraumaPackage.Path, PublicizedDirName));
            
        var gameRoot = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        this.searchRoots.Add(Path.Combine(gameRoot, PublicizedDirName));
    }

    public string TryFindPublicized(string location)
    {
        foreach (var searchRoot in this.searchRoots)
        {
            var pub = Path.Combine(searchRoot, Path.GetFileName(location));
            if (File.Exists(pub))
            {
                return pub;
            }
        }

        return location;
    }
}
