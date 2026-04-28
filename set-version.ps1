param(
    [Parameter(Mandatory)][string]$v
)

$csproj = "Jellyfin.Plugin.BulkDownload\Jellyfin.Plugin.BulkDownload.csproj"
$yaml   = "build.yaml"

(Get-Content $csproj) `
    -replace '<Version>[^<]+</Version>',         "<Version>$v</Version>" `
    -replace '<FileVersion>[^<]+</FileVersion>',  "<FileVersion>$v</FileVersion>" |
    Set-Content $csproj -Encoding utf8

(Get-Content $yaml) `
    -replace '^version: ".+"', "version: `"$v`"" |
    Set-Content $yaml -Encoding utf8

Write-Host "Version set to $v"
