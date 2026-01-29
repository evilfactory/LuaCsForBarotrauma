using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using Microsoft.Xna.Framework;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Interop;
using Sigil;
using Sigil.NonGeneric;

namespace Barotrauma
{
    public delegate void LuaCsAction(params object[] args);
    public delegate object LuaCsFunc(params object[] args);
    public delegate DynValue LuaCsPatchFunc(object instance, LuaCsHook.ParameterTable ptable);

    internal static class SigilExtensions
    {
        /// <summary>
        /// Puts a type on the stack, as a <see cref="Type" /> object instead of a
        /// runtime type token.
        /// </summary>
        /// <param name="il">The IL emitter.</param>
        /// <param name="type">The type to put on the stack.</param>
        public static void LoadType(this Emit il, Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            il.LoadConstant(type); // ldtoken
            // This converts the type token into a Type object
            il.Call(typeof(Type).GetMethod(
                name: nameof(Type.GetTypeFromHandle),
                bindingAttr: BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new Type[] { typeof(RuntimeTypeHandle) },
                modifiers: null));
        }

        /// <summary>
        /// Converts the value on the stack to <see cref="object" />.
        /// </summary>
        /// <param name="il">The IL emitter.</param>
        /// <param name="type">The type of the value on the stack.</param>
        public static void ToObject(this Emit il, Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            il.DerefIfByRef(ref type);
            if (type.IsValueType)
            {
                il.Box(type);
            }
            else if (type != typeof(object))
            {
                il.CastClass<object>();
            }
        }

        /// <summary>
        /// Deferences the value on stack if the provided type is ByRef.
        /// </summary>
        /// <param name="il">The IL emitter.</param>
        /// <param name="type">The type to check if ByRef.</param>
        public static void DerefIfByRef(this Emit il, Type type) => il.DerefIfByRef(ref type);

        /// <summary>
        /// Deferences the value on stack if the provided type is ByRef.
        /// </summary>
        /// <param name="il">The IL emitter.</param>
        /// <param name="type">The type to check if ByRef.</param>
        public static void DerefIfByRef(this Emit il, ref Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (type.IsByRef)
            {
                type = type.GetElementType();
                if (type.IsValueType)
                {
                    il.LoadObject(type);
                }
                else
                {
                    il.LoadIndirect(type);
                }
            }
        }

        // Copied from https://github.com/evilfactory/moonsharp/blob/5264656c6442e783f3c75082cce69a93d66d4cc0/src/MoonSharp.Interpreter/Interop/Converters/ScriptToClrConversions.cs#L79-L99
        private static MethodInfo GetImplicitOperatorMethod(Type baseType, Type targetType)
        {
            try
            {
                return Expression.Convert(Expression.Parameter(baseType, null), targetType).Method;
            }
            catch
            {
                if (baseType.BaseType != null)
                {
                    return GetImplicitOperatorMethod(baseType.BaseType, targetType);
                }

                if (targetType.BaseType != null)
                {
                    return GetImplicitOperatorMethod(baseType, targetType.BaseType);
                }

                return null;
            }
        }

        /// <summary>
        /// Loads a local variable and casts it to the target type.
        /// </summary>
        /// <param name="il">The IL emitter.</param>
        /// <param name="value">The value to cast. Must be of type <see cref="object" />.</param>
        /// <param name="targetType">The type to cast into.</param>
        public static void LoadLocalAndCast(this Emit il, Local value, Type targetType)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (targetType == null) throw new ArgumentNullException(nameof(targetType));
            if (value.LocalType != typeof(object))
            {
                throw new ArgumentException($"Expected local type {typeof(object)}; got {value.LocalType}.", nameof(value));
            }

            var guid = Guid.NewGuid().ToString("N");

            if (targetType.IsByRef)
            {
                targetType = targetType.GetElementType();
            }

            // IL: var baseType = value.GetType();
            var baseType = il.DeclareLocal(typeof(Type), $"cast_baseType_{guid}");
            il.LoadLocal(value);
            il.Call(typeof(object).GetMethod("GetType"));
            il.StoreLocal(baseType);

            // IL: var implicitOperatorMethod = SigilExtensions.GetImplicitOperatorMethod(baseType, <targetType>);
            var implicitOperatorMethod = il.DeclareLocal(typeof(MethodInfo), $"cast_implicitOperatorMethod_{guid}");
            il.LoadLocal(baseType);
            il.LoadType(targetType);
            il.Call(typeof(SigilExtensions).GetMethod(nameof(GetImplicitOperatorMethod), BindingFlags.NonPublic | BindingFlags.Static));
            il.StoreLocal(implicitOperatorMethod);

            // IL: <TargetType> castValue;
            var castValue = il.DeclareLocal(targetType, $"cast_castValue_{guid}");

            // IL: if (implicitConversionMethod != null)
            il.LoadLocal(implicitOperatorMethod);
            il.Branch((il) =>
            {
                // IL: var methodInvokeParams = new object[1];
                var methodInvokeParams = il.DeclareLocal(typeof(object[]), $"cast_methodInvokeParams_{guid}");
                il.LoadConstant(1);
                il.NewArray(typeof(object));
                il.StoreLocal(methodInvokeParams);

                // IL: methodInvokeParams[0] = value;
                il.LoadLocal(methodInvokeParams);
                il.LoadConstant(0);
                il.LoadLocal(value);
                il.StoreElement<object>();

                // IL: castValue = (<TargetType>)implicitConversionMethod.Invoke(null, methodInvokeParams);
                il.LoadLocal(implicitOperatorMethod);
                il.LoadNull(); // first parameter is null because implicit cast operators are static
                il.LoadLocal(methodInvokeParams);
                il.Call(typeof(MethodInfo).GetMethod("Invoke", new[] { typeof(object), typeof(object[]) }));
                if (targetType.IsValueType)
                {
                    il.UnboxAny(targetType);
                }
                else
                {
                    il.CastClass(targetType);
                }
                il.StoreLocal(castValue);
            },
            (il) =>
            {
                // IL: castValue = (<TargetType>)value;
                il.LoadLocal(value);
                if (targetType.IsValueType)
                {
                    il.UnboxAny(targetType);
                }
                else
                {
                    il.CastClass(targetType);
                }
                il.StoreLocal(castValue);
            });

