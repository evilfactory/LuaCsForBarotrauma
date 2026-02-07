using System;
using MoonSharp.Interpreter;
using Microsoft.Xna.Framework;
using FarseerPhysics.Dynamics;
using LuaCsCompatPatchFunc = Barotrauma.LuaCsPatch;
using Barotrauma.Networking;
using System.Collections.Immutable;
using Barotrauma.LuaCs;

namespace Barotrauma
{
    public class LuaConverters
    {
        private readonly Script _script;

        public LuaConverters(Script script)
        {
            _script = script;
        }

        private DynValue Call(object function, params object[] arguments) => _script.Call(function, arguments);

        public void RegisterLuaConverters()
        {
            RegisterAction<Item>();
            RegisterAction<Character>();
            RegisterAction<Character, Character>();
            RegisterAction<Entity>();
            RegisterAction<float>();
            RegisterAction();

            RegisterFunc<Fixture, Vector2, Vector2, float, float>();
            RegisterFunc<AIObjective, bool>();

            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Function, typeof(LuaCsAction), v => (LuaCsAction)(args =>
            {
                if (v.Function.OwnerScript == _script)
                {
                    Call(v.Function, args);
                }
            }));

            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Function, typeof(LuaCsFunc), v => (LuaCsFunc)(args =>
            {
                if (v.Function.OwnerScript == _script)
                {
                    return Call(v.Function, args);
                }
                return default;
            }));

            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Function, typeof(LuaCsCompatPatchFunc), v => (LuaCsCompatPatchFunc)((self, args) =>
            {
                if (v.Function.OwnerScript == _script)
                {
                    return Call(v.Function, self, args);
                }
                return default;
            }));

            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Function, typeof(LuaCsPatchFunc), v => (LuaCsPatchFunc)((self, args) =>
            {
                if (v.Function.OwnerScript == _script)
                {
                    return Call(v.Function, self, args);
                }
                return default;
            }));


            void RegisterHandler<T>(Func<Closure, T> converter) => Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Function, typeof(T), v => converter(v.Function));

            RegisterHandler(f => (Character.OnDeathHandler)((a1, a2) => Call(f, a1, a2)));
            RegisterHandler(f => (Character.OnAttackedHandler)((a1, a2) => Call(f, a1, a2)));

#if CLIENT
            RegisterAction<Microsoft.Xna.Framework.Graphics.SpriteBatch, GUICustomComponent>();
            RegisterAction<float, Microsoft.Xna.Framework.Graphics.SpriteBatch>();
            RegisterAction<Microsoft.Xna.Framework.Graphics.SpriteBatch, float>();

            {
                RegisterHandler(f => (GUIComponent.SecondaryButtonDownHandler)(
                (a1, a2) => Call(f, a1, a2)?.CastToBool() ?? default));

                RegisterHandler(f => (GUIButton.OnClickedHandler)(
                (a1, a2) => Call(f, a1, a2)?.CastToBool() ?? default));
                RegisterHandler(f => (GUIButton.OnButtonDownHandler)(
                () => Call(f)?.CastToBool() ?? default));
                RegisterHandler(f => (GUIButton.OnPressedHandler)(
                () => Call(f)?.CastToBool() ?? default));

                RegisterHandler(f => (GUIColorPicker.OnColorSelectedHandler)(
                (a1, a2) => Call(f, a1, a2)?.CastToBool() ?? default));

                RegisterHandler(f => (GUIDropDown.OnSelectedHandler)(
                (a1, a2) => Call(f, a1, a2)?.CastToBool() ?? default));

                RegisterHandler(f => (GUIListBox.OnSelectedHandler)(
                (a1, a2) => Call(f, a1, a2)?.CastToBool() ?? default));
                RegisterHandler(f => (GUIListBox.OnRearrangedHandler)(
                (a1, a2) => Call(f, a1, a2)));
                RegisterHandler(f => (GUIListBox.CheckSelectedHandler)(
                () => Call(f)?.ToObject() ?? default));

                RegisterHandler(f => (GUINumberInput.OnValueEnteredHandler)(
                (a1) => Call(f, a1)));
                RegisterHandler(f => (GUINumberInput.OnValueChangedHandler)(
                (a1) => Call(f, a1)));

                RegisterHandler(f => (GUIProgressBar.ProgressGetterHandler)(
                () => (float)(Call(f)?.CastToNumber() ?? default)));

                RegisterHandler(f => (GUIRadioButtonGroup.RadioButtonGroupDelegate)(
                (a1, a2) => Call(f, a1, a2)));

                RegisterHandler(f => (GUIScrollBar.OnMovedHandler)(
                (a1, a2) => Call(f, a1, a2)?.CastToBool() ?? default));
                RegisterHandler(f => (GUIScrollBar.ScrollConversion)(
                (a1, a2) => (float)(Call(f, a1, a2)?.CastToNumber() ?? default)));

                RegisterHandler(f => (GUITextBlock.TextGetterHandler)(
                () => Call(f, new object[0])?.CastToString() ?? default));

                RegisterHandler(f => (GUITextBox.OnEnterHandler)(
                (a1, a2) => Call(f, a1, a2)?.CastToBool() ?? default));
                RegisterHandler(f => (GUITextBox.OnTextChangedHandler)(
                (a1, a2) => Call(f, a1, a2)?.CastToBool() ?? default));
                RegisterHandler(f => (TextBoxEvent)(
                (a1, a2) => Call(f, a1, a2)));

                RegisterHandler(f => (GUITickBox.OnSelectedHandler)(
                (a1) => Call(f, a1)?.CastToBool() ?? default));

                RegisterHandler(f => (GUITextBlock.ClickableArea.OnClickDelegate)(
                (a1, a2) => Call(f, a1, a2)));
            }

            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Function, typeof(NetMessageReceived), v => (NetMessageReceived)((arg1) =>
            {
                if (v.Function.OwnerScript == _script)
                {
                    Call(v.Function, arg1);
                }
            }));
