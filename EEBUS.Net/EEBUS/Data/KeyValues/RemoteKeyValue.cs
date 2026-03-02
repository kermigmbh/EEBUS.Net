using EEBUS.Models;
using EEBUS.SPINE.Commands;
using System;
using System.Collections.Generic;
using System.Text;

namespace EEBUS.Net.EEBUS.Data.KeyValues
{
    public class RemoteKeyValue : KeyValue
    {
        private DeviceConfigurationKeyValueDescriptionDataType? _descriptionData;
        private DeviceConfigurationKeyValueDataType? _data;
        public int? KeyId => _descriptionData?.keyId ?? _data?.keyId;
        public RemoteKeyValue(Device device, DeviceConfigurationKeyValueDescriptionDataType? descriptionData, DeviceConfigurationKeyValueDataType? data) : base(device)
        {
            _descriptionData = descriptionData;
            _data = data;
        }

        public void Update(DeviceConfigurationKeyValueDescriptionDataType? descriptionData, DeviceConfigurationKeyValueDataType? data)
        {
            _descriptionData = descriptionData ?? _descriptionData;
            _data = data ?? _data;
        }

        public override string KeyName => _descriptionData?.keyName ?? string.Empty;

        public override string Type => _descriptionData?.valueType ?? string.Empty;

        public override DeviceConfigurationKeyValueDescriptionDataType DescriptionData => _descriptionData ?? new();

        public override DeviceConfigurationKeyValueDataType Data => _data ?? new();

        public override Task SendEventAsync(Connection connection)
        {
            return Task.CompletedTask;
        }

        public override void SetValue(global::EEBUS.SPINE.Commands.ValueType value)
        {
            _data?.value = value;
        }
    }
}
