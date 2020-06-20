using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using SharpBrick.PoweredUp.Protocol;
using SharpBrick.PoweredUp.Protocol.Messages;

namespace SharpBrick.PoweredUp
{
    public abstract class Device : IDisposable
    {
        private CompositeDisposable _compositeDisposable = new CompositeDisposable();

        protected readonly IPoweredUpProtocol _protocol;
        protected readonly byte _hubId;
        protected readonly byte _portId;
        protected readonly IObservable<PortValueData> _portValueObservable;

        public bool IsConnected => (_protocol != null);

        public Device()
        { }

        public Device(IPoweredUpProtocol protocol, byte hubId, byte portId)
        {
            _protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
            _hubId = hubId;
            _portId = portId;

            _portValueObservable = _protocol.UpstreamMessages
                .Where(msg => msg.HubId == _hubId)
                .SelectMany(msg => msg switch
                {
                    PortValueSingleMessage pvsm => pvsm.Data,
                    PortValueCombinedModeMessage pvcmm => pvcmm.Data,
                    _ => Array.Empty<PortValueData>(),
                })
                .Where(pvd => pvd.PortId == _portId);
        }

        protected void ObserveOnLocalProperty<TPayload>(IObservable<Value<TPayload>> modeObservable, params Action<Value<TPayload>>[] updaters)
        {
            var disposable = modeObservable.Subscribe(v =>
            {
                foreach (var u in updaters)
                {
                    u(v);
                }
            });

            _compositeDisposable.Add(disposable);
        }

        protected IObservable<Value<TPayload>> CreateSinglePortModeValueObservable<TPayload>(byte modeIndex)
            => _portValueObservable
                .Where(pvd => pvd.ModeIndex == modeIndex)
                .Cast<PortValueData<TPayload>>()
                .Select(pvd => new Value<TPayload>()
                {
                    Raw = pvd.InputValues[0],
                    SI = pvd.SIInputValues[0],
                    Pct = pvd.PctInputValues[0],
                });

        public async Task SetupNotificationAsync(byte modeIndex, bool enabled, uint deltaInterval = 5)
        {
            await _protocol.SendMessageAsync(new PortInputFormatSetupSingleMessage()
            {
                PortId = _portId,
                Mode = modeIndex,
                DeltaInterval = deltaInterval,
                NotificationEnabled = enabled,
            });
        }

        #region Disposable Pattern
        private bool disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _compositeDisposable?.Dispose();
                    _compositeDisposable = null;
                }

                // TODO: Nicht verwaltete Ressourcen (nicht verwaltete Objekte) freigeben und Finalizer überschreiben
                // TODO: Große Felder auf NULL setzen
                disposedValue = true;
            }
        }

        // // TODO: Finalizer nur überschreiben, wenn "Dispose(bool disposing)" Code für die Freigabe nicht verwalteter Ressourcen enthält
        // ~Device()
        // {
        //     // Ändern Sie diesen Code nicht. Fügen Sie Bereinigungscode in der Methode "Dispose(bool disposing)" ein.
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Ändern Sie diesen Code nicht. Fügen Sie Bereinigungscode in der Methode "Dispose(bool disposing)" ein.
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}