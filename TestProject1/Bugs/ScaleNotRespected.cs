using EEBUS.DataStructures;
using EEBUS.Messages;
using EEBUS.Net;
using EEBUS.Net.EEBUS.Models.Data;
using EEBUS.SHIP.Messages;
using EEBUS.SPINE.Commands;
using EEBUS.StateMachines;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using TestProject1.IntegrationTests;
using Xunit.Abstractions;

namespace TestProject1.Bugs
{
    /*
     * We received a failsafe limit with a scale of 2, but the returned a value with scale = 0. (42 * 10^2 = 4200, but we reported 42)
     * Relevant log output:
     * 09:40:53.235 <--- {"data":[{"header":[{"protocolId":"ee1.0"}]},{"payload":{"datagram":[{"header":[{"specificationVersion":"1.3.0"},{"addressSource":[{"device":"i:51593_u:GTHE0300000086-CLS2"},{"entity":[2]},{"feature":1}]},{"addressDestination":[{"device":"EEBUS_162410145_21821"},{"ent
       ity":[1]},{"feature":3}]},{"msgCounter":79},{"cmdClassifier":"write"},{"ackRequest":true}]},{"payload":[{"cmd":[[{"function":"deviceConfigurationKeyValueListData"},{"filter":[[{"cmdControl":[{"partial":[]}]}]]},{"deviceConfigurationKeyValueListData":[{"deviceConfigurationKeyValueData":
       [[{"keyId":0},{"value":[{"scaledNumber":[{"number":42},{"scale":2}]}]}]]}]}]]}]}]}}]}                                                                                                                                                                                                         

       LPC Failsafe Limit Write Request: Value=42            
       09:40:53.246 ---> {"data":[{"header":[{"protocolId":"ee1.0"}]},{"payload":{"datagram":[{"header":[{"specificationVersion":"1.3.0"},{"addressSource":[{"device":"EEBUS_162410145_21821"},{"entity":[1]},{"feature":3}]},{"addressDestination":[{"device":"i:51593_u:GTHE0300000086-CLS2"},{"ent
       ity":[2]},{"feature":1}]},{"msgCounter":237815},{"cmdClassifier":"notify"}]},{"payload":[{"cmd":[[{"deviceConfigurationKeyValueListData":[{"deviceConfigurationKeyValueData":[[{"keyId":0},{"value":[{"scaledNumber":[{"number":42},{"scale":0}]}]},{"isValueChangeable":true}],[{"keyId":1},{
       "value":[{"duration":"PT2H"}]},{"isValueChangeable":true}]]}]}]]}]}]}}]}
     */
    public class ScaleNotRespected : EebusIntegrationTests
    {
        public ScaleNotRespected(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task ScaleNotRespectedAsync()
        {
            ILogger manager1Logger = GetLogger("CEM", minLogLevel: LogLevel.Debug);
            ILogger manager2Logger = GetLogger("Controlbox");
            using EEBUSManager manager1 = new EEBUSManager(Setup.GetCEMSettings(), logger: manager1Logger);
            var controlBoxSettings = Setup.GetControlBoxSettings();
            using EEBUSManager manager2 = new EEBUSManager(controlBoxSettings, logger: manager2Logger);

            await StartAndConnectManagersAsync(manager1, manager2);
            await Task.Delay(5_000);

            var controlBoxConnection = manager2.GetConnection(manager1.GetLocalData().SKI);
            var cemConnection = manager1.GetConnection(manager2.GetLocalData().SKI);

            //We need to send a limit first, because a write to DeviceConfigration is only possible in limited state
            var limit = controlBoxConnection.Remote.GetDataStructures<LoadControlLimitDataStructure>().First(ds => ds.DescriptionData.scopeType == "activePowerLimit" && ds.DescriptionData.limitDirection == "consume");
            var limitMessage = CreateLimitWriteMessage(controlBoxConnection.Local.GetFeatureAddress("LoadControl", false), controlBoxConnection.Remote.GetFeatureAddress("LoadControl", true), limit.Data.limitId ?? 0, 42, 2, true);
            await SendDataMessageAsync(controlBoxConnection, limitMessage);

            using var dataChangedWaiter = new TestWaiter<DeviceData>(
               subscribe: handler => manager1.OnDeviceDataChanged += handler,
               unsubscribe: handler => manager1.OnDeviceDataChanged -= handler);
            await dataChangedWaiter.Match(data => data.Lpc != null && data.Lpc.LimitState != LimitState.Init, 10000);
            
            var localLimit = cemConnection.Local.GetDataStructures<LoadControlLimitDataStructure>().First(ds => ds.DescriptionData.scopeType == "activePowerLimit" && ds.DescriptionData.limitDirection == "consume");
            Assert.Equal(4200, localLimit.Data.value.ToLong());

            var failSafeKeyValue = controlBoxConnection.Remote.KeyValues.First(kv => kv.DescriptionData.keyName == "failsafeConsumptionActivePowerLimit");
            var failSafeMessage = CreateFailSafeLimitWriteMessage(controlBoxConnection.Local.GetFeatureAddress("DeviceConfiguration", false), controlBoxConnection.Remote.GetFeatureAddress("DeviceConfiguration", true), failSafeKeyValue.Data.keyId, 42, 2);

            await SendDataMessageAsync(controlBoxConnection, failSafeMessage);

            await dataChangedWaiter.Match(data => data.Lpc != null && data.Lpc.FailSafeLimit != null, 10000);

            var localFailSafeKeyValue = cemConnection.Local.KeyValues.First(kv => kv.DescriptionData.keyName == "failsafeConsumptionActivePowerLimit");
            Assert.Equal(4200, localFailSafeKeyValue.Data.value.scaledNumber.ToLong());
        }
    }
}
