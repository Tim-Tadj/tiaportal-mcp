using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.Hmi;
#if TIA_MCP_V19 || TIA_MCP_V20
using Siemens.Engineering.HmiUnified;
#endif
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.Multiuser;
using Siemens.Engineering.Safety;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using TiaMcpServer.Security;

namespace TiaMcpServer.Siemens
{
    public class Portal
    {
        // closing parantheses for regex characters ommitted, because they are not relevant for regex detection
        private TiaPortal? _portal;
        private ProjectBase? _project;
        private LocalSession? _session;
        private readonly ILogger<Portal>? _logger;
        private readonly object _connectionSync = new object();

        #region ctor

        public Portal(ILogger<Portal>? logger = null)
        {
            _logger = logger;
        }

        #endregion

        #region helper for mcp server

        public bool ProjectIsValid
        {
            get
            {
                if (_project == null)
                {
                    return false;
                }

                // Check if the project is a valid Project instance
                if ((_session == null) && (_project is Project))
                {
                    return true;
                }

                // If it's a MultiuserProject, we can also check its validity
                if ((_session != null) && (_project is MultiuserProject))
                {
                    return true;
                }

                return false;
            }
        }

        public bool IsLocalSession
        {
            get
            {
                return _session != null;
            }
        }

        public bool IsLocalProject
        {
            get
            {
                return _session == null;
            }
        }

        #endregion

        #region helper for unit tests

        public static bool IsLocalSessionFile(string sessionPath)
        {
            // Check if the path ends with '.als\d+' using regex
            var regex = BoundedRegex.Create(@"\.als\d+$", RegexOptions.IgnoreCase);
            return regex.IsMatch(sessionPath);
        }

        public static bool IsLocalProjectFile(string projectPath)
        {
            // Check if the path ends with '.ap\d+' using regex
            var regex = BoundedRegex.Create(@"\.ap\d+$", RegexOptions.IgnoreCase);
            return regex.IsMatch(projectPath);
        }

        public void Dispose()
        {
            lock (_connectionSync)
            {
                DisposeLocked();
            }
        }

        private void DisposeLocked()
        {
            var portal = _portal;
            _project = null;
            _session = null;
            _portal = null;
            try
            {
                portal?.Dispose();
            }
            catch (Exception exception)
            {
                _logger?.LogWarning(
                    exception,
                    "Could not detach the MCP worker from TIA Portal during disposal.");
            }
        }

        #endregion

        #region portal

        public bool ConnectPortal(bool allowLaunch = true)
        {
            lock (_connectionSync)
            {
                return ConnectPortalLocked(allowLaunch);
            }
        }

        private bool ConnectPortalLocked(bool allowLaunch)
        {
            _logger?.LogInformation("Connecting to TIA Portal...");

            if (_portal != null && IsPortalConnectionHealthy())
            {
                return true;
            }

            try
            {
                _project = null;
                _session = null;
                _portal = null;

                // connect to running TIA Portal
                var processes = TiaPortal.GetProcesses().ToList();
                if (processes.Count > 1)
                {
                    _logger?.LogWarning(
                        "Multiple TIA Portal processes are running. Refusing to attach without explicit process selection.");
                    return false;
                }

                if (processes.Count == 1)
                {
                    _portal = processes[0].Attach();

                    // check for existing local sessions
                    if (_portal.LocalSessions.Any())
                    {
                        _session = _portal.LocalSessions.First();
                        _project = _session.Project;
                    }
                    // checks for existing projects
                    else if (_portal.Projects.Any())
                    {
                        _project = _portal.Projects.First();
                    }

                    return true;
                }

                if (!allowLaunch)
                {
                    _logger?.LogWarning(
                        "No running TIA Portal process is available and the active access profile does not permit launching one.");
                    return false;
                }

                // start new TIA Portal
                ProfileAccessPolicy.DemandWrite("launch TIA Portal");
                _portal = new TiaPortal(TiaPortalMode.WithUserInterface);

                return true;
            }
            catch (Exception exception)
            {
                _logger?.LogError(exception, "Could not connect to TIA Portal.");
                DisposeLocked();
                return false;
            }
        }

        public bool IsConnected()
        {
            lock (_connectionSync)
            {
                return _portal != null;
            }
        }

        public bool DisconnectPortal()
        {
            lock (_connectionSync)
            {
                return DisconnectPortalLocked();
            }
        }

        private bool DisconnectPortalLocked()
        {
            ProfileAccessPolicy.DemandWrite("disconnect TIA Portal");
            _logger?.LogInformation("Disconnecting from TIA Portal...");

            try
            {
                _project = null;
                _session = null;

                _portal?.Dispose();
                _portal = null;

                return true;
            }
            catch (Exception)
            {
                // Handle exception if needed, e.g., log it
            }

            return false;
        }

        #endregion

        #region status

        public State GetState()
        {
            _logger?.LogInformation("Getting TIA Portal state...");
            if (_portal != null)
            {
                // check for existing local sessions
                if (_portal.LocalSessions.Any())
                {
                    _session = _portal.LocalSessions.First();
                    _project = _session.Project;
                }
                // checks for existing projects
                else if (_portal.Projects.Any())
                {
                    _project = _portal.Projects.First();
                }
            }

            return new State
            {
                IsConnected = IsConnected(),
                Project = _project != null ? _project.Name : "-",
                Session = _session != null ? _session.Project.Name : "-"
            };
        }

        #endregion

        #region project

        public List<ProjectBase> GetProjects()
        {
            _logger?.LogInformation("Getting open projects...");

            if (_portal == null)
            {
                _logger?.LogWarning("No TIA Portal instance available.");

                return [];
            }

            var projects = new List<ProjectBase>();

            if (_portal.Projects != null)
            {
                foreach (var project in _portal.Projects)
                {
                    projects.Add(project);
                }
            }

            return projects;
        }

