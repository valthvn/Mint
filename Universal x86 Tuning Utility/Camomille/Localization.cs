using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
namespace Camomille;
internal sealed class Localization
{
 private readonly string file;
 internal string Language { get; private set; } = "en";
 internal Localization(string? path = null)
 {
  file = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CamomilleReborn", "language.json");
  try { if (File.Exists(file) && JsonSerializer.Deserialize<string>(File.ReadAllText(file)) == "fr") Language = "fr"; }
  catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException) { }
 }
 internal void Select(string language)
 {
  if (language != "en" && language != "fr") throw new ArgumentOutOfRangeException(nameof(language));
  Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
  File.WriteAllText(file + ".tmp", JsonSerializer.Serialize(language));
  File.Move(file + ".tmp", file, true);
  Language = language;
 }
 internal string Text(string french) => Language == "fr" ? french : Translations.GetValueOrDefault(french, french);
 internal static readonly Dictionary<string, string> Translations = new()
 {
  ["  M / THERMIQUE"] = "  M / THERMAL",
  ["Paramètres"] = "Settings",
  ["Masquer"] = "Hide",
  ["Masquer le panneau"] = "Hide panel",
  ["Détection du processeur…"] = "Detecting processor…",
  ["Processeur"] = "Processor",
  ["Température du processeur"] = "Processor temperature",
  ["TEMPÉRATURE CPU"] = "CPU TEMPERATURE",
  ["PIC DE SESSION"] = "SESSION PEAK",
  ["TRACE THERMIQUE"] = "THERMAL HISTORY",
  ["Historique de température"] = "Temperature history",
  ["EN VEILLE"] = "STANDBY",
  ["ACTIF"] = "ACTIVE",
  ["Prêt à rafraîchir"] = "Ready to cool",
  ["Fraîcheur activée"] = "Cooling enabled",
  ["Activer ou désactiver le refroidissement"] = "Enable or disable cooling",
  ["Votre profil d’alimentation reste inchangé."] = "Your power plan remains unchanged.",
  ["FRÉQUENCE"] = "FREQUENCY",
  ["CHARGE CPU"] = "CPU LOAD",
  ["Connexion aux capteurs…"] = "Connecting to sensors…",
  ["← Revenir au cadran"] = "← Back to dashboard",
  ["SOUS LE CAPOT"] = "UNDER THE HOOD",
  ["PROFIL D’ALIMENTATION"] = "POWER PLAN",
  ["Utiliser le profil Mint"] = "Use the Mint power plan",
  ["Utiliser le profil d’alimentation Mint"] = "Use the Mint power plan",
  ["Modifier le profil actuel"] = "Modify the current power plan",
  ["Modifier le profil d’alimentation actuel"] = "Modify the current power plan",
  ["Le mode fraîcheur limite le CPU à 99 % et désactive le boost. À la désactivation, Mint restaure votre profil ou ses réglages. Masquer ou quitter Mint conserve le mode choisi."] = "Cooling limits the CPU to 99% and disables boost. Turning it off restores your power plan or its settings. Hiding or quitting Mint keeps the selected mode.",
  ["Relancer en administrateur"] = "Restart as administrator",
  ["Pour accéder aux capteurs qui nécessitent une élévation"] = "Access sensors requiring administrator privileges",
  ["Quitter Mint"] = "Quit Mint",
  ["Mint 1.0 · Indépendant de Camomile.\nUXTU · LibreHardwareMonitor · GPL-3.0"] = "Mint 1.1 · Independent of Camomile.\nUXTU · LibreHardwareMonitor · GPL-3.0",
  ["LANGUE"] = "LANGUAGE",
  ["Langue de l’interface"] = "Interface language",
  ["Installer le pilote de capteurs"] = "Install sensor driver",
  ["Ouvre le site officiel de PawnIO. Installez le pilote signé, puis relancez Mint."] = "Opens the official PawnIO website. Install the signed driver, then restart Mint.",
  ["Exporter le diagnostic des capteurs"] = "Export sensor diagnostics",
  ["PawnIO est absent. Installez le pilote de capteurs dans les paramètres, puis relancez Mint."] = "PawnIO is missing. Install the sensor driver from Settings, then restart Mint.",
  ["Température CPU indisponible malgré PawnIO. Relancez en administrateur ou exportez le diagnostic dans les paramètres."] = "CPU temperature is unavailable despite PawnIO. Restart as administrator or export sensor diagnostics from Settings.",
  ["Fréquence estimée par Windows (secours), pas une mesure directe des cœurs."] = "Windows-reported frequency (fallback), not a direct core measurement.",
  ["Moyenne des fréquences des cœurs mesurées par les capteurs."] = "Average core frequency measured by hardware sensors.",
  ["Fréquence CPU indisponible."] = "CPU frequency unavailable.",
  ["Capteurs indisponibles : "] = "Sensors unavailable: ",
  ["Indisponible"] = "Unavailable",
  ["Mint · Refroidissement activé"] = "Mint · Cooling enabled",
  ["Mint · Refroidissement désactivé"] = "Mint · Cooling disabled",
  ["Désactiver / restaurer le profil"] = "Disable / restore power plan",
  ["Activer le refroidissement"] = "Enable cooling",
  ["Désactiver le refroidissement"] = "Disable cooling",
  ["Un profil sauvegardé peut être nettoyé. Votre profil actif sera conservé."] = "A saved power plan can be cleaned up. Your active plan will be kept.",
  ["Désactivez le refroidissement pour changer cette option."] = "Disable cooling to change this option.",
  ["Un profil Mint dédié, réutilisé à chaque activation."] = "A dedicated Mint power plan, reused each time cooling is enabled.",
  ["Les réglages CPU seront sauvegardés puis restaurés à la désactivation."] = "CPU settings will be saved and restored when cooling is disabled.",
  ["Modification du profil…"] = "Updating power plan…",
  ["Refroidissement activé."] = "Cooling enabled.",
  ["Refroidissement désactivé. Votre profil est restauré."] = "Cooling disabled. Your power plan is restored.",
  ["Échec : "] = "Failed: ",
  ["Mint · Modification impossible"] = "Mint · Unable to update",
  ["Relance annulée : "] = "Restart cancelled: ",
  ["Ouvrir Mint"] = "Open Mint",
  ["Mint · Prêt à rafraîchir"] = "Mint · Ready to cool",
  ["Mint est ouvert"] = "Mint is running",
  ["Mint reste dans la zone de notification. Cliquez sur la feuille pour gérer le refroidissement."] = "Mint runs in the notification area. Click the leaf to manage cooling.",
  ["Patientez pendant la modification du profil."] = "Please wait while the power plan is updated.",
  ["Diagnostic enregistré : "] = "Diagnostics saved: ",
  ["Désactivez le refroidissement avant de changer de profil."] = "Disable cooling before changing power plans.",
  ["Sauvegarde du profil invalide. Aucun réglage modifié."] = "Invalid power plan backup. No settings were changed.",
  ["Windows n’a pas retourné un profil valide."] = "Windows did not return a valid power plan.",
  ["Impossible de sauvegarder les réglages secteur et batterie du profil."] = "Unable to back up AC and battery power plan settings.",
  ["Les réglages CPU du profil ne peuvent pas être sauvegardés."] = "CPU power plan settings could not be backed up.",
  ["Restaurez d’abord le profil sauvegardé."] = "Restore the saved power plan first.",
  ["Le profil Mint est déjà actif. Choisissez un autre profil Windows avant de l’activer avec Mint."] = "The Mint power plan is already active. Select another Windows power plan before enabling it in Mint.",
  ["Windows a refusé la modification du profil. "] = "Windows refused to update the power plan. "
 };
}
