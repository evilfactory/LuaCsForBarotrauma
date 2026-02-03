using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Interop;

namespace Barotrauma
{
    partial class LuaSafeUserData
    {
        public IUserDataDescriptor this[string index]
        {
            get => LuaUserData.Descriptors.GetValueOrDefault(index);
        }

        private static bool CanBeRegistered(string typeName)
        {
            if (typeName.StartsWith("Barotrauma.Lua", StringComparison.Ordinal) ||
                typeName.StartsWith("Barotrauma.Cs", StringComparison.Ordinal) ||
                typeName.StartsWith("Barotrauma.LuaCs", StringComparison.Ordinal))
            {
                return false;
            }

            if (typeName == "System.Single") { return true; }

            if (typeName.StartsWith("System.Collections", StringComparison.Ordinal))
                return true;

            if (typeName.StartsWith("Microsoft.Xna", StringComparison.Ordinal))
                return true;

            if (typeName.StartsWith("Barotrauma.IO", StringComparison.Ordinal))
                return false;

            if (typeName.StartsWith("Barotrauma.ToolBox", StringComparison.Ordinal))
                return false;

            if (typeName.StartsWith("Barotrauma.SaveUtil", StringComparison.Ordinal))
                return false;

            if (typeName.StartsWith("Barotrauma.", StringComparison.Ordinal))
                return true;

            return false;
        }

        private static bool CanBeReRegistered(string typeName)
        {
            if (typeName.StartsWith("Barotrauma.Lua", StringComparison.Ordinal) ||
                typeName.StartsWith("Barotrauma.Cs", StringComparison.Ordinal) ||
                typeName.StartsWith("Barotrauma.LuaCs", StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }

        private static bool IsAllowed(string typeName)
        {
            if (!CanBeReRegistered(typeName) && LuaUserData.IsRegistered(typeName))
            {
                return false;
            }

            if (!CanBeRegistered(typeName) && !LuaUserData.IsRegistered(typeName))
            {
                return false;
            }

            return true;
        }

        private static void CheckAllowed(string typeName)
        {
            if (!IsAllowed(typeName))
            {
                throw new ScriptRuntimeException($"Type {typeName} can't be registered");
            }
        }

        public static Type GetType(string typeName) 
        {
            CheckAllowed(typeName);

            return LuaUserData.GetType(typeName);
        }

        public static IUserDataDescriptor RegisterType(string typeName)
        {
            CheckAllowed(typeName);

            return LuaUserData.RegisterType(typeName);
        }

        public static IUserDataDescriptor RegisterTypeBarotrauma(string typeName)
        {
            return RegisterType($"Barotrauma.{typeName}");
        }

        public static void RegisterExtensionType(string typeName)
        {
            CheckAllowed(typeName);
            LuaUserData.RegisterExtensionType(typeName);
        }

        public static bool IsRegistered(string typeName)
        {
            return LuaUserData.IsRegistered(typeName);
        }

        public static void UnregisterType(string typeName, bool deleteHistory = false)
        {
            LuaUserData.UnregisterType(typeName, deleteHistory);
        }
        public static IUserDataDescriptor RegisterGenericType(string typeName, params string[] typeNameArguements)
        {
            CheckAllowed(typeName);
            return LuaUserData.RegisterGenericType(typeName, typeNameArguements);
        }

        public static void UnregisterGenericType(string typeName, params string[] typeNameArguements)
        {
            LuaUserData.UnregisterGenericType(typeName, typeNameArguements);
        }

        public static bool IsTargetType(object obj, string typeName)
        {
            return LuaUserData.IsTargetType(obj, typeName);
        }

        public static string TypeOf(object obj)
        {
            return LuaUserData.TypeOf(obj);
        }

        public static object CreateStatic(string typeName)
        {
            CheckAllowed(typeName);
            return LuaUserData.CreateStatic(typeName);
        }

        public static object CreateEnumTable(string typeName)
        {
            return LuaUserData.CreateEnumTable(typeName);
        }

        public static void MakeFieldAccessible(IUserDataDescriptor IUUD, string fieldName)
        {
            LuaUserData.MakeFieldAccessible(IUUD, fieldName);
        }

        public static void MakeMethodAccessible(IUserDataDescriptor IUUD, string methodName, string[] parameters = null)
        {
            LuaUserData.MakeMethodAccessible(IUUD, methodName, parameters);
        }

        public static void MakePropertyAccessible(IUserDataDescriptor IUUD, string propertyName)
        {
            LuaUserData.MakePropertyAccessible(IUUD, propertyName);
        }

        public static void AddMethod(IUserDataDescriptor IUUD, string methodName, object function)
        {
            LuaUserData.AddMethod(IUUD, methodName, function);
        }

        public static void AddField(IUserDataDescriptor IUUD, string fieldName, DynValue value)
        {
            LuaUserData.AddField(IUUD, fieldName, value);
        }

        public static void RemoveMember(IUserDataDescriptor IUUD, string memberName)
        {
            LuaUserData.RemoveMember(IUUD, memberName);
        }

        public static bool HasMember(object obj, string memberName)
        {
            return LuaUserData.HasMember(obj, memberName);
        }

        public static void AddCallMetaTable(object userdata) { }

        public static DynValue CreateUserDataFromDescriptor(DynValue scriptObject, IUserDataDescriptor desiredTypeDescriptor)
        {
            return LuaUserData.CreateUserDataFromDescriptor(scriptObject, desiredTypeDescriptor);
        }

        public static DynValue CreateUserDataFromType(DynValue scriptObject, Type desiredType)
        {
            return LuaUserData.CreateUserDataFromType(scriptObject, desiredType);
        }
    }
}
