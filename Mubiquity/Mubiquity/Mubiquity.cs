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
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.Win32;
using NuGet.VisualStudio;
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
        /// Main automation object for referencing Visual Studio object model
        /// </summary>
        private _DTE Dte = null;

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
            ConnectEvents();
            base.Initialize();
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
                Dte.Events.SolutionEvents.ProjectAdded += new _dispSolutionEvents_ProjectAddedEventHandler(this.OnProjectAdded);
                //Dte.Events.SolutionEvents.QueryCloseSolution += new _dispSolutionEvents_QueryCloseSolutionEventHandler(this.OnQueryCloseSolution);
                //Dte.Events.BuildEvents.OnBuildBegin += new _dispBuildEvents_OnBuildBeginEventHandler(this.OnBuildBegin);
                ((EnvDTE80.Events2)Dte.Events).ProjectItemsEvents.ItemAdded += new _dispProjectItemsEvents_ItemAddedEventHandler(this.OnProjectItemAdded);
            }
        }
        private void DisconnectEvents()
        {
            if (Dte != null)
            {
                //Dte.Events.BuildEvents.OnBuildBegin -= new _dispBuildEvents_OnBuildBeginEventHandler(this.OnBuildBegin);
//                Dte.Events.SolutionEvents.QueryCloseSolution -= new _dispSolutionEvents_QueryCloseSolutionEventHandler(this.OnQueryCloseSolution);
                Dte.Events.SolutionEvents.ProjectAdded -= new _dispSolutionEvents_ProjectAddedEventHandler(this.OnProjectAdded);
                ((EnvDTE80.Events2)Dte.Events).ProjectItemsEvents.ItemAdded -= new _dispProjectItemsEvents_ItemAddedEventHandler(this.OnProjectItemAdded);
            }
        }

        /// <summary>
        /// Returns true if the given project is a Mubiquity project
        /// </summary>
        /// <param name="proj">project to evaluate</param>
        /// <returns>true is the project is a Mubiquity project.  false otherwise.</returns>
        private bool IsMubiquityProject(Project proj)
        {
            // TODO: Detect that this is a Mubiquity project
            return true;
        }

        private void OnProjectItemAdded(ProjectItem newItem)
        {
            Debug.WriteLine(newItem.Name);
            if (newItem.Name.Contains(".ino")) // check to make sure this is only done on ino files
            {
                // make it use the c++ compiler
                newItem.Properties.Item("ItemType").Value = "ClCompile";
            }
        }

        /// <summary>
        /// Callback that gets called when a project is added to a solution or opened in an existing solution
        /// </summary>
        /// <param name="proj">Project that is being added or opened</param>
        private void OnProjectAdded(Project proj)
        {
            if (IsMubiquityProject(proj))
            {
                AddNuGetPackage(proj);
            }
        }


        #endregion

        #region NugetUpdate code
        /// <summary>
        /// Add our nuget package to the project
        /// </summary>
        /// <param name="proj">Project to add the NuGet to.</param>
        private void AddNuGetPackage(Project proj)
        {
            IComponentModel componentModel = (IComponentModel)GetService(typeof(SComponentModel));
            IVsPackageInstaller installerServices = componentModel.GetService<IVsPackageInstaller>();
            if (installerServices != null)
            {
                installerServices.InstallPackage("All", proj, "Microsoft.IoT.Mubiquity", (String)null, false);
            }
        }
        #endregion

    }
}
