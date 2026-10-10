# Pre-boot check: catch XML/DLL mistakes before an 8-minute dedicated restart.
# Exit 1 if anything is wrong.

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Game = "C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die"
$fail = 0

function Fail($msg) {
    Write-Host "FAIL  $msg" -ForegroundColor Red
    $script:fail++
}
function Ok($msg) { Write-Host "ok    $msg" -ForegroundColor Green }
function Warn($msg) { Write-Host "warn  $msg" -ForegroundColor Yellow }

$matXml = Join-Path $Game "Data\Config\materials.xml"
if (-not (Test-Path $matXml)) { Fail "vanilla materials.xml missing: $matXml"; exit 1 }
$materials = [regex]::Matches((Get-Content $matXml -Raw), 'material id="(M[^"]+)"') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
$vanillaItems = [regex]::Matches((Get-Content (Join-Path $Game "Data\Config\items.xml") -Raw), '<item name="([^"]+)"') | ForEach-Object { $_.Groups[1].Value }
$vanillaBlocks = [regex]::Matches((Get-Content (Join-Path $Game "Data\Config\blocks.xml") -Raw), '<block name="([^"]+)"') | ForEach-Object { $_.Groups[1].Value }
$known = @{}
foreach ($n in ($vanillaItems + $vanillaBlocks)) { $known[$n] = $true }

$ourItemsXml = Get-Content (Join-Path $Root "Config\items.xml") -Raw
$ourBlocksXml = Get-Content (Join-Path $Root "Config\blocks.xml") -Raw
$ourItems = [regex]::Matches($ourItemsXml, '<item name="([^"]+)"') | ForEach-Object { $_.Groups[1].Value }
$ourBlocks = [regex]::Matches($ourBlocksXml, '<block name="([^"]+)"') | ForEach-Object { $_.Groups[1].Value }
foreach ($n in ($ourItems + $ourBlocks)) { $known[$n] = $true }

foreach ($m in ([regex]::Matches($ourItemsXml + $ourBlocksXml, 'name="Material"\s+value="([^"]+)"'))) {
    $id = $m.Groups[1].Value
    if ($materials -notcontains $id) { Fail "material '$id' does not exist in vanilla materials.xml" }
    else { Ok "material $id" }
}

$recipeXml = Get-Content (Join-Path $Root "Config\recipes.xml") -Raw
foreach ($n in ([regex]::Matches($recipeXml, '<recipe name="([^"]+)"') | ForEach-Object { $_.Groups[1].Value })) {
    if (-not $known.ContainsKey($n)) { Fail "recipe for unknown item/block '$n'" } else { Ok "recipe $n" }
}
foreach ($n in ([regex]::Matches($recipeXml, '<ingredient name="([^"]+)"') | ForEach-Object { $_.Groups[1].Value })) {
    if (-not $known.ContainsKey($n)) { Fail "recipe ingredient unknown '$n'" }
}

foreach ($n in ([regex]::Matches($ourItemsXml, 'name="Extends"\s+value="([^"]+)"') | ForEach-Object { $_.Groups[1].Value })) {
    if (-not $known.ContainsKey($n)) { Fail "Extends unknown item '$n'" } else { Ok "extends $n" }
}
foreach ($n in ([regex]::Matches($ourItemsXml, 'name="Magazine_items"\s+value="([^"]+)"') | ForEach-Object { $_.Groups[1].Value })) {
    foreach ($p in ($n -split ',')) {
        $p = $p.Trim()
        if ($p -and -not $known.ContainsKey($p)) { Fail "Magazine_items unknown '$p'" } else { Ok "magazine $p" }
    }
}

$dll = Join-Path $Root "HSPortal.dll"
if (-not (Test-Path $dll)) { Fail "HSPortal.dll missing" }
else {
    $bytes = [IO.File]::ReadAllBytes($dll)
    $ascii = [Text.Encoding]::ASCII.GetString($bytes)
    foreach ($t in @("XUiC_HSPortalHud", "ItemActionHSPortalGun", "ItemActionHSPortalGelGun", "HSPortalWornBoots", "HSPortalMod", "BlockHSPortalCube")) {
        if ($ascii.IndexOf($t) -lt 0) { Fail "DLL missing type string '$t'" } else { Ok "dll $t" }
    }
}

$windows = Get-Content (Join-Path $Root "Config\XUi_InGame\windows.xml") -Raw
foreach ($c in ([regex]::Matches($windows, 'controller="([^"]+)"') | ForEach-Object { $_.Groups[1].Value })) {
    if ($c -notmatch ',') { Fail ("XUi controller {0} has no assembly name - use Name, HSPortal" -f $c) }
    elseif ($c -notmatch 'HSPortalHud') { Warn ("unexpected controller {0}" -f $c) }
    else { Ok ("xui controller {0}" -f $c) }
}

$bundle = Join-Path $Root "Resources\HSPortal.unity3d"
if (-not (Test-Path $bundle)) { Fail "Resources/HSPortal.unity3d missing" }
else { Ok ("bundle {0} bytes" -f (Get-Item $bundle).Length) }

if ($fail -gt 0) {
    Write-Host ("{0} check(s) failed. Do not restart the dedicated server." -f $fail) -ForegroundColor Red
    exit 1
}
Write-Host "All checks passed." -ForegroundColor Green
exit 0
