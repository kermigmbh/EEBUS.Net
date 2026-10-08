using EEBUS.Enums;
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
            private DeviceDiagnosisHeartbeatData.Class heartbeatClass = new DeviceDiagnosisHeartbeatData.Class();
            // This method is called by the timer delegate.
            public void Beat(object? connectionObj)
            {
                Connection? connection = connectionObj as Connection;
                if (connection == null) return;

                if (connection.State == Connection.EState.Connected)
                {
                    if (!connection.BindingAndSubscriptionManager.GetSubscriptions(BindingSubscriptionDirection.Outgoing).Any(info => info.serverFeatureType == "DeviceDiagnosis"))
                    {
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
                    if (heartbeatSource == null) return;

                    SendHeartbeatNotification(heartbeatSource, heartbeatClass.CreateNotify(connection), connection);
                }
            }

            private void SendHeartbeatNotification(AddressType serverAddress, SpineCmdPayloadBase payload, Connection connection)
            {
                IEnumerable<AddressType> clientAddresses = connection.BindingAndSubscriptionManager.GetSubscriptionsByServerAddress(serverAddress, BindingSubscriptionDirection.Incoming);

                foreach (var clientAddress in clientAddresses)
                {
                    SpineDatagramPayload reply = new SpineDatagramPayload();
                    reply.datagram.header.addressSource = serverAddress;
                    reply.datagram.header.addressDestination = clientAddress;
                    reply.datagram.header.msgCounter = DataMessage.NextCount;
                    reply.datagram.header.cmdClassifier = "notify";

                    reply.datagram.payload = payload.ToJsonNode();
                    DataMessage dataMessage = new DataMessage();
                    dataMessage.SetPayload(JsonHelper.ToJsonNode(reply) ?? throw new Exception("Failed to serialize data message"));
                    connection.PushDataMessage(dataMessage);
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

        public RemoteDevice? Remote { get; internal set; }


        public DataMessageQueue WaitingMessages { get; protected set; }


        public abstract Task CloseAsync();

        internal RemoteDevice? GetRemote(string id)
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

        // Absolute deadline of the hello phase (T_hello_init, extended by T_hello_inc on
        // each granted prolongation). Null when not in the hello phase.
        private DateTime? _helloDeadlineUtc;

        /// <summary>
        /// Maximum time (ms) we wait for the next message in the given state, or null if
        /// SHIP defines no receive timeout for that state (e.g. Connected).
        /// </summary>
        protected virtual int? GetReceiveTimeout(EState state)
        {
            switch (state)
            {
                case EState.WaitingForConnectionHello:
                    if (_helloDeadlineUtc is null)
                        return SHIPMessageTimeout.T_HELLO_INIT;
                    double remaining = (_helloDeadlineUtc.Value - DateTime.UtcNow).TotalMilliseconds;
                    return (int)Math.Max(SHIPMessageTimeout.T_HELLO_PROLONG_MIN, Math.Min(remaining, int.MaxValue));
                case EState.WaitingForProtocolHandshake:
                case EState.WaitingForProtocolHandshakeConfirm:
                case EState.SendProtocolHandshakeConfirm:
                    return 10_000;  //according to spec
                case EState.Disconnected:
                case EState.WaitingForPinCheck:
                case EState.WaitingForAccessMethodsRequest:
                case EState.WaitingForAccessMethods:
                case EState.WaitingForCloseConfirm:
                    return SHIPMessageTimeout.CMI_TIMEOUT;

                default:
                    return null;
            }
        }

        /// <summary>
        /// Starts the Wait-For-Ready-Timer if it is not already running.
        /// Call this before sending the first hello message so that <see cref="WaitForReadyTimerValue"/> is valid.
        /// </summary>
        /// <param name="durationMs">Timer duration in ms, defaults to T_hello_init.</param>
        internal void StartWaitForReadyTimer(int durationMs = SHIPMessageTimeout.T_HELLO_INIT)
        {
            if (_helloDeadlineUtc is null)
            {
                _helloDeadlineUtc = DateTime.UtcNow.AddMilliseconds(durationMs);
                Logger?.LogDebug("Wait-For-Ready-Timer started, deadline {deadline:HH:mm:ss.fff} UTC", _helloDeadlineUtc);
            }
        }

        /// <summary>
        /// Remaining time (ms) of the Wait-For-Ready-Timer, to be sent in the <c>waiting</c> field of hello messages.
        /// Returns null if the timer is not running; in that case the <c>waiting</c> element must be omitted.
        /// </summary>
        public uint? WaitForReadyTimerValue
        {
            get
            {
                if (_helloDeadlineUtc is null)
                    return null;
                double remaining = (_helloDeadlineUtc.Value - DateTime.UtcNow).TotalMilliseconds;
                return (uint)Math.Clamp(remaining, 0, uint.MaxValue);
            }
        }

        /// <summary>
        /// Stops the Wait-For-Ready-Timer. Must be called when the hello phase is left.
        /// </summary>
        internal void StopWaitForReadyTimer()
        {
            _helloDeadlineUtc = null;
        }

        /// <summary>
        /// Increases the Wait-For-Ready-Timer by the given amount (remaining + increment).
        /// Called when a prolongation request of the peer is granted.
        /// </summary>
        /// <param name="incrementMs">Increment in ms, defaults to T_hello_inc.</param>
        internal void ProlongHelloDeadline(int incrementMs = SHIPMessageTimeout.T_HELLO_INC)
        {
            if (_helloDeadlineUtc != null)
            {
                _helloDeadlineUtc = _helloDeadlineUtc.Value.AddMilliseconds(incrementMs);
            }

            Logger?.LogDebug("Hello deadline prolonged to {deadline:HH:mm:ss.fff} UTC", _helloDeadlineUtc);
        }

        /// <summary>
        /// Receives the next message, applying the SHIP timeout for the current state.
        /// </summary>
        /// <returns>The message, or null if the SHIP timeout for the current state elapsed.</returns>
        protected async Task<ShipMessageBase?> ReceiveWithTimeoutAsync(CancellationToken cancellationToken)
        {
            int? timeout = GetReceiveTimeout(this.state);
            if (timeout is null)
                return await ReceiveAsync(cancellationToken).ConfigureAwait(false);

            using CancellationTokenSource timeoutCts = new CancellationTokenSource(timeout.Value);
            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

            try
            {
                return await ReceiveAsync(linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested && this.state != EState.Stopped)
            {
                Logger?.LogWarning("SHIP timeout ({timeout} ms) elapsed in state {state}/{subState}", timeout, this.state, this.subState);
                return null;
            }
        }

        /// <summary>
        /// Reacts to an elapsed SHIP timeout according to the current state and marks the connection as timed out.
        /// </summary>
        protected virtual async Task OnReceiveTimeoutAsync()
        {
            try
            {
                switch (this.state)
                {
                    case EState.WaitingForConnectionHello:
                        // The Wait-For-Ready-Timer has timed out, which means we did not receive a ConnectionHello message from the peer in time. We need to abort the connection.
                        await new ConnectionHelloMessage(ConnectionHelloPhaseType.aborted).Send(this.ws, Logger).ConfigureAwait(false);
                        break;

                    case EState.WaitingForProtocolHandshake:
                    case EState.WaitingForProtocolHandshakeConfirm:
                    case EState.SendProtocolHandshakeConfirm:
                        await new ProtocolHandshakeErrorMessage(SHIPHandshakeError.TIMEOUT).Send(this.ws, Logger).ConfigureAwait(false);
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "OnReceiveTimeoutAsync: failed to send timeout notification.");
            }

            this.state = EState.ErrorOrTimeout;
            this.subState = ESubState.None;
            StopWaitForReadyTimer();
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

            Logger?.LogDebug(DateTime.Now.ToString("HH:mm:ss.fff") + " <--- " + Encoding.UTF8.GetString(messageSpan) + "\n");

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
            var clientAddress = GetLocalHeartbeatAddress(false);
            var serverAddress = GetRemoteHeartbeatAddress(true);

            if (clientAddress == null || serverAddress == null)
            {
                Logger?.LogInformation("HeartbeatSubscription: Could not find valid client or server address for heartbeat subscription. ClientAddress: {clientAddress}, ServerAddress: {serverAddress}", clientAddress, serverAddress);
                return;
            }

            if (Remote == null)
            {
                Logger?.LogInformation("HeartbeatSubscription: Remote device is not available.");
                return;
            }

            DataMessage message = DataMessage.CreateSubscriptionRequest(this, clientAddress, serverAddress, "DeviceDiagnosis", Local.DeviceId, Remote.DeviceId);
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

        public AddressType? GetLocalHeartbeatAddress(bool server)
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

        public AddressType? GetRemoteHeartbeatAddress(bool server)
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
                BindingSubscriptionInfo? loadControlBinding = BindingAndSubscriptionManager.GetBindings(BindingSubscriptionDirection.Incoming, "LoadControl").FirstOrDefault();
                if (loadControlBinding == null) return null;

                Entity? entity = Remote.Entities.FirstOrDefault(e => e.Index.SequenceEqual(loadControlBinding.clientAddress.entity));  //get remote entity which is bound to our LoadControl feature
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
                            if (!BindingAndSubscriptionManager.HasBinding(featureSourceAddress, featureDestinationAddress, Net.BindingSubscriptionDirection.Outgoing))
                            {
                                DataMessage callMessage = DataMessage.CreateBindingRequest(this, featureSourceAddress, featureDestinationAddress, feature.Type, Local.DeviceId, Remote.DeviceId);
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
                            if (!BindingAndSubscriptionManager.HasSubscription(featureSourceAddress, featureDestinationAddress, Net.BindingSubscriptionDirection.Outgoing))
                            {
                                DataMessage callMessage = DataMessage.CreateSubscriptionRequest(this, featureSourceAddress, featureDestinationAddress, feature.Type, Local.DeviceId, Remote.DeviceId);
                                PushDataMessage(callMessage);
                            }
                        }
                    }
                }
            }
        }
    }
}
