param([switch]$Test, [string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $taskCompiler)) { throw '需要 Windows .NET Framework 4.x 编译器。' }
$taskBuild = Join-Path $taskRoot 'build'
$taskDist = if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { Join-Path $taskRoot 'dist' } else { [IO.Path]::GetFullPath($OutputDirectory) }
$taskWpf = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\WPF'
New-Item -ItemType Directory -Path $taskBuild,$taskDist -Force | Out-Null
$taskSources = @(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
& $taskCompiler /nologo /target:winexe /platform:x64 /optimize+ /utf8output "/win32manifest:$taskRoot\app.manifest" "/win32icon:$taskRoot\assets\watchdog.ico" /out:"$taskDist\CodexWatchdog.exe" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll "/r:$taskWpf\PresentationCore.dll" "/r:$taskWpf\WindowsBase.dll" $taskSources
if ($LASTEXITCODE -ne 0) { throw '主程序编译失败。' }
Copy-Item -LiteralPath (Join-Path $taskRoot 'assets\watchdog.ico') -Destination $taskDist -Force
Copy-Item -LiteralPath (Join-Path $taskRoot 'assets\sounds') -Destination $taskDist -Recurse -Force
if ($Test) {
    & $taskCompiler /nologo /target:exe /utf8output /out:"$taskBuild\engine-tests.exe" /r:System.dll /r:System.Core.dll "$taskRoot\src\Models.cs" "$taskRoot\src\WatchEngine.cs" "$taskRoot\tests\EngineTests.cs"
    if ($LASTEXITCODE -ne 0) { throw '状态测试编译失败。' }
    & "$taskBuild\engine-tests.exe"
    if ($LASTEXITCODE -ne 0) { throw '状态测试失败。' }
    & $taskCompiler /nologo /target:exe /utf8output /out:"$taskBuild\lifecycle-engine-tests.exe" /r:System.dll /r:System.Core.dll "$taskRoot\src\Models.cs" "$taskRoot\src\WatchEngine.cs" "$taskRoot\tests\LifecycleEngineTests.cs"
    if ($LASTEXITCODE -ne 0) { throw '重开基线测试编译失败。' }
    & "$taskBuild\lifecycle-engine-tests.exe"
    if ($LASTEXITCODE -ne 0) { throw '重开基线测试失败。' }
    & $taskCompiler /nologo /target:exe /utf8output /out:"$taskBuild\startup-tests.exe" /r:System.dll "$taskRoot\src\StartupRegistration.cs" "$taskRoot\tests\StartupTests.cs"
    if ($LASTEXITCODE -ne 0) { throw '启动设置测试编译失败。' }
    & "$taskBuild\startup-tests.exe"
    if ($LASTEXITCODE -ne 0) { throw '启动设置测试失败。' }
    & $taskCompiler /nologo /target:exe /utf8output /out:"$taskBuild\presence-tests.exe" /r:System.dll "$taskRoot\src\DesktopPresence.cs" "$taskRoot\tests\PresenceTests.cs"
    if ($LASTEXITCODE -ne 0) { throw '桌面进程识别测试编译失败。' }
    & "$taskBuild\presence-tests.exe"
    if ($LASTEXITCODE -ne 0) { throw '桌面进程识别测试失败。' }
    & $taskCompiler /nologo /target:exe /utf8output /out:"$taskBuild\source-tests.exe" /r:System.dll /r:System.Core.dll /r:System.Web.Extensions.dll "$taskRoot\src\Models.cs" "$taskRoot\src\CodexSessionSource.cs" "$taskRoot\tests\SessionSourceTests.cs"
    if ($LASTEXITCODE -ne 0) { throw '读取测试编译失败。' }
    & "$taskBuild\source-tests.exe"
    if ($LASTEXITCODE -ne 0) { throw '读取测试失败。' }
    & $taskCompiler /nologo /target:exe /utf8output /out:"$taskBuild\transition-source-tests.exe" /r:System.dll /r:System.Core.dll /r:System.Web.Extensions.dll "$taskRoot\src\Models.cs" "$taskRoot\src\CodexSessionSource.cs" "$taskRoot\tests\TransitionSourceTests.cs"
    if ($LASTEXITCODE -ne 0) { throw '状态变化读取测试编译失败。' }
    & "$taskBuild\transition-source-tests.exe"
    if ($LASTEXITCODE -ne 0) { throw '状态变化读取测试失败。' }
    & $taskCompiler /nologo /target:exe /utf8output /out:"$taskBuild\bootstrap-engine-tests.exe" /r:System.dll /r:System.Core.dll /r:System.Web.Extensions.dll "$taskRoot\src\Models.cs" "$taskRoot\src\CodexSessionSource.cs" "$taskRoot\src\WatchEngine.cs" "$taskRoot\tests\BootstrapEngineTests.cs"
    if ($LASTEXITCODE -ne 0) { throw '历史同步与真实停止测试编译失败。' }
    & "$taskBuild\bootstrap-engine-tests.exe"
    if ($LASTEXITCODE -ne 0) { throw '历史同步与真实停止测试失败。' }
    & $taskCompiler /nologo /target:exe /utf8output /out:"$taskBuild\heartbeat-alarm-tests.exe" /r:System.dll /r:System.Core.dll /r:System.Web.Extensions.dll "$taskRoot\src\Models.cs" "$taskRoot\src\CodexSessionSource.cs" "$taskRoot\src\WatchEngine.cs" "$taskRoot\tests\HeartbeatAlarmTests.cs"
    if ($LASTEXITCODE -ne 0) { throw '巡查来源与新问题测试编译失败。' }
    & "$taskBuild\heartbeat-alarm-tests.exe"
    if ($LASTEXITCODE -ne 0) { throw '巡查来源与新问题测试失败。' }
    & $taskCompiler /nologo /target:exe /utf8output /out:"$taskBuild\all-source-tests.exe" /r:System.dll /r:System.Core.dll /r:System.Web.Extensions.dll "$taskRoot\src\Models.cs" "$taskRoot\src\CodexSessionSource.cs" "$taskRoot\tests\AllSourcesTests.cs"
    if ($LASTEXITCODE -ne 0) { throw '全部聊天读取测试编译失败。' }
    & "$taskBuild\all-source-tests.exe"
    if ($LASTEXITCODE -ne 0) { throw '全部聊天读取测试失败。' }
    & $taskCompiler /nologo /target:exe /utf8output /out:"$taskBuild\old-session-tests.exe" /r:System.dll /r:System.Core.dll /r:System.Web.Extensions.dll "$taskRoot\src\Models.cs" "$taskRoot\src\CodexSessionSource.cs" "$taskRoot\tests\OldSessionTests.cs"
    if ($LASTEXITCODE -ne 0) { throw '旧聊天发现测试编译失败。' }
    & "$taskBuild\old-session-tests.exe"
    if ($LASTEXITCODE -ne 0) { throw '旧聊天发现测试失败。' }
    & $taskCompiler /nologo /target:exe /utf8output /out:"$taskBuild\desktop-tests.exe" /r:System.dll "/r:$taskWpf\PresentationCore.dll" "/r:$taskWpf\WindowsBase.dll" "$taskRoot\src\AlarmSound.cs" "$taskRoot\tests\DesktopTests.cs"
    if ($LASTEXITCODE -ne 0) { throw '铃声测试编译失败。' }
    & "$taskBuild\desktop-tests.exe"
    if ($LASTEXITCODE -ne 0) { throw '铃声测试失败。' }
    & $taskCompiler /nologo /target:exe /utf8output /out:"$taskBuild\settings-tests.exe" /r:System.dll /r:System.Web.Extensions.dll "$taskRoot\src\Models.cs" "$taskRoot\tests\SettingsTests.cs"
    if ($LASTEXITCODE -ne 0) { throw '设置测试编译失败。' }
    & "$taskBuild\settings-tests.exe"
    if ($LASTEXITCODE -ne 0) { throw '设置测试失败。' }
    & $taskCompiler /nologo /target:exe /utf8output /out:"$taskBuild\routing-tests.exe" /r:System.dll /r:System.Core.dll /r:System.Web.Extensions.dll "$taskRoot\src\Models.cs" "$taskRoot\src\AlertSoundRouting.cs" "$taskRoot\src\SettingsStore.cs" "$taskRoot\tests\AlertSoundRoutingTests.cs"
    if ($LASTEXITCODE -ne 0) { throw '分类铃声配置测试编译失败。' }
    & "$taskBuild\routing-tests.exe"
    if ($LASTEXITCODE -ne 0) { throw '分类铃声配置测试失败。' }
    & $taskCompiler /nologo /target:exe /utf8output /out:"$taskBuild\scope-settings-tests.exe" /r:System.dll /r:System.Core.dll /r:System.Web.Extensions.dll "$taskRoot\src\Models.cs" "$taskRoot\src\AlertSoundRouting.cs" "$taskRoot\src\SettingsStore.cs" "$taskRoot\tests\ScopeSettingsTests.cs"
    if ($LASTEXITCODE -ne 0) { throw '多选配置测试编译失败。' }
    & "$taskBuild\scope-settings-tests.exe"
    if ($LASTEXITCODE -ne 0) { throw '多选配置测试失败。' }
    & $taskCompiler /nologo /target:exe /utf8output /out:"$taskBuild\media-tests.exe" /r:System.dll /r:System.Windows.Forms.dll "/r:$taskWpf\PresentationCore.dll" "/r:$taskWpf\WindowsBase.dll" "$taskRoot\src\AlarmSound.cs" "$taskRoot\tests\MediaTests.cs"
    if ($LASTEXITCODE -ne 0) { throw '真实音频测试编译失败。' }
    & "$taskBuild\media-tests.exe" "$taskDist\sounds\gentle.wav" 55
    if ($LASTEXITCODE -ne 0) { throw '真实音频循环测试失败。' }
    $taskUiTest = Join-Path $taskBuild 'ui-test'
    New-Item -ItemType Directory -Path $taskUiTest -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $taskDist 'sounds') -Destination $taskUiTest -Recurse -Force
    & $taskCompiler /nologo /target:exe /utf8output /main:AppLifecycleTests "/win32manifest:$taskRoot\app.manifest" "/win32icon:$taskRoot\assets\watchdog.ico" /out:"$taskUiTest\app-lifecycle-tests.exe" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll "/r:$taskWpf\PresentationCore.dll" "/r:$taskWpf\WindowsBase.dll" $taskSources "$taskRoot\tests\AppLifecycleTests.cs"
    if ($LASTEXITCODE -ne 0) { throw '生命周期界面测试编译失败。' }
    & "$taskUiTest\app-lifecycle-tests.exe"
    if ($LASTEXITCODE -ne 0) { throw '生命周期界面测试失败。' }
    & $taskCompiler /nologo /target:exe /utf8output /out:"$taskUiTest\catalog-tests.exe" /r:System.dll /r:System.Core.dll /r:System.Web.Extensions.dll "/r:$taskWpf\PresentationCore.dll" "/r:$taskWpf\WindowsBase.dll" "$taskRoot\src\SoundCatalog.cs" "$taskRoot\tests\SoundCatalogTests.cs"
    if ($LASTEXITCODE -ne 0) { throw '曲库测试编译失败。' }
    & "$taskUiTest\catalog-tests.exe"
    if ($LASTEXITCODE -ne 0) { throw '曲库测试失败。' }
    & $taskCompiler /nologo /target:exe /utf8output "/win32manifest:$taskRoot\app.manifest" /out:"$taskUiTest\per-type-ui-tests.exe" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll "$taskRoot\src\Models.cs" "$taskRoot\src\AlertSoundRouting.cs" "$taskRoot\src\SoundCatalog.cs" "$taskRoot\src\SettingsForm.cs" "$taskRoot\tests\PerTypeSoundUiTests.cs"
    if ($LASTEXITCODE -ne 0) { throw '分类铃声界面测试编译失败。' }
    & "$taskUiTest\per-type-ui-tests.exe"
    if ($LASTEXITCODE -ne 0) { throw '分类铃声界面测试失败。' }
    & $taskCompiler /nologo /target:exe /utf8output /main:DropdownRefreshTests "/win32manifest:$taskRoot\app.manifest" "/win32icon:$taskRoot\assets\watchdog.ico" /out:"$taskUiTest\dropdown-refresh-tests.exe" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll "/r:$taskWpf\PresentationCore.dll" "/r:$taskWpf\WindowsBase.dll" $taskSources "$taskRoot\tests\DropdownRefreshTests.cs"
    if ($LASTEXITCODE -ne 0) { throw '下拉菜单刷新测试编译失败。' }
    & "$taskUiTest\dropdown-refresh-tests.exe"
    if ($LASTEXITCODE -ne 0) { throw '下拉菜单刷新测试失败。' }
    & $taskCompiler /nologo /target:exe /utf8output /main:MultiSelectUiTests "/win32manifest:$taskRoot\app.manifest" /out:"$taskUiTest\multi-select-ui-tests.exe" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll "/r:$taskWpf\PresentationCore.dll" "/r:$taskWpf\WindowsBase.dll" $taskSources "$taskRoot\tests\MultiSelectUiTests.cs"
    if ($LASTEXITCODE -ne 0) { throw '多选界面测试编译失败。' }
    & "$taskUiTest\multi-select-ui-tests.exe"
    if ($LASTEXITCODE -ne 0) { throw '多选界面测试失败。' }
    & $taskCompiler /nologo /target:exe /utf8output /main:ScopeAlarmUiTests "/win32manifest:$taskRoot\app.manifest" /out:"$taskUiTest\scope-alarm-ui-tests.exe" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll "/r:$taskWpf\PresentationCore.dll" "/r:$taskWpf\WindowsBase.dll" $taskSources "$taskRoot\tests\ScopeAlarmUiTests.cs"
    if ($LASTEXITCODE -ne 0) { throw '监控范围与提醒接线测试编译失败。' }
    & "$taskUiTest\scope-alarm-ui-tests.exe"
    if ($LASTEXITCODE -ne 0) { throw '监控范围与提醒接线测试失败。' }
    & $taskCompiler /nologo /target:exe /utf8output /main:PerTypePlaybackTests "/win32manifest:$taskRoot\app.manifest" "/win32icon:$taskRoot\assets\watchdog.ico" /out:"$taskUiTest\per-type-playback-tests.exe" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll "/r:$taskWpf\PresentationCore.dll" "/r:$taskWpf\WindowsBase.dll" $taskSources "$taskRoot\tests\PerTypePlaybackTests.cs"
    if ($LASTEXITCODE -ne 0) { throw '分类真实播放测试编译失败。' }
    & "$taskUiTest\per-type-playback-tests.exe"
    if ($LASTEXITCODE -ne 0) { throw '分类真实播放测试失败。' }
}
Get-Item -LiteralPath "$taskDist\CodexWatchdog.exe" | Select-Object FullName,Length,LastWriteTime
