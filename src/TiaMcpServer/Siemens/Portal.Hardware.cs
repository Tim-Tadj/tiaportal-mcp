using Siemens.Engineering;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TiaMcpServer.Siemens
{
    public partial class Portal
    {
        public object GetAccessibleDevices(string deviceItemPath, string mode, string pcInterfaceName, int pcInterfaceNumber)
        {
#if TIA_MCP_V17 || TIA_MCP_V18
            throw new NotSupportedException("Accessible-device discovery requires TIA Portal V19 or later. This worker cannot call an API absent from its exact-version PublicAPI.");
#else
            var online = RequireOnlineProvider(deviceItemPath);
            var configurationMode = online.Configuration.Modes.Find(mode)
                ?? throw new ArgumentException("Connection mode not found.");
            var pc = configurationMode.PcInterfaces.Find(pcInterfaceName, pcInterfaceNumber)
                ?? throw new ArgumentException("PC interface not found.");
            return pc.GetAccessibleDevices().Select(d => new
            {
                d.Name, d.Address, d.MACAddress, d.DeviceSeries,
                Attributes = TiaMcpServer.ModelContextProtocol.Helper.GetAttributeList(d),
                Services = ((IEngineeringServiceProvider)d).GetServiceInfos().Select(s => s.Type.FullName).ToArray()
            }).ToArray();
#endif
        }

        public object GetHardwareNetworkInfo(string deviceItemPath)
        {
            var item = GetOnlineDeviceItem(deviceItemPath);
            var network = item.GetService<NetworkInterface>();
            return new
            {
                Path = deviceItemPath, item.Name, item.TypeIdentifier,
                Attributes = TiaMcpServer.ModelContextProtocol.Helper.GetAttributeList(item),
                Addresses = item.Addresses.Select((address, index) => new { Index = index, Attributes = TiaMcpServer.ModelContextProtocol.Helper.GetAttributeList(address) }).ToArray(),
                Nodes = network?.Nodes.Select((node, index) => new { Index = index, Attributes = TiaMcpServer.ModelContextProtocol.Helper.GetAttributeList(node), Subnet = node.ConnectedSubnet?.Name }).ToArray(),
                IoControllers = network?.IoControllers.Select((controller, index) => new { Index = index, Attributes = TiaMcpServer.ModelContextProtocol.Helper.GetAttributeList(controller), IoSystem = controller.IoSystem?.Name, Number = controller.IoSystem?.Number }).ToArray(),
                IoConnectors = network?.IoConnectors.Select((connector, index) => new { Index = index, Attributes = TiaMcpServer.ModelContextProtocol.Helper.GetAttributeList(connector), IoSystem = connector.ConnectedToIoSystem?.Name, Number = connector.ConnectedToIoSystem?.Number }).ToArray()
            };
        }

        public object ChangeHardwareType(string deviceItemPath, string expectedTypeIdentifier, string newTypeIdentifier)
        {
            TiaMcpServer.Security.ProfileAccessPolicy.DemandWrite("ChangeHardwareType");
#if TIA_MCP_V17
            throw new NotSupportedException("Offline ChangeType requires TIA Portal V18 or later.");
#else
            var item = GetOnlineDeviceItem(deviceItemPath);
            ValidateHardwareTypeChange(item.TypeIdentifier, expectedTypeIdentifier, newTypeIdentifier);
            item.ChangeType(newTypeIdentifier);
            return new { item.Name, item.TypeIdentifier, Saved = false, Downloaded = false };
#endif
        }

        internal static void ValidateHardwareTypeChange(string currentType, string expectedType, string newType)
        {
            if (String.IsNullOrWhiteSpace(expectedType) || !String.Equals(currentType, expectedType, StringComparison.Ordinal))
            {
                throw new ArgumentException("Current type differs from the expected type. Read it again before changing hardware.");
            }
            var firmwareVersion = newType != null
                && newType.IndexOf("/V", StringComparison.Ordinal) > "OrderNumber:".Length
                && !newType.EndsWith("/V", StringComparison.Ordinal);
            var softwareVersion = newType != null && Regex.IsMatch(newType,
                @"\AOrderNumber:[^/\r\n]+/SW V[0-9]+(?:\.[0-9]+)* \.\.\.\z",
                RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            if (String.IsNullOrWhiteSpace(newType) || !newType!.StartsWith("OrderNumber:", StringComparison.Ordinal)
                || (!firmwareVersion && !softwareVersion))
            {
                throw new ArgumentException("An exact catalogue order number and firmware or software version identifier is required.");
            }
        }

        public object SetBaseUnitPotentialGroup(string deviceItemPath, int expectedValue, bool lightBaseUnit)
        {
            TiaMcpServer.Security.ProfileAccessPolicy.DemandWrite("SetBaseUnitPotentialGroup");
            var item = GetOnlineDeviceItem(deviceItemPath);
            var before = Convert.ToInt32(item.GetAttribute("PotentialGroup"));
            ValidatePotentialGroupChange(before, expectedValue);
            item.SetAttribute("PotentialGroup", lightBaseUnit ? PotentialGroup.LightBaseUnit : PotentialGroup.DarkBaseUnit);
            return new { item.Name, Before = before, After = item.GetAttribute("PotentialGroup"), Saved = false, Downloaded = false };
        }

        internal static void ValidatePotentialGroupChange(int currentValue, int expectedValue)
        {
            if ((expectedValue != 0 && expectedValue != 1) || currentValue != expectedValue)
            {
                throw new ArgumentException("The BaseUnit setting differs from the expected zero (dark) or one (light). Read it again before changing it.");
            }
        }

        public CompileReport CompileHardware(string deviceItemPath)
        {
            TiaMcpServer.Security.ProfileAccessPolicy.DemandWrite("CompileHardware");
            var item = GetOnlineDeviceItem(deviceItemPath);
            var compiler = item.GetService<ICompilable>();
            // Classic SIMATIC PC racks expose the compiler on their parent Device.
            if (compiler == null && item.TypeIdentifier == "System:Rack.PC" && item.Parent is Device pcStation)
            {
                compiler = pcStation.GetService<ICompilable>();
            }
            if (compiler == null) throw new ArgumentException("No hardware compiler at this path.");
            var result = compiler.Compile();
            return RememberCompile(deviceItemPath, result, "Hardware");
        }

    }
}
