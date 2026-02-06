using System;
using System.Xml.Linq;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs;

namespace Barotrauma.LuaCs.Data;

public interface ISettingsRegistrationProvider : IService
{
    void RegisterTypeProviders(IConfigService configService, Func<OneOf<string, XElement, object>, bool> valueChangePredicate);
}

public class SettingsEntryRegistrar : ISettingsRegistrationProvider
{
    private ILuaCsInfoProvider _infoProvider;

    public SettingsEntryRegistrar(ILuaCsInfoProvider infoProvider)
    {
        _infoProvider = infoProvider;
    }

    public void RegisterTypeProviders(IConfigService configService, Func<OneOf<string, XElement, object>, bool> valueChangePredicate)
    {
        // ISettingBase<T>
        RegisterSettingEntry<bool>(configService, "bool");
        RegisterSettingEntry<byte>(configService, "byte");
        RegisterSettingEntry<sbyte>(configService, "sbyte");
        RegisterSettingEntry<short>(configService, "short");
        RegisterSettingEntry<ushort>(configService, "ushort");
        RegisterSettingEntry<int>(configService, "int");
        RegisterSettingEntry<uint>(configService, "uint");
        RegisterSettingEntry<long>(configService, "long");
        RegisterSettingEntry<ulong>(configService, "ulong");
        RegisterSettingEntry<string>(configService, "string");
        // ISettingRangeBase<T>
        // ISettingList
    }

    private void RegisterSettingEntry<T>(IConfigService configService, string typeName) where T : IEquatable<T>, IConvertible
    {
        configService.RegisterSettingTypeInitializer(typeName, cfgInfo =>
        {
            return new SettingEntry<bool>.Factory().CreateInstance(cfgInfo.Info, (val) =>
            {
                return !cfgInfo.Info.Element.GetAttributeBool("ReadOnly", false)
                       && cfgInfo.Info.EditableStates.HasFlag(_infoProvider?.CurrentRunState ?? RunState.Running);
            });
        });
    }

    public void Dispose()
    {
        if (!ModUtils.Threading.CheckIfClearAndSetBool(ref _isDisposed))
        {
            return;
        }
        _infoProvider.Dispose();
        _infoProvider = null;
    }
    
    private int _isDisposed;
    public bool IsDisposed
    {
        get => ModUtils.Threading.GetBool(ref _isDisposed);
        private set => ModUtils.Threading.SetBool(ref _isDisposed, value);
    }
}
