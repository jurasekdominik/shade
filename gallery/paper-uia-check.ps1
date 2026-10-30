# Real Windows UI Automation provider check for the paper form. Start dotnet run --project gallery -c Release -- --paper-gates first.
param()
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$scope=[Windows.Automation.TreeScope]::Descendants
$element=[Windows.Automation.AutomationElement]
$window=$element::RootElement.FindFirst([Windows.Automation.TreeScope]::Children,[Windows.Automation.PropertyCondition]::new($element::NameProperty,'Shade paper acceptance'))
if(!$window){throw 'Run dotnet run --project gallery -c Release -- --paper-gates first.'}
function Field($id){$result=$window.FindFirst($scope,[Windows.Automation.PropertyCondition]::new($element::AutomationIdProperty,$id));if(!$result){throw "Missing field $id"};$result}
function Button($name){$condition=[Windows.Automation.AndCondition]::new([Windows.Automation.PropertyCondition]::new($element::NameProperty,$name),[Windows.Automation.PropertyCondition]::new($element::ControlTypeProperty,[Windows.Automation.ControlType]::Button));$window.FindFirst($scope,$condition).GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke();Start-Sleep -Milliseconds 150}
function Check($ok,$label){if(!$ok){throw $label};Write-Output "ok $label"}
Button 'Fill'
Button 'First page'
$contact=Field 'gate-contact'
Check ($contact.Current.Name -eq 'Contact' -and $contact.Current.ControlType -eq [Windows.Automation.ControlType]::Edit) 'virtual role and name'
Check (!$contact.Current.BoundingRectangle.IsEmpty -and $contact.Current.BoundingRectangle.Width -gt 0) 'virtual field supplies native screen bounds'
$provider=$contact.GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern)
$provider.SetValue('UIA contact')
Check ($provider.Current.Value -eq 'UIA contact') 'virtual SetValue activates native input and commits value'
$contact.SetFocus()
Check ($contact.FindAll($scope,[Windows.Automation.PropertyCondition]::new($element::ControlTypeProperty,[Windows.Automation.ControlType]::Edit)).Count -gt 0) 'active field exposes native editing subtree'
$check=Field 'gate-checked'
$toggle=$check.GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern)
$before=$toggle.Current.ToggleState;$toggle.Toggle()
Check ($toggle.Current.ToggleState -ne $before) 'virtual Toggle commits once'
Button 'Set error'
Check ((Field 'gate-contact').Current.HelpText -match 'Correct the contact') 'validation error exposed through HelpText'
Button 'Clear error'
$next=Field 'gate-next'
Check $next.Current.IsOffscreen 'unrealized page reports offscreen'
$next.SetFocus()
Check (!(Field 'gate-next').Current.IsOffscreen) 'focus realizes next page'
Button 'Preview'
Check ((Field 'gate-next').GetCurrentPattern([Windows.Automation.ValuePattern]::Pattern).Current.IsReadOnly) 'Preview reports read-only'
Button 'Light / dark'
Check ((Field 'gate-next').Current.Name -eq 'Next page') 'virtual peer survives live theme switch'
Button 'Fill'
Button 'First page'
