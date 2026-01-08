# Run in project root: .\Export-ProjectFiles.ps1

# Get all relevant files
$files = Get-ChildItem -Recurse -File | 
    Where-Object { 
        $_.Extension -in @('.cs', '.csproj') -or 
        $_.Name -eq 'Directory.Build.props'
    } |
    Where-Object { 
        $_.FullName -notmatch '\\(obj|bin|\.git)\\' 
    } |
    Sort-Object FullName

# Output header
Write-Output "PROJECT: PhantomExe VM Protector"
Write-Output "FILES: .cs, .csproj, Directory.Build.props"
Write-Output ""

# Output each file
foreach ($file in $files) {
    $relativePath = $file.FullName.Substring((Get-Item .).FullName.Length + 1) -replace '\\', '/'
    Write-Output "[FILE: $relativePath]"
    
    # Read content with UTF-8 encoding (no BOM issues)
    $content = Get-Content $file.FullName -Raw -Encoding UTF8
    Write-Output $content
    Write-Output ""
}