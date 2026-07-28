param(
    [Parameter(Mandatory = $true)]
    [string] $RepositoryRoot
)

$ErrorActionPreference = 'Stop'

$ripgrep = Get-Command rg -CommandType Application -ErrorAction SilentlyContinue
if ($null -eq $ripgrep) {
    throw 'The current product vocabulary guard requires the rg executable in PATH.'
}

$activeRoots = @(
    'README.md',
    'src',
    'tests',
    'deploy',
    'installers',
    'scripts',
    '.github',
    'docs/operations',
    'docs/product',
    'docs/roadmap'
)
$rules = @(
    @{ Pattern = ('\bOperator' + ' App\b'); Label = ('Operator' + ' App') },
    @{ Pattern = ('\bControl' + ' Plane\b'); Label = ('Control' + ' Plane') },
    @{ Pattern = ('/api/' + 'owner(?:/|\b)'); Label = ('/api/' + 'owner') }
)
$violations = [System.Collections.Generic.List[string]]::new()

foreach ($root in $activeRoots) {
    $path = Join-Path $RepositoryRoot $root
    if (-not (Test-Path -LiteralPath $path)) {
        continue
    }

    foreach ($rule in $rules) {
        $matches = & $ripgrep.Source --line-number `
            --glob '!docs/archive/**' `
            --glob '!docs/superpowers/specs/2026-07-28-platform-organization-product-boundary-design.md' `
            --glob '!docs/superpowers/plans/2026-07-28-platform-organization-big-bang-migration.md' `
            $rule.Pattern $path
        if ($LASTEXITCODE -gt 1) {
            throw "rg failed while checking '$($rule.Label)' under '$path'."
        }

        foreach ($match in $matches) {
            $violations.Add("[$($rule.Label)] $match")
        }
    }
}

if ($violations.Count -gt 0) {
    $violations | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host 'Current product vocabulary guard passed.'
