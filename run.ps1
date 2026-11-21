<#
    Builds and plays Beep's Number Line Dash.
    Use another Godot:  .\run.ps1 -GodotDir 'D:\Godot_v4.7.2-stable_mono_win64'
    Open the editor:    .\run.ps1 -Editor
    Run the tests:      .\run.ps1 -Test
    Screenshot a screen without playing (title, map, map:swoop, play:hop, play:bighop, play:boss, play:go,
    play:gowrong, play:auto:7, shop, shop:style, results):
                        .\run.ps1 -Shot play:hop
#>
param(
    # Folder holding the Godot 4.7.2 .NET editor (shared with the other Godot projects)
    [string]$GodotDir = 'C:\my-coding-projects\godotPalaceRoomViewer\.tools\Godot_v4.7.2-stable_mono_win64',
    [switch]$Editor,
    [switch]$Test,
    [string]$Shot,
    [double]$Wait = 2.5
)
$ErrorActionPreference = 'Stop'

if ($Test) {
    dotnet test (Join-Path $PSScriptRoot 'tests\MathRobot.Core.Tests')
    exit $LASTEXITCODE
}

$godot = Join-Path $GodotDir 'Godot_v4.7.2-stable_mono_win64.exe'
if (-not (Test-Path $godot)) { throw "Godot not found at $godot. Pass -GodotDir." }

# Let Godot import the project before the game loads (quick when nothing changed).
& $godot --headless --path $PSScriptRoot --editor --import | Out-Null

dotnet build (Join-Path $PSScriptRoot 'MathRobotGame.csproj')
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

if ($Editor) { & $godot --path $PSScriptRoot --editor }
elseif ($Shot) {
    $out = Join-Path $PSScriptRoot ("shot-" + ($Shot -replace ':', '-') + ".png")
    & $godot --path $PSScriptRoot --resolution 1024x896 -- --shot $out --screen $Shot --wait $Wait
    Write-Host "Saved $out"
}
else { & $godot --path $PSScriptRoot }
