﻿/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Project.ImportWizard {
    internal class ImportSettings : DependencyObject {
        private readonly IInterpreterOptionsService _service;
        private bool _isAutoGeneratedProjectPath;

        private static readonly PythonInterpreterView _defaultInterpreter = new PythonInterpreterView(
            "(Global default or an auto-detected virtual environment)",
            Guid.Empty,
            new Version(),
            null
        );

        private static readonly IList<ProjectCustomization> _projectCustomizations = new [] {
            BottleProjectCustomization.Instance,
            DjangoProjectCustomization.Instance,
            FlaskProjectCustomization.Instance,
            GenericWebProjectCustomization.Instance
        };

        public ImportSettings()
            : this(PythonToolsPackage.ComponentModel.GetService<IInterpreterOptionsService>()) {
        }

        public ImportSettings(IInterpreterOptionsService service) {
            _service = service;

            if (_service != null) {
                AvailableInterpreters = new ObservableCollection<PythonInterpreterView>(
                    Enumerable.Repeat(_defaultInterpreter, 1)
                    .Concat(_service.Interpreters.Select(fact => new PythonInterpreterView(fact)))
                );
            } else {
                AvailableInterpreters = new ObservableCollection<PythonInterpreterView>();
                AvailableInterpreters.Add(_defaultInterpreter);
            }

            SelectedInterpreter = AvailableInterpreters[0];
            TopLevelPythonFiles = new BulkObservableCollection<string>();
            Customization = _projectCustomizations.First();

            Filters = "*.pyw;*.txt;*.htm;*.html;*.css;*.djt;*.js;*.ini;*.png;*.jpg;*.gif;*.bmp;*.ico;*.svg";
        }

        private static string MakeSafePath(string path) {
            if (string.IsNullOrEmpty(path)) {
                return null;
            }
            var safePath = path.Trim(' ', '"');
            if (CommonUtils.IsValidPath(safePath)) {
                return safePath;
            }
            return null;
        }

        public string ProjectPath {
            get { return MakeSafePath((string)GetValue(ProjectPathProperty)); }
            set { SetValue(ProjectPathProperty, value); }
        }

        public string SourcePath {
            get { return MakeSafePath((string)GetValue(SourcePathProperty)); }
            set { SetValue(SourcePathProperty, value); }
        }

        public string Filters {
            get { return (string)GetValue(FiltersProperty); }
            set { SetValue(FiltersProperty, value); }
        }

        public string SearchPaths {
            get { return (string)GetValue(SearchPathsProperty); }
            set { SetValue(SearchPathsProperty, value); }
        }

        public ObservableCollection<PythonInterpreterView> AvailableInterpreters {
            get { return (ObservableCollection<PythonInterpreterView>)GetValue(AvailableInterpretersProperty); }
            set { SetValue(AvailableInterpretersPropertyKey, value); }
        }

        public PythonInterpreterView SelectedInterpreter {
            get { return (PythonInterpreterView)GetValue(SelectedInterpreterProperty); }
            set { SetValue(SelectedInterpreterProperty, value); }
        }

        public ObservableCollection<string> TopLevelPythonFiles {
            get { return (ObservableCollection<string>)GetValue(TopLevelPythonFilesProperty); }
            private set { SetValue(TopLevelPythonFilesPropertyKey, value); }
        }

        public string StartupFile {
            get { return (string)GetValue(StartupFileProperty); }
            set { SetValue(StartupFileProperty, value); }
        }

        public IEnumerable<ProjectCustomization> SupportedProjectCustomizations {
            get {
                return _projectCustomizations;
            }
        }

        public bool UseCustomization {
            get { return (bool)GetValue(UseCustomizationProperty); }
            set { SetValue(UseCustomizationProperty, value); }
        }

        public ProjectCustomization Customization {
            get { return (ProjectCustomization)GetValue(CustomizationProperty); }
            set { SetValue(CustomizationProperty, value); }
        }

        public bool DetectVirtualEnv {
            get { return (bool)GetValue(DetectVirtualEnvProperty); }
            set { SetValue(DetectVirtualEnvProperty, value); }
        }

        public static readonly DependencyProperty ProjectPathProperty = DependencyProperty.Register("ProjectPath", typeof(string), typeof(ImportSettings), new PropertyMetadata(ProjectPath_Updated));
        public static readonly DependencyProperty SourcePathProperty = DependencyProperty.Register("SourcePath", typeof(string), typeof(ImportSettings), new PropertyMetadata());
        public static readonly DependencyProperty FiltersProperty = DependencyProperty.Register("Filters", typeof(string), typeof(ImportSettings), new PropertyMetadata());
        public static readonly DependencyProperty SearchPathsProperty = DependencyProperty.Register("SearchPaths", typeof(string), typeof(ImportSettings), new PropertyMetadata(RecalculateIsValid));
        private static readonly DependencyPropertyKey AvailableInterpretersPropertyKey = DependencyProperty.RegisterReadOnly("AvailableInterpreters", typeof(ObservableCollection<PythonInterpreterView>), typeof(ImportSettings), new PropertyMetadata());
        public static readonly DependencyProperty AvailableInterpretersProperty = AvailableInterpretersPropertyKey.DependencyProperty;
        public static readonly DependencyProperty SelectedInterpreterProperty = DependencyProperty.Register("SelectedInterpreter", typeof(PythonInterpreterView), typeof(ImportSettings), new PropertyMetadata(RecalculateIsValid));
        private static readonly DependencyPropertyKey TopLevelPythonFilesPropertyKey = DependencyProperty.RegisterReadOnly("TopLevelPythonFiles", typeof(ObservableCollection<string>), typeof(ImportSettings), new PropertyMetadata());
        public static readonly DependencyProperty TopLevelPythonFilesProperty = TopLevelPythonFilesPropertyKey.DependencyProperty;
        public static readonly DependencyProperty StartupFileProperty = DependencyProperty.Register("StartupFile", typeof(string), typeof(ImportSettings), new PropertyMetadata());
        public static readonly DependencyProperty UseCustomizationProperty = DependencyProperty.Register("UseCustomization", typeof(bool), typeof(ImportSettings), new PropertyMetadata(false));
        public static readonly DependencyProperty CustomizationProperty = DependencyProperty.Register("Customization", typeof(ProjectCustomization), typeof(ImportSettings), new PropertyMetadata());
        public static readonly DependencyProperty DetectVirtualEnvProperty = DependencyProperty.Register("DetectVirtualEnv", typeof(bool), typeof(ImportSettings), new PropertyMetadata(true));

        public bool IsValid {
            get { return (bool)GetValue(IsValidProperty); }
            private set { SetValue(IsValidPropertyKey, value); }
        }

        private static void RecalculateIsValid(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (!d.Dispatcher.CheckAccess()) {
                var t = d.Dispatcher.BeginInvoke((Action)(() => RecalculateIsValid(d, e)));
                return;
            }

            var s = d as ImportSettings;
            if (s == null) {
                d.SetValue(IsValidPropertyKey, false);
                return;
            }
            s.UpdateIsValid();
        }

        internal void UpdateIsValid() {
            SetValue(IsValidPropertyKey,
                CommonUtils.IsValidPath(SourcePath) &&
                CommonUtils.IsValidPath(ProjectPath) &&
                Directory.Exists(SourcePath) &&
                SelectedInterpreter != null &&
                AvailableInterpreters.Contains(SelectedInterpreter)
            );
        }

        internal async Task UpdateSourcePath() {
            UpdateIsValid();

            if (CommonUtils.IsValidPath(SourcePath) &&
                (string.IsNullOrEmpty(ProjectPath) || _isAutoGeneratedProjectPath)) {
                var newPath = Path.Combine(SourcePath, Path.GetFileName(CommonUtils.TrimEndSeparator(SourcePath)));
                int index = 0;
                if (File.Exists(newPath + ".pyproj")) {
                    string candidateNewPath;
                    do {
                        candidateNewPath = string.Format("{0} ({1})", newPath, ++index);
                    } while (File.Exists(candidateNewPath + ".pyproj"));
                    newPath = candidateNewPath;
                }
                ProjectPath = newPath + ".pyproj";
                _isAutoGeneratedProjectPath = true;
            }

            try {
                var fileList = await GetCandidateStartupFiles(SourcePath, Filters).ConfigureAwait(true);
                var tlpf = TopLevelPythonFiles as BulkObservableCollection<string>;
                if (tlpf != null) {
                    tlpf.Clear();
                    tlpf.AddRange(fileList);
                } else {
                    TopLevelPythonFiles.Clear();
                    foreach (var file in fileList) {
                        TopLevelPythonFiles.Add(file);
                    }
                }

                StartupFile = SelectDefaultStartupFile(fileList, StartupFile);
            } catch (Exception ex) {
                try {
                    Microsoft.VisualStudio.Shell.ActivityLog.LogError(
                        SR.GetString(SR.PythonToolsForVisualStudio),
                        ex.ToString()
                    );
                    MessageBox.Show(
                        SR.GetString(SR.ErrorImportWizardException, ex.GetType().Name),
                        SR.GetString(SR.PythonToolsForVisualStudio)
                    );
                    return;
                } catch (InvalidOperationException) {
                    // No ActivityLog service means we're not in a UI context
                    // and should avoid showing UI.
                }
                throw;
            }

        }

        internal static string SelectDefaultStartupFile(IList<string> fileList, string currentSelection) {
            return string.IsNullOrEmpty(currentSelection) || !fileList.Contains(currentSelection) ?
                fileList.FirstOrDefault() :
                currentSelection;
        }

        internal static async Task<IList<string>> GetCandidateStartupFiles(
            string sourcePath,
            string filters
        ) {
            if (Directory.Exists(sourcePath)) {
                return await Task.Run(() => {
                    var files = Directory.EnumerateFiles(sourcePath, "*.py", SearchOption.TopDirectoryOnly);
                    // Also include *.pyw files if they were in the filter list
                    foreach (var pywFilters in filters
                        .Split(';')
                        .Where(filter => filter.TrimEnd().EndsWith(".pyw", StringComparison.OrdinalIgnoreCase))
                    ) {
                        files = files.Concat(Directory.EnumerateFiles(sourcePath, pywFilters, SearchOption.TopDirectoryOnly));
                    }
                    return files.Select(f => Path.GetFileName(f)).ToList();
                });
            } else {
                return new string[0];
            }
        }

        private static void ProjectPath_Updated(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var self = d as ImportSettings;
            if (self != null) {
                self._isAutoGeneratedProjectPath = false;
            }
            RecalculateIsValid(d, e);
        }

        private static readonly DependencyPropertyKey IsValidPropertyKey = DependencyProperty.RegisterReadOnly("IsValid", typeof(bool), typeof(ImportSettings), new PropertyMetadata(false));
        public static readonly DependencyProperty IsValidProperty = IsValidPropertyKey.DependencyProperty;


        private static XmlWriter GetDefaultWriter(string projectPath) {
            var settings = new XmlWriterSettings {
                CloseOutput = true,
                Encoding = Encoding.UTF8,
                Indent = true,
                IndentChars = "    ",
                NewLineChars = Environment.NewLine,
                NewLineOnAttributes = false
            };

            var dir = Path.GetDirectoryName(projectPath);
            if (!Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }

            return XmlWriter.Create(projectPath, settings);
        }

        public bool ProjectFileExists {
            get {
                return File.Exists(ProjectPath);
            }
        }

        public async Task<string> CreateRequestedProjectAsync() {
            await UpdateSourcePath().ConfigureAwait(true);
            string projectPath = ProjectPath;
            string sourcePath = SourcePath;
            string filters = Filters;
            string searchPaths = string.Join(";", (SearchPaths ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(p => CommonUtils.GetRelativeDirectoryPath(SourcePath, p)));
            string startupFile = StartupFile;
            PythonInterpreterView selectedInterpreter = SelectedInterpreter;
            ProjectCustomization projectCustomization = UseCustomization ? Customization : null;
            bool detectVirtualEnv = DetectVirtualEnv;

            return await Task.Run(() => {
                bool success = false;
                try {
                    Directory.CreateDirectory(Path.GetDirectoryName(projectPath));
                    using (var writer = new StreamWriter(projectPath, false, Encoding.UTF8)) {
                        WriteProjectXml(
                            _service,
                            writer,
                            projectPath,
                            sourcePath,
                            filters,
                            searchPaths,
                            startupFile,
                            selectedInterpreter,
                            projectCustomization,
                            detectVirtualEnv
                        );
                    }
                    success = true;
                    return projectPath;
                } finally {
                    if (!success) {
                        try {
                            File.Delete(projectPath);
                        } catch {
                            // Try and avoid leaving stray files, but it does
                            // not matter much if we do.
                        }
                    }
                }
            });
        }

        internal static void WriteProjectXml(
            IInterpreterOptionsService service,
            TextWriter writer,
            string projectPath,
            string sourcePath,
            string filters,
            string searchPaths,
            string startupFile,
            PythonInterpreterView selectedInterpreter,
            ProjectCustomization customization,
            bool detectVirtualEnv
        ) {
            var projectHome = CommonUtils.GetRelativeDirectoryPath(Path.GetDirectoryName(projectPath), sourcePath);

            var project = ProjectRootElement.Create();

            project.DefaultTargets = "Build";
            project.ToolsVersion = "4.0";

            var globals = project.AddPropertyGroup();
            globals.AddProperty("Configuration", "Debug").Condition = " '$(Configuration)' == '' ";
            globals.AddProperty("SchemaVersion", "2.0");
            globals.AddProperty("ProjectGuid", Guid.NewGuid().ToString("B"));
            globals.AddProperty("ProjectHome", projectHome);
            if (CommonUtils.IsValidPath(startupFile)) {
                globals.AddProperty("StartupFile", startupFile);
            } else {
                globals.AddProperty("StartupFile", "");
            }
            globals.AddProperty("SearchPath", searchPaths);
            globals.AddProperty("WorkingDirectory", ".");
            globals.AddProperty("OutputPath", ".");

            globals.AddProperty("ProjectTypeGuids", "{888888a0-9f3d-457c-b088-3a5042f75d52}");
            globals.AddProperty("LaunchProvider", DefaultLauncherProvider.DefaultLauncherName);

            var interpreterId = globals.AddProperty(PythonConstants.InterpreterId, "");
            var interpreterVersion = globals.AddProperty(PythonConstants.InterpreterVersion, "");

            if (selectedInterpreter != null && selectedInterpreter.Id != Guid.Empty) {
                interpreterId.Value = selectedInterpreter.Id.ToString("B");
                interpreterVersion.Value = selectedInterpreter.Version.ToString();
            }

            // VS requires property groups with conditions for Debug
            // and Release configurations or many COMExceptions are
            // thrown.
            var debugGroup = project.AddPropertyGroup();
            var releaseGroup = project.AddPropertyGroup();
            debugGroup.Condition = "'$(Configuration)' == 'Debug'";
            releaseGroup.Condition = "'$(Configuration)' == 'Release'";


            var folders = new HashSet<string>();
            var virtualEnvPaths = detectVirtualEnv ? new List<string>() : null;

            foreach (var unescapedFile in EnumerateAllFiles(sourcePath, filters, virtualEnvPaths)) {
                var file = ProjectCollection.Escape(unescapedFile);
                var ext = Path.GetExtension(file);
                var fileType = "Content";
                if (PythonConstants.FileExtension.Equals(ext, StringComparison.OrdinalIgnoreCase) ||
                    PythonConstants.WindowsFileExtension.Equals(ext, StringComparison.OrdinalIgnoreCase)) {
                    fileType = "Compile";
                }
                folders.Add(Path.GetDirectoryName(file));
                
                project.AddItem(fileType, file);
            }

            foreach (var folder in folders.Where(s => !string.IsNullOrWhiteSpace(s)).OrderBy(s => s)) {
                project.AddItem("Folder", folder);
            }

            if (selectedInterpreter != null && selectedInterpreter.Id != Guid.Empty) {
                project.AddItem(
                    MSBuildProjectInterpreterFactoryProvider.InterpreterReferenceItem,
                    string.Format("{0:B}\\{1}", selectedInterpreter.Id, selectedInterpreter.Version)
                );
            }
            if (virtualEnvPaths != null && virtualEnvPaths.Any() && service != null) {
                foreach (var prefixPath in virtualEnvPaths) {
                    var options = VirtualEnv.FindInterpreterOptions(prefixPath, service);
                    AddVirtualEnvironment(project, sourcePath, options);

                    if (string.IsNullOrEmpty(interpreterId.Value)) {
                        interpreterId.Value = options.IdString;
                        interpreterVersion.Value = options.LanguageVersionString;
                    }
                }
            }

            var imports = project.AddPropertyGroup();
            imports.AddProperty("VisualStudioVersion", "10.0").Condition = " '$(VisualStudioVersion)' == '' ";
            imports.AddProperty("VSToolsPath", @"$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)")
                .Condition = " '$(VSToolsPath)' == '' ";

            (customization ?? DefaultProjectCustomization.Instance).Process(project, globals);

            project.Save(writer);
        }

        private static ProjectItemElement AddVirtualEnvironment(
            ProjectRootElement project,
            string sourcePath,
            InterpreterFactoryCreationOptions options
        ) {
            var prefixPath = options.PrefixPath;
            var interpreterPath = CommonUtils.GetRelativeFilePath(prefixPath, options.InterpreterPath);
            var windowInterpreterPath = CommonUtils.GetRelativeFilePath(prefixPath, options.WindowInterpreterPath);
            var libraryPath = CommonUtils.GetRelativeDirectoryPath(prefixPath, options.LibraryPath);
            prefixPath = CommonUtils.GetRelativeDirectoryPath(sourcePath, prefixPath);

            return project.AddItem(
                MSBuildProjectInterpreterFactoryProvider.InterpreterItem,
                prefixPath,
                new Dictionary<string, string> {
                    { MSBuildProjectInterpreterFactoryProvider.IdKey, Guid.NewGuid().ToString("B") },
                    { MSBuildProjectInterpreterFactoryProvider.DescriptionKey, options.Description },
                    { MSBuildProjectInterpreterFactoryProvider.BaseInterpreterKey, options.IdString },
                    { MSBuildProjectInterpreterFactoryProvider.InterpreterPathKey, interpreterPath },
                    { MSBuildProjectInterpreterFactoryProvider.WindowsPathKey, windowInterpreterPath },
                    { MSBuildProjectInterpreterFactoryProvider.LibraryPathKey, libraryPath },
                    { MSBuildProjectInterpreterFactoryProvider.VersionKey, options.LanguageVersionString },
                    { MSBuildProjectInterpreterFactoryProvider.ArchitectureKey, options.ArchitectureString },
                    { MSBuildProjectInterpreterFactoryProvider.PathEnvVarKey, options.PathEnvironmentVariableName }
                }
            );
        }

        private static IEnumerable<string> UnwindDirectory(string source) {
            var dir = CommonUtils.TrimEndSeparator(source);
            yield return dir;
            int lastBackslash = dir.LastIndexOf('\\');
            while (lastBackslash > 0) {
                dir = dir.Remove(lastBackslash);
                yield return dir;
                lastBackslash = dir.LastIndexOf('\\');
            }
        }

        private static IEnumerable<string> EnumerateAllFiles(
            string source,
            string filters,
            List<string> virtualEnvPaths
        ) {
            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var patterns = filters.Split(';').Concat(new[] { "*.py" }).Select(p => p.Trim()).ToArray();

            var directories = new List<string>() { source };
            var skipDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try {
                directories.AddRange(Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories));
            } catch (UnauthorizedAccessException) {
            }

            foreach (var dir in directories) {
                if (UnwindDirectory(dir).Any(skipDirectories.Contains)) {
                    continue;
                }
                
                try {
                    if (virtualEnvPaths != null) {
                        var origPrefix = VirtualEnv.GetOrigPrefixPath(dir);
                        if (!string.IsNullOrEmpty(origPrefix)) {
                            virtualEnvPaths.Add(dir);
                            skipDirectories.Add(CommonUtils.TrimEndSeparator(dir));
                            continue;
                        }
                    }

                    foreach (var filter in patterns) {
                        files.UnionWith(Directory.EnumerateFiles(dir, filter));
                    }
                } catch (UnauthorizedAccessException) {
                }
            }

            return files
                .Where(path => path.StartsWith(source))
                .Select(path => path.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

    }
}
