param([string]$Dll, [string]$Arguments = '--paper-form', [string]$Title = 'Shade · Paper form builder')
# UI Automation pass for 0.12.0: the role, name and patterns of the controls this release added or changed.
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$process = Start-Process dotnet -ArgumentList "`"$Dll`" $Arguments" -PassThru
try {
    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $window = $null
    for ($i = 0; $i -lt 60 -and -not $window; $i++) {
        Start-Sleep -Milliseconds 500
        $window = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $process.Id)))
    }
    if (-not $window) { throw "no window" }
    Start-Sleep -Seconds 2
    $all = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    $failed = 0
    function Pattern($e, $p) { $o = $null; return $e.TryGetCurrentPattern($p, [ref]$o) }
    function Check($ok, $what) { if ($ok) { "ok   $what" } else { "FAIL $what"; $script:failed++ } }
    function Named($name) { $all | Where-Object { $_.Current.Name -eq $name } }
    $type = [System.Windows.Automation.ControlType]
    $value = [System.Windows.Automation.ValuePattern]::Pattern
    $toggle = [System.Windows.Automation.TogglePattern]::Pattern
    $invoke = [System.Windows.Automation.InvokePattern]::Pattern
    $expand = [System.Windows.Automation.ExpandCollapsePattern]::Pattern
    $selection = [System.Windows.Automation.SelectionPattern]::Pattern

    foreach ($name in 'Mode', 'Structure', 'Paper format', 'Orientation', 'Zoom') {
        $e = Named $name | Select-Object -First 1
        Check ($e -and $e.Current.ControlType -eq $type::ComboBox -and (Pattern $e $expand)) "toolbar '$name' is a ComboBox with expand/collapse"
    }
    $view = Named 'View' | Select-Object -First 1
    Check ($view -and $view.Current.ControlType -eq $type::Button -and (Pattern $view $invoke)) "View menu is an invokable button"
    foreach ($name in 'Undo', 'Redo') { $e = Named $name | Select-Object -First 1; Check ($e -and (Pattern $e $invoke)) "'$name' icon button has a name and invoke" }
    $layers = Named 'Page elements' | Select-Object -First 1
    Check ($layers -and $layers.Current.ControlType -eq $type::List -and (Pattern $layers $selection)) "Layers is a list with selection"
    $tabs = $all | Where-Object { $_.Current.Name -eq 'Guides' -and $_.Current.ControlType -eq $type::TabItem } | Select-Object -First 1
    Check ($tabs) "Layers/Guides tabs are tab items"
    if ($tabs) { $tabs.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select(); Start-Sleep -Milliseconds 500 }
    $guides = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, 'Page guides')))
    Check ($guides -and $guides.Current.ControlType -eq $type::List -and (Pattern $guides $selection)) "Guides tab: a list with selection"

    # switch to Fill through automation, then read the inputs
    $mode = Named 'Mode' | Select-Object -First 1
    $mode.GetCurrentPattern($expand).Expand(); Start-Sleep -Milliseconds 600
    $fill = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, 'Fill'))) | Where-Object { $_.Current.ProcessId -eq $process.Id } | Select-Object -First 1
    if ($fill) { $fill.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select() }
    Start-Sleep -Seconds 1
    $all = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    $asset = Named 'Asset identifier' | Where-Object { $_.Current.ControlType -eq $type::Edit } | Select-Object -First 1
    Check ($asset -and (Pattern $asset $value) -and $asset.GetCurrentPattern($value).Current.Value -eq '0026018342') "character grid: Edit named by its label, value '0026018342'"
    if ($asset) { $asset.GetCurrentPattern($value).SetValue('1234'); Start-Sleep -Milliseconds 300; Check ($asset.GetCurrentPattern($value).Current.Value -eq '1234') "character grid: automation can write its value" }
    $notes = Named 'Inspection notes' | Where-Object { $_.Current.ControlType -eq $type::Edit } | Select-Object -First 1
    Check ($notes -and (Pattern $notes $value)) "multiline field: Edit with value"
    $check = $all | Where-Object { $_.Current.ControlType -eq $type::CheckBox -and $_.Current.Name -eq 'Inspection completed' } | Select-Object -First 1
    Check ($check -and (Pattern $check $toggle)) "paper check box: CheckBox with toggle, named"
    $date = Named 'Inspection date' | Where-Object { $_.Current.ControlType -eq $type::Edit } | Select-Object -First 1
    Check ($date -and (Pattern $date $value)) "date field: Edit with value"
    $calendar = Named 'Calendar' | Select-Object -First 1
    Check ($calendar -and (Pattern $calendar $invoke)) "date field: calendar button is named and invokable"
    $logo = $all | Where-Object { $_.Current.Name -eq 'Logo' -and $_.Current.ControlType -eq $type::Image } | Select-Object -First 1
    Check ($logo) "image structure: an Image named by its label"
    "$failed failed"
} finally { Stop-Process -Id $process.Id -ErrorAction SilentlyContinue }
