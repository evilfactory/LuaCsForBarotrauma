using System;
using System.Xml.Linq;
using Barotrauma.LuaCs.Data;
using OneOf;

namespace Barotrauma.LuaCs.Data;

public abstract class SettingBase : ISettingBase
{
    protected SettingBase(IConfigInfo configInfo)
    {
        ConfigInfo = configInfo;
    }
    
    protected IConfigInfo ConfigInfo { get; private set; }

    public string InternalName => ConfigInfo.InternalName;
    public ContentPackage OwnerPackage => ConfigInfo.OwnerPackage;

    #if CLIENT
    public IConfigDisplayInfo GetDisplayInfo() => ConfigInfo;
    #endif
    
    public virtual bool Equals(ISettingBase other)
    {
        return other is not null && (
            ReferenceEquals(this, other) || !IsDisposed &&
            OwnerPackage == other.OwnerPackage &&
            InternalName.Equals(other.InternalName));
    }

    private int _isDisposed = 0;
    protected virtual bool IsDisposed
    {
        get => ModUtils.Threading.GetBool(ref _isDisposed);
        private set => ModUtils.Threading.SetBool(ref _isDisposed, value);
    }

    public virtual void Dispose()
    {
        if (!ModUtils.Threading.CheckIfClearAndSetBool(ref _isDisposed))
        {
            return;
        }
        
        ConfigInfo = null;
        OnValueChanged = null;
        GC.SuppressFinalize(this);
    }
    
    // -- Must be implemented
    
    public abstract Type GetValueType();
    public abstract string GetStringValue();
    public abstract string GetDefaultStringValue();

    public abstract bool TrySetValue(OneOf<string, XElement> value);
    public event Action<ISettingBase> OnValueChanged;
    public abstract OneOf<string, XElement> GetSerializableValue();
}
