param(
    [string] $ServiceName = 'AFK4.Agent.Service',

    [int] $DelaySeconds = 5
)

$ErrorActionPreference = 'Stop'

# Агент ждёт этот скрипт и только потом отвечает платформе, а перезапускать надо его самого — поэтому
# перезапуск уходит в отдельный процесс, который переживёт и этот скрипт, и остановку агента.
# Раньше здесь был Start-Job: задание PowerShell — дочерний процесс сессии и умирает вместе с ней,
# то есть сразу после «exit 0». Агент не перезапускался, через минуту снова докладывал старую версию
# и ставил тот же пакет второй раз (на VM-приёмке 27.10.2026 — целый MSI после удачной дельты).
$command = "Start-Sleep -Seconds $DelaySeconds; Restart-Service -Name '$ServiceName' -Force"
Start-Process `
    -FilePath (Join-Path $PSHOME 'powershell.exe') `
    -ArgumentList @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-Command', $command) `
    -WindowStyle Hidden | Out-Null
exit 0
