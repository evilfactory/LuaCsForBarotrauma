using System.Collections.Generic;
using Barotrauma.LuaCs.Configuration;
using Microsoft.Xna.Framework;

namespace Barotrauma.LuaCs.Services.Safe;

public interface ILuaConfigService : ILuaService
{
    // get values
    bool TryGetConfigBool(string packageName, string configName, out bool value);
    bool TryGetConfigInt(string packageName, string configName, out int value);
    bool TryGetConfigFloat(string packageName, string configName, out float value);
    bool TryGetConfigNumber(string packageName, string configName, out double value);
    bool TryGetConfigString(string packageName, string configName, out string value);
    bool TryGetConfigVector2(string packageName, string configName, out Vector2 value);
    bool TryGetConfigVector3(string packageName, string configName, out Vector3 value);
    bool TryGetConfigColor(string packageName, string configName, out Color value);
    bool TryGetConfigList(string packageName, string configName, out IReadOnlyList<string> value);
    // set values
    void SetConfigBool(string packageName, string configName, bool value);
    void SetConfigInt(string packageName, string configName, int value);
    void SetConfigFloat(string packageName, string configName, float value);
    void SetConfigNumber(string packageName, string configName, double value);
    void SetConfigString(string packageName, string configName, string value);
    void SetConfigVector2(string packageName, string configName, Vector2 value);
    void SetConfigVector3(string packageName, string configName, Vector3 value);
    void SetConfigColor(string packageName, string configName, Color value);
    void SetConfigList(string packageName, string configName, string value);
    // profiles
    bool TryApplyProfileSettings(string packageName, string profileName);

}
