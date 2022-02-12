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
using Nd.Core.Factories;
using Nd.Core.Types;
using Nd.ValueObjects;

namespace Nd.Identities {

    public abstract record class GuidIdentity : ValueObject, IIdentity<Guid> {

        private readonly string _stringValue;
        private readonly string _typeName;

        public Guid Value { get; }

        protected GuidIdentity(Guid value) {
            _typeName = Definitions.TypesNamesAndVersions[GetType()]?.FirstOrDefault().Name ??
                throw new TypeDefinitionNotFoundException($"Definition of type has no Name defined: {GetType().ToPrettyString()}");
            Value = value;
            _stringValue = $"{_typeName.ToSnakeCase().TrimEnd(StringComparison.OrdinalIgnoreCase, "_id", "_identity")}-{value.ToString("N").ToUpperInvariant()}";
        }

        protected GuidIdentity(IGuidFactory factory) : this(factory.Create()) { }

        public string TypeName => _typeName;

        public sealed override string ToString() => _stringValue;
    }
}
