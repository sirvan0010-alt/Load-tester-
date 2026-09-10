# AutoPush.ps1
# Univerzalni bezpečný Git/GitHub uploader.
# Spouštěj z kořenové složky projektu.
#
# Použití:
#   .\AutoPush.ps1
#   .\AutoPush.ps1 -Auto
#
# -Auto provede commit + push po jednorázovém potvrzení.
# Bez -Auto pracuje interaktivně.

[CmdletBinding()]
param(
    [switch]$Auto,
    [string]$CommitMessage = ""
)

$ErrorActionPreference = "Stop"

# UTF-8 pro správné zobrazení češtiny v PowerShell/Windows konzoli
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

function Fail($Message) {
    Write-Host "`nCHYBA: $Message" -ForegroundColor Red
    exit 1
}

function Run-Git {
    param([Parameter(Mandatory=$true)][string[]]$Args)
    & git @Args
    if ($LASTEXITCODE -ne 0) {
        throw "Git příkaz selhal: git $($Args -join ' ')"
    }
}

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "           UNIVERSAL AUTOPUSH" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Projekt: $((Get-Location).Path)"
Write-Host ""

# 1) Git musí být nainstalovaný
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Fail "Git není nainstalovaný nebo není v PATH."
}

Write-Host "Git: " -NoNewline
git --version

# 2) Zkontrolujeme, že jde opravdu o projektovou složku
$projectFiles = Get-ChildItem -Force -File -ErrorAction SilentlyContinue
$projectDirs  = Get-ChildItem -Force -Directory -ErrorAction SilentlyContinue

if (($projectFiles.Count -eq 0) -and ($projectDirs.Count -eq 0)) {
    Fail "Aktuální složka je prázdná."
}

# 3) Pokud .git neexistuje, vytvoříme Git repository
if (-not (Test-Path ".git")) {
    Write-Host "`nV této složce zatím není Git repository." -ForegroundColor Yellow
    Write-Host "Vytvářím .git ..."
    Run-Git @("init")
    Write-Host "Git repository vytvořeno." -ForegroundColor Green
}

# 4) GitHub remote
$remoteUrl = ""
try {
    $remoteUrl = (& git remote get-url origin 2>$null).Trim()
} catch {
    $remoteUrl = ""
}

if ([string]::IsNullOrWhiteSpace($remoteUrl)) {
    Write-Host "`nNení nastaven remote 'origin'." -ForegroundColor Yellow
    $remoteUrl = Read-Host "Zadej URL GitHub repozitáře (např. https://github.com/user/repo.git)"

    if ([string]::IsNullOrWhiteSpace($remoteUrl)) {
        Fail "Nebyla zadána URL GitHub repozitáře."
    }

    Run-Git @("remote", "add", "origin", $remoteUrl)
}
else {
    Write-Host "Remote: $remoteUrl" -ForegroundColor Green
}

# 5) Branch
$branch = (& git branch --show-current).Trim()

if ([string]::IsNullOrWhiteSpace($branch)) {
    $branch = "main"
    Write-Host "`nNenalezena aktivní branch. Nastavuji '$branch'."
    Run-Git @("checkout", "-B", $branch)
}

Write-Host "Branch: $branch" -ForegroundColor Green

# 6) Git konfigurace pro dlouhé cesty
try { git config core.longpaths true 2>$null } catch {}
try { git config http.postBuffer 524288000 2>$null } catch {}

# 7) Zabránění náhodnému přidání vnořených Git repozitářů
#    Vnořená repozitářová složka se nemaže a nebude součástí tohoto repozitáře.
#    Pro tento projekt je známá složka mail-guardian.
$gitignore = ".gitignore"

$requiredIgnores = @(
    "# AutoPush: nested Git repositories",
    "**/.git/**",
    "mail-guardian/",
    "# AutoPush temporary files",
    "autopush-log.txt"
)

if (-not (Test-Path $gitignore)) {
    Set-Content -Path $gitignore -Value $requiredIgnores -Encoding UTF8
}
else {
    $existing = @(Get-Content $gitignore -ErrorAction SilentlyContinue)
    foreach ($line in $requiredIgnores) {
        if ($existing -notcontains $line) {
            Add-Content -Path $gitignore -Value $line -Encoding UTF8
        }
    }
}

# Zkontrolujeme, že mail-guardian je opravdu ignorovaný.
if (Test-Path ".\mail-guardian") {
    Write-Host "Vynechávám vnořený projekt: mail-guardian/" -ForegroundColor Yellow
}

