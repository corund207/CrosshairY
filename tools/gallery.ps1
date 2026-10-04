# Gallery automation, run by .github/workflows/gallery.yml (needs gh + GH_TOKEN).
#   -Mode review   validate a submission issue, post a preview comment, set labels
#   -Mode publish  (after a maintainer adds the "approved" label) add it to gallery/gallery.json, commit, close the issue
# Local test: .\tools\gallery.ps1 -Mode review -Issue 12 -DryRun
param(
    [ValidateSet('review', 'publish')] [string]$Mode = 'review',
    [Parameter(Mandatory)] [int]$Issue,
    [string]$Sender = '',
    [switch]$DryRun
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$repo = if ($env:GITHUB_REPOSITORY) { $env:GITHUB_REPOSITORY } else { 'corund207/Reticly' }
$work = Join-Path $root 'build\gallery'
New-Item -ItemType Directory -Force $work | Out-Null

# commits made by the automation use the maintainer's identity
$gitUser = @('-c', 'user.name=Jonah', '-c', 'user.email=190765838+corund207@users.noreply.github.com')

# ---------- build the tool (same compiler as build.ps1) ----------
$csc = Join-Path $root '.tools\roslyn\tasks\net472\csc.exe'
if (-not (Test-Path $csc)) {
    $tools = Join-Path $root '.tools'
    New-Item -ItemType Directory -Force $tools | Out-Null
    $zip = Join-Path $tools 'roslyn.zip'
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest 'https://www.nuget.org/api/v2/package/Microsoft.Net.Compilers.Toolset/4.14.0' -OutFile $zip
    Expand-Archive $zip (Join-Path $tools 'roslyn') -Force
    Remove-Item $zip
}
$exe = Join-Path $work 'GalleryTool.exe'
$sources = @('Core', 'Render', 'Input', 'Overlay', 'Import') | ForEach-Object { Get-ChildItem (Join-Path $root "src\$_") -Filter *.cs } | ForEach-Object { $_.FullName }
& $csc -nologo -langversion:latest -nowarn:CS0649,CS0169,CS0414 "-out:$exe" -r:System.Drawing.dll -r:System.Windows.Forms.dll -r:System.Numerics.dll -r:Microsoft.CSharp.dll (Join-Path $PSScriptRoot 'GalleryTool.cs') @sources
if ($LASTEXITCODE -ne 0) { throw 'GalleryTool failed to compile' }

# ---------- validate ----------
$body = Join-Path $work "issue-$Issue.txt"
$result = Join-Path $work "result-$Issue.json"
$preview = Join-Path $work "preview-$Issue.png"
gh issue view $Issue --repo $repo --json body -q .body | Set-Content -Path $body -Encoding utf8
& $exe validate $body $result $preview
if ($LASTEXITCODE -ne 0) { throw 'validation crashed' }
$r = Get-Content $result -Raw | ConvertFrom-Json

function Comment([string]$text) {
    if ($DryRun) { Write-Host "---- comment ----`n$text"; return }
    $f = Join-Path $work "comment-$Issue.md"
    Set-Content -Path $f -Value $text -Encoding utf8
    gh issue comment $Issue --repo $repo --body-file $f | Out-Null
}
function Labels([string[]]$add = @(), [string[]]$remove = @()) {
    if ($DryRun) { Write-Host "labels +$($add -join ',') -$($remove -join ',')"; return }
    foreach ($l in $add) { gh issue edit $Issue --repo $repo --add-label $l | Out-Null }
    foreach ($l in $remove) { gh issue edit $Issue --repo $repo --remove-label $l 2>$null | Out-Null }
}

# ---------- preview image: kept on the gallery-previews branch so it can be shown in the issue ----------
function PublishPreview {
    if (-not (Test-Path $preview) -or $DryRun) { return $null }
    $wt = Join-Path $work 'previews-wt'
    if (Test-Path $wt) { git -C $root worktree remove --force $wt 2>$null; Remove-Item -Recurse -Force $wt -ErrorAction SilentlyContinue }
    $hasBranch = git -C $root ls-remote --heads origin gallery-previews
    if ($hasBranch) {
        git -C $root fetch -q origin gallery-previews
        git -C $root worktree add -q -B gallery-previews $wt origin/gallery-previews
    } else {
        git -C $root worktree add -q --detach $wt
        git -C $wt checkout -q --orphan gallery-previews
        git -C $wt rm -rq --cached . 2>$null
        Get-ChildItem $wt -Force | Where-Object Name -ne '.git' | Remove-Item -Recurse -Force
    }
    New-Item -ItemType Directory -Force (Join-Path $wt 'previews') | Out-Null
    Copy-Item $preview (Join-Path $wt "previews\$Issue.png") -Force
    git -C $wt add "previews/$Issue.png"
    git -C $wt @gitUser commit -q -m "Preview for #$Issue" 2>$null
    git -C $wt push -q origin gallery-previews
    $sha = git -C $wt rev-parse HEAD
    git -C $root worktree remove --force $wt
    return "https://raw.githubusercontent.com/$repo/$sha/previews/$Issue.png"
}

if ($Mode -eq 'review') {
    if ($r.ok) {
        $img = PublishPreview
        $lines = @("### ✅ Ready for review", "", "**$($r.name)**  ·  $($r.category)  ·  $($r.layers) layer(s): $($r.types)$(if ($r.animated) { '  ·  animated' })$(if ($r.recoil) { '  ·  recoil tracking' })", "")
        if ($img) { $lines += "![preview]($img)"; $lines += "" }
        foreach ($w in $r.warnings) { $lines += "⚠️ $w" }
        $lines += ""
        $lines += "Thanks for sharing! A maintainer will take a look. Once the **approved** label is added, it's published to the in-app gallery automatically."
        Comment ($lines -join "`n")
        Labels -add @('gallery') -remove @('needs-fix')
    } else {
        $lines = @("### ❌ This submission needs a fix", "")
        foreach ($e in $r.errors) { $lines += "- $e" }
        $lines += ""
        $lines += "Edit the issue to fix it and it will be checked again automatically. Tip: in Reticly, use **Discover › Community gallery › Share yours** to fill this in for you."
        Comment ($lines -join "`n")
        Labels -add @('gallery', 'needs-fix')
    }
    exit 0
}

# ---------- publish ----------
if (-not $DryRun) {
    $perm = gh api "repos/$repo/collaborators/$Sender/permission" -q .permission 2>$null
    if ($perm -notin @('admin', 'maintain', 'write')) { Labels -remove @('approved'); Write-Host "ignored: $Sender can't approve"; exit 0 }
}
if (-not $r.ok) {
    Comment "Couldn't publish: the submission still has problems.`n`n$(($r.errors | ForEach-Object { "- $_" }) -join "`n")"
    Labels -add @('needs-fix') -remove @('approved')
    exit 0
}
$author = gh issue view $Issue --repo $repo --json author -q .author.login
git -C $root pull -q --rebase origin main
$gallery = Join-Path $root 'gallery\gallery.json'
$previews = Join-Path $root 'gallery\previews'
$id = & $exe add $result $Issue $author $gallery $previews
if ($LASTEXITCODE -eq 4) {
    Comment 'This design is already in the gallery, so nothing was added. Thanks anyway!'
    if (-not $DryRun) { gh issue close $Issue --repo $repo --reason 'not planned' | Out-Null }
    exit 0
}
if ($LASTEXITCODE -ne 0) { throw 'adding to the gallery failed' }
$id = ($id | Select-Object -Last 1).Trim()
Copy-Item $preview (Join-Path $previews "$id.png") -Force
if ($DryRun) { Write-Host "would publish $id"; git -C $root diff --stat; exit 0 }
git -C $root add gallery/gallery.json "gallery/previews/$id.png"
git -C $root @gitUser commit -q -m "Gallery: add $($r.name) (#$Issue)"
git -C $root push -q origin HEAD:main
Comment "### 🎉 Published`n`n**$($r.name)** is now in **Discover › Community gallery** for everyone (press refresh there to see it right away). Thanks for sharing!"
Labels -remove @('needs-fix')
gh issue close $Issue --repo $repo --reason completed | Out-Null
