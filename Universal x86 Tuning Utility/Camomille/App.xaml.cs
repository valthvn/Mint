using System;
using System.Threading;
using System.Windows;
namespace Camomille;
public partial class App : Application
{
 private Mutex? instance;
 protected override void OnStartup(StartupEventArgs e)
 {
  if (e.Args.Length == 2 && e.Args[0] == "--wait-for-parent" && int.TryParse(e.Args[1], out int parent))
  {
   try { System.Diagnostics.Process.GetProcessById(parent).WaitForExit(15000); }
   catch (ArgumentException) { }
  }
  instance = new Mutex(true, "CamomilleReborn", out bool created);
  if (!created) { Shutdown(); return; }
  base.OnStartup(e);
  var panel = new MainWindow();
  MainWindow = panel;
  panel.StartInTray();
 }
 protected override void OnExit(ExitEventArgs e) { instance?.Dispose(); base.OnExit(e); }
}

