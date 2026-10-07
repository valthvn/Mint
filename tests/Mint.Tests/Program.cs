using Mint;
using System.Text.Json;
static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
string directory = Path.Combine(Path.GetTempPath(), "MintTests-" + Guid.NewGuid());
Directory.CreateDirectory(directory);
try
{
 Guid original = Guid.NewGuid(), active = original;
 var commands = new List<string>();
 string file = Path.Combine(directory, "cooling.json");
 string? fail = null;
 var values = new Dictionary<Guid, CoolingMode.Values> { [original] = new(100, 85, 2, 4) };
 bool invalidQuery = false;
 Task<string> Runner(string command)
 {
  commands.Add(command);
  if (fail != null && command.StartsWith(fail)) throw new InvalidOperationException("Simulated failure");
  if (command == "/getactivescheme") return Task.FromResult("Profil actif : " + active);
  if (command == "/list") return Task.FromResult(string.Join("\n", values.Keys));
  string[] parts = command.Split(' ');
  if (parts[0] == "/duplicatescheme") values.Add(Guid.Parse(parts[2]), values[Guid.Parse(parts[1])]);
  if (parts[0] == "/delete") values.Remove(Guid.Parse(parts[1]));
  if (parts[0] == "/qh")
  {
   if (invalidQuery) return Task.FromResult("Unavailable");
   var settings = values[Guid.Parse(parts[1])];
   bool maximum = parts[3].StartsWith("bc5038f7");
   uint ac = maximum ? settings.MaximumAc : settings.BoostAc, dc = maximum ? settings.MaximumDc : settings.BoostDc;
   return Task.FromResult((maximum ? "Minimum: 0x00000000\r\nMaximum: 0x00000064\r\nIncrement: 0x00000001\r\n" : "") + $"Secteur: 0x{ac:x8}\r\nBatterie: 0x{dc:x8}\r\n");
  }
  if (parts[0] == "/setacvalueindex" || parts[0] == "/setdcvalueindex")
  {
   Guid id = Guid.Parse(parts[1]);
   var settings = values[id]; uint value = uint.Parse(parts[4]);
   bool ac = parts[0] == "/setacvalueindex", maximum = parts[3].StartsWith("bc5038f7");
   values[id] = maximum ? ac ? settings with { MaximumAc = value } : settings with { MaximumDc = value }
    : ac ? settings with { BoostAc = value } : settings with { BoostDc = value };
  }
  if (command.StartsWith("/setactive ")) active = Guid.Parse(command.Split(' ')[1]);
  return Task.FromResult("");
 }
 var mode = new CoolingMode(file, Runner);
 await mode.EnableAsync();
 Guid custom = active;
 var baseline = new CoolingMode.Values(100, 85, 2, 4);
 var limits = new CoolingMode.Values(99, 99, 0, 0);
 Check(values[original] == baseline && values[custom] == limits, "Custom must preserve original settings");
 Check(mode.HasSavedProfile && active != original, "Cooling must activate a separate scheme");
 Check(commands.Count(c => c.StartsWith("/setacvalueindex")) == 2 && commands.Count(c => c.StartsWith("/setdcvalueindex")) == 2, "AC and DC settings must both be applied");
 Check(await mode.IsActiveAsync(), "Saved scheme must be reported active");
 var restarted = new CoolingMode(file, Runner);
 await restarted.DisableAsync();
 Check(values.ContainsKey(custom), "Custom plan must persist after disable");
 await restarted.EnableAsync();
 Check(active == custom && commands.Count(c => c.StartsWith("/duplicatescheme")) == 1, "Custom must be reused after restart");
 await restarted.DisableAsync();
 Check(active == original && !mode.HasSavedProfile, "Recovery after restart must restore original");
 Console.WriteLine("PASS: reusable custom plan, AC/DC limits, persisted restart recovery");
 values.Remove(custom);
 commands.Clear(); fail = "/setdcvalueindex";
 try { await mode.EnableAsync(); throw new Exception("Expected failure"); } catch (InvalidOperationException) { }
 Check(active == original && !mode.HasSavedProfile && commands.Any(c => c.StartsWith("/delete")), "Partial configuration must clean up without activating");
 Console.WriteLine("PASS: failed configuration rollback");
 fail = null; await mode.EnableAsync();
 active = Guid.NewGuid(); Guid manuallySelected = active;
 values[active] = baseline;
 await mode.DisableAsync();
 Check(active == manuallySelected && !mode.HasSavedProfile, "Must preserve a manually selected profile");
 Console.WriteLine("PASS: respects manually selected power scheme");
 active = original;
 Guid legacy = Guid.NewGuid(); values[legacy] = limits; active = legacy;
 await File.WriteAllTextAsync(file, JsonSerializer.Serialize(new CoolingMode.Profile(original, legacy)));
 fail = "/delete";
 try { await mode.DisableAsync(); throw new Exception("Expected failure"); } catch (InvalidOperationException) { }
 Check(mode.HasSavedProfile, "Failed cleanup must retain recovery data");
 fail = null; await mode.DisableAsync();
 Check(!mode.HasSavedProfile, "Cleanup retry must succeed");
 Console.WriteLine("PASS: legacy state recovery and failed cleanup retry");

 mode.SelectMode(CoolingMode.PlanMode.Current);
 restarted = new CoolingMode(file, Runner);
 Check(restarted.SelectedMode == CoolingMode.PlanMode.Current, "Preference must persist");
 commands.Clear(); await restarted.EnableAsync();
 Check(active == original && values[original] == limits, "Current mode must edit the same scheme");
 Check(!commands.Any(c => c.StartsWith("/duplicatescheme") || c.StartsWith("/delete")), "Current mode must not create or delete a plan");
 Check(await mode.IsActiveAsync(), "Current mode must report applied settings");
 try { mode.SelectMode(CoolingMode.PlanMode.Custom); throw new Exception("Expected selection lock"); } catch (InvalidOperationException) { }
 values[original] = values[original] with { MaximumDc = 75 };
 Check(!await mode.IsActiveAsync(), "External edits must not be reported active");
 active = manuallySelected;
 await new CoolingMode(file, Runner).DisableAsync();
 Check(values[original] == baseline && active == manuallySelected && !mode.HasSavedProfile, "Restore exact AC/DC values without changing a manually selected plan");
 active = original;
 Console.WriteLine("PASS: current-plan preference, exact restore after restart, manual switch and external edits");

 fail = "/setdcvalueindex";
 try { await mode.EnableAsync(); throw new Exception("Expected failure"); } catch (InvalidOperationException) { }
 Check(mode.HasSavedProfile, "Failed rollback must retain recovery data");
 fail = null; await mode.DisableAsync();
 Check(values[original] == baseline && !mode.HasSavedProfile, "Retry must restore exact settings");
 await mode.EnableAsync(); fail = "/setdcvalueindex";
 try { await mode.DisableAsync(); throw new Exception("Expected failure"); } catch (InvalidOperationException) { }
 Check(mode.HasSavedProfile, "Failed restore must keep recovery data");
 fail = null; await mode.DisableAsync();
 Check(values[original] == baseline && !mode.HasSavedProfile, "Restore retry must recover settings");
 Console.WriteLine("PASS: current-plan partial rollback and restore failure recovery");

 invalidQuery = true; commands.Clear();
 try { await mode.EnableAsync(); throw new Exception("Expected query failure"); } catch (InvalidOperationException) { }
 Check(!mode.HasSavedProfile && !commands.Any(c => c.StartsWith("/set")), "Unreadable values must abort before modification");
 invalidQuery = false;
 Console.WriteLine("PASS: unreadable settings abort before modification");
 await File.WriteAllTextAsync(file, JsonSerializer.Serialize(new CoolingMode.Profile(Guid.Empty, Guid.Empty)));
 commands.Clear();
 try { await mode.DisableAsync(); throw new Exception("Expected invalid state failure"); } catch (InvalidOperationException) { }
 Check(commands.Count == 0, "Invalid saved state must not execute commands");
 Console.WriteLine("PASS: invalid saved state rejected without modifying Windows");
}
finally { Directory.Delete(directory, true); }

