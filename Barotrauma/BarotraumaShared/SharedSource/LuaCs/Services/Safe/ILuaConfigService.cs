using Barotrauma.LuaCs.Configuration;
using Microsoft.Xna.Framework;

namespace Barotrauma.LuaCs.Services.Safe;

public interface ILuaConfigService : ILuaService
{
    // get
    bool GetConfigBool(string packageName, string configName);
    int GetConfigInt(string packageName, string configName);
    float GetConfigFloat(string packageName, string configName);
    double GetConfigNumber(string packageName, string configName);
    string GetConfigString(string packageName, string configName);
    Vector2 GetConfigVector2(string packageName, string configName);
    Vector3 GetConfigVector3(string packageName, string configName);
    Color GetConfigColor(string packageName, string configName);
    string GetConfigList(string packageName, string configName);
    // set
    void SetConfigBool(string packageName, string configName, bool value);
    void SetConfigInt(string packageName, string configName, int value);
    void SetConfigFloat(string packageName, string configName, float value);
    void SetConfigNumber(string packageName, string configName, double value);
    void SetConfigString(string packageName, string configName, string value);
    void SetConfigVector2(string packageName, string configName, Vector2 value);
    void SetConfigVector3(string packageName, string configName, Vector3 value);
    void SetConfigColor(string packageName, string configName, Color value);
    void SetConfigList(string packageName, string configName, string value);
}
