[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$requiredFiles = @(
    'README.md', 'LICENSE', '.gitignore', '.gitattributes', '.editorconfig',
    'global.json', 'Directory.Build.props', 'Directory.Packages.props', 'NuGet.Config', 'Stockroom.slnx',
    '.config/dotnet-tools.json',
    '.github/CONTRIBUTING.md', '.github/CODE_OF_CONDUCT.md', '.github/SECURITY.md', '.github/SUPPORT.md',
    '.github/workflows/ci.yml', '.github/pull_request_template.md', '.github/dependabot.yml',
    '.github/ISSUE_TEMPLATE/bug_report.yml', '.github/ISSUE_TEMPLATE/feature_request.yml',
    '.github/ISSUE_TEMPLATE/question.yml', '.github/ISSUE_TEMPLATE/config.yml',
    'docs/README.md',
    'docs/planning/mvp.md', 'docs/planning/personas.md', 'docs/planning/user-stories.md',
    'docs/planning/delivery-plan.md', 'docs/planning/definition-of-done.md',
    'docs/development/architecture.md', 'docs/development/setup.md',
    'docs/development/github-repository-setup.md',
    'docs/quality/test-plan.md', 'docs/quality/accessibility.md',
    'docs/security/security-review.md', 'docs/security/dependency-audit.md',
    'docs/security/nuget-vulnerability-report.json', 'docs/security/package-inventory.json',
    'docs/decisions/0001-application-foundation.md', 'docs/decisions/0002-m1-authentication-and-seed.md',
    'docs/releases/v1.0.0.md', 'docs/releases/demo-script.md', 'docs/releases/interview-notes.md',
    'scripts/Test-Repository.ps1', 'scripts/Test-Dependencies.ps1',
    'src/Stockroom.Web/Stockroom.Web.csproj', 'src/Stockroom.Web/packages.lock.json',
    'tests/README.md', 'tests/unit/README.md', 'tests/integration/README.md',
    'tests/e2e/README.md', 'tests/fixtures/README.md',
    'tests/unit/Stockroom.UnitTests.csproj',
    'tests/integration/Stockroom.IntegrationTests.csproj',
    'tests/e2e/Stockroom.E2ETests.csproj',
    'tests/unit/packages.lock.json', 'tests/integration/packages.lock.json',
    'tests/e2e/packages.lock.json'
)
$issues = [Collections.Generic.List[string]]::new()
$trackedFiles = @(git -C $repositoryRoot -c core.quotepath=false ls-files)
if ($LASTEXITCODE -ne 0) {
    throw 'Git could not list tracked files. Run this check inside the initialized repository.'
}

foreach ($relativePath in $requiredFiles) {
    if ($relativePath -notin $trackedFiles) {
        $issues.Add("Required file is not tracked: $relativePath")
    }
    if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot $relativePath) -PathType Leaf)) {
        $issues.Add("Required file is missing: $relativePath")
    }
}

$trackedSet = [Collections.Generic.HashSet[string]]::new([string[]]$trackedFiles, [StringComparer]::Ordinal)
$utf8 = [Text.UTF8Encoding]::new($false, $true)
$textExtensions = @('.md', '.ps1', '.yml', '.yaml', '.json', '.cs', '.cshtml', '.csproj', '.sln', '.slnx', '.props', '.targets', '.config')
$textNames = @('.gitignore', '.gitattributes', '.editorconfig', 'LICENSE')
$markdownLinkPattern = '\[[^\]\r\n]+\]\((?<target>[^)\r\n]+)\)'
$checkedCount = 0

foreach ($relativePath in $trackedFiles) {
    $extension = [IO.Path]::GetExtension($relativePath)
    $fileName = [IO.Path]::GetFileName($relativePath)
    $isGeneratedLockFile = $fileName -eq 'packages.lock.json'
    if ($extension -notin $textExtensions -and $fileName -notin $textNames) {
        continue
    }

    $fullPath = Join-Path $repositoryRoot $relativePath
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        $issues.Add("Tracked file is missing: $relativePath")
        continue
    }

    try {
        $content = $utf8.GetString([IO.File]::ReadAllBytes($fullPath))
    }
    catch {
        $issues.Add("Invalid UTF-8: $relativePath")
        continue
    }
    $checkedCount++
    if ($content.StartsWith([string][char]0xFEFF, [StringComparison]::Ordinal)) {
        $issues.Add("UTF-8 BOM found: $relativePath")
    }
    if (-not $isGeneratedLockFile -and $content.Contains("`r")) {
        $issues.Add("Use LF line endings: $relativePath")
    }
    if (-not $isGeneratedLockFile -and $content.Length -gt 0 -and -not $content.EndsWith("`n")) {
        $issues.Add("Missing final newline: $relativePath")
    }
    if ([regex]::IsMatch($content, '(?m)[\t ]+$')) {
        $issues.Add("Trailing whitespace: $relativePath")
    }

    if ($extension -ne '.md') {
        continue
    }
    foreach ($match in [regex]::Matches($content, $markdownLinkPattern)) {
        $target = $match.Groups['target'].Value.Trim()
        if ($target -match '^(https?://|mailto:|#)') {
            continue
        }
        $target = $target.Trim('<', '>')
        $target = [Uri]::UnescapeDataString(($target -split '[#?]', 2)[0])
        if ([IO.Path]::IsPathRooted($target)) {
            $issues.Add("Use a relative Markdown file link in ${relativePath}: $target")
            continue
        }
        $linkPath = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetDirectoryName($fullPath)) $target))
        $relativeLinkPath = [IO.Path]::GetRelativePath($repositoryRoot, $linkPath)
        if ($relativeLinkPath -eq '..' -or $relativeLinkPath -match '^\.\.[/\\]') {
            $issues.Add("Markdown link leaves the repository in ${relativePath}: $target")
            continue
        }
        if (-not (Test-Path -LiteralPath $linkPath)) {
            $issues.Add("Broken Markdown file link in ${relativePath}: $target")
            continue
        }
        # Windows and macOS resolve paths without regard to case; Linux and GitHub do not. A link must match the
        # tracked path's exact case, or for a directory, the exact case of a tracked file's leading folders.
        $trackedLink = $relativeLinkPath.Replace([IO.Path]::DirectorySeparatorChar, '/')
        if (-not ($trackedSet.Contains($trackedLink) -or $trackedSet.Contains("$trackedLink/") -or
                @($trackedFiles | Where-Object { $_.StartsWith("$trackedLink/", [StringComparison]::Ordinal) }).Count -gt 0)) {
            $issues.Add("Markdown link does not match the tracked path's case, or targets an untracked file, in ${relativePath}: $target")
        }
    }
}

if ($issues.Count -gt 0) {
    foreach ($issue in $issues) {
        Write-Output "FAIL: $issue"
    }
    throw "Repository checks failed with $($issues.Count) issue(s)."
}

Write-Output "Repository checks passed: $($requiredFiles.Count) required files and $checkedCount text files."
