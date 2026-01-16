
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Interop;

namespace Barotrauma
{
    internal class LuaUserData
    {
        [Obsolete("Use IPluginManagementService::GetTypesByName()")]
        public static Type GetType(string typeName) => throw new NotImplementedException(); //LuaCsSetup.GetType(typeName);

        public static IUserDataDescriptor RegisterType(string typeName)
        {
            Type type = GetType(typeName);

            if (type == null)
            {
                throw new ScriptRuntimeException($"tried to register a type that doesn't exist: {typeName}.");
            }

            return UserData.RegisterType(type);
        }

        public static void RegisterExtensionType(string typeName)
        {
            Type type = GetType(typeName);

            if (type == null)
            {
                throw new ScriptRuntimeException($"tried to register a type that doesn't exist: {typeName}.");
            }

            UserData.RegisterExtensionType(type);
        }

        public static bool IsRegistered(string typeName)
        {
            Type type = GetType(typeName);

            if (type == null)
            {
                return false;
            }

            return UserData.GetDescriptorForType(type, true) != null;
        }

        public static void UnregisterType(string typeName, bool deleteHistory = false)
        {
            Type type = GetType(typeName);

            if (type == null)
            {
                throw new ScriptRuntimeException($"tried to unregister a type that doesn't exist: {typeName}.");
            }

            UserData.UnregisterType(type, deleteHistory);
        }
        public static IUserDataDescriptor RegisterGenericType(string typeName, params string[] typeNameArguements)
        {
            Type type = GetType(typeName);
            Type[] typeArguements = typeNameArguements.Select(x => GetType(x)).ToArray();
            Type genericType = type.MakeGenericType(typeArguements);
            return UserData.RegisterType(genericType);
        }

        public static void UnregisterGenericType(string typeName, params string[] typeNameArguements)
        {
            Type type = GetType(typeName);
            Type[] typeArguements = typeNameArguements.Select(x => GetType(x)).ToArray();
            Type genericType = type.MakeGenericType(typeArguements);
            UserData.UnregisterType(genericType);
        }

        public static bool IsTargetType(object obj, string typeName)
        {
            if (obj == null) { throw new ScriptRuntimeException("userdata is nil");  }
            Type targetType = GetType(typeName);
            if (targetType == null) { throw new ScriptRuntimeException("target type not found"); }

            Type type = obj is Type ? (Type)obj : obj.GetType();
            return targetType.IsAssignableFrom(type);
        }

        public static string TypeOf(object obj)
        {
            if (obj == null) { throw new ScriptRuntimeException("userdata is nil"); }

            return obj.GetType().FullName;
        }

        public static object CreateStatic(string typeName)
        {
            Type type = GetType(typeName);

            if (type == null)
            {
                throw new ScriptRuntimeException($"tried to create a static userdata of a type that doesn't exist: {typeName}.");
            }

            MethodInfo method = typeof(UserData).GetMethod(nameof(UserData.CreateStatic), 1, new Type[0]);
            MethodInfo generic = method.MakeGenericMethod(type);
            return generic.Invoke(null, null);
        }

        public static object CreateEnumTable(string typeName)
        {
            Type type = GetType(typeName);

            if (type == null)
            {
                throw new ScriptRuntimeException($"tried to create an enum table with a type that doesn't exist:: {typeName}.");
            }

            Dictionary<string, object> result = new Dictionary<string, object>();

            foreach (var value in Enum.GetValues(type))
            {
                string name = Enum.GetName(type, value);

                result[name] = value;
            }

            return result;
        }

        private static FieldInfo FindFieldRecursively(Type type, string fieldName)
        {
            var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

            if (field == null && type.BaseType != null)
            {
                return FindFieldRecursively(type.BaseType, fieldName);
            }

            return field;
        }

        public static void MakeFieldAccessible(IUserDataDescriptor IUUD, string fieldName)
        {
            if (IUUD == null)
            {
                throw new ScriptRuntimeException($"tried to use a UserDataDescriptor that is null to make {fieldName} accessible.");
            }

            var descriptor = (StandardUserDataDescriptor)IUUD;
            FieldInfo field = FindFieldRecursively(IUUD.Type, fieldName);

            if (field == null)
            {
                throw new ScriptRuntimeException($"tried to make field '{fieldName}' accessible, but the field doesn't exist.");
            }

            descriptor.RemoveMember(fieldName);
            descriptor.AddMember(fieldName, new FieldMemberDescriptor(field, InteropAccessMode.Default));
        }

        private static MethodInfo FindMethodRecursively(Type type, string methodName, Type[] types = null)
        {
            MethodInfo method;

            if (types == null)
            {
                method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            }
            else
            {
                method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, types);
            }

            if (method == null && type.BaseType != null)
            {
                return FindMethodRecursively(type.BaseType, methodName, types);
            }

            return method;
        }

        public static void MakeMethodAccessible(IUserDataDescriptor IUUD, string methodName, string[] parameters = null)
        {
            if (IUUD == null)
            {
                throw new ScriptRuntimeException($"tried to use a UserDataDescriptor that is null to make {methodName} accessible.");
            }

            Type[] parameterTypes = null;


            if (parameters != null)
            {
                parameterTypes = new Type[parameters.Length];

                for (int i = 0; i < parameters.Length; i++)
                {
                    Type type = LuaUserData.GetType(parameters[i]);
                    if (type == null)
                    {
                        throw new ScriptRuntimeException($"invalid parameter type '{parameters[i]}'");
                    }
                    parameterTypes[i] = type;
                }
            }

            var descriptor = (StandardUserDataDescriptor)IUUD;

            MethodBase method;

            try
            {
                method = FindMethodRecursively(IUUD.Type, methodName, parameterTypes);
            }
            catch (AmbiguousMatchException ex)
            {
                throw new ScriptRuntimeException("ambiguous method signature.");
            }

            if (method == null)
            {
                throw new ScriptRuntimeException($"tried to make method '{methodName}' accessible, but the method doesn't exist.");
            }

            descriptor.AddMember(methodName, new MethodMemberDescriptor(method, InteropAccessMode.Default));
        }

