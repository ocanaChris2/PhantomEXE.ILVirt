# 1. Clear ALL caches
dotnet nuget locals all --clear

# 2. Delete global NuGet cache manually
Remove-Item "$env:USERPROFILE\.nuget\packages\asmresolver*" -Recurse -Force -ErrorAction SilentlyContinue

# 3. Reinstall ONLY AsmResolver.PE in clean test
mkdir repair-test
Set-Content repair-test\Program.cs @"
using AsmResolver.PE.DotNet.Resources;
class P { static void Main() { new EmbeddedResource(""x"", new byte[0], 1); } }
"@
Set-Content repair-test\repair-test.csproj @"
<Project Sdk=`"Microsoft.NET.Sdk`">
  <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
  <ItemGroup><PackageReference Include=`"AsmResolver.PE`" Version=`"5.4.0`" /></ItemGroup>
</Project>
"@

cd repair-test
dotnet run