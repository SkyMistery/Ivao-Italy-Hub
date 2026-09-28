#Requires -Version 5.1
<#
.SYNOPSIS
    Prepares a delivery of the hub for an FTP-only server: fetches the GitHub release of a tag, lists what
    changed against the previous delivery, writes the manifest of the files to upload and builds the zip.
    The runbook is docs/DELIVERING.md; the decision behind it is
    docs/internal/decisions/2026-09-27-la-consegna-del-pacchetto.md.

.DESCRIPTION
    The package is never built on this machine. It is the zip that .github/workflows/release.yml attached to
    the GitHub release of the tag, built on a clean checkout: its commit stamp is exact, and no file of this
    machine - secrets included - can be inside it. This script only takes that zip apart and puts together
    what is handed over.

    The rules it keeps come from the delivery of vIPI (tools/prepara-pacchetto.ps1 in that repository),
    where each of them was paid for once:

    - The zip is built from the DECLARED LIST (MANIFEST.txt, with hashes), never by walking a folder. On
      24 and 31 August 2026 the production secrets of vIPI (a connection string with its password, the IVAO
      ClientSecret) were sitting in the folder to upload, and walking the folder put them into the zip that
      was sent by mail. A secret file is protected only by its unguessable name; inside an attachment it is
      protected by nothing.
    - A second net looks INSIDE the declared text files: one holding a password, a secret, a key or a
      connection string stops the delivery even if somebody declared it.
    - The documents (docs/) never mix with the files to upload: they are two parallel branches of the zip.
    - An incremental package holds only files that differ from the previous full package, and they are
      CHOSEN by whoever delivers: the Diff action proposes, a person decides, the Manifest action records.
    - restart.txt is a tool, not a document: an empty file to upload into tmp/ last, so that Passenger
      restarts the application.

    Folders, all under artifacts/ (gitignored):

        publish/                        the CURRENT delivery only
            full-<version>/             the release, unpacked; RELEASE.txt lists what the zip held
            only-<N>-files-<version>/   what is uploaded, when it is not the full package
            docs/                       the sheets of this delivery
            candidates-<version>.txt    the list proposed by Diff, edited by hand
            delivery-<package>.zip      what is handed over, with its .sha256
            ivao-division-hub-v<version>.zip   the release asset as downloaded
        publish_old/<version>/          one folder per past delivery, with the sheets of the time

.PARAMETER Action
    Fetch     download the release zip of -Tag, check its stamp, move the current delivery to
              publish_old/<its version>/, unpack the release into publish/full-<version>/.
    Diff      compare publish/full-<version>/ with the previous full package by hash and write
              publish/candidates-<version>.txt, to be edited by hand.
    Manifest  from -List (the edited candidates), build the package folder and write its MANIFEST.txt.
              With -Full the package is publish/full-<version>/ itself.
    Zip       build publish/delivery-<package>.zip from MANIFEST.txt + docs/ + restart.txt, with the nets.

.PARAMETER Tag
    Fetch: the tag of the release, v<version> (e.g. v0.2.0).

.PARAMETER ReleaseZip
    Fetch: a release zip already on disk, instead of downloading it. For trying the script out.

.PARAMETER Commit
    Fetch: the commit the tag must point to. Read from origin when omitted; given only to try the script
    out on a zip whose tag does not exist.

.PARAMETER Against
    Diff: the version of the previous full package in publish_old/. Default: the highest one below the
    current version.

.PARAMETER List
    Manifest: a text file with one relative path per line (forward slashes); '#' starts a comment.

.PARAMETER Full
    Manifest: the delivery is the full package, publish/full-<version>/, instead of only-<N>-files-<version>/.

.PARAMETER Package
    Zip: the package folder under publish/ (full-0.2.0, only-31-files-0.2.0). Default: the only one there.

.PARAMETER Sheets
    Zip, required: the sheets for whoever uploads - files, or folders whose *.md and *.json are taken (only
    this version's among the names that carry a version). Copied from the repository every time.

.PARAMETER ArtifactsDir
    The artifacts folder. Default: artifacts/ at the root of the repository. For trying the script out.

.EXAMPLE
    .\tools\prepare-delivery.ps1 -Action Fetch -Tag v0.2.0 -WhatIf
    .\tools\prepare-delivery.ps1 -Action Fetch -Tag v0.2.0
    .\tools\prepare-delivery.ps1 -Action Diff
    .\tools\prepare-delivery.ps1 -Action Manifest -List artifacts\publish\candidates-0.2.0.txt
    .\tools\prepare-delivery.ps1 -Action Zip -Sheets docs\internal\deploy
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)][ValidateSet('Fetch', 'Diff', 'Manifest', 'Zip')][string]$Action,
    [string]$Tag,
    [string]$ReleaseZip,
    [string]$Commit,
    [string]$Against,
    [string]$List,
    [switch]$Full,
    [string]$Package,
    [string[]]$Sheets,
    [string]$ArtifactsDir
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $ArtifactsDir) { $ArtifactsDir = Join-Path $repoRoot 'artifacts' }
$publish = Join-Path $ArtifactsDir 'publish'
$old = Join-Path $ArtifactsDir 'publish_old'