        public bool OpenProject(string projectPath)
        {
            ProfileAccessPolicy.DemandWrite("open project");
            _logger?.LogInformation($"Opening project: {projectPath}");

            if (IsPortalNull())
            {
                return false;
            }

            try
            {
                var projects = GetProjects();
                var resolvedProjectPath = Path.GetFullPath(projectPath);
                var openProject = projects.FirstOrDefault(
                    project => PathsEqual(project.Path?.ToString(), resolvedProjectPath));

                if (openProject != null)
                {
                    _project = openProject;

                    return _project != null;
                }
                else
                {
                    if (_session != null)
                    {
                        _session.Close();
                        _session = null;
                        _project = null;
                    }
                    else if (_project != null)
                    {
                        (_project as Project)?.Close();
                        _project = null;
                    }

                    // Exact-version files are opened without an implicit upgrade.
                    _project = _portal?.Projects.Open(new FileInfo(resolvedProjectPath));

                    return _project != null;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        public object? GetProjectInfo()
        {
            _logger?.LogInformation("Getting project info...");

            if (IsPortalNull())
            {
                return null;
            }

            if (IsProjectNull())
            {
                return null;
            }

            var project = _project!;

            var info = new
            {
                Name = project.Name,
                Path = project.Path,
                Type = project.GetType().Name,
                IsMultiuserProject = project is MultiuserProject,
                IsLocalSession = _session != null,
                IsLocalProject = _session == null
            };

            return info;
        }

        public bool SaveProject()
        {
            ProfileAccessPolicy.DemandWrite("save project");
            _logger?.LogInformation("Saving project...");

            if (IsProjectNull())
            {
                return false;
            }

            (_project as Project)?.Save();

            return true;
        }

        public bool SaveAsProject(string path)
        {
            ProfileAccessPolicy.DemandWrite("save project as");
            _logger?.LogInformation($"Saving project as: {path}");

            if (IsProjectNull())
            {
                return false;
            }

            var di = new DirectoryInfo(path);

            (_project as Project)?.SaveAs(di);

            return true;
        }

        public bool CloseProject()
        {
            ProfileAccessPolicy.DemandWrite("close project");
            _logger?.LogInformation("Closing project...");

            if (IsProjectNull())
            {
                return false;
            }

            (_project as Project)?.Close();
            _project = null;

            return true;
        }

        #endregion

        #region session

        public List<ProjectBase> GetSessions()
        {
            _logger?.LogInformation("Getting open local sessions...");

            if (IsPortalNull())
            {
                return [];
            }

            var sessions = new List<ProjectBase>();

            if (_portal?.LocalSessions != null)
            {
                foreach (var session in _portal.LocalSessions)
                {
                    sessions.Add(session.Project as ProjectBase);
                }
            }

            return sessions;
        }

        public bool OpenSession(string localSessionPath)
        {
            ProfileAccessPolicy.DemandWrite("open local session");
            _logger?.LogInformation($"Opening session: {localSessionPath}");

            if (IsPortalNull())
            {
                return false;
            }

            try
            {
                var resolvedSessionPath = Path.GetFullPath(localSessionPath);
                var openSession = _portal?.LocalSessions.FirstOrDefault(
                    session => PathsEqual(
                        session.Project.Path?.ToString(),
                        resolvedSessionPath));

                if (openSession != null)
                {
                    _session = openSession;
                    _project = _session.Project;
                    return _project != null;
                }
                else
                {
                    if (_session != null)
                    {
                        _project = null;
                        _session.Close();
                        _session = null;
                    }
                    else if (_project != null)
                    {
                        (_project as Project)?.Close();
                        _project = null;
                    }

                    _session = _portal?.LocalSessions.Open(
                        new FileInfo(resolvedSessionPath));
                    if (_session != null)
                    {
                        // Correctly cast MultiuserProject to Project  
                        _project = _session.Project;
                        return _project != null;
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }

            return false;
        }

        public bool SaveSession()
        {
            ProfileAccessPolicy.DemandWrite("save local session");
            _logger?.LogInformation("Saving session...");

            if (IsSessionNull())
            {
                return false;
            }

            // Save session
            _session?.Save();

            return true;
        }

        public bool CloseSession()
        {
            ProfileAccessPolicy.DemandWrite("close local session");
            _logger?.LogInformation("Closing session...");

            if (IsSessionNull())
            {
                return false;
            }

            _project = null;
            _session?.Close();
            _session = null;

            return true;
        }

        #endregion

        #region devices

        public string GetProjectTree()
        {
            _logger?.LogInformation("Getting project tree...");

            if (IsProjectNull())
            {
                return string.Empty;
            }

            StringBuilder sb = new();

            sb.AppendLine($"{_project?.Name}");

            var ancestorStates = new List<bool>();
            var sections = new List<Action>();
            
            if (_project?.Devices != null && _project.Devices.Count > 0)
            {
                sections.Add(() => GetProjectTreeDevices(sb, _project.Devices, ancestorStates));
            }
            
            if (_project?.DeviceGroups != null && _project.DeviceGroups.Count > 0)
            {
                sections.Add(() => GetProjectTreeGroups(sb, _project.DeviceGroups, ancestorStates));
            }
            
            if (_project?.UngroupedDevicesGroup != null)
            {
                sections.Add(() => GetProjectTreeUngroupedDeviceGroup(sb, _project.UngroupedDevicesGroup, ancestorStates));
            }
            
            for (int i = 0; i < sections.Count; i++)
            {
                var isLastSection = i == sections.Count - 1;
                if (i == 0)
                {
                    sections[i]();
                }
                else
                {
                    sections[i]();
                }
            }

            return sb.ToString();
        }

        

        public List<Device> GetDevices(string regexName = "")
        {
            _logger?.LogInformation("Getting devices...");

            if (IsProjectNull())
            {
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    "No project is open in TIA Portal.");
            }

            var list = new List<Device>();
            var regex = string.IsNullOrWhiteSpace(regexName)
                ? null
                : BoundedRegex.Create(regexName, RegexOptions.IgnoreCase);

            if (_project?.Devices != null)
            {
                foreach (Device device in _project.Devices)
                {
                    if (regex == null || regex.IsMatch(device.Name))
                    {
                        list.Add(device);
                    }
                }

                foreach (var group in _project.DeviceGroups)
                {
                    GetDevicesRecursive(group, list, regex);
                }

                //foreach (var group in _project.UngroupedDevicesGroup)
                //{
                //    GetDevicesRecursive(_project.UngroupedDevicesGroup, list, regexName);
                //}
            }

            return list;
        }

        public Device? GetDevice(string devicePath)
        {
            _logger?.LogInformation($"Getting device by path: {devicePath}");

            if (IsProjectNull())
            {
                return null;
            }

            // Retrieve the device by its path
            return GetDeviceByPath(devicePath);
        }

        public string GetDevicePath(Device device)
        {
            if (device == null)
            {
                return string.Empty;
            }

            if (_project?.Devices != null &&
                _project.Devices.Any(candidate => ReferenceEquals(candidate, device)))
            {
                return device.Name;
            }

            if (_project?.DeviceGroups != null)
            {
                foreach (var group in _project.DeviceGroups)
                {
                    var path = FindDevicePath(group, device, group.Name);
                    if (path != null)
                    {
                        return path;
                    }
                }
            }

            return device.Name;
        }

        public DeviceItem? GetDeviceItem(string deviceItemPath)
        {
            _logger?.LogInformation($"Getting device item by path: {deviceItemPath}");

            if (IsProjectNull())
            {
                return null;
            }

            // Retrieve the device by its path
            return GetDeviceItemByPath(deviceItemPath);

        }

        #endregion

        #region software

        public PlcSoftware? GetPlcSoftware(string softwarePath)
        {
            _logger?.LogInformation($"Getting software by path: {softwarePath}");

            if (IsProjectNull())
            {
                return null;
            }

            var softwareContainer = GetSoftwareContainer(softwarePath);

            if (softwareContainer?.Software is PlcSoftware plcSoftware)
            {
                return plcSoftware;
            }

            return null;
        }

        public CompilerResult? CompileSoftware(string softwarePath, string password = "")
        {
            ProfileAccessPolicy.DemandWrite("compile PLC software");
            _logger?.LogInformation($"Compiling software by path: {softwarePath}");

            if (IsProjectNull())
            {
                return null; // "Error, no project";
            }

            var softwareContainer = GetSoftwareContainer(softwarePath);

            if (!string.IsNullOrEmpty(password))
            {
                var deviceItem = softwareContainer?.Parent as DeviceItem;

                var admin = deviceItem?.GetService<SafetyAdministration>();
                if (admin != null)
                {
                    if (!admin.IsLoggedOnToSafetyOfflineProgram)
                    {
                        SecureString secString = new NetworkCredential("", password).SecurePassword;
                        try
                        {
                            admin.LoginToSafetyOfflineProgram(secString);
                        }
                        catch (Exception)
                        {
                            return null; // "Error, login to safety offline program failed";
                        }
                    }
                }
            }

            if (softwareContainer?.Software is PlcSoftware plcSoftware)
            {
                try
                {
                    ICompilable compileService = plcSoftware.GetService<ICompilable>();

                    CompilerResult result = compileService.Compile();

                    return result;
                }
                catch (Exception)
                {
                    return null; // "Error, compiling failed";
                }
            }

            return null; // "Error";
        }

        #endregion

        #region blocks/types

        public PlcBlock? GetBlock(string softwarePath, string blockPath)
        {
            _logger?.LogInformation($"Getting block by path: {blockPath}");

            if (IsProjectNull())
            {
                return null;
            }

            var softwareContainer = GetSoftwareContainer(softwarePath);
            if (softwareContainer?.Software is PlcSoftware plcSoftware)
            {
                var blockGroup = plcSoftware?.BlockGroup;

                if (blockGroup != null)
                {
                    var path = blockPath.Contains("/") ? blockPath.Substring(0, blockPath.LastIndexOf("/")) : string.Empty;
                    var regexName = blockPath.Contains("/") ? blockPath.Substring(blockPath.LastIndexOf("/") + 1) : blockPath;

                    PlcBlock? block = null;

                    var group = GetPlcBlockGroupByPath(softwarePath, path);
                    if (group != null)
                    {
                        block = group.Blocks.FirstOrDefault(
                            b => b.Name.Equals(regexName, StringComparison.OrdinalIgnoreCase)) as PlcBlock;

                        return block;
                    }
                }
            }

            return null;
        }

        public PlcType? GetType(string softwarePath, string typePath)
        {
            _logger?.LogInformation($"Getting type by path: {typePath}");

            if (IsProjectNull())
            {
                return null;
            }

            var softwareContainer = GetSoftwareContainer(softwarePath);
            if (softwareContainer?.Software is PlcSoftware plcSoftware)
            {
                var typeGroup = plcSoftware?.TypeGroup;

                if (typeGroup != null)
                {
                    var path = typePath.Contains("/") ? typePath.Substring(0, typePath.LastIndexOf("/")) : string.Empty;
                    var regexName = typePath.Contains("/") ? typePath.Substring(typePath.LastIndexOf("/") + 1) : typePath;

                    PlcType? type = null;

                    var group = GetPlcTypeGroupByPath(softwarePath, path);
                    if (group != null)
                    {
                        type = group.Types.FirstOrDefault(
                            t => t.Name.Equals(regexName, StringComparison.OrdinalIgnoreCase)) as PlcType;

                        return type;
                    }
                }
            }

            return null;
        }

        public string GetBlockPath(PlcBlock block)
        {
            if (block == null)
            {
                return string.Empty;
            }

            if (block.Parent is PlcBlockGroup parentGroup)
            {
                var groupPath = GetPlcBlockGroupPath(parentGroup);
                return string.IsNullOrEmpty(groupPath) ? block.Name : $"{groupPath}/{block.Name}";
            }

            return block.Name;
        }

        public string GetTypePath(PlcType type)
        {
            if (type == null)
            {
                return string.Empty;
            }

            if (type.Parent is PlcTypeGroup parentGroup)
            {
                var groupPath = GetPlcTypeGroupPath(parentGroup);
                return string.IsNullOrEmpty(groupPath) ? type.Name : $"{groupPath}/{type.Name}";
            }

            return type.Name;
        }

        public List<PlcBlock> GetBlocks(string softwarePath, string regexName = "")
        {
            _logger?.LogInformation("Getting blocks...");

            if (IsProjectNull())
            {
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    "No project is open in TIA Portal.");
            }

            var list = new List<PlcBlock>();
            var regex = string.IsNullOrWhiteSpace(regexName)
                ? null
                : BoundedRegex.Create(regexName, RegexOptions.IgnoreCase);

            try
            {
                var softwareContainer = GetSoftwareContainer(softwarePath);
                if (softwareContainer?.Software is PlcSoftware plcSoftware)
                {
                    var group = plcSoftware?.BlockGroup;

                    if (group != null)
                    {
                        GetBlocksRecursive(group, list, regex);
                    }

                    return list;
                }

                throw new PortalException(
                    PortalErrorCode.NotFound,
                    $"PLC software was not found at '{softwarePath}'.");
            }
            catch (RegexMatchTimeoutException)
            {
                throw;
            }
            catch (PortalException)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger?.LogError(
                    exception,
                    "Failed to traverse PLC blocks at {SoftwarePath}",
                    softwarePath);
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"Failed to traverse PLC blocks at '{softwarePath}'.",
                    null,
                    exception);
            }
        }

        public PlcBlockGroup? GetBlockRootGroup(string softwarePath)
        {
            _logger?.LogInformation("Getting block root group...");

            if (IsProjectNull())
            {
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    "No project is open in TIA Portal.");
            }

            try
            {
                var softwareContainer = GetSoftwareContainer(softwarePath);
                if (softwareContainer?.Software is PlcSoftware plcSoftware)
                {
                    return plcSoftware.BlockGroup;
                }

                throw new PortalException(
                    PortalErrorCode.NotFound,
                    $"PLC software was not found at '{softwarePath}'.");
            }
            catch (PortalException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting block root group");
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"Failed to retrieve the block root group at '{softwarePath}'.",
                    null,
                    ex);
            }
        }

        public List<PlcType> GetTypes(string softwarePath, string regexName = "")
        {
            _logger?.LogInformation("Getting types...");

            if (IsProjectNull())
            {
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    "No project is open in TIA Portal.");
            }

            var list = new List<PlcType>();
            var regex = string.IsNullOrWhiteSpace(regexName)
                ? null
                : BoundedRegex.Create(regexName, RegexOptions.IgnoreCase);

