param(
    [string]$Source = (Join-Path $PSScriptRoot "Guia_de_estudio_del_codigo.md"),
    [string]$Output = (Join-Path $PSScriptRoot "GLITCH_Guia_de_estudio_del_codigo.pdf"),
    [switch]$KeepHtml
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Encode-Html([string]$Value) {
    return [System.Net.WebUtility]::HtmlEncode($Value)
}

function Render-Inline([string]$Value) {
    $result = Encode-Html $Value
    $result = [regex]::Replace($result, '`([^`]+)`', '<code>$1</code>')
    $result = [regex]::Replace($result, '\*\*([^*]+)\*\*', '<strong>$1</strong>')
    $result = [regex]::Replace($result, '\*([^*]+)\*', '<em>$1</em>')
    return $result
}

function Get-TableCells([string]$Line) {
    $trimmed = $Line.Trim()
    if ($trimmed.StartsWith('|')) { $trimmed = $trimmed.Substring(1) }
    if ($trimmed.EndsWith('|')) { $trimmed = $trimmed.Substring(0, $trimmed.Length - 1) }
    return @($trimmed -split '\|' | ForEach-Object { $_.Trim() })
}

function Is-TableSeparator([string]$Line) {
    if (-not $Line.Trim().StartsWith('|')) { return $false }
    $cells = Get-TableCells $Line
    if ($cells.Count -eq 0) { return $false }
    foreach ($cell in $cells) {
        if ($cell -notmatch '^:?-{3,}:?$') { return $false }
    }
    return $true
}

function Is-BlockStart([string]$Line) {
    if ([string]::IsNullOrWhiteSpace($Line)) { return $true }
    return $Line -match '^#{1,6}\s+' -or
           $Line -match '^```' -or
           $Line -match '^>\s?' -or
           $Line -match '^\s*[-*+]\s+' -or
           $Line -match '^\s*\d+\.\s+' -or
           $Line.Trim().StartsWith('|')
}

function Get-ChapterClass([string]$Heading) {
    if ($Heading -notmatch '^(\d+)\.') { return '' }
    $chapterStarts = @(1)
    return $(if ($chapterStarts -contains [int]$Matches[1]) { ' chapter-start' } else { '' })
}

$sourcePath = [System.IO.Path]::GetFullPath($Source)
$outputPath = [System.IO.Path]::GetFullPath($Output)
if (-not (Test-Path -LiteralPath $sourcePath)) {
    throw "No se encontro el Markdown: $sourcePath"
}

$chromeCandidates = @(
    "C:\Program Files\Google\Chrome\Application\chrome.exe",
    "C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"
)
$chromePath = $chromeCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($chromePath)) {
    throw "No se encontro Google Chrome para generar el PDF."
}

$lines = @(Get-Content -LiteralPath $sourcePath -Encoding UTF8)
$documentTitle = "GLITCH"
foreach ($line in $lines) {
    if ($line -match '^#\s+(.+)$') {
        $documentTitle = $Matches[1]
        break
    }
}

$toc = New-Object System.Collections.Generic.List[object]
$sectionIndex = 0
foreach ($line in $lines) {
    if ($line -match '^##\s+(.+)$') {
        $sectionIndex++
        $toc.Add([PSCustomObject]@{ Id = "sec-$sectionIndex"; Title = $Matches[1] })
    }
}

