/*
 * Copyright © 2015 - 2021 Rasmus Mikkelsen
 * Copyright © 2015 - 2021 eBay Software Foundation
 * Modified from original source https://github.com/eventflow/EventFlow
 * 
 * Copyright © 2022 Ahmed Zaher
 * https://github.com/adzr/Nd
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy 
 * of this software and associated documentation files (the "Software"), to deal 
 * in the Software without restriction, including without limitation the rights 
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell 
 * copies of the Software, and to permit persons to whom the Software is 
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all 
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, 
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE 
 * SOFTWARE.
 */

using Nd.Core.Extensions;
using Nd.Core.NamedTypes;
using Nd.Core.VersionedTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Nd.Core.Tests.Extensions
{
    public class TypeExtensionsTests
    {
        [Theory]
        [InlineData(typeof(string), "String")]
        [InlineData(typeof(int), "Int32")]
        [InlineData(typeof(IEnumerable<>), "IEnumerable<>")]
        [InlineData(typeof(KeyValuePair<,>), "KeyValuePair<,>")]
        [InlineData(typeof(IEnumerable<string>), "IEnumerable<String>")]
        [InlineData(typeof(IEnumerable<IEnumerable<string>>), "IEnumerable<IEnumerable<String>>")]
        [InlineData(typeof(KeyValuePair<bool, long>), "KeyValuePair<Boolean,Int64>")]
        [InlineData(typeof(KeyValuePair<KeyValuePair<bool, long>, KeyValuePair<bool, long>>), "KeyValuePair<KeyValuePair<Boolean,Int64>,KeyValuePair<Boolean,Int64>>")]
        public void CanConvertToPrettyString(Type type, string expectedPrettyString) =>
            Assert.Equal(expectedPrettyString, type.ToPrettyString());

        [Fact]
        public void CanGetInterfacesOfSpecificTypeOfAType()
        {
            var result = typeof(TypeTest).GetInterfacesOfType<IA>();

            Assert.Equal(6, result.Length);
            Assert.Contains(typeof(IA<ID>), result);
            Assert.Contains(typeof(IA), result);
            Assert.Contains(typeof(IB), result);
            Assert.Contains(typeof(IA<IE>), result);
            Assert.Contains(typeof(IC), result);
            Assert.Contains(typeof(IA<IF>), result);
        }

        [Fact]
        public void CanGetGenericTypeArgumentsOfAType()
        {
            var result = typeof(TypeTest)
                .GetTypeInfo()
                .GetInterfaces()
                .SelectMany(i => i.GetGenericTypeArgumentsOfType<ID>())
                .ToArray();

            Assert.Equal(3, result.Length);
            Assert.Contains(typeof(ID), result);
            Assert.Contains(typeof(IE), result);
            Assert.Contains(typeof(IF), result);
        }

        [Fact]
        public void CanNotGetGenericTypeArgumentsOfAType() =>
            Assert.Empty(typeof(TypeTest).GetTypeInfo().GetInterface(nameof(ID))?.GetGenericTypeArgumentsOfType<ID>() ?? Array.Empty<Type>());

        [Fact]
        public void CanGetMethodWithSingleParameterOfAType() =>
            Assert.NotNull(typeof(TypeTest).GetMethodWithSingleParameterOfType(nameof(TypeTest.DoNothingWith), typeof(string)));

        [Fact]
        public void CanNotGetMethodWithSingleWrongParameterOfAType() =>
            Assert.Throws<NotSupportedException>(() => typeof(TypeTest).GetMethodWithSingleParameterOfType(nameof(TypeTest.DoNothingWith), typeof(int)));

        [Fact]
        public void CanNotGetAbsentMethodWithSingleParameterOfAType() =>
            Assert.Throws<NotSupportedException>(() => typeof(TypeTest).GetMethodWithSingleParameterOfType("IDoNotExist", typeof(int)));

        [Fact]
        public void CanCheckIfHasMethodWithSingleParameterOfAType() =>
            Assert.True(typeof(TypeTest).HasMethodWithSingleParameterOfType(nameof(TypeTest.DoNothingWith), typeof(string)));

        [Fact]
        public void CanCheckIfHasNoMethodWithSingleParameterOfAType() =>
            Assert.False(typeof(TypeTest).HasMethodWithSingleParameterOfType(nameof(TypeTest.DoNothingWith), typeof(int)));

        [Fact]
        public void CanGetNamedTypeName() =>
            Assert.Equal("test-Test", typeof(TypeTest).GetName());

        [Fact]
        public void CanGetVersionedTypeVersion() =>
            Assert.Equal(1u, typeof(TypeTest).GetVersion());

        [Fact]
        public void CanCompileMethodInvocation() =>
            Assert.Equal("Something", typeof(TypeTest)
                .GetMethod(nameof(TypeTest.ReturnArg))?
                .CompileMethodInvocation<Func<TypeTest, string, string>>()(new TypeTest(), "Something") ?? "");
    }

    internal interface IA { }

    internal interface IA<T> : IA where T : ID { }

    internal interface IB : IA<IE> { }

    internal interface IC : IA<IF> { }

    internal interface ID { }

    internal interface IE : ID { }

    internal interface IF : ID { }

    [NamedTypeTest("Test")]
    [VersionedTypeTest(1)]
    internal class TypeTest : IA<ID>, IB, IC, ID
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Test case requirement.")]
        public void DoNothingWith(string _) { }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Test case requirement.")]
        public string ReturnArg(string arg) => arg;
    }

    internal sealed class NamedTypeTestAttribute : NamedTypeAttribute
    {
        public NamedTypeTestAttribute(string name) : base($"test-{name}") { }
    }

    internal sealed class VersionedTypeTestAttribute : VersionedTypeAttribute
    {
        public VersionedTypeTestAttribute(uint version) : base(version) { }
    }
}
