[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $Tag,
    [AllowEmptyString()]
    [string] $EnvironmentFile = $env:GITHUB_ENV
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# NuGet supports the existing four-part release tags as well as SemVer tags.
# Anchor to the absolute end: a trailing newline must never enter GITHUB_ENV.
$pattern = '\A[vV]?(?<version>(?<core>(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)(?:\.(?:0|[1-9][0-9]*))?)(?:-(?<pre>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?)\z'
$match = [regex]::Match($Tag, $pattern, [System.Text.RegularExpressions.RegexOptions]::CultureInvariant, [TimeSpan]::FromSeconds(1))
if (-not $match.Success) {
    throw "Invalid release tag '$Tag'. Expected [v]major.minor.patch[.revision][-prerelease][+metadata]."
}

$parts = $match.Groups['core'].Value.Split('.')
foreach ($part in $parts) {
    $number = 0
    if (-not [int]::TryParse($part, [ref] $number) -or $number -gt 65534) {
        throw 'Release version components must be integers from 0 through 65534 for assembly/file metadata.'
    }
}
if ($match.Groups['pre'].Success) {
    foreach ($identifier in $match.Groups['pre'].Value.Split('.')) {
        if ($identifier -cmatch '\A0[0-9]+\z') {
            throw 'Numeric prerelease identifiers must not have leading zeroes.'
        }
    }
}
if ($parts.Count -eq 3) { $parts += '0' }

$version = $match.Groups['version'].Value
$properties = [ordered] @{
    Version = $version
    PackageVersion = $version
    AssemblyVersion = ($parts -join '.')
    FileVersion = ($parts -join '.')
    InformationalVersion = $version
    IncludeSourceRevisionInInformationalVersion = 'false'
    ContinuousIntegrationBuild = 'true'
}
if (-not [string]::IsNullOrEmpty($env:GITHUB_SHA)) {
    if ($env:GITHUB_SHA -cnotmatch '\A(?:[0-9a-fA-F]{40}|[0-9a-fA-F]{64})\z') {
        throw 'GITHUB_SHA must be a valid hexadecimal Git commit identifier.'
    }
    $properties['RepositoryCommit'] = $env:GITHUB_SHA
    $properties['SourceRevisionId'] = $env:GITHUB_SHA
}

# These exact property names are imported by MSBuild from the environment.
# All restore/build/test/pack commands therefore receive identical metadata.
if (-not [string]::IsNullOrWhiteSpace($EnvironmentFile)) {
    $lines = foreach ($property in $properties.GetEnumerator()) {
        '{0}={1}' -f $property.Key, $property.Value
    }
    Add-Content -LiteralPath $EnvironmentFile -Value $lines -Encoding utf8
}
[pscustomobject] $properties
