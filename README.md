# Mubiquity
Repository for the Mubiquity tools

# Setup and Debugging
The Mubiquity Visual Studio extension has a few parts:
* The VSIX itself
* A Nuget Package
* Source Template

To iterate on this:

1. Fork the Mubiquity repository
1. Install Visual Studio 2015 RC
1. Install Visual Studio 2015 RC SDK
1. Download [nuget.exe](https://nuget.org/nuget.exe) to the nuget directory in the repository
1. build the Nuget Package by running the build script in the Nuget directory
1. (alpha note) Copy the generated nuget package to Mubiquity\Packages
1. (alpha note) Zip the BigBrain directory in the Templates directory
1. (alpha note) Copy the BigBrain.zip to Mubiquity\ProjectTemplates
1. Open and build the vsix solution

At this point, visual studio will have opened a new experimental visual studio instance which contains the Mubiquity extension.

1. File | New
1. Search for BigBrain
1. Create the new project with this template



===

This project has adopted the [Microsoft Open Source Code of Conduct](http://microsoft.github.io/codeofconduct). For more information see the [Code of Conduct FAQ](http://microsoft.github.io/codeofconduct/faq.md) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments. 
