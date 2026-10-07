using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.PawnIo;
using System;
using System.Collections.Generic;
using System.Linq;
namespace Camomille;
internal sealed class SensorMonitor : IDisposable
{
 private readonly Computer computer = new() { IsCpuEnabled = true, IsGpuEnabled = true, IsStorageEnabled = true };
 private bool opened;
 private string lastError = "";
 internal sealed record Device(string Name, float? Temperature);
 internal sealed record Snapshot(string Name, float? Temperature, float? Load, float? Clock, bool WindowsClock, bool DriverInstalled, List<Device> Devices, string Error);
 internal Snapshot Read()
 {
  var errors = new List<string>();
  var devices = new List<Device>();
  var cpuSensors = new List<ISensor>();
  string name = "";
  try
  {
   if (!opened) { computer.Open(); opened = true; }
   foreach (var hardware in computer.Hardware)
   {
    try
    {
     UpdateTree(hardware);
     var sensors = Sensors(hardware).ToArray();
     if (hardware.HardwareType == HardwareType.Cpu) { name = hardware.Name; cpuSensors.AddRange(sensors); }
     else devices.Add(new Device(hardware.Name, CpuReadings.Temperature(sensors.Where(s => s.SensorType == SensorType.Temperature).Select(s => new CpuReadings.Sample(s.Name, s.Value)))));
    }
    catch (Exception ex) { errors.Add(hardware.Name + ": " + ex.Message); }
   }
  }
  catch (Exception ex) { errors.Add(ex.Message); }
  bool driver = PawnIo.IsInstalled;
  float? temperature = driver ? CpuReadings.Temperature(cpuSensors.Where(s => s.SensorType == SensorType.Temperature).Select(s => new CpuReadings.Sample(s.Name, s.Value))) : null;
  float? load = cpuSensors.FirstOrDefault(s => s.SensorType == SensorType.Load && s.Name.Contains("Total", StringComparison.OrdinalIgnoreCase))?.Value;
  float? clock = CpuReadings.Clock(cpuSensors.Where(s => s.SensorType == SensorType.Clock).Select(s => new CpuReadings.Sample(s.Name, s.Value)));
  bool windowsClock = !clock.HasValue;
  if (windowsClock)
  {
   try { clock = WindowsCpuClock.ReadGhz(); }
   catch (Exception ex) { errors.Add("Windows CPU clock: " + ex.Message); }
  }
  lastError = string.Join(Environment.NewLine, errors);
  return new Snapshot(name, temperature, load, clock, windowsClock, driver, devices, lastError);
 }
 private static void UpdateTree(IHardware hardware)
 {
  hardware.Update();
  foreach (var child in hardware.SubHardware) UpdateTree(child);
 }
 private static IEnumerable<ISensor> Sensors(IHardware hardware) => hardware.Sensors.Concat(hardware.SubHardware.SelectMany(Sensors));
 internal string Report() => $"Mint sensor diagnostic · {DateTimeOffset.Now:O}\nPawnIO installed: {PawnIo.IsInstalled}\nPawnIO version: {PawnIo.Version}\nWindows CPU GHz: {WindowsCpuClock.ReadGhz()}\nErrors: {lastError}\n" + computer.GetReport();
 public void Dispose() { if (opened) computer.Close(); }
}
