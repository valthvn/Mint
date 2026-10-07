using System;
using System.Linq;
using System.Runtime.InteropServices;
namespace Camomille;
internal static class WindowsCpuClock
{
 // Windows-reported frequency; this is not a per-core hardware measurement.
 internal static float? ReadGhz()
 {
  var processors = new ProcessorPowerInformation[Environment.ProcessorCount];
  uint size = checked((uint)(processors.Length * Marshal.SizeOf<ProcessorPowerInformation>()));
  if (CallNtPowerInformation(11, IntPtr.Zero, 0, processors, size) != 0) return null;
  var clocks = processors.Where(p => p.CurrentMhz > 0).ToArray();
  return clocks.Length > 0 ? (float)(clocks.Average(p => (double)p.CurrentMhz) / 1000) : null;
 }
 [StructLayout(LayoutKind.Sequential)]
 private struct ProcessorPowerInformation
 {
  public uint Number, MaxMhz, CurrentMhz, MhzLimit, MaxIdleState, CurrentIdleState;
 }
 [DllImport("powrprof.dll")]
 private static extern uint CallNtPowerInformation(int level, IntPtr input, uint inputSize,
  [Out] ProcessorPowerInformation[] output, uint outputSize);
}
