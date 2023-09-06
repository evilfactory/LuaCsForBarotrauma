using Barotrauma;
using Microsoft.Xna.Framework;
using MoonSharp.Interpreter;
using System;
using Xunit;
using Xunit.Abstractions;

namespace TestProject.LuaCs
{
    [Collection("LuaCs")]
    public class HookPatchTests : IClassFixture<LuaCsFixture>
    {
        private readonly LuaCsSetup luaCs;

        public HookPatchTests(LuaCsFixture luaCsFixture, ITestOutputHelper output)
        {
            // XXX: we can't have multiple instances of LuaCs patching the
            // same methods, otherwise we get script ownership exceptions.
            luaCs = luaCsFixture.LuaCs;

            LuaCsLogger.MessageLogger = (o) =>
            {
                output?.WriteLine($"{o}");
            };

            UserData.RegisterType<TestValueType>();
            UserData.RegisterType<IBogusInterface>();
            UserData.RegisterType<InterfaceImplementingType>();
            UserData.RegisterType<PatchTargetSimple>();
            UserData.RegisterType<PatchTargetReturnsObject>();
            UserData.RegisterType<PatchTargetReturnsInterface>();
            UserData.RegisterType<PatchTargetModifyParams>();
            UserData.RegisterType<PatchTargetVector2>();
            UserData.RegisterType<PatchTargetAmbiguous>();
            UserData.RegisterType<PatchTargetConstructor>();
            UserData.RegisterType<PatchTargetNumbers>();

            luaCs.Initialize();
            luaCs.Lua.Globals["TestValueType"] = UserData.CreateStatic<TestValueType>();
            luaCs.Lua.Globals["InterfaceImplementingType"] = UserData.CreateStatic<InterfaceImplementingType>();
        }

        private class PatchTargetSimple
        {
            public bool ran;

            public void Run()
            {
                ran = true;
            }
        }

