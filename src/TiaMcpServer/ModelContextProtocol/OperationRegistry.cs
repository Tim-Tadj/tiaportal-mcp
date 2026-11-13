using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace TiaMcpServer.ModelContextProtocol
{
    public sealed class OperationSummary
    {
        public string Name { get; set; } = "";
        public string Domain { get; set; } = "";
        public string Effect { get; set; } = "";
        public string Profile { get; set; } = "";
        public string Status { get; set; } = "";
        public string RuntimeAvailability { get; set; } = "";
    }

    public sealed class OperationInfo
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Domain { get; set; } = "";
        public string Effect { get; set; } = "";
        public string Profile { get; set; } = "";
        public string Status { get; set; } = "";
        public int MinimumTiaVersion { get; set; }
        public int? MaximumTiaVersion { get; set; }
        public bool? ReadOnly { get; set; }
        public bool Advertised { get; set; }
        public string Parameters { get; set; } = "";
        public string ImplementationStatus { get; set; } = "ImplementedExperimental";
        public string RuntimeAvailability { get; set; } = "NotChecked";
        public string ValidationStatus { get; set; } = "NotRun";
        public string Prerequisites { get; set; } = "";
        public string Recovery { get; set; } = "";
        public bool WritesFiles { get; set; }
        public bool ModifiesProject { get; set; }
        public bool ChangesEngineeringConnection { get; set; }
        public bool ChangesPlc { get; set; }
        public bool NativeEffectsUnclassified { get; set; }
    }

    public sealed class OperationParameter
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public bool Required { get; set; }
        public string? Default { get; set; }
        public string Description { get; set; } = "";
    }

    public sealed class OperationDetails
    {
        public OperationInfo Operation { get; set; } = new OperationInfo();
        public OperationParameter[] Parameters { get; set; } = Array.Empty<OperationParameter>();
    }

    internal static class OperationRegistry
    {
        private static readonly HashSet<string> WriteTools = Names(
            "Disconnect OpenProject SaveProject SaveAsProject CloseProject CompileSoftware ExportBlock ImportBlock ExportBlocks ExportType ImportType ExportTypes ExportAsDocuments ExportBlocksAsDocuments ImportFromDocuments ImportBlocksFromDocuments ImportBlockFromSource GoOnlineDevice GoOfflineDevice ChangeHardwareType CompileHardware SetBaseUnitPotentialGroup CreateHardwareDevice PlugHardwareModule ConfigureHardwareNode ConfigureIoAddress AssignProfinetDevice CreateProfinetIoSystem ExportDeviceConfiguration ReadBlockContent ReadTypeContent");
        private static readonly HashSet<string> WriteProfileReads = Names("GetCompileDiagnostics");
        private static readonly HashSet<string> Hardware = Names("GetAccessibleDevices GetHardwareNetworkInfo ChangeHardwareType CompileHardware SetBaseUnitPotentialGroup FindHardwareCatalog CreateHardwareDevice PlugHardwareModule ConfigureHardwareNode ConfigureIoAddress AssignProfinetDevice CreateProfinetIoSystem");
        private static readonly HashSet<string> Online = Names("GetDeviceOnlineState GetOnlineConnectionOptions GoOnlineDevice GoOfflineDevice ReadPlcValues");
        private static readonly HashSet<string> Projects = Names("ListProjects GetProject GetProjectTree OpenProject SaveProject SaveAsProject CloseProject");
        private static readonly HashSet<string> Devices = Names("GetDevices GetDeviceInfo GetDeviceItemInfo ExportDeviceConfiguration");
        private static readonly HashSet<string> Software = Names("GetSoftwareInfo GetSoftwareTree CompileSoftware GetCompileDiagnostics GetPlcTagTables GetPlcTags FindReferences ImportBlockFromSource GetExternalSources");
        private static readonly HashSet<string> Types = Names("GetTypes GetTypeInfo ExportType ExportTypes ImportType ReadTypeContent");
        private static readonly HashSet<string> Blocks = Names("GetBlocks GetBlockInfo GetBlocksWithHierarchy ExportBlock ExportBlocks ImportBlock ExportAsDocuments ExportBlocksAsDocuments ImportFromDocuments ImportBlocksFromDocuments ReadBlockContent");
        private static readonly HashSet<string> FileWrites = Names("ExportDeviceConfiguration SaveProject SaveAsProject ExportBlock ExportBlocks ExportType ExportTypes ExportAsDocuments ExportBlocksAsDocuments ReadBlockContent ReadTypeContent");
        private static readonly HashSet<string> Discovery = Names("GetCapabilities GetOperations GetOperationInfo GetState GetTiaProcesses Connect Disconnect DescribeEngineeringSurface BrowseEngineeringObject ReadEngineeringSurfaceAttributes DescribeEngineeringObject ListEngineeringChildren GetEngineeringAttributes");
        private static readonly HashSet<string> ConnectionWrites = Names("Disconnect GoOnlineDevice GoOfflineDevice");
        private static readonly HashSet<string> PublicApiReads = Names("ListApiTypes DescribeApiObject ReadApiProperty ReadApiField BrowseApiCollection GetApiService ReadApiEvents DescribeEngineeringCreation DescribeEngineeringActions");
        private static readonly HashSet<string> PublicApiWrites = Names("InvokeApiMethod CreateApiObject WriteApiProperty WriteApiField ReleaseApiObject SubscribeApiEvent UnsubscribeApiEvent CreateEngineeringObject UpdateEngineeringAttributes InvokeEngineeringAction");

        private static HashSet<string> Names(string names) => new HashSet<string>(names.Split(' '), StringComparer.Ordinal);

        public static IReadOnlyList<OperationInfo> GetAll(int version)
        {
            return new[] { typeof(McpServer), typeof(McpListTools) }
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                .Select(m => new { Method = m, Tool = m.GetCustomAttribute<McpServerToolAttribute>() })
                .Where(x => x.Tool != null)
                .Select(x => Create(x.Method, x.Tool!, version))
                .OrderBy(x => x.Name, StringComparer.Ordinal)
                .ToList();
        }

        public static OperationDetails GetDetails(string name, int version)
        {
            var method = new[] { typeof(McpServer), typeof(McpListTools) }
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                .FirstOrDefault(m => (m.GetCustomAttribute<McpServerToolAttribute>()?.Name ?? "") == name)
                ?? throw new ArgumentException("No advertised operation has that exact name.");
            var tool = method.GetCustomAttribute<McpServerToolAttribute>()!;
            return new OperationDetails
            {
                Operation = Create(method, tool, version),
                Parameters = method.GetParameters().Select(p => new OperationParameter
                {
                    Name = p.Name ?? "", Type = FriendlyType(p.ParameterType), Required = !p.HasDefaultValue,
                    Default = p.HasDefaultValue ? Convert.ToString(p.DefaultValue, System.Globalization.CultureInfo.InvariantCulture) : null,
                    Description = p.GetCustomAttribute<DescriptionAttribute>()?.Description ?? ""
                }).ToArray()
            };
        }

        internal static string FriendlyType(Type type)
        {
            if (type.IsArray)
                return FriendlyType(type.GetElementType()!) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
            var nullable = Nullable.GetUnderlyingType(type);
            if (nullable != null) return FriendlyType(nullable) + "?";
            if (!type.IsGenericType) return type.Name;
            var name = type.Name;
            var tick = name.IndexOf('`');
            if (tick >= 0) name = name.Substring(0, tick);
            return name + "<" + string.Join(", ", type.GetGenericArguments().Select(FriendlyType)) + ">";
        }

        private static OperationInfo Create(MethodInfo method, McpServerToolAttribute tool, int version)
        {
            var name = tool.Name ?? method.Name;
            var nativeEffects = PublicApiWrites.Contains(name) && name != "UnsubscribeApiEvent";
            var api = PublicApiReads.Contains(name) || PublicApiWrites.Contains(name);
            var write = WriteTools.Contains(name) || PublicApiWrites.Contains(name);
            var classified = api || Discovery.Contains(name) || Hardware.Contains(name) || Online.Contains(name) || Projects.Contains(name) || Devices.Contains(name) || Software.Contains(name) || Types.Contains(name) || Blocks.Contains(name);
            var min = name == "GetAccessibleDevices" || name == "FindHardwareCatalog" || name == "CreateHardwareDevice" ? 19 : name == "FindReferences" || name == "ChangeHardwareType" ? 18 : 17;
            var documents = name == "ExportAsDocuments" || name == "ExportBlocksAsDocuments" || name == "ImportFromDocuments" || name == "ImportBlocksFromDocuments";
            if (documents) min = 20;
            var supported = version >= min && (!documents || version == 20);
            var domain = api ? "public-api" : Hardware.Contains(name) ? "hardware" : Online.Contains(name) ? "online" : Projects.Contains(name) ? "projects" : Devices.Contains(name) ? "devices" : Software.Contains(name) ? "plc-software" : Types.Contains(name) ? "types" : Blocks.Contains(name) ? "blocks" : "discovery";
            var effect = nativeEffects ? "unclassified native effects; may write files, change projects, connections or PLCs" : name == "UnsubscribeApiEvent" ? "changes event subscription" : !classified ? "unclassified" : name == "Connect" ? "connection; may start TIA in ReadWrite when explicitly requested" : FileWrites.Contains(name) ? "writes files" : ConnectionWrites.Contains(name) ? "changes connection state" : write ? "changes or compiles project" : "read only";
            return new OperationInfo
            {
                Name = name,
                Description = method.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "",
                Domain = domain,
                Effect = effect,
                Profile = !classified ? "unknown" : write || WriteProfileReads.Contains(name) ? "ReadWrite" : "Read, ReadWrite",
                Status = !classified ? "unclassified" : supported ? "implemented" : "unsupported-version",
                RuntimeAvailability = supported ? "NotChecked" : "UnsupportedVersion",
                Prerequisites = domain == "discovery" ? "See parameter descriptions and tool schema." : domain == "online" ? "A suitable target and its connection prerequisites are required." : "A selected project and exact discovered paths may be required.",
                Recovery = supported ? "Check connection, selected project and exact paths if the request fails." : "Use a worker with a supported TIA version; do not retry on this version.",
                WritesFiles = nativeEffects || FileWrites.Contains(name),
                ModifiesProject = nativeEffects || (write && name != "UnsubscribeApiEvent" && !FileWrites.Contains(name) && !ConnectionWrites.Contains(name)),
                ChangesEngineeringConnection = nativeEffects || ConnectionWrites.Contains(name) || name == "Connect" || name == "UnsubscribeApiEvent",
                ChangesPlc = nativeEffects,
                NativeEffectsUnclassified = nativeEffects,
                MinimumTiaVersion = min,
                MaximumTiaVersion = documents ? 20 : (int?)null,
                ReadOnly = !classified ? (bool?)null : !write && name != "Connect",
                Advertised = true,
                Parameters = string.Join(", ", method.GetParameters().Select(p => p.Name + (p.HasDefaultValue ? " (optional)" : " (required)")))
            };
        }
    }
}
