using System;
using System.Threading;
using System.Windows;
namespace Mint;
public partial class App : Application
{
 private Mutex? instance;
 private Mutex? previousInstance;
 protected override void OnStartup(StartupEventArgs e)
 {
  if (e.Args.Length == 2 && e.Args[0] == "--wait-for-parent" && int.TryParse(e.Args[1], out int parent))
  {
   try { System.Diagnostics.Process.GetProcessById(parent).WaitForExit(15000); }
   catch (ArgumentException) { }
  }
  if (e.Args.Length == 2 && e.Args[0] == "--sensor-report")
  {
   try
   {
    using var monitor = new SensorMonitor();
    var snapshot = monitor.Read();
    System.IO.File.WriteAllText(e.Args[1], $"CPU: {snapshot.Name}\nTemperature: {snapshot.Temperature}\nClock: {snapshot.Clock}\nWindows fallback: {snapshot.WindowsClock}\n" + monitor.Report());
    Shutdown(0);
   }
   catch (Exception ex) { System.IO.File.WriteAllText(e.Args[1], ex.ToString()); Shutdown(1); }
   return;
  }
  instance = new Mutex(true, "Mint.App", out bool created);
  if (!created) { Shutdown(); return; }
  previousInstance = new Mutex(true, AppStorage.PreviousInstanceIdentifier, out bool previousCreated);
  if (!previousCreated) { Shutdown(); return; }
  base.OnStartup(e);
  var panel = new MainWindow();
  MainWindow = panel;
  panel.StartInTray();
 }
 protected override void OnExit(ExitEventArgs e) { previousInstance?.Dispose(); instance?.Dispose(); base.OnExit(e); }
}
