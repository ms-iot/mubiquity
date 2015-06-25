param([string]$directoryToZip="directoryToZip", [string]$zipFile="zipFile", [string]$nugetDirectory)

"Zipping " + $directoryToZip
"Zipping To File " + $zipFile
"Creating Nuget " + $nugetDirectory

Add-Type -Assembly System.IO.Compression.FileSystem

# only create the zip if it doesn't exist.
# Changing the template requires rebuild.
# TODO: figure out how to make this happen if the contents change.
#if (!(Test-Path -Path $zipFile))
#{
	Remove-item $zipFile;
	[System.IO.Compression.ZipFile]::CreateFromDirectory($directoryToZip, $zipFile);
#}

#assumes nuget.exe is in the path
Push-Location $nugetDirectory
$nugetCommand = Join-Path $nugetDirectory "build-nupkg.cmd"
cmd.exe /c "`"$nugetCommand`""
Pop-Location

