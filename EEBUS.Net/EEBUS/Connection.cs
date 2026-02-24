using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.AspNetCore.Http;



using EEBUS.Messages;
using EEBUS.Models;
using EEBUS.SHIP.Messages;
using EEBUS.SPINE.Commands;
using EEBUS.Net.EEBUS.UseCases.GridConnectionPoint;
using EEBUS.Net;


namespace EEBUS
{
    public abstract class Connection
    {
        protected HostString host;
        protected WebSocket ws;
        protected EState state;
        protected ESubState subState;
        public DeviceConnectionStatus ConnectionStatus { get; internal set; } = DeviceConnectionStatus.Unknown;

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
            ErrorOrTimeout
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
            public void Beat(object connectionObj)
            {
                Connection connection = (Connection)connectionObj;

                if (connection.State == Connection.EState.Connected)
                {
                    AddressType? source = connection.Local?.GetHeartbeatAddress(true);
                    AddressType? destination = connection.Remote?.GetHeartbeatAddress(false);

                    if (null != source && null != destination)
                    {
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
                        reply.datagram.header.addressSource = source;
                        reply.datagram.header.addressDestination = destination;
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
        }

        protected class ElectricalConnectionCharacteristicTask
        {
            // This method is called by the timer delegate.
            public void SendData(object connectionObj)
            {
                Connection connection = (Connection)connectionObj;

                if (connection.State == Connection.EState.Connected)
                {
                    AddressType? source = connection.Local?.GetElectricalConnectionAddress(true);
                    AddressType? destination = connection.Remote?.GetElectricalConnectionAddress(false);

                    if (null != source && null != destination)
                    {
                        if (connection is Server)
                            Debug.WriteLine("--- Send electrical connection characteristics via server ---");
                        else
                            Debug.WriteLine("--- Send electrical connection characteristics via client ---");

                        SpineDatagramPayload reply = new SpineDatagramPayload();
                        reply.datagram.header.addressSource = source;
                        reply.datagram.header.addressDestination = destination;
                        reply.datagram.header.msgCounter = DataMessage.NextCount;
                        reply.datagram.header.cmdClassifier = "notify";

                        var eccPayload = new ElectricalConnectionCharacteristicListData.Class().CreateNotify(connection);
                        reply.datagram.payload = eccPayload?.ToJsonNode();// JsonSerializer.SerializeToNode(eccPayload);

                        DataMessage eccMessage = new DataMessage();
                        //eccMessage.SetPayload(JsonSerializer.SerializeToNode(reply));
                        eccMessage.SetPayload(JsonSerializer.SerializeToNode(reply) ?? throw new Exception("Failed to serialize electrical connection characteristics message"));

                        connection.PushDataMessage(eccMessage);
                    }
                }
            }
        }

        protected class MeasurementDataTask
        {
            // Dummy data for test purpose
            MGCPOperationalData dummyData = new MGCPOperationalData();

            // This method is called by the timer delegate.
            public void SendData(object connectionObj)
            {
                Connection connection = (Connection)connectionObj;

                if (connection.State == Connection.EState.Connected)
                {
                    AddressType? source = connection.Local.GetMeasurementDataAddress(true);
                    AddressType? destination = connection.Remote?.GetMeasurementDataAddress(false);

                    if (null != source && null != destination)
                    {
                        // Fill dummy data with random values						
                        this.dummyData.FillRandom();
                        List<MGCPOperationalData> dummyList = new();
                        dummyList.Add(this.dummyData);
                        connection.Local.FillData<MGCPOperationalData>(dummyList, connection);

                        if (connection is Server)
                            Debug.WriteLine("--- Send measurement data via server ---");
                        else
                            Debug.WriteLine("--- Send measurement data via client ---");

                        SpineDatagramPayload reply = new SpineDatagramPayload();
                        reply.datagram.header.addressSource = source;
                        reply.datagram.header.addressDestination = destination;
                        reply.datagram.header.msgCounter = DataMessage.NextCount;
                        reply.datagram.header.cmdClassifier = "notify";

                        var measurementPayload = new MeasurementListData.Class().CreateNotify(connection);
                        reply.datagram.payload = measurementPayload?.ToJsonNode();//JsonSerializer.SerializeToNode(measurementPayload);

                        DataMessage dataMessage = new DataMessage();
                        dataMessage.SetPayload(JsonSerializer.SerializeToNode(reply) ?? throw new Exception("Failed to serialize measurement data message"));

                        connection.PushDataMessage(dataMessage);
                    }
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


        private byte[] _receiveBuffer = new byte[10240];
        protected async Task<ShipMessageBase> ReceiveAsync(CancellationToken cancellationToken)
        {
            int totalCount = 0;
            WebSocketReceiveResult result;

            //using CancellationTokenSource timeoutCts = new CancellationTokenSource(SHIPMessageTimeout.CMI_TIMEOUT);
            //using CancellationTokenSource linkedTokenSource =
            //    CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

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




    }
}
