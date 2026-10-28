// Action checks.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Styling;
using Avalonia.VisualTree;
using static Shade.Look;

namespace Shade.Tests
{
    static partial class Program
    {
        static void Activation()
        {
            var tick = new CheckBox(); var chip = new FilterChip("Filter"); var icon = new IconButton(IconButton.Export); var tabs = new TextTabs("One", "Two", "Three");
            var confirm = new ActionButton("Delete") { ConfirmText = "Confirm" }; int clicks = 0, runs = 0; chip.Click += (_, _) => clicks++; confirm.Click += (_, _) => runs++;
            var window = new Window { Width = 500, Height = 300, Content = new StackPanel { Children = { tick, chip, icon, tabs, confirm } } }; window.Show(); Dispatcher.UIThread.RunJobs();
            void Key(Avalonia.Input.Key key, bool release = true) { window.KeyPress(key, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); if (release) window.KeyRelease(key, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Dispatcher.UIThread.RunJobs(); }
            chip.Focus(Avalonia.Input.NavigationMethod.Tab); Key(Avalonia.Input.Key.Space, false); Key(Avalonia.Input.Key.Space, false);
            Ok(clicks == 0 && typeof(Button).IsAssignableFrom(icon.GetType()), "native toggle activation waits for Space release");
            tick.Focus(Avalonia.Input.NavigationMethod.Tab); Dispatcher.UIThread.RunJobs();
            Ok(tick.Classes.Contains(":focus-visible") && tick.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PART_Focus").IsVisible, "a check box reached by Tab shows its focus ring, painted by :focus-visible");
            chip.Focus(Avalonia.Input.NavigationMethod.Tab); Dispatcher.UIThread.RunJobs();
            Ok(!tick.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PART_Focus").IsVisible, "the check box's focus ring goes with the focus");
            window.KeyRelease(Avalonia.Input.Key.Space, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null);
            Ok(clicks == 1, "held Space activates a native filter once on release");
            chip.IsEnabled = false; Key(Avalonia.Input.Key.Enter); Ok(clicks == 1, "disabled actions cannot run through the keyboard");
            var at = tick.TranslatePoint(new Point(4, 4), window)!.Value;
            window.MouseDown(at, Avalonia.Input.MouseButton.Left); window.MouseMove(new Point(490, 290)); window.MouseUp(new Point(490, 290), Avalonia.Input.MouseButton.Left); Dispatcher.UIThread.RunJobs();
            Ok(tick.IsChecked != true, "releasing a captured pointer outside a tick cancels activation");
            var first = tabs.GetVisualDescendants().OfType<StackPanel>().First(c => c.Focusable); first.Focus(Avalonia.Input.NavigationMethod.Tab);
            Key(Avalonia.Input.Key.Right); Ok(tabs.Active == 1, "Right moves focus and selection to the next text tab");
            Key(Avalonia.Input.Key.End); Ok(tabs.Active == 2, "End selects the last text tab");
            bool refused = false; try { tabs.Active = 9; } catch (ArgumentOutOfRangeException) { refused = true; }
            Ok(refused && tabs.Active == 2, "invalid tab selection is rejected without changing the selected tab");
            confirm.Focus(Avalonia.Input.NavigationMethod.Tab); Key(Avalonia.Input.Key.Enter, false); Key(Avalonia.Input.Key.Enter, false);
            Ok(confirm.Armed && runs == 0, "holding Enter cannot arm and confirm a dangerous action");
            window.KeyRelease(Avalonia.Input.Key.Enter, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Key(Avalonia.Input.Key.Enter);
            Ok(runs == 1, "a distinct second key press confirms the action");
            window.Close();
        }

        static void NativeActions()
        {
            var vm = new Vm(); var action = new ActionButton { Content = "Save", Icon = Icons.Plus, ConfirmText = "Confirm save" };
            action.ContentTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<string>((text, _) => new TextBlock { Text = "Formatted " + text });
            action.Bind(ContentControl.ContentProperty, new Avalonia.Data.Binding(nameof(Vm.ActionText)) { Source = vm });
            action.Styles.Add(new Avalonia.Styling.Style(x => x.OfType<ActionButton>()) { Setters = { new Avalonia.Styling.Setter(Avalonia.Controls.Primitives.TemplatedControl.PaddingProperty, new Thickness(18, 0)) } });
            var icon = new IconButton { Icon = Icons.Download }; var capsule = new Capsule { Content = "Preset", Icon = Icons.Bookmark };
            var input = new TextBox(); var command = new Command(); var ordinary = new ActionButton { Content = "Default", IsDefault = true, Command = command };
            var hold = new ActionButton("Unlock") { HoldMs = 40 }; int held = 0; hold.Click += (_, _) => { held++; hold.HoldMs = 0; };
            var window = new Window { Width = 500, Height = 500, Content = new StackPanel { Children = { action, icon, capsule, input, ordinary, hold } } }; window.Show();
            void Key(Avalonia.Input.Key key) { window.KeyPress(key, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); window.KeyRelease(key, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Settle(); }
            Settle();
            Ok(action is Button && icon.Icon != null && capsule.Icon != null && action.Padding.Left == 18, "native action variants support icons after construction and normal style overrides");
            action.PerformClick(); Settle();
            Ok(action.Armed && vm.ActionText == "Save" && action.Content?.ToString() == "Save" && action.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Confirm save"), "confirmation is visual state without replacing bound native Content");
            vm.ActionText = "Apply"; action.Disarm(); Settle();
            Ok(action.Content?.ToString() == "Apply" && action.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Formatted Apply"), "content bindings and templates continue to update after confirmation");
            int invokes = 0; icon.Click += (_, _) => invokes++;
            ((Avalonia.Automation.Provider.IInvokeProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(icon)!).Invoke(); Settle();
            Ok(invokes == 1 && Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(capsule) is Avalonia.Automation.Peers.ButtonAutomationPeer, "icon and capsule actions use Avalonia's native button automation");
            icon.Focus(Avalonia.Input.NavigationMethod.Tab); Settle();
            Ok(icon.BorderThickness.Left == 1 && icon.BorderBrush == B(FocusBorder), "native icon actions show the shared keyboard focus border");
            capsule.Focus(Avalonia.Input.NavigationMethod.Tab); Settle();
            Ok(capsule.BorderThickness.Left == 1 && capsule.BorderBrush == B(FocusBorder), "native capsule actions show the shared keyboard focus border");
            input.Focus(); Key(Avalonia.Input.Key.Enter); Ok(command.Runs == 1, "native default-button activation executes the command from another editor");
            var at = hold.TranslatePoint(new Point(hold.Bounds.Width / 2, hold.Bounds.Height / 2), window)!.Value;
            window.MouseDown(at, Avalonia.Input.MouseButton.Left);
            var timer = System.Diagnostics.Stopwatch.StartNew(); while (timer.ElapsedMilliseconds < 100) { System.Threading.Thread.Sleep(5); Settle(); }
            window.MouseUp(at, Avalonia.Input.MouseButton.Left); Settle();
            Ok(held == 1 && hold.HoldMs == 0, "a completed hold fires once even when its action changes HoldMs before native pointer release");
            hold.HoldMs = 200; window.MouseDown(at, Avalonia.Input.MouseButton.Left); window.MouseMove(new Point(499, 499)); window.MouseUp(new Point(499, 499), Avalonia.Input.MouseButton.Left); Settle();
            Ok(held == 1, "releasing a hold outside the native button cancels it");
            window.Close();
        }

        /// <summary>A dialog composed from a body and the app's own buttons: the key of the one picked, Enter for the main action
        /// (not a danger), Esc for null.</summary>
        static void DialogChoices()
        {
            ActionButton Open(Dialog d, string label) { d.Show(); Dispatcher.UIThread.RunJobs(); return d.GetVisualDescendants().OfType<ActionButton>().First(b => b.Content?.ToString() == label); }
            var options = new[] { new ChoiceOption("cancel", "Cancel"), new ChoiceOption("discard", "Don't save", Danger: true), new ChoiceOption("save", "Save", Primary: true) };
            var field = new Field { Text = "name" };
            var dialog = new Dialog("Save", new StackPanel { Children = { new TextBlock { Text = "Save changes?" }, field } }, options);
            var save = Open(dialog, "Save"); var discard = dialog.GetVisualDescendants().OfType<ActionButton>().First(b => b.Content?.ToString() == "Don't save"); var cancel = dialog.GetVisualDescendants().OfType<ActionButton>().First(b => b.Content?.ToString() == "Cancel");
            Ok(save.Kind == ButtonKind.Primary && discard.Kind == ButtonKind.Normal && discard.Live == Danger && cancel.TranslatePoint(default, dialog)!.Value.X < discard.TranslatePoint(default, dialog)!.Value.X && discard.TranslatePoint(default, dialog)!.Value.X < save.TranslatePoint(default, dialog)!.Value.X, "dialog: the app's buttons keep their order, the main one primary, a danger red");
            Ok(dialog.GetVisualDescendants().OfType<Field>().Contains(field), "dialog: the body holds a field");
            discard.PerformClick(); Dispatcher.UIThread.RunJobs();
            Ok(dialog.Chosen == "discard" && !dialog.IsVisible, "dialog: a click returns that button's key and closes");
            dialog = new Dialog("Save", new TextBlock { Text = "Save changes?" }, options); Open(dialog, "Save");
            dialog.KeyPress(Avalonia.Input.Key.Enter, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Dispatcher.UIThread.RunJobs();
            Ok(dialog.Chosen == "save", "dialog: Enter picks the primary button");
            dialog = new Dialog("Save", new TextBlock { Text = "Delete?" }, new[] { new ChoiceOption("no", "Cancel"), new ChoiceOption("yes", "Delete", Primary: true, Danger: true) }); Open(dialog, "Delete");
            dialog.KeyPress(Avalonia.Input.Key.Enter, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Dispatcher.UIThread.RunJobs();
            Ok(dialog.Chosen == null && dialog.IsVisible, "dialog: Enter never picks a danger");
            dialog.KeyPress(Avalonia.Input.Key.Escape, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Dispatcher.UIThread.RunJobs();
            Ok(dialog.Chosen == null && !dialog.IsVisible, "dialog: Esc closes with no choice");
            bool refused = false; try { new Dialog("x", new TextBlock { Text = "x" }, Array.Empty<ChoiceOption>()); } catch (ArgumentException) { refused = true; }
            Ok(refused, "dialog: a dialog without buttons is refused");
        }
    }
}
