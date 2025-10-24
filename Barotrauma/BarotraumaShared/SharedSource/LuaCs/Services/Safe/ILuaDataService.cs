using MoonSharp.Interpreter;

namespace Barotrauma.LuaCs.Services.Safe;

/// <summary>
/// Service for providing stateful functions and in-memory storage for lua functions 
/// </summary>
public interface ILuaDataService : ILuaService
{
    /// <summary>
    /// Returns stored table for the given object if it exists.
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="tableName"></param>
    /// <returns>The table data or null if none exists.</returns>
    Table GetObjectTable(object obj, string tableName);

    /// <summary>
    /// Returns stored table data under the given name if it exists.
    /// </summary>
    /// <param name="tableName"></param>
    /// <returns>The table data or null if none exists.</returns>
    Table GetTable(string tableName);

    /// <summary>
    /// Returns stored table data for the given object or creates a new table if one doesn't exist.
    /// </summary>
    /// <remarks>Note: tables are stored using weak references and will be automatically deleted when the object is
    /// garbage collected.</remarks>
    /// <param name="obj"></param>
    /// <param name="tableName"></param>
    /// <returns></returns>
    Table GetOrCreateObjectTable(object obj, string tableName);

    /// <summary>
    /// Returns stored table data or creates a new table if one doesn't exist.
    /// </summary>
    /// <param name="tableName"></param>
    /// <returns></returns>
    Table GetOrCreateTable(string tableName);
}