            try
            {
                var softwareContainer = GetSoftwareContainer(softwarePath);
                if (softwareContainer?.Software is PlcSoftware plcSoftware)
                {
                    var group = plcSoftware?.TypeGroup;

                    if (group != null)
                    {
                        GetTypesRecursive(group, list, regex);
                    }

                    return list;
                }

                throw new PortalException(
                    PortalErrorCode.NotFound,
                    $"PLC software was not found at '{softwarePath}'.");
            }
            catch (RegexMatchTimeoutException)
            {
                throw;
            }
            catch (PortalException)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger?.LogError(
                    exception,
                    "Failed to traverse PLC data types at {SoftwarePath}",
                    softwarePath);
                throw new PortalException(
                    PortalErrorCode.InvalidState,
                    $"Failed to traverse PLC data types at '{softwarePath}'.",
                    null,
                    exception);
            }
        }

        public PlcBlock? ExportBlock(
            string softwarePath,
            string blockPath,
            string exportPath,
            bool preservePath = false,
            bool overwrite = false)
        {
            ProfileAccessPolicy.DemandWrite("export PLC block");
            _logger?.LogInformation($"Exporting block by path: {blockPath}");

            try
            {
                if (IsProjectNull())
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "No project is open in TIA Portal");
                }

                var block = GetBlock(softwarePath, blockPath);

                if (block == null)
                {
                    throw new PortalException(PortalErrorCode.NotFound, "Block not found");
                }

                if (preservePath)
                {
                    var groupPath = "";
                    if (block.Parent is PlcBlockGroup parentGroup)
                    {
                        groupPath = GetPlcBlockGroupPath(parentGroup);
                    }

                    exportPath = Path.Combine(exportPath, groupPath.Replace('/', '\\'), $"{block.Name}.xml");
                }
                else
                {
                    exportPath = Path.Combine(exportPath, $"{block.Name}.xml");
                }
                exportPath = OutputPathPolicy.ResolveDirectory(exportPath);

                // TIA Portal never exports inconsistent blocks
                if (!block.IsConsistent)
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "Block is inconsistent; TIA Portal does not export inconsistent blocks.");
                }

                ExportFileSafely(
                    exportPath,
                    overwrite,
                    file => block.Export(file, ExportOptions.None));

                return block;
            }
            catch (Exception ex)
            {
                //If the exception is already a PortalException, use it; otherwise, wrap it in a new PortalException
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.ExportFailed, "Export failed", null, ex);

                pex.Data["softwarePath"] = softwarePath;
                pex.Data["blockPath"] = blockPath;
                pex.Data["exportPath"] = exportPath;

                _logger?.LogError(pex, "ExportBlock failed for {SoftwarePath} {BlockPath} -> {ExportPath}", softwarePath, blockPath, exportPath);
                throw pex;
            }
        }

        public PlcType? ExportType(
            string softwarePath,
            string typePath,
            string exportPath,
            bool preservePath = false,
            bool overwrite = false)
        {
            ProfileAccessPolicy.DemandWrite("export PLC data type");
            _logger?.LogInformation($"Exporting type by path: {typePath}");

            try
            {
                if (IsProjectNull())
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "No project is open in TIA Portal");
                }

                var type = GetType(softwarePath, typePath);

                if (type == null)
                {
                    throw new PortalException(PortalErrorCode.NotFound, "Type not found");
                }

                // TIA Portal never exports inconsistent types
                if (!type.IsConsistent)
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "Type is inconsistent; TIA Portal does not export inconsistent types.");
                }

                if (preservePath)
                {
                    var groupPath = "";
                    if (type.Parent is PlcTypeGroup parentGroup)
                    {
                        groupPath = GetPlcTypeGroupPath(parentGroup);
                    }

                    exportPath = Path.Combine(exportPath, groupPath.Replace('/', '\\'), $"{type.Name}.xml");
                }
                else
                {
                    exportPath = Path.Combine(exportPath, $"{type.Name}.xml");
                }
                exportPath = OutputPathPolicy.ResolveDirectory(exportPath);

                ExportFileSafely(
                    exportPath,
                    overwrite,
                    file => type.Export(file, ExportOptions.None));

                return type;
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.ExportFailed, "Export failed", null, ex);

                if (!pex.Data.Contains("softwarePath")) pex.Data["softwarePath"] = softwarePath;
                if (!pex.Data.Contains("typePath")) pex.Data["typePath"] = typePath;
                if (!pex.Data.Contains("exportPath")) pex.Data["exportPath"] = exportPath;

                _logger?.LogError(pex, "ExportType failed for {SoftwarePath} {TypePath} -> {ExportPath}", softwarePath, typePath, exportPath);
                throw pex;
            }
        }

        public bool ImportBlock(
            string softwarePath,
            string groupPath,
            string importPath,
            bool overwrite = false)
        {
            ProfileAccessPolicy.DemandWrite("import PLC block");
            _logger?.LogInformation($"Importing block from path: {importPath}");

            if (IsProjectNull())
            {
                return false;
            }

            var softwareContainer = GetSoftwareContainer(softwarePath);
            if (softwareContainer?.Software is PlcSoftware plcSoftware)
            {
                var blockGroup = plcSoftware?.BlockGroup;

                if (blockGroup != null)
                {

                    var group = GetPlcBlockGroupByPath(softwarePath, groupPath);
                    if (group == null)
                    {
                        return false;
                    }

                    try
                    {
                        // Correct the argument type by using FileInfo instead of FileStream  
                        var fileInfo = new FileInfo(importPath);
                        if (fileInfo.Exists)
                        {
                            var importOption = overwrite
                                ? ImportOptions.Override
                                : ImportOptions.None;
                            var list = group.Blocks.Import(fileInfo, importOption);
                            if (list != null && list.Count > 0)
                            {
                                return true;
                            }
                        }

                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }
            }

            return false;
        }

        public bool ImportType(
            string softwarePath,
            string groupPath,
            string importPath,
            bool overwrite = false)
        {
            ProfileAccessPolicy.DemandWrite("import PLC data type");
            _logger?.LogInformation($"Importing type from path: {importPath}");

            var success = false;

            if (IsProjectNull())
            {
                return success;
            }

            var softwareContainer = GetSoftwareContainer(softwarePath);
            if (softwareContainer?.Software is PlcSoftware plcSoftware)
            {
                var typeGroup = plcSoftware?.TypeGroup;

                if (typeGroup != null)
                {
                    var group = GetPlcTypeGroupByPath(softwarePath, groupPath);
                    if (group == null)
                    {
                        return false;
                    }

                    try
                    {
                        // Correct the argument type by using FileInfo instead of FileStream  
                        var fileInfo = new FileInfo(importPath);
                        if (fileInfo.Exists)
                        {
                            var importOption = overwrite
                                ? ImportOptions.Override
                                : ImportOptions.None;
                            var list = group.Types.Import(fileInfo, importOption);
                            if (list != null && list.Count > 0)
                            {
                                return true;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }
            }

            return success;
        }

        public IEnumerable<PlcBlock>? ExportBlocks(
            string softwarePath,
            string exportPath,
            string regexName = "",
            bool preservePath = false,
            bool overwrite = false)
        {
            ProfileAccessPolicy.DemandWrite("bulk export PLC blocks");
            _logger?.LogInformation("Exporting blocks...");

            if (IsProjectNull())
            {
                return null;
            }

            var exportList = new List<PlcBlock>();
            var failures = new List<string>();
            
            PlcBlock[] list;

            try
            {
                list = GetBlocks(softwarePath, regexName).ToArray();
            }
            catch (RegexMatchTimeoutException)
            {
                throw;
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to retrieve block list for {SoftwarePath}", softwarePath);
                return exportList;
            }

            for (int k = 0; k < list.Count(); k++)
            {
                var block = list[k];

                _logger?.LogDebug($"- Exporting block {k}/{list.Count()} : {block.Name}");

                string path;
                if (preservePath)
                {
                    var groupPath = "";
                    if (block.Parent is PlcBlockGroup parentGroup)
                    {
                        groupPath = GetPlcBlockGroupPath(parentGroup);
                    }
                    path = Path.Combine(exportPath, groupPath.Replace('/', '\\'), $"{block.Name}.xml");
                }
                else
                {
                    path = Path.Combine(exportPath, $"{block.Name}.xml");
                }
                path = OutputPathPolicy.ResolveDirectory(path);

                try
                {
                    if (!block.IsConsistent)
                    {
                        _logger?.LogWarning("Skipping inconsistent block {Name}", block.Name);

                        continue;
                    }

                    try
                    {
                        ExportFileSafely(
                            path,
                            overwrite,
                            file => block.Export(file, ExportOptions.None));
                    }
                    catch (LicenseNotFoundException licEx)
                    {
                        failures.Add($"{block.Name}: license not found ({licEx.Message})");
                        _logger?.LogError(licEx, "License issue exporting {Block}", block.Name);

                        continue;
                    }
                    catch (EngineeringTargetInvocationException engEx)
                    {
                        failures.Add($"{block.Name}: target invocation failed ({engEx.Message})");
                        _logger?.LogError(engEx, "TargetInvocationException exporting {Block}", block.Name);

                        continue;
                    }
                    catch (Exception ex)
                    {
                        failures.Add($"{block.Name}: export failed ({ex.Message})");
                        _logger?.LogError(ex, "Export failed for {Block}", block.Name);

                        continue;
                    }

                    exportList.Add(block);
                }
                catch (Exception ex)
                {
                    // Catch only truly unexpected wrapper-level errors
                    failures.Add($"{block.Name}: unexpected exception ({ex.Message})");
                    _logger?.LogError(ex, "Unexpected error at block {Block}", block.Name);
                    // continue with next block
                }
            }

            if (failures.Count > 0)
            {
                _logger?.LogWarning($"ExportBlocks completed with {failures.Count} failures out of {list.Count()}. First failure: {failures[0]}");
                // Optionally: _logger?.LogDebug("All failures: {Failures}", string.Join("; ", failures));
            }
            else
            {
                _logger?.LogInformation($"ExportBlocks completed successfully. Exported {exportList.Count} blocks.");
            }

            return exportList;
        }

        public IEnumerable<PlcType>? ExportTypes(
            string softwarePath,
            string exportPath,
            string regexName = "",
            bool preservePath = false,
            bool overwrite = false)
        {
            ProfileAccessPolicy.DemandWrite("bulk export PLC data types");
            _logger?.LogInformation("Exporting types...");

            if (IsProjectNull())
            {
                return null;
            }

            var exportList = new List<PlcType>();
            var failures = new List<string>();

            PlcType[] list;

            try
            {
                list = GetTypes(softwarePath, regexName).ToArray();
            }
            catch (RegexMatchTimeoutException)
            {
                throw;
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to retrieve type list for {SoftwarePath}", softwarePath);
                return exportList;
            }

            for (int i = 0; i < list.Count(); i++)
            {
                var type = list[i];

                _logger?.LogDebug("- Exporting type {Index}/{Total} : {Name}", i, list.Count(), type.Name);

                string path;
                if (preservePath)
                {
                    var groupPath = "";
                    if (type.Parent is PlcTypeGroup parentGroup)
                    {
                        groupPath = GetPlcTypeGroupPath(parentGroup);
                    }
                    path = Path.Combine(exportPath, groupPath.Replace('/', '\\'), $"{type.Name}.xml");
                }
                else
                {
                    path = Path.Combine(exportPath, $"{type.Name}.xml");
                }
                path = OutputPathPolicy.ResolveDirectory(path);

                try
                {
                    if (!type.IsConsistent)
                    {
                        _logger?.LogWarning("Skipping inconsistent type {Name}", type.Name);
                        continue;
                    }

                    try
                    {
                        ExportFileSafely(
                            path,
                            overwrite,
                            file => type.Export(file, ExportOptions.None));
                    }
                    catch (Exception ex)
                    {
                        failures.Add($"{type.Name}: export failed ({ex.Message})");
                        _logger?.LogError(ex, "Export failed for type {Type}", type.Name);
                        continue;
                    }

                    exportList.Add(type);
                }
                catch (Exception ex)
                {
                    failures.Add($"{type.Name}: unexpected exception ({ex.Message})");
                    _logger?.LogError(ex, "Unexpected error at type {Type}", type.Name);
                }
            }

            if (failures.Count > 0)
            {
                _logger?.LogWarning($"ExportTypes completed with {failures.Count} failures out of {list.Count()}. First failure: {failures[0]}");
            }
            else
            {
                _logger?.LogInformation($"ExportTypes completed successfully. Exported {exportList.Count} types.");
            }

            return exportList;
        }
        

#if TIA_MCP_V20
        public bool ExportAsDocuments(
            string softwarePath,
            string blockPath,
            string exportPath,
            bool preservePath = false,
            bool overwrite = false)
        {
            ProfileAccessPolicy.DemandWrite("export PLC block as SIMATIC SD documents");
            _logger?.LogInformation($"Exporting block as documents by path: {blockPath}");
            var success = false;
            try
            {
                if (IsProjectNull())
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "No project is open in TIA Portal");
                }

                if (Engineering.TiaMajorVersion < 20)
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "ExportAsDocuments is available only in the TIA Portal V20 worker for this alpha");
                }

                
                var softwareContainer = GetSoftwareContainer(softwarePath);
                if (softwareContainer?.Software is PlcSoftware plcSoftware)
                {
                    if (plcSoftware != null)
                    {
                        // Export code blocks as documents
                        // https://docs.tia.siemens.cloud/r/en-us/v20/creating-and-managing-blocks/exporting-and-importing-blocks-in-simatic-sd-format-s7-1200-s7-1500/exporting-and-importing-blocks-in-simatic-sd-format-s7-1200-s7-1500

                        var block = GetBlock(softwarePath, blockPath);
                        if (block == null)
                        {
                            throw new PortalException(
                                PortalErrorCode.NotFound,
                                $"Block was not found at '{blockPath}'.");
                        }

                        var blockName = block.Name;
                        var groupPath = block.Parent is PlcBlockGroup parentGroup
                            ? GetPlcBlockGroupPath(parentGroup)
                            : string.Empty;

                        //group?.Blocks.ForEach(b => Console.WriteLine($"Block: {b.Name}, Type: {b.GetType().Name}"));

                        // Join and validate exportPath and groupPath before creating directories.
                        if (preservePath && !string.IsNullOrEmpty(groupPath))
                        {
                            exportPath = Path.Combine(exportPath, groupPath);
                        }
                        exportPath = OutputPathPolicy.ResolveDirectory(exportPath);

                        try
                        {
                            var result = ExportDocumentsSafely(
                                block,
                                exportPath,
                                blockName,
                                overwrite);

                            if (result != null && result.State == DocumentResultState.Success)
                            {
                                success = true;
                            }
                        }
                        catch (EngineeringNotSupportedException ex)
                        {
                            // The export or import of blocks with mixed programming languages is not possible
                            throw new PortalException(PortalErrorCode.ExportFailed, $"EngineeringNotSupportedException at block '{blockName}'. {ex.Message}", null, ex);
                        }
                        catch (PortalException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            throw new PortalException(PortalErrorCode.ExportFailed, $"Exception at block '{blockName}'. {ex.Message}", null, ex);
                        }

                    }

                }


            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.ExportFailed, "Export failed", null, ex);

                pex.Data["softwarePath"] = softwarePath;
                pex.Data["blockPath"] = blockPath;
                pex.Data["exportPath"] = exportPath;

                _logger?.LogError(pex, "ExportAsDocuments failed for {SoftwarePath} {BlockPath} -> {ExportPath}", softwarePath, blockPath, exportPath);
                throw pex;
            }
            return success;
        }

        // TIA portal crashes when exporting blocks as documents, :-(
        public IEnumerable<PlcBlock>? ExportBlocksAsDocuments(
            string softwarePath,
            string exportPath,
            string regexName = "",
            bool preservePath = false,
            bool overwrite = false)
        {
            ProfileAccessPolicy.DemandWrite(
                "bulk export PLC blocks as SIMATIC SD documents");
            _logger?.LogInformation("Exporting blocks as documents...");

            if (IsProjectNull())
            {
                return null;
            }

            if (Engineering.TiaMajorVersion < 20)
            {
                _logger?.LogWarning("ExportBlocksAsDocuments is available only in the TIA Portal V20 worker for this alpha");
                return null;
            }

            var exportList = new List<PlcBlock>();
            var failures = new List<string>();

            PlcBlock[] list;
            try
            {
                list = GetBlocks(softwarePath, regexName).ToArray();
            }
            catch (RegexMatchTimeoutException)
            {
                throw;
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to retrieve block list for {softwarePath}");
                return exportList;
            }

            for (int i = 0; i < list.Count(); i++)
            {
                var block = list[i];

                _logger?.LogDebug($"- Exporting block as document {i}/{list.Count()} : {block.Name}");

                // Skip inconsistent blocks (TIA generally won’t export them)
                if (!block.IsConsistent)
                {
                    _logger?.LogWarning($"Skipping inconsistent block {block.Name}");
                    continue;
                }

                // Determine base directory (preserve group path if requested)
                string targetDir = exportPath;
                if (preservePath && block.Parent is PlcBlockGroup parentGroup)
                {
                    var groupPath = GetPlcBlockGroupPath(parentGroup);
                    if (!string.IsNullOrWhiteSpace(groupPath))
                    {
                        targetDir = Path.Combine(exportPath, groupPath.Replace('/', '\\'));
                    }
                }
                targetDir = OutputPathPolicy.ResolveDirectory(targetDir);

                try
                {
                    DocumentExportResult? result = null;
                    try
                    {
                        result = ExportDocumentsSafely(
                            block,
                            targetDir,
                            block.Name,
                            overwrite);
                    }
                    catch (EngineeringNotSupportedException ex)
                    {
                        failures.Add($"{block.Name}: not supported ({ex.Message})");
                        _logger?.LogWarning(ex, $"EngineeringNotSupported exporting {block.Name}");
                        continue;
                    }
                    catch (LicenseNotFoundException ex)
                    {
                        failures.Add($"{block.Name}: license not found ({ex.Message})");
                        _logger?.LogError(ex, $"License issue exporting {block.Name}");
                        continue;
                    }
                    catch (Exception ex)
                    {
                        failures.Add($"{block.Name}: export threw ({ex.Message})");
                        _logger?.LogError(ex, $"ExportAsDocuments failed for {block.Name}");
                        continue;
                    }

                    if (result == null)
                    {
                        failures.Add($"{block.Name}: no result returned");
                        continue;
                    }

                    if (result.State == DocumentResultState.Success)
                    {
                        exportList.Add(block);
                    }
                    else
                    {
                        failures.Add($"{block.Name}: result state {result.State}");
                    }
                }
                catch (Exception ex)
                {
                    failures.Add($"{block.Name}: unexpected exception ({ex.Message})");
                    _logger?.LogError(ex, $"Unexpected wrapper error for {block.Name}");
                }
            }

            if (failures.Count > 0)
            {
                _logger?.LogWarning($"ExportBlocksAsDocuments completed with {failures.Count} failures out of {list.Count()}. First failure: {failures[0]}");
                // Optional verbose list:
                // _logger?.LogDebug("All failures: {Failures}", string.Join("; ", failures));
            }
            else
            {
                _logger?.LogInformation($"ExportBlocksAsDocuments completed successfully. Exported {exportList.Count} blocks.");
            }

            return exportList;
        }

        public bool ImportFromDocuments(string softwarePath, string groupPath, string importPath, string fileNameWithoutExtension, ImportDocumentOptions option)
        {
            ProfileAccessPolicy.DemandWrite(
                "import PLC block from SIMATIC SD documents");
            _logger?.LogInformation($"Importing block from documents: {fileNameWithoutExtension} in {importPath}");

            if (IsProjectNull())
            {
                return false;
            }

            if (Engineering.TiaMajorVersion < 20)
            {
                _logger?.LogWarning("ImportFromDocuments is available only in the TIA Portal V20 worker for this alpha");
                return false;
            }

            try
            {
                var softwareContainer = GetSoftwareContainer(softwarePath);
                if (softwareContainer?.Software is PlcSoftware plcSoftware)
                {
                    var group = GetPlcBlockGroupByPath(softwarePath, groupPath);
                    var dir = new DirectoryInfo(importPath);
                    if (!dir.Exists)
                    {
                        _logger?.LogWarning($"Import directory does not exist: {importPath}");
                        return false;
                    }

                    DocumentImportResult? result = null;
                    try
                    {
                        result = (group != null)
                            ? group.Blocks.ImportFromDocuments(dir, fileNameWithoutExtension, option)
                            : plcSoftware.BlockGroup.Blocks.ImportFromDocuments(dir, fileNameWithoutExtension, option);
                    }
                    catch (EngineeringNotSupportedException ex)
                    {
                        throw new PortalException(PortalErrorCode.ExportFailed, $"EngineeringNotSupportedException at file '{fileNameWithoutExtension}'. {ex.Message}", null, ex);
                    }

                    if (result != null && result.State == DocumentResultState.Success)
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error importing block from documents");
            }
            return false;
        }

        public IEnumerable<PlcBlock>? ImportBlocksFromDocuments(string softwarePath, string groupPath, string importPath, string regexName, ImportDocumentOptions option, bool preservePath = false)
        {
            ProfileAccessPolicy.DemandWrite(
                "bulk import PLC blocks from SIMATIC SD documents");
            _logger?.LogInformation($"Importing blocks from documents in {importPath} with regex '{regexName}'");

            if (IsProjectNull())
            {
                return null;
            }

            if (Engineering.TiaMajorVersion < 20)
            {
                _logger?.LogWarning("ImportBlocksFromDocuments is available only in the TIA Portal V20 worker for this alpha");
                return null;
            }

            var imported = new List<PlcBlock>();

            try
            {
                var softwareContainer = GetSoftwareContainer(softwarePath);
                if (softwareContainer?.Software is PlcSoftware plcSoftware)
                {
                    var group = GetPlcBlockGroupByPath(softwarePath, groupPath);
                    var dir = new DirectoryInfo(importPath);
                    if (!dir.Exists)
                    {
                        _logger?.LogWarning($"Import directory does not exist: {importPath}");
                        return imported;
                    }

                    var rx = string.IsNullOrWhiteSpace(regexName)
                        ? null
                        : BoundedRegex.Create(regexName, RegexOptions.IgnoreCase);

                    // Consider .s7dcl as the primary index; .s7res is optional supplemental
                    var files = dir.GetFiles("*.s7dcl", SearchOption.TopDirectoryOnly);
                    foreach (var file in files)
                    {
                        var name = Path.GetFileNameWithoutExtension(file.Name);
                        if (rx != null && !rx.IsMatch(name))
                        {
                            continue;
                        }

                        try
                        {
                            var result = (group != null)
                                ? group.Blocks.ImportFromDocuments(dir, name, option)
                                : plcSoftware.BlockGroup.Blocks.ImportFromDocuments(dir, name, option);

                            if (result != null && result.State == DocumentResultState.Success && result.ImportedPlcBlocks != null)
                            {
                                foreach (var blk in result.ImportedPlcBlocks)
                                {
                                    if (blk != null)
                                    {
                                        imported.Add(blk);
                                    }
                                }
                            }
                        }
                        catch (EngineeringNotSupportedException)
                        {
                            // mixed languages etc.; skip but continue batch
                        }
                        catch (Exception)
                        {
                            // skip problematic item, continue
                        }
                    }
                }
            }
            catch (RegexMatchTimeoutException)
            {
                throw;
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error importing blocks from documents");
            }

            return imported;
        }
#endif

        #endregion

        #region private helper

        private void ExportFileSafely(
            string targetPath,
            bool overwrite,
            Action<FileInfo> exportAction)
        {
            var resolvedTargetPath = OutputPathPolicy.ResolveDirectory(targetPath);
            var targetDirectory = Path.GetDirectoryName(resolvedTargetPath);
            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    "The export target must have a parent directory.");
            }

            Directory.CreateDirectory(targetDirectory);
            resolvedTargetPath = OutputPathPolicy.ResolveDirectory(resolvedTargetPath);

            if (File.Exists(resolvedTargetPath) && !overwrite)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    "The output file already exists. Set overwrite to true to replace it.");
            }

            var stagedFileName =
                $".{Path.GetFileNameWithoutExtension(resolvedTargetPath)}." +
                $"{Guid.NewGuid():N}.tmp{Path.GetExtension(resolvedTargetPath)}";
            var stagedPath = OutputPathPolicy.ResolveDirectory(
                Path.Combine(targetDirectory, stagedFileName));

            try
            {
                exportAction(new FileInfo(stagedPath));
                if (!File.Exists(stagedPath))
                {
                    throw new PortalException(
                        PortalErrorCode.ExportFailed,
                        "TIA Portal reported export completion but did not create the staged file.");
                }

                CommitStagedFile(stagedPath, resolvedTargetPath, overwrite);
            }
            finally
            {
                TryDeleteStagingFile(stagedPath);
            }
        }

