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
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization.Conventions;
using Nd.Core.Types;
using Nd.Core.Types.Names;
using Nd.Extensions.Stores.Mongo.Exceptions;

namespace Nd.Extensions.Stores.Mongo
{
    public class NamedTypeDiscriminatorConvention : IDiscriminatorConvention
    {
        private const string TypeNameKey = "_t";
        private const string TypeNameAndVersionSeparator = "#";

        public string ElementName => TypeNameKey;

        public Type GetActualType(IBsonReader bsonReader, Type nominalType)
        {
            if (bsonReader is null)
            {
                throw new ArgumentNullException(nameof(bsonReader));
            }

            if (!typeof(INamedType).IsAssignableFrom(nominalType))
            {
                throw new DiscriminationException(
                    $"Cannot use {nameof(NamedTypeDiscriminatorConvention)} for type {nominalType}");
            }

            var typeName = string.Empty;
            var typeVersion = 0u;

            var bookmark = bsonReader.GetBookmark();

            bsonReader.ReadStartDocument();

            if (bsonReader.FindElement(ElementName))
            {
                var typeString = bsonReader.ReadString();

                var typeNameAndVersion = typeString?
                    .Split(TypeNameAndVersionSeparator,
                    StringSplitOptions.RemoveEmptyEntries) ??
                    Array.Empty<string>();

                if (typeNameAndVersion.Length != 2)
                {
                    throw new DiscriminationException($"Invalid discriminator string: {typeString}");
                }

                typeName = typeNameAndVersion[0].Trim();

                if (!uint.TryParse(typeNameAndVersion[1].Trim(), out typeVersion))
                {
                    throw new DiscriminationException($"Invalid type version in type: {typeString}");
                }
            }
            else
            {
                throw new DiscriminationException($"Missing discriminator element {ElementName}");
            }

            bsonReader.ReturnToBookmark(bookmark);

            return TypeDefinitions.NamesAndVersionsTypes
                .TryGetValue((typeName, typeVersion), out var type)
                ? type
                : throw new DiscriminationException(
                    $"Cannot find type of name {typeName} and version {typeVersion}");
        }

        public BsonValue GetDiscriminator(Type nominalType, Type actualType)
        {
            if (!typeof(INamedType).IsAssignableFrom(actualType))
            {
                throw new DiscriminationException(
                    $"Cannot use {nameof(NamedTypeDiscriminatorConvention)} for type {nominalType}");
            }

            var nameAndVersion = TypeDefinitions.ResolveNameAndVersion(actualType);

            return BsonValue.Create($"{nameAndVersion.Name}#{nameAndVersion.Version}");
        }
    }
}
