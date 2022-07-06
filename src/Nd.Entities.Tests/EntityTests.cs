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

using System;
using Nd.Identities;
using Xunit;

namespace Nd.Entities.Tests
{
    [NamedIdentity("sample")]
    internal record class SampleId : GuidIdentity
    {
        public SampleId(Guid value) : base(value) { }
    }

    internal class SampleEntity : Entity<SampleId>
    {
        public SampleEntity(SampleId identity, string text) : base(identity)
        {
            Text = text;
        }

        public string Text { get; }
    }

    [Trait("Category", "Unit")]
    public class EntityTests
    {
        private const string DefaultText = "Value";
        private readonly Guid _guid = Guid.NewGuid();

        [Fact]
        public void CanBeEqualBasedOnIdentityOnly() =>
            Assert.Equal(new SampleEntity(new SampleId(_guid), DefaultText), new SampleEntity(new SampleId(_guid), "AnotherValue"));

        [Fact]
        public void CanNotBeEqualWithDifferentIdentity() =>
            Assert.NotEqual(new SampleEntity(new SampleId(_guid), DefaultText), new SampleEntity(new SampleId(Guid.NewGuid()), DefaultText));
    }
}
