using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using TiaMcpServer.Security;

namespace TiaMcpServer.Siemens
{
    public partial class Portal
    {
        public System.Collections.Generic.IReadOnlyList<TiaProcessEntry> GetTiaProcesses()
        {
            var processes = TiaPortal.GetProcesses().Take(201).Select(p => new TiaProcessEntry
            {
                Id = p.Id, Mode = p.Mode.ToString(), Path = p.Path?.FullName, ProjectPath = p.ProjectPath?.FullName
            }).ToList();
            if (processes.Count > 200) throw new ArgumentException("More than 200 TIA Portal processes were found.");
            return processes;
        }

        public object CreateProfinetIoSystem(string controllerPath, int controllerIndex, int controllerNodeIndex, string expectedSubnet, string systemName, int systemNumber, string expectedProjectPath)
        {
            ProfileAccessPolicy.DemandWrite("CreateProfinetIoSystem");
            RequireSameSelectedProject(expectedProjectPath);
            if (_project == null || !PathsEqual(_project.Path?.ToString(), expectedProjectPath)) throw new ArgumentException("The open project differs from the expected project path.");
            RequireHardwareName(systemName, "I/O system name");
            if (systemNumber < 1 || systemNumber > 65535) throw new ArgumentException("I/O system number must be between 1 and 65535.");
            var network = GetOnlineDeviceItem(controllerPath).GetService<NetworkInterface>() ?? throw new ArgumentException("Controller interface not found.");
            if (controllerIndex < 0 || controllerIndex >= network.IoControllers.Count || controllerNodeIndex < 0 || controllerNodeIndex >= network.Nodes.Count) throw new ArgumentException("Controller index is outside the available entries.");
            var controller = network.IoControllers[controllerIndex];
            ValidateExactValue(network.Nodes[controllerNodeIndex].ConnectedSubnet?.Name, expectedSubnet, "controller subnet");
            if (controller.IoSystem != null) throw new ArgumentException("Controller already has an I/O system.");
            try
            {
                var system = controller.CreateIoSystem(systemName);
                if (system.Number != systemNumber) system.SetAttribute("Number", systemNumber);
                return new { ControllerPath = controllerPath, system.Name, system.Number, Saved = false, Downloaded = false };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("I/O system creation may be partially applied. Read the controller back before retrying. " + ex.Message, ex);
            }
        }

        public System.Collections.Generic.IReadOnlyList<HardwareCatalogEntry> FindHardwareCatalog(string search)
        {
#if TIA_MCP_V17 || TIA_MCP_V18
            throw new NotSupportedException("Hardware catalogue search requires TIA Portal V19 or later.");
#else
            RequireHardwareName(search, "catalogue search");
            if (search.Length < 3 || search.Length > 100) throw new ArgumentException("Catalogue search must contain 3 to 100 characters.");
            if (_portal == null) throw new ArgumentException("No TIA Portal instance is attached.");
            var matches = _portal.HardwareCatalog.Find(search).Take(1001).Select(x => new HardwareCatalogEntry
            {
                ArticleNumber = x.ArticleNumber, TypeIdentifier = x.TypeIdentifier, Version = Convert.ToString(x.Version), TypeName = x.TypeName
            }).ToList();
            if (matches.Count > 1000) throw new ArgumentException("More than 1000 catalogue matches; narrow the search.");
            return matches;
#endif
        }

