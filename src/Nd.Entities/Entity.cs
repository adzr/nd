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

using System.Diagnostics;
using Nd.Identities;

namespace Nd.Entities {
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public abstract class Entity<TIdentity> : IEntity<TIdentity>
        where TIdentity : IIdentity {
        protected Entity(TIdentity identity) {
            if (identity is null) {
                throw new ArgumentNullException(nameof(identity));
            }

            Identity = identity;
        }

        public TIdentity Identity { get; }

        IIdentity IEntity.Identity => Identity;

        public override bool Equals(object? obj) => obj is Entity<TIdentity> entity &&
                   EqualityComparer<TIdentity>.Default.Equals(Identity, entity.Identity);

        public override int GetHashCode() => HashCode.Combine(Identity);

        private string GetDebuggerDisplay() => ToString() ?? string.Empty;

        public override string? ToString() => Identity?.ToString();
    }
}
