<template>
	<h1>EEBUS EMS Adapter</h1>
	<div>
		<div class="header-frame">
			<div class="header" v-if="local">
				<div class="table2c">
					<p>Local Device</p>
					<p>{{ local.name }}</p>
					<p>Local SKI</p>
					<p>{{ local.ski }}</p>
					<p>Ship ID</p>
					<p class="qrcode-text">{{ local.shipId }}</p>
				</div>
				<div v-if="'' < local.shipId">
					<qrcode-vue  class="qrcode" :value="local.shipId" :size="130" level="H" render-as="svg" />
				</div>
			</div>
			<div v-else>
				<p>No local device found</p>
			</div>
		</div>

		<hr />

		<div class="limit-data">
			<p></p><p>LPC</p><p>LPP</p>
			<p>Active</p><p>{{local?.lpcActive}}</p><p>{{local?.lppActive}}</p>
			<p>Limit</p><p>{{local?.lpcLimit}} W</p><p>{{local?.lppLimit}} W</p>
			<p>Duration</p><p>{{local?.lpcDuration}} s</p><p>{{local?.lppDuration}} s</p>
			<p>Failsafe Limit</p><p>{{local?.lpcFailsafeLimit}} W</p><p>{{local?.lppFailsafeLimit}} W</p>
		</div>
		<hr />
		<div class="limit-data">
			<p></p><p>LPC and LPP</p><p></p>
			<p>Failsafe Duration</p><p>{{local?.failsafeDuration}} s</p><p></p>
			<p>Received Heartbeat</p>
			<p class="heartbeat-container">
				<span v-bind:class = "(local?.heartbeat)?'pulse heartbeat':'pulse'">&#9673;</span>
				<span>{{ local?.heartbeatTimeout ? ('(' + local.heartbeatTimeout + ' s)'): ''}}</span>
			</p><p></p>
		</div>

		<hr />

		<div v-for="remote in remotes">
			<br />
			<div class="table2c">
				<p>ID</p>
				<p>{{ remote.id }}</p>
				<p>Name</p>
				<p>{{ remote.name }}</p>
				<p>SKI</p>
				<p>{{ readableSKI( remote.ski ) }}</p>
				<p>Server state</p>
				<p>{{ remote.serverState ?? 'unknown' }}</p>
				<p>Client state</p>
				<p>{{ remote.clientState ?? 'unknown' }}</p>
			</div>
			<button @click="connect( remote )">Connect</button>
		</div>
	</div>
</template>

