# Run in project root: .\List-ProjectFiles.ps1

function Get-FileTree {
    param(
        [string]$Path = ".",
        [string]$Prefix = ""
    )
    
    $items = Get-ChildItem $Path | 
        Where-Object { $_.Name -notmatch '^(obj|bin|\.git)$' } | 
        Sort-Object Name
    
    for ($i = 0; $i -lt $items.Count; $i++) {
        $item = $items[$i]
        $isLast = ($i -eq $items.Count - 1)
        
        # Use if/else instead of ternary operators (PS 5.1 compatible)
        $connector = if ($isLast) { "└── " } else { "├── " }
        $relativePath = $item.FullName.Substring((Get-Item .).FullName.Length + 1) -replace '\\', '/'
        
        # Only show .cs and .csproj files
        if ($item.PSIsContainer) {
            Write-Output "${Prefix}${connector}${relativePath}/"
            $childPrefix = if ($isLast) { "    " } else { "│   " }
            Get-FileTree -Path $item.FullName -Prefix "${Prefix}${childPrefix}"
        }
        else {
            if ($item.Extension -eq '.cs' -or $item.Extension -eq '.csproj' -or $item.Name -eq 'Directory.Build.props') {
                Write-Output "${Prefix}${connector}${relativePath}"
            }
        }
    }
}

# Output header
Write-Output "PROJECT: PhantomExe VM Protector"
Write-Output "FILES (.cs, .csproj, Directory.Build.props):"
Write-Output ""

# Generate tree
Get-FileTree