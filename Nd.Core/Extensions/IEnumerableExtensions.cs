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

namespace Nd.Core.Extensions
{
    /// <summary>
    /// Contains extension methods for <see cref="IEnumerable{T}"/>.
    /// </summary>
    public static class IEnumerableExtensions
    {
        /// <summary>
        /// Compares two <see cref="IEnumerable{T}"/> collections first based on length then based on each individual element in sequence.
        /// </summary>
        /// <typeparam name="T">The type of the element in the <see cref="IEnumerable{T}"/>, given an element can be null.</typeparam>
        /// <typeparam name="TEnumerable{T}">The type of this <see cref="IEnumerable{T}"/>.</typeparam>
        /// <param name="left">This given as a left-side operand for comparison.</param>
        /// <param name="right">The right-side operand for comparison.</param>
        /// <returns></returns>
        public static int CompareTo<T, TEnumerable>(this TEnumerable left, IEnumerable<T?> right)
            where TEnumerable : IEnumerable<T?>
        {
            // Conversion to arrays for multiple iterations.
            var leftArray = left.ToArray();
            var rightArray = right.ToArray();

            // First compare based on size/length.
            var compareSizeResult = leftArray.Length.CompareTo(rightArray.Length);

            // If they don't match then return result.
            if (compareSizeResult != 0)
            {
                return compareSizeResult;
            }

            // Else compare each element in the left array against
            // its corresponding element in the right one,
            // and return once a difference is found.
            for (var i = 0; i < leftArray.Length; i++)
            {
                var l = leftArray[i];
                var r = rightArray[i];

                if (l is null && r is null)
                {
                    continue;
                }
                else if (l is null)
                {
                    return -1;
                }
                else if (r is null)
                {
                    return 1;
                }

                var compareElementResult = typeof(IComparable<T>).IsAssignableFrom(typeof(T)) ?
                    ((IComparable<T>)l).CompareTo(r) :
                    string.CompareOrdinal(l.ToString(), r.ToString());

                if (compareElementResult != 0)
                {
                    return compareElementResult;
                }
            }

            return 0;
        }
    }
}