Check(CpuReadings.Temperature(new[] { new CpuReadings.Sample("CPU Package", 0), new CpuReadings.Sample("Core #1", 48) }) == 48,
 "A failed driver must not turn zero package readings into real CPU temperatures");
Check(CpuReadings.Temperature(new[] { new CpuReadings.Sample("CPU package", 55), new CpuReadings.Sample("Core #1", 62) }) == 55,
 "Package selection must be case insensitive");
Check(CpuReadings.Clock(new[] { new CpuReadings.Sample("Bus Speed", 100), new CpuReadings.Sample("CPU core #1", 2400), new CpuReadings.Sample("CPU core #2", 2600) }) == 2.5f,
 "CPU clock must include lowercase core names and exclude bus clocks");
Check(CpuReadings.Clock(new[] { new CpuReadings.Sample("Core #1", float.NaN), new CpuReadings.Sample("Core #2", 0) }) == null,
 "Invalid clocks must allow Windows fallback");
Check(CpuReadings.Temperature(new[] { new CpuReadings.Sample("CPU Core #1 Distance to TjMax", 90) }) == null,
 "Distance to the thermal limit is not a CPU temperature");
Console.WriteLine("PASS: valid CPU temperature, package priority and core clock selection");
string languageFile = Path.Combine(Path.GetTempPath(), "MintLanguage-" + Guid.NewGuid() + ".json");
try
{
 var language = new Localization(languageFile);
 Check(language.Language == "en" && language.Text("Paramètres") == "Settings", "First launch must use English");
 language.Select("fr");
 Check(new Localization(languageFile).Text("Paramètres") == "Paramètres", "French must survive restart");
 language.Select("en");
 Check(new Localization(languageFile).Language == "en", "English selection must survive restart");
 File.WriteAllText(languageFile, "broken json");
 Check(new Localization(languageFile).Language == "en", "Corrupted preferences must fall back to English");
 Console.WriteLine("PASS: English default, language persistence and corrupt preference recovery");
}
finally { if (File.Exists(languageFile)) File.Delete(languageFile); }