#elif SERVER
            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Function, typeof(NetMessageReceived), v => (NetMessageReceived)((arg1, arg2) =>
            {
                if (v.Function.OwnerScript == _script)
                {
                    Call(v.Function, arg1, arg2);
                }
            }));
#endif

            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Table, typeof(Pair<JobPrefab, int>), v =>
            {
                return new Pair<JobPrefab, int>((JobPrefab)v.Table.Get(1).ToObject(), (int)v.Table.Get(2).CastToNumber());
            });

            Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion<ulong>((Script script, ulong v) => 
            {
                return DynValue.NewString(v.ToString());
            });

            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.String, typeof(ulong), v =>
            {
                return ulong.Parse(v.String);
            });

            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(
                scriptDataType: DataType.UserData,
                clrDataType: typeof(sbyte),
                canConvert: luaValue => luaValue.UserData?.Object is LuaSByte,
                converter: luaValue => luaValue.UserData.Object is LuaSByte v
                    ? (sbyte)v
                    : throw new ScriptRuntimeException("use SByte(value) to pass primitive type 'sbyte' to C#"));
            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(
                scriptDataType: DataType.UserData,
                clrDataType: typeof(byte),
                canConvert: luaValue => luaValue.UserData?.Object is LuaByte,
                converter: luaValue => luaValue.UserData.Object is LuaByte v
                    ? (byte)v
                    : throw new ScriptRuntimeException("use Byte(value) to pass primitive type 'byte' to C#"));
            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(
                scriptDataType: DataType.UserData,
                clrDataType: typeof(short),
                canConvert: luaValue => luaValue.UserData?.Object is LuaInt16,
                converter: luaValue => luaValue.UserData.Object is LuaInt16 v
                    ? (short)v
                    : throw new ScriptRuntimeException("use Int16(value) to pass primitive type 'short' to C#"));
            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(
                scriptDataType: DataType.UserData,
                clrDataType: typeof(ushort),
                canConvert: luaValue => luaValue.UserData?.Object is LuaUInt16,
                converter: luaValue => luaValue.UserData.Object is LuaUInt16 v
                    ? (ushort)v
                    : throw new ScriptRuntimeException("use UInt16(value) to pass primitive type 'ushort' to C#"));
            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(
                scriptDataType: DataType.UserData,
                clrDataType: typeof(int),
                canConvert: luaValue => luaValue.UserData?.Object is LuaInt32,
                converter: luaValue => luaValue.UserData.Object is LuaInt32 v
                    ? (int)v
                    : throw new ScriptRuntimeException("use Int32(value) to pass primitive type 'int' to C#"));
            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(
                scriptDataType: DataType.UserData,
                clrDataType: typeof(uint),
                canConvert: luaValue => luaValue.UserData?.Object is LuaUInt32,
                converter: luaValue => luaValue.UserData.Object is LuaUInt32 v
                    ? (uint)v
                    : throw new ScriptRuntimeException("use UInt32(value) to pass primitive type 'uint' to C#"));
            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(
                scriptDataType: DataType.UserData,
                clrDataType: typeof(long),
                canConvert: luaValue => luaValue.UserData?.Object is LuaInt64,
                converter: luaValue => luaValue.UserData.Object is LuaInt64 v
                    ? (long)v
                    : throw new ScriptRuntimeException("use Int64(value) to pass primitive type 'long' to C#"));
            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(
                scriptDataType: DataType.UserData,
                clrDataType: typeof(ulong),
                canConvert: luaValue => luaValue.UserData?.Object is LuaUInt64,
                converter: luaValue => luaValue.UserData.Object is LuaUInt64 v
                    ? (ulong)v
                    : throw new ScriptRuntimeException("use UInt64(value) to pass primitive type 'ulong' to C#"));
            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(
                scriptDataType: DataType.UserData,
                clrDataType: typeof(float),
                canConvert: luaValue => luaValue.UserData?.Object is LuaSingle,
                converter: luaValue => luaValue.UserData.Object is LuaSingle v
                    ? (float)v
                    : throw new ScriptRuntimeException("use Single(value) to pass primitive type 'float' to C#"));
            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(
                scriptDataType: DataType.UserData,
                clrDataType: typeof(double),
                canConvert: luaValue => luaValue.UserData?.Object is LuaDouble,
                converter: luaValue => luaValue.UserData.Object is LuaDouble v
                    ? (double)v
                    : throw new ScriptRuntimeException("use Double(value) to pass primitive type 'double' to C#"));

            RegisterOption<Character>(DataType.UserData);
            RegisterOption<AccountId>(DataType.UserData);
            RegisterOption<ContentPackageId>(DataType.UserData);
            RegisterOption<SteamId>(DataType.UserData);
            RegisterOption<DateTime>(DataType.UserData);
            RegisterOption<BannedPlayer>(DataType.UserData);
            RegisterOption<Address>(DataType.UserData);

            RegisterOption<int>(DataType.Number);

            RegisterEither<Address, AccountId>();

            RegisterImmutableArray<FactionPrefab.HireableCharacter>();
        }

        private static void RegisterImmutableArray<T>()
        {
            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Table, typeof(ImmutableArray<T>), v =>
            {
                return v.ToObject<T[]>().ToImmutableArray();
            });
        }

        private static void RegisterEither<T1, T2>()
        {
            DynValue convertEitherIntoDynValue(Either<T1, T2> either)
            {
                if (either.TryGet(out T1 value1))
                {
                    return UserData.Create(value1);
                }

                if (either.TryGet(out T2 value2))
                {
                    return UserData.Create(value2);
                }

                return null;
            }

            Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion(typeof(EitherT<T1, T2>), (Script v, object obj) =>
            {
                if (obj is EitherT<T1, T2> either)
                {
                    return convertEitherIntoDynValue(either);
                }

                return null;
            });

            Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion(typeof(EitherU<T1, T2>), (Script v, object obj) =>
            {
                if (obj is EitherU<T1, T2> either)
                {
                    return convertEitherIntoDynValue(either);
                }

                return null;
            });
        }

        private static void RegisterOption<T>(DataType dataType)
        {
            Script.GlobalOptions.CustomConverters.SetClrToScriptCustomConversion(typeof(Option<T>), (Script v, object obj) =>
            {
                if (obj is Option<T> option)
                {
                    if (option.TryUnwrap(out T outValue))
                    {
                        return UserData.Create(outValue);
                    }
                }

                return DynValue.Nil;
            });

            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(dataType, typeof(Option<T>), v =>
            {
                return Option<T>.Some(v.ToObject<T>());
            });

            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Nil, typeof(Option<T>), v =>
            {
                return Option<T>.None();
            });
        }

        private void RegisterAction<T>()
        {
            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Function, typeof(Action<T>), v =>
            {
                var function = v.Function;
                return (Action<T>)(p => Call(function, p));
            });

            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.ClrFunction, typeof(Action<T>), v =>
            {
                var function = v.Function;
                return (Action<T>)(p => Call(function, p));
            });
        }

        private void RegisterAction<T1, T2>()
        {
            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Function, typeof(Action<T1, T2>), v =>
            {
                var function = v.Function;
                return (Action<T1, T2>)((a1, a2) => Call(function, a1, a2));
            });

            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.ClrFunction, typeof(Action<T1, T2>), v =>
            {
                var function = v.Function;
                return (Action<T1, T2>)((a1, a2) => Call(function, a1, a2));
            });
        }

        private void RegisterAction()
        {
            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Function, typeof(Action), v => 
            {
                var function = v.Function;
                return (Action)(() => Call(function));
            });

            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.ClrFunction, typeof(Action), v =>
            {
                var function = v.Function;
                return (Action)(() => Call(function));
            });
        }

        private void RegisterFunc<T1>()
        {
            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Function, typeof(Func<T1>), v =>
            {
                var function = v.Function;
                return () => function.Call().ToObject<T1>();
            });

            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.ClrFunction, typeof(Func<T1>), v =>
            {
                var function = v.Function;
                return () => function.Call().ToObject<T1>();
            });
        }

        private void RegisterFunc<T1, T2>()
        {
            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Function, typeof(Func<T1, T2>), v =>
            {
                var function = v.Function;
                return (T1 a) => function.Call(a).ToObject<T2>();
            });

            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.ClrFunction, typeof(Func<T1, T2>), v =>
            {
                var function = v.Function;
                return (T1 a) => function.Call(a).ToObject<T2>();
            });
        }

        private void RegisterFunc<T1, T2, T3, T4, T5>()
        {
            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Function, typeof(Func<T1, T2, T3, T4, T5>), v =>
            {
                var function = v.Function;
                return (T1 a, T2 b, T3 c, T4 d) => function.Call(a, b, c, d).ToObject<T5>();
            });

            Script.GlobalOptions.CustomConverters.SetScriptToClrCustomConversion(DataType.Function, typeof(Func<T1, T2, T3, T4, T5>), v =>
            {
                var function = v.Function;
                return (T1 a, T2 b, T3 c, T4 d) => function.Call(a, b, c, d).ToObject<T5>();
            });
        }
    }
}
