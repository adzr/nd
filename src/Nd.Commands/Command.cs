/*
 * Copyright © 2015 - 2021 Rasmus Mikkelsen
 * Copyright © 2015 - 2021 eBay Software Foundation
 * Modified from original source https://github.com/eventflow/EventFlow
 * 
 * Copyright © 2018 - 2021 Lutando Ngqakaza
 * Modified from original source https://github.com/Lutando/Akkatecture
 * 
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

using Nd.Aggregates.Identities;
using Nd.Core.Extensions;
using Nd.Core.Types;
using Nd.ValueObjects;

namespace Nd.Commands {
    public abstract record class Command<TIdentity> : ValueObject, ICommand<TIdentity>
        where TIdentity : IAggregateIdentity {

        private readonly string _typeName;
        private readonly uint _typeVersion;

        protected Command(IIdempotencyIdentity idempotencyIdentity, ICorrelationIdentity correlationIdentity, TIdentity aggregateIdentity) {
            (_typeName, _typeVersion) = Definitions.TypesNamesAndVersions[GetType()]?.FirstOrDefault() ??
            throw new TypeDefinitionNotFoundException($"Definition of type has no Name or Version defined: {GetType().ToPrettyString()}");

            IdempotencyIdentity = idempotencyIdentity;
            CorrelationIdentity = correlationIdentity;
            AggregateIdentity = aggregateIdentity;
        }

        public string TypeName => _typeName;

        public uint TypeVersion => _typeVersion;

        public TIdentity AggregateIdentity { get; }

        public IIdempotencyIdentity IdempotencyIdentity { get; }

        public ICorrelationIdentity CorrelationIdentity { get; }
    }
}
