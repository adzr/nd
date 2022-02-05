/*
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
using Nd.Core.Types.Versions;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Nd.Core.Tests.VersionedTypes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class VersionedTestAttribute : VersionedTypeAttribute
    {
        public VersionedTestAttribute(string name, uint version) : base(name, version) { }
    }

    #region Test types definitions

    public abstract record class UpgradableVersion(
        int Value
        ) : IVersionedType
    {
        public abstract uint TypeVersion { get; }
        public abstract string TypeName { get; }
        public virtual Task<IVersionedType?> UpgradeAsync(CancellationToken cancellationToken) => Task.FromResult<IVersionedType?>(default);
    }

    [VersionedTest(nameof(UpgradableVersion), 1)]
    public record class UpgradableVersion1(int Value) : UpgradableVersion(Value)
    {
        private static readonly (string Name, uint Version) NameAndVersion = typeof(UpgradableVersion1).GetNameAndVersion();

        public override uint TypeVersion => NameAndVersion.Version;

        public override string TypeName => NameAndVersion.Name;

        public override Task<IVersionedType?> UpgradeAsync(CancellationToken _) => Task.FromResult<IVersionedType?>(new UpgradableVersion2(Value * 2));
    }

    [VersionedTest(nameof(UpgradableVersion), 2)]
    public record class UpgradableVersion2(int Value) : UpgradableVersion(Value)
    {
        private static readonly (string Name, uint Version) NameAndVersion = typeof(UpgradableVersion2).GetNameAndVersion();

        public override uint TypeVersion => NameAndVersion.Version;

        public override string TypeName => NameAndVersion.Name;

        public override Task<IVersionedType?> UpgradeAsync(CancellationToken _) => Task.FromResult<IVersionedType?>(new UpgradableVersion3(Value * 2));
    }

    [VersionedTest(nameof(UpgradableVersion), 3)]
    public record class UpgradableVersion3(int Value) : UpgradableVersion(Value)
    {
        private static readonly (string Name, uint Version) NameAndVersion = typeof(UpgradableVersion3).GetNameAndVersion();

        public override uint TypeVersion => NameAndVersion.Version;

        public override string TypeName => NameAndVersion.Name;
    }

    [VersionedTest(nameof(UpgradableVersion), 4)]
    public record class UpgradableVersion4(int Value) : UpgradableVersion(Value)
    {
        private static readonly (string Name, uint Version) NameAndVersion = typeof(UpgradableVersion4).GetNameAndVersion();

        public override uint TypeVersion => NameAndVersion.Version;

        public override string TypeName => NameAndVersion.Name;

        public override Task<IVersionedType?> UpgradeAsync(CancellationToken _) => Task.FromResult<IVersionedType?>(new UpgradableVersion3(Value * 2));
    }

    [VersionedTest(nameof(UpgradableVersion), 4)]
    public record class UpgradableVersion5(int Value) : UpgradableVersion(Value)
    {
        private static readonly (string Name, uint Version) NameAndVersion = typeof(UpgradableVersion5).GetNameAndVersion();

        public override uint TypeVersion => NameAndVersion.Version;

        public override string TypeName => NameAndVersion.Name;

        public override Task<IVersionedType?> UpgradeAsync(CancellationToken _) => Task.FromResult<IVersionedType?>(new UpgradableVersion4(Value * 2));
    }

    [VersionedTest("NonUpgradableVersion", 2)]
    public record class NonUpgradableVersion(int Value) : UpgradableVersion(Value)
    {
        private static readonly (string Name, uint Version) NameAndVersion = typeof(NonUpgradableVersion).GetNameAndVersion();

        public override uint TypeVersion => NameAndVersion.Version;

        public override string TypeName => NameAndVersion.Name;

        public override Task<IVersionedType?> UpgradeAsync(CancellationToken _) => Task.FromResult<IVersionedType?>(new UpgradableVersion3(Value * 2));
    }

    #endregion

    [VersionedTest(nameof(VersionedTypeAttributeTests), 7)]
    public class VersionedTypeAttributeTests
    {
        [Fact]
        public void CanHaveAttributedNameAndVersion()
        {
            var (name, version) = typeof(VersionedTypeAttributeTests).GetNameAndVersion();

            Assert.Equal(nameof(VersionedTypeAttributeTests), name);
            Assert.Equal(7u, version);
        }

        [Fact]
        public void CanUpgradeTypeOfSameTypeNameToGreaterVersion()
        {
            var @base = new UpgradableVersion1(2);
            Assert.Equal(2, @base.Value);
            var @new = @base.UpgradeRecursiveAsync().GetAwaiter().GetResult();
            Assert.True(@new is UpgradableVersion3);
            Assert.Equal(8, ((UpgradableVersion3)@new).Value);
        }

        [Fact]
        public void CannotUpgradeTypeOfSameTypeNameToSameVersion()
        {
            var @base = new UpgradableVersion5(2);
            Assert.Equal(2, @base.Value);
            Assert.Throws<Exception>(() => @base.UpgradeRecursiveAsync().GetAwaiter().GetResult());
        }

        [Fact]
        public void CannotUpgradeTypeOfSameTypeNameToLesserVersion()
        {
            var @base = new UpgradableVersion4(2);
            Assert.Equal(2, @base.Value);
            Assert.Throws<Exception>(() => @base.UpgradeRecursiveAsync().GetAwaiter().GetResult());
        }

        [Fact]
        public void CannotUpgradeTypeToTypeOfDifferentTypeName()
        {
            var @base = new NonUpgradableVersion(2);
            Assert.Equal(2, @base.Value);
            Assert.Throws<Exception>(() => @base.UpgradeRecursiveAsync().GetAwaiter().GetResult());
        }
    }
}