        public object CreateHardwareDevice(string expectedProjectPath, string groupPath, string catalogueSearch, string typeIdentifier, string deviceName, string itemName)
        {
            ProfileAccessPolicy.DemandWrite("CreateHardwareDevice");
            RequireSameSelectedProject(expectedProjectPath);
#if TIA_MCP_V17 || TIA_MCP_V18
            throw new NotSupportedException("Catalogue-verified hardware creation requires TIA Portal V19 or later.");
#else
            if (_project == null || !PathsEqual(_project.Path?.ToString(), expectedProjectPath)) throw new ArgumentException("The open project differs from the expected project path.");
            RequireHardwareName(deviceName, "device name");
            RequireHardwareName(itemName, "item name");
            RequireHardwareName(typeIdentifier, "catalogue type");
            if (!FindHardwareCatalog(catalogueSearch).Any(x => String.Equals(x.TypeIdentifier, typeIdentifier, StringComparison.Ordinal))) throw new ArgumentException("The exact type was not found in the catalogue search results.");
            DeviceComposition devices;
            if (groupPath == "") devices = _project.Devices;
            else if (groupPath == "UngroupedDevices") devices = _project.UngroupedDevicesGroup.Devices;
            else
            {
                var groups = _project.DeviceGroups;
                DeviceUserGroup? selected = null;
                foreach (var part in groupPath.Split('/'))
                {
                    RequireHardwareName(part, "group path segment");
                    selected = groups.SingleOrDefault(g => String.Equals(g.Name, part, StringComparison.Ordinal)) ?? throw new ArgumentException("Device group not found.");
                    groups = selected.Groups;
                }
                devices = selected!.Devices;
            }
            if (devices.Any(d => String.Equals(d.Name, deviceName, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("A device with that name already exists in the target group.");
            var device = devices.CreateWithItem(typeIdentifier, itemName, deviceName);
            var devicePath = GetDevicePath(device);
            return new { device.Name, device.TypeIdentifier, DevicePath = devicePath, DeviceItemPaths = device.DeviceItems.Select(i => devicePath + "/" + i.Name).ToArray(), Saved = false, Downloaded = false };
#endif
        }

        public object PlugHardwareModule(string expectedProjectPath, string parentPath, string expectedParentType, int slot, string typeIdentifier, string name)
        {
            ProfileAccessPolicy.DemandWrite("PlugHardwareModule");
            RequireSameSelectedProject(expectedProjectPath);
            var parent = GetOnlineDeviceItem(parentPath);
            ValidateExactValue(parent.TypeIdentifier, expectedParentType, "parent type");
            RequireHardwareName(typeIdentifier, "catalogue type");
            RequireHardwareName(name, "module name");
            if (slot < 0) throw new ArgumentException("Slot must be non-negative.");
            if (parent.DeviceItems.Any(i => i.PositionNumber == slot)) throw new ArgumentException("The requested slot is occupied.");
            if (!parent.CanPlugNew(typeIdentifier, name, slot)) throw new ArgumentException("Siemens reports that this module cannot be plugged at the requested slot.");
            var item = parent.PlugNew(typeIdentifier, name, slot);
            var canonicalParent = AllHardwareItems().Single(pair => String.Equals(pair.Key, parentPath, StringComparison.OrdinalIgnoreCase)).Key;
            return new { Path = canonicalParent + "/" + item.Name, item.Name, item.TypeIdentifier, item.PositionNumber, Saved = false, Downloaded = false };
        }

        public object ConfigureIoAddress(string expectedProjectPath, string deviceItemPath, int addressIndex, string expectedDirection, int expectedStartAddress, int startAddress)
        {
            ProfileAccessPolicy.DemandWrite("ConfigureIoAddress");
            RequireSameSelectedProject(expectedProjectPath);
            var item = GetOnlineDeviceItem(deviceItemPath);
            if (addressIndex < 0 || addressIndex >= item.Addresses.Count) throw new ArgumentException("Address index is outside the available addresses.");
            if (startAddress < 0 || startAddress > 65535) throw new ArgumentException("Start address must be between 0 and 65535 bytes.");
            var address = item.Addresses[addressIndex];
            ValidateExactValue(Convert.ToString(address.GetAttribute("IoType")), expectedDirection, "address direction");
            if (Convert.ToInt32(address.GetAttribute("StartAddress")) != expectedStartAddress) throw new ArgumentException("Start address differs from the expected value.");
            if (expectedDirection != "Input" && expectedDirection != "Output") throw new ArgumentException("Only Input and Output addresses can be configured.");
            var lengthBits = Convert.ToInt32(address.GetAttribute("Length"));
            if (lengthBits <= 0) throw new ArgumentException("I/O address length must be positive.");
            var end = (long)startAddress + (lengthBits + 7L) / 8;
            if (end > 65536) throw new ArgumentException("I/O address span exceeds byte 65535.");
            var owners = address.AddressControllers.Select(c => c.OwnedBy).ToArray();
            foreach (var pair in AllHardwareItems())
            {
                for (var index = 0; index < pair.Value.Addresses.Count; index++)
                {
                    var other = pair.Value.Addresses[index];
                    if ((String.Equals(pair.Key, deviceItemPath, StringComparison.OrdinalIgnoreCase) && index == addressIndex) || Convert.ToString(other.GetAttribute("IoType")) != expectedDirection) continue;
                    if (!other.AddressControllers.Any(c => owners.Any(owner => Equals(owner, c.OwnedBy)))) continue;
                    var otherStart = Convert.ToInt32(other.GetAttribute("StartAddress"));
                    var otherLength = Convert.ToInt32(other.GetAttribute("Length"));
                    if (otherLength <= 0 || otherStart < 0) continue;
                    var otherEnd = (long)otherStart + (otherLength + 7L) / 8;
                    if (startAddress < otherEnd && otherStart < end) throw new ArgumentException("I/O span overlaps an existing " + expectedDirection + " address at " + pair.Key + ". The addresses share an address controller.");
                }
            }
            address.SetAttribute("StartAddress", startAddress);
            return new { Path = deviceItemPath, Index = addressIndex, Direction = expectedDirection, Before = expectedStartAddress, After = address.GetAttribute("StartAddress"), Validation = "Checked overlaps only where shared address controllers were identifiable; compile hardware to validate all ranges and process images.", Saved = false, Downloaded = false };
        }

        public object ConfigureHardwareNode(string expectedProjectPath, string deviceItemPath, int nodeIndex, string expectedAddress, string expectedSubnetMask, string expectedPnDeviceName, string address, string subnetMask, string pnDeviceName)
        {
            ProfileAccessPolicy.DemandWrite("ConfigureHardwareNode");
            RequireSameSelectedProject(expectedProjectPath);
            var item = GetOnlineDeviceItem(deviceItemPath);
            var network = item.GetService<NetworkInterface>() ?? throw new ArgumentException("No network interface exists at this path.");
            if (nodeIndex < 0 || nodeIndex >= network.Nodes.Count) throw new ArgumentException("Node index is outside the available nodes.");
            ValidateIpv4(address, "address");
            ValidateSubnetMask(subnetMask);
            ValidateUnicastAddress(address);
            var ipBytes = IPAddress.Parse(address).GetAddressBytes();
            var maskBytes = IPAddress.Parse(subnetMask).GetAddressBytes();
            var host = ipBytes.Select((value, index) => value & ~maskBytes[index]).ToArray();
            if (host.All(value => value == 0) || host.Select((value, index) => value == (255 & ~maskBytes[index])).All(value => value)) throw new ArgumentException("Address cannot be the subnet network or broadcast address.");
            ValidateProfinetName(pnDeviceName);
            var node = network.Nodes[nodeIndex];
            ValidateExactValue(Convert.ToString(node.GetAttribute("Address")), expectedAddress, "address");
            ValidateExactValue(Convert.ToString(node.GetAttribute("SubnetMask")), expectedSubnetMask, "subnet mask");
            ValidateExactValue(Convert.ToString(node.GetAttribute("PnDeviceName")), expectedPnDeviceName, "PROFINET device name");
            try
            {
                node.SetAttribute("Address", address);
                node.SetAttribute("SubnetMask", subnetMask);
                node.SetAttribute("PnDeviceName", pnDeviceName);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Node configuration may be partially applied. Read it back before retrying. " + ex.Message, ex);
            }
            return new { Path = deviceItemPath, Index = nodeIndex, Address = node.GetAttribute("Address"), SubnetMask = node.GetAttribute("SubnetMask"), PnDeviceName = node.GetAttribute("PnDeviceName"), Saved = false, Downloaded = false };
        }

        public object AssignProfinetDevice(string expectedProjectPath, string devicePath, int nodeIndex, int connectorIndex, string controllerPath, int controllerNodeIndex, int controllerIndex, string expectedSubnet, string expectedSystem, int expectedSystemNumber, string expectedCurrentSubnet, string expectedCurrentSystem, int? expectedCurrentSystemNumber, string expectedCurrentControllerPath, int expectedCurrentControllerIndex, bool replaceExisting)
        {
            ProfileAccessPolicy.DemandWrite("AssignProfinetDevice");
            RequireSameSelectedProject(expectedProjectPath);
            var device = GetOnlineDeviceItem(devicePath).GetService<NetworkInterface>() ?? throw new ArgumentException("Device interface not found.");
            var controller = GetOnlineDeviceItem(controllerPath).GetService<NetworkInterface>() ?? throw new ArgumentException("Controller interface not found.");
            if (nodeIndex < 0 || nodeIndex >= device.Nodes.Count || connectorIndex < 0 || connectorIndex >= device.IoConnectors.Count || controllerNodeIndex < 0 || controllerNodeIndex >= controller.Nodes.Count || controllerIndex < 0 || controllerIndex >= controller.IoControllers.Count) throw new ArgumentException("An interface index is outside the available entries.");
            var node = device.Nodes[nodeIndex];
            var connector = device.IoConnectors[connectorIndex];
            var subnet = controller.Nodes[controllerNodeIndex].ConnectedSubnet ?? throw new ArgumentException("Controller node is not connected to a subnet.");
            var system = controller.IoControllers[controllerIndex].IoSystem ?? throw new ArgumentException("Controller has no I/O system.");
            ValidateExactValue(subnet.Name, expectedSubnet, "controller subnet");
            ValidateExactValue(system.Name, expectedSystem, "controller I/O system");
            if (system.Number != expectedSystemNumber) throw new ArgumentException("Controller I/O system number differs from the expected value.");
            ValidateExactValue(node.ConnectedSubnet?.Name ?? "", expectedCurrentSubnet, "device subnet");
            ValidateExactValue(connector.ConnectedToIoSystem?.Name ?? "", expectedCurrentSystem, "device I/O system");
            if (connector.ConnectedToIoSystem?.Number != expectedCurrentSystemNumber) throw new ArgumentException("Device I/O system number differs from the expected value.");
            if (connector.ConnectedToIoSystem == null)
            {
                if (expectedCurrentControllerPath != "") throw new ArgumentException("Expected current controller must be empty for an unbound connector.");
            }
            else
            {
                var currentController = GetOnlineDeviceItem(expectedCurrentControllerPath).GetService<NetworkInterface>() ?? throw new ArgumentException("Expected current controller interface not found.");
                if (expectedCurrentControllerIndex < 0 || expectedCurrentControllerIndex >= currentController.IoControllers.Count || currentController.IoControllers[expectedCurrentControllerIndex].IoSystem != connector.ConnectedToIoSystem) throw new ArgumentException("Current controller differs from the expected controller path and index.");
            }
            var changeSubnet = node.ConnectedSubnet != subnet;
            var changeSystem = connector.ConnectedToIoSystem != system;
            if (!replaceExisting && ((changeSubnet && node.ConnectedSubnet != null) || (changeSystem && connector.ConnectedToIoSystem != null))) throw new ArgumentException("Existing bindings require replaceExisting=true.");
            try
            {
                if (changeSubnet && node.ConnectedSubnet != null) node.DisconnectFromSubnet();
                if (changeSubnet) node.ConnectToSubnet(subnet);
                if (changeSystem && connector.ConnectedToIoSystem != null) connector.DisconnectFromIoSystem();
                if (changeSystem) connector.ConnectToIoSystem(system);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("PROFINET assignment may be partially applied. Read it back before retrying. " + ex.Message, ex);
            }
            return new { DevicePath = devicePath, ControllerPath = controllerPath, Subnet = subnet.Name, IoSystem = system.Name, system.Number, Saved = false, Downloaded = false };
        }

        private System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, DeviceItem>> AllHardwareItems()
        {
            return DeviceItems(_project!.Devices, "").Concat(DeviceItems(_project.UngroupedDevicesGroup.Devices, "UngroupedDevices/")).Concat(GroupDeviceItems(_project.DeviceGroups, ""));
        }

        private void RequireSameSelectedProject(string expectedProjectPath)
        {
            var previous = _project;
            if (previous == null) throw new ArgumentException("No project is selected.");
            RefreshSelectedProject();
            if (!SameProject(previous, _project))
            {
                _project = null;
                _session = null;
                throw new ArgumentException("The selected project was closed or changed. Select the intended project again.");
            }
            if (_project == null || !PathsEqual(_project.Path?.ToString(), expectedProjectPath)) throw new ArgumentException("The open project differs from the expected project path.");
        }

        internal static void ValidateExactValue(string? current, string expected, string label)
        {
            if (expected == null || !String.Equals(current, expected, StringComparison.Ordinal)) throw new ArgumentException("Current " + label + " differs from the expected value.");
        }

        internal static void RequireHardwareName(string value, string label)
        {
            if (String.IsNullOrWhiteSpace(value) || value != value.Trim()) throw new ArgumentException("An exact " + label + " is required.");
        }

        internal static void ValidateSubnetMask(string value)
        {
            ValidateIpv4(value, "subnet mask");
            var bytes = IPAddress.Parse(value).GetAddressBytes();
            uint mask = ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
            var inverse = ~mask;
            if (mask == 0 || mask == UInt32.MaxValue || (inverse & (inverse + 1)) != 0) throw new ArgumentException("Subnet mask must be contiguous and have both network and host bits.");
        }

        internal static void ValidateUnicastAddress(string value)
        {
            ValidateIpv4(value, "address");
            var bytes = IPAddress.Parse(value).GetAddressBytes();
            if (bytes[0] == 0 || bytes[0] == 127 || bytes[0] >= 224 || (bytes[0] == 169 && bytes[1] == 254)) throw new ArgumentException("Address must be a unicast IPv4 address.");
        }

        internal static void ValidateProfinetName(string value)
        {
            if (String.IsNullOrEmpty(value) || value.Length > 240 || value.Split('.').Any(label => label.Length == 0 || label.Length > 63 || label[0] == '-' || label[label.Length - 1] == '-' || label.Any(c => !((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-')))) throw new ArgumentException("PROFINET name must contain lower-case DNS labels of up to 63 letters, digits or hyphens, with no leading or trailing hyphens.");
        }

        internal static void ValidateIpv4(string value, string label)
        {
            if (!IPAddress.TryParse(value, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork || ip.ToString() != value) throw new ArgumentException("A canonical IPv4 " + label + " is required.");
        }
    }

    public class TiaProcessEntry
    {
        public int Id { get; set; }
        public string? Mode { get; set; }
        public string? Path { get; set; }
        public string? ProjectPath { get; set; }
    }

    public class HardwareCatalogEntry
    {
        public string? ArticleNumber { get; set; }
        public string? TypeIdentifier { get; set; }
        public string? Version { get; set; }
        public string? TypeName { get; set; }
    }
}
