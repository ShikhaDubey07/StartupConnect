$htmlReplacements = @{
    '\bbg-dark\b' = 'bg-white'
    '\bbg-black\b' = 'bg-white'
    '\btext-white\b' = 'text-dark'
    '\btext-light\b' = 'text-muted'
}

$count = 0
Get-ChildItem -Path "Views" -Filter "*.cshtml" -Recurse | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    $original = $content
    foreach ($k in $htmlReplacements.Keys) {
        $content = [System.Text.RegularExpressions.Regex]::Replace($content, $k, $htmlReplacements[$k])
    }
    if ($content -ne $original) {
        [IO.File]::WriteAllText($_.FullName, $content)
        $count++
    }
}
Write-Host "Updated $count cshtml files."