var flatHistory = TemperatureHistory.Layout(new float[] { 72, 72, 72 }, 330, 48);
Check(flatHistory.All(p => p.Y == 24) && flatHistory.First().X == 2 && flatHistory.Last().X == 328,
 "A flat temperature trace must be centered with equal left and right padding");
var changingHistory = TemperatureHistory.Layout(new float[] { 45, 75, 95, 60 }, 200, 48);
Check(changingHistory.All(p => p.X >= 2 && p.X <= 198 && p.Y >= 2 && p.Y <= 46),
 "Temperature history must stay inside the plot at any width");
Check(TemperatureHistory.Layout(new float[] { 65 }, 330, 48).Single() == new TemperatureHistory.PlotPoint(165, 24),
 "The first temperature sample must be centered");
Check(TemperatureHistory.Layout(Array.Empty<float>(), 330, 48).Length == 0,
 "Empty history must not draw a fabricated trace");
Console.WriteLine("PASS: centered temperature history, responsive bounds and first sample");

string migrationRoot = Path.Combine(Path.GetTempPath(), "MintMigration-" + Guid.NewGuid());
try
{
 string previous = Path.Combine(migrationRoot, "previous"), current = Path.Combine(migrationRoot, "current");
 Directory.CreateDirectory(previous);
 File.WriteAllText(Path.Combine(previous, "cooling.json"), "recovery backup");
 File.WriteAllText(Path.Combine(previous, "cooling.json.custom"), "saved plan");
 File.WriteAllText(Path.Combine(previous, "language.json"), "\"fr\"");
 AppStorage.Migrate(previous, current);
 Check(File.ReadAllText(Path.Combine(current, "cooling.json")) == "recovery backup", "Storage migration must preserve power plan recovery");
 Check(File.Exists(Path.Combine(previous, "cooling.json")), "Migration must retain the original backup");
 File.WriteAllText(Path.Combine(current, "language.json"), "\"en\"");
 AppStorage.Migrate(previous, current);
 Check(File.ReadAllText(Path.Combine(current, "language.json")) == "\"en\"", "Migration must not replace newer preferences");
 Console.WriteLine("PASS: storage migration preserves recovery backups and newer preferences");
}
finally { if (Directory.Exists(migrationRoot)) Directory.Delete(migrationRoot, true); }
