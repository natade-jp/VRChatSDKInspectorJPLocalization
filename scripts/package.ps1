$ErrorActionPreference = 'Stop'

# Use .NET APIs so hashing also works without PowerShell module auto-loading.
function Get-Sha256([string]$Path) {
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        $stream = [IO.File]::OpenRead($Path)
        try {
            return [BitConverter]::ToString($algorithm.ComputeHash($stream))
        }
        finally { $stream.Dispose() }
    }
    finally { $algorithm.Dispose() }
}

try {
    $repositoryRoot = Split-Path -Parent $PSScriptRoot
    $executableName = 'VRChatSDKInspectorJPLocalization.exe'
    $executableSource = Join-Path $repositoryRoot "artifacts\publish\win-x64\$executableName"
    $readmeSource = Join-Path $repositoryRoot 'README.md'
    $distributionPath = Join-Path $repositoryRoot 'artifacts\distribution\VRChatSDKInspectorJPLocalization-win-x64'
    $expectedNames = @($executableName, 'README.md')

    foreach ($sourcePath in @($executableSource, $readmeSource)) {
        if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
            throw "Required file not found: $sourcePath"
        }
    }

    # Refuse redirected output paths and unexpected contents instead of deleting user files.
    $currentPath = $distributionPath
    while ($currentPath) {
        if (Test-Path -LiteralPath $currentPath) {
            $item = Get-Item -LiteralPath $currentPath -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Distribution path must not contain links or junctions: $currentPath"
            }
        }
        $currentPath = Split-Path -Parent $currentPath
    }

    if (Test-Path -LiteralPath $distributionPath) {
        foreach ($item in Get-ChildItem -LiteralPath $distributionPath -Force) {
            if ($item.PSIsContainer -or $item.Name -notin $expectedNames -or
                ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Unexpected item in distribution folder. Move it elsewhere before publishing: $($item.FullName)"
            }
        }
    }

    [IO.Directory]::CreateDirectory($distributionPath) | Out-Null
    Copy-Item -LiteralPath $executableSource -Destination (Join-Path $distributionPath $executableName) -Force
    Copy-Item -LiteralPath $readmeSource -Destination (Join-Path $distributionPath 'README.md') -Force

    foreach ($sourcePath in @($executableSource, $readmeSource)) {
        $destinationPath = Join-Path $distributionPath (Split-Path -Leaf $sourcePath)
        if ((Get-Sha256 $sourcePath) -ne (Get-Sha256 $destinationPath)) {
            throw "Distribution file verification failed: $destinationPath"
        }
    }
    if (@(Get-ChildItem -LiteralPath $distributionPath -Force).Count -ne 2) {
        throw 'Distribution folder must contain exactly the EXE and README.md.'
    }
    Write-Output "Distribution package ready: $distributionPath"
    exit 0
}
catch {
    [Console]::Error.WriteLine("Packaging FAILED: $($_.Exception.Message)")
    exit 1
}
