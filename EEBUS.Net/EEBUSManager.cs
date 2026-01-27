using EEBUS.DataStructures;
using EEBUS.KeyValues;
using EEBUS.Models;
using EEBUS.SHIP.Messages;
using EEBUS.UseCases.ControllableSystem;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;

namespace EEBUS.Net
{
    public class EEBUSManager : IDisposable
    {
        private ClientWebSocket? _wsClient;
        private ConcurrentDictionary<HostString, Client> _clients = new();
        private Devices _devices;
        private readonly MDNSClient _mDNSClient;
        private readonly MDNSService _mDNSService;

        public event EventHandler<RemoteDevice> DeviceFound;

        public EEBUSManager(Devices devices, MDNSClient mDNSClient, MDNSService mDNSService)
        {
            this._devices = devices;
            this._mDNSClient = mDNSClient;
            this._mDNSService = mDNSService;

            this._devices = devices;

            this._devices.RemoteDeviceFound += OnRemoteDeviceFound;
            this._devices.ServerStateChanged += OnServerStateChanged;
            this._devices.ClientStateChanged += OnClientStateChanged;

            this._devices.Local.AddUseCaseEvents(this.lpcEventHandler);
            this._devices.Local.AddUseCaseEvents(this.lppEventHandler);
            this._devices.Local.AddUseCaseEvents(this.lpcOrLppEventHandler);
        }
        public void Dispose()
        {
            _devices.RemoteDeviceFound -= OnRemoteDeviceFound;
            _devices.ServerStateChanged -= OnServerStateChanged;
            _devices.ClientStateChanged -= OnClientStateChanged;
            
            _devices.Local.RemoveUseCaseEvents(this.lpcEventHandler);
            _devices.Local.RemoveUseCaseEvents(this.lppEventHandler);
            _devices.Local.RemoveUseCaseEvents(this.lpcOrLppEventHandler);
        }
        private void OnRemoteDeviceFound(RemoteDevice device)
        {
            //using var _ = Push(new RemoteDeviceFound(device));
            DeviceFound?.Invoke(this, device);
        }

        private void OnServerStateChanged(Connection.EState state, RemoteDevice device)
        {
            //using var _ = Push(new ServerStateChanged(device, state));
        }

        private void OnClientStateChanged(Connection.EState state, RemoteDevice device)
        {
            //using var _ = Push(new ClientStateChanged(device, state));
        }

        private LPCEventHandler lpcEventHandler = new();
        private LPPEventHandler lppEventHandler = new();
        private LPCorLPPEventHandler lpcOrLppEventHandler = new();

        private class LPCEventHandler : LPCEvents
        {
            public void DataUpdateLimit(int counter, bool active, long limit, TimeSpan duration)
            {
                //using var _ = Push(new LimitDataChanged(true, active, limit, duration));
            }

            public void DataUpdateFailsafeConsumptionActivePowerLimit(int counter, long limit)
            {
                //using var _ = Push(new FailsafeLimitDataChanged(true, limit));
            }
        }

        private class LPPEventHandler : LPPEvents
        {
            public void DataUpdateLimit(int counter, bool active, long limit, TimeSpan duration)
            {
                //using var _ = Push(new LimitDataChanged(false, active, limit, duration));
            }

            public void DataUpdateFailsafeProductionActivePowerLimit(int counter, long limit)
            {
                //using var _ = Push(new FailsafeLimitDataChanged(false, limit));
            }
        }

        private class LPCorLPPEventHandler : LPCorLPPEvents
        {
            public void DataUpdateFailsafeDurationMinimum(int counter, TimeSpan duration)
            {
                //using var _ = Push(new FailsafeLimitDurationChanged(duration));
            }

            public void DataUpdateHeartbeat(int counter, RemoteDevice device, uint timeout)
            {
                //using var _ = Push(new HeartbeatReceived(device, timeout));
            }
        }