            il.LoadLocal(castValue);
        }

        /// <summary>
        /// Emits a call to <see cref="string.Format(string, object[])"/>.
        /// </summary>
        /// <param name="il">The IL emitter.</param>
        /// <param name="format">The string format.</param>
        /// <param name="args">The local variables passed to string.Format.</param>
        public static void FormatString(this Emit il, string format, params Local[] args)
        {
            if (format == null) throw new ArgumentNullException(nameof(format));
            if (args == null) throw new ArgumentNullException(nameof(args));

            var guid = Guid.NewGuid().ToString("N");

            var listType = typeof(List<>).MakeGenericType(typeof(object));
            var list = il.DeclareLocal(listType, $"formatString_list_{guid}");
            il.NewObject(listType);
            il.StoreLocal(list);

            foreach (var arg in args)
            {
                il.LoadLocal(list);
                il.LoadLocal(arg);
                il.ToObject(arg.LocalType);
                il.CallVirtual(listType.GetMethod("Add", new[] { typeof(object) }));
            }

            var arr = il.DeclareLocal<object[]>($"formatString_arr_{guid}");
            il.LoadLocal(list);
            il.CallVirtual(listType.GetMethod("ToArray", new Type[0]));
            il.StoreLocal(arr);

            il.LoadConstant(format);
            il.LoadLocal(arr);
            il.Call(typeof(string).GetMethod("Format", new[] { typeof(string), typeof(object[]) }));
        }

        /// <summary>
        /// Emits a call to <see cref="DebugConsole.NewMessage(string, Color?, bool)" />.
        /// </summary>
        /// <param name="il">The IL emitter.</param>
        /// <param name="message">The message to print.</param>
        public static void NewMessage(this Emit il, string message)
        {
            var newMessage = typeof(DebugConsole).GetMethod(
                name: nameof(DebugConsole.NewMessage),
                bindingAttr: BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new Type[] { typeof(string), typeof(Color?), typeof(bool) },
                modifiers: null);
            il.LoadConstant(message);
            il.Call(typeof(Color).GetProperty(nameof(Color.LightBlue), BindingFlags.Public | BindingFlags.Static).GetGetMethod());
            il.LoadConstant(false);
            il.Call(newMessage);
        }

        /// <summary>
        /// Emits a call to <see cref="DebugConsole.NewMessage(string, Color?, bool)" />,
        /// using the string on the stack.
        /// </summary>
        /// <param name="il">The IL emitter.</param>
        public static void NewMessage(this Emit il)
        {
            var newMessage = typeof(DebugConsole).GetMethod(
                name: nameof(DebugConsole.NewMessage),
                bindingAttr: BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new Type[] { typeof(string), typeof(Color?), typeof(bool) },
                modifiers: null);
            il.Call(typeof(Color).GetProperty(nameof(Color.LightBlue), BindingFlags.Public | BindingFlags.Static).GetGetMethod());
            il.LoadConstant(false);
            il.Call(newMessage);
        }

        /// <summary>
        /// Emits a <c>foreach</c> loop that iterates over an <see cref="IEnumerable{T}"/> local variable.
        /// </summary>
        /// <typeparam name="T">The type of elements in the enumerable.</typeparam>
        /// <param name="il">The IL emitter.</param>
        /// <param name="enumerable">The enumerable.</param>
        /// <param name="action">The body of code to run on each iteration.</param>
        public static void ForEachEnumerable<T>(this Emit il, Local enumerable, Action<Emit, Local, Sigil.Label> action)
        {
            if (enumerable == null) throw new ArgumentNullException(nameof(enumerable));
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (!typeof(IEnumerable<T>).IsAssignableFrom(enumerable.LocalType))
            {
                throw new ArgumentException($"Expected local type {typeof(IEnumerator<T>)}; got {enumerable.LocalType}.", nameof(enumerable));
            }

            var guid = Guid.NewGuid().ToString("N");

            var enumerator = il.DeclareLocal<IEnumerator<T>>($"forEachEnumerable_enumerator_{guid}");
            il.LoadLocal(enumerable);
            il.CallVirtual(typeof(IEnumerable<T>).GetMethod("GetEnumerator"));
            il.StoreLocal(enumerator);
            ForEachEnumerator<T>(il, enumerator, action);
        }

        /// <summary>
        /// Emits a <c>foreach</c> loop that iterates over an <see cref="IEnumerator{T}"/> local variable.
        /// </summary>
        /// <typeparam name="T">The type of elements in the enumerable.</typeparam>
        /// <param name="il">The IL emitter.</param>
        /// <param name="enumerator">The enumerator.</param>
        /// <param name="action">The body of code to run on each iteration.</param>
        public static void ForEachEnumerator<T>(this Emit il, Local enumerator, Action<Emit, Local, Sigil.Label> action)
        {
            if (enumerator == null) throw new ArgumentNullException(nameof(enumerator));
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (!typeof(IEnumerator<T>).IsAssignableFrom(enumerator.LocalType))
            {
                throw new ArgumentException($"Expected local type {typeof(IEnumerator<T>)}; got {enumerator.LocalType}.", nameof(enumerator));
            }

            var guid = Guid.NewGuid().ToString("N");
            var labelLoopStart = il.DefineLabel($"forEach_loopStart_{guid}");
            var labelMoveNext = il.DefineLabel($"forEach_moveNext_{guid}");
            var labelLeave = il.DefineLabel($"forEach_leave_{guid}");

            il.BeginExceptionBlock(out var exceptionBlock);
            il.Branch(labelMoveNext); // MoveNext() needs to be called at least once before iterating
            il.MarkLabel(labelLoopStart);

            // IL: var current = enumerator.Current;
            var current = il.DeclareLocal<T>($"forEachEnumerator_current_{guid}");
            il.LoadLocal(enumerator);
            il.CallVirtual(enumerator.LocalType.GetProperty("Current").GetGetMethod());
            il.StoreLocal(current);

            action(il, current, labelLeave);

            il.MarkLabel(labelMoveNext);
            il.LoadLocal(enumerator);
            il.CallVirtual(typeof(IEnumerator).GetMethod("MoveNext"));
            il.BranchIfTrue(labelLoopStart); // loop if MoveNext() returns true

            // IL: finally { enumerator.Dispose(); }
            il.BeginFinallyBlock(exceptionBlock, out var finallyBlock);
            il.LoadLocal(enumerator);
            il.CallVirtual(typeof(IDisposable).GetMethod("Dispose"));
            il.EndFinallyBlock(finallyBlock);

            il.EndExceptionBlock(exceptionBlock);

            il.MarkLabel(labelLeave);
        }

