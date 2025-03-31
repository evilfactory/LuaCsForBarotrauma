using System;
using System.Collections.Generic;
using Barotrauma.LuaCs.Data;

namespace Barotrauma.LuaCs.Configuration;


/// <summary>
/// Base type of all menu displayable types.
/// </summary>
public interface IDisplayableConfigBase : IDataInfo, IConfigDisplayInfo
{
    /// <summary>
    /// Whether the current config is editable.
    /// </summary>
    bool IsEditable { get; }
    /// <summary>
    /// Used to indicate the implemented interface and targeted display logic.
    /// </summary>
    static virtual DisplayType DisplayOption => DisplayType.Undefined;
}

public interface IDisplayableConfigBase<out TDisplay, in TValue> : IDisplayableConfigBase
{
    void SetValue(TValue value);
    TDisplay GetDisplayValue();
}

public interface IDisplayableConfigBool : IDisplayableConfigBase<bool, bool>
{
    static DisplayType IDisplayableConfigBase.DisplayOption => DisplayType.Boolean;
}

public interface IDisplayableConfigText : IDisplayableConfigBase<string, string>
{
    static DisplayType IDisplayableConfigBase.DisplayOption => DisplayType.Text;
}

public interface IDisplayableConfigInt : IDisplayableConfigBase<int, int>
{
    static DisplayType IDisplayableConfigBase.DisplayOption => DisplayType.Integer;
}

public interface IDisplayableConfigFloat : IDisplayableConfigBase<float, float>
{
    static DisplayType IDisplayableConfigBase.DisplayOption => DisplayType.Float;
}

public interface IDisplayableConfigSliderInt : IDisplayableConfigBase<(int Min, int Max, int Value, int Steps), int>
{
    static DisplayType IDisplayableConfigBase.DisplayOption => DisplayType.SliderInt;
}

public interface IDisplayableConfigSliderFloat : IDisplayableConfigBase<(float Min, float Max, float Value, int Steps), float>
{
    static DisplayType IDisplayableConfigBase.DisplayOption => DisplayType.SliderFloat;
}

public interface IDisplayableConfigDropdown : IDisplayableConfigBase<List<string>, string>
{
    static DisplayType IDisplayableConfigBase.DisplayOption => DisplayType.Dropdown;
}

/// <summary>
/// Allows completely custom-designed UI for this configuration component.
/// </summary>
public interface IDisplayableConfigCustom : IDisplayableConfigBase
{
    static DisplayType IDisplayableConfigBase.DisplayOption => DisplayType.Custom;
    /// <summary>
    /// Draw your menu settings option.
    /// </summary>
    /// <param name="layoutGroup">Parent layout component.</param>
    void DrawComponent(GUILayoutGroup layoutGroup);
    /// <summary>
    /// Called when the config element is set to be disposed to allow for cleanup.
    /// </summary>
    void DisposeGUI();
    /// <summary>
    /// Called when the UI indicates to save the current value as permanent.
    /// </summary>
    void OnValueSaved();
    /// <summary>
    /// Called when the UI indicates to discard the currently displayed value and revert to the last saved value.
    /// </summary>
    void OnValueDiscarded();
}



/// <summary>
/// Indicates the intended display and feedback logic to be used by the <see cref="SettingsMenu"/>.
/// <br/><b>[Important]</b>
/// <br/>The type must implement the indicated interface for the selected option, or it will not be displayed.
/// </summary>
public enum DisplayType
{
    /// <summary>
    /// Will not be displayed in menus.
    /// </summary>
    Undefined,
    /// <summary>
    /// Will be shown as a checkbox.
    /// <br/><b>[Requires(<see cref="IDisplayableConfigBool"/>)]</b>
    /// </summary>
    Boolean,
    /// <summary>
    /// Shown as an editable text input.
    /// <br/><b>[Requires(<see cref="IDisplayableConfigText"/>)]</b>
    /// </summary>
    Text,
    /// <summary>
    /// Shown as number input (no decimal input).
    /// <br/><b>[Requires(<see cref="IDisplayableConfigInt"/>)]</b>
    /// </summary>
    Integer,
    /// <summary>
    /// Shown as a number input.
    /// <br/><b>[Requires(<see cref="IDisplayableConfigFloat"/>)]</b>
    /// </summary>
    Float,
    /// <summary>
    /// Shown as a slider, values parsed as integers.
    /// <br/><b>[Requires(<see cref="IDisplayableConfigSliderInt"/>)]</b>
    /// </summary>
    SliderInt,
    /// <summary>
    /// Shown as a slider, values parsed as single-precision decimal numbers.
    /// <br/><b>[Requires(<see cref="IDisplayableConfigSliderFloat"/>)]</b>
    /// </summary>
    SliderFloat,
    /// <summary>
    /// Shown as a <see cref="GUIDropDown"/> menu, values parsed as strings.
    /// <br/><b>[Requires(<see cref="IDisplayableConfigDropdown"/>)]</b>
    /// </summary>
    Dropdown,
    /// <summary>
    /// UI Display is implemented by inheritor and actioned by a call to <see cref="IDisplayableConfigCustom.DrawComponent"/>.
    /// <br/><b>[Requires(<see cref="IDisplayableConfigCustom"/>)]</b>
    /// </summary>
    Custom
}
