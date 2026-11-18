[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script = Join-Path $PSScriptRoot 'Set-ReleaseVersion.ps1'
$cases = @(
    @{ Tag = '1.2.3'; Version = '1.2.3'; Numeric = '1.2.3.0' },
    @{ Tag = 'v1.2.3'; Version = '1.2.3'; Numeric = '1.2.3.0' },
    @{ Tag = 'V1.2.3.4'; Version = '1.2.3.4'; Numeric = '1.2.3.4' },
    @{ Tag = 'v7.6.5.4-rc.2+build.9'; Version = '7.6.5.4-rc.2+build.9'; Numeric = '7.6.5.4' },
    @{ Tag = '0.0.0-alpha.0'; Version = '0.0.0-alpha.0'; Numeric = '0.0.0.0' },
    @{ Tag = '65534.65534.65534.65534'; Version = '65534.65534.65534.65534'; Numeric = '65534.65534.65534.65534' }
)
foreach ($case in $cases) {
    # Explicitly disable GITHUB_ENV output: tests must not inject fixture versions.
    $result = & $script -Tag $case.Tag -EnvironmentFile ''
    foreach ($name in 'Version', 'PackageVersion', 'InformationalVersion') {
        if ($result.$name -cne $case.Version) { throw "Incorrect $name for $($case.Tag)." }
    }
    foreach ($name in 'AssemblyVersion', 'FileVersion') {
        if ($result.$name -cne $case.Numeric) { throw "Incorrect $name for $($case.Tag)." }
    }
    if ($result.IncludeSourceRevisionInInformationalVersion -cne 'false' -or $result.ContinuousIntegrationBuild -cne 'true') {
        throw 'Incorrect CI/source-revision flags.'
    }
}

$invalid = @('', 'release', 'v1.2', 'refs/tags/v1.2.3', '1.2.3.4.5', '01.2.3', '1.02.3',
    '1.2.03', '1.2.3-01', '1.2.3-rc.01', '1.2.3-', '1.2.3+', '65535.0.0',
    '2147483648.0.0', "1.2.3`n", "1.2.3`nVersion=9.9.9", '1.2.3 beta')
foreach ($tag in $invalid) {
    $rejected = $false
    try { $null = & $script -Tag $tag -EnvironmentFile '' } catch { $rejected = $true }
    if (-not $rejected) { throw "Invalid tag was accepted: '$tag'." }
}
Write-Output "Release tag tests passed ($($cases.Count) valid, $($invalid.Count) invalid)."