        public JObject GetLocal()
        {
            LocalDevice? local = _devices?.Local;

            if (null == local)
                return new();

            bool lpcActive = false;
            long lpcLimit = 0;
            TimeSpan lpcDuration = new();
            long lpcFailsafeLimit = 0;

            bool lppActive = false;
            long lppLimit = 0;
            TimeSpan lppDuration = new();
            long lppFailsafeLimit = 0;

            TimeSpan failsafeDuration = new();

            foreach (LoadControlLimitDataStructure data in local.GetDataStructures<LoadControlLimitDataStructure>())
            {
                if (data.LimitDirection == "consume")
                {
                    lpcActive = data.LimitActive;
                    lpcLimit = data.Number;
                    lpcDuration = XmlConvert.ToTimeSpan(data.EndTime);
                }
                else if (data.LimitDirection == "produce")
                {
                    lppActive = data.LimitActive;
                    lppLimit = data.Number;
                    lppDuration = XmlConvert.ToTimeSpan(data.EndTime);
                }
            }

            FailsafeConsumptionActivePowerLimitKeyValue lpcFailsafeLimitKeyValue = local.GetKeyValue<FailsafeConsumptionActivePowerLimitKeyValue>();
            if (null != lpcFailsafeLimitKeyValue)
                lpcFailsafeLimit = lpcFailsafeLimitKeyValue.Value;

            FailsafeProductionActivePowerLimitKeyValue lppFailsafeLimitKeyValue = local.GetKeyValue<FailsafeProductionActivePowerLimitKeyValue>();
            if (null != lppFailsafeLimitKeyValue)
                lppFailsafeLimit = lppFailsafeLimitKeyValue.Value;

            FailsafeDurationMinimumKeyValue failsafeDurationKeyValue = local.GetKeyValue<FailsafeDurationMinimumKeyValue>();
            if (null != lppFailsafeLimitKeyValue)
                failsafeDuration = XmlConvert.ToTimeSpan(failsafeDurationKeyValue.Duration);

            return JObject.FromObject(new
            {
                name = local.Name,
                ski = local.SKI.ToReadable(),
                shipId = local.ShipID,

                lpcActive = lpcActive,
                lpcLimit = lpcLimit,
                lpcDuration = lpcDuration,
                lpcFailsafeLimit = lpcFailsafeLimit,

                lppActive = lppActive,
                lppLimit = lppLimit,
                lppDuration = lppDuration,
                lppFailsafeLimit = lppFailsafeLimit,

                failsafeDuration = failsafeDuration
            });
        }

        public JArray GetRemotes()
        {
            JArray devlist = new();

            _devices.Remote.ForEach(rd =>
            {
                devlist.Add(JObject.FromObject(new
                {
                    id = rd.Id,
                    name = rd.Name,
                    ski = rd.SKI.ToReadable(),
                    url = rd.Url,
                    serverState = rd.serverState.ToString(),
                    clientState = rd.clientState.ToString()
                }));
            });

            return devlist;
        }

        public async Task<string?> Connect(string ski)
        {
            SKI Ski = new SKI(ski);

            RemoteDevice? device = _devices.Remote.FirstOrDefault(rd => rd.SKI == Ski);
            if (null == device)
            {
                return null;
            }

            try
            {
                Uri uri = new Uri("wss://" + device.Url);

                _wsClient = new ClientWebSocket();
                _wsClient.Options.AddSubProtocol("ship");
                _wsClient.Options.RemoteCertificateValidationCallback = (object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors) => true;
                _wsClient.Options.ClientCertificates.Add(this._mDNSService.Cert);
                await _wsClient.ConnectAsync(uri, CancellationToken.None).ConfigureAwait(false);

                HostString hostString = new HostString(uri.Host, uri.Port);

                if (_wsClient.State == WebSocketState.Open)
                {
                    Client client = new Client(hostString, _wsClient, _devices, device);
                    _clients[hostString] = client;
                    await client.Run().ConfigureAwait(false);
                    return hostString.ToString();
                }
                else
                {
                    await Disconnect(hostString).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Connect Error: " + ex.Message);
            }
            return null;
        }

        public async Task Disconnect(HostString host)
        {
            try
            {
                var wsClient = _clients.TryGetValue(host, out Client? client) ? client?.WebSocket : null;
                if (wsClient == null)
                    return;

                // send close message
                CloseMessage closeMessage = new CloseMessage(ConnectionClosePhaseType.announce);
                await closeMessage.Send(wsClient);

                // wait for close response message from server
                closeMessage = await CloseMessage.Receive(wsClient);

                if (closeMessage == null)
                {
                    throw new Exception("Close message parsing failed!");
                }

                if (closeMessage.connectionClose[0].phase != ConnectionClosePhaseType.confirm)
                {
                    throw new Exception("Close confirmation message expected!");
                }

                // now close websocket
                await wsClient.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None).ConfigureAwait(false);
                wsClient.Dispose();
                wsClient = null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Disconnect Error: " + ex.Message);
            }
            finally
            {
                _clients.TryRemove(host, out _);
            }
        }

        public void StartDeviceSearch()
        {
            _mDNSClient.Run(_devices);
        }
    }
}
