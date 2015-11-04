/*
    Copyright(c) Microsoft Corp. All rights reserved.
    
    The MIT License(MIT)
    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files(the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and / or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions :
    The above copyright notice and this permission notice shall be included in
    all copies or substantial portions of the Software.
    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
    THE SOFTWARE.
*/

using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Win32;
//using NuGet.VisualStudio;
using EnvDTE;
using Microsoft.VisualStudio.ComponentModelHost;

namespace Mubiquity
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the 
    /// IVsPackage interface and uses the registration attributes defined in the framework to 
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
    [Guid(MubiquityGuids.PackageGuidString)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.ShellInitialized_string)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    public sealed class Mubiquity : Package
    {
        /// <summary>
        /// Main automation object for referencing Visual Studio object model.
        /// These need to have managed references or the object can be garbage collected
        /// </summary>
        private _DTE Dte = null;
        private BuildEvents buildEvents = null;
        private SolutionEvents solutionEvents = null;
        private ProjectItemsEvents projectItemsEvents = null;
        private TelemetryClient telemetry = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="Mubiquity"/> class.
        /// </summary>
        public Mubiquity()
        {
            // Inside this method you can place any initialization code that does not require 
            // any Visual Studio service because at this point the package object is created but 
            // not sited yet inside Visual Studio environment. The place to do all the other 
            // initialization is the Initialize method.
            Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering constructor for: {0}", this.ToString()));
        }

        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize()
        {
            Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering Initialize() of: {0}", this.ToString()));
            Dte = this.GetService(typeof(_DTE)) as _DTE;
            buildEvents = Dte.Events.BuildEvents;
            solutionEvents = Dte.Events.SolutionEvents;
            projectItemsEvents = ((EnvDTE80.Events2)Dte.Events).ProjectItemsEvents;

            ConnectEvents();

            base.Initialize();

            System.Threading.Tasks.Task.Run(() =>
            {
                telemetry = new TelemetryClient();
                telemetry.InstrumentationKey = "d5013fa4-b253-4166-9071-5b6c874acd0f";
                telemetry.Context.Session.Id = Guid.NewGuid().ToString();
                telemetry.Context.Device.OperatingSystem = Environment.OSVersion.ToString();
                telemetry.Context.Device.Model = "VSIX";
                FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(typeof(Mubiquity).Assembly.Location);
                telemetry.Context.Device.Type = fvi.FileVersion;
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisconnectEvents();
            }
        }

        private void ConnectEvents()
        {
            if (Dte != null)
            {
                solutionEvents.ProjectAdded += new _dispSolutionEvents_ProjectAddedEventHandler(this.OnProjectAdded);
                solutionEvents.Opened += new _dispSolutionEvents_OpenedEventHandler(this.OnSolutionOpened);
                solutionEvents.QueryCloseSolution += new _dispSolutionEvents_QueryCloseSolutionEventHandler(this.OnQueryCloseSolution);
                buildEvents.OnBuildBegin += new _dispBuildEvents_OnBuildBeginEventHandler(this.OnBuildBegin);
                buildEvents.OnBuildDone += new _dispBuildEvents_OnBuildDoneEventHandler(this.OnBuildDone);
                projectItemsEvents.ItemAdded += new _dispProjectItemsEvents_ItemAddedEventHandler(this.OnProjectItemAdded);
            }
        }
        private void DisconnectEvents()
        {
            if (Dte != null)
            {
                buildEvents.OnBuildBegin -= new _dispBuildEvents_OnBuildBeginEventHandler(this.OnBuildBegin);
                buildEvents.OnBuildDone -= new _dispBuildEvents_OnBuildDoneEventHandler(this.OnBuildDone);
                solutionEvents.Opened -= new _dispSolutionEvents_OpenedEventHandler(this.OnSolutionOpened);
                solutionEvents.QueryCloseSolution -= new _dispSolutionEvents_QueryCloseSolutionEventHandler(this.OnQueryCloseSolution);
                solutionEvents.ProjectAdded -= new _dispSolutionEvents_ProjectAddedEventHandler(this.OnProjectAdded);
                projectItemsEvents.ItemAdded -= new _dispProjectItemsEvents_ItemAddedEventHandler(this.OnProjectItemAdded);
            }
        }

        private ProjectItem findInoInProject(ProjectItems projItems)
        {
            // Can be null
            if (projItems == null)
            {
                return null;
            }

            foreach (var item in projItems)
            {
                var pi = item as ProjectItem;
                var filename = pi.Name.ToLower();
                if (filename.Contains(".ino"))
                {
                    return pi;
                }
                else
                {
                    ProjectItem found = findInoInProject(pi.ProjectItems);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return null;
        }
        private ProjectItem findFirmwareFolderInProject(ProjectItems projItems)
        {
            // Can be null
            if (projItems == null)
            {
                return null;
            }

            foreach (var item in projItems)
            {
                var pi = item as ProjectItem;
                var filename = pi.Name.ToLower();
                if (filename.Contains("firmware"))
                {
                    return pi;
                }
                else
                {
                    ProjectItem found = findFirmwareFolderInProject(pi.ProjectItems);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return null;
        }
        private ProjectItem findPathInProject(string path, ProjectItems projItems)
        {
            string comparePath = path.ToLower();
            // Can be null
            if (projItems == null)
            {
                return null;
            }

            foreach (var item in projItems)
            {
                var pi = item as ProjectItem;
                var filename = pi.Name.ToLower();
                if (filename.Contains(Path.GetFileName(comparePath)))
                {
                    return pi;
                }
                else
                {
                    ProjectItem found = findPathInProject(path, pi.ProjectItems);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return null;
        }

        private bool HasArduinoProjectItems(ProjectItems projItems)
        {
            return findInoInProject(projItems) != null;
        }

        /// <summary>
        /// Returns true if the given project is a Mubiquity Solution
        /// </summary>
        /// <param name="proj">project to evaluate</param>
        /// <returns>true is the project is a Mubiquity Solution.  false otherwise.</returns>
        private bool IsMubiquitySolution(Solution solution)
        {
            if (solution?.Projects == null)
            {
                return false;
            }

            foreach (var p in solution.Projects)
            {
                var proj = p as Project;
                if (HasArduinoProjectItems(proj.ProjectItems))
                {
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// Returns true if the given project is a Mubiquity project
        /// </summary>
        /// <param name="proj">project to evaluate</param>
        /// <returns>true is the project is a Mubiquity project.  false otherwise.</returns>
        private bool IsMubiquityProject(Project proj)
        {
            if (IsMubiquitySolution(Dte.Solution))
            {
                // Is mubiquity solution, but is it the non-arduino project?
                return !HasArduinoProjectItems(proj.ProjectItems);
            }

            return false;
        }

        private void OnProjectItemAdded(ProjectItem newItem)
        {
            // Nothing for now
        }

        private void OnBuildBegin(vsBuildScope scope, vsBuildAction action)
        {
            Debug.WriteLine("Build beginning");

            // Enumerate to see if we have a Makefile project
            var solution = Dte.Solution;
            List<ProjectItem> inosEncountered = new List<ProjectItem>();

            foreach (var p in solution.Projects)
            {
                var project = p as Project;
                ProjectItem ino = findInoInProject(project.ProjectItems);
                if (ino != null)
                {
                    inosEncountered.Add(ino);
                    var projectPath = Path.GetDirectoryName(project.FullName);

                    ArduinoProjectHelper arduinoHelper = new ArduinoProjectHelper(Path.Combine(projectPath, "Makefile"), ino.Name);
                    arduinoHelper.ManifestPrefix = "Mubiquity.Resources";

                    arduinoHelper.regenerateMakefile();
                }
            }

            if (inosEncountered.Count > 0)
            {
                if (telemetry != null)
                {
                    telemetry.TrackEvent("MubiquityBuild");
                }

                // Ensure that the output of the Arduino project is in the firmware folder of the mubiquity project
                foreach (var p in solution.Projects)
                {
                    var project = p as Project;
                    ProjectItem ino = findInoInProject(project.ProjectItems);

                    // Not an INO.
                    if (ino == null)
                    {
                        foreach (var inos in inosEncountered)
                        {
                            // Inject the output.
                            var inoProjectPath = inos.FileNames[0];
                            var hexPath = Path.ChangeExtension(inoProjectPath, "Hex");

                            // Only inject if we have a firmware Folder.
                            var firmwareFolderItem = findFirmwareFolderInProject(project.ProjectItems);
                            if (firmwareFolderItem != null)
                            {
                                if (findPathInProject(hexPath, firmwareFolderItem.ProjectItems) == null)
                                {
                                    if (!File.Exists(hexPath))
                                    {
                                        // Temporarily create one so we can add it
                                        // This will be replaced during build with a real makefile.
                                        using (StreamWriter fakeHexFile = new StreamWriter(hexPath, false))
                                        {
                                            fakeHexFile.WriteLine("Hello World!");
                                        }
                                    }

                                    firmwareFolderItem.ProjectItems.AddFromFile(hexPath);

                                    var hexFirmwareProjectItem = findPathInProject(hexPath, firmwareFolderItem.ProjectItems);
                                    hexFirmwareProjectItem.Properties.Item("ItemType").Value = "Content";
                                    hexFirmwareProjectItem.Properties.Item("CopyToOutputDirectory").Value = 1;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void OnBuildDone(vsBuildScope scope, vsBuildAction action)
        {
            Debug.WriteLine("Build Finished");
            var solution = Dte.Solution;
            if (telemetry != null)
            {
                if (IsMubiquitySolution(solution))
                {
                    // Represents the number of failures
                    if (solution.SolutionBuild.LastBuildInfo > 0)
                    {
                        telemetry.TrackEvent("MubiquityBuildFailed");
                    }
                    else
                    {
                        telemetry.TrackEvent("MubiquityBuildSucceeded");
                    }
                }
            }
        }


        /// <summary>
        /// Callback that gets called when a project is added to a solution or opened in an existing solution
        /// </summary>
        /// <param name="proj">Project that is being added or opened</param>
        private void OnProjectAdded(Project project)
        {
            // nothing for now
        }

        /// <summary>
        /// Callback that gets called when a project is added to a solution or opened in an existing solution
        /// </summary>
        /// <param name="proj">Project that is being added or opened</param>
        private void OnSolutionOpened()
        {
            var solution = Dte.Solution;
            List<Project> microControllerProject = new List<Project>();
            Project uwpProject = null;

            // Set the uwp project dependencies
            foreach (var p in solution.Projects)
            {
                var project = p as Project;
                ProjectItem ino = findInoInProject(project.ProjectItems);
                if (ino == null)
                {
                    uwpProject = project;
                }
                else
                {
                    microControllerProject.Add(project);
                }
            }

            if (uwpProject != null && microControllerProject.Count > 0)
            {
                if (telemetry != null)
                {
                    telemetry.TrackEvent("MubiquitySolutionOpened");
                }

                foreach (var mcu in microControllerProject)
                {
                    solution.SolutionBuild.BuildDependencies.Item(uwpProject.UniqueName).AddProject(mcu.UniqueName);
                }
            }
        }

        private void OnQueryCloseSolution(ref bool fCancel)
        {
            fCancel = false;
            if (telemetry != null)
            {
                telemetry.Flush();
            }
        }

        #endregion
    }
}
