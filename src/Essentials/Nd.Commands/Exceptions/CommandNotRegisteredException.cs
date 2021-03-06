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

namespace Nd.Commands.Exceptions
{
    [Serializable]
    public class CommandNotRegisteredException : Exception
    {
        public CommandNotRegisteredException(ICommand command, Exception? exception = default)
            : base($"Command \"{command?.TypeName ?? throw new ArgumentNullException(nameof(command))}\" is not found in the command registery, cannot find a handler for an unknown command", exception)
        {
            CommandTypeName = command.TypeName;
        }

        public string? CommandTypeName { get; }

        public CommandNotRegisteredException() : this($"Command is not found in the command registery, cannot find a handler for an unknown command")
        {
        }

        public CommandNotRegisteredException(string message) : base(message)
        {
        }

        public CommandNotRegisteredException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected CommandNotRegisteredException(SerializationInfo serializationInfo, StreamingContext streamingContext)
        {
            throw new NotImplementedException();
        }
    }
}