#if TIA_MCP_V20
        private DocumentExportResult? ExportDocumentsSafely(
            PlcBlock block,
            string targetDirectory,
            string blockName,
            bool overwrite)
        {
            var resolvedTargetDirectory =
                OutputPathPolicy.ResolveDirectory(targetDirectory);
            Directory.CreateDirectory(resolvedTargetDirectory);
            resolvedTargetDirectory =
                OutputPathPolicy.ResolveDirectory(resolvedTargetDirectory);

            var targetDclPath = OutputPathPolicy.ResolveDirectory(
                Path.Combine(resolvedTargetDirectory, $"{blockName}.s7dcl"));
            var targetResPath = OutputPathPolicy.ResolveDirectory(
                Path.Combine(resolvedTargetDirectory, $"{blockName}.s7res"));

            if (!overwrite &&
                (File.Exists(targetDclPath) || File.Exists(targetResPath)))
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    "A document output already exists. Set overwrite to true to replace it.");
            }

            var stagingDirectory = OutputPathPolicy.ResolveDirectory(
                Path.Combine(
                    resolvedTargetDirectory,
                    $".tia-mcp-export-{Guid.NewGuid():N}"));
            Directory.CreateDirectory(stagingDirectory);
            stagingDirectory = OutputPathPolicy.ResolveDirectory(stagingDirectory);
            var preserveStagingDirectory = false;

            try
            {
                var result = block.ExportAsDocuments(
                    new DirectoryInfo(stagingDirectory),
                    blockName);
                if (result == null || result.State != DocumentResultState.Success)
                {
                    return result;
                }

                var stagedDclPath = OutputPathPolicy.ResolveDirectory(
                    Path.Combine(stagingDirectory, $"{blockName}.s7dcl"));
                var stagedResPath = OutputPathPolicy.ResolveDirectory(
                    Path.Combine(stagingDirectory, $"{blockName}.s7res"));
                if (!File.Exists(stagedDclPath))
                {
                    throw new PortalException(
                        PortalErrorCode.ExportFailed,
                        "TIA Portal reported document export success but did not create the .s7dcl file.");
                }

                CommitDocumentPair(
                    stagedDclPath,
                    File.Exists(stagedResPath) ? stagedResPath : null,
                    targetDclPath,
                    targetResPath,
                    overwrite,
                    ref preserveStagingDirectory);

                return result;
            }
            finally
            {
                if (preserveStagingDirectory)
                {
                    _logger?.LogError(
                        "Document export recovery files were preserved at {StagingDirectory}",
                        stagingDirectory);
                }
                else
                {
                    TryDeleteStagingDirectory(stagingDirectory);
                }
            }
        }
