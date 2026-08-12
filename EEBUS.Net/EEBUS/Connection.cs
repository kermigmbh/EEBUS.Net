using EEBUS.Messages;
using EEBUS.Models;
using EEBUS.Net;
using EEBUS.SHIP.Messages;
using EEBUS.SPINE.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;


namespace EEBUS
{
    public abstract class Connection
    {
        protected HostString host;
        protected WebSocket ws;
        protected EState state;
        protected ESubState subState;
        public DeviceConnectionStatus ConnectionStatus { get; internal set; } = DeviceConnectionStatus.Unknown;
        private ConcurrentDictionary<string, TaskCompletionSource<ShipMessageBase?>> _pendingRequests = new();

        public HostString RemoteHost
        {
            get
            {
                return host;
            }
        }

        public enum EState
        {
            Disconnected,
            WaitingForConnectionHello,
            WaitingForProtocolHandshake,
            SendProtocolHandshakeError,
            SendProtocolHandshakeConfirm,
            WaitingForProtocolHandshakeConfirm,
            WaitingForPinCheck,
            WaitingForAccessMethodsRequest,
            WaitingForAccessMethods,
            Connected,
            Stopped,
            ErrorOrTimeout,
            WaitingForCloseConfirm
        }

        public enum ESubState
        {
            None,
            FirstPending,
            SecondPending,
            UnexpectedMessage,
            FormatMismatch
        }

        protected class HeartBeatTask
        {
            private bool heartbeatSubscribed = false;
            private DeviceDiagnosisHeartbeatData.Class heartbeatClass = new DeviceDiagnosisHeartbeatData.Class();
            // This method is called by the timer delegate.
            public void Beat(object? connectionObj)
            {
                Connection? connection = connectionObj as Connection;
                if (connection == null) return;

                if (connection.State == Connection.EState.Connected)
                {
                    //AddressType? heartbeatSource = connection.Local?.GetFeatureAddress("DeviceDiagnosis", true, connection);
                    //AddressType? heartbeatDestination = connection.Remote?.GetFeatureAddress("DeviceDiagnosis", false, connection);

                    if (!this.heartbeatSubscribed)
                    {
                        this.heartbeatSubscribed = true;

                        if (connection is Server)
                            Debug.WriteLine("--- Request heartbeat via server ---");
                        else
                            Debug.WriteLine("--- Request heartbeat via client ---");

                        connection.HeartbeatSubscription();
                        connection.HeartbeatRead();
                    }

                    if (connection is Server)
                        Debug.WriteLine("--- Send heartbeat via server ---");
                    else
                        Debug.WriteLine("--- Send heartbeat via client ---");


                    AddressType? heartbeatSource = connection.GetLocalHeartbeatAddress(true);
                    AddressType? heartbeatDestination = connection.GetRemoteHeartbeatAddress(false);

                    if (heartbeatSource == null || heartbeatDestination == null) return;
                    SpineDatagramPayload reply = new SpineDatagramPayload();
                    reply.datagram.header.addressSource = heartbeatSource;
                    reply.datagram.header.addressDestination = heartbeatDestination;
                    reply.datagram.header.msgCounter = DataMessage.NextCount;
                    reply.datagram.header.cmdClassifier = "notify";

                    SpineCmdPayloadBase? heartbeat = heartbeatClass.CreateNotify(connection);
                    // serialize heartbeat into a JsonNode payload
                    reply.datagram.payload = heartbeat?.ToJsonNode();

                    DataMessage heartbeatMessage = new DataMessage();
                    heartbeatMessage.SetPayload(JsonHelper.ToJsonNode(reply) ?? throw new Exception("Failed to serialize heartbeat message"));

                    connection.PushDataMessage(heartbeatMessage);
                }
            }
        }

        protected ILogger? Logger { get; private set; }
        public Connection(HostString host, WebSocket ws, Devices devices, ILogger? logger = null)
        {
            this.host = host;
            this.ws = ws;
            this.devices = devices;
            Logger = logger;

            this.WaitingMessages = new(this, logger);
            this.BindingAndSubscriptionManager = new BindingAndSubscriptionManager(this);
        }

        public BindingAndSubscriptionManager BindingAndSubscriptionManager { get; }

        public WebSocket WebSocket { get { return this.ws; } }

        public EState State { get { return this.state; } }

        public ESubState SubState { get { return this.subState; } }


        private Devices devices;

        public LocalDevice Local { get { return this.devices.Local; } }

        public RemoteDevice? Remote { get; protected set; }


        public DataMessageQueue WaitingMessages { get; protected set; }


        public abstract Task CloseAsync();

        protected RemoteDevice? GetRemote(string id)
        {
            if (null == id)
                return null;

            return this.devices.GetRemote(id);
        }

