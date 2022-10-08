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
using Nd.Aggregates.Persistence;
using Nd.Core.Exceptions;
using Nd.Subscriptions.Persistence;

namespace Nd.Subscriptions.Exceptions
{
    [Serializable]
    public class SubscriptionWatchException : NdCoreException
    {
        public ISubscriptionState? State { get; }
        public ICommittedEvent? Event { get; }
        public Exception? HandlerException { get; }
        public Exception? FailureCallbackException { get; }

        public SubscriptionWatchException()
        {
        }

        public SubscriptionWatchException(string? message) : base(message)
        {
        }

        public SubscriptionWatchException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        public SubscriptionWatchException(ISubscriptionState state, ICommittedEvent @event, Exception handlerException, Exception failureCallbackException)
        {
            State = state;
            Event = @event;
            HandlerException = handlerException;
            FailureCallbackException = failureCallbackException;
        }

        protected SubscriptionWatchException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