#endif

        private static void CommitStagedFile(
            string stagedPath,
            string targetPath,
            bool overwrite)
        {
            stagedPath = OutputPathPolicy.ResolveDirectory(stagedPath);
            targetPath = OutputPathPolicy.ResolveDirectory(targetPath);

            if (File.Exists(targetPath))
            {
                if (!overwrite)
                {
                    throw new PortalException(
                        PortalErrorCode.InvalidParams,
                        "The output file appeared while the export was running. " +
                        "Set overwrite to true to replace it.");
                }

                File.Replace(stagedPath, targetPath, null);
                return;
            }

            File.Move(stagedPath, targetPath);
        }

#if TIA_MCP_V20
        private static void CommitDocumentPair(
            string stagedDclPath,
            string? stagedResPath,
            string targetDclPath,
            string targetResPath,
            bool overwrite,
            ref bool preserveRecoveryFiles)
        {
            stagedDclPath = OutputPathPolicy.ResolveDirectory(stagedDclPath);
            stagedResPath = stagedResPath == null
                ? null
                : OutputPathPolicy.ResolveDirectory(stagedResPath);
            targetDclPath = OutputPathPolicy.ResolveDirectory(targetDclPath);
            targetResPath = OutputPathPolicy.ResolveDirectory(targetResPath);

            var stagedDclFingerprint = CaptureFileFingerprint(stagedDclPath)
                ?? throw new PortalException(
                    PortalErrorCode.ExportFailed,
                    "The staged .s7dcl document disappeared before it could be committed.");
            var stagedResFingerprint = stagedResPath == null
                ? null
                : CaptureFileFingerprint(stagedResPath)
                    ?? throw new PortalException(
                        PortalErrorCode.ExportFailed,
                        "The staged .s7res document disappeared before it could be committed.");
            var originalDclFingerprint = CaptureFileFingerprint(targetDclPath);
            var originalResFingerprint = CaptureFileFingerprint(targetResPath);
            var backupDirectory = Path.GetDirectoryName(stagedDclPath)
                ?? throw new PortalException(
                    PortalErrorCode.ExportFailed,
                    "The staged document export has no parent directory.");
            var transactionId = Guid.NewGuid().ToString("N");
            var backupDclPath = OutputPathPolicy.ResolveDirectory(
                Path.Combine(backupDirectory, $".previous-{transactionId}.s7dcl"));
            var backupResPath = OutputPathPolicy.ResolveDirectory(
                Path.Combine(backupDirectory, $".previous-{transactionId}.s7res"));

            EnsureFileFingerprint(
                targetDclPath,
                originalDclFingerprint,
                "The .s7dcl output changed while the export was being prepared.");
            EnsureFileFingerprint(
                targetResPath,
                originalResFingerprint,
                "The .s7res output changed while the export was being prepared.");

            var dclCommitted = false;
            var resCommitted = false;
            var resDeleted = false;
            var previousDclExisted = false;
            var previousResExisted = false;

            try
            {
                previousDclExisted = CommitStagedDocumentFile(
                    stagedDclPath,
                    targetDclPath,
                    backupDclPath,
                    overwrite);
                dclCommitted = true;
                if (stagedResPath != null)
                {
                    previousResExisted = CommitStagedDocumentFile(
                        stagedResPath,
                        targetResPath,
                        backupResPath,
                        overwrite);
                    resCommitted = true;
                }
                else if (overwrite && File.Exists(targetResPath))
                {
                    File.Move(targetResPath, backupResPath);
                    previousResExisted = true;
                    resDeleted = true;
                }

                EnsureFileFingerprint(
                    targetDclPath,
                    stagedDclFingerprint,
                    "The committed .s7dcl output changed during the document transaction.");
                EnsureFileFingerprint(
                    targetResPath,
                    stagedResFingerprint,
                    "The committed .s7res output changed during the document transaction.");
            }
            catch (Exception commitException)
            {
                var rollbackExceptions = new List<Exception>();
                if (dclCommitted)
                {
                    try
                    {
                        RestoreDocumentFile(
                            targetDclPath,
                            backupDclPath,
                            previousDclExisted,
                            stagedDclFingerprint);
                    }
                    catch (Exception exception)
                    {
                        rollbackExceptions.Add(exception);
                    }
                }
                if (resCommitted || resDeleted)
                {
                    try
                    {
                        RestoreDocumentFile(
                            targetResPath,
                            backupResPath,
                            previousResExisted,
                            resCommitted ? stagedResFingerprint : null);
                    }
                    catch (Exception exception)
                    {
                        rollbackExceptions.Add(exception);
                    }
                }

                if (rollbackExceptions.Count > 0)
                {
                    preserveRecoveryFiles = true;
                    var transactionExceptions = new List<Exception>
                    {
                        commitException
                    };
                    transactionExceptions.AddRange(rollbackExceptions);
                    throw new PortalException(
                        PortalErrorCode.ExportFailed,
                        $"Document export failed and the previous document pair could not be fully restored. " +
                        $"Recovery files are preserved in '{backupDirectory}'.",
                        null,
                        new AggregateException(transactionExceptions));
                }

                throw;
            }
        }

        private static bool CommitStagedDocumentFile(
            string stagedPath,
            string targetPath,
            string backupPath,
            bool overwrite)
        {
            stagedPath = OutputPathPolicy.ResolveDirectory(stagedPath);
            targetPath = OutputPathPolicy.ResolveDirectory(targetPath);
            backupPath = OutputPathPolicy.ResolveDirectory(backupPath);

            if (!File.Exists(targetPath))
            {
                File.Move(stagedPath, targetPath);
                return false;
            }

            if (!overwrite)
            {
                throw new PortalException(
                    PortalErrorCode.InvalidParams,
                    "A document output appeared while the export was running. " +
                    "Set overwrite to true to replace it.");
            }

            File.Replace(stagedPath, targetPath, backupPath);
            return true;
        }

        private static void RestoreDocumentFile(
            string targetPath,
            string backupPath,
            bool originalExisted,
            string? committedFingerprint)
        {
            targetPath = OutputPathPolicy.ResolveDirectory(targetPath);
            backupPath = OutputPathPolicy.ResolveDirectory(backupPath);
            EnsureFileFingerprint(
                targetPath,
                committedFingerprint,
                $"The document output '{targetPath}' changed before rollback.");

            if (!originalExisted)
            {
                if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                }

                return;
            }

            var backupFingerprint = CaptureFileFingerprint(backupPath)
                ?? throw new IOException(
                    $"The document backup '{backupPath}' is unavailable.");
            File.Copy(backupPath, targetPath, true);
            EnsureFileFingerprint(
                targetPath,
                backupFingerprint,
                $"The previous document output '{targetPath}' was not restored correctly.");
        }

        private static string? CaptureFileFingerprint(string path)
        {
            path = OutputPathPolicy.ResolveDirectory(path);
            if (!File.Exists(path))
            {
                return null;
            }

            using (var algorithm = SHA256.Create())
            using (var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read))
            {
                return Convert.ToBase64String(algorithm.ComputeHash(stream));
            }
        }

        private static void EnsureFileFingerprint(
            string path,
            string? expectedFingerprint,
            string errorMessage)
        {
            var actualFingerprint = CaptureFileFingerprint(path);
            if (!string.Equals(
                actualFingerprint,
                expectedFingerprint,
                StringComparison.Ordinal))
            {
                throw new IOException(errorMessage);
            }
        }
