using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs.Services;
using Barotrauma.Networking;

namespace Barotrauma.LuaCs.Configuration;

public interface ISettingBase : IDataInfo, IEquatable<ISettingBase>, IDisposable
{
    /// <summary>
    /// Settings production factory. Should be implemented by all types and registered with the Dependency Injector.
    /// </summary>
    /// <typeparam name="T">An interface type derived from <see cref="ISettingBase"/>.</typeparam>
    public interface IFactory<out T> where T : ISettingBase
    {
        /// <summary>
        /// Creates an instance of the given <see cref="ISettingBase"/> type.
        /// </summary>
        /// <param name="configInfo">Configuration information.</param>
        /// <param name="valueChangePredicate">Called before a new value is assigned. Returns a boolean whether to allow
        /// the value to be changed to the one given.</param>
        /// <returns></returns>
        T CreateInstance([NotNull]IConfigInfo configInfo, Func<OneOf<string, XElement, object>, bool> valueChangePredicate);
    }
    
    #if CLIENT
    IConfigDisplayInfo GetDisplayInfo();
    #endif
    Type GetValueType();
    string GetStringValue();
    string GetDefaultStringValue();
    bool TrySetValue(OneOf.OneOf<string, XElement> value);
    event Action<ISettingBase> OnValueChanged;
    OneOf.OneOf<string, XElement> GetSerializableValue();
}

/// <summary>
/// Creates a setting representing a value of the given <see cref="Type"/>. Must be a compatible listed type. <br/>
/// </summary>
/// <typeparam name="T">
/// <b>Compatible Types:</b><br/>
/// Any primitive type:<br/>
/// - <see cref="byte"/><br/>
/// - <see cref="sbyte"/><br/>
/// - <see cref="ushort"/><br/>
/// - <see cref="short"/><br/>
/// - <see cref="int"/><br/>
/// - <see cref="uint"/><br/>
/// - <see cref="long"/><br/>
/// - <see cref="ulong"/><br/>
/// - <see cref="float"/><br/>
/// - <see cref="double"/><br/>
/// Extension types and Enums: <br/>
/// - <see cref="string"/><br/>
/// - <see cref="Enum"/><br/>
/// </typeparam>
public interface ISettingBase<T> : ISettingBase where T : IEquatable<T>, IConvertible
{
    [NotNull]
    T Value { get; }
    [NotNull]
    T DefaultValue { get; }
    bool TrySetValue(T value);
}

/// <summary>
/// Creates a setting representing a value of the given <see cref="Type"/> with a minimum and maximum value.
/// Must be a type compatible with <see cref="ISettingBase{T}"/>.
/// </summary>
/// <typeparam name="T">The value type. See <see cref="ISettingBase{T}"/></typeparam>
public interface ISettingRangeBase<T> : ISettingBase<T> where T : IEquatable<T>, IConvertible
{
    T MinValue { get; }
    T MaxValue { get; }
    int IncrementalSteps { get; }
}

/// <summary>
/// Creates a setting representing a value of the given <see cref="Type"/> with a distinct list of selectable values.
/// Must be a type compatible with <see cref="ISettingBase{T}"/>.
/// </summary>
/// <typeparam name="T">The value type. See <see cref="ISettingBase{T}"/></typeparam>
public interface ISettingList<T> : ISettingBase<T> where T : IEquatable<T>, IConvertible
{
    IReadOnlyList<T> Options { get; }
    IReadOnlyList<string> StringOptions { get; }
}