        internal bool IsKnownRemote(string id)
        {
            var remote = this.devices.GetRemote(id);
            return remote != null;
        }
        public async Task<DataMessage> PushDataMessageAsync(DataMessage message) 
        {
            uint timeout = 5000;
            string messageId = message.GetId();
            TaskCompletionSource<ShipMessageBase?> tcs = new TaskCompletionSource<ShipMessageBase?>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingRequests.AddOrUpdate(messageId, tcs, (msgId, completionSource) => completionSource);

            this.WaitingMessages.Push(message);

            try
            {
                var returnMessage = await tcs.Task.WaitAsync(TimeSpan.FromMilliseconds(timeout)).ConfigureAwait(false);
                return returnMessage as DataMessage ?? throw new Exception("Received message is not a DataMessage");
            }
            finally
            {
                _pendingRequests.TryRemove(messageId, out _);
            }
        }



        public void PushDataMessage(DataMessage message)
        {
            this.WaitingMessages.Push(message);
        }

        /// <summary>
        /// Pushes a close message, signalling the end of communication
        /// </summary>
        /// <param name="closeMessage">The message to send</param>
        /// <returns>The answer to <paramref name="closeMessage"/>, or null if no answer was sent within the specified maxTime</returns>
        public async Task<CloseMessage?> PushCloseMessageAsync(CloseMessage closeMessage)
        {
            uint timeout = closeMessage.connectionClose.maxTime;
            string messageId = closeMessage.GetId();

            TaskCompletionSource<ShipMessageBase?> tcs = new TaskCompletionSource<ShipMessageBase?>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingRequests.AddOrUpdate(messageId, tcs, (msgId, completionSource) => completionSource);

            ShipMessageBase? returnMessage = null;
            try
            {
                //TODO: rework DataMessageQueue to be able to handle every kind of message, so we can also send the close message over the queue. Could include an option enum, e.g. insert at the start/end, delete queue before insert, etc.
                await closeMessage.Send(WebSocket, Logger).ConfigureAwait(false);
                this.state = EState.WaitingForCloseConfirm;

                try
                {
                    returnMessage = await tcs.Task.WaitAsync(TimeSpan.FromMilliseconds(timeout)).ConfigureAwait(false);
                }
                catch (TimeoutException) { /* no reply within maxTime - swallow, returnMessage stays null */ }
                catch (OperationCanceledException) { /* cancelled - swallow, returnMessage stays null */ }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "PushCloseMessageAsync: failed to send close message.");
                throw;
            }
            finally
            {
                _pendingRequests.TryRemove(messageId, out _);
            }

            this.state = EState.Disconnected;
            return returnMessage as CloseMessage;
        }

        protected void ResolvePendingRequest(ShipMessageBase message)
        {
            if (message.GetMessageDirection() != Net.EEBUS.Models.ShipMessageDirection.Response) return;

            string? referencedId = message.GetReferencedId();
            if (string.IsNullOrEmpty(referencedId)) return;

            if (_pendingRequests.TryGetValue(referencedId, out TaskCompletionSource<ShipMessageBase?>? tcs))
            {
                tcs?.TrySetResult(message);
            }
        }

        private byte[] _receiveBuffer = new byte[10240];
        // Hard cap so a malicious or buggy peer can't make us allocate
        // unbounded memory. 1 MiB is well beyond anything legitimate SHIP/SPINE
        // traffic should produce.
        private const int MaxReceiveBufferSize = 1024 * 1024;
        protected async Task<ShipMessageBase> ReceiveAsync(CancellationToken cancellationToken)
        {
            int totalCount = 0;
            WebSocketReceiveResult result;

            // Accumulate frames until EndOfMessage
            do
            {
                if (totalCount >= _receiveBuffer.Length)
                {
                    if (_receiveBuffer.Length >= MaxReceiveBufferSize)
                        throw new Exception("EEBUS payload exceeds maximum receive buffer size (" + MaxReceiveBufferSize + " bytes).");
                    Array.Resize(ref _receiveBuffer, Math.Min(_receiveBuffer.Length * 2, MaxReceiveBufferSize));
                }

                var segment = new ArraySegment<byte>(
                    _receiveBuffer,
                    totalCount,
                    _receiveBuffer.Length - totalCount);

                result = await ws.ReceiveAsync(segment, cancellationToken).ConfigureAwait(false);

                if (result.CloseStatus.HasValue || result.MessageType == WebSocketMessageType.Close)
                {
                    this.state = EState.Stopped;
                    break;
                }

                totalCount += result.Count;

            } while (!result.EndOfMessage && !cancellationToken.IsCancellationRequested);

            if (this.state == EState.Stopped || cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException();
            }

            ReadOnlySpan<byte> messageSpan = _receiveBuffer.AsSpan(0, totalCount);

            ShipMessageBase? message = ShipMessageBase.Create(messageSpan);
            if (message == null)
            {
                throw new Exception("Message couldn't be recognized");
            }

            Logger?.LogTrace(DateTime.Now.ToString("HH:mm:ss.fff") + " <--- " + Encoding.UTF8.GetString(messageSpan) + "\n");

            return message;
        }