        private static PropertyInfo FindPropertyRecursively(Type type, string propertyName)
        {
            var property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

            if (property == null && type.BaseType != null)
            {
                return FindPropertyRecursively(type.BaseType, propertyName);
            }

            return property;
        }

        public static void MakePropertyAccessible(IUserDataDescriptor IUUD, string propertyName)
        {
            if (IUUD == null)
            {
                throw new ScriptRuntimeException($"tried to use a UserDataDescriptor that is null to make {propertyName} accessible.");
            }

            var descriptor = (StandardUserDataDescriptor)IUUD;
            PropertyInfo property = FindPropertyRecursively(IUUD.Type, propertyName);

            if (property == null)
            {
                throw new ScriptRuntimeException($"tried to make property '{propertyName}' accessible, but the property doesn't exist.");
            }

            descriptor.RemoveMember(propertyName);
            descriptor.AddMember(propertyName, new PropertyMemberDescriptor(property, InteropAccessMode.Default, property.GetGetMethod(true), property.GetSetMethod(true)));
        }

        public static void AddMethod(IUserDataDescriptor IUUD, string methodName, object function)
        {
            if (IUUD == null)
            {
                throw new ScriptRuntimeException($"tried to use a UserDataDescriptor that is null to add method {methodName}.");
            }

            var descriptor = (StandardUserDataDescriptor)IUUD;

            descriptor.RemoveMember(methodName);
            descriptor.AddMember(methodName, new ObjectCallbackMemberDescriptor(methodName, (object arg1, ScriptExecutionContext arg2, CallbackArguments arg3) =>
            {
                if (GameMain.LuaCs != null)
                    return GameMain.LuaCs.CallLuaFunction(function, arg3.GetArray());
                return null;
            }));
        }

        public static void AddField(IUserDataDescriptor IUUD, string fieldName, DynValue value)
        {
            if (IUUD == null)
            {
                throw new ScriptRuntimeException($"tried to use a UserDataDescriptor that is null to add field {fieldName}.");
            }

            var descriptor = (StandardUserDataDescriptor)IUUD;
            descriptor.RemoveMember(fieldName);
            descriptor.AddMember(fieldName, new DynValueMemberDescriptor(fieldName, value));
        }

        public static void RemoveMember(IUserDataDescriptor IUUD, string memberName)
        {
            if (IUUD == null)
            {
                throw new ScriptRuntimeException($"tried to use a UserDataDescriptor that is null to remove the member {memberName}.");
            }

            var descriptor = (StandardUserDataDescriptor)IUUD;
            descriptor.RemoveMember(memberName);
        }

        public static bool HasMember(object obj, string memberName)
        {
            if (obj == null) { throw new ScriptRuntimeException("object is nil"); }

            Type type;
            if (obj is Type)
            {
                type = (Type)obj;
            }
            else if(obj is IUserDataDescriptor descriptor)
            {
                type = descriptor.Type;

                if (((StandardUserDataDescriptor)descriptor).HasMember(memberName))
                {
                    return true;
                }
            }
            else
            {
                type = obj.GetType();
            }

            if (type.GetMember(memberName).Length == 0)
            {
                return false;
            }

            return true;
        }


        /// <summary>
        /// See <see cref="CreateUserDataFromType"/>.
        /// </summary>
        /// <param name="scriptObject">Lua value to convert and wrap in a userdata.</param>
        /// <param name="desiredTypeDescriptor">Descriptor of the type of the object to convert the Lua value to. Uses MoonSharp ScriptToClr converters.</param>
        /// <returns>A userdata that wraps the Lua value converted to an object of the desired type as described by <paramref name="desiredTypeDescriptor"/>.</returns>
        public static DynValue CreateUserDataFromDescriptor(DynValue scriptObject, IUserDataDescriptor desiredTypeDescriptor)
        {
            return UserData.Create(scriptObject.ToObject(desiredTypeDescriptor.Type), desiredTypeDescriptor);
        }

        /// <summary>
        /// Converts a Lua value to a CLR object of a desired type and wraps it in a userdata.
        /// If the type is not registered, then a new <see cref="MoonSharp.Interpreter.Interop.StandardUserDataDescriptor"/> will be created and used.
        /// The goal of this method is to allow Lua scripts to create userdata to wrap certain data without having to register types.
        /// <remarks>Wrapping the value in a userdata preserves the original type during script-to-CLR conversions.</remarks>
        /// <example>A Lua script needs to pass a List`1 to a CLR method expecting System.Object, MoonSharp gets
        /// in the way by converting the List`1 to a MoonSharp.Interpreter.Table and breaking everything.
        /// Registering the List`1 type can break other scripts relying on default converters, so instead
        /// it is better to manually wrap the List`1 object into a userdata.
        /// </example>
        /// </summary>
        /// <param name="scriptObject">Lua value to convert and wrap in a userdata.</param>
        /// <param name="desiredType">Type describing the CLR type of the object to convert the Lua value to.</param>
        /// <returns>A userdata that wraps the Lua value converted to an object of the desired type.</returns>
        public static DynValue CreateUserDataFromType(DynValue scriptObject, Type desiredType)
        {
            IUserDataDescriptor descriptor = UserData.GetDescriptorForType(desiredType, true);
            descriptor ??= new StandardUserDataDescriptor(desiredType, InteropAccessMode.Default);
            return CreateUserDataFromDescriptor(scriptObject, descriptor);
        }
    }
}

