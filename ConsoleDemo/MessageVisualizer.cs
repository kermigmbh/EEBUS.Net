using EEBUS.Messages;
using EEBUS.SHIP.Messages;
using EEBUS.SPINE.Commands;
using System.Runtime.CompilerServices;
using System.Text;

namespace ConsoleDemo
{
    public static class MessageVisualizer
    {
        public static void Visualize(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                Console.WriteLine("No message given!");
                return;
            }

            ShipMessageBase? shipMessage;
            try
            {
                // make sure message/command types are registered before lookup
                RuntimeHelpers.RunClassConstructor(typeof(DataMessage).TypeHandle);
                RuntimeHelpers.RunClassConstructor(typeof(NodeManagementDetailedDiscoveryData).TypeHandle);
                RuntimeHelpers.RunClassConstructor(typeof(NodeManagementUseCaseData).TypeHandle);

                shipMessage = ShipMessageBase.Create(Encoding.UTF8.GetBytes(message.Trim()));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to parse SHIP message: {ex.Message}");
                return;
            }

            if (shipMessage == null)
            {
                Console.WriteLine("Unknown SHIP message type!");
                return;
            }

            if (shipMessage is not DataMessage dataMessage)
            {
                Console.WriteLine($"Unsupported SHIP message type: {shipMessage.GetType().Name}");
                return;
            }

            Console.WriteLine("SHIP message type: data");

            SpineDatagramPayload datagram;
            try
            {
                datagram = dataMessage.SpineDatagramPayload;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to deserialize SPINE datagram: {ex.Message}");
                return;
            }

            HeaderType header = datagram.datagram.header;
            Console.WriteLine($"cmdClassifier: {header.cmdClassifier}, msgCounter: {header.msgCounter}" +
                (header.msgCounterReference.HasValue ? $", msgCounterReference: {header.msgCounterReference}" : string.Empty));
            Console.WriteLine($"source: {FormatAddress(header.addressSource)}");
            Console.WriteLine($"destination: {FormatAddress(header.addressDestination)}");

            SpineCmdPayloadBase? payload;
            try
            {
                payload = datagram.DeserializePayload();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to deserialize SPINE payload: {ex.Message}");
                return;
            }

            switch (payload)
            {
                case SpineCmdPayload<CmdNodeManagementDetailedDiscoveryDataType> discovery:
                    VisualizeDiscoveryData(discovery, header);
                    break;
                case SpineCmdPayload<CmdNodeManagementUseCaseDataType> useCase:
                    VisualizeUseCaseData(useCase, header);
                    break;
                default:
                    Console.WriteLine("Unsupported SPINE payload: only nodeManagementDetailedDiscoveryData and nodeManagementUseCaseData are supported");
                    break;
            }
        }

        private static void VisualizeDiscoveryData(SpineCmdPayload<CmdNodeManagementDetailedDiscoveryDataType> discovery, HeaderType header)
        {
            NodeManagementDetailedDiscoveryDataType? data = discovery.cmd.FirstOrDefault()?.nodeManagementDetailedDiscoveryData;
            if (data == null)
            {
                Console.WriteLine("Payload contains no nodeManagementDetailedDiscoveryData");
                return;
            }

            Console.WriteLine("payload: nodeManagementDetailedDiscoveryData");

            string? deviceId = data.deviceInformation?.description?.deviceAddress?.device ?? header.addressSource?.device;
            if (!string.IsNullOrEmpty(deviceId))
            {
                DiscoveryDataStore.Set(deviceId, data);
                Console.WriteLine($"stored discovery data for device '{deviceId}'");
            }
            else
            {
                Console.WriteLine("no device id found, discovery data not stored");
            }

            PrintDiscoveryData(data);
        }

