using ModelContextProtocol.Server;
using System.ComponentModel;

namespace TiaMcpServer.ModelContextProtocol
{
    [McpServerPromptType]
    public sealed class McpPrompts
    {
        #region Basic Connection Templates

        [McpServerPrompt(Name = "Connect"), Description("Connect to TIA Portal")]
        public static string Connect()
        {
#if TIA_MCP_READ_WRITE
            return @"Connect to TIA Portal.

This will establish a connection to either a running TIA Portal instance or start a new one.

Use the Connect tool to initiate the connection.";
#else
            return @"Attach to the single running TIA Portal instance.

The read-only worker never starts TIA Portal. If no instance is running, ask the user to start the exact TIA Portal version selected for this package, then call Connect again.";
#endif
        }

#if TIA_MCP_READ_WRITE
        [McpServerPrompt(Name = "OpenProject"), Description("Open a TIA Portal project")]
        public static string OpenProject(string projectPath)
        {
            return $@"Open the TIA Portal project.

Common parameter values:
- projectPath: the full path to the project file (.ap18, .ap19, .ap20, etc.) or local session file (.als18, .als19, .als20, etc.).

Use the OpenProject tool with this parameter:
- path: {projectPath}";
        }

        [McpServerPrompt(Name = "CloseProject"), Description("Close the currently open TIA Portal project")]
        public static string CloseProject()
        {
            return @"Close the currently open TIA Portal project.

This will close the active project and return TIA Portal to the main screen.

Use the CloseProject tool to close the current project.";
        }

        [McpServerPrompt(Name = "Disconnect"), Description("Disconnect from TIA Portal")]
        public static string Disconnect()
        {
            return @"Disconnect from TIA Portal.

Use the Disconnect tool to remove the connection.";
        }
#endif

        #endregion

        #region Project Information Templates

        [McpServerPrompt(Name = "DiscoverCapabilities"), Description("Discover the active worker before choosing a TIA Portal workflow")]
        public static string DiscoverCapabilities()
        {
            return @"Call GetCapabilities before using domain tools.

Use the returned exact TIA Portal version, access profile and response-format policy as authoritative. Then call GetState. Keep responseFormat Auto unless a downstream parser requires a specific format. Auto uses CSV for the smallest flat summaries, TOON for richer eligible tables, and compact JSON when nested data cannot be represented losslessly. Follow paging cursors only as needed and reuse returned names or canonical paths. If a capability is absent, report that limitation instead of guessing or attempting a similarly named write tool.";
        }

        [McpServerPrompt(Name = "InspectProjectSafely"), Description("Inspect the active project with compact, read-only discovery")]
        public static string InspectProjectSafely()
        {
            return @"Inspect the active TIA Portal project without changing it.

1. Call GetState to confirm the connection and active project.
2. Call ListProjects only when project identity or session information is needed.
3. Call GetDevices with detailLevel Summary, responseFormat Auto and the default page size.
4. Follow nextCursor only while more devices are relevant to the request.
5. Request Full detail only for a selected device when diagnostics require raw attributes.

Auto selects compact CSV for the flat Summary result. Use Standard when the model benefits from TOON's explicit fields, row count and scalar typing. Full uses compact JSON because nested raw attributes cannot be represented losslessly in the bounded table profile.

Reuse exact names and paths returned by discovery tools. Do not call save, compile, import, export, close or other mutating tools unless the user clearly asks for a write workflow.";
        }

        [McpServerPrompt(Name = "GetProjectTree"), Description("Get the project structure/tree on the current TIA Portal project")]
        public static string GetProjectTree()
        {
            return @"Retrieve the legacy complete structure of the current TIA Portal project only when bounded list tools cannot answer the request.

The hierarchical tree will display:
- All devices
- Device items
- Groups
- PLC/HMI software

Prefer ListProjects and paged GetDevices for normal discovery. Use GetProjectTree only when the user explicitly needs the complete hierarchy.";
        }

