#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace Barotrauma
{
    public static class ReflectionUtils
    {
        private static readonly ConcurrentDictionary<Assembly, ImmutableArray<Type>> CachedNonAbstractTypes
            = new ConcurrentDictionary<Assembly, ImmutableArray<Type>>();
        private static readonly ConcurrentDictionary<string, ImmutableArray<Type>> TypeSearchCache = new();

        public static IEnumerable<Type> GetDerivedNonAbstract<T>()
        {
            Type t = typeof(T);
            string typeName = t.FullName ?? t.Name;
            
            // search quick lookup cache
            if (TypeSearchCache.TryGetValue(typeName, out var value))
            {
                return value;
            }
            
            // doesn't exist so let's add it.
            Assembly assembly = typeof(T).Assembly;
            if (!CachedNonAbstractTypes.ContainsKey(assembly))
            {
                AddNonAbstractAssemblyTypes(assembly);
            }            
                
            // build cache from registered assemblies' types.
            var list = CachedNonAbstractTypes.Values
                .SelectMany(arr => arr.Where(type => type.IsSubclassOf(t)))
                .ToImmutableArray();

            if (list.Length == 0)
            {
                return ImmutableArray<Type>.Empty;    // No types, don't add to cache
            }            
            
            if (!TypeSearchCache.TryAdd(typeName, list))
            {
                DebugConsole.LogError($"ReflectionUtils::AddNonAbstractAssemblyTypes() | Error while adding to quick lookup cache.");
            }
            return list;
        }

        /// <summary>
        /// Adds an assembly's Non-Abstract Types to the cache for Barotrauma's Type lookup. 
        /// </summary>
        /// <param name="assembly">Assembly to be added</param>
        /// <param name="overwrite">Whether or not to overwrite an entry if the assembly already exists within it.</param>
        public static void AddNonAbstractAssemblyTypes(Assembly assembly, bool overwrite = false)
        {
            if (CachedNonAbstractTypes.ContainsKey(assembly))
            {
                if (!overwrite)
                {
                    DebugConsole.LogError(
                        $"ReflectionUtils::AddNonAbstractAssemblyTypes() | The assembly [{assembly.GetName()}] already exists in the cache.");
                    return;
                }

                CachedNonAbstractTypes.Remove(assembly, out _);
            }

            try
            {
                if (!CachedNonAbstractTypes.TryAdd(assembly, assembly.GetSafeTypes().Where(t => !t.IsAbstract).ToImmutableArray()))
                {
                    DebugConsole.LogError($"ReflectionUtils::AddNonAbstractAssemblyTypes() | Unable to add types from Assembly to cache.");
                }
                else
                {
                    TypeSearchCache.Clear();    // Needs to be rebuilt to include potential new types
                }
            }
            catch (ReflectionTypeLoadException e)
            {
                DebugConsole.LogError($"ReflectionUtils::AddNonAbstractAssemblyTypes() | RTFException: Unable to load Assembly Types from {assembly.GetName()}.");
            }
        }

        /// <summary>
        /// Removes an assembly from the cache for Barotrauma's Type lookup.
        /// </summary>
        /// <param name="assembly">Assembly to remove.</param>
        public static void RemoveAssemblyFromCache(Assembly assembly)
        {
            CachedNonAbstractTypes.Remove(assembly, out _);
            TypeSearchCache.Clear();
        }

        /// <summary>
        /// Clears all cached assembly data and rebuilds types list only to include base Barotrauma types. 
        /// </summary>
        internal static void ResetCache()
        {
            CachedNonAbstractTypes.Clear();
            CachedNonAbstractTypes.TryAdd(typeof(ReflectionUtils).Assembly, typeof(ReflectionUtils).Assembly.GetSafeTypes().ToImmutableArray());
            TypeSearchCache.Clear();
        }
        
        public static Option<TBase> ParseDerived<TBase, TInput>(TInput input) where TInput : notnull where TBase : notnull
        {
            static Option<TBase> none() => Option<TBase>.None();
            
            var derivedTypes = GetDerivedNonAbstract<TBase>();

            Option<TBase> parseOfType(Type t)
            {
                //every TBase type is expected to have a method with the following signature:
                //  public static Option<T> Parse(TInput str)
                var parseFunc = t.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static);
                if (parseFunc is null) { return none(); }
                
                var parameters = parseFunc.GetParameters();
                if (parameters.Length != 1) { return none(); }
                
                var returnType = parseFunc.ReturnType;
                if (!returnType.IsConstructedGenericType) { return none(); }
                if (returnType.GetGenericTypeDefinition() != typeof(Option<>)) { return none(); }
                if (returnType.GenericTypeArguments[0] != t) { return none(); }

                //some hacky business to convert from Option<T2> to Option<TBase> when we only know T2 at runtime
                static Option<TBase> convert<T2>(Option<T2> option) where T2 : TBase
                    => option.Select(v => (TBase)v);
                Func<Option<TBase>, Option<TBase>> f = convert;
                var genericArgs = f.Method.GetGenericArguments();
                genericArgs[^1] = t;
                var constructedConverter =
                    f.Method.GetGenericMethodDefinition().MakeGenericMethod(genericArgs);

                return constructedConverter.Invoke(null, new[] { parseFunc.Invoke(null, new object[] { input }) })
                    as Option<TBase>? ?? none();
            }

            return derivedTypes.Select(parseOfType).FirstOrDefault(t => t.IsSome());
        }

        public static string NameWithGenerics(this Type t)
        {
            if (!t.IsGenericType) { return t.Name; }
            
            string result = t.Name[..t.Name.IndexOf('`')];
            result += $"<{string.Join(", ", t.GetGenericArguments().Select(NameWithGenerics))}>";
            return result;
        }
    }
}
