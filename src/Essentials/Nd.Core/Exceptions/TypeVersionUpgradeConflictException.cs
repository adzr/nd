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

using System;
using System.Runtime.Serialization;
using Nd.Core.Types.Versions;

namespace Nd.Core.Exceptions
{
    [Serializable]
    public class TypeVersionUpgradeConflictException : NdCoreException
    {
        public IVersionedType? Upgradable { get; }
        public uint? From { get; }
        public uint? To { get; }

        public TypeVersionUpgradeConflictException(IVersionedType upgradable, uint from, uint to, Exception? innerException = null)
            : base($"Trying to upgrade type of name '{upgradable?.TypeName}' from version {from}" +
                    $" to version '{to}', the upgraded version must be greater than the given one", innerException)
        {
            Upgradable = upgradable;
            From = from;
            To = to;
        }

        public TypeVersionUpgradeConflictException() : this($"Trying to upgrade type to an invalid version")
        {
        }

        public TypeVersionUpgradeConflictException(string message) : base(message)
        {
        }

        public TypeVersionUpgradeConflictException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected TypeVersionUpgradeConflictException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
