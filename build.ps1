$ErrorActionPreference = 'Stop'

# 本地运行需要设置，Actions 会自动提供
$env:GITHUB_WORKSPACE = (Get-Location).Path

# 公开仓库可以不设；如果遇到 rate limit 或私有仓库，设一个 PAT
# $env:GH_TOKEN = "ghp_xxx"




if (Test-Path dccm) { }
else
{
  
    $headers = @{ Accept = "application/vnd.github+json" }
  if ($env:GH_TOKEN) {
    $headers.Authorization = "Bearer $env:GH_TOKEN"
  }

  $release = Invoke-RestMethod -Uri "https://api.github.com/repos/dead-cells-core-modding/core/releases/latest" -Headers $headers
  $asset = $release.assets | Where-Object { $_.name -eq 'win-x64.zip' } | Select-Object -First 1
  if (-not $asset) { throw "未找到 win-x64.zip" }

  Invoke-WebRequest -Uri $asset.browser_download_url -Headers $headers -OutFile dccm.zip

  Expand-Archive -Path dccm.zip -DestinationPath dccm -Force
}


$mdkRoot = Join-Path $env:GITHUB_WORKSPACE 'dccm/core/mdk'
$env:DCCM_MDK_ROOT = $mdkRoot
Write-Host "DCCM_MDK_ROOT=$mdkRoot"

dotnet nuget remove source dccm-local 2>$null | Out-Null
dotnet nuget add source (Join-Path $mdkRoot 'packages') --name dccm-local

dotnet build -c Release