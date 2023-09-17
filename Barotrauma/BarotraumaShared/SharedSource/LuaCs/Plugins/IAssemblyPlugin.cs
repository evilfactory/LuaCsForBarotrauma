using System;

namespace Barotrauma;

public interface IAssemblyPlugin : IDisposable
{
    /// <summary>
    /// Called on plugin normal, use this for basic/core loading that does not rely on any other modded content.
    /// </summary>
    void Initialize();
    
    /// <summary>
    /// Called once all plugins have been loaded. if you have integrations with any other mod, put that code here.
    /// </summary>
    void OnLoadCompleted();


    /// <summary>
    /// Called before Barotrauma initializes vanilla content. WARNING: This method may be called before Initialize()!
    /// </summary>
    void PreInitPatching();
}
