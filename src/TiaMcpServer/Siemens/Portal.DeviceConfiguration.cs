using Siemens.Engineering;
using Siemens.Engineering.Cax;
using System;
using System.IO;
using System.Linq;
using TiaMcpServer.Security;

namespace TiaMcpServer.Siemens
{
    public partial class Portal
    {
        public object ExportDeviceConfiguration(string expectedProjectPath, string devicePath, string outputDirectory)
        {
            ProfileAccessPolicy.DemandWrite("export offline device configuration");
            RequireSameSelectedProject(expectedProjectPath);
            var project = _project as Project ?? throw new NotSupportedException("CAx export is implemented for local projects only. Multiuser sessions need separate qualification.");
            var device = GetDevices().SingleOrDefault(d => string.Equals(GetDevicePath(d), devicePath, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException("Device not found at the exact discovered path. Use GetDevices first.");
            var provider = project.GetService<CaxProvider>() ?? throw new NotSupportedException("This project does not expose the CAx export service.");
            var parent = OutputPathPolicy.ResolveDirectory(outputDirectory);
            var folder = OutputPathPolicy.ResolveDirectory(Path.Combine(parent, "device-configuration-" + Guid.NewGuid().ToString("N")));
            if (Directory.Exists(folder) || File.Exists(folder)) throw new ArgumentException("The export directory already exists. Retry with a new operation.");
            Directory.CreateDirectory(folder);
            folder = OutputPathPolicy.ResolveDirectory(folder);
            var aml = Path.Combine(folder, "configuration.aml");
            var log = Path.Combine(folder, "export.log");
            var succeeded = provider.Export(device, new FileInfo(aml), new FileInfo(log));
            return new
            {
                DevicePath = GetDevicePath(device), NativeExportSucceeded = succeeded,
                ConfigurationPath = File.Exists(aml) ? aml : null,
                LogPath = File.Exists(log) ? log : null,
                Saved = false, Downloaded = false,
                Interpretation = "Offline engineering configuration only. Inspect the native export log for omissions and warnings. This is not a live device backup or runtime health report; failed exports may leave partial files in the returned directory.",
                OutputDirectory = folder
            };
        }
    }
}
