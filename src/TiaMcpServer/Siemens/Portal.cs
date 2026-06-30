using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.Hmi;
using Siemens.Engineering.HmiUnified;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.Library;
using Siemens.Engineering.Library.MasterCopies;
using Siemens.Engineering.Multiuser;
using Siemens.Engineering.Download;
using Siemens.Engineering.Download.Configurations;
using Siemens.Engineering.Online;
using Siemens.Engineering.Safety;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Tags;
using Siemens.Engineering.SW.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;

namespace TiaMcpServer.Siemens
{
    public class Portal
    {
        // closing parantheses for regex characters ommitted, because they are not relevant for regex detection
        private readonly char[] _regexChars = ['.', '^', '$', '*', '+', '?', '(', '[', '{', '\\', '|'];

        private TiaPortal? _portal;
        private ProjectBase? _project;
        private LocalSession? _session;
        private readonly ILogger<Portal>? _logger;

        // Live handles exposed for the EvalCSharp dev tool (run Openness code in-process without rebuilds).
        public ProjectBase? CurrentProject => _project;
        public TiaPortal? CurrentPortal => _portal;

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
            var regex = new Regex(@"\.als\d+$", RegexOptions.IgnoreCase);
            return regex.IsMatch(sessionPath);
        }

        public static bool IsLocalProjectFile(string projectPath)
        {
            // Check if the path ends with '.ap\d+' using regex
            var regex = new Regex(@"\.ap\d+$", RegexOptions.IgnoreCase);
            return regex.IsMatch(projectPath);
        }

        public void Dispose()
        {
            try
            {
                (_project as Project)?.Close();
            }
            catch (Exception)
            {
                // Console.WriteLine($"Error closing the project: {ex.Message}");
            }

            try
            {
                _portal?.Dispose();
            }
            catch (Exception)
            {
                // Console.WriteLine($"Error closing the portal: {ex.Message}");
            }
        }

        #endregion

        #region portal

