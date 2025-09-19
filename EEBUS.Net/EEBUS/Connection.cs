using System.Diagnostics;
using System.Net.WebSockets;

using Microsoft.AspNetCore.Http;

using Newtonsoft.Json.Linq;

using EEBUS.Messages;
using EEBUS.Models;
using EEBUS.SHIP.Messages;
using EEBUS.SPINE.Commands;
using EEBUS.Net.EEBUS.UseCases.GridConnectionPoint;


namespace EEBUS
{
	public class Connection
	{
		protected HostString host;
		protected WebSocket	 ws;
		protected EState	 state;
		protected ESubState	 subState;

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

			// This method is called by the timer delegate.
			public void Beat( object connectionObj )
			{
				Connection connection = (Connection) connectionObj;
				
				if ( connection.State == Connection.EState.Connected )
				{
					AddressType source		= connection.Local.GetHeartbeatAddress( true );
					AddressType destination	= connection.Remote.GetHeartbeatAddress( false );

					if ( null != source && null != destination )
					{
						if ( ! this.heartbeatSubscribed )
						{
							this.heartbeatSubscribed = true;

							if ( connection is Server )
								Debug.WriteLine( "--- Request heartbeat via server ---" );
							else
								Debug.WriteLine( "--- Request heartbeat via client ---" );

							connection.HeartbeatSubscription();
							connection.HeartbeatRead();
						}

						if ( connection is Server )
							Debug.WriteLine( "--- Send heartbeat via server ---" );
						else
							Debug.WriteLine( "--- Send heartbeat via client ---" );

						SpineDatagramPayload reply = new SpineDatagramPayload();
						reply.datagram.header.addressSource		 = source;
						reply.datagram.header.addressDestination = destination;
						reply.datagram.header.msgCounter		 = DataMessage.NextCount;
						reply.datagram.header.cmdClassifier		 = "notify";

						SpineCmdPayloadBase heartbeat = new DeviceDiagnosisHeartbeatData.Class().CreateNotify( connection );
						reply.datagram.payload = JObject.FromObject( heartbeat );
					
						DataMessage heartbeatMessage = new DataMessage();
						heartbeatMessage.SetPayload( JObject.FromObject( reply ) );

						connection.PushDataMessage( heartbeatMessage );
					}
				}
			}
		}

		protected class ElectricalConnectionCharacteristicTask
		{
			// This method is called by the timer delegate.
			public void SendData( object connectionObj )
			{
				Connection connection = (Connection) connectionObj;
				
				if ( connection.State == Connection.EState.Connected )
				{
					AddressType source		= connection.Local.GetElectricalConnectionAddress( true );
					AddressType destination	= connection.Remote.GetElectricalConnectionAddress( false );

					if ( null != source && null != destination )
					{
						if ( connection is Server )
							Debug.WriteLine( "--- Send electrical connection characteristics via server ---" );
						else
							Debug.WriteLine( "--- Send electrical connection characteristics via client ---" );

						SpineDatagramPayload reply = new SpineDatagramPayload();
						reply.datagram.header.addressSource		 = source;
						reply.datagram.header.addressDestination = destination;
						reply.datagram.header.msgCounter		 = DataMessage.NextCount;
						reply.datagram.header.cmdClassifier		 = "notify";

						reply.datagram.payload = JObject.FromObject( new ElectricalConnectionCharacteristicListData.Class().CreateNotify( connection ) );

						DataMessage eccMessage = new DataMessage();
						eccMessage.SetPayload( JObject.FromObject( reply ) );

						connection.PushDataMessage( eccMessage );
					}
				}
			}
		}

		protected class MeasurementDataTask
		{
			// Dummy data for test purpose
			MGCPOperationalData dummyData = new MGCPOperationalData();

			// This method is called by the timer delegate.
			public void SendData( object connectionObj )
			{
				Connection connection = (Connection) connectionObj;
				
				if ( connection.State == Connection.EState.Connected )
				{
					AddressType source		= connection.Local.GetMeasurementDataAddress( true );
					AddressType destination	= connection.Remote.GetMeasurementDataAddress( false );

					if ( null != source && null != destination )
					{
						// Fill dummy data with random values						
						this.dummyData.FillRandom();
						List<MGCPOperationalData> dummyList = new();
						dummyList.Add( this.dummyData );
						connection.Local.FillData<MGCPOperationalData>( dummyList, connection );

						if ( connection is Server )
							Debug.WriteLine( "--- Send measurement data via server ---" );
						else
							Debug.WriteLine( "--- Send measurement data via client ---" );

						SpineDatagramPayload reply = new SpineDatagramPayload();
						reply.datagram.header.addressSource		 = source;
						reply.datagram.header.addressDestination = destination;
						reply.datagram.header.msgCounter		 = DataMessage.NextCount;
						reply.datagram.header.cmdClassifier		 = "notify";

						reply.datagram.payload = JObject.FromObject( new MeasurementListData.Class().CreateNotify( connection ) );

						DataMessage dataMessage = new DataMessage();
						dataMessage.SetPayload( JObject.FromObject( reply ) );

						connection.PushDataMessage( dataMessage );
					}
				}
			}
		}

