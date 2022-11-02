#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

namespace Barotrauma
{
    public static class ReflectionUtils
    {
        private static readonly Dictionary<Assembly, ImmutableArray<Type>> cachedNonAbstractTypes
            = new Dictionary<Assembly, ImmutableArray<Type>>();

        
        public static IEnumerable<Type> GetDerivedNonAbstract<T>()
        {
            Assembly assembly = typeof(T).Assembly;
            if (!cachedNonAbstractTypes.ContainsKey(assembly))
                AddNonAbstractAssemblyTypes(assembly);

            #warning TODO: Add safety checks in case an assembly is unloaded without being removed from the cache.
            
            List<Type> types = new List<Type>();
            foreach (var typearr in cachedNonAbstractTypes)
                types = types.Concat(typearr.Value.Where(t => t.IsSubclassOf(typeof(T)))).ToList();
            
            return types;
        }

        /// <summary>
        /// Adds an assembly's Non-Abstract Types to the cache for Barotrauma's Type lookup. 
        /// </summary>
        /// <param name="assembly">Assembly to be added</param>
        /// <param name="overwrite">Whether or not to overwrite an entry if the assembly already exists within it.</param>
        public static void AddNonAbstractAssemblyTypes(Assembly assembly, bool overwrite = false)
        {
            if (cachedNonAbstractTypes.ContainsKey(assembly))
            {
                if (!overwrite)
                {
                    DebugConsole.LogError(
                        $"ReflectionUtils::AddNonAbstractAssemblyTypes() | The assembly [{assembly.GetName()}] already exists in the cache.");
                    return;
                }
                cachedNonAbstractTypes.Remove(assembly);
            }

            try
            {
                if (!cachedNonAbstractTypes.TryAdd(assembly, assembly.GetTypes().Where(t => !t.IsAbstract).ToImmutableArray()))
                    DebugConsole.LogError($"ReflectionUtils::AddNonAbstractAssemblyTypes() | Unable to add types from Assembly to cache.");
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
        public static void RemoveAssemblyFromCache(Assembly assembly) => cachedNonAbstractTypes.Remove(assembly);

        public static Option<TBase> ParseDerived<TBase, TInput>(TInput input) where TInput : notnull
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
                    as Option<TBase> ?? none();
            }

            return derivedTypes.Select(parseOfType).FirstOrDefault(t => t.IsSome()) ?? none();
        }
    }
}
