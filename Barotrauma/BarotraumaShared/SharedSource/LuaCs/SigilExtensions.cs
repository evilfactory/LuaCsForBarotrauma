using Microsoft.Xna.Framework;
using Sigil;
using Sigil.NonGeneric;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Barotrauma.LuaCs;

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