		public Connection( HostString host, WebSocket ws, Devices devices )
		{
			this.host			 = host;
			this.ws				 = ws;
			this.devices		 = devices;

			this.WaitingMessages = new( this );
		}

		public WebSocket    WebSocket { get { return this.ws; } }

		public EState	    State	  { get { return this.state; } }

		public ESubState    SubState  { get { return this.subState; } }


		private Devices	    devices;

		public LocalDevice  Local	  { get { return this.devices.Local; } }

		public RemoteDevice Remote	  { get; protected set; }


		public DataMessageQueue WaitingMessages { get; protected set; }


		protected RemoteDevice GetRemote( string id )
		{
			if ( null == id )
				return null;

			return this.devices.GetRemote( id );
		}

		public void PushDataMessage( DataMessage message )
		{
			this.WaitingMessages.Push( message );
		}

		public void RequestRemoteDeviceConfiguration()
		{
			SpineDatagramPayload read = new SpineDatagramPayload();
			read.datagram.header.addressSource				= new();
			read.datagram.header.addressSource.device		= this.Local.DeviceId;
			read.datagram.header.addressSource.entity		= [0];
			read.datagram.header.addressSource.feature		= 0;
			read.datagram.header.addressDestination			= new();
			read.datagram.header.addressDestination.entity	= [0];
			read.datagram.header.addressDestination.feature	= 0;
			read.datagram.header.msgCounter					= DataMessage.NextCount;
			read.datagram.header.cmdClassifier				= "read";

			read.datagram.payload = JObject.FromObject( new NodeManagementDetailedDiscoveryData.Class().CreateRead( this ) );

			DataMessage message = new DataMessage();
			message.SetPayload( JObject.FromObject( read ) );

			PushDataMessage( message );
		}

		public void HeartbeatSubscription()
		{
			SpineDatagramPayload call = new SpineDatagramPayload();
			call.datagram.header.addressSource				= new();
			call.datagram.header.addressSource.device		= this.Local.DeviceId;
			call.datagram.header.addressSource.entity		= [0];
			call.datagram.header.addressSource.feature		= 0;
			call.datagram.header.addressDestination			= new();
			call.datagram.header.addressDestination.device	= this.Remote.DeviceId;
			call.datagram.header.addressDestination.entity	= [0];
			call.datagram.header.addressDestination.feature	= 0;
			call.datagram.header.msgCounter					= DataMessage.NextCount;
			call.datagram.header.cmdClassifier				= "call";

			NodeManagementSubscriptionRequestCall payload = new NodeManagementSubscriptionRequestCall();
			SubscriptionRequestType subscriptionRequest = payload.cmd[0].nodeManagementSubscriptionRequestCall.subscriptionRequest;
			subscriptionRequest.clientAddress	  = this.Local.GetHeartbeatAddress( false );
			subscriptionRequest.serverAddress	  = this.Remote.GetHeartbeatAddress( true );
			subscriptionRequest.serverFeatureType = "DeviceDiagnosis";

			call.datagram.payload = JObject.FromObject( payload );

			DataMessage message = new DataMessage();
			message.SetPayload( JObject.FromObject( call ) );

			PushDataMessage( message );
		}

		public void HeartbeatRead()
		{
			AddressType source		= this.Local.GetHeartbeatAddress( false );
			AddressType destination = this.Remote.GetHeartbeatAddress( true );

			SpineDatagramPayload read = new SpineDatagramPayload();
			read.datagram.header.addressSource		= source;
			read.datagram.header.addressDestination	= destination;
			read.datagram.header.msgCounter			= DataMessage.NextCount;
			read.datagram.header.cmdClassifier		= "read";

			read.datagram.payload = JObject.FromObject( new DeviceDiagnosisHeartbeatData.Class().CreateRead( this ) );

			DataMessage message = new DataMessage();
			message.SetPayload( JObject.FromObject( read ) );

			PushDataMessage( message );
		}
	}
}
