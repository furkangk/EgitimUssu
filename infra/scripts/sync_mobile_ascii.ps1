$ErrorActionPreference = "Stop"

$source = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\..\mobile"))
$target = "C:\Users\furkan.gokdemir\AppData\Local\Temp\egitimussu_mobile_workspace"

$targetParent = [System.IO.Path]::GetFullPath("C:\Users\furkan.gokdemir\AppData\Local\Temp")
$resolvedTarget = [System.IO.Path]::GetFullPath($target)

if (-not $resolvedTarget.StartsWith($targetParent, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to sync outside temp workspace."
}

if (-not (Test-Path -LiteralPath $source)) {
    throw "Source mobile workspace not found: $source"
}

New-Item -ItemType Directory -Force -Path $target | Out-Null

$arguments = @(
    $source,
    $target,
    "/MIR",
    "/XD", "build", ".dart_tool", ".idea",
    "/XF", "tmp_*.log", "*.iml"
)

& robocopy @arguments | Out-Null

if ($LASTEXITCODE -ge 8) {
    throw "robocopy failed with exit code $LASTEXITCODE"
}

Write-Output $target
