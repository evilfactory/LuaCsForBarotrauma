using System;

namespace Barotrauma.LuaCs.Configuration;

public interface IConfigRangeEntry<T> : IConfigEntry<T> where T : IConvertible, IEquatable<T>
{
    T MinValue { get; }
    T MaxValue { get; }
    
    int GetStepCount();
    float GetRangeMin();
    float GetRangeMax();
}