#endif

        private bool IsPortalConnectionHealthy()
        {
            try
            {
                _portal?.Projects.Count();
                _portal?.LocalSessions.Count();
                return _portal != null;
            }
            catch (Exception exception)
            {
                _logger?.LogWarning(
                    exception,
                    "The existing TIA Portal connection is no longer usable. Reconnecting.");
                DisposeLocked();
                return false;
            }
        }

        private static bool PathsEqual(string? firstPath, string secondPath)
        {
            if (string.IsNullOrWhiteSpace(firstPath))
            {
                return false;
            }

            return string.Equals(
                Path.GetFullPath(firstPath),
                Path.GetFullPath(secondPath),
                StringComparison.OrdinalIgnoreCase);
        }

        private void TryDeleteStagingFile(string stagedPath)
        {
            try
            {
                if (File.Exists(stagedPath))
                {
                    File.Delete(stagedPath);
                }
            }
            catch (Exception exception)
            {
                _logger?.LogWarning(
                    exception,
                    "Could not remove staged export file {StagedPath}",
                    stagedPath);
            }
        }

        private void TryDeleteStagingDirectory(string stagingDirectory)
        {
            try
            {
                if (Directory.Exists(stagingDirectory))
                {
                    Directory.Delete(stagingDirectory, true);
                }
            }
            catch (Exception exception)
            {
                _logger?.LogWarning(
                    exception,
                    "Could not remove staged export directory {StagingDirectory}",
                    stagingDirectory);
            }
        }

        private bool IsPortalNull()
        {
            if (_portal == null)
            {
                _logger?.LogWarning("No TIA portal available.");

                return true;
            }

            return false;
        }

        private bool IsProjectNull()
        {
            if (_project == null)
            {
                _logger?.LogWarning("No TIA project available.");

                return true;
            }

            return false;
        }

        private bool IsSessionNull()
        {
            if (_session == null)
            {
                _logger?.LogWarning("No TIA session available.");

                return true;
            }

            return false;
        }

        #region  GetTree ...

        private string GetTreePrefix(List<bool> ancestorStates, bool isLast)
        {
            var prefix = new StringBuilder();
            
            // Build prefix based on ancestor states
            for (int i = 0; i < ancestorStates.Count; i++)
            {
                prefix.Append(ancestorStates[i] ? "    " : "│   ");
            }
            
            // Add current level connector
            prefix.Append(isLast ? "└── " : "├── ");
            return prefix.ToString();
        }

        private void GetProjectTreeDevices(StringBuilder sb, DeviceComposition devices, List<bool> ancestorStates)
        {
            if (devices.Count == 0) return;
            
            // Check if this is the last main section
            var hasOtherSections = (_project?.DeviceGroups != null && _project.DeviceGroups.Count > 0) ||
                                  (_project?.UngroupedDevicesGroup != null);
            var isLastMainSection = !hasOtherSections;
            
            sb.AppendLine($"{GetTreePrefix(ancestorStates, isLastMainSection)}Devices [Collection]");

            var deviceList = devices.ToList();
            var newAncestorStates = new List<bool>(ancestorStates) { isLastMainSection };
            
            for (int i = 0; i < deviceList.Count; i++)
            {
                var device = deviceList[i];
                var isLastDevice = i == deviceList.Count - 1;
                
                sb.AppendLine($"{GetTreePrefix(newAncestorStates, isLastDevice)}{device.Name} [Device: {device.TypeIdentifier}]");

                if (device.DeviceItems != null && device.DeviceItems.Count > 0)
                {
                    GetProjectTreeDeviceItemsRecursive(sb, device.DeviceItems, new List<bool>(newAncestorStates) { isLastDevice });
                }
            }
        }

        private void GetProjectTreeGroups(StringBuilder sb, DeviceUserGroupComposition groups, List<bool> ancestorStates)
        {
            if (groups.Count == 0) return;
            
            var isLastMainSection = _project?.UngroupedDevicesGroup == null;
            
            sb.AppendLine($"{GetTreePrefix(ancestorStates, isLastMainSection)}Groups [Collection]");

            var groupList = groups.ToList();
            var newAncestorStates = new List<bool>(ancestorStates) { isLastMainSection };
            
            for (int i = 0; i < groupList.Count; i++)
            {
                var group = groupList[i];
                var isLastGroup = i == groupList.Count - 1;
                
                sb.AppendLine($"{GetTreePrefix(newAncestorStates, isLastGroup)}{group.Name} [Group]");

                var groupAncestorStates = new List<bool>(newAncestorStates) { isLastGroup };
                
                if (group.Devices != null && group.Devices.Count > 0)
                {
                    GetProjectTreeGroupDevices(sb, group.Devices, groupAncestorStates, group.Groups != null && group.Groups.Count > 0);
                }
                
                if (group.Groups != null && group.Groups.Count > 0)
                {
                    GetProjectTreeSubGroups(sb, group.Groups, groupAncestorStates);
                }
            }
        }
        
        private void GetProjectTreeGroupDevices(StringBuilder sb, DeviceComposition devices, List<bool> ancestorStates, bool hasSubGroups)
        {
            var deviceList = devices.ToList();
            
            for (int i = 0; i < deviceList.Count; i++)
            {
                var device = deviceList[i];
                var isLastDevice = i == deviceList.Count - 1 && !hasSubGroups;
                
                sb.AppendLine($"{GetTreePrefix(ancestorStates, isLastDevice)}{device.Name} [Device]");
                
                if (device.DeviceItems != null && device.DeviceItems.Count > 0)
                {
                    GetProjectTreeDeviceItemsRecursive(sb, device.DeviceItems, new List<bool>(ancestorStates) { isLastDevice });
                }
            }
        }
        
        private void GetProjectTreeSubGroups(StringBuilder sb, DeviceUserGroupComposition groups, List<bool> ancestorStates)
        {
            var groupList = groups.ToList();
            
            for (int i = 0; i < groupList.Count; i++)
            {
                var group = groupList[i];
                var isLastGroup = i == groupList.Count - 1;
                
                sb.AppendLine($"{GetTreePrefix(ancestorStates, isLastGroup)}{group.Name} [Subgroup]");
                
                var groupAncestorStates = new List<bool>(ancestorStates) { isLastGroup };
                
                if (group.Devices != null && group.Devices.Count > 0)
                {
                    GetProjectTreeGroupDevices(sb, group.Devices, groupAncestorStates, group.Groups != null && group.Groups.Count > 0);
                }
                
                if (group.Groups != null && group.Groups.Count > 0)
                {
                    GetProjectTreeSubGroups(sb, group.Groups, groupAncestorStates);
                }
            }
        }

        private void GetProjectTreeDeviceItemsRecursive(StringBuilder sb, DeviceItemComposition deviceItems, List<bool> ancestorStates)
        {
            var deviceItemsList = deviceItems.ToList();
            
            for (int i = 0; i < deviceItemsList.Count; i++)
            {
                var deviceItem = deviceItemsList[i];
                var isLastDeviceItem = i == deviceItemsList.Count - 1;
                
                sb.AppendLine($"{GetTreePrefix(ancestorStates, isLastDeviceItem)}{deviceItem.Name} [DeviceItem]");
                
                var itemAncestorStates = new List<bool>(ancestorStates) { isLastDeviceItem };
                
                // Get software first
                GetProjectTreeDeviceItemSoftware(sb, deviceItem, itemAncestorStates);
                
                // Then get items
                if (deviceItem.Items != null && deviceItem.Items.Count > 0)
                {
                    GetProjectTreeItems(sb, deviceItem.Items, itemAncestorStates, deviceItem.DeviceItems != null && deviceItem.DeviceItems.Count > 0);
                }
                
                // Finally get sub-device items
                if (deviceItem.DeviceItems != null && deviceItem.DeviceItems.Count > 0)
                {
                    GetProjectTreeDeviceItemsRecursive(sb, deviceItem.DeviceItems, itemAncestorStates);
                }
            }
        }
        
        private void GetProjectTreeItems(StringBuilder sb, DeviceItemAssociation items, List<bool> ancestorStates, bool hasSubDeviceItems)
        {
            var itemsList = items.ToList();
            
            for (int i = 0; i < itemsList.Count; i++)
            {
                var subItem = itemsList[i];
                var isLastItem = i == itemsList.Count - 1 && !hasSubDeviceItems;
                
                sb.AppendLine($"{GetTreePrefix(ancestorStates, isLastItem)}{subItem.Name} [Hardware Component]");
            }
        }


        private void GetProjectTreeDeviceItemSoftware(StringBuilder sb, DeviceItem deviceItem, List<bool> ancestorStates)
        {
            var softwareContainer = deviceItem.GetService<SoftwareContainer>();
            var hasSoftware = false;
            
            //PLC software
            if (softwareContainer?.Software is PlcSoftware plcSoftware)
            {
                var hasOtherItems = (deviceItem.Items != null && deviceItem.Items.Count > 0) ||
                                   (deviceItem.DeviceItems != null && deviceItem.DeviceItems.Count > 0);
                sb.AppendLine($"{GetTreePrefix(ancestorStates, !hasOtherItems)}PlcSoftware: {plcSoftware.Name} [PLC Program]");
                hasSoftware = true;
            }

            //WinCC HMI software
            if (softwareContainer?.Software is HmiTarget hmiTarget)
            {
                var hasOtherItems = (deviceItem.Items != null && deviceItem.Items.Count > 0) ||
                                   (deviceItem.DeviceItems != null && deviceItem.DeviceItems.Count > 0);
                sb.AppendLine($"{GetTreePrefix(ancestorStates, !hasOtherItems && !hasSoftware)}HmiTarget: {hmiTarget.Name} [HMI Program]");
            }

            //Unified HMI software: dlls will only exist on TIA Portal V19 and newer.
#if TIA_MCP_V19 || TIA_MCP_V20
            TryGetUnifiedSoftware(sb, deviceItem, ancestorStates, softwareContainer, hasSoftware);
#endif
        }

#if TIA_MCP_V19 || TIA_MCP_V20
        private bool TryGetUnifiedSoftware(StringBuilder sb, DeviceItem deviceItem, List<bool> ancestorStates, SoftwareContainer? softwareContainer, bool hasSoftware)
        {
            if (softwareContainer?.Software is HmiSoftware hmiSoftware)
            {
                var hasOtherItems = (deviceItem.Items != null && deviceItem.Items.Count > 0) ||
                                    (deviceItem.DeviceItems != null && deviceItem.DeviceItems.Count > 0);
                sb.AppendLine($"{GetTreePrefix(ancestorStates, !hasOtherItems && !hasSoftware)}HmiSoftware: {hmiSoftware.Name} [HMI Program]");
                hasSoftware = true;
            }

            return hasSoftware;
        }
