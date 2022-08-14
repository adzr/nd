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
using System.Runtime.Serialization;

namespace Nd.Queries.Exceptions
{
    [Serializable]
    public class QueryExecutionException : Exception
    {
        public IQuery? Query { get; }
        public Guid Id { get; }

        public QueryExecutionException() : this("Query execution has unexpectedly failed")
        {
        }

        public QueryExecutionException(string? message) : base(message)
        {
        }

        public QueryExecutionException(IQuery query, Guid id, Exception ex) : this($"Query {id}: {query} execution has unexpectedly failed", ex)
        {
            Query = query;
            Id = id;
        }

        public QueryExecutionException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        protected QueryExecutionException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
