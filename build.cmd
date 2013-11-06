@"%ProgramFiles(x86)%\Microsoft SDKs\F#\3.0\Framework\v4.0\fsi" GenerateScripts.fsx
rm *.nupkg
nuget\NuGet.exe pack FsLab.nuspec