#endif

        private void GetProjectTreeUngroupedDeviceGroup(StringBuilder sb, DeviceSystemGroup ungroupedDevicesGroup, List<bool> ancestorStates)
        {
            sb.AppendLine($"{GetTreePrefix(ancestorStates, true)}UngroupedDevicesGroup: {ungroupedDevicesGroup.Name} [System Group]");

            if (ungroupedDevicesGroup.Devices != null && ungroupedDevicesGroup.Devices.Count > 0)
            {
                var deviceList = ungroupedDevicesGroup.Devices.ToList();
                var newAncestorStates = new List<bool>(ancestorStates) { true };
                
                for (int i = 0; i < deviceList.Count; i++)
                {
                    var device = deviceList[i];
                    var isLastDevice = i == deviceList.Count - 1;
                    
                    sb.AppendLine($"{GetTreePrefix(newAncestorStates, isLastDevice)}{device.Name} [{device.TypeIdentifier}]");
                }
            }
        }

        #endregion

        #region GetSoftwareTree ...

        public string GetSoftwareTree(string softwarePath)
        {
            _logger?.LogInformation("Getting software tree for path: {SoftwarePath}", softwarePath);

            if (IsProjectNull())
            {
                return string.Empty;
            }

            try
            {
                var softwareContainer = GetSoftwareContainer(softwarePath);
                if (softwareContainer?.Software is PlcSoftware plcSoftware)
                {
                    StringBuilder sb = new();
                    sb.AppendLine($"{plcSoftware.Name} [PLC Software]");
                    
                    var ancestorStates = new List<bool>();
                    var sections = new List<Action>();
                    
                    var hasBlocks = plcSoftware.BlockGroup != null;
                    var hasTypes = plcSoftware.TypeGroup != null;
                    
                    // Add blocks section
                    if (hasBlocks)
                    {
                        var blockGroup = plcSoftware.BlockGroup;
                        if (blockGroup != null)
                        {
                            sections.Add(() => GetSoftwareTreeBlockGroup(sb, blockGroup, ancestorStates, "Program blocks", !hasTypes));
                        }
                    }
                    
                    // Add types section
                    if (hasTypes)
                    {
                        var typeGroup = plcSoftware.TypeGroup;
                        if (typeGroup != null)
                        {
                            sections.Add(() => GetSoftwareTreeTypeGroup(sb, typeGroup, ancestorStates, "PLC data types", true));
                        }
                    }
                    
                    
                    // Execute sections
                    for (int i = 0; i < sections.Count; i++)
                    {
                        sections[i]();
                    }

                    return sb.ToString();
                }
                else
                {
                    return $"No PLC software found at path: {softwarePath}";
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting software tree for {SoftwarePath}", softwarePath);
                return $"Error retrieving software tree: {ex.Message}";
            }
        }
        
        private void GetSoftwareTreeBlockGroup(StringBuilder sb, PlcBlockGroup blockGroup, List<bool> ancestorStates, string groupLabel, bool isLastSection)
        {
            sb.AppendLine($"{GetTreePrefix(ancestorStates, isLastSection)}{groupLabel}"); // [Collection]
            var newAncestorStates = new List<bool>(ancestorStates) { isLastSection };
            
            // Get blocks in this group
            var blocks = blockGroup.Blocks.ToList();
            var subGroups = blockGroup.Groups.ToList();
            
            // First, add all blocks
            for (int i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                // Block is last only if it's the last block AND there are no subgroups following
                var isLastBlock = (i == blocks.Count - 1) && (subGroups.Count == 0);

                var blockTypeName = new[] { "ArrayDB", "GlobalDB", "InstanceDB" }.Contains(block.GetType().Name)
                    ? "DB"
                    : block.GetType().Name;

                sb.AppendLine($"{GetTreePrefix(newAncestorStates, isLastBlock)}{block.Name} [{blockTypeName}{block.Number}, {block.ProgrammingLanguage}]");
            }
            
            // Then, add all subgroups recursively
            for (int i = 0; i < subGroups.Count; i++)
            {
                var subGroup = subGroups[i];
                var isLastGroup = i == subGroups.Count - 1;
                
                sb.AppendLine($"{GetTreePrefix(newAncestorStates, isLastGroup)}{subGroup.Name}"); // [Block Group]

                var groupAncestorStates = new List<bool>(newAncestorStates) { isLastGroup };
                GetSoftwareTreeBlockGroupRecursive(sb, subGroup, groupAncestorStates);
            }
        }
        
        private void GetSoftwareTreeBlockGroupRecursive(StringBuilder sb, PlcBlockGroup blockGroup, List<bool> ancestorStates)
        {
            // Get blocks in this group
            var blocks = blockGroup.Blocks.ToList();
            var subGroups = blockGroup.Groups.ToList();
            
            // First, add all blocks
            for (int i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                // Block is last only if it's the last block AND there are no subgroups following
                var isLastBlock = (i == blocks.Count - 1) && (subGroups.Count == 0);

                var blockTypeName = new[] { "ArrayDB", "GlobalDB", "InstanceDB" }.Contains(block.GetType().Name)
                    ? "DB"
                    : block.GetType().Name;

                sb.AppendLine($"{GetTreePrefix(ancestorStates, isLastBlock)}{block.Name} [{blockTypeName}{block.Number}, {block.ProgrammingLanguage}]");
            }
            
            // Then, add all subgroups recursively
            for (int i = 0; i < subGroups.Count; i++)
            {
                var subGroup = subGroups[i];
                var isLastGroup = i == subGroups.Count - 1;
                
                sb.AppendLine($"{GetTreePrefix(ancestorStates, isLastGroup)}{subGroup.Name}"); // [Block Group]

                var groupAncestorStates = new List<bool>(ancestorStates) { isLastGroup };
                GetSoftwareTreeBlockGroupRecursive(sb, subGroup, groupAncestorStates);
            }
        }
        
        private void GetSoftwareTreeTypeGroup(StringBuilder sb, PlcTypeGroup typeGroup, List<bool> ancestorStates, string groupLabel, bool isLastSection)
        {
            
            sb.AppendLine($"{GetTreePrefix(ancestorStates, isLastSection)}{groupLabel}"); // [Collection]
            var newAncestorStates = new List<bool>(ancestorStates) { isLastSection };
            
            // Get types in this group
            var types = typeGroup.Types.ToList();
            var subGroups = typeGroup.Groups.ToList();
            
            // First, add all types
            for (int i = 0; i < types.Count; i++)
            {
                var type = types[i];
                // Type is last only if it's the last type AND there are no subgroups following
                var isLastType = (i == types.Count - 1) && (subGroups.Count == 0);

                var typeTypeName = type.GetType().Name;
                typeTypeName = typeTypeName=="PlcStruct" ? "UDT": typeTypeName;

                sb.AppendLine($"{GetTreePrefix(newAncestorStates, isLastType)}{type.Name} [{typeTypeName}]");
            }
            
            // Then, add all subgroups recursively
            for (int i = 0; i < subGroups.Count; i++)
            {
                var subGroup = subGroups[i];
                var isLastGroup = i == subGroups.Count - 1;
                
                sb.AppendLine($"{GetTreePrefix(newAncestorStates, isLastGroup)}{subGroup.Name}"); // [Type Group]

                var groupAncestorStates = new List<bool>(newAncestorStates) { isLastGroup };
                GetSoftwareTreeTypeGroupRecursive(sb, subGroup, groupAncestorStates);
            }
        }
        
        private void GetSoftwareTreeTypeGroupRecursive(StringBuilder sb, PlcTypeGroup typeGroup, List<bool> ancestorStates)
        {
            // Get types in this group
            var types = typeGroup.Types.ToList();
            var subGroups = typeGroup.Groups.ToList();
            
            // First, add all types
            for (int i = 0; i < types.Count; i++)
            {
                var type = types[i];
                // Type is last only if it's the last type AND there are no subgroups following
                var isLastType = (i == types.Count - 1) && (subGroups.Count == 0);

                var typeTypeName = type.GetType().Name;
                typeTypeName = typeTypeName == "PlcStruct" ? "UDT" : typeTypeName;

                sb.AppendLine($"{GetTreePrefix(ancestorStates, isLastType)}{type.Name} [{typeTypeName}]");
            }
            
            // Then, add all subgroups recursively
            for (int i = 0; i < subGroups.Count; i++)
            {
                var subGroup = subGroups[i];
                var isLastGroup = i == subGroups.Count - 1;
                
                sb.AppendLine($"{GetTreePrefix(ancestorStates, isLastGroup)}{subGroup.Name}"); // [Type Group]

                var groupAncestorStates = new List<bool>(ancestorStates) { isLastGroup };
                GetSoftwareTreeTypeGroupRecursive(sb, subGroup, groupAncestorStates);
            }
        }

        #endregion

        #region GetSoftwareContainer ...

        private SoftwareContainer? GetSoftwareContainer(string softwarePath)
        {
            if (_project == null)
            {
                return null;
            }

            string[] pathSegments = softwarePath.Split('/');
            int index = 0;

            if (index >= pathSegments.Length)
                return null;

            string segment = pathSegments[index];
            SoftwareContainer? softwareContainer = null;

            // in Devices
            if (_project.Devices != null)
            {
                softwareContainer = GetSoftwareContainerInDevices(_project.Devices, pathSegments, index);
                if (softwareContainer != null)
                {
                    return softwareContainer;
                }
            }

            // in Groups
            if (_project.DeviceGroups != null)
            {
                softwareContainer = GetSoftwareContainerInGroups(_project.DeviceGroups, pathSegments, index);
                if (softwareContainer != null)
                {
                    return softwareContainer;
                }
            }

            return null;
        }

        private SoftwareContainer? GetSoftwareContainerInDevices(DeviceComposition devices, string[] pathSegments, int index)
        {

            if (index >= pathSegments.Length)
                return null;

            string segment = pathSegments[index];
            string nextSegment = index + 1 < pathSegments.Length ? pathSegments[index + 1] : string.Empty;

            if (devices != null)
            {
                SoftwareContainer? softwareContainer = null;
                Device? device = null;
                DeviceItem? deviceItem = null;

                // a pc based plc has a Device.Name = 'PC-System_1' or something like that, which is visible in the TIA-Portal IDE
                // use segment to find device
                device = devices.FirstOrDefault(d => d.Name.Equals(segment, StringComparison.OrdinalIgnoreCase));
                if (device != null)
                {
                    // then use next segment to find device item
                    deviceItem = device.DeviceItems.FirstOrDefault(di => di.Name.Equals(nextSegment, StringComparison.OrdinalIgnoreCase));
                    // but here we use next segment to find device item
                    softwareContainer = GetSoftwareContainerInDeviceItem(deviceItem, pathSegments, index + 1);
                    if (softwareContainer != null)
                    {
                        return softwareContainer;
                    }
                }

                // a hardware plc has a Device.Name = 'S7-1500/ET200MP-Station_1' or something like that, which is not visible in the TIA-Portal IDE
                // ignored segment for Device.Name and use it for DeviceItem.Name
                deviceItem = devices
                    .SelectMany(d => d.DeviceItems)
                    .FirstOrDefault(di => di.Name.Equals(segment, StringComparison.OrdinalIgnoreCase));
                if (deviceItem != null)
                {
                    return GetSoftwareContainerInDeviceItem(deviceItem, pathSegments, index);
                }

            }

            return null;
        }

        private SoftwareContainer? GetSoftwareContainerInGroups(DeviceUserGroupComposition groups, string[] pathSegments, int index)
        {
            if (index >= pathSegments.Length)
                return null;

            string segment = pathSegments[index];
            SoftwareContainer? softwareContainer = null;

            if (groups != null)
            {
                var group = groups.FirstOrDefault(g => g.Name.Equals(segment));
                if (group != null)
                {
                    // when segment matched
                    softwareContainer = GetSoftwareContainerInDevices(group.Devices, pathSegments, index + 1);
                    if (softwareContainer != null)
                    {
                        return softwareContainer;
                    }

                    return GetSoftwareContainerInGroups(group.Groups, pathSegments, index + 1);
                }
            }

            return null;
        }

        private SoftwareContainer? GetSoftwareContainerInDeviceItem(DeviceItem deviceItem, string[] pathSegments, int index)
        {
            if (deviceItem != null)
            {
                // when segment matched
                if (index == pathSegments.Length - 1)
                {
                    // get from DeviceItem
                    var softwareContainer = deviceItem.GetService<SoftwareContainer>();
                    if (softwareContainer != null)
                    {
                        return softwareContainer;
                    }
                }
            }

            return null;
        }

        #endregion

        #region Get...ByPath

        private Device? GetDeviceByPath(string devicePath)
        {
            if (_project?.Devices == null || string.IsNullOrWhiteSpace(devicePath))
                return null;

            var pathSegments = devicePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (pathSegments.Length == 0)
            {
                return null;
            }

            // Try top-level device first
            if (pathSegments.Length == 1)
            {
                return _project.Devices.FirstOrDefault(d => d.Name.Equals(pathSegments[0], StringComparison.OrdinalIgnoreCase));
            }

            // Traverse device groups
            DeviceUserGroupComposition? groups = _project.DeviceGroups;
            DeviceUserGroup? group = groups?.FirstOrDefault(g => g.Name.Equals(pathSegments[0], StringComparison.OrdinalIgnoreCase));

            if (group == null)
            {
                return null;
            }

            for (int i = 1; i < pathSegments.Length; i++)
            {
                // Try to find device in current group
                var device = group.Devices.FirstOrDefault(d => d.Name.Equals(pathSegments[i], StringComparison.OrdinalIgnoreCase));
                if (device != null)
                {
                    return device;
                }

                // Try to find subgroup
                group = group.Groups.FirstOrDefault(g => g.Name.Equals(pathSegments[i], StringComparison.OrdinalIgnoreCase));
                if (group == null)
                {
                    break;
                }
            }

            return null;
        }

        private static string? FindDevicePath(
            DeviceUserGroup group,
            Device target,
            string groupPath)
        {
            foreach (var device in group.Devices)
            {
                if (ReferenceEquals(device, target))
                {
                    return $"{groupPath}/{device.Name}";
                }
            }

            foreach (var subgroup in group.Groups)
            {
                var path = FindDevicePath(
                    subgroup,
                    target,
                    $"{groupPath}/{subgroup.Name}");
                if (path != null)
                {
                    return path;
                }
            }

            return null;
        }

        private DeviceItem? GetDeviceItemByPath(string deviceItemPath)
        {
            if (_project == null || _project.Devices == null)
            {
                return null;
            }

            // Split the device path by '/' to get each device name  
            var pathSegments = deviceItemPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            DeviceItem? deviceItem = null;

            // initial devices and groups
            var devices = _project.Devices;
            var groups = _project.DeviceGroups;

            for (int index = 0; index < pathSegments.Length; index++)
            {
                deviceItem = GetDeviceItemFromDevice(pathSegments, devices, index);

                if (deviceItem == null)
                {
                    // search in groups
                    var group = groups?.FirstOrDefault(g => g.Name.Equals(pathSegments[index], StringComparison.OrdinalIgnoreCase));
                    if (group != null)
                    {
                        devices = group.Devices;
                        if (devices != null)
                        {
                            deviceItem = GetDeviceItemFromDevice(pathSegments, devices, index + 1);
                        }

                        if (deviceItem != null)
                        {
                            return deviceItem;
                        }

                        // not found, but on the path
                        groups = group.Groups;
                        devices = group.Devices;
                    }
                }
                else
                {
                    return deviceItem;
                }
            }

            return deviceItem;
        }

        private static DeviceItem? GetDeviceItemFromDevice(string[] pathSegments, DeviceComposition? devices, int index)
        {
            string segment = pathSegments[index];
            string nextSegment = index + 1 < pathSegments.Length ? pathSegments[index + 1] : string.Empty;

            DeviceItem? deviceItem = null;

            // a pc based plc has a Device.Name = 'PC-System_1' or something like that, which is visible in the TIA-Portal IDE
            // use segment to find device
            var device = devices.FirstOrDefault(d => d.Name.Equals(segment, StringComparison.OrdinalIgnoreCase));
            if (device != null)
            {
                // then use next segment to find device item
                deviceItem = device.DeviceItems.FirstOrDefault(di => di.Name.Equals(nextSegment, StringComparison.OrdinalIgnoreCase));

            }

            // a hardware plc has a Device.Name = 'S7-1500/ET200MP-Station_1' or something like that, which is not visible in the TIA-Portal IDE
            if (device == null)
            {
                deviceItem = devices
                .SelectMany(d => d.DeviceItems)
                .FirstOrDefault(di => di.Name.Equals(segment, StringComparison.OrdinalIgnoreCase));
            }

            return deviceItem;
        }

        private PlcBlockGroup? GetPlcBlockGroupByPath(string softwarePath, string groupPath)
        {
            if (_project == null)
            {
                return null;
            }

            var softwareContainer = GetSoftwareContainer(softwarePath);
            if (softwareContainer?.Software is PlcSoftware plcSoftware)
            {
                if (plcSoftware?.BlockGroup == null)
                {
                    return null;
                }


                // Split the path by '/' to get each group name
                var groupNames = groupPath.Split(['/'], StringSplitOptions.RemoveEmptyEntries);

                PlcBlockGroup? currentGroup = plcSoftware.BlockGroup;

                foreach (var groupName in groupNames)
                {
                    currentGroup = currentGroup.Groups.FirstOrDefault(g => g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase));

                    if (currentGroup == null)
                    {
                        return null;
                    }
                }

                return currentGroup;
            }

            return null;
        }

        private PlcTypeGroup? GetPlcTypeGroupByPath(string softwarePath, string groupPath)
        {
            if (_project == null)
            {
                return null;
            }

            var softwareContainer = GetSoftwareContainer(softwarePath);
            if (softwareContainer?.Software is PlcSoftware plcSoftware)
            {
                if (plcSoftware?.TypeGroup == null)
                {
                    return null;
                }

                var groupNames = groupPath.Split(['/'], StringSplitOptions.RemoveEmptyEntries);

                PlcTypeGroup? currentGroup = plcSoftware.TypeGroup;

                foreach (var groupName in groupNames)
                {
                    currentGroup = currentGroup.Groups.FirstOrDefault(g => g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase));

                    if (currentGroup == null)
                    {
                        return null;
                    }
                }

                return currentGroup;
            }

            return null;
        }

        private string GetPlcBlockGroupPath(PlcBlockGroup group)
        {
            if (group == null)
            {
                return string.Empty;
            }

            PlcBlockGroup? nullableGroup = group;
            var path = group.Name;

            while (nullableGroup != null && nullableGroup.Parent != null)
            {
                try
                {
                    //group = (PlcBlockGroup) group.Parent;
                    if (group is PlcBlockSystemGroup systemGroup)
                    {
                        // do not get parent for system group
                        break;
                    }

                    nullableGroup = nullableGroup.Parent as PlcBlockGroup;
                }
                catch (Exception)
                {
                    // Handle any exceptions that may occur while accessing the parent
                    break;
                }

                if (nullableGroup != null)
                {
                    path = $"{nullableGroup.Name}/{path}";
                }
            }

            return path;
        }

        private string GetPlcTypeGroupPath(PlcTypeGroup group)
        {
            if (group == null)
            {
                return string.Empty;
            }

            PlcTypeGroup? nullableGroup = group;
            var path = group.Name;

            while (nullableGroup != null && nullableGroup.Parent != null)
            {
                try
                {
                    //group = (PlcTypeGroup) group.Parent;
                    if (group is PlcTypeSystemGroup systemGroup)
                    {
                        // do not get parent for system group
                        break;
                    }

                    nullableGroup = nullableGroup.Parent as PlcTypeGroup;
                }
                catch (Exception)
                {
                    // Handle any exceptions that may occur while accessing the parent
                    break;
                }

                if (nullableGroup != null)
                {
                    path = $"{nullableGroup.Name}/{path}";
                }
            }

            return path;
        }

        #endregion

        #region GetRecursive ...

        private bool GetDevicesRecursive(
            DeviceUserGroup group,
            List<Device> list,
            Regex? regex)
        {
            var anySuccess = false;

            foreach (var composition in group.Devices)
            {
                if (composition is Device device)
                {
                    if (regex != null && !regex.IsMatch(device.Name))
                    {
                        continue;
                    }

                    list.Add(device);

                    anySuccess = true;
                }
            }

            foreach (var subgroup in group.Groups)
            {
                anySuccess = GetDevicesRecursive(subgroup, list, regex) || anySuccess;
            }

            return anySuccess;
        }

        private bool GetBlocksRecursive(
            PlcBlockGroup group,
            List<PlcBlock> list,
            Regex? regex)
        {
            var anySuccess = false;

            foreach (var composition in group.Blocks)
            {
                if (composition is PlcBlock block)
                {
                    if (regex != null && !regex.IsMatch(block.Name))
                    {
                        continue;
                    }

                    list.Add(block);

                    anySuccess = true;
                }
            }

            foreach (var subgroup in group.Groups)
            {
                anySuccess = GetBlocksRecursive(subgroup, list, regex) || anySuccess;
            }

            return anySuccess;
        }

        private bool GetTypesRecursive(
            PlcTypeGroup group,
            List<PlcType> list,
            Regex? regex)
        {
            var anySuccess = false;

            foreach (var composition in group.Types)
            {
                if (composition is PlcType type)
                {
                    if (regex != null && !regex.IsMatch(type.Name))
                    {
                        continue;
                    }

                    list.Add(type);

                    anySuccess = true;
                }

            }

            foreach (PlcTypeGroup subgroup in group.Groups)
            {
                anySuccess = GetTypesRecursive(subgroup, list, regex) || anySuccess;
            }

            return anySuccess;
        }

        #endregion

        #endregion

    }


}
