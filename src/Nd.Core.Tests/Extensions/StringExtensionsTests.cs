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
using Xunit;

namespace Nd.Core.Tests.Extensions
{
    public class StringExtensionsTests
    {
        [Theory]
        [InlineData("SomeTestData", "some_test_data")]
        [InlineData("Some_Test_Data", "some_test_data")]
        [InlineData("some_test_data", "some_test_data")]
        [InlineData("some-test-data", "some_test_data")]
        [InlineData("some test data", "some_test_data")]
        [InlineData("Some test data", "some_test_data")]
        [InlineData("Some Test Data", "some_test_data")]
        [InlineData(" Some Test Data ", "some_test_data")]
        [InlineData("_some_test_data_", "some_test_data")]
        [InlineData("_Some_Test_Data_", "some_test_data")]
        [InlineData("#Some#Test#Data#", "some_test_data")]
        [InlineData("Some#Test#Data#1", "some_test_data_1")]
        [InlineData("SomeTestData1", "some_test_data_1")]
        [InlineData("SomeTestData_1", "some_test_data_1")]
        [InlineData("??Some??Test??Data??1??", "some_test_data_1")]
        [InlineData("??some??test??data??1??", "some_test_data_1")]
        public void CanConvertToSnakeCase(string input, string expected)
            => Assert.Equal(expected, input.ToSnakeCase());

        [Theory]
        [InlineData("SomeTestData", "", "SomeTestData")]
        [InlineData("SomeTestData", "ata", "SomeTestD")]
        [InlineData("SomeTestData_1", "_1", "SomeTestData")]
        [InlineData("SomeTestData_//", "_//", "SomeTestData")]
        public void CanTrimEnd(string input, string trim, string expected)
            => Assert.Equal(expected, input.TrimEnd(trim));
    }
}