        public void RequestRemoteDeviceConfiguration()
        {
            //The order here is important! We first need to get the discovery data to create the entities
            SendNodeManagementDetailedDiscoveryRead();
            //SendUseCaseDiscoveryRead();
        }

        private void SendNodeManagementDetailedDiscoveryRead()
        {
            SpineDatagramPayload read = new SpineDatagramPayload();
            read.datagram.header.addressSource = new();
            read.datagram.header.addressSource.device = this.Local.DeviceId;
            read.datagram.header.addressSource.entity = [0];
            read.datagram.header.addressSource.feature = 0;
            read.datagram.header.addressDestination = new();
            read.datagram.header.addressDestination.entity = [0];
            read.datagram.header.addressDestination.feature = 0;
            read.datagram.header.msgCounter = DataMessage.NextCount;
            read.datagram.header.cmdClassifier = "read";

            var discoveryPayload = new NodeManagementDetailedDiscoveryData.Class().CreateRead(this);
            read.datagram.payload = discoveryPayload.ToJsonNode();

            DataMessage message = new DataMessage();
            message.SetPayload(JsonHelper.ToJsonNode(read) ?? throw new Exception("Failed to serialize discovery read message"));

            PushDataMessage(message);
        }

        public void SendUseCaseDiscoveryRead()
        {
            SpineDatagramPayload read = new SpineDatagramPayload();
            read.datagram.header.addressSource = new();
            read.datagram.header.addressSource.device = this.Local.DeviceId;
            read.datagram.header.addressSource.entity = [0];
            read.datagram.header.addressSource.feature = 0;
            read.datagram.header.addressDestination = new();
            read.datagram.header.addressDestination.entity = [0];
            read.datagram.header.addressDestination.feature = 0;
            read.datagram.header.msgCounter = DataMessage.NextCount;
            read.datagram.header.cmdClassifier = "read";

            var discoveryPayload = new NodeManagementUseCaseData.Class().CreateRead(this);
            read.datagram.payload = discoveryPayload?.ToJsonNode();

            DataMessage message = new DataMessage();
            message.SetPayload(JsonHelper.ToJsonNode(read) ?? throw new Exception("Failed to serialize use case discovery read message"));

            PushDataMessage(message);
        }

        public void HeartbeatSubscription()
        {
            //SpineDatagramPayload call = new SpineDatagramPayload();
            //call.datagram.header.addressSource = new();
            //call.datagram.header.addressSource.device = this.Local.DeviceId;
            //call.datagram.header.addressSource.entity = [0];
            //call.datagram.header.addressSource.feature = 0;
            //call.datagram.header.addressDestination = new();
            //call.datagram.header.addressDestination.device = this.Remote.DeviceId;
            //call.datagram.header.addressDestination.entity = [0];
            //call.datagram.header.addressDestination.feature = 0;
            //call.datagram.header.msgCounter = DataMessage.NextCount;
            //call.datagram.header.cmdClassifier = "call";

            //NodeManagementSubscriptionRequestCall payload = new NodeManagementSubscriptionRequestCall();
            //SubscriptionRequestType subscriptionRequest = payload.cmd[0].nodeManagementSubscriptionRequestCall.subscriptionRequest;

            var clientAddress = GetLocalHeartbeatAddress(false);
            var serverAddress = GetRemoteHeartbeatAddress(true);

            if (clientAddress == null || serverAddress == null)
            {
                return;
            }

            //subscriptionRequest.clientAddress = clientAddress;
            //subscriptionRequest.serverAddress = serverAddress;
            //subscriptionRequest.serverFeatureType = "DeviceDiagnosis";

            //call.datagram.payload = payload.ToJsonNode();

            //DataMessage message = new DataMessage();
            //message.SetPayload(JsonHelper.ToJsonNode(call) ?? throw new Exception("Failed to serialize heartbeat subscription message"));
            if (Remote == null) return;
            DataMessage message = DataMessage.CreateSubscription(clientAddress, serverAddress, "DeviceDiagnosis", Local.DeviceId, Remote.DeviceId);
            PushDataMessage(message);
        }

