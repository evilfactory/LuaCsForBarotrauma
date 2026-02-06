using System.Collections.Immutable;

namespace Barotrauma.LuaCs;

public interface ISafeStorageService : IStorageService, ISafeStorageValidation { }

public interface ISafeStorageValidation
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
    /// <param name="path">The path to the file, exactly as it will be passed to the Try(Load|Save) methods in <see cref="StorageService"/>.</param>
    /// <param name="readOnly">Whether to add it to the read whitelist only, or Read+Write whitelists.</param>
    void AddFileToWhitelist(string path, bool readOnly = true);
    
    /// <summary>
    /// Adds the given collection of file paths to whitelists (Read|+Write)
    /// </summary>
    /// <param name="paths">The paths to the files, formatted exactly as it will be passed to the Try(Load|Save) methods in <see cref="StorageService"/>.</param>
    /// <param name="readOnly">Whether to add it to the read whitelist only, or Read+Write whitelists.</param>
    void AddFilesToWhitelist(ImmutableArray<string> paths, bool readOnly = true);
    
    /// <summary>
    /// Removes the given path from all whitelists (Read|+Write).
    /// </summary>
    /// <param name="path"></param>
    void RemoveFileFromAllWhitelists(string path);

    /// <summary>
    /// Sets the whitelist filtering for read-only file permissions for the instance. Overwrites previous list.
    /// </summary>
    /// <param name="filePaths">List of file paths allowed, as will be passed to the <see cref="StorageService"/> Try(Load|Save) methods.</param>
    FluentResults.Result SetReadOnlyWhitelist(ImmutableArray<string> filePaths);

    /// <summary>
    /// Sets the whitelist filtering for read & write file permissions for the instance. Overwrites previous lists.
    /// </summary>
    /// <param name="filePaths">List of file paths allowed, as will be passed to the <see cref="StorageService"/> Try(Load|Save) methods.</param>
    FluentResults.Result SetReadWriteWhitelist(ImmutableArray<string> filePaths);
    
    /// <summary>
    /// Deletes all paths from all white lists.
    /// </summary>
    void ClearAllWhitelists();
}