# The name release.yml gives the asset. Change both together.
function Get-AssetName([string]$tag) { "ivao-division-hub-$tag.zip" }

# The one assembly whose stamp names the package: /api/version reads it.
$stampAssembly = 'IvaoHub.Web.dll'

$utf8 = New-Object System.Text.UTF8Encoding($false)
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem

# Temporary folders of a Fetch, removed when the script stops (best effort: they are under %TEMP%).
$scratch = New-Object System.Collections.Generic.List[string]

function Stop-Delivery([string]$message) {
    Write-Host ''
    Write-Host "STOPPED: $message" -ForegroundColor Red
    foreach ($dir in $scratch) { try { Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction Stop } catch { } }
    exit 1
}

function Invoke-Step([string]$target, [string]$what, [scriptblock]$step) {
    if ($PSCmdlet.ShouldProcess($target, $what)) { & $step }
}

function ConvertTo-Native([string]$relative) { $relative -replace '/', '\' }

# git, with its stderr thrown away. Under 'Stop', PowerShell 5.1 turns any line a native program writes
# to stderr into a terminating error, even with 2>$null: a "not a valid object" would end the script
# instead of being an answer. The caller reads $LASTEXITCODE.
function Invoke-Git {
    $ErrorActionPreference = 'Continue'
    & git -C $repoRoot @args 2>$null
}

function Get-Sha256([string]$path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLower() }

function Get-VersionKey([string]$version) {
    if ($version -match '^(\d+)\.(\d+)\.(\d+)') { return [version]"$($Matches[1]).$($Matches[2]).$($Matches[3])" }
    return $null
}

function Format-HashLine([string]$hash, [long]$size, [string]$relative) {
    '{0}  {1,10} B  {2}' -f $hash, $size, $relative
}

# A hash list: RELEASE.txt (what the release held) or MANIFEST.txt (what is delivered). Returns an
# ordered map path -> @{ Hash; Size } and the header keys.
function Read-HashList([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { Stop-Delivery "$path is missing." }
    $files = [ordered]@{}
    $header = @{}
    foreach ($line in [IO.File]::ReadAllLines($path, $utf8)) {
        if ($line -match '^([0-9a-f]{64})\s+(\d+)\s+B\s+(.+)$') {
            $files[$Matches[3].Trim()] = @{ Hash = $Matches[1]; Size = [long]$Matches[2] }
        }
        elseif ($line -match '^([a-z]+)\s{2,}(.+)$') { $header[$Matches[1]] = $Matches[2].Trim() }
    }
    return @{ Files = $files; Header = $header }
}

function Read-PathList([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { Stop-Delivery "the list $path does not exist." }
    $paths = New-Object System.Collections.Generic.List[string]
    foreach ($line in [IO.File]::ReadAllLines($path, $utf8)) {
        $entry = ($line -replace '\s+#.*$', '').Trim()
        if ($entry -eq '' -or $entry.StartsWith('#')) { continue }
        $entry = $entry -replace '\\', '/'
        if ($paths.Contains($entry)) { Stop-Delivery "the list names $entry twice." }
        $paths.Add($entry)
    }
    return , $paths
}

# What belongs to an installation and never to a package (README "Packaging", plan section 11.3 point 2).
# A declared path under one of these stops the delivery whatever it contains.
$installationPaths = @(
    '^secrets/', '^hub-keys/', '^media/', '^logs/', '^diagnostics/', '^tmp/',
    '^config/division\.json$', '^config/ivao-oauth\.json$', '\.env$', '\.pfx$', '\.p12$', '\.pem$', '\.key$'
)

function Test-InstallationPath([string]$relative) {
    foreach ($pattern in $installationPaths) { if ($relative -match $pattern) { return $pattern } }
    return $null
}

# The second net. It looks INSIDE the text files, because a name says nothing: the secrets file has an
# unguessable name on purpose. It reports the KEY that gave it away, never the value, so its output can be
# pasted into a chat without thinking.
#
# Only text files. vIPI's first version read every small file and accused its own Web assembly, where
# "ClientSecret" is the NAME of a configuration key written in the code; an alarm that rings on every
# delivery over a file that must be there is how people stop reading alarms.
#
# And only a key WITH A VALUE. The package carries config/*.example.json on purpose (README "Packaging"),
# which name "ClientSecret" with the placeholder "<client secret>", and a delivery's sheets may carry a
# template of the secrets file for the administrator to fill in on the server. A value that is empty, a
# <placeholder> or UPPER-CASE-WORDS-WITH-DASHES (no digit, so no GUID) is a form to fill in, not a
# credential; anything else is.
$textExtensions = @('.json', '.txt', '.xml', '.config', '.env', '.ini', '.yml', '.yaml', '.md')
$secretKey = '"(?<key>[A-Za-z0-9_.:$-]*(?:password|passwd|pwd|secret|apikey|api_key|token|privatekey))"\s*:\s*"(?<value>[^"]*)"'
$secretInString = '(?i)\b(?<key>password|pwd)\s*=\s*(?<value>[^;"\s]*)'

function Test-Placeholder([string]$value) {
    $v = $value.Trim()
    return ($v -eq '' -or $v -match '^<[^>]*>$' -or $v -cmatch '^[A-Z]+(-[A-Z]+)+$')
}

function Find-Credentials([string]$folder, [string[]]$relatives) {
    $found = @()
    foreach ($relative in $relatives) {
        $path = Join-Path $folder (ConvertTo-Native $relative)
        if ($textExtensions -notcontains [IO.Path]::GetExtension($path).ToLower()) { continue }
        if ((Get-Item -LiteralPath $path).Length -gt 4MB) { continue }
        $text = [IO.File]::ReadAllText($path)
        $clue = $null
        foreach ($m in @([regex]::Matches($text, $secretKey, 'IgnoreCase')) + @([regex]::Matches($text, $secretInString))) {
            if (-not (Test-Placeholder $m.Groups['value'].Value)) { $clue = "a value under '$($m.Groups['key'].Value)'"; break }
        }
        if (-not $clue -and $text -match '-----BEGIN [A-Z ]*PRIVATE KEY-----') { $clue = 'a private key' }
        if (-not $clue -and $text -match '<key id=') { $clue = 'a Data Protection key' }
        if ($clue) { $found += [pscustomobject]@{ File = $relative; Clue = $clue } }
    }
    return $found
}

# The one delivery in publish/, by its full-<version> folder. Exactly one, or the script does not guess:
# vIPI once had two in the same folder, and taking "the first one" would have archived the delivery that
# was online under the wrong name and diffed against the wrong one.
function Get-CurrentFull {
    if (-not (Test-Path -LiteralPath $publish)) { return $null }
    $all = @(Get-ChildItem -LiteralPath $publish -Directory -Filter 'full-*')
    if ($all.Count -gt 1) {
        Stop-Delivery ("$publish holds $($all.Count) full-* folders: $($all.Name -join ', '). I do not know which " +
                       'one is the current delivery: move the others by hand, then run again.')
    }
    if ($all.Count -eq 0) { return $null }
    return @{ Dir = $all[0].FullName; Version = $all[0].Name.Substring(5) }
}

function Get-Release([string]$fullDir) { Read-HashList (Join-Path $fullDir 'RELEASE.txt') }

switch ($Action) {

    # -- Fetch -------------------------------------------------------------------------------------------
    'Fetch' {
        if (-not $Tag) { Stop-Delivery 'Fetch needs -Tag (e.g. v0.2.0).' }
        if ($Tag -notmatch '^v\d+\.\d+\.\d+') { Stop-Delivery "the tag '$Tag' is not v<version>." }
        $version = $Tag.Substring(1)
        $asset = Get-AssetName $Tag

        $current = Get-CurrentFull
        if ($current) {
            if ($current.Version -eq $version) { Stop-Delivery "full-$version is already the current delivery in $publish." }
            if ((Get-VersionKey $version) -lt (Get-VersionKey $current.Version)) {
                Stop-Delivery ("$version is older than the current delivery $($current.Version). A delivery goes " +
                               'forward; going back is the zip of that version in publish_old/.')
            }
            $rotateTo = Join-Path $old $current.Version
            if (Test-Path -LiteralPath $rotateTo) { Stop-Delivery "$rotateTo already exists: a delivery is not rotated twice." }
        }
        elseif ((Test-Path -LiteralPath $publish) -and @(Get-ChildItem -LiteralPath $publish -Force).Count -gt 0) {
            Stop-Delivery "$publish holds files but no full-* folder: I do not know which delivery they belong to. Move them by hand."
        }

        # The commit the tag names. The package must carry exactly that one in its stamp.
        if (-not $Commit) {
            $refs = @(Invoke-Git ls-remote --tags origin "refs/tags/$Tag" "refs/tags/$Tag^{}")
            if ($LASTEXITCODE -ne 0 -or $refs.Count -eq 0) { Stop-Delivery "origin has no tag $Tag." }
            $peeled = @($refs | Where-Object { $_ -match '\^\{\}$' })
            $Commit = ((@($peeled + $refs))[0] -split '\s+')[0]
        }
        $Commit = $Commit.ToLower()
        Write-Host "Tag $Tag -> commit $Commit" -ForegroundColor Cyan

        if ($ReleaseZip) {
            if (-not (Test-Path -LiteralPath $ReleaseZip)) { Stop-Delivery "$ReleaseZip does not exist." }
            $zipPath = (Resolve-Path -LiteralPath $ReleaseZip).Path
            Write-Host "Release zip given: $zipPath"
        }
        else {
            $download = Join-Path $env:TEMP ('hub-release-' + [guid]::NewGuid().ToString('N'))
            $zipPath = Join-Path $download $asset
            $isDryRun = $true
            Invoke-Step $asset "download from the GitHub release $Tag" { $script:isDryRun = $false }
            if ($isDryRun) {
                Write-Host '(dry run: nothing downloaded, so nothing else can be checked)' -ForegroundColor Yellow
                if ($current) {
                    Write-Host "Would rotate publish/full-$($current.Version) and its companions to publish_old/$($current.Version)/."
                }
                Write-Host "Would unpack the release into publish/full-$version/."
                break
            }
            New-Item -ItemType Directory -Force -Path $download | Out-Null; $scratch.Add($download)
            Push-Location $repoRoot
            try { gh release download $Tag --pattern $asset --dir $download } finally { Pop-Location }
            if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $zipPath)) { Stop-Delivery "could not download $asset of $Tag." }
        }
        $zipHash = Get-Sha256 $zipPath

        # Read the zip. Entries are checked before anything is written: no absolute path, no '..'.
        $archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
        try {
            $entries = @($archive.Entries | Where-Object { -not $_.FullName.EndsWith('/') })
            foreach ($e in $entries) {
                $name = $e.FullName -replace '\\', '/'
                if ($name.StartsWith('/') -or $name -match '(^|/)\.\.(/|$)' -or $name -match '^[A-Za-z]:') {
                    Stop-Delivery "the release zip holds an entry outside its folder: $name"
                }
                $owned = Test-InstallationPath $name
                if ($owned) {
                    Stop-Delivery ("the release zip holds $name, which belongs to an installation ($owned). " +
                                   'The release is wrong: do not deliver it.')
                }
            }
            if (-not ($entries | Where-Object { $_.FullName -eq $stampAssembly })) {
                Stop-Delivery "the release zip has no $stampAssembly at its root: it is not a package of the hub."
            }
            Write-Host "Release zip: $($entries.Count) files, sha256 $zipHash"

            $unpack = Join-Path $env:TEMP ('hub-unpack-' + [guid]::NewGuid().ToString('N'))
            $isDryRun = $true
            Invoke-Step "publish/full-$version" 'unpack the release, check its stamp, rotate the current delivery' {
                $script:isDryRun = $false
            }
            if ($isDryRun) {
                if ($current) {
                    Write-Host "Would rotate publish/full-$($current.Version) and its companions to publish_old/$($current.Version)/."
                }
                Write-Host "Would unpack $($entries.Count) files into publish/full-$version/."
                break
            }
            New-Item -ItemType Directory -Force -Path $unpack | Out-Null; $scratch.Add($unpack)
            $lines = New-Object System.Collections.Generic.List[string]
            foreach ($e in ($entries | Sort-Object FullName)) {
                $name = $e.FullName -replace '\\', '/'
                $dest = Join-Path $unpack (ConvertTo-Native $name)
                New-Item -ItemType Directory -Force -Path (Split-Path $dest) | Out-Null
                [IO.Compression.ZipFileExtensions]::ExtractToFile($e, $dest, $true)
                $lines.Add((Format-HashLine (Get-Sha256 $dest) $e.Length $name))
            }
        }
        finally { $archive.Dispose() }

        # The stamp: <version>+<commit> in the version resource of the Web assembly, the same string
        # /api/version shows. A package whose stamp is not the tag's is not the package of that tag.
        $stamp = (Get-Item -LiteralPath (Join-Path $unpack $stampAssembly)).VersionInfo.ProductVersion
        if ($stamp -ne "$version+$Commit") {
            Stop-Delivery "the stamp of the package is '$stamp', the tag says '$version+$Commit'. Nothing was moved."
        }
        Write-Host "Stamp $stamp - matches the tag." -ForegroundColor Green

        $release = New-Object System.Collections.Generic.List[string]
        $release.Add("IVAO Division Hub - the GitHub release $Tag, as downloaded")
        $release.Add("tag      $Tag")
        $release.Add("version  $version")
        $release.Add("commit   $Commit")
        $release.Add("stamp    $stamp")
        $release.Add("asset    $asset")
        $release.Add("sha256   $zipHash")
        $release.Add("fetched  $((Get-Date).ToString('yyyy-MM-dd HH:mm'))")
        $release.Add("files    $($lines.Count)")
        $release.Add(('=' * 100))
        $release.AddRange($lines)
        [IO.File]::WriteAllLines((Join-Path $unpack 'RELEASE.txt'), $release, $utf8)

        # Only now, with a package that is right, the current delivery becomes history - with ITS sheets,
        # which are never updated again: they are what we told whoever uploaded it, that day.
        if ($current) {
            New-Item -ItemType Directory -Force -Path $rotateTo | Out-Null
            foreach ($item in @(Get-ChildItem -LiteralPath $publish -Force)) {
                Move-Item -LiteralPath $item.FullName -Destination (Join-Path $rotateTo $item.Name)
            }
            Write-Host "Rotated: the delivery $($current.Version) is now in $rotateTo" -ForegroundColor Green
        }
        New-Item -ItemType Directory -Force -Path $publish | Out-Null
        Move-Item -LiteralPath $unpack -Destination (Join-Path $publish "full-$version")
        Copy-Item -LiteralPath $zipPath -Destination (Join-Path $publish $asset)
        if (-not $ReleaseZip) { Remove-Item -LiteralPath (Split-Path $zipPath) -Recurse -Force }
        Write-Host "Done: publish/full-$version ($($lines.Count) files) and publish/$asset" -ForegroundColor Green
        Write-Host 'Next: -Action Diff.'
    }

    # -- Diff --------------------------------------------------------------------------------------------
    # The hash says what DIFFERS. It does not say what must go: that is chosen by a person, reading the
    # list this writes. What the script knows for certain it writes as a comment next to the line.
    'Diff' {
        $current = Get-CurrentFull
        if (-not $current) { Stop-Delivery "no full-* folder in $publish. Run -Action Fetch first." }
        $version = $current.Version
        $now = Get-Release $current.Dir

        $previous = $null
        if (Test-Path -LiteralPath $old) {
            $candidates = @(Get-ChildItem -LiteralPath $old -Directory | Where-Object {
                (Test-Path -LiteralPath (Join-Path $_.FullName "full-$($_.Name)\RELEASE.txt")) -and (Get-VersionKey $_.Name)
            })
            if ($Against) { $candidates = @($candidates | Where-Object { $_.Name -eq $Against }) }
            else { $candidates = @($candidates | Where-Object { (Get-VersionKey $_.Name) -lt (Get-VersionKey $version) }) }
            $pick = @($candidates | Sort-Object { Get-VersionKey $_.Name } -Descending)
            if ($pick.Count -gt 0) { $previous = @{ Version = $pick[0].Name; Dir = Join-Path $pick[0].FullName "full-$($pick[0].Name)" } }
            elseif ($Against) { Stop-Delivery "publish_old/$Against/full-$Against/RELEASE.txt does not exist." }
        }

        $out = New-Object System.Collections.Generic.List[string]
        $changed = @(); $added = @(); $removed = @()
        if ($previous) {
            $before = Get-Release $previous.Dir
            foreach ($p in $now.Files.Keys) {
                if (-not $before.Files.Contains($p)) { $added += $p }
                elseif ($before.Files[$p].Hash -ne $now.Files[$p].Hash) { $changed += $p }
            }
            foreach ($p in $before.Files.Keys) { if (-not $now.Files.Contains($p)) { $removed += $p } }
            $out.Add("# Candidates for the delivery $version, against the full package $($previous.Version)")
            $out.Add("# ($($now.Header['commit']) against $($before.Header['commit'])): " +
                     "$($changed.Count) changed, $($added.Count) new, $($removed.Count) removed.")
        }
        else {
            $added = @($now.Files.Keys)
            $out.Add("# Candidates for the delivery $version. There is no previous full package in publish_old/:")
            $out.Add('# this is a FULL delivery. Build it with -Action Manifest -Full.')
        }
        $out.Add('#')
        $out.Add('# The hash says what differs, not what must go. Read every line; delete or comment out what must')
        $out.Add('# not be uploaded, then: -Action Manifest -List <this file>. The rules are in docs/DELIVERING.md.')
        $out.Add('')

        $hubAssembly = '^IvaoHub\.[A-Za-z.]+\.(dll|pdb|xml)$|^IvaoHub\.Web(\.deps|\.runtimeconfig)\.json$|^IvaoHub\.Web$'
        $sections = [ordered]@{
            ('The hub''s own assemblies. They ALL go, together: the version is part of every assembly''s identity, ' +
             'and a new Web.dll referencing a Core.dll of the previous version on the server does not load.') = @()
            ('wwwroot: the SPA. index.html names the new hashed files, so every new one goes with it, and so does ' +
             'the index of the static assets; old ones stay on the server, unused.') = @()
            'Language files, templates, configuration examples, legal files.' = @()
            'Everything else: the runtime and third-party libraries. A change here is a package or SDK upgrade.' = @()
        }
        $keys = @($sections.Keys)
        foreach ($p in @($changed + $added | Sort-Object)) {
            $tagText = if ($added -contains $p) { 'new' } else { 'changed' }
            $line = "$p  # $tagText"
            if ($p -match '(^|/)appsettings\.[^/]+\.json$') {
                $line = "# $p  # $tagText - an environment's settings file from the repository; " +
                        'production does not read it. Never delivered.'
            }
            if ($p -match $hubAssembly) { $sections[$keys[0]] += $line }
            elseif ($p -match '^wwwroot/|\.staticwebassets\.endpoints\.json$') { $sections[$keys[1]] += $line }
            elseif ($p -match '^(locales|seed|config)/|^(LICENSE|NOTICE)$') { $sections[$keys[2]] += $line }
            else { $sections[$keys[3]] += $line }
        }
        foreach ($k in $keys) {
            if ($sections[$k].Count -eq 0) { continue }
            $out.Add("# -- $k")
            $out.AddRange([string[]]$sections[$k])
            $out.Add('')
        }
        if ($removed.Count -gt 0) {
            $out.Add('# -- Removed since the previous package. They stay on the server, unused: name them in the sheet.')
            foreach ($p in $removed) { $out.Add("#    $p") }
            $out.Add('')
        }

        # What git knows between the two commits and a hash cannot say.
        if ($previous) {
            $from = $before.Header['commit']; $to = $now.Header['commit']
            Invoke-Git cat-file -e "$from^{commit}" | Out-Null; $haveFrom = ($LASTEXITCODE -eq 0)
            Invoke-Git cat-file -e "$to^{commit}" | Out-Null; $haveTo = ($LASTEXITCODE -eq 0)
            if ($haveFrom -and $haveTo) {
                $migrations = @(Invoke-Git diff --name-only $from $to -- 'src/*/Migrations/*.cs' |
                    Where-Object { $_ -notmatch '(Designer|ModelSnapshot)\.cs$' })
                if ($migrations.Count -gt 0) {
                    $out.Add("# -- MIGRATIONS between the two commits: $($migrations.Count). They run at start-up, alone:")
                    $out.Add('#    read the version rule in Directory.Build.props before delivering.')
                    foreach ($m in $migrations) { $out.Add("#    $m") }
                }
                else { $out.Add('# -- No migration between the two commits.') }
            }
            else {
                $out.Add("# -- git does not have both commits ($from, $to): run 'git fetch' to see the migrations between them.")
            }
        }

        $listPath = Join-Path $publish "candidates-$version.txt"
        Invoke-Step $listPath 'write the candidate list' { [IO.File]::WriteAllLines($listPath, $out, $utf8) }
        $out | ForEach-Object { Write-Host $_ }
    }

    # -- Manifest ----------------------------------------------------------------------------------------
    # The declared list. From here on IT says what goes into the zip, never a folder.
    'Manifest' {
        if (-not $List) { Stop-Delivery 'Manifest needs -List: the candidate list, edited.' }
        $current = Get-CurrentFull
        if (-not $current) { Stop-Delivery "no full-* folder in $publish. Run -Action Fetch first." }
        $version = $current.Version
        $release = Get-Release $current.Dir
        $paths = Read-PathList $List
        if ($paths.Count -eq 0) { Stop-Delivery "$List declares no file." }

        foreach ($p in $paths) {
            if (-not $release.Files.Contains($p)) { Stop-Delivery "the list names $p, which is not in the release $version." }
            $owned = Test-InstallationPath $p
            if ($owned) { Stop-Delivery "the list names $p, which belongs to an installation ($owned)." }
            $onDisk = Join-Path $current.Dir (ConvertTo-Native $p)
            if (-not (Test-Path -LiteralPath $onDisk)) { Stop-Delivery "$p is in the release but not in full-$version any more." }
            if ((Get-Sha256 $onDisk) -ne $release.Files[$p].Hash) {
                Stop-Delivery "$p in full-$version is not the file the release held: it was changed after the download. Fetch again."
            }
        }
        # The hub's assemblies go all together or not at all: every release raises the version, the version
        # is part of each assembly's identity, and a new IvaoHub.Web.dll next to an IvaoHub.Core.dll of the
        # previous version does not load.
        $hubDlls = @($release.Files.Keys | Where-Object { $_ -match '^IvaoHub\.[A-Za-z.]+\.dll$' })
        $chosenDlls = @($paths | Where-Object { $hubDlls -contains $_ })
        if ($chosenDlls.Count -gt 0 -and $chosenDlls.Count -ne $hubDlls.Count) {
            $absent = @($hubDlls | Where-Object { $chosenDlls -notcontains $_ })
            Stop-Delivery ("the list carries $($chosenDlls.Count) of the $($hubDlls.Count) assemblies of the hub. They go together: " +
                           "add $($absent -join ', ').")
        }
        $suspects = @(Find-Credentials $current.Dir ([string[]]$paths))
        if ($suspects.Count -gt 0) {
            foreach ($s in $suspects) { Write-Host ("  {0}  -> holds {1}" -f $s.File, $s.Clue) -ForegroundColor Red }
            Stop-Delivery ('a declared file seems to hold a credential. Before asking how to silence the net, ask whether ' +
                           'that file must be delivered at all; almost always it must not.')
        }

        if ($Full) {
            $name = "full-$version"
            $target = $current.Dir
            $missing = @($release.Files.Keys | Where-Object { $paths -notcontains $_ })
            if ($missing.Count -gt 0) {
                Write-Host "Left out of the full package ($($missing.Count)), as the list says:" -ForegroundColor Yellow
                $missing | ForEach-Object { Write-Host "    $_" -ForegroundColor Yellow }
            }
        }
        else {
            $name = "only-$($paths.Count)-files-$version"
            $target = Join-Path $publish $name
            $stale = @(Get-ChildItem -LiteralPath $publish -Directory -Filter "only-*-files-$version")
            if ($stale.Count -gt 0) {
                Stop-Delivery ("$($stale.Name -join ', ') already exists in publish/. It is built by this action from the list: " +
                               'delete it by hand and run again.')
            }
        }

        $lines = New-Object System.Collections.Generic.List[string]
        $lines.Add("IVAO Division Hub $version - $name - sha256 of the $($paths.Count) files to upload")
        $lines.Add("commit   $($release.Header['commit'])")
        $lines.Add(('=' * 100))
        foreach ($p in $paths) { $lines.Add((Format-HashLine $release.Files[$p].Hash $release.Files[$p].Size $p)) }

        Invoke-Step $target "write MANIFEST.txt for $($paths.Count) files" {
            if (-not $Full) {
                foreach ($p in $paths) {
                    $dest = Join-Path $target (ConvertTo-Native $p)
                    New-Item -ItemType Directory -Force -Path (Split-Path $dest) | Out-Null
                    Copy-Item -LiteralPath (Join-Path $current.Dir (ConvertTo-Native $p)) -Destination $dest
                }
            }
            [IO.File]::WriteAllLines((Join-Path $target 'MANIFEST.txt'), $lines, $utf8)
            Write-Host "Done: publish/$name/MANIFEST.txt, $($paths.Count) files." -ForegroundColor Green
            Write-Host 'Next: -Action Zip.'
        }
    }

    # -- Zip ---------------------------------------------------------------------------------------------
    'Zip' {
        $current = Get-CurrentFull
        if (-not $current) { Stop-Delivery "no full-* folder in $publish. Run -Action Fetch first." }
        $version = $current.Version
        if (-not $Package) {
            $declared = @(Get-ChildItem -LiteralPath $publish -Directory |
                Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'MANIFEST.txt') })
            if ($declared.Count -ne 1) {
                Stop-Delivery ("$($declared.Count) folders of publish/ have a MANIFEST.txt ($($declared.Name -join ', ')): " +
                               'name the package with -Package.')
            }
            $Package = $declared[0].Name
        }
        if (-not $Sheets) {
            Stop-Delivery ('Zip needs -Sheets: the sheets for whoever uploads, files or folders of the repository ' +
                           '(e.g. docs\internal\deploy). A delivery without its sheet is not a delivery.')
        }
        $folder = Join-Path $publish $Package
        $manifest = Join-Path $folder 'MANIFEST.txt'
        if (-not (Test-Path -LiteralPath $manifest)) {
            Stop-Delivery "$manifest is missing: the zip is built from there, never from the folder. See -Action Manifest."
        }
        $declaredFiles = (Read-HashList $manifest).Files
        if ($declaredFiles.Count -eq 0) { Stop-Delivery "$manifest declares no file." }
        $paths = [string[]]@($declaredFiles.Keys)

        # FIRST NET: what is in the folder and nobody declared stays out, and is said out loud.
        $known = @('MANIFEST.txt', 'RELEASE.txt')
        $onDisk = @(Get-ChildItem -LiteralPath $folder -Recurse -File -Force | ForEach-Object {
            $_.FullName.Substring($folder.Length + 1) -replace '\\', '/'
        })
        $undeclared = @($onDisk | Where-Object { $known -notcontains $_ -and $paths -notcontains $_ })
        # In a full package the folder is the release itself, so the files the list left out on purpose are
        # still there. One that is byte for byte the file the release held was built by CI on a clean
        # checkout: it is left out, not an intruder. Anything else - not in the release, or changed since -
        # came from this machine.
        $release = (Get-Release $current.Dir).Files
        $leftOut = @($undeclared | Where-Object {
            $release.Contains($_) -and (Get-Sha256 (Join-Path $folder (ConvertTo-Native $_))) -eq $release[$_].Hash
        })
        $intruders = @($undeclared | Where-Object { $leftOut -notcontains $_ })
        if ($leftOut.Count -gt 0) {
            Write-Host "Left out by the list, unchanged since the release ($($leftOut.Count)):"
            $leftOut | ForEach-Object { Write-Host "    $_" }
        }
        if ($intruders.Count -gt 0) {
            Write-Host ''
            Write-Host 'FILES IN THE FOLDER THAT THE MANIFEST DOES NOT DECLARE. They stay out of the zip:' -ForegroundColor Yellow
            $intruders | ForEach-Object { Write-Host "    $_" -ForegroundColor Yellow }
            Write-Host '  If one of them had to be delivered, it goes into the list (-Action Manifest), never into the zip on the side.'
        }

        # Every declared file must still be the one declared.
        foreach ($p in $paths) {
            $path = Join-Path $folder (ConvertTo-Native $p)
            if (-not (Test-Path -LiteralPath $path)) { Stop-Delivery "the manifest declares $p, which is not in $Package." }
            if ((Get-Sha256 $path) -ne $declaredFiles[$p].Hash) {
                Stop-Delivery "$p is not the file the manifest declares: it changed after -Action Manifest."
            }
            $owned = Test-InstallationPath $p
            if ($owned) { Stop-Delivery "the manifest declares $p, which belongs to an installation ($owned)." }
        }

        # SECOND NET: inside the declared files, and inside the intruders too, so that a secret lying in the
        # folder is reported even though it would have stayed out.
        # The @() matters: with ONE suspect PowerShell 5.1 hands back a scalar, whose .Count is not 1 - in
        # vIPI this net stayed silent exactly in the case it exists for.
        $suspects = @(Find-Credentials $folder ([string[]]@($paths + $intruders)))
        if ($suspects.Count -gt 0) {
            Write-Host ''
            foreach ($s in $suspects) { Write-Host ("  {0}  -> holds {1}" -f $s.File, $s.Clue) -ForegroundColor Red }
            Stop-Delivery ('a file of the package, or lying in its folder, seems to hold a credential. Take it out of the folder ' +
                           'and run again. Before asking how to silence the net, ask whether that file must be delivered at all.')
        }

        # The sheets are copied from the repository every time: a copy of its own ages by itself. A folder
        # gives its *.md and *.json, and among the names that carry a version only this version's.
        $docs = Join-Path $publish 'docs'
        $sheetFiles = @()
        foreach ($s in $Sheets) {
            if (-not (Test-Path -LiteralPath $s)) { Stop-Delivery "the sheet $s does not exist." }
            $item = Get-Item -LiteralPath $s
            if ($item.PSIsContainer) {
                $sheetFiles += @(Get-ChildItem -LiteralPath $item.FullName -File | Where-Object {
                    @('.md', '.json') -contains $_.Extension.ToLower() -and
                    ($_.BaseName -notmatch '\d+\.\d+\.\d+' -or $_.BaseName -match ('(^|[^0-9.])' + [regex]::Escape($version) + '$'))
                })
            }
            else { $sheetFiles += $item }
        }
        $twice = @($sheetFiles | Group-Object Name | Where-Object { $_.Count -gt 1 })
        if ($twice.Count -gt 0) { Stop-Delivery "two sheets have the same name: $($twice.Name -join ', ')." }
        if (-not ($sheetFiles | Where-Object { $_.Name -match [regex]::Escape($version) })) {
            Write-Host "  (no sheet names ${version}: the delivery goes out without a sheet of its own)" -ForegroundColor Yellow
        }
        # The net looks at the sheets too: a template of the secrets file travels with them, and a template
        # filled in "just to try" is the file this whole script exists to keep out.
        foreach ($f in $sheetFiles) {
            $hit = @(Find-Credentials $f.DirectoryName @($f.Name))
            if ($hit.Count -gt 0) {
                Stop-Delivery "the sheet $($f.FullName) holds $($hit[0].Clue). A sheet carries placeholders, never values."
            }
        }
        $kind = if ($Package -like 'full-*') { 'full' } else { 'changed files' }
        $zip = Join-Path $publish "delivery-$Package.zip"
        Write-Host ''
        Write-Host ("Zip ($kind): $($paths.Count) declared files + MANIFEST.txt, " +
                    "$($sheetFiles.Count) sheets in docs/, restart.txt") -ForegroundColor Cyan

        Invoke-Step $zip 'build the delivery zip' {
            if (Test-Path -LiteralPath $docs) { Remove-Item -LiteralPath $docs -Recurse -Force }
            New-Item -ItemType Directory -Force -Path $docs | Out-Null
            foreach ($f in $sheetFiles) { Copy-Item -LiteralPath $f.FullName -Destination (Join-Path $docs $f.Name) }
            $restart = Join-Path $publish 'restart.txt'
            [IO.File]::WriteAllText($restart, '', $utf8)

            # Entries are written by hand, never with Compress-Archive: the one of PowerShell 5.1 writes them
            # with BACKSLASHES, which the zip format does not allow and which an extractor on Linux turns
            # into flat files called "bin\IvaoHub.Web.dll" (measured in vIPI, 13 September 2026).
            $entries = New-Object System.Collections.Generic.List[object]
            foreach ($p in @($paths + 'MANIFEST.txt')) { $entries.Add(@((Join-Path $folder (ConvertTo-Native $p)), "$Package/$p")) }
            foreach ($f in $sheetFiles) { $entries.Add(@((Join-Path $docs $f.Name), "docs/$($f.Name)")) }
            $entries.Add(@($restart, 'restart.txt'))

            if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
            if (Test-Path -LiteralPath "$zip.sha256") { Remove-Item -LiteralPath "$zip.sha256" -Force }
            $archive = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Create)
            try {
                foreach ($e in $entries) {
                    [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                        $archive, $e[0], $e[1], [IO.Compression.CompressionLevel]::Optimal) | Out-Null
                }
            }
            finally { $archive.Dispose() }

            $read = [IO.Compression.ZipFile]::OpenRead($zip)
            try {
                $bent = @($read.Entries | Where-Object { $_.FullName.Contains('\') })
                $count = $read.Entries.Count
            }
            finally { $read.Dispose() }
            if ($bent.Count -gt 0) {
                Stop-Delivery "the zip has $($bent.Count) entries with a backslash: an extractor on Linux would write them flat."
            }
            if ($count -ne $entries.Count) { Stop-Delivery "the zip has $count entries, $($entries.Count) were written." }

            $hash = Get-Sha256 $zip
            [IO.File]::WriteAllText("$zip.sha256", "$hash  $(Split-Path $zip -Leaf)`n", $utf8)
            $size = (Get-Item -LiteralPath $zip).Length / 1MB
            Write-Host ('Done: {0}  {1:N2} MB' -f (Split-Path $zip -Leaf), $size) -ForegroundColor Green
            Write-Host "sha256 $hash"
        }
    }
}
exit 0
