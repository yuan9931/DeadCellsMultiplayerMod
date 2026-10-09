$ErrorActionPreference = 'Stop'

$scriptRoot = $PSScriptRoot
if (-not $scriptRoot) { $scriptRoot = (Get-Location).Path }
Push-Location $scriptRoot
try {
    $env:GITHUB_WORKSPACE = $scriptRoot

    # 公开仓库可以不设；如果遇到 rate limit 或私有仓库，设一个 PAT
    # $env:GH_TOKEN = "ghp_xxx"

    $dccmRoot = Join-Path $scriptRoot 'dccm'
    $dccmZip = Join-Path $scriptRoot 'dccm.zip'

    if (-not (Test-Path $dccmRoot)) {
        if (Test-Path $dccmZip) {
            Write-Host "Using local dccm.zip fallback: $dccmZip"
            Expand-Archive -Path $dccmZip -DestinationPath $dccmRoot -Force
        }
        else {
            $headers = @{ Accept = "application/vnd.github+json" }
            if ($env:GH_TOKEN) {
                $headers.Authorization = "Bearer $env:GH_TOKEN"
            }

            try {
                $release = Invoke-RestMethod -Uri "https://api.github.com/repos/dead-cells-core-modding/core/releases/latest" -Headers $headers
                $asset = $release.assets | Where-Object { $_.name -eq 'win-x64.zip' } | Select-Object -First 1
                if (-not $asset) { throw "未找到 win-x64.zip" }

                Invoke-WebRequest -Uri $asset.browser_download_url -Headers $headers -OutFile $dccmZip
                Expand-Archive -Path $dccmZip -DestinationPath $dccmRoot -Force
            }
            catch {
                throw "无法下载 DCCM MDK；请确保 $dccmZip 存在，或设置 GH_TOKEN 并重试。原始错误: $($_.Exception.Message)"
            }
        }
    }

    $mdkRoot = Join-Path $dccmRoot 'core/mdk'
    if (-not (Test-Path $mdkRoot)) {
        throw "未找到 DCCM MDK 根目录：$mdkRoot"
    }

    $env:DCCM_MDK_ROOT = $mdkRoot
    Write-Host "DCCM_MDK_ROOT=$mdkRoot"

    dotnet nuget remove source dccm-local 2>$null | Out-Null
    dotnet nuget add source (Join-Path $mdkRoot 'packages') --name dccm-local

    dotnet build -c Release
}
finally {
    Pop-Location
}