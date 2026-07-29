using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using TiaMcpServer.Runtime;
using TiaMcpServer.Security;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    [McpServerToolType]
    public static class McpServer
    {
        private static IServiceProvider? _services;
        private static Portal? _portal;

        public static ILogger? Logger { get; set; }

        public static Portal Portal
        {
            get
            {
                if (_services !=null)
                {
                    return _services.GetRequiredService<Portal>();
                }
                else
                {
                    if (_portal == null)
                    {
                        _portal = new Portal();
                    }
                    return _portal;
                }
            }
            set
            {
                _portal = value ?? throw new ArgumentNullException(nameof(value), "Portal cannot be null");
            }
        }

        public static void SetServiceProvider(IServiceProvider services)
        {
            _services = services;
        }

        #region portal

        [McpServerTool(Name = "Connect"), Description("Attach to the single running TIA Portal process. The read-write worker may launch TIA Portal when none is running; the read worker never launches it.")]
        public static ResponseConnect Connect()
        {
            Logger?.LogInformation("Connecting to TIA Portal...");

            try
            {
                var allowLaunch = WorkerBuild.Current.AccessProfile == AccessProfile.ReadWrite;
                if (Portal.ConnectPortal(allowLaunch))
                {
                    return new ResponseConnect
                    {
                        Message = "Connected to TIA-Portal",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException("Failed to connect to TIA-Portal", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error connecting to TIA-Portal: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

#if TIA_MCP_READ_WRITE
        [McpServerTool(Name = "Disconnect"), Description("Detach this ReadWrite worker from TIA Portal. This changes connection state and is not available in the Read profile.")]
        public static ResponseDisconnect Disconnect()
        {
            try
            {
                if (Portal.DisconnectPortal())
                {
                    return new ResponseDisconnect
                    {
                        Message = "Disconnected from TIA-Portal",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException("Failed disconnecting from TIA-Portal", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error disconnecting from TIA-Portal: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }
#endif

        #endregion

        #region state

        [McpServerTool(Name = "GetCapabilities"), Description("Call first to learn the exact TIA version, access profile, response formats, defaults and safe discovery sequence for this worker.")]
        public static ResponseCapabilities GetCapabilities()
        {
            var worker = WorkerBuild.Current;
            var toolFamilies = new List<string>
            {
                "connection",
                "state",
                "projects",
                "devices",
                "plc-software",
                "blocks",
                "types"
            };

#if TIA_MCP_READ_WRITE
            toolFamilies.Add("project-lifecycle");
            toolFamilies.Add("compile");
            toolFamilies.Add("xml-import-export");

            if (worker.TiaMajorVersion >= 20)
            {
                toolFamilies.Add("simatic-sd-documents");
            }
#endif

            return new ResponseCapabilities
            {
                TiaMajorVersion = worker.TiaMajorVersion,
                AccessProfile = worker.AccessProfile.ToString(),
                SupportStatus = "Experimental",
                Contract = "v1-with-toon-csv-preview",
                DefaultPageLimit = PageRequest.DefaultLimit,
                MaximumPageLimit = PageRequest.MaximumLimit,
                DefaultDetailLevel = ResponseDetailLevel.Summary.ToString(),
                DefaultResponseFormat = "Auto",
                SupportedResponseFormats = new[]
                {
                    "Auto",
                    "Toon",
                    "Csv",
                    "Json"
                },
                ToonProfile = "tia-toon-table/1 (TOON v4.1, scalar cells, maximum 200 rows and 32 columns)",
                ResponseFormatPolicy = new[]
                {
                    "Auto uses CSV for compact flat Summary lists.",
                    "Auto uses TOON for eligible Standard lists to preserve explicit fields, row counts and scalar types for the model.",
                    "Auto uses compact JSON for Full or nested results which cannot be represented losslessly as a bounded table.",
                    "CSV quotes string cells to preserve scalar types. Empty TOON arrays report their stable columns in result metadata.",
                    "Explicit Toon or Csv is rejected when it would lose result data. Explicit Json is always available."
                },
                CanModifyProject = worker.AccessProfile == AccessProfile.ReadWrite,
                CanWriteFiles = worker.AccessProfile == AccessProfile.ReadWrite,
                ToolFamilies = toolFamilies,
                Guidance = new[]
                {
                    "Call GetState before project discovery.",
                    "Use summary detail and follow cursors only as far as needed.",
                    "Keep responseFormat Auto unless a specific downstream parser requires Toon, Csv or Json.",
                    "Reuse exact names and paths returned by discovery tools.",
                    worker.AccessProfile == AccessProfile.Read
                        ? "This worker cannot save, compile, import, export or close projects."
                        : "Use mutating tools only when the user clearly requests the side effect."
                }
            };
        }

        [McpServerTool(Name = "GetState"), Description("Report connection and selected project or session state. Call after GetCapabilities and before project-scoped tools.")]
        public static ResponseState GetState()
        {
            try
            {
                var state = Portal.GetState();

                if (state != null)
                {
                    return new ResponseState
                    {
                        Message = "TIA-Portal MCP server state retrieved",
                        IsConnected = state.IsConnected,
                        Project = state.Project,
                        Session = state.Session,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException("Failed to retrieve TIA-Portal MCP server state", McpErrorCode.InternalError);
                }
                

            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving TIA-Portal MCP server state: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        #endregion

        #region project/session

        [McpServerTool(Name = "GetProject"), Description("Deprecated compatibility alias for ListProjects. Returns open projects and sessions.")]
        public static ResponseGetProjects GetProjects(
            [Description("detailLevel: Summary returns canonical identity; Full also includes bounded raw attributes")] ResponseDetailLevel detailLevel = ResponseDetailLevel.Summary)
        {
            return ListProjects(detailLevel);
        }

        public static ResponseGetProjects ListProjects(
            [Description("detailLevel: Summary returns canonical identity; Full also includes bounded raw attributes")] ResponseDetailLevel detailLevel = ResponseDetailLevel.Summary)
        {
            try
            {
                var list = Portal.GetProjects();

                list.AddRange(Portal.GetSessions());

                var responseList = new List<ResponseProjectInfo>();
                foreach (var project in list
                    .Where(project => project != null)
                    .OrderBy(project => project.Path?.ToString(), StringComparer.OrdinalIgnoreCase)
                    .ThenBy(project => project.Path?.ToString(), StringComparer.Ordinal))
                {
                    responseList.Add(new ResponseProjectInfo
                    {
                        Path = project.Path?.ToString(),
                        Name = project.Name,
                        Attributes = detailLevel == ResponseDetailLevel.Full
                            ? Helper.GetAttributeList(project)
                            : null
                    });
                }

                return new ResponseGetProjects
                {
                    Message = detailLevel == ResponseDetailLevel.Full
                        ? "Open projects and sessions retrieved"
                        : null,
                    Items = responseList,
                    Meta = detailLevel == ResponseDetailLevel.Full
                        ? new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                        : null
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving open projects: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

#if TIA_MCP_READ_WRITE
        [McpServerTool(Name = "OpenProject"), Description("Open an exact-version local project or session. This may close the currently selected project, so use only with clear user intent.")]
        public static ResponseOpenProject OpenProject(
            [Description("path: existing .apNN project or .alsNN session file matching this worker's exact TIA Portal major version")] string path)
        {
            try
            {
                var resolvedPath = Path.GetFullPath(
                    Environment.ExpandEnvironmentVariables(path));
                if (!File.Exists(resolvedPath))
                {
                    throw new McpException(
                        $"Project or session file does not exist: '{resolvedPath}'.",
                        McpErrorCode.InvalidParams);
                }

                var extension = Path.GetExtension(resolvedPath).ToLowerInvariant();
                var version = WorkerBuild.Current.TiaMajorVersion;
                var projectExtension = $".ap{version}";
                var sessionExtension = $".als{version}";
                if (extension != projectExtension && extension != sessionExtension)
                {
                    throw new McpException(
                        $"This exact TIA Portal V{version} worker accepts only " +
                        $"{projectExtension} projects or {sessionExtension} sessions.",
                        McpErrorCode.InvalidParams);
                }

                var success = extension == projectExtension
                    ? Portal.OpenProject(resolvedPath)
                    : Portal.OpenSession(resolvedPath);

                if (success)
                {
                    return new ResponseOpenProject
                    {
                        Message = $"Project or session '{resolvedPath}' opened",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException(
                        $"Failed to open project or session '{resolvedPath}'.",
                        McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (
                ex is ArgumentException ||
                ex is NotSupportedException ||
                ex is PathTooLongException)
            {
                throw new McpException(ex.Message, ex, McpErrorCode.InvalidParams);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error opening project '{path}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "SaveProject"), Description("Persist the current project or local session in place. This mutates project data and requires clear user intent.")]
        public static ResponseSaveProject SaveProject()
        {
            try
            {
                if (Portal.IsLocalSession)
                {
                    if (Portal.SaveSession())
                    {
                        return new ResponseSaveProject
                        {
                            Message = "Local session saved",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true
                            }
                        };
                    }
                    else
                    {
                        throw new McpException("Failed to save local session", McpErrorCode.InternalError);
                    }
                }
                else
                {
                    if (Portal.SaveProject())
                    {
                        return new ResponseSaveProject
                        {
                            Message = "Local project saved",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true
                            }
                        };
                    }
                    else
                    {
                        throw new McpException("Failed to save project", McpErrorCode.InternalError);
                    }
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error saving local project/session: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "SaveAsProject"), Description("Save the current local project to a new path. This writes a new project and is unavailable for local sessions.")]
        public static ResponseSaveAsProject SaveAsProject(
            [Description("newProjectPath: defines the new path where to save the project")] string newProjectPath)
        {
            try
            {
                if (Portal.IsLocalSession)
                {
                    throw new McpException($"Cannot save local session as '{newProjectPath}'", McpErrorCode.InvalidParams);
                }
                else
                {
                    if (Portal.SaveAsProject(newProjectPath))
                    {
                        return new ResponseSaveAsProject
                        {
                            Message = $"Local project saved as '{newProjectPath}'",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true
                            }
                        };
                    }
                    else
                    {
                        throw new McpException($"Failed saving local project as '{newProjectPath}'", McpErrorCode.InternalError);
                    }
                }

            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error saving local project/session as '{newProjectPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "CloseProject"), Description("Close the selected project or local session without closing TIA Portal. Save first only when the user explicitly requests it.")]
        public static ResponseCloseProject CloseProject()
        {
            try
            {
                bool success;

                if (Portal.IsLocalSession)
                {
                    success = Portal.CloseSession();
                    if (success)
                    {
                        return new ResponseCloseProject
                        {
                            Message = "Local session closed",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true
                            }
                        };
                    }
                    else
                    {
                        throw new McpException("Failed closing local session", McpErrorCode.InternalError);
                    }
                }
                else
                {
                    success = Portal.CloseProject();
                    if (success)
                    {
                        return new ResponseCloseProject
                        {
                            Message = "Local project closed",
                            Meta = new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true
                            }
                        };
                    }
                    else
                    {
                        throw new McpException("Failed closing project", McpErrorCode.InternalError);
                    }
                }

            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error closing local project/session: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }
#endif

        #endregion

        #region devices

        [McpServerTool(Name = "GetProjectTree"), Description("Legacy unpaged project tree which may be large. Prefer ListProjects and paged GetDevices for discovery.")]
        public static ResponseProjectTree GetProjectTree()
        {
            try
            {
                var tree = Portal.GetProjectTree();

                if (!string.IsNullOrEmpty(tree))
                {
                    return new ResponseProjectTree
                    {
                        Message = "Project tree retrieved",
                        Tree = "```\n" + tree + "\n```",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException("Failed retrieving project tree", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving project tree: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "GetDeviceInfo"), Description("Get one device using compact detail by default. Request Full only when bounded raw attributes are needed.")]
        public static ResponseDeviceInfo GetDeviceInfo(
            [Description("devicePath: exact path returned by device discovery")] string devicePath,
            [Description("detailLevel: Summary and Standard return identity; Full also includes bounded raw attributes and diagnostics")] ResponseDetailLevel detailLevel = ResponseDetailLevel.Standard)
        {
            try
            {
                var device = Portal.GetDevice(devicePath);

                if (device != null)
                {
                    return new ResponseDeviceInfo
                    {
                        Message = detailLevel == ResponseDetailLevel.Full
                            ? $"Device info retrieved from '{devicePath}'"
                            : null,
                        Path = Portal.GetDevicePath(device),
                        Name = device.Name,
                        Attributes = detailLevel == ResponseDetailLevel.Full
                            ? Helper.GetAttributeList(device)
                            : null,
                        Description = detailLevel == ResponseDetailLevel.Full
                            ? device.ToString()
                            : null,
                        Meta = detailLevel == ResponseDetailLevel.Full
                            ? new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true
                            }
                            : null
                    };
                }
                else
                {
                    throw new McpException($"Device not found at '{devicePath}'", McpErrorCode.InvalidParams);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving device info from '{devicePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "GetDeviceItemInfo"), Description("Get one device item using compact detail by default. Request Full only when bounded raw attributes are needed.")]
        public static ResponseDeviceItemInfo GetDeviceItemInfo(
            [Description("deviceItemPath: exact path returned by device discovery")] string deviceItemPath,
            [Description("detailLevel: Summary and Standard return identity; Full also includes bounded raw attributes and diagnostics")] ResponseDetailLevel detailLevel = ResponseDetailLevel.Standard)
        {
            try
            {
                var deviceItem = Portal.GetDeviceItem(deviceItemPath);

                if (deviceItem != null)
                {
                    return new ResponseDeviceItemInfo
                    {
                        Message = detailLevel == ResponseDetailLevel.Full
                            ? $"Device item info retrieved from '{deviceItemPath}'"
                            : null,
                        Path = deviceItemPath,
                        Name = deviceItem.Name,
                        Attributes = detailLevel == ResponseDetailLevel.Full
                            ? Helper.GetAttributeList(deviceItem)
                            : null,
                        Description = detailLevel == ResponseDetailLevel.Full
                            ? deviceItem.ToString()
                            : null,
                        Meta = detailLevel == ResponseDetailLevel.Full
                            ? new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true
                            }
                            : null
                    };
                }
                else
                {
                    throw new McpException($"Device item not found at '{deviceItemPath}'", McpErrorCode.InvalidParams);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving device item info from '{deviceItemPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        public static ResponseDevices GetDevices(
            [Description("detailLevel: Summary returns names only, Standard is reserved for typed device fields, and Full includes raw attributes and diagnostic descriptions")] ResponseDetailLevel detailLevel = ResponseDetailLevel.Summary,
            [Description("limit: maximum devices to return. Defaults to 50 and is capped at 200")] int limit = PageRequest.DefaultLimit,
            [Description("cursor: opaque cursor returned by the previous page; omit for the first page")] string cursor = "")
        {
            try
            {
                var pageRequest = new PageRequest
                {
                    Limit = limit,
                    Cursor = cursor
                };
                const string cursorScope = "GetDevices";
                var boundedLimit = pageRequest.GetBoundedLimit();
                var offset = pageRequest.GetOffset(cursorScope);
                var list = Portal.GetDevices()
                    .Where(device => device != null)
                    .Select(device => new
                    {
                        Device = device,
                        Path = Portal.GetDevicePath(device)
                    })
                    .OrderBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(entry => entry.Path, StringComparer.Ordinal)
                    .ToList();
                var responseList = new List<ResponseDeviceInfo>();

                if (offset > list.Count)
                {
                    throw new McpException("The paging cursor is outside the available device list.", McpErrorCode.InvalidParams);
                }

                var pageItems = list
                    .Skip(offset)
                    .Take(boundedLimit + 1)
                    .ToList();
                var hasMore = pageItems.Count > boundedLimit;

                if (hasMore)
                {
                    pageItems.RemoveAt(pageItems.Count - 1);
                }

                foreach (var entry in pageItems)
                {
                    var device = entry.Device;
                    var attributes = detailLevel == ResponseDetailLevel.Full
                        ? Helper.GetAttributeList(device)
                        : null;

                    responseList.Add(new ResponseDeviceInfo
                    {
                        Path = entry.Path,
                        Name = device.Name,
                        Attributes = attributes,
                        Description = detailLevel == ResponseDetailLevel.Full
                            ? device.ToString()
                            : null
                    });
                }

                var nextOffset = offset + responseList.Count;

                return new ResponseDevices
                {
                    Message = detailLevel == ResponseDetailLevel.Full
                        ? "Devices retrieved"
                        : null,
                    Items = responseList,
                    Page = new PageInfo
                    {
                        Returned = responseList.Count,
                        HasMore = hasMore,
                        NextCursor = hasMore
                            ? PageRequest.CreateCursor(nextOffset, cursorScope)
                            : null
                    },
                    Meta = detailLevel == ResponseDetailLevel.Full
                        ? new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                        : null
                };
            }
            catch (RegexMatchTimeoutException ex)
            {
                throw new McpException(
                    "The device filter exceeded the one-second match timeout.",
                    ex,
                    McpErrorCode.InvalidParams);
            }
            catch (ArgumentException ex)
            {
                throw new McpException(ex.Message, ex, McpErrorCode.InvalidParams);
            }
            catch (PortalException ex)
            {
                throw MapPortalException("Failed to retrieve devices", ex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving devices: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        #endregion

        #region plc software

        [McpServerTool(Name = "GetSoftwareInfo"), Description("Get one PLC software object using compact detail by default. Request Full only when bounded raw attributes are needed.")]
        public static ResponseSoftwareInfo GetSoftwareInfo(
            [Description("softwarePath: exact path returned by project discovery")] string softwarePath,
            [Description("detailLevel: Summary and Standard return identity; Full also includes bounded raw attributes and diagnostics")] ResponseDetailLevel detailLevel = ResponseDetailLevel.Standard)
        {
            try
            {
                var software = Portal.GetPlcSoftware(softwarePath);
                if (software != null)
                {

                    return new ResponseSoftwareInfo
                    {
                        Message = detailLevel == ResponseDetailLevel.Full
                            ? $"Software info retrieved from '{softwarePath}'"
                            : null,
                        Path = softwarePath,
                        Name = software.Name,
                        Attributes = detailLevel == ResponseDetailLevel.Full
                            ? Helper.GetAttributeList(software)
                            : null,
                        Description = detailLevel == ResponseDetailLevel.Full
                            ? software.ToString()
                            : null,
                        Meta = detailLevel == ResponseDetailLevel.Full
                            ? new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true
                            }
                            : null
                    };
                }
                else
                {
                    throw new McpException($"Software not found at '{softwarePath}'", McpErrorCode.InvalidParams);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving software info from '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

#if TIA_MCP_READ_WRITE
        [McpServerTool(Name = "CompileSoftware"), Description("Compile PLC software and return a compact state summary. Detailed diagnostics remain a planned paged surface.")]
        public static ResponseCompileSoftware CompileSoftware(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("password: optional safety administration password. Tool arguments may be retained by the MCP client, so leave empty unless the user explicitly accepts that exposure")] string password = "")
        {
            try
            {
                var result = Portal.CompileSoftware(softwarePath, password);
                if (result != null && !result.State.ToString().Equals("Error"))
                {
                    return new ResponseCompileSoftware
                    {
                        Message =
                            $"Software '{softwarePath}' compilation completed with state '{result.State}'.",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true,
                            ["state"] = result.State.ToString()
                        }
                    };
                }
                else
                {
                    var state = result?.State.ToString() ?? "NoResult";
                    throw new McpException(
                        $"Failed compiling software '{softwarePath}' with state '{state}'.",
                        McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error compiling software '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }
#endif

        [McpServerTool(Name = "GetSoftwareTree"), Description("Legacy unpaged PLC software tree which may be large. Prefer paged GetBlocks and GetTypes for discovery.")]
        public static ResponseSoftwareTree GetSoftwareTree(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath)
        {
            try
            {
                var tree = Portal.GetSoftwareTree(softwarePath);

                if (!string.IsNullOrEmpty(tree))
                {
                    return new ResponseSoftwareTree
                    {
                        Message = $"Software tree retrieved from '{softwarePath}'",
                        Tree = "```\n" + tree + "\n```",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed retrieving software tree from '{softwarePath}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving software tree from '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        #endregion

        #region blocks

        [McpServerTool(Name = "GetBlockInfo"), Description("Get one PLC block with typed compact detail. Request Full only when bounded raw attributes are needed.")]
        public static ResponseBlockInfo GetBlockInfo(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("blockPath: exact canonical path returned by GetBlocks")] string blockPath,
            [Description("detailLevel: Summary returns identity and core PLC fields, Standard adds typed metadata, and Full adds bounded raw attributes")] ResponseDetailLevel detailLevel = ResponseDetailLevel.Standard)
        {
            try
            {
                var block = Portal.GetBlock(softwarePath, blockPath);
                if (block != null)
                {
                    var includeStandard = detailLevel >= ResponseDetailLevel.Standard;
                    var includeFull = detailLevel == ResponseDetailLevel.Full;

                    return new ResponseBlockInfo
                    {
                        Message = includeFull
                            ? $"Block info retrieved from '{blockPath}' in '{softwarePath}'"
                            : null,
                        Path = Portal.GetBlockPath(block),
                        Name = block.Name,
                        TypeName = block.GetType().Name,
                        Namespace = includeStandard ? block.Namespace : null,
                        ProgrammingLanguage = Enum.GetName(typeof(ProgrammingLanguage),block.ProgrammingLanguage),
                        MemoryLayout = includeStandard
                            ? Enum.GetName(typeof(MemoryLayout), block.MemoryLayout)
                            : null,
                        IsConsistent = block.IsConsistent,
                        HeaderName = includeStandard ? block.HeaderName : null,
                        ModifiedDate = includeStandard ? block.ModifiedDate : null,
                        IsKnowHowProtected = includeStandard ? block.IsKnowHowProtected : null,
                        Attributes = includeFull ? Helper.GetAttributeList(block) : null,
                        Description = includeFull ? block.ToString() : null,
                        Meta = includeFull
                            ? new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true
                            }
                            : null
                    };
                }
                else
                {
                    throw new McpException($"Block not found at '{blockPath}' in '{softwarePath}'", McpErrorCode.InvalidParams);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving block info from '{blockPath}' in '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        public static ResponseBlocks GetBlocks(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("regexName: optional regular expression applied to block names; maximum 256 characters and one-second match timeout")] string regexName = "",
            [Description("detailLevel: Summary returns identity and core PLC fields, Standard adds typed metadata, and Full adds bounded raw attributes")] ResponseDetailLevel detailLevel = ResponseDetailLevel.Summary,
            [Description("limit: maximum blocks to return. Defaults to 50 and is capped at 200")] int limit = PageRequest.DefaultLimit,
            [Description("cursor: opaque cursor returned by the previous page; omit for the first page")] string cursor = "")
        {
            try
            {
                var pageRequest = new PageRequest
                {
                    Limit = limit,
                    Cursor = cursor
                };
                var cursorScope = $"GetBlocks\n{softwarePath}\n{regexName}";
                var boundedLimit = pageRequest.GetBoundedLimit();
                var offset = pageRequest.GetOffset(cursorScope);
                if (!string.IsNullOrWhiteSpace(regexName))
                {
                    _ = BoundedRegex.Create(regexName, RegexOptions.IgnoreCase);
                }

                var list = Portal.GetBlocks(softwarePath, regexName)
                    .Select(block => new
                    {
                        Block = block,
                        Path = Portal.GetBlockPath(block)
                    })
                    .OrderBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(entry => entry.Path, StringComparer.Ordinal)
                    .ToList();

                if (offset > list.Count)
                {
                    throw new McpException("The paging cursor is outside the available block list.", McpErrorCode.InvalidParams);
                }

                var pageItems = list
                    .Skip(offset)
                    .Take(boundedLimit + 1)
                    .ToList();
                var hasMore = pageItems.Count > boundedLimit;

                if (hasMore)
                {
                    pageItems.RemoveAt(pageItems.Count - 1);
                }

                var responseList = new List<ResponseBlockInfo>();
                foreach (var entry in pageItems)
                {
                    var block = entry.Block;
                    var includeStandard = detailLevel >= ResponseDetailLevel.Standard;
                    var includeFull = detailLevel == ResponseDetailLevel.Full;

                    responseList.Add(new ResponseBlockInfo
                    {
                        Path = entry.Path,
                        Name = block.Name,
                        TypeName = block.GetType().Name,
                        Namespace = includeStandard ? block.Namespace : null,
                        ProgrammingLanguage = Enum.GetName(typeof(ProgrammingLanguage), block.ProgrammingLanguage),
                        MemoryLayout = includeStandard
                            ? Enum.GetName(typeof(MemoryLayout), block.MemoryLayout)
                            : null,
                        IsConsistent = block.IsConsistent,
                        HeaderName = includeStandard ? block.HeaderName : null,
                        ModifiedDate = includeStandard ? block.ModifiedDate : null,
                        IsKnowHowProtected = includeStandard ? block.IsKnowHowProtected : null,
                        Attributes = includeFull ? Helper.GetAttributeList(block) : null,
                        Description = includeFull ? block.ToString() : null
                    });
                }

                var nextOffset = offset + responseList.Count;
                return new ResponseBlocks
                {
                    Message = detailLevel == ResponseDetailLevel.Full
                        ? $"Blocks with regex '{regexName}' retrieved from '{softwarePath}'"
                        : null,
                    Items = responseList,
                    Page = new PageInfo
                    {
                        Returned = responseList.Count,
                        HasMore = hasMore,
                        NextCursor = hasMore
                            ? PageRequest.CreateCursor(nextOffset, cursorScope)
                            : null
                    },
                    Meta = detailLevel == ResponseDetailLevel.Full
                        ? new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                        : null
                };
            }
            catch (RegexMatchTimeoutException ex)
            {
                throw new McpException(
                    "The block filter exceeded the one-second match timeout.",
                    ex,
                    McpErrorCode.InvalidParams);
            }
            catch (ArgumentException ex)
            {
                throw new McpException(ex.Message, ex, McpErrorCode.InvalidParams);
            }
            catch (PortalException ex)
            {
                throw MapPortalException("Failed to retrieve PLC blocks", ex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving blocks with regex '{regexName}' in '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "GetBlocksWithHierarchy"), Description("Legacy unpaged block hierarchy which may be large. Prefer paged GetBlocks unless the complete hierarchy is explicitly required.")]
        public static ResponseBlocksWithHierarchy GetBlocksWithHierarchy(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("detailLevel: Summary returns identity and core PLC fields; Full also includes bounded raw attributes")] ResponseDetailLevel detailLevel = ResponseDetailLevel.Summary)
        {
            try
            {
                var rootGroup = Portal.GetBlockRootGroup(softwarePath);
                if (rootGroup != null)
                {
                    var hierarchy = Helper.BuildBlockHierarchy(rootGroup, detailLevel);
                    return new ResponseBlocksWithHierarchy
                    {
                        Message = $"Block hierarchy retrieved from '{softwarePath}'",
                        Root = hierarchy,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    // Specific failure: root group could not be resolved
                    throw new McpException($"Block root group not found for '{softwarePath}'", McpErrorCode.InvalidParams);
                }
            }
            catch (PortalException ex)
            {
                throw MapPortalException("Failed to retrieve the PLC block hierarchy", ex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                // Generic unexpected failure wrapper
                throw new McpException($"Unexpected error retrieving block hierarchy for '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }



#if TIA_MCP_READ_WRITE
        [McpServerTool(Name = "ExportBlock"), Description("Export one exact PLC block as XML beneath the configured output root. Existing files are preserved unless overwrite is explicitly true.")]
        public static ResponseExportBlock ExportBlock(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("blockPath: full path to the block in the project structure, e.g. 'Group/Subgroup/Name' (single names are ambiguous)")] string blockPath,
            [Description("exportPath: directory beneath the configured output root, or an absolute directory contained by that root")] string exportPath,
            [Description("preservePath: preserves the path/structure of the plc software")] bool preservePath = false,
            [Description("overwrite: replace an existing output file. Defaults to false")] bool overwrite = false)
        {
            try
            {
                var resolvedExportPath = ResolveOutputDirectory(exportPath);
                var block = Portal.ExportBlock(
                    softwarePath,
                    blockPath,
                    resolvedExportPath,
                    preservePath,
                    overwrite);
                if (block != null)
                {
                    return new ResponseExportBlock
                    {
                        Message = $"Block exported from '{blockPath}' to '{resolvedExportPath}'",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                // Should not be reachable because Portal.ExportBlock throws on failure
                throw new McpException($"Failed exporting block from '{blockPath}' to '{exportPath}'", McpErrorCode.InternalError);
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                // Map known portal errors to sharper MCP errors and messages.
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.NotFound:
                        {
                            var suggestionNote = string.Empty;
                            // If the path has no '/', it may be incomplete; build suggestions using Portal's regex search and path resolver
                            if (!string.IsNullOrEmpty(blockPath) && !blockPath.Contains('/'))
                            {
                                try
                                {
                                    var escaped = Regex.Escape(blockPath);
                                    var blocks = Portal.GetBlocks(softwarePath, $"^{escaped}$");
                                    if (blocks == null || blocks.Count == 0)
                                    {
                                        blocks = Portal.GetBlocks(softwarePath, escaped);
                                    }

                                    var candidates = blocks
                                        .Take(10)
                                        .Select(b => Portal.GetBlockPath(b))
                                        .Where(p => !string.IsNullOrWhiteSpace(p))
                                        .Distinct(StringComparer.OrdinalIgnoreCase)
                                        .ToList();

                                    if (candidates.Count > 0)
                                    {
                                        suggestionNote = $" Did you mean: {string.Join(", ", candidates)}?";
                                    }
                                }
                                catch
                                {
                                    // Best-effort suggestions only
                                }
                            }

                            var msg = $"Block not found.{suggestionNote}".Trim();
                            throw new McpException(msg, McpErrorCode.InvalidParams);
                        }

                    case TiaMcpServer.Siemens.PortalErrorCode.ExportFailed:
                        {
                            // Relay underlying portal error with concise reason; log full details
                            var reason = pex.InnerException?.Message?.Trim();
                            var msg = "Failed to export block.";
                            if (!string.IsNullOrEmpty(reason)) msg += $" Reason: {reason}";

                            Logger?.LogError(pex, "MCP ExportBlock failed for {SoftwarePath} {BlockPath} -> {ExportPath}",
                                pex.Data?["softwarePath"], pex.Data?["blockPath"], pex.Data?["exportPath"]);

                            throw new McpException(msg, McpErrorCode.InternalError);
                        }

                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidParams:
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                        {
                            throw new McpException(pex.Message, McpErrorCode.InvalidParams);
                        }
                }

                // Fallback
                throw new McpException(pex.Message, McpErrorCode.InternalError);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error exporting block from '{blockPath}' to '{exportPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        private static string BuildBlockPathSuggestion(string softwarePath, string blockPath)
        {
            if (string.IsNullOrEmpty(blockPath) || blockPath.Contains('/')) return string.Empty;
            try
            {
                var escaped = Regex.Escape(blockPath);
                var blocks = Portal.GetBlocks(softwarePath, $"^{escaped}$");
                if (blocks == null || blocks.Count == 0)
                {
                    blocks = Portal.GetBlocks(softwarePath, escaped);
                }

                var candidates = blocks
                    .Take(10)
                    .Select(b =>
                    {
                        var name = b.Name;
                        var parts = new List<string> { name };
                        var parent = b.Parent;
                        while (parent != null)
                        {
                            if (parent is PlcBlockSystemGroup) break;
                            if (parent is PlcBlockGroup grp)
                            {
                                parts.Insert(0, grp.Name);
                                parent = grp.Parent;
                            }
                            else break;
                        }
                        if (parts.Count > 1) parts.RemoveAt(0);
                        return string.Join("/", parts);
                    })
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return candidates.Count > 0 ? $" Did you mean: {string.Join(", ", candidates)}?" : string.Empty;
            }
            catch
            {
                return string.Empty; // best effort only
            }
        }

        private static string ResolveOutputDirectory(string requestedPath)
        {
            try
            {
                return OutputPathPolicy.ResolveDirectory(requestedPath);
            }
            catch (Exception ex) when (
                ex is ArgumentException ||
                ex is InvalidOperationException ||
                ex is NotSupportedException ||
                ex is PathTooLongException)
            {
                throw new McpException(ex.Message, ex, McpErrorCode.InvalidParams);
            }
        }

        [McpServerTool(Name = "ImportBlock"), Description("Import one XML block into PLC software. This mutates the project and does not replace an existing block unless overwrite is explicitly true.")]
        public static ResponseImportBlock ImportBlock(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: defines the path in the project structure to the group, where to import the block")] string groupPath,
            [Description("importPath: defines the path of the xml file from where to import the block")] string importPath,
            [Description("overwrite: replace an existing block with the same identity. Defaults to false")] bool overwrite = false)
        {
            try
            {
                if (Portal.ImportBlock(softwarePath, groupPath, importPath, overwrite))
                {
                    return new ResponseImportBlock
                    {
                        Message = $"Block imported from '{importPath}' to '{groupPath}'",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed importing block from '{importPath}' to '{groupPath}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error importing block from '{importPath}' to '{groupPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "ExportBlocks"), Description("Export matching PLC blocks as XML beneath the configured output root. Use a bounded filter and inspect the compact partial-result counts.")]
        public static async Task<ResponseExportBlocks> ExportBlocks(
            IMcpServer server,
            RequestContext<CallToolRequestParams> context,
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("exportPath: directory beneath the configured output root, or an absolute directory contained by that root")] string exportPath,
            [Description("regexName: optional regular expression applied to block names; maximum 256 characters and one-second match timeout")] string regexName = "",
            [Description("preservePath: preserves the path/structure of the plc software")] bool preservePath = false,
            [Description("overwrite: replace existing output files. Defaults to false; existing items are skipped")] bool overwrite = false,
            [Description("resultLimit: maximum compact item summaries returned after the bulk operation. Defaults to 50 and is capped at 200")] int resultLimit = PageRequest.DefaultLimit)
        {
            var startTime = DateTime.Now;
            var progressToken = context.Params?.ProgressToken;
            
            try
            {
                var resolvedExportPath = ResolveOutputDirectory(exportPath);
                var boundedResultLimit = new PageRequest
                {
                    Limit = resultLimit
                }.GetBoundedLimit();
                if (!string.IsNullOrWhiteSpace(regexName))
                {
                    _ = BoundedRegex.Create(regexName, RegexOptions.IgnoreCase);
                }

                // First, get the list of blocks to determine total count
                Logger?.LogInformation($"Starting export of blocks from '{softwarePath}' to '{resolvedExportPath}'");
                
                var allBlocks = await Task.Run(() => Portal.GetBlocks(softwarePath, regexName));
                var totalBlocks = allBlocks?.Count ?? 0;

                if (totalBlocks == 0)
                {
                    if (progressToken != null)
                    {
                        await server.SendNotificationAsync("notifications/progress", new
                        {
                            Progress = 0,
                            Total = 0,
                            Message = "No blocks found to export",
                            progressToken
                        });
                    }
                    
                    return new ResponseExportBlocks
                    {
                        Message = $"No blocks found with regex '{regexName}' in '{softwarePath}'",
                        Items = new List<ResponseBlockInfo>(),
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true,
                            ["totalBlocks"] = 0,
                            ["exportedBlocks"] = 0,
                            ["duration"] = (DateTime.Now - startTime).TotalSeconds
                        }
                    };
                }

                // Send initial progress notification
                if (progressToken != null)
                {
                    await server.SendNotificationAsync("notifications/progress", new
                    {
                        Progress = 0,
                        Total = totalBlocks,
                        Message = $"Starting export of {totalBlocks} blocks...",
                        progressToken
                    });
                }

                // Export blocks asynchronously
                var exportedBlocks = await Task.Run(() => Portal.ExportBlocks(
                    softwarePath,
                    resolvedExportPath,
                    regexName,
                    preservePath,
                    overwrite));

                // Build list of inconsistent (skipped) blocks for reporting
                var inconsistentInfos = new List<ResponseBlockInfo>();
                var inconsistentCount = 0;
                if (allBlocks != null)
                {
                    foreach (var b in allBlocks)
                    {
                        if (b != null && b.IsConsistent == false)
                        {
                            inconsistentCount++;
                            if (inconsistentInfos.Count < boundedResultLimit)
                            {
                                inconsistentInfos.Add(new ResponseBlockInfo
                                {
                                    Path = Portal.GetBlockPath(b),
                                    Name = b.Name,
                                    TypeName = b.GetType().Name,
                                    ProgrammingLanguage = Enum.GetName(
                                        typeof(ProgrammingLanguage),
                                        b.ProgrammingLanguage),
                                    IsConsistent = b.IsConsistent
                                });
                            }
                        }
                    }
                }
                
                // Send progress update after export completion
                if (exportedBlocks != null && progressToken != null)
                {
                    var exportedCount = exportedBlocks.Count();
                    await server.SendNotificationAsync("notifications/progress", new
                    {
                        Progress = exportedCount,
                        Total = totalBlocks,
                        Message = $"Exported {exportedCount} of {totalBlocks} blocks",
                        progressToken
                    });
                }

                if (exportedBlocks != null)
                {
                    var responseList = new List<ResponseBlockInfo>();
                    var processedCount = 0;
                    
                    foreach (var block in exportedBlocks)
                    {
                        if (block != null)
                        {
                            if (responseList.Count < boundedResultLimit)
                            {
                                responseList.Add(new ResponseBlockInfo
                                {
                                    Path = Portal.GetBlockPath(block),
                                    Name = block.Name,
                                    TypeName = block.GetType().Name,
                                    ProgrammingLanguage = Enum.GetName(
                                        typeof(ProgrammingLanguage),
                                        block.ProgrammingLanguage),
                                    IsConsistent = block.IsConsistent
                                });
                            }
                        }
                        processedCount++;
                    }

                    // Send final progress notification
                    if (progressToken != null)
                    {
                        await server.SendNotificationAsync("notifications/progress", new
                        {
                            Progress = processedCount,
                            Total = totalBlocks,
                            Message = $"Export completed: {processedCount} blocks exported successfully",
                            progressToken
                        });
                    }

                    var duration = (DateTime.Now - startTime).TotalSeconds;
                    var expectedExportCount = Math.Max(
                        0,
                        totalBlocks - inconsistentCount);
                    var notExportedCount = Math.Max(
                        0,
                        expectedExportCount - processedCount);
                    Logger?.LogInformation($"Export completed: {processedCount} blocks exported in {duration:F2} seconds");

                    return new ResponseExportBlocks
                    {
                        Message = $"Export completed: {processedCount} blocks with regex '{regexName}' exported from '{softwarePath}' to '{resolvedExportPath}'",
                        Items = responseList,
                        Inconsistent = inconsistentInfos,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = notExportedCount == 0,
                            ["totalBlocks"] = totalBlocks,
                            ["exportedBlocks"] = processedCount,
                            ["inconsistentBlocks"] = inconsistentCount,
                            ["notExportedBlocks"] = notExportedCount,
                            ["returnedItems"] = responseList.Count,
                            ["itemsTruncated"] = processedCount > responseList.Count,
                            ["duration"] = duration
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed exporting blocks with '{regexName}' from '{softwarePath}' to {exportPath}", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (
                ex is ArgumentException ||
                ex is RegexMatchTimeoutException)
            {
                throw new McpException(
                    $"Invalid block filter '{regexName}': {ex.Message}",
                    ex,
                    McpErrorCode.InvalidParams);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                // Send error progress notification if we have a progress token
                if (progressToken != null)
                {
                    try
                    {
                        await server.SendNotificationAsync("notifications/progress", new
                        {
                            Progress = 0,
                            Total = 0,
                            Message = $"Export failed: {ex.Message}",
                            Error = true,
                            progressToken
                        });
                    }
                    catch
                    {
                        // Ignore notification errors during error handling
                    }
                }
                
                Logger?.LogError(ex, $"Failed exporting blocks with '{regexName}' from '{softwarePath}' to {exportPath}");
                throw new McpException($"Unexpected error exporting blocks with '{regexName}' from '{softwarePath}' to {exportPath}: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }
#endif

        #endregion

        #region types

        [McpServerTool(Name = "GetTypeInfo"), Description("Get one PLC data type with typed compact detail. Request Full only when bounded raw attributes are needed.")]
        public static ResponseTypeInfo GetTypeInfo(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("typePath: exact canonical path returned by GetTypes")] string typePath,
            [Description("detailLevel: Summary returns identity and consistency, Standard adds typed metadata, and Full adds bounded raw attributes")] ResponseDetailLevel detailLevel = ResponseDetailLevel.Standard)
        {
            try
            {
                var type = Portal.GetType(softwarePath, typePath);
                if (type != null)
                {
                    var includeStandard = detailLevel >= ResponseDetailLevel.Standard;
                    var includeFull = detailLevel == ResponseDetailLevel.Full;

                    return new ResponseTypeInfo
                    {
                        Message = includeFull
                            ? $"Type info retrieved from '{typePath}' in '{softwarePath}'"
                            : null,
                        Path = Portal.GetTypePath(type),
                        Name = type.Name,
                        TypeName = type.GetType().Name,
                        Namespace = includeStandard ? type.Namespace : null,
                        IsConsistent = type.IsConsistent,
                        ModifiedDate = includeStandard ? type.ModifiedDate : null,
                        IsKnowHowProtected = includeStandard ? type.IsKnowHowProtected : null,
                        Attributes = includeFull ? Helper.GetAttributeList(type) : null,
                        Description = includeFull ? type.ToString() : null,
                        Meta = includeFull
                            ? new JsonObject
                            {
                                ["timestamp"] = DateTime.Now,
                                ["success"] = true
                            }
                            : null
                    };
                }
                else
                {
                    throw new McpException($"Type not found at '{typePath}' in '{softwarePath}'", McpErrorCode.InvalidParams);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving type info from '{typePath}' in '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        public static ResponseTypes GetTypes(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("regexName: optional regular expression applied to type names; maximum 256 characters and one-second match timeout")] string regexName = "",
            [Description("detailLevel: Summary returns identity and consistency, Standard adds typed metadata, and Full adds bounded raw attributes")] ResponseDetailLevel detailLevel = ResponseDetailLevel.Summary,
            [Description("limit: maximum types to return. Defaults to 50 and is capped at 200")] int limit = PageRequest.DefaultLimit,
            [Description("cursor: opaque cursor returned by the previous page; omit for the first page")] string cursor = "")
        {
            try
            {
                var pageRequest = new PageRequest
                {
                    Limit = limit,
                    Cursor = cursor
                };
                var cursorScope = $"GetTypes\n{softwarePath}\n{regexName}";
                var boundedLimit = pageRequest.GetBoundedLimit();
                var offset = pageRequest.GetOffset(cursorScope);
                if (!string.IsNullOrWhiteSpace(regexName))
                {
                    _ = BoundedRegex.Create(regexName, RegexOptions.IgnoreCase);
                }

                var list = Portal.GetTypes(softwarePath, regexName)
                    .Select(type => new
                    {
                        Type = type,
                        Path = Portal.GetTypePath(type)
                    })
                    .OrderBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(entry => entry.Path, StringComparer.Ordinal)
                    .ToList();

                if (offset > list.Count)
                {
                    throw new McpException("The paging cursor is outside the available type list.", McpErrorCode.InvalidParams);
                }

                var pageItems = list
                    .Skip(offset)
                    .Take(boundedLimit + 1)
                    .ToList();
                var hasMore = pageItems.Count > boundedLimit;

                if (hasMore)
                {
                    pageItems.RemoveAt(pageItems.Count - 1);
                }

                var responseList = new List<ResponseTypeInfo>();
                foreach (var entry in pageItems)
                {
                    var type = entry.Type;
                    var includeStandard = detailLevel >= ResponseDetailLevel.Standard;
                    var includeFull = detailLevel == ResponseDetailLevel.Full;

                    responseList.Add(new ResponseTypeInfo
                    {
                        Path = entry.Path,
                        Name = type.Name,
                        TypeName = type.GetType().Name,
                        Namespace = includeStandard ? type.Namespace : null,
                        IsConsistent = type.IsConsistent,
                        ModifiedDate = includeStandard ? type.ModifiedDate : null,
                        IsKnowHowProtected = includeStandard ? type.IsKnowHowProtected : null,
                        Attributes = includeFull ? Helper.GetAttributeList(type) : null,
                        Description = includeFull ? type.ToString() : null
                    });
                }

                var nextOffset = offset + responseList.Count;
                return new ResponseTypes
                {
                    Message = detailLevel == ResponseDetailLevel.Full
                        ? $"Types with regex '{regexName}' retrieved from '{softwarePath}'"
                        : null,
                    Items = responseList,
                    Page = new PageInfo
                    {
                        Returned = responseList.Count,
                        HasMore = hasMore,
                        NextCursor = hasMore
                            ? PageRequest.CreateCursor(nextOffset, cursorScope)
                            : null
                    },
                    Meta = detailLevel == ResponseDetailLevel.Full
                        ? new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                        : null
                };
            }
            catch (RegexMatchTimeoutException ex)
            {
                throw new McpException(
                    "The type filter exceeded the one-second match timeout.",
                    ex,
                    McpErrorCode.InvalidParams);
            }
            catch (ArgumentException ex)
            {
                throw new McpException(ex.Message, ex, McpErrorCode.InvalidParams);
            }
            catch (PortalException ex)
            {
                throw MapPortalException("Failed to retrieve PLC data types", ex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving user defined types with regex '{regexName}' in '{softwarePath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

#if TIA_MCP_READ_WRITE
        [McpServerTool(Name = "ExportType"), Description("Export one exact PLC data type as XML beneath the configured output root. Existing files are preserved unless overwrite is explicitly true.")]
        public static ResponseExportType ExportType(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("exportPath: directory beneath the configured output root, or an absolute directory contained by that root")] string exportPath,
            [Description("typePath: defines the path in the project structure to the type")] string typePath,
            [Description("preservePath: preserves the path/structure of the plc software")] bool preservePath = false,
            [Description("overwrite: replace an existing output file. Defaults to false")] bool overwrite = false)
        {
            try
            {
                var resolvedExportPath = ResolveOutputDirectory(exportPath);
                var type = Portal.ExportType(
                    softwarePath,
                    typePath,
                    resolvedExportPath,
                    preservePath,
                    overwrite);
                if (type != null)
                {
                    return new ResponseExportType
                    {
                        Message = $"Type exported from '{typePath}' to '{resolvedExportPath}'",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed exporting type from '{typePath}' to '{exportPath}'", McpErrorCode.InternalError);
                }
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                switch (pex.Code)
                {
                    case TiaMcpServer.Siemens.PortalErrorCode.NotFound:
                        throw new McpException("Type not found.", McpErrorCode.InvalidParams);
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidState:
                    case TiaMcpServer.Siemens.PortalErrorCode.InvalidParams:
                        throw new McpException(pex.Message, McpErrorCode.InvalidParams);
                    case TiaMcpServer.Siemens.PortalErrorCode.ExportFailed:
                        {
                            var reason = pex.InnerException?.Message?.Trim();
                            var msg = "Failed to export type.";
                            if (!string.IsNullOrEmpty(reason)) msg += $" Reason: {reason}";
                            Logger?.LogError(pex, "MCP ExportType failed for {SoftwarePath} {TypePath} -> {ExportPath}",
                                pex.Data?["softwarePath"], pex.Data?["typePath"], pex.Data?["exportPath"]);
                            throw new McpException(msg, McpErrorCode.InternalError);
                        }
                }
                throw new McpException(pex.Message, McpErrorCode.InternalError);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error exporting type from '{typePath}' to '{exportPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "ImportType"), Description("Import one XML PLC data type. This mutates the project and does not replace an existing type unless overwrite is explicitly true.")]
        public static ResponseImportType ImportType(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: defines the path in the project structure to the group, where to import the type")] string groupPath,
            [Description("importPath: defines the path of the xml file from where to import the type")] string importPath,
            [Description("overwrite: replace an existing type with the same identity. Defaults to false")] bool overwrite = false)
        {
            try
            {
                if (Portal.ImportType(softwarePath, groupPath, importPath, overwrite))
                {
                    return new ResponseImportType
                    {
                        Message = $"Type imported from '{importPath}' to '{groupPath}'",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed importing type from '{importPath}' to '{groupPath}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error importing type from '{importPath}' to '{groupPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "ExportTypes"), Description("Export matching PLC data types as XML beneath the configured output root. Use a bounded filter and inspect the compact partial-result counts.")]
        public static async Task<ResponseExportTypes> ExportTypes(
            IMcpServer server,
            RequestContext<CallToolRequestParams> context,
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("exportPath: directory beneath the configured output root, or an absolute directory contained by that root")] string exportPath,
            [Description("regexName: optional regular expression applied to type names; maximum 256 characters and one-second match timeout")] string regexName = "",
            [Description("preservePath: preserves the path/structure of the plc software")] bool preservePath = false,
            [Description("overwrite: replace existing output files. Defaults to false; existing items are skipped")] bool overwrite = false,
            [Description("resultLimit: maximum compact item summaries returned after the bulk operation. Defaults to 50 and is capped at 200")] int resultLimit = PageRequest.DefaultLimit)
        {
            var startTime = DateTime.Now;
            var progressToken = context.Params?.ProgressToken;
            
            try
            {
                var resolvedExportPath = ResolveOutputDirectory(exportPath);
                var boundedResultLimit = new PageRequest
                {
                    Limit = resultLimit
                }.GetBoundedLimit();
                if (!string.IsNullOrWhiteSpace(regexName))
                {
                    _ = BoundedRegex.Create(regexName, RegexOptions.IgnoreCase);
                }

                // First, get the list of types to determine total count
                Logger?.LogInformation($"Starting export of types from '{softwarePath}' to '{resolvedExportPath}'");
                
                var allTypes = await Task.Run(() => Portal.GetTypes(softwarePath, regexName));
                var totalTypes = allTypes?.Count ?? 0;

                if (totalTypes == 0)
                {
                    if (progressToken != null)
                    {
                        await server.SendNotificationAsync("notifications/progress", new
                        {
                            Progress = 0,
                            Total = 0,
                            Message = "No types found to export",
                            progressToken
                        });
                    }
                    
                    return new ResponseExportTypes
                    {
                        Message = $"No types found with regex '{regexName}' in '{softwarePath}'",
                        Items = new List<ResponseTypeInfo>(),
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true,
                            ["totalTypes"] = 0,
                            ["exportedTypes"] = 0,
                            ["duration"] = (DateTime.Now - startTime).TotalSeconds
                        }
                    };
                }

                // Send initial progress notification
                if (progressToken != null)
                {
                    await server.SendNotificationAsync("notifications/progress", new
                    {
                        Progress = 0,
                        Total = totalTypes,
                        Message = $"Starting export of {totalTypes} types...",
                        progressToken
                    });
                }

                // Export types asynchronously
                var exportedTypes = await Task.Run(() => Portal.ExportTypes(
                    softwarePath,
                    resolvedExportPath,
                    regexName,
                    preservePath,
                    overwrite));

                // Build list of inconsistent (skipped) types for reporting
                var inconsistentTypeInfos = new List<ResponseTypeInfo>();
                var inconsistentTypeCount = 0;
                if (allTypes != null)
                {
                    foreach (var t in allTypes)
                    {
                        if (t != null && t.IsConsistent == false)
                        {
                            inconsistentTypeCount++;
                            if (inconsistentTypeInfos.Count < boundedResultLimit)
                            {
                                inconsistentTypeInfos.Add(new ResponseTypeInfo
                                {
                                    Path = Portal.GetTypePath(t),
                                    Name = t.Name,
                                    TypeName = t.GetType().Name,
                                    IsConsistent = t.IsConsistent
                                });
                            }
                        }
                    }
                }
                
                // Send progress update after export completion
                if (exportedTypes != null && progressToken != null)
                {
                    var exportedCount = exportedTypes.Count();
                    await server.SendNotificationAsync("notifications/progress", new
                    {
                        Progress = exportedCount,
                        Total = totalTypes,
                        Message = $"Exported {exportedCount} of {totalTypes} types",
                        progressToken
                    });
                }

                if (exportedTypes != null)
                {
                    var responseList = new List<ResponseTypeInfo>();
                    var processedCount = 0;
                    
                    foreach (var type in exportedTypes)
                    {
                        if (type != null)
                        {
                            if (responseList.Count < boundedResultLimit)
                            {
                                responseList.Add(new ResponseTypeInfo
                                {
                                    Path = Portal.GetTypePath(type),
                                    Name = type.Name,
                                    TypeName = type.GetType().Name,
                                    IsConsistent = type.IsConsistent
                                });
                            }
                        }
                        processedCount++;
                    }

                    // Send final progress notification
                    if (progressToken != null)
                    {
                        await server.SendNotificationAsync("notifications/progress", new
                        {
                            Progress = processedCount,
                            Total = totalTypes,
                            Message = $"Export completed: {processedCount} types exported successfully",
                            progressToken
                        });
                    }

                    var duration = (DateTime.Now - startTime).TotalSeconds;
                    var expectedExportCount = Math.Max(
                        0,
                        totalTypes - inconsistentTypeCount);
                    var notExportedCount = Math.Max(
                        0,
                        expectedExportCount - processedCount);
                    Logger?.LogInformation($"Type export completed: {processedCount} types exported in {duration:F2} seconds");

                    return new ResponseExportTypes
                    {
                        Message = $"Export completed: {processedCount} types with regex '{regexName}' exported from '{softwarePath}' to '{resolvedExportPath}'",
                        Items = responseList,
                        Inconsistent = inconsistentTypeInfos,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = notExportedCount == 0,
                            ["totalTypes"] = totalTypes,
                            ["exportedTypes"] = processedCount,
                            ["inconsistentTypes"] = inconsistentTypeCount,
                            ["notExportedTypes"] = notExportedCount,
                            ["returnedItems"] = responseList.Count,
                            ["itemsTruncated"] = processedCount > responseList.Count,
                            ["duration"] = duration
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed exporting types '{regexName}' from '{softwarePath}' to {exportPath}", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (
                ex is ArgumentException ||
                ex is RegexMatchTimeoutException)
            {
                throw new McpException(
                    $"Invalid type filter '{regexName}': {ex.Message}",
                    ex,
                    McpErrorCode.InvalidParams);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                // Send error progress notification if we have a progress token
                if (progressToken != null)
                {
                    try
                    {
                        await server.SendNotificationAsync("notifications/progress", new
                        {
                            Progress = 0,
                            Total = 0,
                            Message = $"Type export failed: {ex.Message}",
                            Error = true,
                            progressToken
                        });
                    }
                    catch
                    {
                        // Ignore notification errors during error handling
                    }
                }
                
                Logger?.LogError(ex, $"Failed exporting types '{regexName}' from '{softwarePath}' to {exportPath}");
                throw new McpException($"Unexpected error exporting types '{regexName}' from '{softwarePath}' to {exportPath}: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }
#endif

        #endregion

        #region documents

#if TIA_MCP_READ_WRITE && TIA_MCP_V20
        [McpServerTool(Name = "ExportAsDocuments"), Description("V20 only. Export one exact block as SIMATIC SD documents beneath the configured output root, without replacing files by default.")]
        public static ResponseExportAsDocuments ExportAsDocuments(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("blockPath: defines the path in the project structure to the block")] string blockPath,
            [Description("exportPath: directory beneath the configured output root, or an absolute directory contained by that root")] string exportPath,
            [Description("preservePath: preserves the path/structure of the plc software")] bool preservePath = false,
            [Description("overwrite: replace existing .s7dcl or .s7res files. Defaults to false")] bool overwrite = false)
        {
            try
            {
                var resolvedExportPath = ResolveOutputDirectory(exportPath);
                if (Engineering.TiaMajorVersion < 20)
                {
                    throw new McpException("ExportAsDocuments requires TIA Portal V20 or newer", McpErrorCode.InvalidParams);
                }
                if (Portal.ExportAsDocuments(
                    softwarePath,
                    blockPath,
                    resolvedExportPath,
                    preservePath,
                    overwrite))
                {
                    return new ResponseExportAsDocuments
                    {
                        Message = $"Documents exported from '{blockPath}' to '{resolvedExportPath}'",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed exporting documents from '{blockPath}' to '{exportPath}'", McpErrorCode.InternalError);
                }
            }
            catch (PortalException ex) when (
                ex.Code == PortalErrorCode.NotFound ||
                ex.Code == PortalErrorCode.InvalidParams ||
                ex.Code == PortalErrorCode.InvalidState)
            {
                throw new McpException(ex.Message, ex, McpErrorCode.InvalidParams);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error exporting documents from '{blockPath}' to '{exportPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "ExportBlocksAsDocuments"), Description("V20 only. Export matching blocks as SIMATIC SD documents beneath the configured output root and return bounded result counts.")]
        public static async Task<ResponseExportBlocksAsDocuments> ExportBlocksAsDocuments(
            IMcpServer server,
            RequestContext<CallToolRequestParams> context,
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("exportPath: directory beneath the configured output root, or an absolute directory contained by that root")] string exportPath,
            [Description("regexName: optional regular expression applied to block names; maximum 256 characters and one-second match timeout")] string regexName = "",
            [Description("preservePath: preserves the path/structure of the plc software")] bool preservePath = false,
            [Description("overwrite: replace existing .s7dcl or .s7res files. Defaults to false; existing items are skipped")] bool overwrite = false,
            [Description("resultLimit: maximum compact item summaries returned after the bulk operation. Defaults to 50 and is capped at 200")] int resultLimit = PageRequest.DefaultLimit)
        {
            var startTime = DateTime.Now;
            var progressToken = context.Params?.ProgressToken;
            
            try
            {
                var resolvedExportPath = ResolveOutputDirectory(exportPath);
                var boundedResultLimit = new PageRequest
                {
                    Limit = resultLimit
                }.GetBoundedLimit();
                if (!string.IsNullOrWhiteSpace(regexName))
                {
                    _ = BoundedRegex.Create(regexName, RegexOptions.IgnoreCase);
                }
                if (Engineering.TiaMajorVersion < 20)
                {
                    throw new McpException("ExportBlocksAsDocuments requires TIA Portal V20 or newer", McpErrorCode.InvalidParams);
                }
                // First, get the list of blocks to determine total count
                Logger?.LogInformation($"Starting export of blocks as documents from '{softwarePath}' to '{resolvedExportPath}'");
                
                var allBlocks = await Task.Run(() => Portal.GetBlocks(softwarePath, regexName));
                var totalBlocks = allBlocks?.Count ?? 0;

                if (totalBlocks == 0)
                {
                    if (progressToken != null)
                    {
                        await server.SendNotificationAsync("notifications/progress", new
                        {
                            Progress = 0,
                            Total = 0,
                            Message = "No blocks found to export as documents",
                            progressToken
                        });
                    }
                    
                    return new ResponseExportBlocksAsDocuments
                    {
                        Message = $"No blocks found with regex '{regexName}' in '{softwarePath}'",
                        Items = new List<ResponseBlockInfo>(),
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true,
                            ["totalBlocks"] = 0,
                            ["exportedBlocks"] = 0,
                            ["duration"] = (DateTime.Now - startTime).TotalSeconds
                        }
                    };
                }

                // Send initial progress notification
                if (progressToken != null)
                {
                    await server.SendNotificationAsync("notifications/progress", new
                    {
                        Progress = 0,
                        Total = totalBlocks,
                        Message = $"Starting export of {totalBlocks} blocks as documents...",
                        progressToken
                    });
                }

                // Export blocks as documents asynchronously
                var exportedBlocks = await Task.Run(() => Portal.ExportBlocksAsDocuments(
                    softwarePath,
                    resolvedExportPath,
                    regexName,
                    preservePath,
                    overwrite));
                
                // Send progress update after export completion
                if (exportedBlocks != null && progressToken != null)
                {
                    var exportedCount = exportedBlocks.Count();
                    await server.SendNotificationAsync("notifications/progress", new
                    {
                        Progress = exportedCount,
                        Total = totalBlocks,
                        Message = $"Exported {exportedCount} of {totalBlocks} blocks as documents",
                        progressToken
                    });
                }

                if (exportedBlocks != null)
                {
                    var responseList = new List<ResponseBlockInfo>();
                    var processedCount = 0;
                    
                    foreach (var block in exportedBlocks)
                    {
                        if (block != null)
                        {
                            if (responseList.Count < boundedResultLimit)
                            {
                                responseList.Add(new ResponseBlockInfo
                                {
                                    Path = Portal.GetBlockPath(block),
                                    Name = block.Name,
                                    TypeName = block.GetType().Name,
                                    ProgrammingLanguage = Enum.GetName(
                                        typeof(ProgrammingLanguage),
                                        block.ProgrammingLanguage),
                                    IsConsistent = block.IsConsistent
                                });
                            }
                        }
                        processedCount++;
                    }

                    // Send final progress notification
                    if (progressToken != null)
                    {
                        await server.SendNotificationAsync("notifications/progress", new
                        {
                            Progress = processedCount,
                            Total = totalBlocks,
                            Message = $"Document export completed: {processedCount} blocks exported successfully",
                            progressToken
                        });
                    }

                    var duration = (DateTime.Now - startTime).TotalSeconds;
                    var notExportedCount = Math.Max(
                        0,
                        totalBlocks - processedCount);
                    Logger?.LogInformation($"Document export completed: {processedCount} blocks exported in {duration:F2} seconds");

                    return new ResponseExportBlocksAsDocuments
                    {
                        Message = $"Document export completed: {processedCount} blocks with regex '{regexName}' exported from '{softwarePath}' to '{resolvedExportPath}'",
                        Items = responseList,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = notExportedCount == 0,
                            ["totalBlocks"] = totalBlocks,
                            ["exportedBlocks"] = processedCount,
                            ["notExportedOrInconsistentBlocks"] = notExportedCount,
                            ["returnedItems"] = responseList.Count,
                            ["itemsTruncated"] = processedCount > responseList.Count,
                            ["duration"] = duration
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed exporting documents to '{exportPath}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (
                ex is ArgumentException ||
                ex is RegexMatchTimeoutException)
            {
                throw new McpException(
                    $"Invalid block filter '{regexName}': {ex.Message}",
                    ex,
                    McpErrorCode.InvalidParams);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                // Send error progress notification if we have a progress token
                if (progressToken != null)
                {
                    try
                    {
                        await server.SendNotificationAsync("notifications/progress", new
                        {
                            Progress = 0,
                            Total = 0,
                            Message = $"Document export failed: {ex.Message}",
                            Error = true,
                            progressToken
                        });
                    }
                    catch
                    {
                        // Ignore notification errors during error handling
                    }
                }
                
                Logger?.LogError(ex, $"Failed exporting documents to '{exportPath}'");
                throw new McpException($"Unexpected error exporting documents to '{exportPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "ImportFromDocuments"), Description("V20 only. Import one SIMATIC SD document pair into PLC software using an explicit conflict policy. This mutates the project.")]
        public static ResponseImportFromDocuments ImportFromDocuments(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: optional path within the PLC program where the block should be placed (empty for root)")] string groupPath,
            [Description("importPath: directory containing the document files (.s7dcl/.s7res)")] string importPath,
            [Description("fileNameWithoutExtension: name of the block file without extension") ] string fileNameWithoutExtension,
            [Description("importOption: conflict policy. Defaults to None; use Override only when the user explicitly asks to replace existing content")] string importOption = "None")
        {
            try
            {
                if (Engineering.TiaMajorVersion < 20)
                {
                    throw new McpException("ImportFromDocuments requires TIA Portal V20 or newer", McpErrorCode.InvalidParams);
                }

                var option = ParseImportDocumentOption(importOption);

                // Pre-check .s7res for missing en-US tags
                var warnings = new JsonArray();
                try
                {
                    var missingIds = GetResMissingEnUsIds(importPath, fileNameWithoutExtension);
                    if (missingIds != null && missingIds.Count > 0)
                    {
                        Logger?.LogWarning($".s7res for '{fileNameWithoutExtension}' missing en-US tags for {missingIds.Count} items: {string.Join(", ", missingIds)}");
                        warnings.Add(new JsonObject
                        {
                            ["name"] = fileNameWithoutExtension,
                            ["missingEnUsIds"] = new JsonArray(missingIds.Select(id => (JsonNode)id).ToArray())
                        });
                    }
                }
                catch (Exception ex)
                {
                    Logger?.LogDebug(ex, "Failed to evaluate .s7res warnings");
                }

                var ok = Portal.ImportFromDocuments(softwarePath, groupPath, importPath, fileNameWithoutExtension, option);
                if (ok)
                {
                    return new ResponseImportFromDocuments
                    {
                        Message = $"Imported '{fileNameWithoutExtension}' from '{importPath}'",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true,
                            ["warnings"] = warnings
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed importing '{fileNameWithoutExtension}' from '{importPath}'", McpErrorCode.InternalError);
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error importing from documents: {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        [McpServerTool(Name = "ImportBlocksFromDocuments"), Description("V20 only. Import matching SIMATIC SD documents into PLC software using an explicit conflict policy and return bounded result counts.")]
        public static async Task<ResponseImportBlocksFromDocuments> ImportBlocksFromDocuments(
            IMcpServer server,
            RequestContext<CallToolRequestParams> context,
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("groupPath: optional path within the PLC program where the blocks should be placed (empty for root)")] string groupPath,
            [Description("importPath: directory containing the document files (.s7dcl/.s7res)")] string importPath,
            [Description("regexName: optional regular expression applied to block filenames; maximum 256 characters and one-second match timeout")] string regexName = "",
            [Description("importOption: conflict policy. Defaults to None; use Override only when the user explicitly asks to replace existing content")] string importOption = "None",
            [Description("resultLimit: maximum compact item summaries returned after the bulk operation. Defaults to 50 and is capped at 200")] int resultLimit = PageRequest.DefaultLimit)
        {
            var startTime = DateTime.Now;
            var progressToken = context.Params?.ProgressToken;

            try
            {
                var boundedResultLimit = new PageRequest
                {
                    Limit = resultLimit
                }.GetBoundedLimit();
                var filterRegex = string.IsNullOrWhiteSpace(regexName)
                    ? null
                    : BoundedRegex.Create(regexName, RegexOptions.IgnoreCase);

                if (Engineering.TiaMajorVersion < 20)
                {
                    throw new McpException("ImportBlocksFromDocuments requires TIA Portal V20 or newer", McpErrorCode.InvalidParams);
                }

                // Determine total by scanning .s7dcl files matching regex
                int total = 0;
                var scanWarnings = new JsonArray();
                try
                {
                    if (Directory.Exists(importPath))
                    {
                        var files = Directory.GetFiles(importPath, "*.s7dcl", SearchOption.TopDirectoryOnly);
                        foreach (var f in files)
                        {
                            var name = Path.GetFileNameWithoutExtension(f);
                            if (filterRegex != null && !filterRegex.IsMatch(name))
                                continue;
                            total++;

                            try
                            {
                                var missingIds = GetResMissingEnUsIds(importPath, name);
                                if (missingIds != null && missingIds.Count > 0)
                                {
                                    scanWarnings.Add(new JsonObject
                                    {
                                        ["name"] = name,
                                        ["missingEnUsIds"] = new JsonArray(missingIds.Select(id => (JsonNode)id).ToArray())
                                    });
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                    throw;
                }
                catch
                {
                    // Ignore non-filter pre-scan errors; the import reports its own result.
                }

                if (progressToken != null)
                {
                    await server.SendNotificationAsync("notifications/progress", new
                    {
                        Progress = 0,
                        Total = total,
                        Message = total > 0 ? $"Starting import of {total} blocks from documents..." : "Scanning import directory...",
                        progressToken
                    });
                }

                var option = ParseImportDocumentOption(importOption);
                var imported = await Task.Run(() => Portal.ImportBlocksFromDocuments(softwarePath, groupPath, importPath, regexName, option));

                var responseList = new List<ResponseBlockInfo>();
                int processed = 0;
                if (imported != null)
                {
                    foreach (var block in imported)
                    {
                        if (block != null)
                        {
                            if (responseList.Count < boundedResultLimit)
                            {
                                responseList.Add(new ResponseBlockInfo
                                {
                                    Path = Portal.GetBlockPath(block),
                                    Name = block.Name,
                                    TypeName = block.GetType().Name,
                                    ProgrammingLanguage = Enum.GetName(
                                        typeof(ProgrammingLanguage),
                                        block.ProgrammingLanguage),
                                    IsConsistent = block.IsConsistent
                                });
                            }
                        }
                        processed++;
                    }
                }

                if (progressToken != null)
                {
                    await server.SendNotificationAsync("notifications/progress", new
                    {
                        Progress = processed,
                        Total = total,
                        Message = $"Document import completed: {processed} blocks imported successfully",
                        progressToken
                    });
                }

                var duration = (DateTime.Now - startTime).TotalSeconds;
                var notImportedCount = Math.Max(0, total - processed);
                Logger?.LogInformation($"Document import completed: {processed} blocks imported in {duration:F2} seconds");

                return new ResponseImportBlocksFromDocuments
                {
                    Message = $"Document import completed: {processed} blocks imported from '{importPath}'",
                    Items = responseList,
                    Meta = new JsonObject
                    {
                        ["timestamp"] = DateTime.Now,
                        ["success"] = notImportedCount == 0,
                        ["totalBlocks"] = total,
                        ["importedBlocks"] = processed,
                        ["notImportedBlocks"] = notImportedCount,
                        ["returnedItems"] = responseList.Count,
                        ["itemsTruncated"] = processed > responseList.Count,
                        ["duration"] = duration,
                        ["warnings"] = scanWarnings
                    }
                };
            }
            catch (Exception ex) when (
                ex is ArgumentException ||
                ex is RegexMatchTimeoutException)
            {
                throw new McpException(
                    $"Invalid document filter '{regexName}': {ex.Message}",
                    ex,
                    McpErrorCode.InvalidParams);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                if (progressToken != null)
                {
                    try
                    {
                        await server.SendNotificationAsync("notifications/progress", new
                        {
                            Progress = 0,
                            Total = 0,
                            Message = $"Document import failed: {ex.Message}",
                            Error = true,
                            progressToken
                        });
                    }
                    catch { }
                }

                Logger?.LogError(ex, $"Failed importing documents from '{importPath}'");
                throw new McpException($"Unexpected error importing documents from '{importPath}': {ex.Message}", ex, McpErrorCode.InternalError);
            }
        }

        private static ImportDocumentOptions ParseImportDocumentOption(string option)
        {
            if (string.IsNullOrWhiteSpace(option)) return ImportDocumentOptions.None;

            var normalized = option.Trim();

            // Primary: accept exact enum names (case-insensitive)
            if (Enum.TryParse<ImportDocumentOptions>(normalized, ignoreCase: true, out var parsed))
            {
                return parsed;
            }

            // Aliases and common misspellings
            switch (normalized.ToLowerInvariant())
            {
                case "override": return ImportDocumentOptions.Override;
                case "none": return ImportDocumentOptions.None;
                case "skipinactiveculture":
                case "skipinactivecultures":
                case "skipinactive":
                case "skipinactivecult":
                    return ImportDocumentOptions.SkipInactiveCultures;
                case "activeinactiveculture":
                case "activateinactivecultures":
                case "activeinactivecultures":
                case "activateinactive":
                    return ImportDocumentOptions.ActivateInactiveCultures;
                default:
                    throw new McpException($"Invalid importOption '{option}'. Allowed: None, Override, SkipInactiveCultures, ActivateInactiveCultures", McpErrorCode.InvalidParams);
            }
        }

        private static List<string> GetResMissingEnUsIds(string directory, string baseName)
        {
            var resPath = Path.Combine(directory, baseName + ".s7res");
            var missing = new List<string>();
            if (!File.Exists(resPath))
            {
                return missing;
            }
            var xdoc = XDocument.Load(resPath);
            XNamespace ns = xdoc.Root?.Name.Namespace ?? XNamespace.None;
            foreach (var comment in xdoc.Descendants(ns + "Comment"))
            {
                var hasEnUs = comment.Elements(ns + "MultiLanguageText")
                                     .Any(e => string.Equals((string?)e.Attribute("Lang"), "en-US", StringComparison.OrdinalIgnoreCase));
                if (!hasEnUs)
                {
                    var id = (string?)comment.Attribute("Id") ?? "";
                    missing.Add(id);
                }
            }
            return missing;
        }
#endif

        private static McpException MapPortalException(
            string operation,
            PortalException exception)
        {
            var errorCode =
                exception.Code == PortalErrorCode.NotFound ||
                exception.Code == PortalErrorCode.InvalidParams ||
                exception.Code == PortalErrorCode.InvalidState
                    ? McpErrorCode.InvalidParams
                    : McpErrorCode.InternalError;

            return new McpException(
                $"{operation}: {exception.Message}",
                exception,
                errorCode);
        }

        #endregion
    }
}

