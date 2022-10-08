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

using Docker.DotNet.Models;

namespace Nd.Containers
{
    public sealed class MongoContainer : DockerContainerBase
    {
        private const string HostIPAddress = "127.0.0.1";
        private readonly string _host;
        private readonly string _port;
        private readonly string _username;
        private readonly string _password;

        public MongoContainer(
            string host = HostIPAddress,
            string port = "27017",
            string username = "mongoroot",
            string password = "secret") :
            base(new ConfigurationParameters
            {
                Image = "mongo",
                Tag = "6.0.2-focal",
                PortBindings = new Dictionary<string, IList<PortBinding>>
                {
                    ["27017/tcp"] = new List<PortBinding> {
                        new PortBinding {
                            HostIP = host,
                            HostPort = port
                        }
                    }
                },
                EnrironmentVariables = new Dictionary<string, string>
                {
                    ["MONGO_INITDB_ROOT_USERNAME"] = username,
                    ["MONGO_INITDB_ROOT_PASSWORD"] = password
                }
            })
        {
            _host = host;
            _port = port;
            _username = username;
            _password = password;
        }

        public string ConnectionString => $"mongodb://{_username}:{_password}@{_host}:{_port}";
    }
}
