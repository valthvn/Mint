using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
namespace Mint;
internal sealed class CoolingMode
{
 internal enum PlanMode { Custom, Current }
 internal sealed record Values(uint MaximumAc, uint MaximumDc, uint BoostAc, uint BoostDc);
 internal sealed record Profile(Guid Original, Guid Cooling, Values? Restore = null, bool KeepCustom = false);
 private readonly string stateFile;
 private readonly string preferenceFile;
 private readonly string customFile;
 private readonly Func<string, Task<string>> run;
 private const string Processor = "54533251-82be-4824-96c1-47b60b740d00";
 private const string Maximum = "bc5038f7-23e0-4960-96da-33abaf5935ec";
 private const string Boost = "be337238-0d82-4146-a960-4f3749d470c7";
 public CoolingMode(string? statePath = null, Func<string, Task<string>>? runner = null)
 {
  stateFile = statePath ?? AppStorage.FilePath("cooling.json");
  preferenceFile = stateFile + ".preference";
  customFile = stateFile + ".custom";
  run = runner ?? RunPowerCfg;
 }
 public PlanMode SelectedMode
 {
  get => File.Exists(preferenceFile) && File.ReadAllText(preferenceFile) == "Current" ? PlanMode.Current : PlanMode.Custom;
 }
 public void SelectMode(PlanMode mode)
 {
  if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
  if (HasSavedProfile) throw new InvalidOperationException("Désactivez le refroidissement avant de changer de profil.");
  WriteAtomic(preferenceFile, mode.ToString());
 }
 private static void WriteAtomic(string path, string text)
 {
  Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
  File.WriteAllText(path + ".tmp", text);
  File.Move(path + ".tmp", path, true);
 }
 public bool HasSavedProfile => File.Exists(stateFile);
 private Profile ReadProfile()
 {
  var profile = JsonSerializer.Deserialize<Profile>(File.ReadAllText(stateFile));
  if (profile == null || profile.Original == Guid.Empty || profile.Cooling == Guid.Empty ||
   (profile.Restore == null ? profile.Original == profile.Cooling : profile.Original != profile.Cooling || profile.KeepCustom || profile.Restore.MaximumAc > 100 || profile.Restore.MaximumDc > 100 || profile.Restore.BoostAc > 6 || profile.Restore.BoostDc > 6))
   throw new InvalidOperationException("Sauvegarde du profil invalide. Aucun réglage modifié.");
  return profile;
 }
 private static Guid ParseGuid(string output)
 {
  var match = Regex.Match(output, @"\b[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}\b");
  if (!match.Success) throw new InvalidOperationException("Windows n’a pas retourné un profil valide.");
  return Guid.Parse(match.Value);
 }
 public async Task<bool> IsActiveAsync()
 {
  if (!HasSavedProfile) return false;
  var profile = ReadProfile();
  if (ParseGuid(await run("/getactivescheme")) != profile.Cooling) return false;
  return profile.Restore == null || await ReadValues(profile.Cooling) == new Values(99, 99, 0, 0);
 }
 private async Task<(uint ac, uint dc)> ReadSetting(Guid scheme, string setting)
 {
  string output = await run($"/qh {scheme} {Processor} {setting}");
  var matches = Regex.Matches(output, @"0x([0-9a-fA-F]{1,8})\s*$", RegexOptions.Multiline);
  int expected = setting == Maximum ? 5 : 2;
  if (matches.Count != expected) throw new InvalidOperationException("Impossible de sauvegarder les réglages secteur et batterie du profil.");
  return (Convert.ToUInt32(matches[^2].Groups[1].Value, 16), Convert.ToUInt32(matches[^1].Groups[1].Value, 16));
 }
 private async Task<Values> ReadValues(Guid scheme)
 {
  var maximum = await ReadSetting(scheme, Maximum);
  var boost = await ReadSetting(scheme, Boost);
  var values = new Values(maximum.ac, maximum.dc, boost.ac, boost.dc);
  if (values.MaximumAc > 100 || values.MaximumDc > 100 || values.BoostAc > 6 || values.BoostDc > 6)
   throw new InvalidOperationException("Les réglages CPU du profil ne peuvent pas être sauvegardés.");
  return values;
 }
 private async Task ApplyValues(Guid scheme, Values values)
 {
  await run($"/setacvalueindex {scheme} {Processor} {Maximum} {values.MaximumAc}");
  await run($"/setdcvalueindex {scheme} {Processor} {Maximum} {values.MaximumDc}");
  await run($"/setacvalueindex {scheme} {Processor} {Boost} {values.BoostAc}");
  await run($"/setdcvalueindex {scheme} {Processor} {Boost} {values.BoostDc}");
 }
 public async Task EnableAsync()
 {
  if (HasSavedProfile) throw new InvalidOperationException("Restaurez d’abord le profil sauvegardé.");
  Guid original = ParseGuid(await run("/getactivescheme"));
  if (SelectedMode == PlanMode.Current)
  {
   Values backup = await ReadValues(original);
   WriteAtomic(stateFile, JsonSerializer.Serialize(new Profile(original, original, backup)));
   try
   {
    await ApplyValues(original, new Values(99, 99, 0, 0));
    if (ParseGuid(await run("/getactivescheme")) == original) await run($"/setactive {original}");
   }
   catch
   {
    try { await DisableAsync(); } catch { /* Keep the backup for a retry. */ }
    throw;
   }
   return;
  }
  Guid cooling = Guid.NewGuid();
  bool created = true;
  if (File.Exists(customFile) && Guid.TryParse(File.ReadAllText(customFile), out Guid known) && known != Guid.Empty &&
   Regex.IsMatch(await run("/list"), $@"\b{known}\b", RegexOptions.IgnoreCase))
  {
   if (known == original) throw new InvalidOperationException("Le profil Mint est déjà actif. Choisissez un autre profil Windows avant de l’activer avec Mint.");
   cooling = known;
   created = false;
  }
  if (created) await run($"/duplicatescheme {original} {cooling}");
  bool saved = false;
  try
  {
   WriteAtomic(customFile, cooling.ToString());
   WriteAtomic(stateFile, JsonSerializer.Serialize(new Profile(original, cooling, KeepCustom: true)));
   saved = true;
   await run($"/changename {cooling} \"Mint - Refroidissement\"");
   await ApplyValues(cooling, new Values(99, 99, 0, 0));
   await run($"/setactive {cooling}");
  }
  catch
  {
   // Keep recovery data if either rollback operation fails.
   try
   {
    if (saved && ParseGuid(await run("/getactivescheme")) == cooling) await run($"/setactive {original}");
    if (created)
    {
     await run($"/delete {cooling}");
     if (File.Exists(customFile) && File.ReadAllText(customFile) == cooling.ToString()) File.Delete(customFile);
    }
    if (saved) File.Delete(stateFile);
   }
   catch { /* The saved profile allows a later recovery attempt. */ }
   throw;
  }
 }
 public async Task DisableAsync()
 {
  if (!HasSavedProfile) return;
  Profile profile = ReadProfile();
  if (profile.Restore != null)
  {
   await ApplyValues(profile.Original, profile.Restore);
   if (ParseGuid(await run("/getactivescheme")) == profile.Original) await run($"/setactive {profile.Original}");
   File.Delete(stateFile);
   return;
  }
  // Respect a different profile selected manually while cooling was enabled.
  if (ParseGuid(await run("/getactivescheme")) == profile.Cooling) await run($"/setactive {profile.Original}");
  if (!profile.KeepCustom) await run($"/delete {profile.Cooling}");
  File.Delete(stateFile);
 }
 private static async Task<string> RunPowerCfg(string arguments)
 {
  using var process = new Process { StartInfo = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "powercfg.exe"), arguments) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true } };
  process.Start();
  Task<string> output = process.StandardOutput.ReadToEndAsync();
  Task<string> error = process.StandardError.ReadToEndAsync();
  await process.WaitForExitAsync();
  string text = await output;
  string failure = await error;
  if (process.ExitCode != 0) throw new InvalidOperationException("Windows a refusé la modification du profil. " + (string.IsNullOrWhiteSpace(failure) ? text : failure).Trim());
  return text;
 }
}

