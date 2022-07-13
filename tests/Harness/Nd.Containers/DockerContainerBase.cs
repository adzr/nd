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

using System.Security.Cryptography.X509Certificates;
using Docker.DotNet;
using Docker.DotNet.BasicAuth;
using Docker.DotNet.Models;
using Docker.DotNet.X509;
using Nd.Core.Threading;
using Version = System.Version;

namespace Nd.Containers
{
    public class ConfigurationParameters
    {
        public Uri? Uri { get; private set; }
        public X509Certificate? Certificate { get; private set; }
        public string? AccessToken { get; private set; } = "7b821549-0412-40c9-92b5-6892a9f8411d";
        public string Image { get; private set; } = "mongo";
        public string Tag { get; private set; } = "latest";
        public string? Version { get; internal set; }
        public string? Username { get; internal set; }
        public string? Password { get; internal set; }
    }

    public abstract class DockerContainerBase : IDisposable
    {
        private readonly IAsyncLocker _locker;
        private readonly ConfigurationParameters _config;
        private readonly DockerClientConfiguration _configuration;
        private readonly DockerClient _client;

        private X509Certificate2? _certificate;
        private Credentials? _credentials;
        private CreateContainerResponse? _container;

        private bool _started;
        private bool _disposed;

        protected DockerContainerBase(ConfigurationParameters config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            _certificate = _config.Certificate is null ? null : new X509Certificate2(_config.Certificate);

            _credentials = _certificate is not null ?
                new CertificateCredentials(_certificate) :
                !string.IsNullOrWhiteSpace(_config.Username) && !string.IsNullOrWhiteSpace(_config.Password) ?
                    new BasicAuthCredentials(_config.Username, _config.Password) :
                    null;

            _configuration = _config.Uri is null ?
                new DockerClientConfiguration(credentials: _credentials) :
                new DockerClientConfiguration(_config.Uri, _credentials);

            _client = _config.Version is null ?
                _configuration.CreateClient() :
                _configuration.CreateClient(new Version(_config.Version));

            _locker = ExclusiveAsyncLocker.Create();
        }

        public async Task StartAsync(CancellationToken cancellation = default)
        {
            using var @lock = await _locker.WaitAsync(cancellation).ConfigureAwait(false);

            var progress = new Progress<JSONMessage>();

            progress.ProgressChanged += (e, m) =>
            {
                // TODO: log progress.
            };

            await _client.Images.CreateImageAsync(
                new ImagesCreateParameters
                {
                    FromImage = _config.Image,
                    Tag = _config.Tag
                },
                new AuthConfig
                {
                    IdentityToken = _config.AccessToken
                },
                progress,
                cancellation
            ).ConfigureAwait(false);

            _container = await _client.Containers.CreateContainerAsync(
                new CreateContainerParameters
                {
                    Image = _config.Image
                }, cancellation).ConfigureAwait(false);

            _started = await _client.Containers.StartContainerAsync(_container.ID,
                new ContainerStartParameters { }, cancellation).ConfigureAwait(false);
        }

        public async Task StopAsync(CancellationToken cancellation = default)
        {
            using var @lock = await _locker.WaitAsync(cancellation).ConfigureAwait(false);
            await StopContainerInternal(cancellation).ConfigureAwait(false);
        }

        private async Task StopContainerInternal(CancellationToken cancellation = default)
        {
            if (_started && _container is not null)
            {
                _started = !await _client.Containers.StopContainerAsync(_container.ID,
                    new ContainerStopParameters { }, cancellation).ConfigureAwait(false);

                if (!_started)
                {
                    await _client.Containers.RemoveContainerAsync(_container.ID,
                        new ContainerRemoveParameters
                        {
                            RemoveLinks = true,
                            RemoveVolumes = true
                        }, cancellation).ConfigureAwait(false);
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~DockerContainerBase() => Dispose(false);

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            using var @lock = _locker.Wait();

            if (_disposed)
            {
                return;
            }

            _disposed = true;

            StopContainerInternal().GetAwaiter().GetResult();

            _container = null;
            _client.Dispose();
            _configuration.Dispose();
            _credentials?.Dispose();
            _credentials = null;
            _certificate?.Dispose();
            _certificate = null;
            _locker.Dispose();
        }
    }

    public sealed class MongoContainer : DockerContainerBase
    {
        public MongoContainer(ConfigurationParameters config) : base(config)
        {
        }
    }
}
