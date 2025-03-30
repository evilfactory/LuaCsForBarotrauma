using System.Collections.Immutable;

namespace Barotrauma.LuaCs.Services.Safe;

public interface ISafeStorageService : IStorageService
{
    /// <summary>
    /// Checks the given file path to see if it can be read. This includes any permissions, whitelists and OS checks.
    /// </summary>
    /// <param name="path">The absolute path to the file.</param>
    /// <param name="readOnly">Whether to only check for read permissions only, or full RWM if false.</param>
    /// <param name="checkWhitelistOnly">Whether to only check if the file is safe to access, without checking accessibility at the OS level.</param>
    /// <returns>Whether the file is accessible.</returns>
    bool IsFileAccessible(string path, bool readOnly, bool checkWhitelistOnly = true);

    /// <summary>
    /// Adds the given path to the specified whitelists. 
    /// </summary>
    /// <param name="path">Either the fully-qualified or local reference path to the given file.</param>
    /// <param name="readOnly"></param>
    void AddFileToWhitelist(string path, bool readOnly = true);
    
    /// <summary>
    /// Removes the given path from all whitelists (Read|Write).
    /// </summary>
    /// <param name="path"></param>
    void RemoveFileFromAllWhitelists(string path);

    /// <summary>
    /// Sets the whitelist filtering for read-only file permissions for the instance.
    /// </summary>
    /// <param name="filePaths">List of absolute file paths allowed.</param>
    FluentResults.Result SetReadOnlyWhitelist(ImmutableArray<string> filePaths);

    /// <summary>
    /// Sets the whitelist filtering for read & write file permissions for the instance.
    /// </summary>
    /// <param name="filePaths">List of absolute file paths allowed.</param>
    FluentResults.Result SetReadWriteWhitelist(ImmutableArray<string> filePaths);
    
    /// <summary>
    /// Deletes all paths from all white lists.
    /// </summary>
    void ClearAllWhitelists();

    /// <summary>
    /// Whether the service instance is in file read-only mode.
    /// </summary>
    bool IsReadOnlyMode { get; }
    
    /// <summary>
    /// Sets the service into file read-only mode. Cannot be undone.
    /// </summary>
    /// <returns></returns>
    bool EnableReadOnlyMode();
}