<script lang="ts">
	import { Component, Vue, Watch, toNative } from 'vue-facing-decorator'
	import QrcodeVue from 'qrcode.vue'

	interface Device {
		id:			 string;
		name:		 string;
		ski:		 string;
		serverState: string;
		clientState: string;
	}

	interface LocalDevice extends Device {
		url:			  string;
		shipId:			  string;

		lpcActive:		  boolean;
		lpcLimit:		  number;
		lpcDuration:	  number;
		lpcFailsafeLimit: number;

		lppActive:		  boolean;
		lppLimit:		  number;
		lppDuration:	  number;
		lppFailsafeLimit: number;

		failsafeDuration: number;
		heartbeat:		  boolean;
		heartbeatTimeout: number;
	}

	interface PushData {
		cmd:  string;
		data: any;
	}

	type pushFunction = (data: any) => void;

	@Component( {
		components: {
			QrcodeVue
		}
	} )
	export class EEBUS extends Vue {
		private connection: WebSocket | null = null;
		private timerId: any;

		public local:	LocalDevice | undefined	= undefined;
		public remotes: Device[] = [];

		@Watch('$route')
		routeWatcher( newVal: any, oldVal: any ) {
			this.fetchData();
		}

		async created() {
			// fetch the data when the view is created and the data is
			// already being observed
			await this.fetchData();
		}

		mounted() {
			this.connectPushService();

			document.addEventListener( "visibilitychange", () => {
				if ( document.hidden ) {
					this.disappear();
				} else {
					this.connectPushService();
				}
			} );
		}

		protected disappear() {
			clearInterval( this.timerId );
			if ( null != this.connection ) {
				this.connection.close();

				setTimeout( () => {
					this.connection = null;
				}, 1000 );
			}
		}

		private async fetchData() {
			try {
				var response = await fetch( '/devices/getlocal' );
				if ( response.ok ) {
					this.local = await response.json();
				}

				response = await fetch( '/devices/getremotes' );
				if ( response.ok ) {
					this.remotes = await response.json();
				}
			}
			catch ( ex )
			{
				console.error( ex );
			}
		}

		private connectPushService() {
			if ( null != this.connection )
				return;

			this.connection = new WebSocket( "/ws/register" );

			let _this: EEBUS = this;

			this.timerId = setInterval( function() {
				if ( null == _this.connection ) {
					_this.connectPushService();
				}
			}, 1000 );

			if ( null == this.connection )
				return;

			this.connection.onmessage = function( event ) {

				let data = JSON.parse( event.data ) as PushData;

				var fct: pushFunction = (_this as any)[data.cmd] as pushFunction;
				if ( fct ) {
					fct( data.data );
				}
			}

			this.connection.onopen = function( event ) {
				console.log( "Successfully connected to the websocket server..." )
				console.log( event )
			}

			this.connection.onclose = function( event ) {
				console.log( "Connection to the websocket server closed..." )
				console.log( event );

				_this.connection = null;
			}

			this.connection.onerror = function( event ) {
				console.log( "Error with websocket server..." )
				console.log( event );

				_this.connection = null;
			}
		}

		public async connect( remote: Device ) {
			await fetch( '/devices/connect?ski=' + remote.ski );
		}

		public remoteDeviceFound( data: any ) {
			this.remotes.push( {
				id:			 data.id,
				name:		 data.name,
				ski:		 data.ski,
				serverState: data.serverState,
				clientState: data.clientState,
			} as Device );
		}

		public serverStateChanged( data: any ) {
			var device = this.remotes.find( rd => rd.id == data.id || rd.ski == data.ski );
			if ( device )
				device.serverState = data.state;
		}

		public clientStateChanged( data: any ) {
			var device = this.remotes.find( rd => rd.id == data.id || rd.ski == data.ski );
			if ( device )
				device.clientState = data.state;
		}

		public limitDataChanged( data: any ) {
			if ( data.direction == "consume" && this.local ) {
				this.local.lpcActive	= data.active;
				this.local.lpcLimit	= data.limit;
				this.local.lpcDuration	= data.duration;
			}
			else if ( data.direction == "produce" && this.local ) {
				this.local.lppActive	= data.active;
				this.local.lppLimit	= data.limit;
				this.local.lppDuration	= data.duration;
			}
		}

		public failsafeLimitDataChanged( data: any ) {
			if ( data.direction == "consume" && this.local ) {
				this.local.lpcFailsafeLimit = data.limit;
			}
			else if ( data.direction == "produce" && this.local ) {
				this.local.lppFailsafeLimit = data.limit;
			}
		}

		public failsafeLimitDurationChanged(data: any) {
			if ( this.local )
				this.local.failsafeDuration = data.duration;
		}

		public heartbeatReceived( data: any ) {
			if ( this.local ) {
				this.local.heartbeat		= true;
				this.local.heartbeatTimeout	= data.timeout;

				setTimeout( () => this.local!.heartbeat = false, 1000 );
			}
		}

		public readableSKI( ski: string ): string {
			if ( 40 < ski.length )
				return ski;

			var parts: string[] = [];
			for ( var i = 0; i < 10; i++ )
				parts[i] = ski.substring( 4*i, 4*i +4 );

			return parts.join( ' ' );
		}

		public formatted( val: number ): string {
			return new Intl.NumberFormat( 'en-US' ).format( val );
		}
	}

	export default toNative( EEBUS );
</script>

<style scoped>
	.header-frame {
		display: inline-table;
		margin-bottom: 10px;
	}

	.header {
		display: grid;
		grid-template-columns: 200fr 25fr;
		column-gap: 25px;
	}

	.qrcode-text {
		width: 370px;
		text-align: left;
		word-break: break-all;
	}

	.qrcode {
		width: 130px;
		height: 100%;
	}

	.table2c {
		display: grid;
		grid-template-columns: 100px 1fr;
		column-gap: 5px;
	}

	.limit-data {
		display: grid;
		grid-template-columns: 50fr 25fr 25fr;
		column-gap: 10px;
	}

	.pulse {
		font-size: 25px;
		line-height: 1;
		position: relative;
		top: -2px;
	}

	.heartbeat-container {
		display: grid;
		grid-template-columns: 30px 50px;
	}

	.heartbeat {
		animation-name: heartbeat;
		animation-duration: 1s;
		/* animation-iteration-count: infinite; */
	}

	@keyframes heartbeat {
		from {
			color: rgb(0,255,0);
		}

		25% {
			color: rgb(0,255,0);
		}

		50% {
			color: rgb(0,127,0);
		}

		75% {
			color: rgb(0,63,0);
		}

		to {
			color: rgb(0,0,0);
		}
	}

</style>
