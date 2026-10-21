# Ejecutar con PowerShell desde cualquier ubicación. No requiere administrador.
[CmdletBinding()]
param([switch]$NoPause)

$ErrorActionPreference = 'Stop'

try {
    $rallyExecutable = Join-Path -Path $PSScriptRoot -ChildPath 'Build\Windows\Rally.exe'
    if (-not (Test-Path -LiteralPath $rallyExecutable -PathType Leaf)) {
        throw "No se encontró el juego en: $rallyExecutable`nAbrí Unity y ejecutá Build > Windows Release primero."
    }

    $rallyExecutable = (Resolve-Path -LiteralPath $rallyExecutable).ProviderPath
    $rallyDesktop = [Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory)
    if ([string]::IsNullOrWhiteSpace($rallyDesktop)) {
        throw 'Windows no devolvió la carpeta de Escritorio del usuario.'
    }
    if (-not (Test-Path -LiteralPath $rallyDesktop -PathType Container)) {
        New-Item -Path $rallyDesktop -ItemType Directory -Force | Out-Null
    }

    $rallyShortcutPath = Join-Path -Path $rallyDesktop -ChildPath 'Rally.lnk'
    $rallyShell = New-Object -ComObject WScript.Shell
    try {
        # Save reemplaza el acceso directo si ya existe, sin borrar otros archivos.
        $rallyShortcut = $rallyShell.CreateShortcut($rallyShortcutPath)
        try {
            $rallyShortcut.TargetPath = $rallyExecutable
            $rallyShortcut.Arguments = ''
            $rallyShortcut.WorkingDirectory = [IO.Path]::GetDirectoryName($rallyExecutable)
            $rallyShortcut.IconLocation = "$rallyExecutable,0"
            $rallyShortcut.Description = 'Rally - juego de carreras'
            $rallyShortcut.WindowStyle = 1
            $rallyShortcut.Save()
        }
        finally {
            if ($null -ne $rallyShortcut) { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($rallyShortcut) }
        }
    }
    finally {
        if ($null -ne $rallyShell) { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($rallyShell) }
    }

    Write-Host "Acceso directo creado/actualizado: $rallyShortcutPath" -ForegroundColor Green
    Write-Host "Juego: $rallyExecutable"
}
catch {
    Write-Host "No se pudo crear el acceso directo: $($_.Exception.Message)" -ForegroundColor Red
    if (-not $NoPause -and [Environment]::UserInteractive) { [void](Read-Host 'Presioná Enter para cerrar') }
    exit 1
}

if (-not $NoPause -and [Environment]::UserInteractive) { [void](Read-Host 'Presioná Enter para cerrar') }
