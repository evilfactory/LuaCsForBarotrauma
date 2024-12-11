#nullable enable
using Barotrauma.Debugging;
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
        private static readonly ConcurrentDictionary<Assembly, ImmutableArray<Type>> CachedNonAbstractTypes = new();
        private static readonly ConcurrentDictionary<string, ImmutableArray<Type>> TypeSearchCache = new();

        public static T GetValueFromStaticProperty<T>(this PropertyInfo property)
        {
            if (property.GetMethod is not { IsStatic: true })
            {
                throw new ArgumentException($"Property {property} is not static");
            }

            var value = property.GetValue(obj: null);
            if (value is not T castValue)
            {
                throw new ArgumentException($"Property {property} is null or not of type {typeof(T)}");
            }

            return castValue;
        }

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
                DebugConsoleCore.Log($"ReflectionUtils.AddNonAbstractAssemblyTypes(): Error while adding to quick lookup cache.");
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
                    return;
                }

                CachedNonAbstractTypes.Remove(assembly, out _);
            }

            try
            {
                if (!CachedNonAbstractTypes.TryAdd(assembly, assembly.GetTypes().Where(t => !t.IsAbstract).ToImmutableArray()))
                {
                }
                else
                {
                    TypeSearchCache.Clear();    // Needs to be rebuilt to include potential new types
                }
            }
            catch (ReflectionTypeLoadException)
            {
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
        public static void ResetCache()
        {
            CachedNonAbstractTypes.Clear();
            CachedNonAbstractTypes.TryAdd(typeof(ReflectionUtils).Assembly, typeof(ReflectionUtils).Assembly.GetTypes().Where(t => !t.IsAbstract).ToImmutableArray());
            TypeSearchCache.Clear();
        }
        
        public static Type? GetType(string nameWithNamespace)
        {
            if (Type.GetType(nameWithNamespace) is Type t) { return t; }

            var entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly?.GetType(nameWithNamespace) is Type t2) { return t2; }

            return null;
        }

        public static Option<TBase> ParseDerived<TBase, TInput>(TInput input)
            where TBase : notnull
            where TInput : notnull
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

        /// <summary>
        /// Gets a type by its name, with backwards compatibility for types that have been renamed.
        /// <see cref="TypePreviouslyKnownAs"/>
        /// </summary>
        public static Type? GetTypeWithBackwardsCompatibility(Assembly assembly, string nameSpace, string typeName, bool throwOnError, bool ignoreCase)
        {
            var types = assembly
                .GetTypes()
                .Where(t => NameMatches(t.Namespace, nameSpace, ignoreCase));

            foreach (Type type in types)
            {
                if (NameMatches(type.Name, typeName, ignoreCase))
                {
                    return type;
                }

                if (type.GetCustomAttribute<TypePreviouslyKnownAs>() is { } knownAsAttribute)
                {
                    if (NameMatches(knownAsAttribute.PreviousName, typeName, ignoreCase))
                    {
                        return type;
                    }
                }
            }

            if (throwOnError)
            {
                throw new TypeLoadException($"Could not find the type {typeName} in namespace {nameSpace}");
            }

            return null;

            static bool NameMatches(string? name1, string? name2, bool ignoreCase)
                => string.Equals(name1, name2, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }

        /// <summary>
        /// The names of generic types include the arity at the end (fancy way of saying the number of parameters, e.g. GUISelectionCarousel<T> would be GUISelectionCarousel`1)
        /// This method strips that part out.
        /// </summary>
        public static Identifier GetTypeNameWithoutGenericArity(Type type)
        {
            string name = type.Name;
            int index = name.IndexOf('`');
            return (index == -1 ? name : name.Substring(0, index)).ToIdentifier();
        }
    }
}
