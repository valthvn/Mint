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
 private readonly SensorMonitor sensorMonitor = new();
 private readonly Localization localization = new();
 private readonly SemaphoreSlim sensorLock = new(1, 1);
 private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(2) };
 private readonly CoolingMode cooling = new();
 private readonly Queue<float> history = new();
 private bool closing, busy, readyToClose, showingPanel;
 private bool updatingLanguage;
 private System.Windows.Forms.ToolStripMenuItem? openItem, quitItem;
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
  ApplyLanguage();
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
   ModeTitle.Text = active ? T("Fraîcheur activée") : T("Prêt à rafraîchir");
   if (tray != null) tray.Text = active ? T("Mint · Refroidissement activé") : T("Mint · Refroidissement désactivé");
   if (toggleItem != null) toggleItem.Text = saved ? T("Désactiver / restaurer le profil") : T("Activer le refroidissement");
   StateLabel.Text = active ? T("ACTIF") : T("EN VEILLE");
   ModeDescription.Text = active ? "" : T("Votre profil d’alimentation reste inchangé.");
   string action = saved ? T("Désactiver le refroidissement") : T("Activer le refroidissement");
   CoolingButton.ToolTip = action;
   System.Windows.Automation.AutomationProperties.SetName(CoolingButton, action);
   CoolingSurface.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(active ? "#B5F7CB" : "#ECF2DF"));
   CoolingStatus.Text = saved && !active ? T("Un profil sauvegardé peut être nettoyé. Votre profil actif sera conservé.") : "";
  }
  catch (Exception ex) { CoolingStatus.Text = ErrorText(ex.Message); }
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
    ? T("Désactivez le refroidissement pour changer cette option.")
    : custom ? T("Un profil Mint dédié, réutilisé à chaque activation.")
    : T("Les réglages CPU seront sauvegardés puis restaurés à la désactivation.");
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
  catch (Exception ex) { CoolingStatus.Text = ErrorText(ex.Message); }
  UpdatePlanChoices();
 }
 private async Task RefreshSensors()
 {
  if (closing || !await sensorLock.WaitAsync(0)) return;
  try
  {
   var snapshot = await Task.Run(sensorMonitor.Read);
   if (closing) return;
   CpuName.Text = string.IsNullOrEmpty(snapshot.Name) ? T("Processeur") : snapshot.Name;
   GaugeValue.Data = snapshot.Temperature.HasValue ? GaugeArc(Math.Clamp(snapshot.Temperature.Value / 120.0, 0, 1)) : Geometry.Empty;
   CpuTemperature.Text = snapshot.Temperature.HasValue ? $"{snapshot.Temperature:0} °C" : "— °C";
   CpuLoad.Text = snapshot.Load.HasValue ? $"{snapshot.Load:0} %" : "— %";
   CpuClock.Text = snapshot.Clock.HasValue ? $"{snapshot.Clock:0.00} GHz" : "— GHz";
   if (snapshot.Temperature.HasValue)
   {
    peak = Math.Max(peak, snapshot.Temperature.Value);
    history.Enqueue(snapshot.Temperature.Value);
    while (history.Count > 60) history.Dequeue();
    DrawHistory();
   }
   PeakTemperature.Text = peak > 0 ? $"{peak:0} °C" : "— °C";
   HardwareRows.ItemsSource = snapshot.Devices.Select(d => new HardwareRow(d.Name, d.Temperature.HasValue ? $"{d.Temperature:0} °C" : T("Indisponible"))).ToArray();
   CpuClock.ToolTip = T(snapshot.WindowsClock ? "Fréquence estimée par Windows (secours), pas une mesure directe des cœurs." : "Moyenne des fréquences des cœurs mesurées par les capteurs.");
   var messages = new List<string>();
   if (!snapshot.Temperature.HasValue) messages.Add(T(snapshot.DriverInstalled
    ? "Température CPU indisponible malgré PawnIO. Relancez en administrateur ou exportez le diagnostic dans les paramètres."
    : "PawnIO est absent. Installez le pilote de capteurs dans les paramètres, puis relancez Mint."));
   if (!snapshot.Clock.HasValue) messages.Add(T("Fréquence CPU indisponible."));
   if (!string.IsNullOrEmpty(snapshot.Error)) messages.Add(T("Capteurs indisponibles : ") + snapshot.Error);
   SensorStatus.Text = string.Join(" ", messages);
  }
  catch (Exception ex) { SensorStatus.Text = T("Capteurs indisponibles : ") + ErrorText(ex.Message); }
  finally { sensorLock.Release(); }
 }
 private string T(string french) => localization.Text(french);
 private string ErrorText(string message)
 {
  foreach (var key in Localization.Translations.Keys.OrderByDescending(k => k.Length))
   if (message.StartsWith(key, StringComparison.Ordinal)) return T(key) + message.Substring(key.Length);
  return message;
 }
 private void ApplyLanguage()
 {
  int index = 0;
  foreach (var pair in Localization.Translations) Resources["L" + (index++).ToString("000")] = T(pair.Key);
  Resources["VersionText"] = localization.Language == "fr"
   ? "Mint 1.1 · Indépendant de Camomile.\nUXTU · LibreHardwareMonitor · GPL-3.0"
   : "Mint 1.1 · Independent of Camomile.\nUXTU · LibreHardwareMonitor · GPL-3.0";
  Resources["PlanCustomDetail"] = localization.Language == "fr" ? "Un profil dédié, conservé pour les prochaines activations." : "A dedicated plan, kept for the next time you enable cooling.";
  Resources["PlanCurrentDetail"] = localization.Language == "fr" ? "Vos réglages sont sauvegardés, puis restaurés à l’arrêt." : "Your settings are backed up, then restored when cooling is disabled.";
  HistoryCanvas.ToolTip = localization.Language == "fr" ? "Historique de température · échelle verticale ajustée aux mesures." : "Temperature history · vertical scale adapts to the readings.";
  Language = System.Windows.Markup.XmlLanguage.GetLanguage(localization.Language);
  updatingLanguage = true;
  try { EnglishChoice.IsChecked = localization.Language == "en"; FrenchChoice.IsChecked = localization.Language == "fr"; }
  finally { updatingLanguage = false; }
  if (openItem != null) openItem.Text = T("Ouvrir Mint");
  if (quitItem != null) quitItem.Text = T("Quitter Mint");
  UpdatePlanChoices();
 }
 private async void Language_Checked(object sender, RoutedEventArgs e)
 {
  if (updatingLanguage || EnglishChoice == null || FrenchChoice == null) return;
  try
  {
   localization.Select(ReferenceEquals(sender, FrenchChoice) ? "fr" : "en");
   ApplyLanguage();
   await UpdateCoolingState();
   await RefreshSensors();
  }
  catch (Exception ex) { CoolingStatus.Text = ErrorText(ex.Message); ApplyLanguage(); }
 }
 private void Driver_Click(object sender, RoutedEventArgs e)
 {
  try { Process.Start(new ProcessStartInfo("https://pawnio.eu/") { UseShellExecute = true }); }
  catch (Exception ex) { CoolingStatus.Text = ErrorText(ex.Message); }
 }
 private async void Diagnostic_Click(object sender, RoutedEventArgs e)
 {
  var dialog = new Microsoft.Win32.SaveFileDialog { FileName = "Mint-sensors.txt", Filter = "Text (*.txt)|*.txt" };
  if (dialog.ShowDialog() != true) return;
  await sensorLock.WaitAsync();
  try
  {
   string report = await Task.Run(sensorMonitor.Report);
   await File.WriteAllTextAsync(dialog.FileName, report);
   CoolingStatus.Text = T("Diagnostic enregistré : ") + dialog.FileName;
  }
  catch (Exception ex) { CoolingStatus.Text = ErrorText(ex.Message); }
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
  double width = HistoryCanvas.ActualWidth, height = HistoryCanvas.ActualHeight;
  HistoryLine.Points = new PointCollection(TemperatureHistory.Layout(history.ToArray(), width, height).Select(p => new Point(p.X, p.Y)));
  var guides = new StreamGeometry();
  if (width > 4 && height > 4)
  {
   using var context = guides.Open();
   foreach (double y in new[] { 2.0, height - 2 })
   {
    context.BeginFigure(new Point(2, y), false, false);
    context.LineTo(new Point(width - 2, y), true, false);
   }
  }
  guides.Freeze();
  HistoryGuides.Data = guides;
 }
 private void History_SizeChanged(object sender, SizeChangedEventArgs e) => DrawHistory();
 private async void Cooling_Click(object sender, RoutedEventArgs e) => await ToggleCoolingAsync();
 private async Task ToggleCoolingAsync()
 {
  if (busy || closing) return;
  busy = true; CoolingButton.IsEnabled = false;
  UpdatePlanChoices();
  if (toggleItem != null) toggleItem.Enabled = false;
  CoolingStatus.Text = T("Modification du profil…");
  try
  {
   if (cooling.HasSavedProfile) await cooling.DisableAsync(); else await cooling.EnableAsync();
   await UpdateCoolingState();
   tray?.ShowBalloonTip(2500, "Mint", cooling.HasSavedProfile ? T("Refroidissement activé.") : T("Refroidissement désactivé. Votre profil est restauré."), System.Windows.Forms.ToolTipIcon.Info);
  }
  catch (Exception ex)
  {
   await UpdateCoolingState();
   CoolingStatus.Text = T("Échec : ") + ErrorText(ex.Message);
   tray?.ShowBalloonTip(4000, T("Mint · Modification impossible"), ErrorText(ex.Message), System.Windows.Forms.ToolTipIcon.Warning);
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
  catch (Exception ex) { CoolingStatus.Text = T("Relance annulée : ") + ErrorText(ex.Message); }
 }
 public void StartInTray()
 {
  trayIcon = CreateTrayIcon();
  var menu = new System.Windows.Forms.ContextMenuStrip();
  openItem = new System.Windows.Forms.ToolStripMenuItem(T("Ouvrir Mint"), null, (_, _) => ShowPanel());
  menu.Items.Add(openItem);
  toggleItem = new System.Windows.Forms.ToolStripMenuItem(T("Activer le refroidissement"));
  toggleItem.Click += async (_, _) => await ToggleCoolingAsync();
  menu.Items.Add(toggleItem);
  menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
  quitItem = new System.Windows.Forms.ToolStripMenuItem(T("Quitter Mint"), null, async (_, _) => await QuitAsync());
  menu.Items.Add(quitItem);
  tray = new System.Windows.Forms.NotifyIcon { Icon = trayIcon, Text = T("Mint · Prêt à rafraîchir"), ContextMenuStrip = menu, Visible = true };
  tray.MouseClick += (_, e) => { if (e.Button == System.Windows.Forms.MouseButtons.Left) { if (IsVisible) Hide(); else ShowPanel(); } };
  tray.BalloonTipClicked += (_, _) => ShowPanel();
  tray.ShowBalloonTip(4000, T("Mint est ouvert"), T("Mint reste dans la zone de notification. Cliquez sur la feuille pour gérer le refroidissement."), System.Windows.Forms.ToolTipIcon.Info);
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
  if (busy) { CoolingStatus.Text = T("Patientez pendant la modification du profil."); ShowPanel(); return; }
  if (closing) return;
  closing = true; timer.Stop(); Hide();
  await sensorLock.WaitAsync();
  try { await Task.Run(sensorMonitor.Dispose); }
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
