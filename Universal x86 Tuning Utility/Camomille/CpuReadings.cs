using System;
using System.Collections.Generic;
using System.Linq;
namespace Camomille;
internal static class CpuReadings
{
 internal readonly record struct Sample(string Name, float? Value);
 internal static float? Temperature(IEnumerable<Sample> samples)
 {
  var valid = samples.Where(s => !s.Name.Contains("Distance", StringComparison.OrdinalIgnoreCase) && s.Value.HasValue && float.IsFinite(s.Value.Value) && s.Value > 0 && s.Value < 150).ToArray();
  var package = valid.FirstOrDefault(s => s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) || s.Name.Contains("Tctl", StringComparison.OrdinalIgnoreCase));
  return package.Value ?? (valid.Length > 0 ? valid.Max(s => s.Value) : null);
 }
 internal static float? Clock(IEnumerable<Sample> samples)
 {
  var valid = samples.Where(s => s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase) && s.Value.HasValue && float.IsFinite(s.Value.Value) && s.Value > 0).ToArray();
  return valid.Length > 0 ? valid.Average(s => s.Value!.Value) / 1000 : null;
 }
}