# 8) Kontrola souborů > 100 MB
Write-Host "`nKontrola velkých souborů..."
$largeFiles = Get-ChildItem -Recurse -File -Force -ErrorAction SilentlyContinue |
    Where-Object {
        $_.FullName -notmatch '[\\/]\.git[\\/]' -and
        $_.Length -gt 100MB
    }

if ($largeFiles.Count -gt 0) {
    Write-Host "Nalezeny soubory větší než 100 MB:" -ForegroundColor Red
    foreach ($file in $largeFiles) {
        Write-Host ("  {0}  ({1:N1} MB)" -f $file.FullName, ($file.Length / 1MB))
    }
    Fail "GitHub běžně odmítá soubory nad 100 MB. Nejprve je odstraň nebo použij Git LFS."
}
else {
    Write-Host "OK - žádný soubor > 100 MB." -ForegroundColor Green
}

# 9) Status
Write-Host "`n------------------------------------------"
Write-Host "ZMĚNY"
Write-Host "------------------------------------------"

$status = @(git status --short --untracked-files=all | Where-Object { $_ -notmatch "^\?\? mail-guardian/" })

if ($status.Count -eq 0) {
    Write-Host "Žádné změny k odeslání." -ForegroundColor Green
    Write-Host "`nHotovo."
    exit 0
}

$status | ForEach-Object { Write-Host $_ }

# 10) Statistiky
Write-Host "`nPočet změněných položek: $($status.Count)"

# 11) Interaktivní volba
if (-not $Auto) {
    Write-Host "`n------------------------------------------"
    Write-Host "[C] Commit + Push"
    Write-Host "[P] Pouze Push"
    Write-Host "[S] Status"
    Write-Host "[Q] Konec"
    Write-Host "------------------------------------------"

    $choice = (Read-Host "Volba").ToUpperInvariant()

    switch ($choice) {
        "Q" { exit 0 }
        "S" {
            git status
            exit 0
        }
        "P" {
            Run-Git @("push", "-u", "origin", $branch)
            Write-Host "`nPush dokončen." -ForegroundColor Green
            exit 0
        }
        "C" { }
        default { Fail "Neplatná volba." }
    }
}

# 12) Commit message
if ([string]::IsNullOrWhiteSpace($CommitMessage)) {
    $CommitMessage = Read-Host "Commit message"
}

if ([string]::IsNullOrWhiteSpace($CommitMessage)) {
    $CommitMessage = "Update project"
}

Write-Host "`nPřipravuji commit:" -ForegroundColor Cyan
Write-Host "  $CommitMessage"

# 13) Add
Run-Git @("add", "--all")

# Bezpečnostní kontrola: vnořený mail-guardian nesmí být staged.
$nestedStaged = @(git diff --cached --name-only | Where-Object { $_ -like "mail-guardian/*" })
if ($nestedStaged.Count -gt 0) {
    Fail "Bezpečnostní kontrola selhala: mail-guardian/ se dostal do staged změn."
}

Run-Git @("status", "--short")

# 14) Znovu zobrazíme staged změny
Write-Host "`nSTAGED ZMĚNY:"
git status --short

$confirm = "A"
if (-not $Auto) {
    $confirm = (Read-Host "`nOpravdu vytvořit commit a pushnout na GitHub? [A/N]").ToUpperInvariant()
}

if ($confirm -ne "A") {
    Write-Host "Operace zrušena." -ForegroundColor Yellow
    exit 0
}

# 15) Commit
Run-Git @("commit", "-m", $CommitMessage)

# 16) Push s několika pokusy
$success = $false

for ($attempt = 1; $attempt -le 3; $attempt++) {
    try {
        Write-Host "`nPush - pokus $attempt/3 ..." -ForegroundColor Cyan
        Run-Git @("push", "-u", "origin", $branch)
        $success = $true
        break
    }
    catch {
        Write-Host $_.Exception.Message -ForegroundColor Yellow

        if ($attempt -lt 3) {
            Write-Host "Čekám 3 sekundy a zkouším znovu..."
            Start-Sleep -Seconds 3
        }
    }
}

if (-not $success) {
    Fail "Push se nepodařil ani po 3 pokusech."
}

Write-Host "`n==========================================" -ForegroundColor Green
Write-Host "             HOTOVO" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
Write-Host "Branch : $branch"
Write-Host "Remote : $remoteUrl"
Write-Host "Commit : $CommitMessage"
Write-Host ""
Write-Host "Projekt byl odeslán na GitHub." -ForegroundColor Green
