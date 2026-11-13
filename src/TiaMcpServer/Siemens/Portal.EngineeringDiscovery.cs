using Siemens.Engineering;
using Siemens.Engineering.HW;
using System;
using System.Linq;
using TiaMcpServer.ModelContextProtocol;
using EngineeringAttribute = TiaMcpServer.ModelContextProtocol.Attribute;

namespace TiaMcpServer.Siemens
{
    public partial class Portal
    {
        private IEngineeringObject ResolveEngineeringDiscovery(string kind, string path, string softwarePath)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Length > 1024 || softwarePath == null || softwarePath.Length > 1024)
                throw new ArgumentException("Supply an exact discovered path of at most 1024 characters.");
            IEngineeringObject? target;
            switch (kind)
            {
                case "device": target = FindExactDiscoveryDevice(path); break;
                case "deviceItem": target = GetOnlineDeviceItem(path); break;
                case "plcSoftware": target = GetPlcSoftware(path); break;
                case "block": target = GetBlock(softwarePath, path); break;
                case "type": target = GetType(softwarePath, path); break;
                default: throw new ArgumentException("Supported kinds are device, deviceItem, plcSoftware, block and type.");
            }
            return target ?? throw new ArgumentException("No object was found at the exact path.");
        }

        private Device? FindExactDiscoveryDevice(string path)
        {
            var matches = GetDevices().Where(d => string.Equals(GetDevicePath(d), path, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count > 1) throw new ArgumentException("The device path is ambiguous.");
            return matches.SingleOrDefault();
        }

        private string DiscoveryProjectScope()
        {
            RefreshSelectedProject();
            if (_project == null) throw new ArgumentException("No project is selected.");
            return _project.Path?.FullName ?? throw new ArgumentException("The selected project has no path.");
        }

        public object DescribeEngineeringDiscovery(string kind, string path, string softwarePath)
        {
            var project = DiscoveryProjectScope();
            var target = ResolveEngineeringDiscovery(kind, path, softwarePath);
            var services = target is IEngineeringServiceProvider provider
                ? provider.GetServiceInfos().Select(s => s.Type.FullName ?? s.Type.Name).Take(201).ToArray()
                : Array.Empty<string>();
            var collections = kind == "device" || kind == "deviceItem" ? new[] { "deviceItems" }
                : kind == "plcSoftware" ? new[] { "blocks", "types", "tagTables", "externalSources" }
                : Array.Empty<string>();
            return new { Kind = kind, Path = path, SoftwarePath = softwarePath, ProjectPath = project, AllowedChildCollections = collections,
                DeclaredServices = services.Take(200).ToArray(), ServicesTruncated = services.Length > 200, ServiceNote = "Declared services are metadata; runtime availability depends on the selected object and TIA state." };
        }

        public PagedResponse<EngineeringAttribute> GetEngineeringDiscoveryAttributes(string kind, string path, string softwarePath, int limit, string cursor)
        {
            var project = DiscoveryProjectScope();
            var target = ResolveEngineeringDiscovery(kind, path, softwarePath);
            var infos = target.GetAttributeInfos().OrderBy(a => a.Name, StringComparer.Ordinal).ToArray();
            var request = new PageRequest { Limit = limit, Cursor = cursor };
            var scope = "engineering-attributes:" + project + "\n" + kind + "\n" + path + "\n" + softwarePath + "\n" + string.Join("\n", infos.Select(a => a.Name + ":" + a.AccessMode));
            var offset = request.GetOffset(scope);
            if (offset > infos.Length) throw new ArgumentException("The cursor is outside the available attributes.");
            var rows = infos.Skip(offset).Take(request.GetBoundedLimit()).Select(a =>
            {
                object? value;
                try
                {
                    var name = a.Name;
                    var sensitive = new[] { "password", "secret", "credential", "token", "privatekey" }.Any(x => name.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0);
                    value = sensitive ? "<redacted>" : Helper.GetAttributeValue(target.GetAttribute(name));
                }
                catch (Exception ex) { value = "<unavailable: " + ex.GetType().Name + ">"; }
                return new EngineeringAttribute { Name = a.Name, AccessMode = a.AccessMode.ToString(), Value = value };
            }).ToList();
            var more = offset + rows.Count < infos.Length;
            return new PagedResponse<EngineeringAttribute> { Items = rows, Page = new PageInfo { Returned = rows.Count,
                HasMore = more, NextCursor = more ? PageRequest.CreateCursor(offset + rows.Count, scope) : null } };
        }

        public PagedResponse<EngineeringChild> GetEngineeringDeviceItemChildren(string path, int limit, string cursor)
        {
            var project = DiscoveryProjectScope();
            var device = FindExactDiscoveryDevice(path);
            var childItems = device != null ? device.DeviceItems : GetOnlineDeviceItem(path).DeviceItems;
            var items = childItems.Select(i => new EngineeringChild { Kind = "deviceItem", Name = i.Name, Path = path.TrimEnd('/') + "/" + i.Name }).OrderBy(i => i.Path, StringComparer.Ordinal).ToArray();
            var scope = "engineering-children:" + project + "\n" + path + "\n" + string.Join("\n", items.Select(i => i.Path));
            var request = new PageRequest { Limit = limit, Cursor = cursor };
            var offset = request.GetOffset(scope);
            if (offset > items.Length) throw new ArgumentException("The cursor is outside the available children.");
            var rows = items.Skip(offset).Take(request.GetBoundedLimit()).ToArray();
            var more = offset + rows.Length < items.Length;
            return new PagedResponse<EngineeringChild> { Items = rows, Page = new PageInfo { Returned = rows.Length,
                HasMore = more, NextCursor = more ? PageRequest.CreateCursor(offset + rows.Length, scope) : null } };
        }
    }

    public sealed class EngineeringChild
    {
        public string Kind { get; set; } = "";
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
    }
}