        /// <summary>
        /// Emits a branch that only executes if the last value on the stack
        /// is truthy (e.g. non-null references, 1, etc).
        /// </summary>
        /// <param name="il">The IL emitter.</param>
        /// <param name="action">The body of code to run if the value is truthy.</param>
        public static void If(this Emit il, Action<Emit> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            il.Branch(@if: action);
        }

        /// <summary>
        /// Emits a branch that only executes if the last value on the stack
        /// is falsy (e.g. null references, 0, etc).
        /// </summary>
        /// <param name="il">The IL emitter.</param>
        /// <param name="action">The body of code to run if the value is falsy.</param>
        public static void IfNot(this Emit il, Action<Emit> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            il.Branch(@else: action);
        }

        /// <summary>
        /// Emits two branches that diverge based on a condition -- analogous
        /// to an if-else statement. If either <paramref name="if"/>
        /// or <paramref name="else"/> are omitted, it behaves the same as
        /// <see cref="If(Emit, Action{Emit})"/>
        /// and <see cref="IfNot(Emit, Action{Emit})"/>.
        /// </summary>
        /// <param name="il">The IL emitter.</param>
        /// <param name="if">The body of code to run if the value is truthy.</param>
        /// <param name="else">The body of code to run if the value is falsy.</param>
        public static void Branch(this Emit il, Action<Emit> @if = null, Action<Emit> @else = null)
        {
            if (@if == null && @else == null) throw new ArgumentException("At least one of the two branches must be defined.");

            var guid = Guid.NewGuid().ToString("N");
            var labelEnd = il.DefineLabel($"branch_end_{guid}");
            if (@if != null && @else != null)
            {
                var labelElse = il.DefineLabel($"branch_else_{guid}");
                il.BranchIfFalse(labelElse);
                @if(il);
                il.Branch(labelEnd);
                il.MarkLabel(labelElse);
                @else(il);
            }
            else if (@if != null)
            {
                il.BranchIfFalse(labelEnd);
                @if(il);
            }
            else
            {
                il.BranchIfTrue(labelEnd);
                @else(il);
            }
            il.MarkLabel(labelEnd);
        }
    }

    public partial class LuaCsHook
    {
        public enum HookMethodType
        {
            Before, After
        }

        private class LuaCsHookCallback
        {
            public string name;
            public string hookName;
            public LuaCsFunc func;

            public LuaCsHookCallback(string name, string hookName, LuaCsFunc func)
            {
                this.name = name;
                this.hookName = hookName;
                this.func = func;
            }
        }

        private class LuaCsPatch
        {
            public string Identifier { get; set; }

            public LuaCsPatchFunc PatchFunc { get; set; }
        }

        private class PatchedMethod
        {
            public PatchedMethod(MethodInfo harmonyPrefix, MethodInfo harmonyPostfix)
            {
                HarmonyPrefixMethod = harmonyPrefix;
                HarmonyPostfixMethod = harmonyPostfix;
                Prefixes = new Dictionary<string, LuaCsPatch>();
                Postfixes = new Dictionary<string, LuaCsPatch>();
            }

            public MethodInfo HarmonyPrefixMethod { get; }

            public MethodInfo HarmonyPostfixMethod { get; }

            public IEnumerator<LuaCsPatch> GetPrefixEnumerator() => Prefixes.Values.GetEnumerator();

            public IEnumerator<LuaCsPatch> GetPostfixEnumerator() => Postfixes.Values.GetEnumerator();

            public Dictionary<string, LuaCsPatch> Prefixes { get; }

            public Dictionary<string, LuaCsPatch> Postfixes { get; }
        }

        public class ParameterTable
        {
            private readonly Dictionary<string, object> parameters;
            private bool returnValueModified;
            private object returnValue;

            public ParameterTable(Dictionary<string, object> dict)
            {
                parameters = dict;
            }

            public object this[string paramName]
            {
                get
                {
                    if (ModifiedParameters.TryGetValue(paramName, out var value))
                    {
                        return value;
                    }
                    return OriginalParameters[paramName];
                }
                set
                {
                    ModifiedParameters[paramName] = value;
                }
            }

            public object OriginalReturnValue { get; private set; }

            public object ReturnValue
            {
                get
                {
                    if (returnValueModified) return returnValue;
                    return OriginalReturnValue;
                }
                set
                {
                    returnValueModified = true;
                    returnValue = value;
                }
            }

            public bool PreventExecution { get; set; }

            public Dictionary<string, object> OriginalParameters => parameters;

            [MoonSharpHidden]
            public Dictionary<string, object> ModifiedParameters { get; } = new Dictionary<string, object>();
        }

        private static readonly string[] prohibitedHooks =
        {
            "Barotrauma.Lua",
            "Barotrauma.Cs",
            "Barotrauma.ContentPackageManager",
        };

        private static void ValidatePatchTarget(MethodBase method)
        {
            if (prohibitedHooks.Any(h => method.DeclaringType.FullName.StartsWith(h)))
            {
                throw new ArgumentException("Hooks into the modding environment are prohibited.");
            }
        }

        private static string NormalizeIdentifier(string identifier)
        {
            return identifier?.Trim().ToLowerInvariant();
        }

        private Harmony harmony;

        private Lazy<ModuleBuilder> patchModuleBuilder;

        private readonly Dictionary<string, Dictionary<string, (LuaCsHookCallback, ACsMod)>> hookFunctions = new Dictionary<string, Dictionary<string, (LuaCsHookCallback, ACsMod)>>();

        private readonly Dictionary<MethodKey, PatchedMethod> registeredPatches = new Dictionary<MethodKey, PatchedMethod>();

        private LuaCsSetup luaCs;

        private static LuaCsHook instance;

        private struct MethodKey : IEquatable<MethodKey>
        {
            public ModuleHandle ModuleHandle { get; set; }

            public int MetadataToken { get; set; }

            public override bool Equals(object obj)
            {
                return obj is MethodKey key && Equals(key);
            }

            public bool Equals(MethodKey other)
            {
                return ModuleHandle.Equals(other.ModuleHandle) && MetadataToken == other.MetadataToken;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(ModuleHandle, MetadataToken);
            }

            public static bool operator ==(MethodKey left, MethodKey right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(MethodKey left, MethodKey right)
            {
                return !(left == right);
            }

            public static MethodKey Create(MethodBase method) => new MethodKey
            {
                ModuleHandle = method.Module.ModuleHandle,
                MetadataToken = method.MetadataToken,
            };
        }

        internal LuaCsHook(LuaCsSetup luaCs)
        {
            instance = this;
            this.luaCs = luaCs;
        }

        public void Initialize()
        {
            harmony = new Harmony("LuaCsForBarotrauma");
            patchModuleBuilder = new Lazy<ModuleBuilder>(CreateModuleBuilder);

            UserData.RegisterType<ParameterTable>();
            var hookType = UserData.RegisterType<LuaCsHook>();
            var hookDesc = (StandardUserDataDescriptor)hookType;
            typeof(LuaCsHook).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).ToList().ForEach(m => {
                if (
                    m.Name.Contains("HookMethod") ||
                    m.Name.Contains("UnhookMethod") ||
                    m.Name.Contains("EnqueueFunction") ||
                    m.Name.Contains("EnqueueTimedFunction")
                )
                {
                    hookDesc.AddMember(m.Name, new MethodMemberDescriptor(m, InteropAccessMode.Default));
                }
            });
        }

        private ModuleBuilder CreateModuleBuilder()
        {
            var assemblyName = $"LuaCsHookPatch-{Guid.NewGuid():N}";
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.RunAndCollect);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule("LuaCsHookPatch");

            // This code emits the Roslyn attribute
            // "IgnoresAccessChecksToAttribute" so we can freely access
            // the Barotrauma assembly from our dynamic patches.
            // This is important because the generated IL references
            // non-public types/members.

            // class IgnoresAccessChecksToAttribute {
            var typeBuilder = moduleBuilder.DefineType(
                name: "System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute",
                attr: TypeAttributes.NotPublic | TypeAttributes.Sealed | TypeAttributes.Class,
                parent: typeof(Attribute));

            // [AttributeUsage(AllowMultiple = true)]
            var attributeUsageAttribute = new CustomAttributeBuilder(
                con: typeof(AttributeUsageAttribute).GetConstructor(new[] { typeof(AttributeTargets) }),
                constructorArgs: new object[] { AttributeTargets.Assembly },
                namedProperties: new[] { typeof(AttributeUsageAttribute).GetProperty("AllowMultiple") },
                propertyValues: new object[] { true });
            typeBuilder.SetCustomAttribute(attributeUsageAttribute);

            // private readonly string assemblyName;
            var attributeTypeFieldBuilder = typeBuilder.DefineField(
                fieldName: "assemblyName",
                type: typeof(string),
                attributes: FieldAttributes.Private | FieldAttributes.InitOnly);

            var ctor = Emit.BuildConstructor(
                parameterTypes: new[] { typeof(string) },
                type: typeBuilder,
                attributes: MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                callingConvention: CallingConventions.Standard | CallingConventions.HasThis);
            // IL: this.assemblyName = arg;
            ctor.LoadArgument(0);
            ctor.LoadArgument(1);
            ctor.StoreField(attributeTypeFieldBuilder);
            ctor.Return();
            ctor.CreateConstructor();

            // public string AttributeName => this.assemblyName;
            var attributeNameGetter = Emit.BuildMethod(
                returnType: typeof(string),
                parameterTypes: new Type[0],
                type: typeBuilder,
                name: "get_AttributeName",
                attributes: MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                callingConvention: CallingConventions.Standard | CallingConventions.HasThis);
            attributeNameGetter.LoadArgument(0);
            attributeNameGetter.LoadField(attributeTypeFieldBuilder);
            attributeNameGetter.Return();

            var attributeName = typeBuilder.DefineProperty(
                name: "AttributeName",
                attributes: PropertyAttributes.None,
                returnType: typeof(string),
                parameterTypes: null);
            attributeName.SetGetMethod(attributeNameGetter.CreateMethod());
            // }

            var type = typeBuilder.CreateTypeInfo().AsType();

            // The assembly names are hardcoded, otherwise it would
            // break unit tests.
            var assembliesToExpose = new[] { "Barotrauma", "DedicatedServer" };
            foreach (var name in assembliesToExpose)
            {
                var attr = new CustomAttributeBuilder(
                    con: type.GetConstructor(new[] { typeof(string)}),
                    constructorArgs: new[] { name });
                assemblyBuilder.SetCustomAttribute(attr);
            }

            return moduleBuilder;
        }

        public void Add(string name, LuaCsFunc func, ACsMod owner = null) => Add(name, name, func, owner);

        public void Add(string name, string identifier, LuaCsFunc func, ACsMod owner = null)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (identifier == null) throw new ArgumentNullException(nameof(identifier));
            if (func == null) throw new ArgumentNullException(nameof(func));

            name = NormalizeIdentifier(name);
            identifier = NormalizeIdentifier(identifier);

            if (!hookFunctions.ContainsKey(name))
            {
                hookFunctions.Add(name, new Dictionary<string, (LuaCsHookCallback, ACsMod)>());
            }

            hookFunctions[name][identifier] = (new LuaCsHookCallback(name, identifier, func), owner);
        }

        public bool Exists(string name, string identifier)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (identifier == null) throw new ArgumentNullException(nameof(identifier));

            name = NormalizeIdentifier(name);
            identifier = NormalizeIdentifier(identifier);

            if (!hookFunctions.ContainsKey(name))
            {
                return false;
            }

            return hookFunctions[name].ContainsKey(identifier);
        }

        public void Remove(string name, string identifier)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (identifier == null) throw new ArgumentNullException(nameof(identifier));

            name = NormalizeIdentifier(name);
            identifier = NormalizeIdentifier(identifier);

            if (hookFunctions.ContainsKey(name) && hookFunctions[name].ContainsKey(identifier))
            {
                hookFunctions[name].Remove(identifier);
            }
        }

        public void Clear()
        {
            harmony?.UnpatchSelf();

            foreach (var (_, patch) in registeredPatches)
            {
                // Remove references stored in our dynamic types so the generated
                // assembly can be garbage-collected.
                patch.HarmonyPrefixMethod.DeclaringType
                    .GetField(FIELD_LUACS, BindingFlags.Public | BindingFlags.Static)
                    .SetValue(null, null);
                patch.HarmonyPostfixMethod.DeclaringType
                    .GetField(FIELD_LUACS, BindingFlags.Public | BindingFlags.Static)
                    .SetValue(null, null);
            }

            hookFunctions.Clear();
            registeredPatches.Clear();
            patchModuleBuilder = null;

            compatHookPrefixMethods.Clear();
            compatHookPostfixMethods.Clear();
        }

        private Stopwatch performanceMeasurement = new Stopwatch();

        [MoonSharpHidden]
        public T Call<T>(string name, params object[] args)
        {
            if (name == null) throw new ArgumentNullException(name);
            if (args == null) args = new object[0];

            name = NormalizeIdentifier(name);
            if (!hookFunctions.ContainsKey(name)) return default;

            T lastResult = default;

            var hooks = hookFunctions[name].ToArray();
            foreach ((string key, var tuple) in hooks)
            {
                if (tuple.Item2 != null && tuple.Item2.IsDisposed)
                {
                    hookFunctions[name].Remove(key);
                    continue;
                }

                try
                {
                    var result = tuple.Item1.func(args);

                    if (result is DynValue luaResult)
                    {
                        if (luaResult.Type == DataType.Tuple)
                        {
                            bool replaceNil = luaResult.Tuple.Length > 1 && luaResult.Tuple[1].CastToBool();

                            if (!luaResult.Tuple[0].IsNil() || replaceNil)
                            {
                                lastResult = luaResult.ToObject<T>();
                            }
                        }
                        else if (!luaResult.IsNil())
                        {
                            lastResult = luaResult.ToObject<T>();
                        }
                    }
                    else
                    {
                        lastResult = (T)result;
                    }
                }
                catch (Exception e)
                {
                    var argsSb = new StringBuilder();
                    foreach (var arg in args)
                    {
                        argsSb.Append(arg + " ");
                    }
                    LuaCsLogger.LogError($"Error in Hook '{name}'->'{key}', with args '{argsSb}':\n{e}", LuaCsMessageOrigin.Unknown);
                    LuaCsLogger.HandleException(e, LuaCsMessageOrigin.Unknown);
                }
            }

            return lastResult;
        }

        public object Call(string name, params object[] args) => Call<object>(name, args);

        private static MethodBase ResolveMethod(string className, string methodName, string[] parameters)
        {
            var classType = GameMain.LuaCs.PluginManagementService.GetType(className);
            if (classType == null) throw new ScriptRuntimeException($"invalid class name '{className}'");

            const BindingFlags BINDING_FLAGS = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            MethodBase method = null;

            try
            {
                if (parameters != null)
                {
                    Type[] parameterTypes = new Type[parameters.Length];

                    for (int i = 0; i < parameters.Length; i++)
                    {
                        Type type = GameMain.LuaCs.PluginManagementService.GetType(parameters[i]);
                        if (type == null)
                        {
                            throw new ScriptRuntimeException($"invalid parameter type '{parameters[i]}'");
                        }
                        parameterTypes[i] = type;
                    }

                    method = methodName switch
                    {
                        ".cctor" => classType.TypeInitializer,
                        ".ctor" => classType.GetConstructors(BINDING_FLAGS)
                            .Except(new[] { classType.TypeInitializer })
                            .Where(x => x.GetParameters().Select(x => x.ParameterType).SequenceEqual(parameterTypes))
                            .SingleOrDefault(),
                        _ => classType.GetMethod(methodName, BINDING_FLAGS, null, parameterTypes, null),
                    };
                }
                else
                {
                    ConstructorInfo GetCtor()
                    {
                        var ctors = classType.GetConstructors(BINDING_FLAGS)
                            .Except(new[] { classType.TypeInitializer })
                            .GetEnumerator();

                        if (!ctors.MoveNext()) return null;
                        var ctor = ctors.Current;

                        if (ctors.MoveNext()) throw new AmbiguousMatchException();
                        return ctor;
                    }

                    method = methodName switch
                    {
                        ".cctor" => throw new ScriptRuntimeException("type initializers can't have parameters"),
                        ".ctor" => GetCtor(),
                        _ => classType.GetMethod(methodName, BINDING_FLAGS),
                    };
                }
            }
            catch (AmbiguousMatchException)
            {
                throw new ScriptRuntimeException("ambiguous method signature");
            }

            if (method == null)
            {
                var parameterNamesStr = parameters == null ? "" : string.Join(", ", parameters);
                throw new ScriptRuntimeException($"method '{methodName}({parameterNamesStr})' not found in class '{className}'");
            }

            return method;
        }

        private class DynamicParameterMapping
        {
            public DynamicParameterMapping(string name, Type originalMethodParamType, Type harmonyPatchParamType)
            {
                ParameterName = name;
                OriginalMethodParamType = originalMethodParamType;
                HarmonyPatchParamType = harmonyPatchParamType;
            }

            public string ParameterName { get; set; }

            public Type OriginalMethodParamType { get; set; }

            public Type HarmonyPatchParamType { get; set; }
        }

        private static readonly Regex InvalidIdentifierCharsRegex = new Regex(@"[^\w\d]", RegexOptions.Compiled);

        private const string FIELD_LUACS = "LuaCs";

        // If you need to debug this:
        //   - use https://sharplab.io ; it's a very useful for resource for writing IL by hand.
        //   - use il.NewMessage("") or il.WriteLine("") to see where the IL crashes at runtime.
        private MethodInfo CreateDynamicHarmonyPatch(string identifier, MethodBase original, HookMethodType hookType)
        {
            var parameters = new List<DynamicParameterMapping>
            {
                new DynamicParameterMapping("__originalMethod", null, typeof(MethodBase)),
                new DynamicParameterMapping("__instance", null, typeof(object)),
            };

            var hasReturnType = original is MethodInfo mi && mi.ReturnType != typeof(void);
            if (hasReturnType)
            {
                parameters.Add(new DynamicParameterMapping("__result", null, typeof(object).MakeByRefType()));
            }

            foreach (var parameter in original.GetParameters())
            {
                var paramName = parameter.Name;
                var originalMethodParamType = parameter.ParameterType;
                var harmonyPatchParamType = originalMethodParamType.IsByRef
                    ? originalMethodParamType
                    // Make all parameters modifiable by the harmony patch
                    : originalMethodParamType.MakeByRefType();
                parameters.Add(new DynamicParameterMapping(paramName, originalMethodParamType, harmonyPatchParamType));
            }

            static string MangleName(object o) => InvalidIdentifierCharsRegex.Replace(o?.ToString(), "_");

            var moduleBuilder = patchModuleBuilder.Value;
            var mangledName = original.DeclaringType != null
                ? $"{MangleName(original.DeclaringType)}-{MangleName(original)}"
                : MangleName(original);
            var typeBuilder = moduleBuilder.DefineType($"Patch_{identifier}_{Guid.NewGuid():N}_{mangledName}", TypeAttributes.Public);

            var luaCsField = typeBuilder.DefineField(FIELD_LUACS, typeof(LuaCsSetup), FieldAttributes.Public | FieldAttributes.Static);

            var methodName = hookType == HookMethodType.Before ? "HarmonyPrefix" : "HarmonyPostfix";
            var il = Emit.BuildMethod(
                returnType: hookType == HookMethodType.Before ? typeof(bool) : typeof(void),
                parameterTypes: parameters.Select(x => x.HarmonyPatchParamType).ToArray(),
                type: typeBuilder,
                name: methodName,
                attributes: MethodAttributes.Public | MethodAttributes.Static,
                callingConvention: CallingConventions.Standard);

            var labelReturn = il.DefineLabel("endOfFunction");

            il.BeginExceptionBlock(out var exceptionBlock);

            // IL: var harmonyReturnValue = true;
            var harmonyReturnValue = il.DeclareLocal<bool>("harmonyReturnValue");
            il.LoadConstant(true);
            il.StoreLocal(harmonyReturnValue);

            // IL: var patchKey = MethodKey.Create(__originalMethod);
            var patchKey = il.DeclareLocal<MethodKey>("patchKey");
            il.LoadArgument(0); // load __originalMethod
            il.CastClass<MethodBase>();
            il.Call(typeof(MethodKey).GetMethod(nameof(MethodKey.Create)));
            il.StoreLocal(patchKey);

            // IL: var patchExists = instance.registeredPatches.TryGetValue(patchKey, out MethodPatches patches)
            var patchExists = il.DeclareLocal<bool>("patchExists");
            var patches = il.DeclareLocal<PatchedMethod>("patches");
            il.LoadField(typeof(LuaCsHook).GetField(nameof(instance), BindingFlags.NonPublic | BindingFlags.Static));
            il.LoadField(typeof(LuaCsHook).GetField(nameof(registeredPatches), BindingFlags.NonPublic | BindingFlags.Instance));
            il.LoadLocal(patchKey);
            il.LoadLocalAddress(patches); // out parameter
            il.Call(typeof(Dictionary<MethodKey, PatchedMethod>).GetMethod("TryGetValue"));
            il.StoreLocal(patchExists);

            // IL: if (!patchExists)
            il.LoadLocal(patchExists);
            il.IfNot((il) =>
            {
                // XXX: if we get here, it's probably because a patched
                // method was running when `reloadlua` was executed.
                // This can happen with a postfix on
                // `Barotrauma.Networking.GameServer#Update`.
                il.Leave(labelReturn);
            });

            // IL: var parameterDict = new Dictionary<string, object>(<paramCount>);
            var parameterDict = il.DeclareLocal<Dictionary<string, object>>("parameterDict");
            il.LoadConstant(parameters.Count(x => x.OriginalMethodParamType != null)); // preallocate the dictionary using the # of args
            il.NewObject(typeof(Dictionary<string, object>), typeof(int));
            il.StoreLocal(parameterDict);

            for (ushort i = 0; i < parameters.Count; i++)
            {
                // Skip parameters that don't exist in the original method
                if (parameters[i].OriginalMethodParamType == null) continue;

                // IL: parameterDict.Add(<paramName>, <paramValue>);
                il.LoadLocal(parameterDict);
                il.LoadConstant(parameters[i].ParameterName);
                il.LoadArgument(i);
                il.ToObject(parameters[i].HarmonyPatchParamType);
                il.Call(typeof(Dictionary<string, object>).GetMethod("Add"));
            }

            // IL: var ptable = new ParameterTable(parameterDict);
            var ptable = il.DeclareLocal<ParameterTable>("ptable");
            il.LoadLocal(parameterDict);
            il.NewObject(typeof(ParameterTable), typeof(Dictionary<string, object>));
            il.StoreLocal(ptable);

            if (hasReturnType && hookType == HookMethodType.After)
            {
                // IL: ptable.OriginalReturnValue = __result;
                il.LoadLocal(ptable);
                il.LoadArgument(2); // ref __result
                il.ToObject(parameters[2].HarmonyPatchParamType);
                il.Call(typeof(ParameterTable).GetProperty(nameof(ParameterTable.OriginalReturnValue)).GetSetMethod(nonPublic: true));
            }

            // IL: var enumerator = patches.GetPrefixEnumerator();
            var enumerator = il.DeclareLocal<IEnumerator<LuaCsPatch>>("enumerator");
            il.LoadLocal(patches);
            il.CallVirtual(typeof(PatchedMethod).GetMethod(
                name: hookType == HookMethodType.Before
                    ? nameof(PatchedMethod.GetPrefixEnumerator)
                    : nameof(PatchedMethod.GetPostfixEnumerator),
                bindingAttr: BindingFlags.Public | BindingFlags.Instance));
            il.StoreLocal(enumerator);

            var labelUpdateParameters = il.DefineLabel("updateParameters");

            // Iterate over prefixes/postfixes
            il.ForEachEnumerator<LuaCsPatch>(enumerator, (il, current, labelLeave) =>
            {
                // IL: var luaReturnValue = current.PatchFunc.Invoke(__instance, ptable);
                var luaReturnValue = il.DeclareLocal<DynValue>("luaReturnValue");
                il.LoadLocal(current);
                il.Call(typeof(LuaCsPatch).GetProperty(nameof(LuaCsPatch.PatchFunc)).GetGetMethod());
                il.LoadArgument(1); // __instance
                il.LoadLocal(ptable);
                il.CallVirtual(typeof(LuaCsPatchFunc).GetMethod("Invoke"));
                il.StoreLocal(luaReturnValue);

                if (hasReturnType)
                {
                    // IL: var ptableReturnValue = ptable.ReturnValue;
                    var ptableReturnValue = il.DeclareLocal<object>("ptableReturnValue");
                    il.LoadLocal(ptable);
                    il.Call(typeof(ParameterTable).GetProperty(nameof(ParameterTable.ReturnValue)).GetGetMethod());
                    il.StoreLocal(ptableReturnValue);

                    // IL: if (ptableReturnValue != null)
                    il.LoadLocal(ptableReturnValue);
                    il.If((il) =>
                    {
                        // IL: __result = ptableReturnValue;
                        il.LoadArgument(2); // ref __result
                        il.LoadLocal(ptableReturnValue);
                        il.StoreIndirect(typeof(object));
                        il.Break();
                    });

                    // IL: if (luaReturnValue != null)
                    il.LoadLocal(luaReturnValue);
                    il.If((il) =>
                    {
                        // IL: if (!luaReturnValue.IsVoid())
                        il.LoadLocal(luaReturnValue);
                        il.Call(typeof(DynValue).GetMethod(nameof(DynValue.IsVoid)));
                        il.IfNot((il) =>
                        {
                            // IL: var csReturnType = Type.GetTypeFromHandle(<original.ReturnType>);
                            var csReturnType = il.DeclareLocal<Type>("csReturnType");
                            il.LoadType(((MethodInfo)original).ReturnType);
                            il.StoreLocal(csReturnType);

                            // IL: var csReturnValue = luaReturnValue.ToObject(csReturnType);
                            var csReturnValue = il.DeclareLocal<object>("csReturnValue");
                            il.LoadLocal(luaReturnValue);
                            il.LoadLocal(csReturnType);
                            il.Call(typeof(DynValue).GetMethod(
                                name: nameof(DynValue.ToObject),
                                bindingAttr: BindingFlags.Public | BindingFlags.Instance,
                                binder: null,
                                types: new Type[] { typeof(Type) },
                                modifiers: null));
                            il.StoreLocal(csReturnValue);

                            // IL: __result = csReturnValue;
                            il.LoadArgument(2); // ref __result
                            il.LoadLocal(csReturnValue);
                            il.StoreIndirect(typeof(object));
                        });
                    });
                }

                // IL: if (ptable.PreventExecution)
                il.LoadLocal(ptable);
                il.Call(typeof(ParameterTable).GetProperty(nameof(ParameterTable.PreventExecution)).GetGetMethod());
                il.If((il) =>
                {
                    // IL: harmonyReturnValue = false;
                    il.LoadConstant(false);
                    il.StoreLocal(harmonyReturnValue);

                    // IL: break;
                    il.Leave(labelLeave);
                });
            });

            // IL: var modifiedParameters = ptable.ModifiedParameters;
            var modifiedParameters = il.DeclareLocal<Dictionary<string, object>>("modifiedParameters");
            il.LoadLocal(ptable);
            il.Call(typeof(ParameterTable).GetProperty(nameof(ParameterTable.ModifiedParameters)).GetGetMethod());
            il.StoreLocal(modifiedParameters);
            // IL: object modifiedValue;
            var modifiedValue = il.DeclareLocal<object>("modifiedValue");

            // Update the parameters
            for (ushort i = 0; i < parameters.Count; i++)
            {
                // Skip parameters that don't exist in the original method
                if (parameters[i].OriginalMethodParamType == null) continue;

                // IL: if (modifiedParameters.TryGetValue("parameterName", out modifiedValue))
                il.LoadLocal(modifiedParameters);
                il.LoadConstant(parameters[i].ParameterName);
                il.LoadLocalAddress(modifiedValue); // out parameter
                il.Call(typeof(Dictionary<string, object>).GetMethod(nameof(Dictionary<string, object>.TryGetValue)));
                il.If((il) =>
                {
                    // XXX: GetElementType() gets the "real" type behind
                    // the ByRef. This is safe because all the parameters
                    // are made into ByRef to support modification.
                    var paramType = parameters[i].HarmonyPatchParamType.GetElementType();

                    // IL: ref argName = modifiedValue;
                    il.LoadArgument(i);
                    il.LoadLocalAndCast(modifiedValue, paramType);
                    if (paramType.IsValueType)
                    {
                        il.StoreObject(paramType);
                    }
                    else
                    {
                        il.StoreIndirect(paramType);
                    }
                });
            }

            il.MarkLabel(labelReturn);

            // IL: catch (Exception exception)
            il.BeginCatchAllBlock(exceptionBlock, out var catchBlock);
            var exception = il.DeclareLocal<Exception>("exception");
            il.StoreLocal(exception);

            // IL: if (LuaCs != null)
            il.LoadField(luaCsField);
            il.If((il) =>
            {
                // IL: LuaCs.HandleException(exception, LuaCsMessageOrigin.LuaMod);
                il.LoadLocal(exception);
                il.LoadConstant((int)LuaCsMessageOrigin.LuaMod); // underlying enum type is int
                il.Call(typeof(LuaCsLogger).GetMethod(nameof(LuaCsLogger.HandleException), BindingFlags.Public | BindingFlags.Static));
            });

            il.EndCatchBlock(catchBlock);

            il.EndExceptionBlock(exceptionBlock);

            // Only prefixes return a bool
            if (hookType == HookMethodType.Before)
            {
                il.LoadLocal(harmonyReturnValue);
            }
            il.Return();

            var method = il.CreateMethod();
            for (var i = 0; i < parameters.Count; i++)
            {
                method.DefineParameter(i + 1, ParameterAttributes.None, parameters[i].ParameterName);
            }

            var type = typeBuilder.CreateType();
            type.GetField(FIELD_LUACS, BindingFlags.Public | BindingFlags.Static).SetValue(null, luaCs);
            return type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
        }

        private string Patch(string identifier, MethodBase method, LuaCsPatchFunc patch, HookMethodType hookType = HookMethodType.Before)
        {
            if (method == null) throw new ArgumentNullException(nameof(method));
            if (patch == null) throw new ArgumentNullException(nameof(patch));
            ValidatePatchTarget(method);

            identifier ??= Guid.NewGuid().ToString("N");
            identifier = NormalizeIdentifier(identifier);

            var patchKey = MethodKey.Create(method);
            if (!registeredPatches.TryGetValue(patchKey, out var methodPatches))
            {
                var harmonyPrefix = CreateDynamicHarmonyPatch(identifier, method, HookMethodType.Before);
                var harmonyPostfix = CreateDynamicHarmonyPatch(identifier, method, HookMethodType.After);
                harmony.Patch(method, prefix: new HarmonyMethod(harmonyPrefix), postfix: new HarmonyMethod(harmonyPostfix));
                methodPatches = registeredPatches[patchKey] = new PatchedMethod(harmonyPrefix, harmonyPostfix);
            }

            if (hookType == HookMethodType.Before)
            {
                if (methodPatches.Prefixes.Remove(identifier))
                {
                    LuaCsLogger.LogMessage($"Replacing existing prefix: {identifier}");
                }

                methodPatches.Prefixes.Add(identifier, new LuaCsPatch
                {
                    Identifier = identifier,
                    PatchFunc = patch,
                });
            }
            else if (hookType == HookMethodType.After)
            {
                if (methodPatches.Postfixes.Remove(identifier))
                {
                    LuaCsLogger.LogMessage($"Replacing existing postfix: {identifier}");
                }

                methodPatches.Postfixes.Add(identifier, new LuaCsPatch
                {
                    Identifier = identifier,
                    PatchFunc = patch,
                });
            }

            return identifier;
        }

        public string Patch(string identifier, string className, string methodName, string[] parameterTypes, LuaCsPatchFunc patch, HookMethodType hookType = HookMethodType.Before)
        {
            var method = ResolveMethod(className, methodName, parameterTypes);
            return Patch(identifier, method, patch, hookType);
        }

        public string Patch(string identifier, string className, string methodName, LuaCsPatchFunc patch, HookMethodType hookType = HookMethodType.Before)
        {
            var method = ResolveMethod(className, methodName, null);
            return Patch(identifier, method, patch, hookType);
        }

        public string Patch(string className, string methodName, string[] parameterTypes, LuaCsPatchFunc patch, HookMethodType hookType = HookMethodType.Before)
        {
            var method = ResolveMethod(className, methodName, parameterTypes);
            return Patch(null, method, patch, hookType);
        }

        public string Patch(string className, string methodName, LuaCsPatchFunc patch, HookMethodType hookType = HookMethodType.Before)
        {
            var method = ResolveMethod(className, methodName, null);
            return Patch(null, method, patch, hookType);
        }

        private bool RemovePatch(string identifier, MethodBase method, HookMethodType hookType)
        {
            if (identifier == null) throw new ArgumentNullException(nameof(identifier));
            identifier = NormalizeIdentifier(identifier);

            var patchKey = MethodKey.Create(method);
            if (!registeredPatches.TryGetValue(patchKey, out var methodPatches))
            {
                return false;
            }

            return hookType switch
            {
                HookMethodType.Before => methodPatches.Prefixes.Remove(identifier),
                HookMethodType.After => methodPatches.Postfixes.Remove(identifier),
                _ => throw new ArgumentException($"Invalid {nameof(HookMethodType)} enum value.", nameof(hookType)),
            };
        }

        public bool RemovePatch(string identifier, string className, string methodName, string[] parameterTypes, HookMethodType hookType)
        {
            var method = ResolveMethod(className, methodName, parameterTypes);
            return RemovePatch(identifier, method, hookType);
        }

        public bool RemovePatch(string identifier, string className, string methodName, HookMethodType hookType)
        {
            var method = ResolveMethod(className, methodName, null);
            return RemovePatch(identifier, method, hookType);
        }
    }
}
