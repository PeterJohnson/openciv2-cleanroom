# Configure the hosted Windows compiler without a JavaScript setup action.
$ErrorActionPreference = 'Stop'
$initial = @{}
Get-ChildItem Env: | ForEach-Object { $initial[$_.Name] = $_.Value }
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$installation = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (!$installation) { throw 'Visual Studio C++ build tools were not found.' }
$developerShell = Join-Path $installation 'Common7/Tools/Launch-VsDevShell.ps1'
& $developerShell -Arch amd64 -HostArch amd64 -SkipAutomaticLocation
if (!$env:VSCMD_VER) { throw 'Visual Studio developer environment was not initialized.' }
Get-Command cl.exe -CommandType Application -ErrorAction Stop | Out-Null
foreach ($variable in Get-ChildItem Env:) {
    $name = $variable.Name
    $value = $variable.Value
    if ($name -ne 'Platform' -and (!$initial.ContainsKey($name) -or $initial[$name] -cne $value)) {
        "$name=$value" | Out-File -FilePath $env:GITHUB_ENV -Encoding utf8 -Append
    }
}
# MSVC's Platform=x64 must not override the solution's Any CPU configuration.
'Platform=' | Out-File -FilePath $env:GITHUB_ENV -Encoding utf8 -Append
