using System;
using System.Collections.Generic;
using System.Linq;
namespace Mint;
internal static class TemperatureHistory
{
 internal readonly record struct PlotPoint(double X, double Y);
 internal static PlotPoint[] Layout(IReadOnlyList<float> values, double width, double height)
 {
  if (values.Count == 0 || width <= 4 || height <= 4) return Array.Empty<PlotPoint>();
  double minimum = values.Min(), maximum = values.Max();
  double center = (minimum + maximum) / 2;
  double span = Math.Max(20, maximum - minimum + 10);
  return values.Select((value, index) => new PlotPoint(
   values.Count == 1 ? width / 2 : 2 + index * (width - 4) / (values.Count - 1),
   2 + (center + span / 2 - value) / span * (height - 4))).ToArray();
 }
}
