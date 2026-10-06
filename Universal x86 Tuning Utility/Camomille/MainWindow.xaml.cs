using LibreHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
namespace Camomille;
public partial class MainWindow : Window
{
 private readonly Computer computer = new() { IsCpuEnabled = true, IsGpuEnabled = true, IsStorageEnabled = true };
 private readonly SemaphoreSlim sensorLock = new(1, 1);
 private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(2) };
 private readonly CoolingMode cooling = new();
 private readonly Queue<float> history = new();
 private bool opened, closing, busy, readyToClose, showingPanel;
 private System.Windows.Forms.NotifyIcon? tray;
 private System.Drawing.Icon? trayIcon;
 private System.Windows.Forms.ToolStripMenuItem? toggleItem;
 private float peak;
 private bool settingsOpen;
 private bool updatingPlanChoices;
 private int navigationVersion;
 private static readonly DependencyProperty AnimatedScrollOffsetProperty = DependencyProperty.Register(
  "AnimatedScrollOffset", typeof(double), typeof(MainWindow), new PropertyMetadata(0.0, (owner, args) =>
  {
   var window = (MainWindow)owner;
   window.PanelScroll.ScrollToVerticalOffset((double)args.NewValue);
  }));
 private static DoubleAnimation Motion(double from, double to, int milliseconds) => new(from, to, TimeSpan.FromMilliseconds(milliseconds))
 {
  EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop
 };
 public MainWindow()
 {
  InitializeComponent();
  UpdatePlanChoices();
  DrawGaugeScale();
  timer.Tick += async (_, _) => await RefreshSensors();
 }
 public async Task InitializeAsync()
 {
  await UpdateCoolingState();
  await RefreshSensors();
  if (!closing) timer.Start();
 }
 private async Task UpdateCoolingState()
 {
  try
  {
   bool saved = cooling.HasSavedProfile;
   bool active = await cooling.IsActiveAsync();
   UpdatePlanChoices();
   ModeTitle.Text = active ? "Fraîcheur activée" : "Prêt à rafraîchir";
   if (tray != null) tray.Text = active ? "Mint · Refroidissement activé" : "Mint · Refroidissement désactivé";
   if (toggleItem != null) toggleItem.Text = saved ? "Désactiver / restaurer le profil" : "Activer le refroidissement";
   StateLabel.Text = active ? "ACTIF" : "EN VEILLE";
   ModeDescription.Text = active ? "" : "Votre profil d’alimentation reste inchangé.";
   string action = saved ? "Désactiver le refroidissement" : "Activer le refroidissement";
   CoolingButton.ToolTip = action;
   System.Windows.Automation.AutomationProperties.SetName(CoolingButton, action);
   CoolingSurface.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(active ? "#B5F7CB" : "#ECF2DF"));
   CoolingStatus.Text = saved && !active ? "Un profil sauvegardé peut être nettoyé. Votre profil actif sera conservé." : "";
  }
  catch (Exception ex) { CoolingStatus.Text = ex.Message; }
 }
 private void UpdatePlanChoices()
 {
  updatingPlanChoices = true;
  try
  {
   bool custom = cooling.SelectedMode == CoolingMode.PlanMode.Custom;
   CustomPlanChoice.IsChecked = custom;
   CurrentPlanChoice.IsChecked = !custom;
   PlanChoices.IsEnabled = !busy && !cooling.HasSavedProfile;
   PlanChoiceHint.Text = cooling.HasSavedProfile
    ? "Désactivez le refroidissement pour changer cette option."
    : custom ? "Un profil Mint dédié, réutilisé à chaque activation."
    : "Les réglages CPU seront sauvegardés puis restaurés à la désactivation.";
  }
  finally { updatingPlanChoices = false; }
 }
 private void PlanChoice_Checked(object sender, RoutedEventArgs e)
 {
  if (updatingPlanChoices || PlanChoices == null) return;
  try
  {
   cooling.SelectMode(ReferenceEquals(sender, CurrentPlanChoice) ? CoolingMode.PlanMode.Current : CoolingMode.PlanMode.Custom);
   CoolingStatus.Text = "";
  }
  catch (Exception ex) { CoolingStatus.Text = ex.Message; }
  UpdatePlanChoices();
 }
 private async Task RefreshSensors()
 {
  if (closing || !await sensorLock.WaitAsync(0)) return;
  try
  {
   var snapshot = await Task.Run(() =>
   {
    if (!opened) { computer.Open(); opened = true; }
    var rows = new List<HardwareRow>();
    string name = "Processeur";
    float? temperature = null, load = null, clock = null;
    foreach (var hardware in computer.Hardware)
    {
     hardware.Update();
     foreach (var child in hardware.SubHardware) child.Update();
     var sensors = hardware.Sensors.Concat(hardware.SubHardware.SelectMany(h => h.Sensors)).ToArray();
     var temperatures = sensors.Where(s => s.SensorType == SensorType.Temperature && s.Value.HasValue).ToArray();
     if (hardware.HardwareType == HardwareType.Cpu)
     {
      name = hardware.Name;
      var package = temperatures.FirstOrDefault(s => s.Name.Contains("Package") || s.Name.Contains("Tctl"));
      temperature = package?.Value ?? (temperatures.Length > 0 ? temperatures.Max(s => s.Value) : null);
      load = sensors.FirstOrDefault(s => s.SensorType == SensorType.Load && s.Name.Contains("Total"))?.Value;
      var clocks = sensors.Where(s => s.SensorType == SensorType.Clock && s.Name.Contains("Core") && s.Value.HasValue).ToArray();
      clock = clocks.Length > 0 ? clocks.Average(s => s.Value!.Value) / 1000 : null;
     }
     else rows.Add(new HardwareRow(hardware.Name, temperatures.Length > 0 ? $"{temperatures.Max(s => s.Value):0} °C" : "Indisponible"));
    }
    return (name, temperature, load, clock, rows);
   });
   if (closing) return;
   CpuName.Text = snapshot.name;
   GaugeValue.Data = snapshot.temperature.HasValue ? GaugeArc(Math.Clamp(snapshot.temperature.Value / 120.0, 0, 1)) : Geometry.Empty;
   CpuTemperature.Text = snapshot.temperature.HasValue ? $"{snapshot.temperature:0} °C" : "— °C";
   CpuLoad.Text = snapshot.load.HasValue ? $"{snapshot.load:0} %" : "— %";
   CpuClock.Text = snapshot.clock.HasValue ? $"{snapshot.clock:0.00} GHz" : "— GHz";
   if (snapshot.temperature.HasValue)
   {
    peak = Math.Max(peak, snapshot.temperature.Value);
    history.Enqueue(snapshot.temperature.Value);
    while (history.Count > 60) history.Dequeue();
    DrawHistory();
   }
   PeakTemperature.Text = peak > 0 ? $"{peak:0} °C" : "— °C";
   HardwareRows.ItemsSource = snapshot.rows;
   SensorStatus.Text = snapshot.temperature.HasValue ? "" : "Capteur CPU indisponible. Essayez le mode administrateur dans les paramètres.";
  }
  catch (Exception ex) { SensorStatus.Text = "Capteurs indisponibles : " + ex.Message; }
  finally { sensorLock.Release(); }
 }
 private static Point GaugePoint(double angle, double radius)
 {
  double radians = angle * Math.PI / 180;
  return new Point(150 + Math.Cos(radians) * radius, 120 + Math.Sin(radians) * radius);
 }
 private static Geometry GaugeArc(double fraction)
 {
  if (fraction <= 0) return Geometry.Empty;
  var geometry = new StreamGeometry();
  using (var context = geometry.Open())
  {
   context.BeginFigure(GaugePoint(135, 94), false, false);
   context.ArcTo(GaugePoint(135 + 270 * fraction, 94), new Size(94, 94), 0, fraction > 2.0 / 3, SweepDirection.Clockwise, true, false);
  }
  geometry.Freeze();
  return geometry;
 }
 private void DrawGaugeScale()
 {
  GaugeTrack.Data = GaugeArc(1);
  var ticks = new StreamGeometry();
  using (var context = ticks.Open())
  {
   for (int i = 0; i <= 36; i++)
   {
    double angle = 135 + 7.5 * i;
    context.BeginFigure(GaugePoint(angle, i % 6 == 0 ? 103 : 108), false, false);
    context.LineTo(GaugePoint(angle, 114), true, false);
   }
  }
  ticks.Freeze();
  GaugeTicks.Data = ticks;
 }
 private void DrawHistory()
 {
  float[] values = history.ToArray();
  var points = new PointCollection();
  for (int i = 0; i < values.Length; i++) points.Add(new Point(i * HistoryCanvas.ActualWidth / Math.Max(1, values.Length - 1), HistoryCanvas.ActualHeight - 2 - Math.Clamp(values[i], 0, 120) / 120 * Math.Max(0, HistoryCanvas.ActualHeight - 4)));
  HistoryLine.Points = points;
 }
 private void History_SizeChanged(object sender, SizeChangedEventArgs e) => DrawHistory();
 private async void Cooling_Click(object sender, RoutedEventArgs e) => await ToggleCoolingAsync();
 private async Task ToggleCoolingAsync()
 {
  if (busy || closing) return;
  busy = true; CoolingButton.IsEnabled = false;
  UpdatePlanChoices();
  if (toggleItem != null) toggleItem.Enabled = false;
  CoolingStatus.Text = "Modification du profil…";
  try
  {
   if (cooling.HasSavedProfile) await cooling.DisableAsync(); else await cooling.EnableAsync();
   await UpdateCoolingState();
   tray?.ShowBalloonTip(2500, "Mint", cooling.HasSavedProfile ? "Refroidissement activé." : "Refroidissement désactivé. Votre profil est restauré.", System.Windows.Forms.ToolTipIcon.Info);
  }
  catch (Exception ex)
  {
   await UpdateCoolingState();
   CoolingStatus.Text = "Échec : " + ex.Message;
   tray?.ShowBalloonTip(4000, "Mint · Modification impossible", ex.Message, System.Windows.Forms.ToolTipIcon.Warning);
  }
  finally { busy = false; CoolingButton.IsEnabled = true; UpdatePlanChoices(); if (toggleItem != null) toggleItem.Enabled = true; }
 }
 private void Settings_Click(object sender, RoutedEventArgs e)
 {
  settingsOpen = !settingsOpen;
  int version = ++navigationVersion;
  bool opening = settingsOpen;
  SettingsPanel.Visibility = Visibility.Visible;
  PanelScroll.UpdateLayout();
  double from = PanelScroll.VerticalOffset;
  double settingsBottom = SettingsPanel.TranslatePoint(new Point(0, SettingsPanel.ActualHeight), PanelScroll).Y + from;
  double target = opening ? Math.Clamp(settingsBottom + 14 - PanelScroll.ViewportHeight, 0, PanelScroll.ScrollableHeight) : 0;
  BeginAnimation(AnimatedScrollOffsetProperty, null);
  SetValue(AnimatedScrollOffsetProperty, from);
  if (!SystemParameters.ClientAreaAnimation)
  {
   SetValue(AnimatedScrollOffsetProperty, target);
   if (!opening) SettingsPanel.Visibility = Visibility.Collapsed;
   return;
  }
  var animation = Motion(from, target, 340);
  animation.Completed += (_, _) =>
  {
   if (version != navigationVersion) return;
   SetValue(AnimatedScrollOffsetProperty, target);
   if (!opening) SettingsPanel.Visibility = Visibility.Collapsed;
  };
  BeginAnimation(AnimatedScrollOffsetProperty, animation);
  if (opening)
  {
   SettingsPanel.BeginAnimation(OpacityProperty, Motion(0.45, 1, 260));
  }
 }
 private void Button_Motion(object sender, System.Windows.Input.MouseEventArgs e)
 {
  if (!SystemParameters.ClientAreaAnimation || sender is not System.Windows.Controls.Button button) return;
  if (button.RenderTransform is not ScaleTransform)
  {
   button.RenderTransformOrigin = new Point(0.5, 0.5);
   button.RenderTransform = new ScaleTransform(1, 1);
  }
  var scale = (ScaleTransform)button.RenderTransform;
  bool pressed = e.RoutedEvent == System.Windows.Input.Mouse.PreviewMouseDownEvent;
  double target = pressed ? 0.96 : button.IsMouseOver ? 1.025 : 1;
  var x = Motion(scale.ScaleX, target, pressed ? 80 : 150);
  var y = Motion(scale.ScaleY, target, pressed ? 80 : 150);
  scale.ScaleX = target; scale.ScaleY = target;
  scale.BeginAnimation(ScaleTransform.ScaleXProperty, x);
  scale.BeginAnimation(ScaleTransform.ScaleYProperty, y);
 }
 
 private async void Admin_Click(object sender, RoutedEventArgs e)
 {
  if (busy) return;
  try
  {
   Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas", Arguments = "--wait-for-parent " + Environment.ProcessId });
   await QuitAsync();
  }
  catch (Exception ex) { CoolingStatus.Text = "Relance annulée : " + ex.Message; }
 }
 public void StartInTray()
 {
  trayIcon = CreateTrayIcon();
  var menu = new System.Windows.Forms.ContextMenuStrip();
  menu.Items.Add("Ouvrir Mint", null, (_, _) => ShowPanel());
  toggleItem = new System.Windows.Forms.ToolStripMenuItem("Activer le refroidissement");
  toggleItem.Click += async (_, _) => await ToggleCoolingAsync();
  menu.Items.Add(toggleItem);
  menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
  menu.Items.Add("Quitter Mint", null, async (_, _) => await QuitAsync());
  tray = new System.Windows.Forms.NotifyIcon { Icon = trayIcon, Text = "Mint · Prêt à rafraîchir", ContextMenuStrip = menu, Visible = true };
  tray.MouseClick += (_, e) => { if (e.Button == System.Windows.Forms.MouseButtons.Left) { if (IsVisible) Hide(); else ShowPanel(); } };
  tray.BalloonTipClicked += (_, _) => ShowPanel();
  tray.ShowBalloonTip(4000, "Mint est ouvert", "Mint reste dans la zone de notification. Cliquez sur la feuille pour gérer le refroidissement.", System.Windows.Forms.ToolTipIcon.Info);
  _ = InitializeAsync();
 }
 public void ShowPanel()
 {
  if (!Dispatcher.CheckAccess())
  {
   Dispatcher.BeginInvoke(new Action(ShowPanel));
   return;
  }
  if (closing || showingPanel) return;
  bool wasVisible = IsVisible;
  showingPanel = true;
  try
  {
  var cursor = System.Windows.Forms.Cursor.Position;
  var screen = System.Windows.Forms.Screen.FromPoint(cursor);
  var area = screen.WorkingArea;
  var handle = new System.Windows.Interop.WindowInteropHelper(this).EnsureHandle();
  // Les coordonnées natives restent correctes entre écrans de DPI différents.
  SetWindowPos(handle, IntPtr.Zero, area.Right - (int)Width, area.Bottom - (int)Height, 0, 0, 0x15);
  Show();
  var dpi = VisualTreeHelper.GetDpi(this);
  MaxHeight = Math.Max(120, area.Height / dpi.DpiScaleY - 16);
  MaxWidth = Math.Max(120, area.Width / dpi.DpiScaleX - 16);
  UpdateLayout();
  int popupWidth = (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX);
  int popupHeight = (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY);
  int x = Math.Clamp(cursor.X - popupWidth / 2, area.Left + 8, Math.Max(area.Left + 8, area.Right - popupWidth - 8));
  int y = area.Top > screen.Bounds.Top ? area.Top + 8 : Math.Max(area.Top + 8, area.Bottom - popupHeight - 8);
  SetWindowPos(handle, IntPtr.Zero, x, y, 0, 0, 0x15);
  if (IsVisible) Activate();
  if (!wasVisible && SystemParameters.ClientAreaAnimation)
  {
   PanelSurface.BeginAnimation(OpacityProperty, Motion(0, 1, 180));
   ((TranslateTransform)PanelSurface.RenderTransform).BeginAnimation(TranslateTransform.YProperty, Motion(8, 0, 220));
  }
  _ = UpdateCoolingState();
  }
  finally { showingPanel = false; }
 }
 private void Panel_Deactivated(object? sender, EventArgs e)
 {
  if (!showingPanel) Hide();
 }
 private void Panel_KeyDown(object sender, System.Windows.Input.KeyEventArgs e) { if (e.Key == System.Windows.Input.Key.Escape) Hide(); }
 private void Hide_Click(object sender, RoutedEventArgs e) => Hide();
 private async void Quit_Click(object sender, RoutedEventArgs e) => await QuitAsync();
 private void Window_Closing(object? sender, CancelEventArgs e) { if (!readyToClose) { e.Cancel = true; Hide(); } }
 public async Task QuitAsync()
 {
  if (busy) { CoolingStatus.Text = "Patientez pendant la modification du profil."; ShowPanel(); return; }
  if (closing) return;
  closing = true; timer.Stop(); Hide();
  await sensorLock.WaitAsync();
  try { if (opened) await Task.Run(computer.Close); }
  catch (Exception ex) { Debug.WriteLine(ex); }
  finally
  {
   sensorLock.Release(); tray?.ContextMenuStrip?.Dispose(); tray?.Dispose(); trayIcon?.Dispose(); readyToClose = true; Close();
   Application.Current.Shutdown();
  }
 }
 private static System.Drawing.Icon CreateTrayIcon()
 {
  using var bitmap = new System.Drawing.Bitmap(32, 32);
  using var graphics = System.Drawing.Graphics.FromImage(bitmap);
  graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
  using var background = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(12, 105, 86));
  using var leaf = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(173, 246, 211));
  using var vein = new System.Drawing.Pen(System.Drawing.Color.FromArgb(12, 105, 86), 2);
  graphics.FillEllipse(background, 1, 1, 30, 30);
  using var path = new System.Drawing.Drawing2D.GraphicsPath();
  path.AddBezier(8, 24, 3, 9, 16, 5, 25, 7); path.AddBezier(25, 7, 27, 20, 19, 27, 8, 24);
  graphics.FillPath(leaf, path); graphics.DrawLine(vein, 9, 23, 21, 11);
  var handle = bitmap.GetHicon();
  try { using var icon = System.Drawing.Icon.FromHandle(handle); return (System.Drawing.Icon)icon.Clone(); }
  finally { DestroyIcon(handle); }
 }
 [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);
 [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
 private sealed record HardwareRow(string Name, string Temperature);
}
