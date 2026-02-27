using EEBUS.Models;
using EEBUS.SPINE.Commands;
using System;
using System.Collections.Generic;
using System.Text;

namespace EEBUS.Net.EEBUS.Data.DataStructures
{
    public class ElectricalConnectionCharacteristicDataStructure : DataStructure
    {
        public uint ElectricalConnectionId { get; set; }
        public uint ParameterId { get; set; }
        public string CharacteristicType { get; set; }
        public long Number {  get; set; }
        public short Scale { get; set; }

        private uint _characteristicId;
        private string _characteristicContext;
        private string _unit;

        public ElectricalConnectionCharacteristicDataStructure(string characteristicType, long value, short scale) : base("ElectricalConnection")
        {
            /*
            LPP
                ecc.electricalConnectionId = 0;
                ecc.parameterId = 0;
                ecc.characteristicId = id;
                ecc.characteristicContext = "entity";
                ecc.characteristicType = "contractualProductionNominalMax";
                ecc.value.number = connection.Local.GetSettings().GetProductionNominalMax();
                ecc.value.scale = 0;
                ecc.unit = "W";

            LPC
                ecc.electricalConnectionId = 0;
                ecc.parameterId = 0;
                ecc.characteristicId = id;
                ecc.characteristicContext = "entity";
                ecc.characteristicType = "contractualConsumptionNominalMax";
                ecc.value.number = connection.Local.GetSettings().GetConsumptionNominalMax();
                ecc.value.scale = 0;
                ecc.unit = "W";

             */

            ElectricalConnectionId = 0;
            ParameterId = 0;
            _characteristicId = 0;
            _characteristicContext = "entity";
            CharacteristicType = characteristicType;
            Number = value;
            Scale = scale;
            _unit = "W";
        }

        public override uint Id { get => _characteristicId; set => _characteristicId = value; }

        public override Task SendEventAsync(Connection connection)
        {
            throw new NotImplementedException();
        }

        public ElectricalConnectionCharacteristicDataType Data
        {
            get
            {
                ElectricalConnectionCharacteristicDataType data = new();
                data.electricalConnectionId = ElectricalConnectionId;
                data.parameterId = ParameterId;
                data.characteristicType = CharacteristicType;
                data.characteristicContext = _characteristicContext;
                data.characteristicId = _characteristicId;
                data.unit = _unit;
                data.value = new()
                {
                    number = Number,
                    scale = Scale,
                };

                return data;
            }
        }
    }
}
