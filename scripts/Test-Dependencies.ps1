[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$solution = Join-Path $repositoryRoot 'MakerspaceLedger.slnx'

Push-Location $repositoryRoot
try {
    dotnet restore $solution --locked-mode --force-evaluate --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        throw 'Dependency restore or vulnerability audit failed. Resolve reported advisories or source errors.'
    }

    $reportLines = @(dotnet package list --project $solution --include-transitive --vulnerable --format json --output-version 1 --no-restore)
    if ($LASTEXITCODE -ne 0) {
        throw 'NuGet could not produce the vulnerability report.'
    }
    $report = ($reportLines -join "`n") | ConvertFrom-Json
    if (-not $report.projects -or @($report.projects).Count -lt 3) {
        throw 'The vulnerability report did not include all three test projects.'
    }

    $findings = [Collections.Generic.List[string]]::new()
    foreach ($project in $report.projects) {
        if ($project.PSObject.Properties.Name -contains 'frameworks') {
            foreach ($framework in $project.frameworks) {
                foreach ($group in @('topLevelPackages', 'transitivePackages')) {
                    if ($framework.PSObject.Properties.Name -contains $group) {
                        foreach ($package in $framework.$group) {
                            if ($package.PSObject.Properties.Name -contains 'vulnerabilities') {
                                foreach ($vulnerability in $package.vulnerabilities) {
                                    $findings.Add("$($package.id) $($package.resolvedVersion): $($vulnerability.severity) $($vulnerability.advisoryurl)")
                                }
                            }
                        }
                    }
                }
            }
        }
    }
    if ($findings.Count -gt 0) {
        $findings | ForEach-Object { Write-Output "FAIL: $_" }
        throw "NuGet reported $($findings.Count) vulnerability finding(s)."
    }
    Write-Output "Dependency audit passed: $(@($report.projects).Count) projects; no known NuGet vulnerabilities reported."
}
finally {
    Pop-Location
}
