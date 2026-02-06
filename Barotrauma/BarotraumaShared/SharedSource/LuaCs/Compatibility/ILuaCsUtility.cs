namespace Barotrauma.LuaCs.Compatibility;

public interface ILuaCsUtility : ILuaCsShim
{
    public bool CanReadFromPath(string file);
    public bool CanWriteToPath(string file);
    internal bool IsPathAllowedException(string path, bool write = true, 
        LuaCsMessageOrigin origin = LuaCsMessageOrigin.Unknown);
    
}
