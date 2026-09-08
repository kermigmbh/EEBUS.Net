using EEBUS;
using EEBUS.Messages;
using EEBUS.SHIP.Messages;
using EEBUS.SPINE.Commands;
using System;
using System.Collections.Generic;
using System.Text;

namespace TestProject1.ConsistencyTests
{
    public class BindingAndScubscriptionTests : EebusTests
    {
        protected override DeviceSettings GetDeviceSettings()
        {
            return new DeviceSettings()
            {
                Name = "ConsoleDemoDevice",
                Id = "Kermi-EEBUS-Demo-Client",
                Model = "KermiDemo",
                Brand = "Kermi",
                Type = "EnergyManagementSystem",
                Serial = "123456",
                Port = 7200,
                Entities = [
                       new EntitySettings { Type = "DeviceInformation" },
                       new EntitySettings { Type  = "CEM", UseCases = [
                           new UseCaseSettings {
                               Type = "limitationOfPowerConsumption",
                               Actor = "ControllableSystem",
                               InitLimits = new LimitSettings {
                                   Active = false,
                                   Limit = 4300,
                                   Duration = TimeSpan.FromSeconds(7200),
                                   FailsafeLimit = 7200,
                                   NominalMax = 40000
                               }
                           }
                       ]}
                ]
            };
        }

        
    }
}
