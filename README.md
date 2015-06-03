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
1. Ensure that the output of the nuget package is in the Nuget Package Sources
1. Open and build the vsix solution

At this point, visual studio will have opened a new experimental visual studio instance which contains the Mubiquity extension.

1. File | New

