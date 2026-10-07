using System;
using System.IO;
using System.Text;
namespace Mint;
internal static class AppStorage
{
 // Compatibility identifier from versions up to 1.1.1, used only for data recovery and the instance lock.
 internal static string PreviousInstanceIdentifier => Encoding.UTF8.GetString(Convert.FromBase64String("Q2Ftb21pbGxlUmVib3Ju"));
 private static readonly Lazy<string> directory = new(() =>
 {
  string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
  string current = Path.Combine(local, "Mint");
  Migrate(Path.Combine(local, PreviousInstanceIdentifier), current);
  return current;
 });
 internal static string FilePath(string name) => Path.Combine(directory.Value, name);
 internal static void Migrate(string previous, string current)
 {
  if (!Directory.Exists(previous)) return;
  Directory.CreateDirectory(current);
  foreach (string name in new[] { "cooling.json", "cooling.json.preference", "cooling.json.custom", "language.json" })
  {
   string source = Path.Combine(previous, name), destination = Path.Combine(current, name);
   if (!File.Exists(source) || File.Exists(destination)) continue;
   string temporary = destination + ".migration";
   File.Copy(source, temporary, true);
   File.Move(temporary, destination);
  }
 }
}