        public bool ConnectPortal()
        {
            _logger?.LogInformation("Connecting to TIA Portal...");

            try
            {
                _project = null;
                _session = null;
                _portal = null;

                // connect to running TIA Portal
                var processes = TiaPortal.GetProcesses();
                if (processes.Any())
                {
                    _portal = processes.First().Attach();

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

                // start new TIA Portal
                _portal = new TiaPortal(TiaPortalMode.WithUserInterface);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool IsConnected()
        {
            return _portal != null;
        }

        public bool DisconnectPortal()
        {
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
            _logger?.LogInformation($"Opening project: {projectPath}");

            if (IsPortalNull())
            {
                return false;
            }

            if (_project != null)
            {
                (_project as Project)?.Close();
                _project = null;
            }

            if (_session != null)
            {
                _session.Close();
                _session = null;
            }

            try
            {
                var projects = GetProjects();
                var projectName = Path.GetFileNameWithoutExtension(projectPath);

                if (!string.IsNullOrEmpty(projectName) && projects.Any(p => p.Name.Equals(projectName)))
                {
                    // Project is already open
                    _project = _portal?.Projects.FirstOrDefault(p => p.Name == projectName);

                    return _project != null;
                }
                else
                {
                    // see [5.3.1 Projekt öffnen, S.113]
                    _project = _portal?.Projects.OpenWithUpgrade(new FileInfo(projectPath));

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
            _logger?.LogInformation($"Saving project as: {path}");

            if (IsProjectNull())
            {
                return false;
            }

            var di = new DirectoryInfo(path);

            (_project as Project)?.SaveAs(di);

            return true;
        }

        public bool ArchiveProject(string archivePath)
        {
            _logger?.LogInformation($"Archiving project to: {archivePath}");

            if (string.IsNullOrWhiteSpace(archivePath))
            {
                return false;
            }

            if (IsProjectNull())
            {
                return false;
            }

            var directoryName = Path.GetDirectoryName(archivePath);
            var archiveName = Path.GetFileName(archivePath);

            if (string.IsNullOrEmpty(directoryName) || string.IsNullOrEmpty(archiveName))
            {
                return false;
            }

            var targetDir = new DirectoryInfo(directoryName);

            if (!targetDir.Exists)
            {
                targetDir.Create();
            }

            if (!(_project is Project project))
            {
                return false;
            }

            project.Archive(targetDir, archiveName, ProjectArchivationMode.Compressed);

            return true;
        }

        public bool RetrieveProject(string archivePath, string targetProjectPath)
        {
            _logger?.LogInformation($"Retrieving project from archive: {archivePath} to {targetProjectPath}");

            if (string.IsNullOrWhiteSpace(archivePath) || string.IsNullOrWhiteSpace(targetProjectPath))
            {
                return false;
            }

            if (IsPortalNull())
            {
                return false;
            }

            var sourceFile = new FileInfo(archivePath);

            if (!sourceFile.Exists)
            {
                return false;
            }

            if (_project != null)
            {
                (_project as Project)?.Close();
                _project = null;
            }

            var targetDir = new DirectoryInfo(targetProjectPath);

            if (!targetDir.Exists)
            {
                targetDir.Create();
            }

            var project = _portal.Projects.Retrieve(sourceFile, targetDir);

            if (project != null)
            {
                _project = project;
                return true;
            }

            return false;
        }

        public bool CloseProject()
        {
            _logger?.LogInformation("Closing project...");

            if (IsProjectNull())
            {
                return false;
            }

            (_project as Project)?.Close();
            _project = null;

            return true;
        }

        public bool CreateProject(string projectPath, string projectName)
        {
            _logger?.LogInformation($"Creating project: {projectName} at {projectPath}");

            if (IsPortalNull())
            {
                return false;
            }

            if (_project != null)
            {
                (_project as Project)?.Close();
                _project = null;
            }

            if (_session != null)
            {
                _session.Close();
                _session = null;
            }

            try
            {
                var di = new DirectoryInfo(projectPath);
                if (!di.Exists)
                {
                    di.Create();
                }

                _project = _portal.Projects.Create(di, projectName);
                return _project != null;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "CreateProject failed for {ProjectPath} {ProjectName}", projectPath, projectName);
                return false;
            }
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
            _logger?.LogInformation($"Opening session: {localSessionPath}");

            if (IsPortalNull())
            {
                return false;
            }

            if (_session != null)
            {
                _project = null;
                _session?.Close();
                _session = null;
            }

            try
            {
                var sessions = GetSessions();
                var projectName = Path.GetFileNameWithoutExtension(localSessionPath);
                var sessionName = Regex.Replace(projectName, @"_(LS|ES)_\d$", string.Empty, RegexOptions.IgnoreCase);

                if (!string.IsNullOrEmpty(sessionName) && sessions.Any(s => s.Name.Equals(sessionName)))
                {
                    // Session is already open  
                    _session = _portal?.LocalSessions.FirstOrDefault(s => s.Project.Name == sessionName);
                    if (_session != null)
                    {
                        // Correctly cast MultiuserProject to Project  
                        _project = _session.Project;
                        return _project != null;
                    }
                }
                else
                {
                    _session = _portal?.LocalSessions.Open(new FileInfo(localSessionPath));
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

            if (_portal != null && _project == null)
            {
                try
                {
                    if (_portal.LocalSessions.Any())
                    {
                        _session = _portal.LocalSessions.First();
                        _project = _session.Project;
                    }
                    else if (_portal.Projects.Any())
                    {
                        _project = _portal.Projects.First();
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "GetProjectTree: failed to refresh project from portal, retrying ConnectPortal");
                    ConnectPortal();
                }
            }

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

            if (_portal != null && _project == null)
            {
                try
                {
                    if (_portal.LocalSessions.Any())
                    {
                        _session = _portal.LocalSessions.First();
                        _project = _session.Project;
                    }
                    else if (_portal.Projects.Any())
                    {
                        _project = _portal.Projects.First();
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "GetDevices: failed to refresh project from portal, retrying ConnectPortal");
                    ConnectPortal();
                }
            }

            if (IsProjectNull())
            {
                return [];
            }

            var list = new List<Device>();

            var dbgLog = @"C:\Tools\TiaMcpServer-V21\debug_getdevices.txt";
            System.IO.File.AppendAllText(dbgLog, $"[{DateTime.Now:HH:mm:ss.fff}] GetDevices called. _project={_project?.Name}, type={_project?.GetType().Name}\n");

            try
            {
                System.IO.File.AppendAllText(dbgLog, $"[{DateTime.Now:HH:mm:ss.fff}] Accessing _project.Devices...\n");
                var devices = _project?.Devices;
                System.IO.File.AppendAllText(dbgLog, $"[{DateTime.Now:HH:mm:ss.fff}] devices count={devices?.Count}\n");

                if (devices != null)
                {
                    foreach (Device device in devices)
                    {
                        list.Add(device);
                        try
                        {
                            var itemNames = string.Join(", ", device.DeviceItems.Cast<DeviceItem>().Select(di => di.Name));
                            System.IO.File.AppendAllText(dbgLog, $"[{DateTime.Now:HH:mm:ss.fff}] Device '{device.Name}' items: [{itemNames}]\n");
                        }
                        catch (Exception ex2)
                        {
                            System.IO.File.AppendAllText(dbgLog, $"[{DateTime.Now:HH:mm:ss.fff}] Device '{device.Name}' items error: {ex2.Message}\n");
                        }
                    }

                    foreach (var group in _project.DeviceGroups)
                        GetDevicesRecursive(group, list, regexName);
                }
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText(dbgLog, $"[{DateTime.Now:HH:mm:ss.fff}] EXCEPTION: {ex.GetType().Name}: {ex.Message}\nStack: {ex.StackTrace}\n");
                throw;
            }

            System.IO.File.AppendAllText(dbgLog, $"[{DateTime.Now:HH:mm:ss.fff}] Returning {list.Count} devices\n");
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

        public CompilerResult? CompileHardware(string devicePath)
        {
            _logger?.LogInformation($"Compiling hardware for device: {devicePath}");

            if (IsProjectNull())
            {
                return null;
            }

            var device = GetDeviceByPath(devicePath);
            if (device == null)
            {
                throw new PortalException(PortalErrorCode.NotFound, $"Device not found: {devicePath}");
            }

            try
            {
                ICompilable compileService = device.GetService<ICompilable>();
                CompilerResult result = compileService.Compile();
                return result;
            }
            catch (PortalException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "CompileHardware failed for {DevicePath}", devicePath);
                return null;
            }
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
                        if (regexName.IndexOfAny(_regexChars) >= 0)
                        {
                            try
                            {
                                var regex = new Regex(regexName, RegexOptions.IgnoreCase);
                                block = group.Blocks.FirstOrDefault(b => regex.IsMatch(b.Name)) as PlcBlock;
                            }
                            catch (Exception)
                            {
                                // Invalid regex, return null
                                return null;
                            }
                        }
                        else
                        {
                            block = group.Blocks.FirstOrDefault(b => b.Name.Equals(regexName, StringComparison.OrdinalIgnoreCase));
                        }

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
                        if (regexName.IndexOfAny(_regexChars) >= 0)
                        {
                            try
                            {
                                var regex = new Regex(regexName, RegexOptions.IgnoreCase);
                                type = group.Types.FirstOrDefault(t => regex.IsMatch(t.Name)) as PlcType;
                            }
                            catch (Exception)
                            {
                                // Invalid regex, return null
                                return null;
                            }
                        }
                        else
                        {
                            type = group.Types.FirstOrDefault(t => t.Name.Equals(regexName, StringComparison.OrdinalIgnoreCase));
                        }

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

        public List<PlcBlock> GetBlocks(string softwarePath, string regexName = "")
        {
            _logger?.LogInformation("Getting blocks...");

            if (IsProjectNull())
            {
                return [];
            }

            var list = new List<PlcBlock>();

            try
            {
                var softwareContainer = GetSoftwareContainer(softwarePath);
                if (softwareContainer?.Software is PlcSoftware plcSoftware)
                {
                    var group = plcSoftware?.BlockGroup;

                    if (group != null)
                    {
                        GetBlocksRecursive(group, list, regexName);
                    }
                }
            }
            catch (Exception)
            {
                // Console.WriteLine($"Error getting blocks: {ex.Message}");
            }

            return list;
        }

        public PlcBlockGroup? GetBlockRootGroup(string softwarePath)
        {
            _logger?.LogInformation("Getting block root group...");

            if (IsProjectNull())
            {
                return null;
            }

            try
            {
                var softwareContainer = GetSoftwareContainer(softwarePath);
                if (softwareContainer?.Software is PlcSoftware plcSoftware)
                {
                    return plcSoftware.BlockGroup;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting block root group");
            }

            return null;
        }

        public List<PlcType> GetTypes(string softwarePath, string regexName = "")
        {
            _logger?.LogInformation("Getting types...");

            if (IsProjectNull())
            {
                return [];
            }

            var list = new List<PlcType>();

            try
            {
                var softwareContainer = GetSoftwareContainer(softwarePath);
                if (softwareContainer?.Software is PlcSoftware plcSoftware)
                {
                    var group = plcSoftware?.TypeGroup;

                    if (group != null)
                    {
                        GetTypesRecursive(group, list, regexName);
                    }
                }
            }
            catch (Exception)
            {
                // Console.WriteLine($"Error getting user defined types: {ex.Message}");
            }

            return list;
        }

        #region hmi

        public List<IEngineeringObject> GetHmiScreens(string softwarePath, string regexName = "")
        {
            _logger?.LogInformation("Getting HMI screens...");

            if (IsProjectNull())
            {
                return [];
            }

            var list = new List<IEngineeringObject>();

            try
            {
                var software = GetSoftwareContainer(softwarePath)?.Software;

                // WinCC Comfort/Unified - enumerate root screens AND screens nested in folders
                if (software != null)
                {
                    foreach (var screen in EnumerateScreens(software))
                    {
                        if (screen is not IEngineeringObject eo) continue;
                        try
                        {
                            var name = screen.GetType().GetProperty("Name")?.GetValue(screen) as string ?? "";
                            if (!string.IsNullOrEmpty(regexName) && !Regex.IsMatch(name, regexName, RegexOptions.IgnoreCase))
                            {
                                continue;
                            }
                        }
                        catch (Exception)
                        {
                            continue;
                        }

                        list.Add(eo);
                    }
                }
            }
            catch (Exception)
            {
                // Silently fail
            }

            return list;
        }

        public bool ExportHmiScreen(string softwarePath, string screenName, string exportPath)
        {
            _logger?.LogInformation($"Exporting HMI screen: {screenName}");

            try
            {
                if (IsProjectNull())
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "No project is open in TIA Portal");
                }

                var softwareContainer = GetSoftwareContainer(softwarePath);
                var software = softwareContainer?.Software;
                if (software == null)
                {
                    throw new PortalException(PortalErrorCode.NotFound, $"HMI software not found: {softwarePath}");
                }

                _logger?.LogInformation("HMI software runtime type: {Type}", software.GetType().FullName);

                var screen = FindHmiScreen(software, screenName);
                if (screen == null)
                {
                    throw new PortalException(PortalErrorCode.NotFound, $"HMI screen not found: {screenName}");
                }

                _logger?.LogInformation("HMI screen runtime type: {Type}", screen.GetType().FullName);

                if (!Directory.Exists(exportPath))
                {
                    Directory.CreateDirectory(exportPath);
                }

                var filePath = Path.Combine(exportPath, $"{screenName}.xml");
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                InvokeExport(screen, new FileInfo(filePath));

                return true;
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.ExportFailed, "Export HMI screen failed", null, ex);

                if (!pex.Data.Contains("softwarePath")) pex.Data["softwarePath"] = softwarePath;
                if (!pex.Data.Contains("screenName")) pex.Data["screenName"] = screenName;
                if (!pex.Data.Contains("exportPath")) pex.Data["exportPath"] = exportPath;

                _logger?.LogError(pex, "ExportHmiScreen failed for {SoftwarePath} {ScreenName}", softwarePath, screenName);
                throw pex;
            }
        }

        public bool ImportHmiScreen(string softwarePath, string importPath)
        {
            _logger?.LogInformation($"Importing HMI screen from: {importPath}");

            if (IsProjectNull())
            {
                return false;
            }

            try
            {
                var softwareContainer = GetSoftwareContainer(softwarePath);

                var fileInfo = new FileInfo(importPath);
                if (!fileInfo.Exists)
                {
                    return false;
                }

                var software = softwareContainer?.Software;
                if (software != null)
                {
                    _logger?.LogInformation("HMI software runtime type: {Type}", software.GetType().FullName);

                    // Get the "Screens" composition via reflection (works for Comfort HmiSoftware and Unified HmiUnifiedSoftware)
                    var screensProp = software.GetType().GetProperty("Screens");
                    var screens = screensProp?.GetValue(software);
                    if (screens == null)
                    {
                        return false;
                    }

                    var importMethods = screens.GetType().GetMethods()
                        .Where(m => m.Name == "Import").ToList();
                    foreach (var m in importMethods)
                    {
                        _logger?.LogInformation("Import overload: ({Params})",
                            string.Join(", ", m.GetParameters().Select(p => p.ParameterType.FullName)));
                    }

                    var importMethod = importMethods.FirstOrDefault(m =>
                    {
                        var ps = m.GetParameters();
                        return ps.Length >= 1 && typeof(FileInfo).IsAssignableFrom(ps[0].ParameterType);
                    });
                    if (importMethod == null)
                    {
                        return false;
                    }

                    var pars = importMethod.GetParameters();
                    var args = new object?[pars.Length];
                    args[0] = fileInfo;
                    for (int i = 1; i < pars.Length; i++)
                    {
                        if (pars[i].ParameterType == typeof(ImportOptions)) args[i] = ImportOptions.Override;
                        else if (pars[i].HasDefaultValue) args[i] = pars[i].DefaultValue;
                        else args[i] = pars[i].ParameterType.IsValueType ? Activator.CreateInstance(pars[i].ParameterType) : null;
                    }
                    importMethod.Invoke(screens, args);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ImportHmiScreen failed for {SoftwarePath} {ImportPath}", softwarePath, importPath);
                return false;
            }

            return false;
        }

        // Finds an HMI screen by name on any HMI software type (Comfort HmiSoftware or Unified HmiUnifiedSoftware),
        // searching the root Screens collection AND recursively inside ScreenFolders (WinCC Unified organizes
        // screens in folders, which the root Screens property does not include).
        private object? FindHmiScreen(object software, string screenName)
        {
            foreach (var s in EnumerateScreens(software))
            {
                var name = s.GetType().GetProperty("Name")?.GetValue(s) as string;
                if (string.Equals(name, screenName, StringComparison.OrdinalIgnoreCase))
                {
                    return s;
                }
            }
            return null;
        }

        // Snapshots an Openness composition into a list sorted by the object's Name (case-insensitive,
        // ordinal) so outputs match the WinCC editor's alphabetical order instead of raw API order.
        private List<object> SnapshotSortedByName(object enumerable)
        {
            var list = Snapshot(enumerable);
            list.Sort((a, b) => string.Compare(
                a.GetType().GetProperty("Name")?.GetValue(a) as string ?? "",
                b.GetType().GetProperty("Name")?.GetValue(b) as string ?? "",
                StringComparison.OrdinalIgnoreCase));
            return list;
        }

        // Yields every screen of an HMI software, descending recursively through screen folders.
        // Works for both Comfort (HmiSoftware) and Unified (HmiUnifiedSoftware) via reflection.
        // Screens and groups are returned sorted by name to match the editor.
        private IEnumerable<object> EnumerateScreens(object container)
        {
            // screens directly in this container (sorted by name)
            foreach (var s in SnapshotSortedByName(container.GetType().GetProperty("Screens")?.GetValue(container)))
            {
                yield return s;
            }

            // recurse into screen groups (WinCC Unified: "ScreenGroups" on the software root, "Groups" on a group), sorted
            foreach (var groupPropName in new[] { "ScreenGroups", "Groups" })
            {
                foreach (var group in SnapshotSortedByName(container.GetType().GetProperty(groupPropName)?.GetValue(container)))
                {
                    foreach (var s in EnumerateScreens(group))
                    {
                        yield return s;
                    }
                }
            }
        }

        // Builds an indented tree of screen folders and screens for an HMI software.
        public string GetHmiScreenTree(string softwarePath)
        {
            if (IsProjectNull()) return "ERROR: no project open";
            try
            {
                var software = GetSoftwareContainer(softwarePath)?.Software;
                if (software == null) return $"ERROR: HMI software not found: {softwarePath}";
                var sb = new StringBuilder();
                sb.AppendLine($"HMI software '{softwarePath}' [{software.GetType().FullName}]");
                DumpScreenFolder(software, sb, 1);
                return sb.ToString();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "GetHmiScreenTree failed for {SoftwarePath}", softwarePath);
                return "ERROR: " + ex.Message;
            }
        }

        private void DumpScreenFolder(object container, StringBuilder sb, int depth)
        {
            var pad = new string(' ', depth * 2);
            foreach (var s in SnapshotSortedByName(container.GetType().GetProperty("Screens")?.GetValue(container)))
            {
                var name = s.GetType().GetProperty("Name")?.GetValue(s) as string;
                sb.AppendLine($"{pad}[screen] {name}");
            }
            foreach (var groupPropName in new[] { "ScreenGroups", "Groups" })
            {
                foreach (var group in SnapshotSortedByName(container.GetType().GetProperty(groupPropName)?.GetValue(container)))
                {
                    var fname = group.GetType().GetProperty("Name")?.GetValue(group) as string;
                    sb.AppendLine($"{pad}<group> {fname}");
                    DumpScreenFolder(group, sb, depth + 1);
                }
            }
        }

        // Invokes the screen's Export method via reflection, matching any overload whose first
        // parameter is a FileInfo. Logs all available overloads for diagnostics.
        private void InvokeExport(object screen, FileInfo file)
        {
            var exportMethods = screen.GetType().GetMethods()
                .Where(m => m.Name == "Export").ToList();
            foreach (var m in exportMethods)
            {
                _logger?.LogInformation("Export overload: ({Params})",
                    string.Join(", ", m.GetParameters().Select(p => p.ParameterType.FullName)));
            }

            var method = exportMethods.FirstOrDefault(m =>
            {
                var ps = m.GetParameters();
                return ps.Length >= 1 && typeof(FileInfo).IsAssignableFrom(ps[0].ParameterType);
            });
            if (method == null)
            {
                // Diagnostic: list all export/import/save-like methods with full signatures
                var candidates = screen.GetType().GetMethods()
                    .Where(m => m.Name.IndexOf("xport", StringComparison.OrdinalIgnoreCase) >= 0
                             || m.Name.IndexOf("mport", StringComparison.OrdinalIgnoreCase) >= 0
                             || m.Name.IndexOf("Save", StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(m => m.Name + "(" + string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)) + ")")
                    .Distinct()
                    .ToList();
                var dump = candidates.Count > 0 ? string.Join(" | ", candidates) : "(none)";
                throw new PortalException(PortalErrorCode.ExportFailed,
                    "Screen type " + screen.GetType().FullName + " exposes no XML Export method (candidates: " + dump + "). " +
                    "WinCC Unified screens do not support SimaticML export/import via Openness; use the screen object model (HmiScreenComposition.Create, HmiScreen.ScreenItems) instead.");
            }

            var pars = method.GetParameters();
            var args = new object?[pars.Length];
            args[0] = file;
            for (int i = 1; i < pars.Length; i++)
            {
                if (pars[i].ParameterType == typeof(ExportOptions)) args[i] = ExportOptions.None;
                else if (pars[i].HasDefaultValue) args[i] = pars[i].DefaultValue;
                else args[i] = pars[i].ParameterType.IsValueType ? Activator.CreateInstance(pars[i].ParameterType) : null;
            }
            method.Invoke(screen, args);
        }

        // Reads the ScreenItems tree of an HMI screen (works for WinCC Unified via the object model)
        // and returns an indented text dump including item types, key attributes and Dynamizations (tag links).
        public string GetHmiScreenItems(string softwarePath, string screenName)
        {
            if (IsProjectNull())
            {
                return "ERROR: no project open";
            }

            try
            {
                var software = GetSoftwareContainer(softwarePath)?.Software;
                if (software == null) return $"ERROR: HMI software not found: {softwarePath}";

                var screen = FindHmiScreen(software, screenName);
                if (screen == null) return $"ERROR: HMI screen not found: {screenName}";

                var sb = new StringBuilder();
                sb.AppendLine($"Screen '{screenName}' [{screen.GetType().FullName}]");
                DumpScreenObject(screen, sb, 1);
                return sb.ToString();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "GetHmiScreenItems failed for {SoftwarePath} {ScreenName}", softwarePath, screenName);
                return "ERROR: " + ex.Message;
            }
        }

        private static readonly string[] _hmiKeyAttrNames =
            { "Name", "Left", "Top", "Width", "Height", "Text", "Caption", "Visible", "ProcessValue", "Tag", "Content", "ToolTipText" };

        // Recursively dumps an HMI engineering object: its key attributes, Dynamizations (tag bindings),
        // EventHandlers, and any nested ScreenItems composition.
        private void DumpHmiObject(object obj, StringBuilder sb, int depth)
        {
            if (obj == null || depth > 8) return;
            var pad = new string(' ', depth * 2);
            var type = obj.GetType();

            // Name
            string name = "";
            try { name = type.GetProperty("Name")?.GetValue(obj) as string ?? ""; } catch { }
            sb.AppendLine($"{pad}- {type.Name} \"{name}\"");

            // Key attributes via Openness GetAttribute
            var getAttr = type.GetMethod("GetAttribute", new[] { typeof(string) });
            if (getAttr != null)
            {
                foreach (var an in _hmiKeyAttrNames)
                {
                    try
                    {
                        var v = getAttr.Invoke(obj, new object[] { an });
                        if (v != null)
                        {
                            var s = v.ToString();
                            if (!string.IsNullOrEmpty(s)) sb.AppendLine($"{pad}    .{an} = {s}");
                        }
                    }
                    catch { }
                }
            }

            // Dynamizations (tag / expression bindings = the PLC link)
            DumpHmiSubComposition(obj, "Dynamizations", sb, depth);
            // Event handlers (scripts / system functions)
            DumpHmiSubComposition(obj, "EventHandlers", sb, depth);
            // Script code inside event handlers and script dynamizations (may hold hard-coded tag names)
            DumpScripts(obj, sb, depth);

            // For faceplate containers: dump the faceplate type and its interface assignments (tag links)
            if (type.Name.IndexOf("Faceplate", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                DumpFaceplateDetails(obj, sb, depth);
            }
        }

        // Dumps the script code held by an item's event handlers (EventHandler.Script.ScriptCode)
        // and any script dynamizations (ScriptDynamization.ScriptCode) - truncated for readability.
        private void DumpScripts(object obj, StringBuilder sb, int depth)
        {
            var pad = new string(' ', depth * 2);
            // event handler scripts
            foreach (var eh in Snapshot(obj.GetType().GetProperty("EventHandlers")?.GetValue(obj)))
            {
                var script = eh.GetType().GetProperty("Script")?.GetValue(eh);
                if (script == null) continue;
                foreach (var sp in new[] { "ScriptCode", "GlobalDefinitionAreaScriptCode" })
                {
                    var code = script.GetType().GetProperty(sp)?.GetValue(script) as string;
                    if (!string.IsNullOrWhiteSpace(code))
                        sb.AppendLine($"{pad}    [script:{eh.GetType().Name}.{sp}] {Trunc(code, 300)}");
                }
            }
            // script dynamizations
            foreach (var d in Snapshot(obj.GetType().GetProperty("Dynamizations")?.GetValue(obj)))
            {
                if (d.GetType().Name.IndexOf("Script", StringComparison.OrdinalIgnoreCase) < 0) continue;
                var code = d.GetType().GetProperty("ScriptCode")?.GetValue(d) as string;
                if (!string.IsNullOrWhiteSpace(code))
                    sb.AppendLine($"{pad}    [scriptDyn.ScriptCode] {Trunc(code, 300)}");
            }
        }

        private static string Trunc(string s, int n)
        {
            s = s.Replace("\r", " ").Replace("\n", " ");
            return s.Length <= n ? s : s.Substring(0, n) + " …";
        }

        // Dumps the faceplate type and interface property assignments of an HmiFaceplateContainer.
        // The interface assignments are how the faceplate's interface members are bound to HMI tags / values.
        private void DumpFaceplateDetails(object obj, StringBuilder sb, int depth)
        {
            var pad = new string(' ', depth * 2);
            var type = obj.GetType();

            // scalar properties that identify the faceplate type
            foreach (var pn in new[] { "ContainedType", "FaceplateType", "Faceplate", "Version", "AdaptName" })
            {
                try
                {
                    var v = type.GetProperty(pn)?.GetValue(obj);
                    if (v != null && !string.IsNullOrEmpty(v.ToString()))
                        sb.AppendLine($"{pad}    <faceplate> .{pn} = {v}");
                }
                catch { }
            }

            // interface assignments: enumerate any composition-like property and dump child name/value/tag
            foreach (var prop in type.GetProperties())
            {
                object? val;
                try { val = prop.GetValue(obj); } catch { continue; }
                if (val is not System.Collections.IEnumerable en || val is string) continue;
                if (prop.Name == "Dynamizations" || prop.Name == "EventHandlers" || prop.Name == "ScreenItems") continue;

                var rows = new List<string>();
                foreach (var c in en)
                {
                    if (c == null) continue;
                    var getAttr = c.GetType().GetMethod("GetAttribute", new[] { typeof(string) });
                    string? nm = null, tg = null, vl = null;
                    try { nm = getAttr?.Invoke(c, new object[] { "Name" })?.ToString(); } catch { }
                    try { tg = getAttr?.Invoke(c, new object[] { "Tag" })?.ToString(); } catch { }
                    try { vl = getAttr?.Invoke(c, new object[] { "Value" })?.ToString(); } catch { }
                    if (nm == null && tg == null && vl == null)
                    {
                        nm = c.GetType().GetProperty("Name")?.GetValue(c) as string;
                    }
                    if (nm != null || tg != null || vl != null)
                        rows.Add($"{pad}        {c.GetType().Name} Name={nm} Tag={tg} Value={vl}");
                }
                if (rows.Count > 0)
                {
                    sb.AppendLine($"{pad}    [{prop.Name}]");
                    foreach (var r in rows) sb.AppendLine(r);
                }
            }
        }

        private void DumpHmiSubComposition(object obj, string propName, StringBuilder sb, int depth)
        {
            try
            {
                var comp = obj.GetType().GetProperty(propName)?.GetValue(obj) as System.Collections.IEnumerable;
                if (comp == null) return;
                var pad = new string(' ', depth * 2);
                foreach (var c in comp)
                {
                    if (c == null) continue;
                    sb.AppendLine($"{pad}    [{propName}] {c.GetType().Name}");
                    var getAttr = c.GetType().GetMethod("GetAttribute", new[] { typeof(string) });
                    if (getAttr != null)
                    {
                        foreach (var an in new[] { "PropertyName", "Tag", "Expression", "Name", "SourceType", "DynamizationType" })
                        {
                            try
                            {
                                var v = getAttr.Invoke(c, new object[] { an });
                                if (v != null && !string.IsNullOrEmpty(v.ToString()))
                                    sb.AppendLine($"{pad}        .{an} = {v}");
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }
        }

        // Dumps a screen (or screen item) and recurses into its ScreenItems composition.
        private void DumpScreenObject(object obj, StringBuilder sb, int depth)
        {
            var itemsObj = obj.GetType().GetProperty("ScreenItems")?.GetValue(obj) as System.Collections.IEnumerable;
            if (itemsObj == null) return;
            foreach (var item in itemsObj)
            {
                if (item == null) continue;
                DumpHmiObject(item, sb, depth);
                // recurse: containers / faceplate instances may hold nested ScreenItems
                DumpScreenObject(item, sb, depth + 1);
            }
        }

        // Lists HMI tags (WinCC Unified) with their connection, PLC tag/address and data type.
        // Enumerates the default Tags collection plus every tag table (including those nested in tag table groups).
        public string GetHmiTags(string softwarePath, string regexName = "")
        {
            if (IsProjectNull()) return "ERROR: no project open";
            try
            {
                var software = GetSoftwareContainer(softwarePath)?.Software;
                if (software == null) return $"ERROR: HMI software not found: {softwarePath}";

                var sb = new StringBuilder();
                int count = 0;
                AppendHmiTags(software, "(default)", regexName, sb, ref count);
                foreach (var table in EnumerateTagTables(software))
                {
                    var tn = table.GetType().GetProperty("Name")?.GetValue(table) as string ?? "";
                    AppendHmiTags(table, tn, regexName, sb, ref count);
                }
                sb.Insert(0, $"HMI tags in '{softwarePath}' matching '{regexName}': {count}\r\n");
                return sb.ToString();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "GetHmiTags failed for {SoftwarePath}", softwarePath);
                return "ERROR: " + ex.Message;
            }
        }

        private IEnumerable<object> EnumerateTagTables(object container)
        {
            if (container.GetType().GetProperty("TagTables")?.GetValue(container) is System.Collections.IEnumerable tables)
            {
                foreach (var t in tables) { if (t != null) yield return t; }
            }
            foreach (var grpProp in new[] { "TagTableGroups", "Groups" })
            {
                if (container.GetType().GetProperty(grpProp)?.GetValue(container) is System.Collections.IEnumerable groups)
                {
                    foreach (var g in groups)
                    {
                        if (g == null) continue;
                        foreach (var t in EnumerateTagTables(g)) yield return t;
                    }
                }
            }
        }

        private void AppendHmiTags(object container, string tableName, string regexName, StringBuilder sb, ref int count)
        {
            if (container.GetType().GetProperty("Tags")?.GetValue(container) is not System.Collections.IEnumerable tags) return;
            foreach (var tag in tags)
            {
                if (tag == null) continue;
                var name = ReadHmiProp(tag, "Name");
                if (string.IsNullOrEmpty(name)) continue;
                if (!string.IsNullOrEmpty(regexName))
                {
                    try { if (!Regex.IsMatch(name, regexName, RegexOptions.IgnoreCase)) continue; }
                    catch { }
                }
                var conn = ReadHmiProp(tag, "Connection");
                var plcTag = ReadHmiProp(tag, "PlcTag");
                var addr = ReadHmiProp(tag, "Address");
                var dt = ReadHmiProp(tag, "DataType");
                sb.AppendLine($"[{tableName}] {name} | type={dt} | conn={conn} | plcTag={plcTag} | addr={addr}");
                count++;
            }
        }

        // Reads an HMI object property by reflection, returning a readable string (uses .Name for nested objects).
        private string ReadHmiProp(object obj, string propName)
        {
            try
            {
                var v = obj.GetType().GetProperty(propName)?.GetValue(obj);
                if (v == null) return "";
                var t = v.GetType();
                if (v is string s) return s;
                if (t.IsPrimitive || t.IsEnum) return v.ToString();
                var nm = t.GetProperty("Name")?.GetValue(v) as string;
                return nm ?? v.ToString();
            }
            catch { return ""; }
        }

        // Proof-of-concept: recreate (clone) a WinCC Unified screen by rebuilding its screen items
        // via the object model, since Openness offers no screen Copy/Export. Best-effort: copies item
        // type, attributes and Dynamizations; returns a coverage report so fidelity can be judged.
        public string CloneHmiScreen(string softwarePath, string srcName, string destName)
        {
            if (IsProjectNull()) return "ERROR: no project open";
            try
            {
                var software = GetSoftwareContainer(softwarePath)?.Software;
                if (software == null) return $"ERROR: HMI software not found: {softwarePath}";

                var src = FindHmiScreen(software, srcName);
                if (src == null) return $"ERROR: source screen not found: {srcName}";
                if (FindHmiScreen(software, destName) != null) return $"ERROR: destination screen already exists: {destName}";

                var screensComp = software.GetType().GetProperty("Screens")?.GetValue(software);
                var createScreen = screensComp?.GetType().GetMethod("Create", new[] { typeof(string) });
                if (createScreen == null) return "ERROR: cannot find Screens.Create(string)";
                var dest = createScreen.Invoke(screensComp, new object[] { destName });

                var rep = new CloneReport();
                CopyAttributes(src, dest, rep);            // screen-level attributes (Width, Height, background, ...)
                CloneScreenItems(src, dest, rep, 0);

                var sb = new StringBuilder();
                sb.AppendLine($"Cloned '{srcName}' -> '{destName}'");
                sb.AppendLine($"Items created: {rep.ItemsCreated}, item-create failures: {rep.ItemsFailed}");
                sb.AppendLine($"Attributes copied: {rep.AttrCopied}, skipped/failed: {rep.AttrFailed}");
                sb.AppendLine($"Dynamizations created: {rep.DynCreated}, failed: {rep.DynFailed}");
                if (rep.Notes.Count > 0)
                {
                    sb.AppendLine("Notes (first 40):");
                    foreach (var n in rep.Notes.Take(40)) sb.AppendLine("  - " + n);
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "CloneHmiScreen failed {Src}->{Dest}", srcName, destName);
                return "ERROR: " + ex.Message;
            }
        }

        private class CloneReport
        {
            public int ItemsCreated, ItemsFailed, AttrCopied, AttrFailed, DynCreated, DynFailed;
            public List<string> Notes = new List<string>();
            public void Note(string s) { if (Notes.Count < 200) Notes.Add(s); }
        }

        // Snapshots an Openness composition into a list, because holding a live enumerator across a
        // mutating Create() call disposes the enumerator ("Access to a disposed object ...").
        private static List<object> Snapshot(object enumerable)
        {
            var list = new List<object>();
            if (enumerable is System.Collections.IEnumerable e)
                foreach (var x in e) { if (x != null) list.Add(x); }
            return list;
        }

        private void CloneScreenItems(object srcContainer, object destContainer, CloneReport rep, int depth)
        {
            if (depth > 6) return;
            var srcItems = Snapshot(srcContainer.GetType().GetProperty("ScreenItems")?.GetValue(srcContainer));
            var destComp = destContainer.GetType().GetProperty("ScreenItems")?.GetValue(destContainer);
            if (srcItems.Count == 0 || destComp == null) return;

            // generic Create<T>(string name)
            var createGen = destComp.GetType().GetMethods()
                .FirstOrDefault(m => m.Name == "Create" && m.IsGenericMethodDefinition
                    && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));
            if (createGen == null) { rep.Note("no generic Create<T>(string) on ScreenItems"); return; }

            foreach (var item in srcItems)
            {
                if (item == null) continue;
                var itemType = item.GetType();
                var name = ReadHmiProp(item, "Name");
                object newItem;
                try
                {
                    newItem = createGen.MakeGenericMethod(itemType).Invoke(destComp, new object[] { name });
                    rep.ItemsCreated++;
                }
                catch (Exception ex)
                {
                    rep.ItemsFailed++;
                    rep.Note($"create {itemType.Name} '{name}' failed: {ex.InnerException?.Message ?? ex.Message}");
                    continue;
                }

                CopyAttributes(item, newItem, rep);
                CloneDynamizations(item, newItem, rep);
                // recurse into nested ScreenItems (containers)
                CloneScreenItems(item, newItem, rep, depth + 1);
            }
        }

        private static readonly HashSet<string> _cloneSkipAttrs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Name", "Parent" };

        private void CopyAttributes(object src, object dest, CloneReport rep)
        {
            var infosM = src.GetType().GetMethod("GetAttributeInfos", Type.EmptyTypes);
            if (infosM == null) return;
            object infosObj;
            try { infosObj = infosM.Invoke(src, null); } catch { return; }
            var infos = Snapshot(infosObj);

            var getA = src.GetType().GetMethod("GetAttribute", new[] { typeof(string) });
            var setA = dest.GetType().GetMethod("SetAttribute", new[] { typeof(string), typeof(object) });
            if (getA == null || setA == null) return;

            foreach (var info in infos)
            {
                if (info == null) continue;
                var an = info.GetType().GetProperty("Name")?.GetValue(info) as string;
                if (string.IsNullOrEmpty(an) || _cloneSkipAttrs.Contains(an)) continue;
                try
                {
                    var val = getA.Invoke(src, new object[] { an });
                    if (val == null) continue;
                    setA.Invoke(dest, new object[] { an, val });
                    rep.AttrCopied++;
                }
                catch (Exception ex)
                {
                    rep.AttrFailed++;
                    rep.Note($"attr '{an}' on {src.GetType().Name}: {ex.InnerException?.Message ?? ex.Message}");
                }
            }
        }

        private void CloneDynamizations(object src, object dest, CloneReport rep)
        {
            var srcDyn = Snapshot(src.GetType().GetProperty("Dynamizations")?.GetValue(src));
            var destDynComp = dest.GetType().GetProperty("Dynamizations")?.GetValue(dest);
            if (srcDyn.Count == 0 || destDynComp == null) return;

            var createGen = destDynComp.GetType().GetMethods()
                .FirstOrDefault(m => m.Name == "Create" && m.IsGenericMethodDefinition
                    && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));
            if (createGen == null) return;

            foreach (var d in srcDyn)
            {
                if (d == null) continue;
                var dtype = d.GetType();
                var propName = ReadHmiProp(d, "PropertyName");
                try
                {
                    var newD = createGen.MakeGenericMethod(dtype).Invoke(destDynComp, new object[] { propName });
                    CopyAttributes(d, newD, rep);
                    rep.DynCreated++;
                }
                catch (Exception ex)
                {
                    rep.DynFailed++;
                    rep.Note($"dyn {dtype.Name} on '{propName}': {ex.InnerException?.Message ?? ex.Message}");
                }
            }
        }

        // Finds an HMI tag table by name (searches default-less: tables + nested groups). Returns the table object or null.
        private object? FindHmiTagTable(object software, string tableName)
        {
            foreach (var t in EnumerateTagTables(software))
            {
                var n = t.GetType().GetProperty("Name")?.GetValue(t) as string;
                if (string.Equals(n, tableName, StringComparison.OrdinalIgnoreCase)) return t;
            }
            return null;
        }

        // Exports an HMI tag table's tags to a directory (WinCC Unified HmiTagComposition.Export).
        public string ExportHmiTags(string softwarePath, string tableName, string exportDir)
        {
            if (IsProjectNull()) return "ERROR: no project open";
            try
            {
                var software = GetSoftwareContainer(softwarePath)?.Software;
                if (software == null) return $"ERROR: HMI software not found: {softwarePath}";
                var table = FindHmiTagTable(software, tableName);
                if (table == null) return $"ERROR: tag table not found: {tableName}";
                var tagsComp = table.GetType().GetProperty("Tags")?.GetValue(table);
                if (tagsComp == null) return "ERROR: table has no Tags composition";
                if (!Directory.Exists(exportDir)) Directory.CreateDirectory(exportDir);
                var exportM = tagsComp.GetType().GetMethod("Export", new[] { typeof(DirectoryInfo) });
                if (exportM == null) return "ERROR: no Export(DirectoryInfo) on tag composition";
                exportM.Invoke(tagsComp, new object[] { new DirectoryInfo(exportDir) });
                return $"OK: exported tags of '{tableName}' to '{exportDir}'";
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ExportHmiTags failed");
                return "ERROR: " + (ex.InnerException?.Message ?? ex.Message);
            }
        }

        // Imports HMI tags into a tag table from a directory (WinCC Unified HmiTagComposition.Import).
        public string ImportHmiTags(string softwarePath, string tableName, string importDir)
        {
            if (IsProjectNull()) return "ERROR: no project open";
            try
            {
                var software = GetSoftwareContainer(softwarePath)?.Software;
                if (software == null) return $"ERROR: HMI software not found: {softwarePath}";
                var table = FindHmiTagTable(software, tableName);
                if (table == null) return $"ERROR: tag table not found: {tableName}";
                var tagsComp = table.GetType().GetProperty("Tags")?.GetValue(table);
                if (tagsComp == null) return "ERROR: table has no Tags composition";
                var importM = tagsComp.GetType().GetMethods()
                    .FirstOrDefault(m => m.Name == "Import" && m.GetParameters().Length >= 1
                        && m.GetParameters()[0].ParameterType == typeof(DirectoryInfo));
                if (importM == null) return "ERROR: no Import(DirectoryInfo) on tag composition";
                var pars = importM.GetParameters();
                var args = new object?[pars.Length];
                args[0] = new DirectoryInfo(importDir);
                for (int i = 1; i < pars.Length; i++)
                {
                    if (pars[i].ParameterType == typeof(ImportOptions)) args[i] = ImportOptions.Override;
                    else if (pars[i].HasDefaultValue) args[i] = pars[i].DefaultValue;
                    else args[i] = pars[i].ParameterType.IsValueType ? Activator.CreateInstance(pars[i].ParameterType) : null;
                }
                importM.Invoke(tagsComp, args);
                return $"OK: imported tags into '{tableName}' from '{importDir}'";
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ImportHmiTags failed");
                return "ERROR: " + (ex.InnerException?.Message ?? ex.Message);
            }
        }

        // Repoints tag bindings on a screen: in every screen item, replaces 'find' with 'replace' inside
        // TagDynamization.Tag values and faceplate interface Value entries. No item creation (safe).
        public string RepointScreenBindings(string softwarePath, string screenName, string find, string replace)
        {
            if (IsProjectNull()) return "ERROR: no project open";
            try
            {
                var software = GetSoftwareContainer(softwarePath)?.Software;
                if (software == null) return $"ERROR: HMI software not found: {softwarePath}";
                var screen = FindHmiScreen(software, screenName);
                if (screen == null) return $"ERROR: screen not found: {screenName}";
                var rep = new CloneReport();
                RepointItems(screen, find, replace, rep, 0);
                var sb = new StringBuilder();
                sb.AppendLine($"Repoint '{screenName}': '{find}' -> '{replace}'");
                sb.AppendLine($"Bindings changed: {rep.DynCreated}, failures: {rep.DynFailed}");
                foreach (var n in rep.Notes.Take(60)) sb.AppendLine("  " + n);
                return sb.ToString();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "RepointScreenBindings failed");
                return "ERROR: " + (ex.InnerException?.Message ?? ex.Message);
            }
        }

        private void RepointItems(object container, string find, string replace, CloneReport rep, int depth)
        {
            if (depth > 6) return;
            foreach (var item in Snapshot(container.GetType().GetProperty("ScreenItems")?.GetValue(container)))
            {
                // 1) plain tag dynamizations + script dynamizations
                foreach (var d in Snapshot(item.GetType().GetProperty("Dynamizations")?.GetValue(item)))
                {
                    RepointAttribute(d, "Tag", find, replace, rep);
                    RepointStringProp(d, "ScriptCode", find, replace, rep);
                }
                // 1b) event handler scripts (button clicks etc.) - Script.ScriptCode holds hard-coded tag writes / nav targets
                foreach (var eh in Snapshot(item.GetType().GetProperty("EventHandlers")?.GetValue(item)))
                {
                    var script = eh.GetType().GetProperty("Script")?.GetValue(eh);
                    if (script == null) continue;
                    RepointStringProp(script, "ScriptCode", find, replace, rep);
                    RepointStringProp(script, "GlobalDefinitionAreaScriptCode", find, replace, rep);
                }
                // 2) faceplate interface entries (Value holds the tag name)
                if (item.GetType().Name.IndexOf("Faceplate", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    foreach (var prop in item.GetType().GetProperties())
                    {
                        object? val; try { val = prop.GetValue(item); } catch { continue; }
                        if (val is not System.Collections.IEnumerable en || val is string) continue;
                        if (prop.Name is "Dynamizations" or "EventHandlers" or "ScreenItems") continue;
                        foreach (var entry in Snapshot(en))
                        {
                            RepointAttribute(entry, "Value", find, replace, rep);
                        }
                    }
                }
                // recurse
                RepointItems(item, find, replace, rep, depth + 1);
            }
        }

        private void RepointAttribute(object obj, string attrName, string find, string replace, CloneReport rep)
        {
            var getA = obj.GetType().GetMethod("GetAttribute", new[] { typeof(string) });
            var setA = obj.GetType().GetMethod("SetAttribute", new[] { typeof(string), typeof(object) });
            if (getA == null || setA == null) return;
            try
            {
                var cur = getA.Invoke(obj, new object[] { attrName }) as string;
                if (string.IsNullOrEmpty(cur) || !cur.Contains(find)) return;
                var next = cur.Replace(find, replace);
                setA.Invoke(obj, new object[] { attrName, next });
                rep.DynCreated++;
                rep.Note($"{obj.GetType().Name}.{attrName}: {cur} -> {next}");
            }
            catch (Exception ex)
            {
                rep.DynFailed++;
                rep.Note($"{obj.GetType().Name}.{attrName} failed: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        // Repoints a writable string PROPERTY (e.g. IHmiScript.ScriptCode, ScriptDynamization.ScriptCode)
        // by replacing 'find' with 'replace'. Used for button/event scripts that hold hard-coded tag names.
        private void RepointStringProp(object obj, string propName, string find, string replace, CloneReport rep)
        {
            try
            {
                var p = obj.GetType().GetProperty(propName);
                if (p == null || !p.CanRead || !p.CanWrite) return;
                if (p.GetValue(obj) is not string cur || string.IsNullOrEmpty(cur) || !cur.Contains(find)) return;
                p.SetValue(obj, cur.Replace(find, replace));
                rep.DynCreated++;
                rep.Note($"{obj.GetType().Name}.{propName}: repointed ({CountOcc(cur, find)}x)");
            }
            catch (Exception ex)
            {
                rep.DynFailed++;
                rep.Note($"{obj.GetType().Name}.{propName} failed: {ex.InnerException?.Message ?? ex.Message}");
            }
        }

        private static int CountOcc(string s, string sub)
        {
            int c = 0, i = 0;
            while ((i = s.IndexOf(sub, i, StringComparison.Ordinal)) >= 0) { c++; i += sub.Length; }
            return c;
        }

        #endregion

        #region plc tag tables

        public List<PlcTagTable> GetPlcTagTables(string softwarePath, string regexName = "")
        {
            _logger?.LogInformation("Getting PLC tag tables...");

            if (IsProjectNull())
            {
                return [];
            }

            var list = new List<PlcTagTable>();

            try
            {
                var softwareContainer = GetSoftwareContainer(softwarePath);
                if (softwareContainer?.Software is PlcSoftware plcSoftware)
                {
                    var group = plcSoftware?.TagTableGroup;

                    if (group != null)
                    {
                        GetPlcTagTablesRecursive(group, list, regexName);
                    }
                }
            }
            catch (Exception)
            {
                // Silently fail, return empty list
            }

            return list;
        }

        public List<PlcTag> GetPlcTags(string softwarePath, string tagTablePath)
        {
            _logger?.LogInformation($"Getting PLC tags from table: {tagTablePath}");

            if (IsProjectNull())
            {
                return [];
            }

            var list = new List<PlcTag>();

            try
            {
                var tagTable = GetPlcTagTableByPath(softwarePath, tagTablePath);
                if (tagTable != null)
                {
                    foreach (var tag in tagTable.Tags)
                    {
                        list.Add(tag);
                    }
                }
            }
            catch (Exception)
            {
                // Silently fail, return empty list
            }

            return list;
        }

        public PlcTagTable? ExportPlcTagTable(string softwarePath, string tagTablePath, string exportPath)
        {
            _logger?.LogInformation($"Exporting PLC tag table: {tagTablePath}");

            try
            {
                if (IsProjectNull())
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "No project is open in TIA Portal");
                }

                var tagTable = GetPlcTagTableByPath(softwarePath, tagTablePath);

                if (tagTable == null)
                {
                    throw new PortalException(PortalErrorCode.NotFound, "Tag table not found");
                }

                exportPath = Path.Combine(exportPath, $"{tagTable.Name}.xml");

                if (File.Exists(exportPath))
                {
                    File.Delete(exportPath);
                }

                tagTable.Export(new FileInfo(exportPath), ExportOptions.None);

                return tagTable;
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.ExportFailed, "Export failed", null, ex);

                if (!pex.Data.Contains("softwarePath")) pex.Data["softwarePath"] = softwarePath;
                if (!pex.Data.Contains("tagTablePath")) pex.Data["tagTablePath"] = tagTablePath;
                if (!pex.Data.Contains("exportPath")) pex.Data["exportPath"] = exportPath;

                _logger?.LogError(pex, "ExportPlcTagTable failed for {SoftwarePath} {TagTablePath} -> {ExportPath}", softwarePath, tagTablePath, exportPath);
                throw pex;
            }
        }

        public bool ImportPlcTagTable(string softwarePath, string groupPath, string importPath)
        {
            _logger?.LogInformation($"Importing PLC tag table from: {importPath}");

            if (IsProjectNull())
            {
                return false;
            }

            var softwareContainer = GetSoftwareContainer(softwarePath);
            if (softwareContainer?.Software is PlcSoftware plcSoftware)
            {
                var tagTableGroup = plcSoftware?.TagTableGroup;

                if (tagTableGroup != null)
                {
                    var group = GetPlcTagTableGroupByPath(softwarePath, groupPath);
                    if (group == null)
                    {
                        return false;
                    }

                    try
                    {
                        var fileInfo = new FileInfo(importPath);
                        if (fileInfo.Exists)
                        {
                            var list = group.TagTables.Import(fileInfo, ImportOptions.Override);
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

        #endregion

        #region libraries

        public List<string> GetLibraries()
        {
            _logger?.LogInformation("Getting libraries...");

            var list = new List<string>();

            if (IsProjectNull())
            {
                return list;
            }

            try
            {
                // Project library
                if (_project is Project project)
                {
                    var projLib = project.ProjectLibrary;
                    if (projLib != null)
                    {
                        list.Add($"[ProjectLibrary] {project.Name}");
                    }
                }

                // Global libraries
                if (_portal != null)
                {
                    foreach (var lib in _portal.GlobalLibraries)
                    {
                        list.Add($"[GlobalLibrary] {lib.Name}");
                    }
                }
            }
            catch (Exception)
            {
                // Silently fail
            }

            return list;
        }

        public List<string> GetLibraryMasterCopies(string libraryType, string folderPath = "")
        {
            _logger?.LogInformation($"Getting library master copies from: {libraryType}/{folderPath}");

            var list = new List<string>();

            if (IsProjectNull())
            {
                return list;
            }

            try
            {
                MasterCopyFolder? rootFolder = null;

                if (libraryType.Equals("project", StringComparison.OrdinalIgnoreCase) && _project is Project project)
                {
                    rootFolder = project.ProjectLibrary?.MasterCopyFolder;
                }
                else if (libraryType.Equals("global", StringComparison.OrdinalIgnoreCase) && _portal != null)
                {
                    foreach (var lib in _portal.GlobalLibraries)
                    {
                        rootFolder = lib.MasterCopyFolder;
                        break; // Use first global library
                    }
                }

                if (rootFolder == null)
                {
                    return list;
                }

                // Navigate to subfolder if specified
                var folder = rootFolder;
                if (!string.IsNullOrEmpty(folderPath))
                {
                    var segments = folderPath.Split(['/'], StringSplitOptions.RemoveEmptyEntries);
                    foreach (var seg in segments)
                    {
                        MasterCopyFolder? nextFolder = null;
                        foreach (var sub in folder.Folders)
                        {
                            if (sub.Name.Equals(seg, StringComparison.OrdinalIgnoreCase))
                            {
                                nextFolder = sub;
                                break;
                            }
                        }
                        if (nextFolder == null)
                        {
                            return list;
                        }
                        folder = nextFolder;
                    }
                }

                // List master copies
                GetMasterCopiesRecursive(folder, list, "");
            }
            catch (Exception)
            {
                // Silently fail
            }

            return list;
        }

        private void GetMasterCopiesRecursive(MasterCopyFolder folder, List<string> list, string prefix)
        {
            foreach (var mc in folder.MasterCopies)
            {
                list.Add(string.IsNullOrEmpty(prefix) ? mc.Name : $"{prefix}/{mc.Name}");
            }

            foreach (var sub in folder.Folders)
            {
                var subPrefix = string.IsNullOrEmpty(prefix) ? sub.Name : $"{prefix}/{sub.Name}";
                GetMasterCopiesRecursive(sub, list, subPrefix);
            }
        }

        public bool CopyFromLibrary(string softwarePath, string libraryType, string masterCopyPath, string targetGroupPath = "")
        {
            _logger?.LogInformation($"Copying from library: {libraryType}/{masterCopyPath} -> {softwarePath}/{targetGroupPath}");

            if (IsProjectNull())
            {
                return false;
            }

            try
            {
                MasterCopyFolder? rootFolder = null;

                if (libraryType.Equals("project", StringComparison.OrdinalIgnoreCase) && _project is Project project)
                {
                    rootFolder = project.ProjectLibrary?.MasterCopyFolder;
                }
                else if (libraryType.Equals("global", StringComparison.OrdinalIgnoreCase) && _portal != null)
                {
                    foreach (var lib in _portal.GlobalLibraries)
                    {
                        rootFolder = lib.MasterCopyFolder;
                        break;
                    }
                }

                if (rootFolder == null)
                {
                    return false;
                }

                // Navigate to master copy
                var segments = masterCopyPath.Split(['/'], StringSplitOptions.RemoveEmptyEntries);
                var folder = rootFolder;

                for (int i = 0; i < segments.Length - 1; i++)
                {
                    MasterCopyFolder? nextFolder = null;
                    foreach (var sub in folder.Folders)
                    {
                        if (sub.Name.Equals(segments[i], StringComparison.OrdinalIgnoreCase))
                        {
                            nextFolder = sub;
                            break;
                        }
                    }
                    if (nextFolder == null) return false;
                    folder = nextFolder;
                }

                MasterCopy? masterCopy = null;
                var mcName = segments[segments.Length - 1];
                foreach (var mc in folder.MasterCopies)
                {
                    if (mc.Name.Equals(mcName, StringComparison.OrdinalIgnoreCase))
                    {
                        masterCopy = mc;
                        break;
                    }
                }

                if (masterCopy == null) return false;

                // Get target block group
                var softwareContainer = GetSoftwareContainer(softwarePath);
                if (softwareContainer?.Software is PlcSoftware plcSoftware)
                {
                    PlcBlockGroup targetGroup;
                    if (string.IsNullOrEmpty(targetGroupPath))
                    {
                        targetGroup = plcSoftware.BlockGroup;
                    }
                    else
                    {
                        var group = GetPlcBlockGroupByPath(softwarePath, targetGroupPath);
                        if (group == null) return false;
                        targetGroup = group;
                    }

                    targetGroup.Blocks.CreateFrom(masterCopy);
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }

            return false;
        }

        #endregion

        #region network

        public List<Dictionary<string, string>> GetNetworkInterfaces(string devicePath)
        {
            _logger?.LogInformation($"Getting network interfaces for: {devicePath}");

            var list = new List<Dictionary<string, string>>();

            if (IsProjectNull())
            {
                return list;
            }

            try
            {
                var device = GetDeviceByPath(devicePath);
                if (device == null)
                {
                    return list;
                }

                GetNetworkInterfacesRecursive(device.DeviceItems, list);
            }
            catch (Exception)
            {
                // Silently fail
            }

            return list;
        }

        private void GetNetworkInterfacesRecursive(DeviceItemComposition items, List<Dictionary<string, string>> list)
        {
            foreach (DeviceItem item in items)
            {
                try
                {
                    var netInterface = item.GetService<NetworkInterface>();
                    if (netInterface != null)
                    {
                        foreach (var node in netInterface.Nodes)
                        {
                            var info = new Dictionary<string, string>
                            {
                                ["DeviceItem"] = item.Name,
                                ["NodeName"] = node.Name ?? ""
                            };

                            // Get address via node attributes
                            try
                            {
                                foreach (var attr in ((IEngineeringObject)node).GetAttributeInfos())
                                {
                                    try
                                    {
                                        var val = ((IEngineeringObject)node).GetAttribute(attr.Name);
                                        if (val != null)
                                        {
                                            info[attr.Name] = val.ToString() ?? "";
                                        }
                                    }
                                    catch (Exception) { }
                                }
                            }
                            catch (Exception) { }

                            if (node.ConnectedSubnet != null)
                            {
                                info["SubnetName"] = node.ConnectedSubnet.Name ?? "";
                            }

                            list.Add(info);
                        }
                    }
                }
                catch (Exception) { }

                // Recurse into sub-items
                if (item.DeviceItems != null && item.DeviceItems.Count > 0)
                {
                    GetNetworkInterfacesRecursive(item.DeviceItems, list);
                }
            }
        }

        public List<Dictionary<string, string>> GetSubnets()
        {
            _logger?.LogInformation("Getting subnets...");

            var list = new List<Dictionary<string, string>>();

            if (IsProjectNull())
            {
                return list;
            }

            try
            {
                if (_project is Project project)
                {
                    foreach (var subnet in project.Subnets)
                    {
                        var info = new Dictionary<string, string>
                        {
                            ["Name"] = subnet.Name ?? "",
                            ["TypeIdentifier"] = subnet.TypeIdentifier ?? ""
                        };

                        try
                        {
                            foreach (var attr in ((IEngineeringObject)subnet).GetAttributeInfos())
                            {
                                try
                                {
                                    var val = ((IEngineeringObject)subnet).GetAttribute(attr.Name);
                                    if (val != null)
                                    {
                                        info[attr.Name] = val.ToString() ?? "";
                                    }
                                }
                                catch (Exception) { }
                            }
                        }
                        catch (Exception) { }

                        list.Add(info);
                    }
                }
            }
            catch (Exception)
            {
                // Silently fail
            }

            return list;
        }

        #endregion

        public PlcBlock? ExportBlock(string softwarePath, string blockPath, string exportPath, bool preservePath = false)
        {
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

                // TIA Portal never exports inconsistent blocks
                if (!block.IsConsistent)
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "Block is inconsistent; TIA Portal does not export inconsistent blocks.");
                }

                if (File.Exists(exportPath))
                {
                    File.Delete(exportPath);
                }

                block.Export(new FileInfo(exportPath), ExportOptions.None);

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

        public PlcType? ExportType(string softwarePath, string typePath, string exportPath, bool preservePath = false)
        {
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

                if (File.Exists(exportPath))
                {
                    File.Delete(exportPath);
                }

                type.Export(new FileInfo(exportPath), ExportOptions.None);

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

        public bool ImportBlock(string softwarePath, string groupPath, string importPath)
        {
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
                            var list = group.Blocks.Import(fileInfo, ImportOptions.Override);
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

        public bool ImportType(string softwarePath, string groupPath, string importPath)
        {
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
                            var list = group.Types.Import(fileInfo, ImportOptions.Override);
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

        public IEnumerable<PlcBlock>? ExportBlocks(string softwarePath, string exportPath, string regexName = "", bool preservePath = false)
        {
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

                try
                {
                    if (!block.IsConsistent)
                    {
                        _logger?.LogWarning("Skipping inconsistent block {Name}", block.Name);

                        continue;
                    }

                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    if (File.Exists(path))
                    {
                        try { File.Delete(path); }
                        catch (Exception ioEx)
                        {
                            failures.Add($"{block.Name}: cannot delete existing file ({ioEx.Message})");
                            _logger?.LogError(ioEx, "Delete failed for {File}", path);

                            continue;
                        }
                    }

                    try
                    {
                        block.Export(new FileInfo(path), ExportOptions.None);
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

        public IEnumerable<PlcType>? ExportTypes(string softwarePath, string exportPath, string regexName = "", bool preservePath = false)
        {
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

                try
                {
                    if (!type.IsConsistent)
                    {
                        _logger?.LogWarning("Skipping inconsistent type {Name}", type.Name);
                        continue;
                    }

                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    if (File.Exists(path))
                    {
                        try
                        {
                            File.Delete(path);
                        }
                        catch (Exception ioEx)
                        {
                            failures.Add($"{type.Name}: cannot delete existing file ({ioEx.Message})");
                            _logger?.LogError(ioEx, "Delete failed for {File}", path);
                            continue;
                        }
                    }

                    try
                    {
                        type.Export(new FileInfo(path), ExportOptions.None);
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
        

        public bool ExportAsDocuments(string softwarePath, string blockPath, string exportPath, bool preservePath = false)
        {
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
                    throw new PortalException(PortalErrorCode.InvalidState, "ExportAsDocuments requires TIA Portal V20 or newer");
                }

                
                var softwareContainer = GetSoftwareContainer(softwarePath);
                if (softwareContainer?.Software is PlcSoftware plcSoftware)
                {
                    if (plcSoftware != null)
                    {
                        // Export code blocks as documents
                        // https://docs.tia.siemens.cloud/r/en-us/v20/creating-and-managing-blocks/exporting-and-importing-blocks-in-simatic-sd-format-s7-1200-s7-1500/exporting-and-importing-blocks-in-simatic-sd-format-s7-1200-s7-1500

                        var groupPath = blockPath.Contains("/") ? blockPath.Substring(0, blockPath.LastIndexOf("/")) : string.Empty;
                        var blockName = blockPath.Contains("/") ? blockPath.Substring(blockPath.LastIndexOf("/") + 1) : blockPath;

                        var group = GetPlcBlockGroupByPath(softwarePath, groupPath);

                        //group?.Blocks.ForEach(b => Console.WriteLine($"Block: {b.Name}, Type: {b.GetType().Name}"));

                        // join exportPath and groupPath
                        if (!Directory.Exists(exportPath))
                        {
                            Directory.CreateDirectory(exportPath);
                        }

                        if (preservePath && !string.IsNullOrEmpty(groupPath))
                        {
                            exportPath = Path.Combine(exportPath, groupPath);

                            if (!Directory.Exists(exportPath))
                            {
                                Directory.CreateDirectory(exportPath);
                            }
                        }

                        try
                        {
                            // delete files s7dcl/s7res if already exists
                            var blockFiles7dclPath = Path.Combine(exportPath, $"{blockName}.s7dcl");
                            if (File.Exists(blockFiles7dclPath))
                            {
                                File.Delete(blockFiles7dclPath);
                            }
                            var blockFiles7resPath = Path.Combine(exportPath, $"{blockName}.s7res");
                            if (File.Exists(blockFiles7resPath))
                            {
                                File.Delete(blockFiles7resPath);
                            }

                            var result = group?.Blocks.Find(blockName)?.ExportAsDocuments(new DirectoryInfo(exportPath), blockName);

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
        public IEnumerable<PlcBlock>? ExportBlocksAsDocuments(string softwarePath, string exportPath, string regexName = "", bool preservePath = false)
        {
            _logger?.LogInformation("Exporting blocks as documents...");

            if (IsProjectNull())
            {
                return null;
            }

            if (Engineering.TiaMajorVersion < 20)
            {
                _logger?.LogWarning("ExportBlocksAsDocuments is only supported on TIA Portal V20 or newer");
                return null;
            }

            var exportList = new List<PlcBlock>();
            var failures = new List<string>();

            PlcBlock[] list;
            try
            {
                list = GetBlocks(softwarePath, regexName).ToArray();
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

                try
                {
                    if (!Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }
                }
                catch (Exception ex)
                {
                    failures.Add($"{block.Name}: cannot create directory '{targetDir}' ({ex.Message})");
                    _logger?.LogError(ex, $"Directory creation failed for {targetDir}");
                    continue;
                }

                var fileDcl = Path.Combine(targetDir, $"{block.Name}.s7dcl");
                var fileRes = Path.Combine(targetDir, $"{block.Name}.s7res");

                // Clean previous artifacts
                foreach (var f in new[] { fileDcl, fileRes })
                {
                    try
                    {
                        if (File.Exists(f))
                        {
                            File.Delete(f);
                        }
                    }
                    catch (Exception ex)
                    {
                        failures.Add($"{block.Name}: cannot delete existing '{Path.GetFileName(f)}' ({ex.Message})");
                        _logger?.LogError(ex, $"Failed deleting existing file {f}");
                        // Continue anyway; export might overwrite.
                    }
                }

                try
                {
                    DocumentExportResult? result = null;
                    try
                    {
                        result = block.ExportAsDocuments(new DirectoryInfo(targetDir), block.Name);
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
            _logger?.LogInformation($"Importing block from documents: {fileNameWithoutExtension} in {importPath}");

            if (IsProjectNull())
            {
                return false;
            }

            if (Engineering.TiaMajorVersion < 20)
            {
                _logger?.LogWarning("ImportFromDocuments is only supported on TIA Portal V20 or newer");
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
            _logger?.LogInformation($"Importing blocks from documents in {importPath} with regex '{regexName}'");

            if (IsProjectNull())
            {
                return null;
            }

            if (Engineering.TiaMajorVersion < 20)
            {
                _logger?.LogWarning("ImportBlocksFromDocuments is only supported on TIA Portal V20 or newer");
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
                        : new Regex(regexName, RegexOptions.Compiled);

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
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error importing blocks from documents");
            }

            return imported;
        }

        #endregion

        #region private helper

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
            if (Engineering.TiaMajorVersion >= 19)
                TryGetUnifiedSoftware(sb, deviceItem, ancestorStates, softwareContainer, hasSoftware);
        }

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
            if (_project == null) return null;

            var softwareContainer = GetSoftwareContainer(softwarePath);
            if (softwareContainer?.Software is PlcSoftware plcSoftware)
            {
                if (plcSoftware?.BlockGroup == null) return null;
                if (string.IsNullOrEmpty(groupPath)) return plcSoftware.BlockGroup;
                return FindBlockGroupByPath(plcSoftware.BlockGroup, groupPath);
            }
            return null;
        }

        private PlcBlockGroup? FindBlockGroupByPath(PlcBlockGroup parent, string remainingPath)
        {
            if (string.IsNullOrEmpty(remainingPath)) return parent;

            PlcBlockGroup? bestMatch = null;
            string? bestRemaining = null;

            foreach (PlcBlockGroup child in parent.Groups)
            {
                if (remainingPath.Equals(child.Name, StringComparison.OrdinalIgnoreCase))
                    return child;

                if (remainingPath.StartsWith(child.Name + "/", StringComparison.OrdinalIgnoreCase))
                {
                    var subPath = remainingPath.Substring(child.Name.Length + 1);
                    if (bestMatch == null || child.Name.Length > bestMatch.Name.Length)
                    {
                        bestMatch = child;
                        bestRemaining = subPath;
                    }
                }
            }

            if (bestMatch != null)
                return FindBlockGroupByPath(bestMatch, bestRemaining!);

            return null;
        }

        private PlcTypeGroup? GetPlcTypeGroupByPath(string softwarePath, string groupPath)
        {
            if (_project == null) return null;

            var softwareContainer = GetSoftwareContainer(softwarePath);
            if (softwareContainer?.Software is PlcSoftware plcSoftware)
            {
                if (plcSoftware?.TypeGroup == null) return null;
                if (string.IsNullOrEmpty(groupPath)) return plcSoftware.TypeGroup;
                return FindTypeGroupByPath(plcSoftware.TypeGroup, groupPath);
            }
            return null;
        }

        private PlcTypeGroup? FindTypeGroupByPath(PlcTypeGroup parent, string remainingPath)
        {
            if (string.IsNullOrEmpty(remainingPath)) return parent;

            PlcTypeGroup? bestMatch = null;
            string? bestRemaining = null;

            foreach (PlcTypeGroup child in parent.Groups)
            {
                if (remainingPath.Equals(child.Name, StringComparison.OrdinalIgnoreCase))
                    return child;

                if (remainingPath.StartsWith(child.Name + "/", StringComparison.OrdinalIgnoreCase))
                {
                    var subPath = remainingPath.Substring(child.Name.Length + 1);
                    if (bestMatch == null || child.Name.Length > bestMatch.Name.Length)
                    {
                        bestMatch = child;
                        bestRemaining = subPath;
                    }
                }
            }

            if (bestMatch != null)
                return FindTypeGroupByPath(bestMatch, bestRemaining!);

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

        private PlcTagTableGroup? GetPlcTagTableGroupByPath(string softwarePath, string groupPath)
        {
            if (_project == null)
            {
                return null;
            }

            var softwareContainer = GetSoftwareContainer(softwarePath);
            if (softwareContainer?.Software is PlcSoftware plcSoftware)
            {
                if (plcSoftware?.TagTableGroup == null)
                {
                    return null;
                }

                if (string.IsNullOrEmpty(groupPath))
                {
                    return plcSoftware.TagTableGroup;
                }

                var groupNames = groupPath.Split(['/'], StringSplitOptions.RemoveEmptyEntries);

                PlcTagTableUserGroup? currentGroup = null;

                // First level: search in TagTableGroup.Groups
                var firstGroupName = groupNames[0];
                foreach (PlcTagTableUserGroup g in plcSoftware.TagTableGroup.Groups)
                {
                    if (g.Name.Equals(firstGroupName, StringComparison.OrdinalIgnoreCase))
                    {
                        currentGroup = g;
                        break;
                    }
                }

                if (currentGroup == null)
                {
                    return null;
                }

                // Subsequent levels
                for (int i = 1; i < groupNames.Length; i++)
                {
                    PlcTagTableUserGroup? nextGroup = null;
                    foreach (PlcTagTableUserGroup g in currentGroup.Groups)
                    {
                        if (g.Name.Equals(groupNames[i], StringComparison.OrdinalIgnoreCase))
                        {
                            nextGroup = g;
                            break;
                        }
                    }

                    if (nextGroup == null)
                    {
                        return null;
                    }

                    currentGroup = nextGroup;
                }

                return currentGroup;
            }

            return null;
        }

        private PlcTagTable? GetPlcTagTableByPath(string softwarePath, string tagTablePath)
        {
            if (_project == null)
            {
                return null;
            }

            // Split path: last segment is table name, rest is group path
            var segments = tagTablePath.Split(['/'], StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return null;
            }

            var tableName = segments[segments.Length - 1];

            var softwareContainer = GetSoftwareContainer(softwarePath);
            if (softwareContainer?.Software is PlcSoftware plcSoftware)
            {
                if (plcSoftware?.TagTableGroup == null)
                {
                    return null;
                }

                if (segments.Length == 1)
                {
                    // Search in root tag table group
                    foreach (var table in plcSoftware.TagTableGroup.TagTables)
                    {
                        if (table.Name.Equals(tableName, StringComparison.OrdinalIgnoreCase))
                        {
                            return table;
                        }
                    }
                    return null;
                }

                // Navigate to subgroup, then find table
                var groupPath = string.Join("/", segments, 0, segments.Length - 1);
                var group = GetPlcTagTableGroupByPath(softwarePath, groupPath);
                if (group is PlcTagTableUserGroup userGroup)
                {
                    foreach (var table in userGroup.TagTables)
                    {
                        if (table.Name.Equals(tableName, StringComparison.OrdinalIgnoreCase))
                        {
                            return table;
                        }
                    }
                }
            }

            return null;
        }

        #endregion

        #region GetRecursive ...

        private bool GetDevicesRecursive(DeviceUserGroup group, List<Device> list, string regexName = "")
        {
            var anySuccess = false;

            foreach (var composition in group.Devices)
            {
                if (composition is Device device)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(regexName) && !Regex.IsMatch(device.Name, regexName, RegexOptions.IgnoreCase))
                        {
                            continue; // Skip this device if it doesn't match the pattern
                        }
                    }
                    catch (Exception)
                    {
                        // Invalid regex pattern, skip this device
                        continue;
                    }

                    list.Add(device);

                    anySuccess = true;
                }
            }

            foreach (var subgroup in group.Groups)
            {
                anySuccess = GetDevicesRecursive(subgroup, list, regexName);
            }

            return anySuccess;
        }

        private bool GetBlocksRecursive(PlcBlockGroup group, List<PlcBlock> list, string regexName = "")
        {
            var anySuccess = false;

            foreach (var composition in group.Blocks)
            {
                if (composition is PlcBlock block)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(regexName) && !Regex.IsMatch(block.Name, regexName, RegexOptions.IgnoreCase))
                        {
                            continue; // Skip this block if it doesn't match the pattern
                        }
                    }
                    catch (Exception)
                    {
                        // Invalid regex pattern, skip this block
                        continue;
                    }

                    list.Add(block);

                    anySuccess = true;
                }
            }

            foreach (var subgroup in group.Groups)
            {
                anySuccess = GetBlocksRecursive(subgroup, list, regexName);
            }

            return anySuccess;
        }

        private bool GetTypesRecursive(PlcTypeGroup group, List<PlcType> list, string regexName = "")
        {
            var anySuccess = false;

            foreach (var composition in group.Types)
            {
                if (composition is PlcType type)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(regexName) && !Regex.IsMatch(type.Name, regexName, RegexOptions.IgnoreCase))
                        {
                            continue; // Skip this block if it doesn't match the pattern
                        }
                    }
                    catch (Exception)
                    {
                        // Invalid regex pattern, skip this block
                        continue;
                    }

                    list.Add(type);

                    anySuccess = true;
                }

            }

            foreach (PlcTypeGroup subgroup in group.Groups)
            {
                anySuccess = GetTypesRecursive(subgroup, list, regexName);
            }

            return anySuccess;
        }

        private bool GetPlcTagTablesRecursive(PlcTagTableGroup group, List<PlcTagTable> list, string regexName = "")
        {
            var anySuccess = false;

            foreach (var table in group.TagTables)
            {
                try
                {
                    if (!string.IsNullOrEmpty(regexName) && !Regex.IsMatch(table.Name, regexName, RegexOptions.IgnoreCase))
                    {
                        continue;
                    }
                }
                catch (Exception)
                {
                    continue;
                }

                list.Add(table);
                anySuccess = true;
            }

            foreach (PlcTagTableUserGroup subgroup in group.Groups)
            {
                anySuccess = GetPlcTagTablesRecursive(subgroup, list, regexName);
            }

            return anySuccess;
        }

        private bool GetPlcTagTablesRecursive(PlcTagTableUserGroup group, List<PlcTagTable> list, string regexName = "")
        {
            var anySuccess = false;

            foreach (var table in group.TagTables)
            {
                try
                {
                    if (!string.IsNullOrEmpty(regexName) && !Regex.IsMatch(table.Name, regexName, RegexOptions.IgnoreCase))
                    {
                        continue;
                    }
                }
                catch (Exception)
                {
                    continue;
                }

                list.Add(table);
                anySuccess = true;
            }

            foreach (PlcTagTableUserGroup subgroup in group.Groups)
            {
                anySuccess = GetPlcTagTablesRecursive(subgroup, list, regexName);
            }

            return anySuccess;
        }

        #endregion

        #endregion

        #region hardware & network write

        public Device AddDevice(string typeIdentifier, string deviceName, string name)
        {
            _logger?.LogInformation($"Adding device: {deviceName} ({typeIdentifier})");

            if (IsProjectNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, "No project is open");
            }

            if (_project is not Project project)
            {
                throw new PortalException(PortalErrorCode.InvalidState, "Cannot add device to multiuser session");
            }

            try
            {
                var device = project.Devices.CreateWithItem(typeIdentifier, name, deviceName);
                _logger?.LogInformation($"Device '{deviceName}' added successfully");
                return device;
            }
            catch (Exception ex)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"Failed to add device '{deviceName}': {ex.Message}");
            }
        }

        public void RemoveDevice(string devicePath)
        {
            _logger?.LogInformation($"Removing device: {devicePath}");

            if (IsProjectNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, "No project is open");
            }

            var device = GetDeviceByPath(devicePath);
            if (device == null)
            {
                throw new PortalException(PortalErrorCode.NotFound, $"Device '{devicePath}' not found");
            }

            try
            {
                device.Delete();
                _logger?.LogInformation($"Device '{devicePath}' removed successfully");
            }
            catch (Exception ex)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"Failed to remove device '{devicePath}': {ex.Message}");
            }
        }

        public Subnet CreateSubnet(string typeIdentifier, string name)
        {
            _logger?.LogInformation($"Creating subnet: {name} ({typeIdentifier})");

            if (IsProjectNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, "No project is open");
            }

            if (_project is not Project project)
            {
                throw new PortalException(PortalErrorCode.InvalidState, "Cannot create subnet in multiuser session");
            }

            try
            {
                var subnet = project.Subnets.Create(typeIdentifier, name);
                _logger?.LogInformation($"Subnet '{name}' created successfully");
                return subnet;
            }
            catch (Exception ex)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"Failed to create subnet '{name}': {ex.Message}");
            }
        }

        public void ConnectToSubnet(string devicePath, string interfaceName, string subnetName)
        {
            _logger?.LogInformation($"Connecting {devicePath}/{interfaceName} to subnet {subnetName}");

            if (IsProjectNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, "No project is open");
            }

            if (_project is not Project project)
            {
                throw new PortalException(PortalErrorCode.InvalidState, "Cannot connect to subnet in multiuser session");
            }

            // Find the subnet
            Subnet? subnet = null;
            foreach (var s in project.Subnets)
            {
                if (s.Name == subnetName)
                {
                    subnet = s;
                    break;
                }
            }
            if (subnet == null)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"Subnet '{subnetName}' not found");
            }

            // Find the device and interface
            var device = GetDeviceByPath(devicePath);
            if (device == null)
            {
                throw new PortalException(PortalErrorCode.NotFound, $"Device '{devicePath}' not found");
            }

            try
            {
                var node = FindNetworkNode(device.DeviceItems, interfaceName);
                if (node == null)
                {
                    throw new PortalException(PortalErrorCode.InvalidState, $"Network interface '{interfaceName}' not found on device '{devicePath}'");
                }

                node.ConnectToSubnet(subnet);
                _logger?.LogInformation($"Connected '{devicePath}/{interfaceName}' to subnet '{subnetName}'");
            }
            catch (PortalException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"Failed to connect to subnet: {ex.Message}");
            }
        }

        private Node? FindNetworkNode(DeviceItemComposition items, string interfaceName)
        {
            foreach (DeviceItem item in items)
            {
                try
                {
                    var netInterface = item.GetService<NetworkInterface>();
                    if (netInterface != null)
                    {
                        foreach (var node in netInterface.Nodes)
                        {
                            if (item.Name == interfaceName || node.Name == interfaceName)
                            {
                                return node;
                            }
                        }
                    }
                }
                catch (Exception) { }

                // Recurse
                if (item.DeviceItems != null && item.DeviceItems.Count > 0)
                {
                    var result = FindNetworkNode(item.DeviceItems, interfaceName);
                    if (result != null) return result;
                }
            }
            return null;
        }

        public void SetNetworkAttribute(string devicePath, string interfaceName, string attributeName, string attributeValue)
        {
            _logger?.LogInformation($"Setting {attributeName}={attributeValue} on {devicePath}/{interfaceName}");

            if (IsProjectNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, "No project is open");
            }

            var device = GetDeviceByPath(devicePath);
            if (device == null)
            {
                throw new PortalException(PortalErrorCode.NotFound, $"Device '{devicePath}' not found");
            }

            try
            {
                var node = FindNetworkNode(device.DeviceItems, interfaceName);
                if (node == null)
                {
                    throw new PortalException(PortalErrorCode.InvalidState, $"Network interface '{interfaceName}' not found on device '{devicePath}'");
                }

                ((IEngineeringObject)node).SetAttribute(attributeName, attributeValue);
                _logger?.LogInformation($"Set {attributeName}={attributeValue} on '{devicePath}/{interfaceName}'");
            }
            catch (PortalException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"Failed to set attribute: {ex.Message}");
            }
        }

        #endregion

        #region download & online (Phase 6)

        public Dictionary<string, string> DownloadToDevice(string softwarePath)
        {
            _logger?.LogInformation($"Downloading to device for: {softwarePath}");

            if (IsProjectNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, "No project is open");
            }

            var softwareContainer = GetSoftwareContainer(softwarePath);
            if (softwareContainer == null)
            {
                throw new PortalException(PortalErrorCode.NotFound, $"Software not found at '{softwarePath}'");
            }

            try
            {
                if (softwareContainer.Software is not PlcSoftware plcSoftware)
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "Software is not PLC software");
                }

                var downloadProvider = plcSoftware.GetService<DownloadProvider>();
                if (downloadProvider == null)
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "Download provider not available for this software");
                }

                // Configure download: accept all default options
                DownloadConfigurationDelegate preDownloadConfig = delegate (DownloadConfiguration config)
                {
                    // Accept defaults — no changes needed
                };

                // Use the simple Download overload with DirectoryInfo (temp dir for download staging)
                var tempDir = new DirectoryInfo(Path.GetTempPath());
                var result = downloadProvider.Download(tempDir, preDownloadConfig);

                var info = new Dictionary<string, string>
                {
                    ["State"] = result.State.ToString(),
                    ["WarningCount"] = result.WarningCount.ToString(),
                    ["ErrorCount"] = result.ErrorCount.ToString()
                };

                foreach (DownloadResultMessage msg in result.Messages)
                {
                    try
                    {
                        info[$"Message_{msg.DateTime:HHmmss}"] = msg.Message;
                    }
                    catch (Exception) { }
                }

                return info;
            }
            catch (PortalException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"Download failed: {ex.Message}");
            }
        }

        public Dictionary<string, string> GoOnline(string softwarePath)
        {
            _logger?.LogInformation($"Going online for: {softwarePath}");

            if (IsProjectNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, "No project is open");
            }

            var softwareContainer = GetSoftwareContainer(softwarePath);
            if (softwareContainer == null)
            {
                throw new PortalException(PortalErrorCode.NotFound, $"Software not found at '{softwarePath}'");
            }

            try
            {
                if (softwareContainer.Software is not PlcSoftware plcSoftwareOnline)
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "Software is not PLC software");
                }

                var onlineProvider = plcSoftwareOnline.GetService<OnlineProvider>();
                if (onlineProvider == null)
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "Online provider not available for this software");
                }

                onlineProvider.GoOnline();

                var info = new Dictionary<string, string>
                {
                    ["State"] = onlineProvider.State.ToString()
                };

                return info;
            }
            catch (PortalException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"Go online failed: {ex.Message}");
            }
        }

        public void GoOffline(string softwarePath)
        {
            _logger?.LogInformation($"Going offline for: {softwarePath}");

            if (IsProjectNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, "No project is open");
            }

            var softwareContainer = GetSoftwareContainer(softwarePath);
            if (softwareContainer == null)
            {
                throw new PortalException(PortalErrorCode.NotFound, $"Software not found at '{softwarePath}'");
            }

            try
            {
                if (softwareContainer.Software is not PlcSoftware plcSoftwareOff)
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "Software is not PLC software");
                }

                var onlineProviderOff = plcSoftwareOff.GetService<OnlineProvider>();
                if (onlineProviderOff == null)
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "Online provider not available for this software");
                }

                onlineProviderOff.GoOffline();
            }
            catch (PortalException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"Go offline failed: {ex.Message}");
            }
        }

        #endregion

        #region safety (Phase 7)

        public Dictionary<string, string> GetSafetyInfo(string softwarePath)
        {
            _logger?.LogInformation($"Getting safety info for: {softwarePath}");

            if (IsProjectNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, "No project is open");
            }

            var softwareContainer = GetSoftwareContainer(softwarePath);
            if (softwareContainer == null)
            {
                throw new PortalException(PortalErrorCode.NotFound, $"Software not found at '{softwarePath}'");
            }

            var info = new Dictionary<string, string>();

            try
            {
                var deviceItem = softwareContainer.Parent as DeviceItem;
                var admin = deviceItem?.GetService<SafetyAdministration>();

                if (admin == null)
                {
                    info["SafetySupported"] = "false";
                    return info;
                }

                info["SafetySupported"] = "true";
                info["IsLoggedOn"] = admin.IsLoggedOnToSafetyOfflineProgram.ToString();

                try
                {
                    foreach (var attr in ((IEngineeringObject)admin).GetAttributeInfos())
                    {
                        try
                        {
                            var val = ((IEngineeringObject)admin).GetAttribute(attr.Name);
                            if (val != null)
                            {
                                info[attr.Name] = val.ToString() ?? "";
                            }
                        }
                        catch (Exception) { }
                    }
                }
                catch (Exception) { }
            }
            catch (PortalException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"Failed to get safety info: {ex.Message}");
            }

            return info;
        }

        public Dictionary<string, string> CompileSafety(string softwarePath, string password)
        {
            _logger?.LogInformation($"Compiling safety for: {softwarePath}");

            if (IsProjectNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, "No project is open");
            }

            var softwareContainer = GetSoftwareContainer(softwarePath);
            if (softwareContainer == null)
            {
                throw new PortalException(PortalErrorCode.NotFound, $"Software not found at '{softwarePath}'");
            }

            try
            {
                var deviceItem = softwareContainer.Parent as DeviceItem;
                var admin = deviceItem?.GetService<SafetyAdministration>();

                if (admin == null)
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "Safety administration not available for this device");
                }

                // Login if needed
                if (!admin.IsLoggedOnToSafetyOfflineProgram && !string.IsNullOrEmpty(password))
                {
                    SecureString secString = new NetworkCredential("", password).SecurePassword;
                    admin.LoginToSafetyOfflineProgram(secString);
                }

                // Compile
                if (softwareContainer.Software is not PlcSoftware plcSoftwareSafety)
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "Software is not PLC software");
                }
                var compilable = plcSoftwareSafety.GetService<ICompilable>();
                if (compilable == null)
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "Software is not compilable");
                }

                var result = compilable.Compile();

                var info = new Dictionary<string, string>
                {
                    ["State"] = result.State.ToString(),
                    ["WarningCount"] = result.WarningCount.ToString(),
                    ["ErrorCount"] = result.ErrorCount.ToString()
                };

                return info;
            }
            catch (PortalException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"Safety compilation failed: {ex.Message}");
            }
        }

        #endregion

        #region hardware catalog (Phase 8)

        public List<Dictionary<string, string>> SearchHardwareCatalog(string searchText)
        {
            _logger?.LogInformation($"Searching hardware catalog for: {searchText}");

            var list = new List<Dictionary<string, string>>();

            if (IsProjectNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, "No project is open");
            }

            if (_project is not Project project)
            {
                throw new PortalException(PortalErrorCode.InvalidState, "Cannot access hardware catalog in multiuser session");
            }

            try
            {
                // In V20 Openness, devices already in the project can be listed.
                // For catalog search, we enumerate existing devices and their TypeIdentifiers.
                // The HardwareCatalog class in V20 requires browsing via attributes.
                foreach (var device in project.Devices)
                {
                    try
                    {
                        var name = device.Name ?? "";
                        var typeId = device.TypeIdentifier ?? "";

                        if (name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            typeId.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            list.Add(new Dictionary<string, string>
                            {
                                ["Name"] = name,
                                ["TypeIdentifier"] = typeId,
                                ["Source"] = "Project"
                            });
                        }
                    }
                    catch (Exception) { }
                }

                // Try to access HardwareCatalog if available
                try
                {
                    foreach (var utility in project.HwUtilities)
                    {
                        try
                        {
                            var utilName = ((IEngineeringObject)utility).GetAttribute("Name")?.ToString() ?? "";
                            var utilType = utility.GetType().Name;

                            if (utilName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                list.Add(new Dictionary<string, string>
                                {
                                    ["Name"] = utilName,
                                    ["Type"] = utilType,
                                    ["Source"] = "HwUtilities"
                                });
                            }
                        }
                        catch (Exception) { }
                    }
                }
                catch (Exception) { }
            }
            catch (PortalException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new PortalException(PortalErrorCode.InvalidState, $"Failed to search hardware catalog: {ex.Message}");
            }

            return list;
        }

        #endregion

    }


}
