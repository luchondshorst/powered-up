using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SharpBrick.PoweredUp.Bluetooth;
using SharpBrick.PoweredUp.Devices;
using SharpBrick.PoweredUp.Protocol.Formatter;
using SharpBrick.PoweredUp.Protocol.Knowledge;
using SharpBrick.PoweredUp.Protocol.Messages;

namespace SharpBrick.PoweredUp.Protocol
{
    public class PoweredUpProtocol : IPoweredUpProtocol
    {
        private readonly BluetoothKernel _kernel;
        private readonly ILogger<PoweredUpProtocol> _logger;
        private readonly IDeviceFactory _deviceFactory;
        private Subject<(byte[] data, PoweredUpMessage message)> _upstreamSubject = null;

        public ProtocolKnowledge Knowledge { get; } = new ProtocolKnowledge();

        public IObservable<(byte[] data, PoweredUpMessage message)> UpstreamRawMessages => _upstreamSubject;
        public IObservable<PoweredUpMessage> UpstreamMessages => _upstreamSubject.Select(x => x.message);
        public IServiceProvider ServiceProvider { get; }

        public PoweredUpProtocol(BluetoothKernel kernel, ILogger<PoweredUpProtocol> logger, IDeviceFactory deviceFactory, IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
            _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
            _logger = logger;
            _deviceFactory = deviceFactory ?? throw new ArgumentNullException(nameof(_deviceFactory));
            _upstreamSubject = new Subject<(byte[] data, PoweredUpMessage message)>();
        }

        public async Task ConnectAsync()
        {
            await _kernel.ConnectAsync();

            await _kernel.ReceiveBytesAsync(async data =>
            {
                try
                {
                    var message = MessageEncoder.Decode(data, Knowledge);

                    await KnowledgeManager.ApplyDynamicProtocolKnowledge(message, Knowledge, _deviceFactory);

                    _upstreamSubject.OnNext((data, message));
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Exception in PoweredUpProtocol Decode/Knowledge");

                    throw;
                }
            });
        }

        public async Task DisconnectAsync()
        {
            await _kernel.DisconnectAsync();
        }

        public async Task SendMessageAsync(PoweredUpMessage message)
        {
            try
            {
                var data = MessageEncoder.Encode(message, Knowledge);

                await KnowledgeManager.ApplyDynamicProtocolKnowledge(message, Knowledge, _deviceFactory);

                await _kernel.SendBytesAsync(data);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception in PoweredUpProtocol Encode/Knowledge");

                throw;
            }
        }

        #region Disposable Pattern
        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _kernel?.Dispose();
                }
                disposedValue = true;
            }
        }

        // ~PoweredUpProtocol()
        // {
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}