        [McpServerPrompt(Name = "GetSoftwareTree"), Description("Get the structure/tree of a specific PLC software showing blocks and types")]
        public static string GetSoftwareTree(string softwarePath)
        {
            return $@"Retrieve the legacy complete structure of PLC software only when bounded list tools cannot answer the request.

The hierarchical tree will display:
- Function (OB, FB, FC) and data (ArrayDB, GlobalDB, InstanceDB) blocks (organized by groups and subgroups)
- User-defined data types (organized by groups and subgroups)
- Hierarchical organization with proper tree formatting

Common parameter values:
- softwarePath: normally something like 'PLC_1' for hardware PLC, 'PC-System_1/Software PLC_1' for PC based PLC

Prefer paged GetBlocks and GetTypes for normal discovery. Use GetSoftwareTree only when the user explicitly needs the complete hierarchy.

Use the GetSoftwareTree tool with these parameters:
- softwarePath: {softwarePath}";
        }

        #endregion

#if TIA_MCP_READ_WRITE
        #region Export Templates

        [McpServerPrompt(Name = "ExportBlocks"), Description("Export blocks from PLC software")]
        public static string ExportBlocks(string softwarePath, string exportPath, string regexName, bool preservePath)
        {
            return $@"Export blocks from PLC software.

Common parameter values:
- softwarePath: normally something like 'PLC_1' for hardware PLC, 'PC-System_1/Software PLC_1' for PC based PLC
- exportPath: a directory relative to the configured output root
- regexName: Use empty string """" for all blocks, or patterns like ""FB_.*"" for function blocks
- preservePath: Use false for flat export, true to maintain folder structure
- overwrite: defaults to false; use true only when the user explicitly asks to replace files

Use the ExportBlocks tool with these parameters:
- softwarePath: {softwarePath}
- exportPath: {exportPath}
- regexName: {regexName}
- preservePath: {preservePath.ToString().ToLower()}";
        }

        [McpServerPrompt(Name = "ExportTypes"), Description("Export types from PLC software")]
        public static string ExportTypes(string softwarePath, string exportPath, string regexName, bool preservePath)
        {
            return $@"Export user-defined types from PLC software.

Common parameter values:
- softwarePath: normally something like 'PLC_1' for hardware PLC, 'PC-System_1/Software PLC_1' for PC based PLC
- exportPath: a directory relative to the configured output root
- regexName: Use empty string """" for all types, or patterns like ""Typ_.*""
- preservePath: Use false for flat export, true to maintain folder structure
- overwrite: defaults to false; use true only when the user explicitly asks to replace files

Use the ExportTypes tool with these parameters:
- softwarePath: {softwarePath}
- exportPath: {exportPath}
- regexName: {regexName}
- preservePath: {preservePath.ToString().ToLower()}";
        }

#if TIA_MCP_V20
        [McpServerPrompt(Name = "ExportBlocksAsDocuments"), Description("Export blocks as documents (.s7dcl/.s7res format)")]
        public static string ExportBlocksAsDocuments(string softwarePath, string exportPath, string regexName, bool preservePath)
        {
            return $@"Export blocks as SIMATIC SD documents (.s7dcl/.s7res format) from PLC software.
Available only in the TIA Portal V20 worker for this alpha.

Common parameter values:
- softwarePath: normally something like 'PLC_1' for hardware PLC, 'PC-System_1/Software PLC_1' for PC based PLC
- exportPath: a directory relative to the configured output root
- regexName: Use empty string """" for all blocks, or patterns like ""FB_.*""
- preservePath: Use false for flat export, true to maintain folder structure
- overwrite: defaults to false; use true only when the user explicitly asks to replace files

Use the ExportBlocksAsDocuments tool with these parameters:
- softwarePath: {softwarePath}
- exportPath: {exportPath}
- regexName: {regexName}
- preservePath: {preservePath.ToString().ToLower()}";
        }
#endif

        #endregion

        #region Convenience Export Templates

        [McpServerPrompt(Name = "ExportAllBlocksFlattened"), Description("Export all blocks from PLC software (flattened)")]
        public static string ExportAllBlocksFlattened(string softwarePath, string exportPath)
        {
            return ExportBlocks(softwarePath, exportPath, "", false);
        }

        [McpServerPrompt(Name = "ExportAllBlocksStructured"), Description("Export all blocks from PLC software (structured)")]
        public static string ExportAllBlocksStructured(string softwarePath, string exportPath)
        {
            return ExportBlocks(softwarePath, exportPath, "", true);
        }

        [McpServerPrompt(Name = "ExportAllTypesFlattened"), Description("Export all types from PLC software (flattened)")]
        public static string ExportAllTypesFlattened(string softwarePath, string exportPath)
        {
            return ExportTypes(softwarePath, exportPath, "", false);
        }

        [McpServerPrompt(Name = "ExportAllTypesStructured"), Description("Export all types from PLC software (structured)")]
        public static string ExportAllTypesStructured(string softwarePath, string exportPath)
        {
            return ExportTypes(softwarePath, exportPath, "", true);
        }

#if TIA_MCP_V20
        [McpServerPrompt(Name = "ExportAllBlocksAsDocumentsFlattened"), Description("Export all blocks as documents from PLC software (flattened)")]
        public static string ExportAllBlocksAsDocumentsFlattened(string softwarePath, string exportPath)
        {
            return ExportBlocksAsDocuments(softwarePath, exportPath, "", false);
        }

        [McpServerPrompt(Name = "ExportAllBlocksAsDocumentsStructured"), Description("Export all blocks as documents from PLC software (structured)")]
        public static string ExportAllBlocksAsDocumentsStructured(string softwarePath, string exportPath)
        {
            return ExportBlocksAsDocuments(softwarePath, exportPath, "", true);
        }
#endif

        #endregion

#if TIA_MCP_V20
        #region Import From Documents Templates

        [McpServerPrompt(Name = "ImportFromDocuments"), Description("Import a single block from SIMATIC SD documents (.s7dcl/.s7res) in the V20 worker")]
        public static string ImportFromDocuments(string softwarePath, string groupPath, string importPath, string fileNameWithoutExtension, string importOption)
        {
            return $@"Import a single program block from SIMATIC SD documents into PLC software (available only in the TIA Portal V20 worker for this alpha).

Common parameter values:
- softwarePath: e.g. 'PLC_1' for hardware PLC
- groupPath: optional, e.g. 'Program blocks/FBs'
- importPath: folder containing .s7dcl/.s7res files
- fileNameWithoutExtension: e.g. 'FC_DateTime'
- importOption: 'None' (default), 'Override', 'SkipInactiveCultures', 'ActivateInactiveCultures'

Note: As of 2025-09-02, importing Ladder (LAD) blocks requires the companion .s7res to contain en-US tags for all items; otherwise import may fail.

Use the ImportFromDocuments tool with these parameters:
- softwarePath: {softwarePath}
- groupPath: {groupPath}
- importPath: {importPath}
- fileNameWithoutExtension: {fileNameWithoutExtension}
- importOption: {importOption}";
        }

        [McpServerPrompt(Name = "ImportBlocksFromDocuments"), Description("Import blocks from SIMATIC SD documents (.s7dcl/.s7res) in the V20 worker")]
        public static string ImportBlocksFromDocuments(string softwarePath, string groupPath, string importPath, string regexName, string importOption)
        {
            return $@"Import multiple program blocks from SIMATIC SD documents into PLC software (available only in the TIA Portal V20 worker for this alpha).

Common parameter values:
- softwarePath: e.g. 'PLC_1' for hardware PLC
- groupPath: optional target group path, empty for root
- importPath: folder containing .s7dcl/.s7res files
- regexName: empty for all, or e.g. 'FB_.*'
- importOption: 'None' (default), 'Override', 'SkipInactiveCultures', 'ActivateInactiveCultures'

Note: As of 2025-09-02, importing Ladder (LAD) blocks requires the companion .s7res to contain en-US tags for all items; otherwise import may fail.

Use the ImportBlocksFromDocuments tool with these parameters:
- softwarePath: {softwarePath}
- groupPath: {groupPath}
- importPath: {importPath}
- regexName: {regexName}
- importOption: {importOption}";
        }

        #endregion
#endif
#endif
    }
}