        [Fact]
        public void TestFullMethodReplacement()
        {
            using var patchTargetHandle = HookPatchHelpers.LockPatchTarget<PatchTargetSimple>();
            var target = new PatchTargetSimple();
            using var patchHandle = luaCs.AddPrefix<PatchTargetSimple>(@"
                ptable.PreventExecution = true
            ", nameof(PatchTargetSimple.Run));
            target.Run();
            Assert.False(target.ran);
        }

        [Fact]
        public void TestOverrideExistingPatch()
        {
            using var patchTargetHandle = HookPatchHelpers.LockPatchTarget<PatchTargetSimple>();
            var target = new PatchTargetSimple();
            using var patchHandle = luaCs.AddPrefix<PatchTargetSimple>(@"
                ptable.PreventExecution = true
                originalPatchRan = true
            ", nameof(PatchTargetSimple.Run), patchId: "test");
            target.Run();
            Assert.False(target.ran);
            Assert.True(luaCs.Lua.Globals["originalPatchRan"] as bool?);

            // Reset this global so we can test if the original patch ran
            // after replacing it.
            luaCs.Lua.Globals["originalPatchRan"] = false;

            // Replace the existing prefix, but don't prevent execution this time
            luaCs.AddPrefix<PatchTargetSimple>(@"
                replacementPatchRan = true
            ", nameof(PatchTargetSimple.Run), patchId: "test");
            target.Run();
            Assert.True(target.ran);

            // Make sure the original patch didn't run
            Assert.False(luaCs.Lua.Globals["originalPatchRan"] as bool?);

            // Test if the replacement patch ran
            Assert.True(luaCs.Lua.Globals["replacementPatchRan"] as bool?);
        }

        [Fact]
        public void TestRemovePrefix()
        {
            using var patchTargetHandle = HookPatchHelpers.LockPatchTarget<PatchTargetSimple>();
            var target = new PatchTargetSimple();
            using (var patchHandle = luaCs.AddPrefix<PatchTargetSimple>(@"
                ptable.PreventExecution = true
                patchRan = true
            ", nameof(PatchTargetSimple.Run)))
            {
                target.Run();
                Assert.False(target.ran);
                Assert.True(luaCs.Lua.Globals["patchRan"] as bool?);

                luaCs.Lua.Globals["patchRan"] = false;
            }

            target.Run();
            Assert.True(target.ran);
            Assert.False(luaCs.Lua.Globals["patchRan"] as bool?);
        }

        [Fact]
        public void TestRemovePostfix()
        {
            using var patchTargetHandle = HookPatchHelpers.LockPatchTarget<PatchTargetSimple>();
            var target = new PatchTargetSimple();
            using (var patchHandle = luaCs.AddPostfix<PatchTargetSimple>(@"
                patchRan = true
            ", nameof(PatchTargetSimple.Run)))
            {
                target.Run();
                Assert.True(target.ran);
                Assert.True(luaCs.Lua.Globals["patchRan"] as bool?);

                target.ran = false;
                luaCs.Lua.Globals["patchRan"] = false;
            }

            target.Run();
            Assert.True(target.ran);
            Assert.False(luaCs.Lua.Globals["patchRan"] as bool?);
        }

        public struct TestValueType
        {
            public int foo;

            public TestValueType(int foo)
            {
                this.foo = foo;
            }
        }

        private class PatchTargetReturnsObject
        {
            public bool ran;

            public object Run()
            {
                ran = true;
                return 5;
            }
        }

        public interface IBogusInterface
        {
            int GetFoo();
        }

        private class InterfaceImplementingType : IBogusInterface
        {
            private readonly int foo;

            public InterfaceImplementingType(int foo)
            {
                this.foo = foo;
            }

            public int GetFoo() => foo;
        }

        [Fact]
        public void TestReturnBoxed()
        {
            using var patchTargetHandle = HookPatchHelpers.LockPatchTarget<PatchTargetReturnsObject>();
            var target = new PatchTargetReturnsObject();
            using var patchHandle = luaCs.AddPrefix<PatchTargetReturnsObject>(@"
                ptable.PreventExecution = true
                return 123
            ", nameof(PatchTargetReturnsObject.Run));
            var returnValue = target.Run();
            Assert.False(target.ran);
            Assert.Equal(123, (int)(double)returnValue);
        }

        [Fact]
        public void TestReturnVoid()
        {
            using var patchTargetHandle = HookPatchHelpers.LockPatchTarget<PatchTargetReturnsObject>();
            var target = new PatchTargetReturnsObject();
            // This should have no effect
            using var patchHandle = luaCs.AddPrefix<PatchTargetReturnsObject>(@"
                return
            ", nameof(PatchTargetReturnsObject.Run));
            var returnValue = target.Run();
            Assert.True(target.ran);
            Assert.Equal(5, returnValue);
        }

        [Fact]
        public void TestReturnNil()
        {
            using var patchTargetHandle = HookPatchHelpers.LockPatchTarget<PatchTargetReturnsObject>();
            var target = new PatchTargetReturnsObject();
            // This should modify the return value to "null"
            using var patchHandle = luaCs.AddPostfix<PatchTargetReturnsObject>(@"
                return nil
            ", nameof(PatchTargetReturnsObject.Run));
            var returnValue = target.Run();
            Assert.True(target.ran);
            Assert.Null(returnValue);
        }

        [Fact]
        public void TestReturnValueType()
        {
            using var patchTargetHandle = HookPatchHelpers.LockPatchTarget<PatchTargetReturnsObject>();
            var target = new PatchTargetReturnsObject();
            using var patchHandle = luaCs.AddPostfix<PatchTargetReturnsObject>(@"
                return TestValueType.__new(100)
            ", nameof(PatchTargetSimple.Run));
            var returnValue = target.Run();
            Assert.True(target.ran);
            Assert.IsType<TestValueType>(returnValue);
            Assert.Equal(100, ((TestValueType)returnValue).foo);
        }

        private class PatchTargetReturnsInterface
        {
            public bool ran;

            public IBogusInterface Run()
            {
                ran = true;
                return new InterfaceImplementingType(5);
            }
        }

        [Fact]
        public void TestReturnInterfaceImplementingType()
        {
            using var patchTargetHandle = HookPatchHelpers.LockPatchTarget<PatchTargetReturnsInterface>();
            var target = new PatchTargetReturnsInterface();
            using var patchHandle = luaCs.AddPostfix<PatchTargetReturnsInterface>(@"
                return InterfaceImplementingType.__new(100)
            ", nameof(PatchTargetReturnsInterface.Run));
            var returnValue = target.Run()!;
            Assert.True(target.ran);
            Assert.Equal(100, returnValue.GetFoo());
        }

        private class PatchTargetModifyParams
        {
            public bool ran;

            public void Run(out string result, int a, string b, ref byte c)
            {
                ran = true;
                result = a + b + c;
            }
        }

        [Fact]
        public void TestModifyParameters()
        {
            using var patchTargetHandle = HookPatchHelpers.LockPatchTarget<PatchTargetModifyParams>();
            var target = new PatchTargetModifyParams();
            using var patchHandle = luaCs.AddPrefix<PatchTargetModifyParams>(@"
                ptable['a'] = Int32(100)
                ptable['b'] = 'abc'
                ptable['c'] = Byte(4)
            ", nameof(PatchTargetModifyParams.Run));
            byte c = 123;
            target.Run(out var result, 5, "foo", ref c);
            Assert.True(target.ran);
            Assert.Equal(4, c);
            Assert.Equal("100abc4", result);
        }

        private class PatchTargetVector2
        {
            public bool ran;

            public string Run(Vector2 vec)
            {
                ran = true;
                return vec.ToString();
            }
        }

        [Fact]
        public void TestParameterValueType()
        {
            using var patchTargetHandle = HookPatchHelpers.LockPatchTarget<PatchTargetVector2>();
            var target = new PatchTargetVector2();
            using var patchHandle = luaCs.AddPrefix<PatchTargetVector2>(@"
                patchRan = true
            ", nameof(PatchTargetVector2.Run));
            var returnValue = target.Run(new Vector2(1, 2));
            Assert.True(target.ran);
            Assert.True(luaCs.Lua.Globals["patchRan"] as bool?);
            Assert.Equal("{X:1 Y:2}", returnValue);
        }

        private class PatchTargetAmbiguous
        {
            public bool ran;

            public PatchTargetAmbiguous() { }

            public PatchTargetAmbiguous(int a)
            {
                throw new NotImplementedException();
            }

            public void Run(out string result, int a, string b, ref byte c)
            {
                ran = true;
                result = a + b + c;
            }

            public void Run(string result, int a, string b, byte c)
            {
                throw new NotImplementedException();
            }

            public void Run(out string result, int a, string b, byte c)
            {
                throw new NotImplementedException();
            }

            public void Run(string result, int a, string b, ref byte c)
            {
                throw new NotImplementedException();
            }

            public void Run()
            {
                throw new NotImplementedException();
            }
        }

        [Fact]
        public void TestPatchDisambiguation()
        {
            using var patchTargetHandle = HookPatchHelpers.LockPatchTarget<PatchTargetAmbiguous>();
            var target = new PatchTargetAmbiguous();
            using var patchHandle = luaCs.AddPrefix<PatchTargetAmbiguous>(@"
                ptable['a'] = Int32(100)
                ptable['b'] = 'abc'
                ptable['c'] = Byte(4)
            ", nameof(PatchTargetAmbiguous.Run), new[]
            {
                $"out {typeof(string).FullName!}",
                typeof(int).FullName!,
                typeof(string).FullName!,
                $"ref {typeof(byte).FullName!}",
            });
            byte c = 123;
            target.Run(out var result, 5, "foo", ref c);
            Assert.True(target.ran);
            Assert.Equal(4, c);
            Assert.Equal("100abc4", result);
        }

        [Fact]
        public void TestPatchAmbiguous()
        {
            using var patchTargetHandle = HookPatchHelpers.LockPatchTarget<PatchTargetAmbiguous>();

            Assert.Throws<ScriptRuntimeException>(() =>
            {
                using var postfixHandle = luaCs.AddPostfix<PatchTargetAmbiguous>("", ".ctor");
            });
            Assert.Throws<ScriptRuntimeException>(() =>
            {
                using var prefixHandle = luaCs.AddPrefix<PatchTargetAmbiguous>("", ".ctor");
            });

            Assert.Throws<ScriptRuntimeException>(() =>
            {
                using var postfixHandle = luaCs.AddPostfix<PatchTargetAmbiguous>("", nameof(PatchTargetAmbiguous.Run));
            });
            Assert.Throws<ScriptRuntimeException>(() =>
            {
                using var prefixHandle = luaCs.AddPrefix<PatchTargetAmbiguous>("", nameof(PatchTargetAmbiguous.Run));
            });
        }

        private class PatchTargetConstructor
        {
            public enum CtorType
            {
                None,
                Patched,
                Default,
                Int,
                StringString,
            }

            public CtorType Ctor { get; set; }

            public bool PrefixRan { get; set; }

            public PatchTargetConstructor()
            {
                Ctor = CtorType.Default;
            }

            public PatchTargetConstructor(int a = default)
            {
                Ctor = CtorType.Int;
            }

            public PatchTargetConstructor(string a, string b)
            {
                Ctor = CtorType.StringString;
            }
        }

        [Fact]
        public void TestPatchConstructor()
        {
            using var patchTargetHandle = HookPatchHelpers.LockPatchTarget<PatchTargetConstructor>();

            {
                using var postfixHandle = luaCs.AddPostfix<PatchTargetConstructor>(@$"
                    instance.Ctor = {(int)PatchTargetConstructor.CtorType.Patched}
                ", ".ctor", Array.Empty<string>());
                using var prefixHandle = luaCs.AddPrefix<PatchTargetConstructor>(@$"
                    instance.PrefixRan = true
                ", ".ctor", Array.Empty<string>());
                var target = new PatchTargetConstructor();
                Assert.Equal(PatchTargetConstructor.CtorType.Patched, target.Ctor);
                Assert.True(target.PrefixRan);
            }

            {
                using var postfixHandle = luaCs.AddPostfix<PatchTargetConstructor>(@$"
                    instance.Ctor = {(int)PatchTargetConstructor.CtorType.Patched}
                ", ".ctor", new[] { typeof(int).FullName! });
                using var prefixHandle = luaCs.AddPrefix<PatchTargetConstructor>(@$"
                    instance.PrefixRan = true
                ", ".ctor", new[] { typeof(int).FullName! });
                var target = new PatchTargetConstructor(1);
                Assert.Equal(PatchTargetConstructor.CtorType.Patched, target.Ctor);
                Assert.True(target.PrefixRan);
            }

            {
                using var postfixHandle = luaCs.AddPostfix<PatchTargetConstructor>(@$"
                    instance.Ctor = {(int)PatchTargetConstructor.CtorType.Patched}
                ", ".ctor", new[] { typeof(string).FullName!, typeof(string).FullName! });
                using var prefixHandle = luaCs.AddPrefix<PatchTargetConstructor>(@$"
                    instance.PrefixRan = true
                ", ".ctor", new[] { typeof(string).FullName!, typeof(string).FullName! });
                var target = new PatchTargetConstructor("", "");
                Assert.Equal(PatchTargetConstructor.CtorType.Patched, target.Ctor);
                Assert.True(target.PrefixRan);
            }
        }

        private class PatchTargetNumbers
        {
            public bool ran;

            public sbyte RunSByte(sbyte v)
            {
                ran = true;
                return v;
            }

            public byte RunByte(byte v)
            {
                ran = true;
                return v;
            }

            public short RunInt16(short v)
            {
                ran = true;
                return v;
            }

            public ushort RunUInt16(ushort v)
            {
                ran = true;
                return v;
            }

            public int RunInt32(int v)
            {
                ran = true;
                return v;
            }

            public uint RunUInt32(uint v)
            {
                ran = true;
                return v;
            }

            public long RunInt64(long v)
            {
                ran = true;
                return v;
            }

            public ulong RunUInt64(ulong v)
            {
                ran = true;
                return v;
            }

            public float RunSingle(float v)
            {
                ran = true;
                return v;
            }

            public double RunDouble(double v)
            {
                ran = true;
                return v;
            }
        }

        [Fact]
        public void TestCastSByte()
        {
            using var patchTargetHandle = HookPatchHelpers.LockPatchTarget<PatchTargetNumbers>();
            var target = new PatchTargetNumbers();
            using var patchHandle = luaCs.AddPrefix<PatchTargetNumbers>(@"
                ptable['v'] = SByte(-6)
            ", nameof(PatchTargetNumbers.RunSByte));
            var returnValue = target.RunSByte(-5);
            Assert.True(target.ran);
            Assert.Equal(-6, returnValue);
        }

        [Fact]
        public void TestCastByte()
        {
            using var patchTargetHandle = HookPatchHelpers.LockPatchTarget<PatchTargetNumbers>();
            var target = new PatchTargetNumbers();
            using var patchHandle = luaCs.AddPrefix<PatchTargetNumbers>(@"
                ptable['v'] = Byte(6)
            ", nameof(PatchTargetNumbers.RunByte));
            var returnValue = target.RunByte(5);
            Assert.True(target.ran);
            Assert.Equal(6, returnValue);
        }

        [Fact]
        public void TestCastInt16()
        {
            using var patchTargetHandle = HookPatchHelpers.LockPatchTarget<PatchTargetNumbers>();
            var target = new PatchTargetNumbers();
            using var patchHandle = luaCs.AddPrefix<PatchTargetNumbers>(@"
                ptable['v'] = Int16(-25000)
            ", nameof(PatchTargetNumbers.RunInt16));
            var returnValue = target.RunInt16(30000);
            Assert.True(target.ran);
            Assert.Equal(-25000, returnValue);
        }

        [Fact]
        public void TestCastUInt16()
        {
            using var patchTargetHandle = HookPatchHelpers.LockPatchTarget<PatchTargetNumbers>();
            var target = new PatchTargetNumbers();
            using var patchHandle = luaCs.AddPrefix<PatchTargetNumbers>(@"
                ptable['v'] = UInt16(60000)
            ", nameof(PatchTargetNumbers.RunUInt16));
            var returnValue = target.RunUInt16(50000);
            Assert.True(target.ran);
            Assert.Equal(60000, returnValue);
        }

        [Fact]
        public void TestCastInt32()
        {
            using var patchTargetHandle = HookPatchHelpers.LockPatchTarget<PatchTargetNumbers>();
            var target = new PatchTargetNumbers();
            using var patchHandle = luaCs.AddPrefix<PatchTargetNumbers>(@"
                ptable['v'] = Int32('7FFFFF00', 16)
            ", nameof(PatchTargetNumbers.RunInt32));
            var returnValue = target.RunInt32(900000);
            Assert.True(target.ran);
            Assert.Equal(0x7FFFFF00, returnValue);
        }

        [Fact]
        public void TestCastUInt32()
        {
            using var patchTargetHandle = HookPatchHelpers.LockPatchTarget<PatchTargetNumbers>();
            var target = new PatchTargetNumbers();
            using var patchHandle = luaCs.AddPrefix<PatchTargetNumbers>(@"
                ptable['v'] = UInt32('AFFFFFFF', 16)
            ", nameof(PatchTargetNumbers.RunUInt32));
            var returnValue = target.RunUInt32(300500);
            Assert.True(target.ran);
            Assert.Equal(0xAFFFFFFF, returnValue);
        }

        [Fact]
        public void TestCastInt64()
        {
            using var patchTargetHandle = HookPatchHelpers.LockPatchTarget<PatchTargetNumbers>();
            var target = new PatchTargetNumbers();
            using var patchHandle = luaCs.AddPrefix<PatchTargetNumbers>(@"
                ptable['v'] = Int64('7555555555555555', 16)
            ", nameof(PatchTargetNumbers.RunInt64));
            var returnValue = target.RunInt64(0x7FFFFFFF00000000);
            Assert.True(target.ran);
            Assert.Equal(0x7555555555555555, returnValue);
        }

        [Fact]
        public void TestCastUInt64()
        {
            using var patchTargetHandle = HookPatchHelpers.LockPatchTarget<PatchTargetNumbers>();
            var target = new PatchTargetNumbers();
            using var patchHandle = luaCs.AddPrefix<PatchTargetNumbers>(@"
                ptable['v'] = UInt64('F555555555555555', 16)
            ", nameof(PatchTargetNumbers.RunUInt64));
            var returnValue = target.RunUInt64(0xFFFFFFFF00000000);
            Assert.True(target.ran);
            Assert.Equal(0xF555555555555555, returnValue);
        }

        [Fact]
        public void TestCastSingle()
        {
            using var patchTargetHandle = HookPatchHelpers.LockPatchTarget<PatchTargetNumbers>();
            var target = new PatchTargetNumbers();
            using var patchHandle = luaCs.AddPrefix<PatchTargetNumbers>(@"
                ptable['v'] = Single(123.456)
            ", nameof(PatchTargetNumbers.RunSingle));
            var returnValue = target.RunSingle(111.111f);
            Assert.True(target.ran);
            Assert.Equal(123.456f, returnValue);
        }

        [Fact]
        public void TestCastDouble()
        {
            using var patchTargetHandle = HookPatchHelpers.LockPatchTarget<PatchTargetNumbers>();
            var target = new PatchTargetNumbers();
            using var patchHandle = luaCs.AddPrefix<PatchTargetNumbers>(@"
                ptable['v'] = Double(123.456)
            ", nameof(PatchTargetNumbers.RunDouble));
            var returnValue = target.RunDouble(111.111d);
            Assert.True(target.ran);
            Assert.Equal(123.456d, returnValue);
        }
    }
}
