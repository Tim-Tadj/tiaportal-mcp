using Siemens.Engineering;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    public partial class Portal
    {
        public object GetAccessibleDevices(string deviceItemPath, string mode, string pcInterfaceName, int pcInterfaceNumber)
        {
            var online = RequireOnlineProvider(deviceItemPath);
            var configurationMode = online.Configuration.Modes.Find(mode)
                ?? throw new ArgumentException("Connection mode not found.");
            var pc = configurationMode.PcInterfaces.Find(pcInterfaceName, pcInterfaceNumber)
                ?? throw new ArgumentException("PC interface not found.");
            return pc.GetAccessibleDevices().Select(d => new
            {
                d.Name, d.Address, d.MACAddress, d.DeviceSeries,
                Attributes = d.GetAttributeInfos().Select(a => new { a.Name, Value = Convert.ToString(d.GetAttribute(a.Name)) }).ToArray(),
                Services = ((IEngineeringServiceProvider)d).GetServiceInfos().Select(s => s.Type.FullName).ToArray()
            }).ToArray();
        }

        public object GetHardwareNetworkInfo(string deviceItemPath)
        {
            var item = GetOnlineDeviceItem(deviceItemPath);
            var network = item.GetService<NetworkInterface>();
            return new
            {
                item.Name, item.TypeIdentifier,
                Attributes = item.GetAttributeInfos().Select(a => new { a.Name, Value = Convert.ToString(item.GetAttribute(a.Name)) }).ToArray(),
                Addresses = item.Addresses.Select(a => a.GetAttributeInfos().Select(i => new { i.Name, Value = Convert.ToString(a.GetAttribute(i.Name)) }).ToArray()).ToArray(),
                Nodes = network?.Nodes.Select(n => new { Attributes = n.GetAttributeInfos().Select(a => new { a.Name, Value = Convert.ToString(n.GetAttribute(a.Name)) }).ToArray(), Subnet = n.ConnectedSubnet?.Name }).ToArray(),
                IoConnectors = network?.IoConnectors.Select(c => new { Attributes = c.GetAttributeInfos().Select(a => new { a.Name, Value = Convert.ToString(c.GetAttribute(a.Name)) }).ToArray(), IoSystem = c.ConnectedToIoSystem?.Name, Number = c.ConnectedToIoSystem?.Number }).ToArray()
            };
        }

        public object ChangeHardwareType(string deviceItemPath, string expectedTypeIdentifier, string newTypeIdentifier)
        {
            TiaMcpServer.Security.ProfileAccessPolicy.DemandWrite("ChangeHardwareType");
            var item = GetOnlineDeviceItem(deviceItemPath);
            ValidateHardwareTypeChange(item.TypeIdentifier, expectedTypeIdentifier, newTypeIdentifier);
            item.ChangeType(newTypeIdentifier);
            return new { item.Name, item.TypeIdentifier, Saved = false, Downloaded = false };
        }

        internal static void ValidateHardwareTypeChange(string currentType, string expectedType, string newType)
        {
            if (String.IsNullOrWhiteSpace(expectedType) || !String.Equals(currentType, expectedType, StringComparison.Ordinal))
            {
                throw new ArgumentException("Current type differs from the expected type. Read it again before changing hardware.");
            }
            if (String.IsNullOrWhiteSpace(newType) || !newType.StartsWith("OrderNumber:", StringComparison.Ordinal)
                || newType.IndexOf("/V", StringComparison.Ordinal) <= "OrderNumber:".Length
                || newType.EndsWith("/V", StringComparison.Ordinal))
            {
                throw new ArgumentException("An exact order number and firmware type identifier is required.");
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

        public object CompileHardware(string deviceItemPath)
        {
            TiaMcpServer.Security.ProfileAccessPolicy.DemandWrite("CompileHardware");
            var item = GetOnlineDeviceItem(deviceItemPath);
            var compiler = item.GetService<ICompilable>()
                ?? throw new ArgumentException("No hardware compiler at this path.");
            var result = compiler.Compile();
            return new { State = result.State.ToString(), result.ErrorCount, result.WarningCount, Messages = HardwareMessages(result.Messages).ToArray() };
        }

        private static IEnumerable<object> HardwareMessages(CompilerResultMessageComposition messages)
        {
            foreach (var m in messages)
            {
                yield return new { State = m.State.ToString(), m.Path, m.Description };
                foreach (var child in HardwareMessages(m.Messages)) yield return child;
            }
        }

    }
}
