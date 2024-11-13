using Barotrauma.LuaCs.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Barotrauma
{
    public interface IPerformanceData
    {
        public string Identifier { get; }
        public long ElapsedTicks { get; }
    }

    public class SimplePerformanceData : IPerformanceData
    {
        public string Identifier { get; }
        public long ElapsedTicks { get; }

        public SimplePerformanceData(string identifier, long elapsedTicks)
        {
            Identifier = identifier;
            ElapsedTicks = elapsedTicks;
        }
    }

    public class PerformanceCounterService : IService
    {
        public bool EnablePerformanceCounter = false;

        private Dictionary<string, List<IPerformanceData>> _data = new Dictionary<string, List<IPerformanceData>>();

        public void AddElapsedTicks(IPerformanceData data)
        {
            if (EnablePerformanceCounter) { return; }

            if (!_data.ContainsKey(data.Identifier))
            {
                _data.Add(data.Identifier, new List<IPerformanceData>());
            }

            _data[data.Identifier].Add(data);

            Trim(data.Identifier, 100);
        }

        public PerformanceData GetLatestSnapshot<PerformanceData>(string identifier) where PerformanceData : IPerformanceData
        {
            if (!_data.ContainsKey(identifier)) { return default; }

            return (PerformanceData)_data[identifier].Last();
        }

        public PerformanceData[] GetSnapshot<PerformanceData>(string identifier, int length) where PerformanceData : IPerformanceData
        {
            if (!_data.ContainsKey(identifier)) { return new PerformanceData[] { }; }

            length = Math.Min(length, _data[identifier].Count);

            return _data[identifier].GetRange(_data[identifier].Count - length, length).Cast<PerformanceData>().ToArray();
        }

        public void Trim(string identifier, int maxSize)
        {
            if (!_data.ContainsKey(identifier)) { return; }

            if (_data[identifier].Count > maxSize)
            {
                _data[identifier].RemoveRange(0, _data[identifier].Count - maxSize);
            }
        }

        public FluentResults.Result Reset()
        {
            _data = new Dictionary<string, List<IPerformanceData>>();
            return FluentResults.Result.Ok();
        }

        public void Dispose() { }
    }
}