        private static void VisualizeUseCaseData(SpineCmdPayload<CmdNodeManagementUseCaseDataType> useCase, HeaderType header)
        {
            NodeManagementUseCaseDataType? data = useCase.cmd.FirstOrDefault()?.nodeManagementUseCaseData;
            if (data == null)
            {
                Console.WriteLine("Payload contains no nodeManagementUseCaseData");
                return;
            }

            Console.WriteLine("payload: nodeManagementUseCaseData");

            UseCaseInformationType[] infos = data.useCaseInformation ?? [];
            string? deviceId = infos.Select(i => i.address?.device).FirstOrDefault(d => !string.IsNullOrEmpty(d)) ?? header.addressSource?.device;
            NodeManagementDetailedDiscoveryDataType? discovery = string.IsNullOrEmpty(deviceId) ? null : DiscoveryDataStore.Get(deviceId);

            if (discovery == null)
            {
                Console.WriteLine($"no discovery data stored for device '{deviceId}'");
            }
            else
            {
                DeviceInformationDescriptionType? device = discovery.deviceInformation?.description;
                if (device != null)
                {
                    Console.WriteLine($"Device: {device.deviceAddress?.device} ({device.deviceType})");
                }
            }

            var entityGroups = infos
                .GroupBy(i => i.address?.entity ?? [], new EntityAddressEqualityComparer())
                .OrderBy(g => g.Key, new EntityAddressComparer());

            foreach (var entityGroup in entityGroups)
            {
                int[] address = entityGroup.Key;
                string indent = new string(' ', Math.Max(address.Length - 1, 0) * 4);
                string? entityType = discovery?.entityInformation?
                    .FirstOrDefault(e => (e.description.entityAddress.entity ?? []).SequenceEqual(address))?
                    .description.entityType;

                Console.WriteLine();
                Console.WriteLine(string.IsNullOrEmpty(entityType)
                    ? $"{indent}Entity [{string.Join(",", address)}]"
                    : $"{indent}Entity [{string.Join(",", address)}] {entityType}");

                var useCases = entityGroup
                    .SelectMany(i => (i.useCaseSupport ?? []).Select(s => (name: s.useCaseName, actor: i.actor)))
                    .GroupBy(x => x.name)
                    .OrderBy(g => g.Key);

                foreach (var useCaseGroup in useCases)
                {
                    string actors = string.Join(", ", useCaseGroup.Select(x => x.actor).Distinct().OrderBy(a => a));
                    Console.WriteLine($"{indent}    {useCaseGroup.Key} - {actors}");
                }
            }
        }

        private sealed class EntityAddressEqualityComparer : IEqualityComparer<int[]>
        {
            public bool Equals(int[]? x, int[]? y) => (x ?? []).SequenceEqual(y ?? []);

            public int GetHashCode(int[] obj)
            {
                HashCode hash = new();
                foreach (int i in obj) hash.Add(i);
                return hash.ToHashCode();
            }
        }

        private static void PrintDiscoveryData(NodeManagementDetailedDiscoveryDataType data)
        {
            DeviceInformationDescriptionType? device = data.deviceInformation?.description;
            if (device != null)
            {
                Console.WriteLine($"Device: {device.deviceAddress?.device} ({device.deviceType})");
            }

            EntityInformationType[] entities = (data.entityInformation ?? [])
                .OrderBy(e => e.description.entityAddress.entity ?? [], new EntityAddressComparer())
                .ToArray();
            FeatureInformationType[] features = data.featureInformation ?? [];

            foreach (EntityInformationType entity in entities)
            {
                int[] address = entity.description.entityAddress.entity ?? [];
                string indent = new string(' ', Math.Max(address.Length - 1, 0) * 4);

                Console.WriteLine();
                Console.WriteLine($"{indent}Entity [{string.Join(",", address)}] {entity.description.entityType}");

                foreach (FeatureInformationType feature in features.Where(f => (f.description.featureAddress.entity ?? []).SequenceEqual(address)))
                {
                    PrintFeature(feature, indent + "    ");
                }
            }

            FeatureInformationType[] orphans = features
                .Where(f => !entities.Any(e => (e.description.entityAddress.entity ?? []).SequenceEqual(f.description.featureAddress.entity ?? [])))
                .ToArray();
            if (orphans.Length > 0)
            {
                Console.WriteLine("Features without matching entity:");
                foreach (FeatureInformationType feature in orphans)
                {
                    PrintFeature(feature, "    ");
                }
            }
        }

        private static void PrintFeature(FeatureInformationType feature, string indent)
        {
            FeatureInformationDescriptionType desc = feature.description;
            string description = string.IsNullOrEmpty(desc.description) ? string.Empty : $" - {desc.description}";
            Console.WriteLine($"{indent}Feature {desc.featureAddress.feature}: {desc.featureType} ({desc.role}){description}");

            foreach (SupportedFunctionType function in desc.supportedFunction ?? [])
            {
                List<string> operations = new();
                if (function.possibleOperations?.read != null) operations.Add("read");
                if (function.possibleOperations?.write != null) operations.Add("write");

                string ops = operations.Count > 0 ? $" ({string.Join("/", operations)})" : string.Empty;
                Console.WriteLine($"{indent}    {function.function}{ops}");
            }
        }

        private static string FormatAddress(AddressType? address)
        {
            if (address == null) return "<none>";
            string entity = $"[{string.Join(",", address.entity ?? [])}]";
            return address.feature.HasValue
                ? $"{address.device} {entity} feature {address.feature}"
                : $"{address.device} {entity}";
        }

        private sealed class EntityAddressComparer : IComparer<int[]>
        {
            public int Compare(int[]? x, int[]? y)
            {
                x ??= [];
                y ??= [];
                int len = Math.Min(x.Length, y.Length);
                for (int i = 0; i < len; i++)
                {
                    int c = x[i].CompareTo(y[i]);
                    if (c != 0) return c;
                }
                return x.Length.CompareTo(y.Length);
            }
        }
    }
}
