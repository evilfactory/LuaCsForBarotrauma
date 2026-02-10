using System;
using System.Xml.Linq;
using Barotrauma.LuaCs.Data;
using Barotrauma.LuaCs;
using OneOf;

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
        // TODO: Replace this with a proper IFactory lookup pattern.
        // ISettingBase<T>
        RegisterSettingEntry<bool>(configService, "bool", valueChangePredicate);
        RegisterSettingEntry<byte>(configService, "byte", valueChangePredicate);
        RegisterSettingEntry<sbyte>(configService, "sbyte", valueChangePredicate);
        RegisterSettingEntry<short>(configService, "short", valueChangePredicate);
        RegisterSettingEntry<ushort>(configService, "ushort", valueChangePredicate);
        RegisterSettingEntry<int>(configService, "int", valueChangePredicate);
        RegisterSettingEntry<uint>(configService, "uint", valueChangePredicate);
        RegisterSettingEntry<long>(configService, "long", valueChangePredicate);
        RegisterSettingEntry<ulong>(configService, "ulong", valueChangePredicate);
        RegisterSettingEntry<string>(configService, "string", valueChangePredicate);
        
        // ISettingRangeBase<T>
        configService.RegisterSettingTypeInitializer("rangeInt", cfgInfo =>
        {
            return new SettingRangeInt.RangeFactory().CreateInstance(cfgInfo.Info, (val) =>
                IsValueChangeAllowed(cfgInfo.Info, val, valueChangePredicate));
        });
        
        configService.RegisterSettingTypeInitializer("rangeFloat", cfgInfo =>
        {
            return new SettingRangeFloat.RangeFactory().CreateInstance(cfgInfo.Info, (val) =>
                IsValueChangeAllowed(cfgInfo.Info, val, valueChangePredicate));
        });
        
        // ISettingList : Not Implemented yet
    }

    private void RegisterSettingEntry<T>(IConfigService configService, string typeName, Func<OneOf<string, XElement, object>, bool> valueChangePredicate) where T : IEquatable<T>, IConvertible
    {
        configService.RegisterSettingTypeInitializer(typeName, cfgInfo =>
        {
            return new SettingEntry<T>.Factory().CreateInstance(cfgInfo.Info, (val) =>
                IsValueChangeAllowed(cfgInfo.Info, val, valueChangePredicate));
        });
    }

    private bool IsValueChangeAllowed(IConfigInfo info, OneOf<string, XElement, object> newValue,
        Func<OneOf<string, XElement, object>, bool> valueChangePredicate)
    {
#if CLIENT
        return !info.Element.GetAttributeBool("ReadOnly", false)
               || info.EditableStates < _infoProvider.CurrentRunState 
               || valueChangePredicate is null 
               || valueChangePredicate.Invoke(newValue);
#else
        // Server has absolute authority.
        return true;
#endif
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
