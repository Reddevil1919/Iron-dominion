param(
    [Parameter(Mandatory)] [string] $ArtifactDirectory,
    [Parameter(Mandatory)] [string] $ReportDirectory,
    [Parameter(Mandatory)] [string] $ExpectedCommit
)

$ErrorActionPreference = 'Stop'
$artifact = (Resolve-Path $ArtifactDirectory).Path
$archive = Join-Path $artifact 'IronDominion-Windows-x64.zip'
$expectedHash = (Get-Content (Join-Path $artifact 'SHA256SUMS.txt') -Raw).Split(' ', 2)[0]
$actualHash = (Get-FileHash $archive -Algorithm SHA256).Hash
if ($actualHash -ne $expectedHash) {
    throw "Archive SHA-256 mismatch: $actualHash"
}

$reports = [IO.Path]::GetFullPath($ReportDirectory)
New-Item -ItemType Directory -Path $reports -Force | Out-Null
$unpacked = Join-Path $env:RUNNER_TEMP ("iron-dominion-" + [guid]::NewGuid())
Expand-Archive -LiteralPath $archive -DestinationPath $unpacked
$game = Join-Path $unpacked 'IronDominion'
if ((Get-Content (Join-Path $game 'BUILD.txt') -TotalCount 1) -ne "Commit $ExpectedCommit") {
    throw 'The packaged source commit does not match the workflow checkout.'
}

function Invoke-CheckedGame([string] $Log, [string[]] $Arguments) {
    $exe = Join-Path $game 'IronDominion.exe'
    $argsWithLog = @('--headless', '--max-fps', '120', '--log-file', ('"{0}"' -f $Log)) + $Arguments
    $process = Start-Process -FilePath $exe -WorkingDirectory $game -ArgumentList $argsWithLog -PassThru
    if (-not $process.WaitForExit(90000)) {
        $process.Kill()
        throw 'The Windows executable did not exit before the timeout.'
    }
    if (-not (Test-Path -LiteralPath $Log)) {
        throw "The executable did not write its log (exit $($process.ExitCode))."
    }
    Get-Content -LiteralPath $Log
    if ($process.ExitCode -ne 0 -or (Select-String -Path $Log -Pattern '(^|\s)(ERROR|Error):' -Quiet)) {
        throw "The Windows executable failed (exit $($process.ExitCode))."
    }
}

$startup = Join-Path $reports 'windows-startup.log'
Invoke-CheckedGame $startup @('--verbose', '--quit-after', '180')
$exportedScene = '^Loading resource: res://\.godot/exported/[^/]+/export-[0-9a-f]+-Battlefield\.scn$'
if (-not (Select-String -Path $startup -Pattern $exportedScene -Quiet)) {
    throw 'The default run did not report loading the battlefield.'
}

$benchmark = Join-Path $reports 'windows-headless-50.json'
Invoke-CheckedGame (Join-Path $reports 'windows-benchmark.log') @('--', '--benchmark', '50', '8', ('"{0}"' -f $benchmark))
$data = Get-Content -LiteralPath $benchmark -Raw | ConvertFrom-Json
if (
    $data.activeUnits -ne 50 -or $data.simulationSamples -lt 100 -or $data.pathRequests -lt 50 -or
    $data.rejectedMoveOrders -ne 0 -or $data.shots -le 0 -or $data.architecture -ne 'X64'
) {
    throw 'The native smoke did not finish the 50-unit workload as expected.'
}
Get-Content -LiteralPath $benchmark
Write-Output "Windows native smoke passed for SHA-256 $actualHash"
