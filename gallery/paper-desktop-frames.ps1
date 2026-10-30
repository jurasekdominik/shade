param([string]$Output='out/paper-desktop-frames.csv')
# Start gallery with --paper-desktop-frames first. Targets only that window; leaves the desktop pointer alone.
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class PaperFrameInput {
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
    [DllImport("user32.dll")] public static extern bool ScreenToClient(IntPtr window, ref Point point);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] public static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
}
'@
$element=[Windows.Automation.AutomationElement]
$scope=[Windows.Automation.TreeScope]::Descendants
$window=$element::RootElement.FindFirst([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new($element::NameProperty,'Shade paper desktop frames'))
if(!$window){throw 'Run dotnet run --project gallery -c Release -- --paper-desktop-frames first.'}
$hwnd=[IntPtr]$window.Current.NativeWindowHandle
if([PaperFrameInput]::GetAsyncKeyState(0x11) -lt 0){throw 'Release Ctrl before the frame probe.'}
[void][PaperFrameInput]::ShowWindow($hwnd,5)
[void][PaperFrameInput]::SetForegroundWindow($hwnd)
Start-Sleep -Milliseconds 200
if([PaperFrameInput]::GetForegroundWindow() -ne $hwnd){throw 'The frame window must be foreground for native Ctrl+wheel.'}
$surface=$window.FindFirst($scope,[Windows.Automation.PropertyCondition]::new($element::NameProperty,'Paper design grid'))
if(!$surface){throw 'The design surface is missing from the automation tree.'}
$bounds=$surface.Current.BoundingRectangle
$point=[PaperFrameInput+Point]::new();$point.X=[int]($bounds.Left+80);$point.Y=[int]($bounds.Top+85)
$screenX=$point.X;$screenY=$point.Y
[void][PaperFrameInput]::ScreenToClient($hwnd,[ref]$point)
function Packed($x,$y){[IntPtr](($x -band 65535) -bor (($y -band 65535) -shl 16))}
function Mouse($message,$buttons,$x,$y){[void][PaperFrameInput]::PostMessage($hwnd,$message,[IntPtr]$buttons,(Packed $x $y))}
$record=$window.FindFirst($scope,[Windows.Automation.AndCondition]::new([Windows.Automation.PropertyCondition]::new($element::NameProperty,'Record frames'),[Windows.Automation.PropertyCondition]::new($element::ControlTypeProperty,[Windows.Automation.ControlType]::Button)))
$started=[DateTime]::UtcNow
$record.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke()
Start-Sleep -Milliseconds 300
Mouse 0x200 0 $point.X $point.Y
Mouse 0x201 1 $point.X $point.Y
for($i=1;$i -le 40;$i++){Mouse 0x200 1 ($point.X+$i) ($point.Y+$i);Start-Sleep -Milliseconds 16}
Mouse 0x202 0 ($point.X+40) ($point.Y+40)
Start-Sleep -Milliseconds 300
[PaperFrameInput]::keybd_event(0x11,0,0,[UIntPtr]::Zero)
try {
    for($i=0;$i -lt 20;$i++){$delta=if($i%2 -eq 0){120}else{-120};Mouse 0x20A (($delta -shl 16) -bor 8) $screenX $screenY;Start-Sleep -Milliseconds 16}
} finally { [PaperFrameInput]::keybd_event(0x11,0,2,[UIntPtr]::Zero) }
for($i=0;$i -lt 40;$i++){
    if((Test-Path -LiteralPath ($Output+'.txt')) -and (Get-Item -LiteralPath ($Output+'.txt')).LastWriteTimeUtc -gt $started){break}
    Start-Sleep -Milliseconds 500
}
if(!(Test-Path -LiteralPath $Output) -or (Get-Item -LiteralPath $Output).LastWriteTimeUtc -le $started){throw 'Frame recording did not finish.'}
$summary=Get-Content -LiteralPath ($Output+'.txt') -Raw
if($summary -notmatch 'Changes: [1-9]\d*;' -or $summary -notmatch 'zoom exercised: True'){throw "Input exercise failed: $summary"}
Write-Output $summary
$rows=Import-Csv -LiteralPath $Output
foreach($phase in @('drag','zoom')){
    $values=@($rows | Where-Object phase -eq $phase | ForEach-Object {[double]::Parse($_.frame_interval_ms,[Globalization.CultureInfo]::InvariantCulture)} | Sort-Object)
    if($values.Count -eq 0){throw "No $phase frame samples"}
    Write-Output ('{0}: {1} frames, median {2:F2} ms, p95 {3:F2} ms, max {4:F2} ms' -f $phase,$values.Count,$values[[int][Math]::Floor(($values.Count-1)*.5)],$values[[int][Math]::Floor(($values.Count-1)*.95)],$values[-1])
}
