param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$worldModelRoot = $PSScriptRoot
$repositoryRoot = Split-Path -Parent $worldModelRoot
$pluginTarget = Join-Path $repositoryRoot "Assets\Steppe\Plugins\Simulation"
$simulationProject = Join-Path $worldModelRoot "Steppe.Simulation\Steppe.Simulation.csproj"

& dotnet build $simulationProject -c $Configuration -f netstandard2.1 --nologo
if ($LASTEXITCODE -ne 0) {
    throw "Unity simulation plugin build failed with exit code $LASTEXITCODE."
}

New-Item -ItemType Directory -Path $pluginTarget -Force | Out-Null

$assemblies = @(
    Join-Path $worldModelRoot "Steppe.Simulation\bin\$Configuration\netstandard2.1\Steppe.Simulation.dll"
)

foreach ($assembly in $assemblies) {
    Copy-Item -LiteralPath $assembly -Destination $pluginTarget -Force
    Write-Host "Published $([System.IO.Path]::GetFileName($assembly))"
}
