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

using Xunit;

namespace Nd.ValueObjects.Tests
{
    internal record class SampleValueObject(
            int Id,
            string Name
        ) : ValueObject;

    public class ValueObjectTests
    {
        [Theory]
        [InlineData(1, "left", 2, "right", -1)]
        [InlineData(1, "left", 1, "right", -6)]
        [InlineData(1, "left", 2, "left", -1)]
        [InlineData(1, "left", 1, "left", 0)]
        [InlineData(2, "right", 2, "right", 0)]
        public void CanBeCompared(int leftId, string leftName, int rightId, string rightName, int result) =>
            Assert.Equal(result,
                new SampleValueObject(leftId, leftName)
                .CompareTo(new SampleValueObject(rightId, rightName)));

        [Theory]
        [InlineData(1, "left", 2, "right", false)]
        [InlineData(1, "left", 1, "right", false)]
        [InlineData(1, "left", 2, "left", false)]
        [InlineData(1, "left", 1, "left", true)]
        [InlineData(2, "right", 2, "right", true)]
        public void CanBeEqualled(int leftId, string leftName, int rightId, string rightName, bool result) =>
            Assert.Equal(result,
                new SampleValueObject(leftId, leftName)
                .Equals(new SampleValueObject(rightId, rightName)));

        [Theory]
        [InlineData(1, "left", 2, "right", false)]
        [InlineData(1, "left", 1, "right", false)]
        [InlineData(1, "left", 2, "left", false)]
        [InlineData(1, "left", 1, "left", true)]
        [InlineData(2, "right", 2, "right", true)]
        public void CanHaveDeterministicHashCode(int leftId, string leftName, int rightId, string rightName, bool result) =>
            Assert.Equal(result,
                new SampleValueObject(leftId, leftName).GetHashCode()
                .Equals(new SampleValueObject(rightId, rightName).GetHashCode()));

        [Theory]
        [InlineData(1, "val1", "SampleValueObject { Id = 1, Name = val1 }")]
        [InlineData(2, "val2", "SampleValueObject { Id = 2, Name = val2 }")]
        public void CanBeConvertedToValidString(int id, string name, string result) =>
            Assert.Equal(result, new SampleValueObject(id, name).ToString());
    }
}