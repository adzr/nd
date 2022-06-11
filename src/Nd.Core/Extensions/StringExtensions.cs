/*
 * Copyright © 2022 Ahmed Zaher
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

using System.Text.RegularExpressions;

namespace Nd.Core.Extensions
{
    /// <summary>
    /// Contains extension methods for <see cref="string"/>.
    /// </summary>
    public static class StringExtensions
    {
        private static readonly Regex s_camelCasePattern = new(@"(([A-Z]+[a-z]*)|([0-9]+)|([a-z]+))", RegexOptions.Compiled);

        /// <summary>
        /// Converts this <see cref="string"/> into a snake case notation.
        /// </summary>
        /// <param name="value">The value of the given <see cref="string"/>.</param>
        /// <param name="separator">An optional separator to use for overridinng the default '_'.</param>
        /// <returns>A lower case <see cref="string"/> where words are separated using the specified separator, defaults to '_'.</returns>
        public static string ToSnakeCase(this string value, string separator = "_") =>
            string.Join(separator, s_camelCasePattern.Matches(value).Select(g => g.Value.ToUpperInvariant()));

        /// <summary>
        /// Trims the end of this <see cref="string"/> of the first matching value of the given arrays.
        /// </summary>
        /// <param name="value">The value of the given <see cref="string"/>.</param>
        /// <param name="trimStrings">A <see cref="string"/> array that contains the values to be trimmed of the end of the given <see cref="string"/>.</param>
        /// <returns>An end-trimmed <see cref="string"/> of the first value match found in the given array.</returns>
        public static string TrimEnd(this string value, StringComparison comparison, params string[] trimStrings)
        {
            var trim = trimStrings?.FirstOrDefault((s) => value.EndsWith(s, comparison));

            return !string.IsNullOrEmpty(trim) ? value[..^trim.Length] : value;
        }
    }
}
