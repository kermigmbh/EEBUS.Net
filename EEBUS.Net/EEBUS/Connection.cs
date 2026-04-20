using EEBUS.Messages;
using EEBUS.Models;
using EEBUS.Net;
using EEBUS.SHIP.Messages;
using EEBUS.SPINE.Commands;
using Microsoft.AspNetCore.Http;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
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

        private const int DefaultCloseMessageTimeoutMs = 1000;

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
                    AddressType? heartbeatSource = connection.Local?.GetHeartbeatAddress(true);
                    AddressType? heartbeatDestination = connection.Remote?.GetHeartbeatAddress(false);

                    if (heartbeatSource == null || heartbeatDestination == null) return;

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

                    

                    SpineDatagramPayload reply = new SpineDatagramPayload();
                    reply.datagram.header.addressSource = heartbeatSource;
                    reply.datagram.header.addressDestination = heartbeatSource;
                    reply.datagram.header.msgCounter = DataMessage.NextCount;
                    reply.datagram.header.cmdClassifier = "notify";

                    SpineCmdPayloadBase? heartbeat = heartbeatClass.CreateNotify(connection);
                    // serialize heartbeat into a JsonNode payload
                    reply.datagram.payload = heartbeat?.ToJsonNode();// JsonSerializer.SerializeToNode(heartbeat);

                    DataMessage heartbeatMessage = new DataMessage();
                    heartbeatMessage.SetPayload(JsonSerializer.SerializeToNode(reply) ?? throw new Exception("Failed to serialize heartbeat message"));

                    connection.PushDataMessage(heartbeatMessage);
                }
            }
        }

        public Connection(HostString host, WebSocket ws, Devices devices)
        {
            this.host = host;
            this.ws = ws;
            this.devices = devices;

            this.WaitingMessages = new(this);
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
            int timeout = (int?)closeMessage.connectionClose.FirstOrDefault()?.maxTime ?? DefaultCloseMessageTimeoutMs;

            TaskCompletionSource<ShipMessageBase?> tcs = new TaskCompletionSource<ShipMessageBase?>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingRequests.AddOrUpdate(closeMessage.GetId(), tcs, (msgId, completionSource) => completionSource);
            //TODO: rework DataMessageQueue to be able to handle every kind of message, so we can also send the close message over the queue. Could include an option enum, e.g. insert at the start/end, delete queue before insert, etc.
            await closeMessage.Send(WebSocket);
            this.state = EState.WaitingForCloseConfirm;
            ShipMessageBase? returnMessage = null;
            try
            {
                returnMessage = await tcs.Task.WaitAsync(TimeSpan.FromMilliseconds(timeout));
            } catch(Exception) {  }
            //tcs.TrySetCanceled();
            this.state = EState.Disconnected;
            _pendingRequests.TryRemove(closeMessage.GetId(), out _);
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
        protected async Task<ShipMessageBase> ReceiveAsync(CancellationToken cancellationToken)
        {
            int totalCount = 0;
            WebSocketReceiveResult result;

            // Accumulate frames until EndOfMessage
            do
            {
                if (totalCount >= _receiveBuffer.Length)
                    throw new Exception("EEBUS payload too large for receive buffer.");

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

            Debug.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff") + " <=== " + message.ToString() + "\n");

            return message;
        }


        public void RequestRemoteDeviceConfiguration()
        {
            //The order here is important! We first need to get the discovery data to create the entities
            SendNodeManagementDetailedDiscoveryRead();
            SendUseCaseDiscoveryRead();
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
            read.datagram.payload = discoveryPayload.ToJsonNode();// JsonSerializer.SerializeToNode(discoveryPayload);

            DataMessage message = new DataMessage();
            message.SetPayload(JsonSerializer.SerializeToNode(read) ?? throw new Exception("Failed to serialize discovery read message"));

            PushDataMessage(message);
        }

        private void SendUseCaseDiscoveryRead()
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
            read.datagram.payload = discoveryPayload?.ToJsonNode();// JsonSerializer.SerializeToNode(discoveryPayload);

            DataMessage message = new DataMessage();
            message.SetPayload(JsonSerializer.SerializeToNode(read) ?? throw new Exception("Failed to serialize use case discovery read message"));

            PushDataMessage(message);
        }

        public void HeartbeatSubscription()
        {
            SpineDatagramPayload call = new SpineDatagramPayload();
            call.datagram.header.addressSource = new();
            call.datagram.header.addressSource.device = this.Local.DeviceId;
            call.datagram.header.addressSource.entity = [0];
            call.datagram.header.addressSource.feature = 0;
            call.datagram.header.addressDestination = new();
            call.datagram.header.addressDestination.device = this.Remote.DeviceId;
            call.datagram.header.addressDestination.entity = [0];
            call.datagram.header.addressDestination.feature = 0;
            call.datagram.header.msgCounter = DataMessage.NextCount;
            call.datagram.header.cmdClassifier = "call";

            NodeManagementSubscriptionRequestCall payload = new NodeManagementSubscriptionRequestCall();
            SubscriptionRequestType subscriptionRequest = payload.cmd[0].nodeManagementSubscriptionRequestCall.subscriptionRequest;
            subscriptionRequest.clientAddress = this.Local.GetHeartbeatAddress(false);
            subscriptionRequest.serverAddress = this.Remote.GetHeartbeatAddress(true);
            subscriptionRequest.serverFeatureType = "DeviceDiagnosis";

            call.datagram.payload = payload.ToJsonNode();//JsonSerializer.SerializeToNode(payload);

            DataMessage message = new DataMessage();
            message.SetPayload(JsonSerializer.SerializeToNode(call) ?? throw new Exception("Failed to serialize heartbeat subscription message"));

            PushDataMessage(message);
        }

        public void HeartbeatRead()
        {
            AddressType source = this.Local.GetHeartbeatAddress(false);
            AddressType  destination = this.Remote?.GetHeartbeatAddress(true) ?? throw new Exception("Remote device is not available");

            SpineDatagramPayload read = new SpineDatagramPayload();
            read.datagram.header.addressSource = source;
            read.datagram.header.addressDestination = destination;
            read.datagram.header.msgCounter = DataMessage.NextCount;
            read.datagram.header.cmdClassifier = "read";

            var heartbeatReadPayload = new DeviceDiagnosisHeartbeatData.Class().CreateRead(this);
            read.datagram.payload = heartbeatReadPayload.ToJsonNode();// JsonSerializer.SerializeToNode(heartbeatReadPayload);

            DataMessage message = new DataMessage();
            message.SetPayload(JsonSerializer.SerializeToNode(read) ?? throw new Exception("Failed to serialize heartbeat read message"));

            PushDataMessage(message);
        }

        public void ReadAndSubscribe()
        {
            if (this.Remote != null && this.Local != null)
            {
                foreach (Entity entity in this.Remote.Entities)
                {
                    foreach (Feature feature in entity.Features)
                    {
                        if (feature.Role != "server") continue;

                        AddressType? featureSourceAddress = this.Local.GetFeatureAddress(feature.Type, false);    //client address
                        AddressType? featureDestinationAddress = this.Remote.GetFeatureAddress(feature.Type, true);  //server address


                        if (featureSourceAddress != null && featureDestinationAddress != null)
                        {
                            foreach (Function function in feature.Functions)
                            {
                                if (function.SupportedFunction.possibleOperations.read != null)
                                {
                                    //read
                                    SpineCmdPayloadBase? payload = SpineCmdPayloadBase.GetClass(function.SupportedFunction.function)?.CreateRead(this);
                                    DataMessage readMessage = DataMessage.CreateRead(featureSourceAddress, featureDestinationAddress, payload);
                                    PushDataMessage(readMessage);
                                }
                            }

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
