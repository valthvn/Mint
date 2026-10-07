# Mint

Mint est une petite application Windows qui reste dans la zone de notification. Elle affiche la température du CPU et permet de réduire sa consommation en un clic.

<p>
  <img src="docs/screenshots/dashboard.png" alt="Cadran de Mint avec la température, la fréquence et la charge du CPU" width="340">
  <img src="docs/screenshots/settings.png" alt="Paramètres de Mint : langue et choix du profil d'alimentation" width="340">
</p>

## Ce que fait Mint

Le cadran affiche la température CPU, le pic de la session, un historique sur deux minutes, la fréquence et la charge. Les températures du GPU et du stockage apparaissent dans les paramètres quand les capteurs les fournissent.

Le refroidissement limite le CPU à 99 % et désactive le boost, sur secteur comme sur batterie. Deux options sont disponibles :

- **Profil Mint** : crée un profil dédié et le réutilise aux prochaines activations.
- **Profil actuel** : sauvegarde les réglages du profil Windows en cours avant de les modifier.

Désactiver le refroidissement restaure le profil ou ses réglages. Masquer ou quitter l'application laisse le mode choisi actif. Il faut désactiver le refroidissement avant de changer d'option.

L'interface est en anglais au premier lancement. Le français est disponible dans les paramètres, sans redémarrage.

## Utilisation

Lancer `Mint.exe`, puis cliquer sur la feuille près de l'horloge pour ouvrir le panneau. Un clic droit donne accès aux actions rapides. Échap ou un clic en dehors du panneau le masque.

Les températures CPU nécessitent le pilote signé [PawnIO](https://pawnio.eu/) et peuvent demander une exécution en administrateur. Après l'installation du pilote, quitter et relancer Mint. Les paramètres permettent aussi d'exporter un diagnostic si un capteur reste indisponible.

Si la fréquence matérielle ne peut pas être lue, Mint utilise celle fournie par Windows. L'infobulle indique la source ; la valeur Windows peut différer d'une mesure directe des cœurs.

Mint ne pilote pas les ventilateurs. Le mode de refroidissement peut réduire les performances du processeur.

Les préférences et les sauvegardes sont enregistrées dans `%LOCALAPPDATA%\Mint`. Ne pas supprimer ces fichiers tant que le refroidissement est actif. Les données des versions précédentes sont reprises automatiquement.

## Compiler

Prérequis : Windows 64 bits et le [SDK .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
.\Build-Mint.ps1
```

Le script exécute les tests, puis publie l'application dans `artifacts/Mint`. Cette version inclut .NET : le SDK n'est pas nécessaire sur le PC qui l'utilise.

Pour lancer seulement les tests :

```powershell
.\Build-Mint.ps1 -TestOnly
```

La solution `Mint.sln` peut également être ouverte dans Visual Studio. Les sources sont dans `src/Mint` et les tests dans `tests/Mint.Tests`. Les tests des profils simulent `powercfg` et ne modifient pas les réglages Windows.

## Origine et licence

Mint reprend des bases de [Universal x86 Tuning Utility](https://github.com/JamesCJ60/Universal-x86-Tuning-Utility), de James C. Jones et de l'équipe UXTU. L'application, son interface et sa structure sont désormais maintenues sous le nom Mint.

Attribution d'origine : Copyright (C) 2024 James C.Jones.

Le projet est distribué sous [GNU GPLv3](LICENSE), sans garantie. La lecture des capteurs utilise [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor), sous [Mozilla Public License 2.0](LICENSE-LibreHardwareMonitor.txt).