        public void HeartbeatRead()
        {
            if (Remote == null) throw new Exception("Remote device is not available");
            AddressType? source = GetLocalHeartbeatAddress(false);
            AddressType? destination = GetRemoteHeartbeatAddress(true);

            if (source == null || destination == null) return;

            SpineDatagramPayload read = new SpineDatagramPayload();
            read.datagram.header.addressSource = source;
            read.datagram.header.addressDestination = destination;
            read.datagram.header.msgCounter = DataMessage.NextCount;
            read.datagram.header.cmdClassifier = "read";

            var heartbeatReadPayload = new DeviceDiagnosisHeartbeatData.Class().CreateRead(this);
            read.datagram.payload = heartbeatReadPayload.ToJsonNode();// JsonSerializer.SerializeToNode(heartbeatReadPayload);

            DataMessage message = new DataMessage();
            message.SetPayload(JsonHelper.ToJsonNode(read) ?? throw new Exception("Failed to serialize heartbeat read message"));

            PushDataMessage(message);
        }

        private AddressType? GetLocalHeartbeatAddress(bool server)
        {
            string role = server ? "server" : "client";

            IEnumerable<AddressType> addresses = this.Local.GetAllFeatureAddresses("DeviceDiagnosis", server);

            if (addresses.Count() == 1)
            {
                return addresses.Single();
            } else
            {
                /* Should normally not happen, but it could be that we have multiple entities which offer the DeviceDiagnosis feature.
                 * In this case we will just return the first one, as we right now don't have any other information to select a specific one.
                */
                return addresses.FirstOrDefault();
            }
        }

        private AddressType? GetRemoteHeartbeatAddress(bool server)
        {
            if (Remote == null) return null;

            string role = server ? "server" : "client";
            Debug.WriteLine($"[{Local.Name}] GetRemoteHeartBeatAddress for {role}");
            IEnumerable<AddressType> addresses = this.Remote.GetAllFeatureAddresses("DeviceDiagnosis", server);

            if (addresses.Count() == 1)
            {
                Debug.WriteLine($"[{Local.Name}] Found exactly one address, returning...");
                return addresses.Single();
            }
            else
            {
                /* If the communication partner has multiple entities which offer the DeviceDiagnosis feature, we will try to find the one which is bound to our LoadControl feature.
                 * This is specified in the testing specification for lpc and lpp.
                 */
                BindingSubscriptionInfo? binding = BindingAndSubscriptionManager.GetBindings("LoadControl").FirstOrDefault();
                if (binding == null) return null;

                Entity? entity = Remote.Entities.FirstOrDefault(e => e.Index.SequenceEqual(binding.serverAddress.entity));
                Feature? feature = entity?.Features.Find(f => null != f && f.Type == "DeviceDiagnosis" && f.Role == role);
                if (entity != null && feature != null)
                {
                    return new AddressType() { device = Remote.DeviceId, entity = entity.Index, feature = feature.Index };
                }

                return null;
            }
        }


        public void ReadAndSubscribe()
        {
            if (Remote == null || Local == null) return;

            foreach (Entity entity in Remote.Entities)
            {
                foreach (UseCase useCase in entity.UseCases)
                {
                    foreach (var feature in useCase.Features)
                    {
                        AddressType? featureSourceAddress = this.Local.GetFeatureAddress(feature.Type, false);    //client address
                        AddressType? featureDestinationAddress = this.Remote.GetFeatureAddress(feature.Type, true);  //server address
                        if (featureSourceAddress == null || featureDestinationAddress == null) continue;

                        //Binding
                        if (useCase.SupportsBinding(feature))
                        {
                            if (!BindingAndSubscriptionManager.HasBinding(featureSourceAddress, featureDestinationAddress))
                            {
                                DataMessage callMessage = DataMessage.CreateBinding(featureSourceAddress, featureDestinationAddress, feature.Type, Local.DeviceId, Remote.DeviceId);
                                PushDataMessage(callMessage);
                            }
                        }

                        if (useCase.SupportsSubscription(feature) && feature.Type != "DeviceDiagnosis") //we have our own logic for heartbeat subscription, so we skip it here
                        {
                            //Reading
                            foreach (Function function in feature.Functions)
                            {
                                if (function.SupportedFunction.possibleOperations.read != null)
                                {
                                    SpineCmdPayloadBase? payload = SpineCmdPayloadBase.GetClass(function.SupportedFunction.function)?.CreateRead(this);
                                    DataMessage readMessage = DataMessage.CreateRead(featureSourceAddress, featureDestinationAddress, payload);
                                    PushDataMessage(readMessage);
                                }
                            }

                            //Subscribing
                            if (!BindingAndSubscriptionManager.HasSubscription(featureSourceAddress, featureDestinationAddress))
                            {
                                DataMessage callMessage = DataMessage.CreateSubscription(featureSourceAddress, featureDestinationAddress, feature.Type, Local.DeviceId, Remote.DeviceId);
                                PushDataMessage(callMessage);
                            }
                        }
                    }
                }
            }
        }
    }
}
