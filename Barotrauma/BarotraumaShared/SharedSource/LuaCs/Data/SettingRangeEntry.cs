using System;
using System.Xml.Linq;
using Barotrauma.LuaCs.Data;
using Microsoft.Toolkit.Diagnostics;
using OneOf;

namespace Barotrauma.LuaCs.Data;

public abstract class SettingRangeBase<T> : SettingEntry<T>, ISettingRangeBase<T> where T : IEquatable<T>, IConvertible
{
    public SettingRangeBase(IConfigInfo configInfo, Func<OneOf<string, XElement, object>, bool> valueChangePredicate) : base(configInfo, valueChangePredicate)
    {
    }

    public T MinValue { get; protected init; }
    public T MaxValue { get; protected init; }
    public int IncrementalSteps { get; protected init; }
}

public class SettingRangeFloat : SettingRangeBase<float>
{
    public class RangeFactory : ISettingBase.IFactory<SettingRangeFloat>
    {
        public SettingRangeFloat CreateInstance(IConfigInfo configInfo, Func<OneOf<string, XElement, object>, bool> valueChangePredicate)
        {
            Guard.IsNotNull(configInfo, nameof(configInfo));
            return new SettingRangeFloat(configInfo, valueChangePredicate);
        }
    }
    
    public SettingRangeFloat(IConfigInfo configInfo, Func<OneOf<string, XElement, object>, bool> valueChangePredicate) : base(configInfo, valueChangePredicate)
    {
        // funny values in case they forget to set them in the config.
        MinValue = configInfo.Element.GetAttributeFloat("Min", float.MinValue);
        MaxValue = configInfo.Element.GetAttributeFloat("Max", float.MaxValue);
        IncrementalSteps = configInfo.Element.GetAttributeInt("Steps", 3);
    }

    public override bool TrySetValue(float value)
    {
        if (value > MaxValue || value < MinValue)
        {
            return false;
        }
        return base.TrySetValue(value);
    }
}

public class SettingRangeInt : SettingRangeBase<int>
{
    public class RangeFactory : ISettingBase.IFactory<SettingRangeInt>
    {
        public SettingRangeInt CreateInstance(IConfigInfo configInfo, Func<OneOf<string, XElement, object>, bool> valueChangePredicate)
        {
            Guard.IsNotNull(configInfo, nameof(configInfo));
            return new SettingRangeInt(configInfo, valueChangePredicate);
        }
    }
    
    public SettingRangeInt(IConfigInfo configInfo, Func<OneOf<string, XElement, object>, bool> valueChangePredicate) : base(configInfo, valueChangePredicate)
    {
        // funny values in case they forget to set them in the config.
        MinValue = configInfo.Element.GetAttributeInt("Min", int.MinValue);
        MaxValue = configInfo.Element.GetAttributeInt("Max", int.MaxValue);
        IncrementalSteps = configInfo.Element.GetAttributeInt("Steps", 3);
    }

    public override bool TrySetValue(int value)
    {
        if (value > MaxValue || value < MinValue)
        {
            return false;
        }
        return base.TrySetValue(value);
    }
}