$body = New-Object System.Text.StringBuilder
$headingIndex = 0
$i = 0
while ($i -lt $lines.Count) {
    $line = $lines[$i]

    if ([string]::IsNullOrWhiteSpace($line)) {
        $i++
        continue
    }

    if ($line -match '^#\s+') {
        $i++
        continue
    }

    if ($line -match '^(#{2,6})\s+(.+)$') {
        $level = $Matches[1].Length
        $title = $Matches[2]
        $id = ''
        $className = ''
        if ($level -eq 2) {
            $headingIndex++
            $id = " id=`"sec-$headingIndex`""
            $className = Get-ChapterClass $title
        }
        [void]$body.AppendLine("<h$level$id class=`"section-heading$className`">$(Render-Inline $title)</h$level>")
        $i++
        continue
    }

    if ($line -match '^```') {
        $language = $line.Substring(3).Trim()
        $i++
        $codeLines = New-Object System.Collections.Generic.List[string]
        while ($i -lt $lines.Count -and $lines[$i] -notmatch '^```') {
            $codeLines.Add($lines[$i])
            $i++
        }
        if ($i -lt $lines.Count) { $i++ }
        $languageClass = $(if ($language) { " class=`"language-$(Encode-Html $language)`"" } else { '' })
        [void]$body.AppendLine("<pre><code$languageClass>$(Encode-Html ($codeLines -join "`n"))</code></pre>")
        continue
    }

    if ($line -match '^>\s?(.*)$') {
        $quoteLines = New-Object System.Collections.Generic.List[string]
        while ($i -lt $lines.Count -and $lines[$i] -match '^>\s?(.*)$') {
            $quoteLines.Add($Matches[1])
            $i++
        }
        [void]$body.AppendLine('<aside class="oral-script">')
        $quoteParagraph = New-Object System.Collections.Generic.List[string]
        foreach ($quoteLine in $quoteLines) {
            if ([string]::IsNullOrWhiteSpace($quoteLine)) {
                if ($quoteParagraph.Count -gt 0) {
                    [void]$body.AppendLine("<p>$(Render-Inline ($quoteParagraph -join ' '))</p>")
                    $quoteParagraph.Clear()
                }
            } else {
                $quoteParagraph.Add($quoteLine)
            }
        }
        if ($quoteParagraph.Count -gt 0) {
            [void]$body.AppendLine("<p>$(Render-Inline ($quoteParagraph -join ' '))</p>")
        }
        [void]$body.AppendLine('</aside>')
        continue
    }

    if ($line -match '^\s*[-*+]\s+(.+)$') {
        [void]$body.AppendLine('<ul>')
        while ($i -lt $lines.Count -and $lines[$i] -match '^\s*[-*+]\s+(.+)$') {
            [void]$body.AppendLine("<li>$(Render-Inline $Matches[1])</li>")
            $i++
        }
        [void]$body.AppendLine('</ul>')
        continue
    }

    if ($line -match '^\s*\d+\.\s+(.+)$') {
        [void]$body.AppendLine('<ol>')
        while ($i -lt $lines.Count -and $lines[$i] -match '^\s*\d+\.\s+(.+)$') {
            [void]$body.AppendLine("<li>$(Render-Inline $Matches[1])</li>")
            $i++
        }
        [void]$body.AppendLine('</ol>')
        continue
    }

    if ($line.Trim().StartsWith('|') -and $i + 1 -lt $lines.Count -and (Is-TableSeparator $lines[$i + 1])) {
        $headers = Get-TableCells $line
        $alignments = Get-TableCells $lines[$i + 1]
        $i += 2
        [void]$body.AppendLine('<div class="table-wrap"><table><thead><tr>')
        for ($column = 0; $column -lt $headers.Count; $column++) {
            $align = 'left'
            if ($column -lt $alignments.Count) {
                if ($alignments[$column] -match '^:-+:$') { $align = 'center' }
                elseif ($alignments[$column] -match '^-+:$') { $align = 'right' }
            }
            [void]$body.AppendLine("<th class=`"align-$align`">$(Render-Inline $headers[$column])</th>")
        }
        [void]$body.AppendLine('</tr></thead><tbody>')
        while ($i -lt $lines.Count -and $lines[$i].Trim().StartsWith('|')) {
            $cells = Get-TableCells $lines[$i]
            [void]$body.AppendLine('<tr>')
            for ($column = 0; $column -lt $headers.Count; $column++) {
                $value = $(if ($column -lt $cells.Count) { $cells[$column] } else { '' })
                $align = 'left'
                if ($column -lt $alignments.Count) {
                    if ($alignments[$column] -match '^:-+:$') { $align = 'center' }
                    elseif ($alignments[$column] -match '^-+:$') { $align = 'right' }
                }
                [void]$body.AppendLine("<td class=`"align-$align`">$(Render-Inline $value)</td>")
            }
            [void]$body.AppendLine('</tr>')
            $i++
        }
        [void]$body.AppendLine('</tbody></table></div>')
        continue
    }

    $paragraph = New-Object System.Collections.Generic.List[string]
    while ($i -lt $lines.Count -and -not (Is-BlockStart $lines[$i])) {
        $paragraph.Add($lines[$i].Trim())
        $i++
    }
    if ($paragraph.Count -gt 0) {
        [void]$body.AppendLine("<p>$(Render-Inline ($paragraph -join ' '))</p>")
        continue
    }

    $i++
}

$tocHtml = New-Object System.Text.StringBuilder
foreach ($entry in $toc) {
    $number = ''
    $label = $entry.Title
    if ($entry.Title -match '^(\d+)\.\s*(.+)$') {
        $number = $Matches[1].PadLeft(2, '0')
        $label = $Matches[2]
    }
    [void]$tocHtml.AppendLine("<a class=`"toc-item`" href=`"#$($entry.Id)`"><span class=`"toc-number`">$number</span><span>$(Render-Inline $label)</span></a>")
}

$css = @'
@page {
  size: A4;
  margin: 18mm 16mm 20mm;
  @bottom-left { content: "GLITCH  |  GUIA DE CODIGO"; color: #64748b; font: 8pt "Segoe UI", sans-serif; }
  @bottom-right { content: counter(page); color: #64748b; font: 8pt "Segoe UI", sans-serif; }
}
* { box-sizing: border-box; }
html { color-scheme: light; }
body {
  margin: 0;
  color: #172033;
  background: #ffffff;
  font-family: "Segoe UI", Inter, Arial, sans-serif;
  font-size: 10.3pt;
  line-height: 1.58;
  -webkit-print-color-adjust: exact;
  print-color-adjust: exact;
}
.compact-header { margin: 0 0 10mm; padding: 0 0 6mm; border-bottom: 1.2mm solid #22c7df; }
.compact-header h1 { margin: 0 0 2mm; color: #0b1730; font-size: 28pt; line-height: 1; letter-spacing: -.035em; }
.compact-header p { margin: 0; color: #52627a; font-size: 12pt; }
.cover {
  width: 210mm;
  height: 297mm;
  padding: 27mm 23mm 23mm;
  color: #f8fbff;
  background:
    radial-gradient(circle at 79% 18%, rgba(34, 211, 238, .22), transparent 25%),
    radial-gradient(circle at 18% 82%, rgba(168, 85, 247, .22), transparent 29%),
    linear-gradient(145deg, #050b18 0%, #0a1730 54%, #07101f 100%);
  position: relative;
  overflow: hidden;
  break-after: page;
}
.cover::before, .cover::after {
  content: "";
  position: absolute;
  border: 1px solid rgba(103, 232, 249, .26);
  transform: rotate(45deg);
}
.cover::before { width: 130mm; height: 130mm; right: -72mm; top: 30mm; }
.cover::after { width: 86mm; height: 86mm; left: -48mm; bottom: 28mm; border-color: rgba(192, 132, 252, .24); }
.cover-kicker { margin-top: 2mm; color: #67e8f9; font-size: 10pt; font-weight: 700; letter-spacing: .22em; text-transform: uppercase; }
.cover-rule { width: 30mm; height: 1.2mm; margin: 9mm 0 13mm; background: linear-gradient(90deg, #22d3ee, #a855f7); }
.cover h1 { margin: 0; max-width: 150mm; color: #ffffff; font-size: 38pt; line-height: 1.02; letter-spacing: -.035em; }
.cover-subtitle { max-width: 132mm; margin: 8mm 0 0; color: #cbd8ec; font-size: 16pt; line-height: 1.35; }
.cover-grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 4mm; margin-top: 25mm; max-width: 145mm; }
.cover-chip { padding: 4mm 5mm; border-left: 1.2mm solid #22d3ee; background: rgba(255,255,255,.055); color: #dcecff; font-size: 9pt; text-transform: uppercase; letter-spacing: .08em; }
.cover-chip:nth-child(even) { border-left-color: #a855f7; }
.cover-footer { position: absolute; left: 23mm; right: 23mm; bottom: 22mm; display: flex; justify-content: space-between; color: #8292ad; font-size: 9pt; letter-spacing: .05em; }
.toc-page { break-after: page; }
.toc-title { margin: 0 0 4mm; color: #0f172a; font-size: 24pt; letter-spacing: -.025em; }
.toc-lead { max-width: 150mm; margin: 0 0 8mm; color: #52627a; }
.toc-grid { display: grid; grid-template-columns: 1fr 1fr; column-gap: 9mm; }
.toc-item { display: grid; grid-template-columns: 9mm 1fr; gap: 2mm; align-items: baseline; padding: 2.4mm 0; color: #26364f; text-decoration: none; border-bottom: .25mm solid #e6edf6; break-inside: avoid; }
.toc-number { color: #0891b2; font-family: "Cascadia Mono", Consolas, monospace; font-weight: 700; }
.section-heading { color: #0d1b2f; break-after: avoid; }
h2 { margin: 10mm 0 4mm; padding: 0 0 2.5mm 4mm; border-left: 1.4mm solid #06b6d4; border-bottom: .35mm solid #dbe7f3; font-size: 19pt; line-height: 1.17; letter-spacing: -.025em; }
h2.chapter-start { break-before: page; margin-top: 0; }
h3 { margin: 6mm 0 2mm; color: #263c5b; font-size: 13.2pt; line-height: 1.25; }
h4, h5, h6 { margin: 4mm 0 1.5mm; color: #334a68; font-size: 11.2pt; }
p { margin: 0 0 3.3mm; orphans: 3; widows: 3; }
strong { color: #0f172a; }
a { color: #087b94; }
code { padding: .25mm 1.1mm; color: #5b21b6; background: #f2edff; border: .25mm solid #e4dbff; border-radius: 1mm; font: 8.8pt "Cascadia Mono", Consolas, monospace; }
pre { margin: 4mm 0 5mm; padding: 4mm 5mm; color: #dbeafe; background: #071425; border-left: 1.2mm solid #22d3ee; border-radius: 1.4mm; white-space: pre-wrap; break-inside: avoid; }
pre code { padding: 0; color: inherit; background: transparent; border: 0; font-size: 8.5pt; }
ul, ol { margin: 1.5mm 0 4mm; padding-left: 7mm; }
li { margin: 1.1mm 0; padding-left: 1mm; }
li::marker { color: #0891b2; font-weight: 700; }
.table-wrap { margin: 4mm 0 5.5mm; break-inside: auto; }
table { width: 100%; border-collapse: collapse; font-size: 8.8pt; line-height: 1.42; }
thead { display: table-header-group; }
tr { break-inside: avoid; }
th { padding: 2.4mm 2.8mm; color: #f8fbff; background: #17304f; border: .25mm solid #294867; font-weight: 700; }
td { padding: 2.2mm 2.8mm; vertical-align: top; border: .25mm solid #d7e2ee; }
tbody tr:nth-child(even) td { background: #f5f8fc; }
.align-center { text-align: center; }
.align-right { text-align: right; }
.oral-script { margin: 5mm 0; padding: 5mm 6mm; color: #26344a; background: linear-gradient(135deg, #eefbff, #f6f1ff); border-left: 1.4mm solid #8b5cf6; border-radius: 1.5mm; break-inside: auto; }
.oral-script::before { content: "GUION SUGERIDO"; display: block; margin-bottom: 2.5mm; color: #6d28d9; font-size: 8pt; font-weight: 800; letter-spacing: .14em; }
.oral-script p:last-child { margin-bottom: 0; }
@media screen {
  body { max-width: 210mm; margin: 0 auto; box-shadow: 0 0 30px rgba(15,23,42,.18); }
  .toc-page, .document-body { padding: 18mm 16mm 20mm; }
}
'@

$html = @"
<!doctype html>
<html lang="es">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>$(Encode-Html $documentTitle)</title>
  <style>$css</style>
</head>
<body>
  <main class="document-body">
    <header class="compact-header">
      <h1>GLITCH</h1>
      <p>Gu&iacute;a breve para entender y explicar el funcionamiento del c&oacute;digo</p>
    </header>
    $body
  </main>
</body>
</html>
"@

$tempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$buildId = [Guid]::NewGuid().ToString('N')
$tempHtml = if ($KeepHtml) {
    [System.IO.Path]::ChangeExtension($outputPath, '.html')
} else {
    Join-Path $tempRoot "glitch-codigo-$buildId.html"
}
$profilePath = Join-Path $tempRoot "glitch-chrome-$buildId"

try {
    [System.IO.File]::WriteAllText($tempHtml, $html, (New-Object System.Text.UTF8Encoding($false)))
    [System.IO.Directory]::CreateDirectory($profilePath) | Out-Null
    [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($outputPath)) | Out-Null
    if (Test-Path -LiteralPath $outputPath) { Remove-Item -LiteralPath $outputPath -Force }

    $uri = (New-Object System.Uri($tempHtml)).AbsoluteUri
    $arguments = @(
        '--headless=new',
        '--disable-gpu',
        '--no-sandbox',
        '--no-pdf-header-footer',
        "--user-data-dir=$profilePath",
        "--print-to-pdf=$outputPath",
        $uri
    )
    $process = Start-Process -FilePath $chromePath -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden
    if ($process.ExitCode -ne 0) { throw "Chrome termino con codigo $($process.ExitCode)." }
    if (-not (Test-Path -LiteralPath $outputPath)) { throw "Chrome no genero el PDF esperado." }
    Write-Output $outputPath
}
finally {
    if (-not $KeepHtml -and (Test-Path -LiteralPath $tempHtml)) { Remove-Item -LiteralPath $tempHtml -Force }
    $safeProfile = [System.IO.Path]::GetFullPath($profilePath)
    if ($safeProfile.StartsWith($tempRoot, [System.StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $safeProfile)) {
        Remove-Item -LiteralPath $safeProfile -Recurse -Force -ErrorAction SilentlyContinue
    }
}
