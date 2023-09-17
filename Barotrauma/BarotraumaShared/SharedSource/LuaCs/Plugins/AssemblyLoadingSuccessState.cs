namespace Barotrauma;

public enum AssemblyLoadingSuccessState
{
    ACLLoadFailure,
    AlreadyLoaded,
    BadFilePath,
    CannotLoadFile,
    InvalidAssembly,
    NoAssemblyFound,
    PluginInstanceFailure,
    BadName,
    CannotLoadFromStream,
    Success
}
