# Mint

Application Windows compacte pour surveiller la température du processeur et activer un mode de refroidissement depuis la zone de notification.

## Nouveautés 1.1

- Anglais par défaut ; choix **English / Français** dans les paramètres, appliqué immédiatement et mémorisé au redémarrage.
- Détection du pilote PawnIO manquant, avec lien d'installation officiel dans les paramètres.
- Fréquence de secours fournie par Windows lorsque la mesure des cœurs est indisponible. L'infobulle précise la source ; cette valeur Windows peut différer d'une mesure matérielle du boost.
- Export du diagnostic des capteurs depuis les paramètres. Les valeurs CPU nulles, non finies ou à zéro ne sont pas présentées comme des mesures valides.
- Historique thermique centré avec une échelle adaptée aux mesures et deux repères discrets ; choix des profils sous forme de cartes. Les clics ne laissent plus de contour jaune, et le focus clavier garde un repère couleur menthe.

## Fonctionnalités

- Température CPU, pic de session, historique des deux dernières minutes, fréquence et charge.
- Températures GPU et stockage dans les paramètres, lorsque les capteurs sont disponibles.
- Panneau français avec jauge, transitions discrètes et accès animé aux paramètres sans barre de défilement visible.
- Deux modes d'alimentation : profil Mint dédié et réutilisable, ou modification du profil actif avec sauvegarde des valeurs d'origine.
- Limite maximale du CPU à 99 % et boost désactivé, sur secteur et batterie.
- Restauration du profil ou de ses réglages à la désactivation, y compris après un redémarrage de Mint.

Le choix du mode est mémorisé. Désactiver le refroidissement avant de changer cette option. Masquer ou quitter Mint conserve le mode d'alimentation choisi. Les autres profils sélectionnés manuellement dans Windows restent actifs lors de la restauration.

## Compiler

Prérequis : Windows et SDK .NET 10 avec prise en charge WPF.

```powershell
dotnet build Mint.sln -c Release
dotnet run --project Camomille.Tests -c Release
dotnet publish "Universal x86 Tuning Utility/Universal x86 Tuning Utility.csproj" -c Release -r win-x64 --self-contained true -o artifacts/Mint
```

Ou exécuter `Build-Mint.ps1`. La version publiée inclut .NET ; le SDK n'est pas nécessaire pour l'utiliser. Lancer `artifacts/Mint/Mint.exe`.

## Utilisation

Mint démarre dans la zone de notification. Un clic gauche sur la feuille ouvre ou masque le panneau. Le clic droit propose l'ouverture, le refroidissement et la fermeture de l'application. Échap ou un clic ailleurs masque le panneau.

La lecture des températures CPU par LibreHardwareMonitor 0.9.6 nécessite le pilote signé [PawnIO](https://pawnio.eu/), même si Mint est lancé en administrateur. L'installer depuis le site officiel puis quitter et relancer Mint. Si les températures restent indisponibles, utiliser **Export sensor diagnostics / Exporter le diagnostic des capteurs** ; Mint ne remplace pas une température manquante par une valeur simulée.

Dans les paramètres, choisir **Utiliser le profil Mint** ou **Modifier le profil actuel**, puis activer le refroidissement avec le bouton principal. L'application ne change pas le profil d'alimentation au démarrage.

Les sauvegardes sont stockées dans `%LOCALAPPDATA%/CamomilleReborn/`. Ne pas les supprimer avant d'avoir désactivé le refroidissement. Les sauvegardes des versions précédentes restent compatibles.

Les limites CPU peuvent réduire les performances. Mint ne contrôle pas directement les ventilateurs et ne réalise ni undervolting ni overclocking. Les capteurs indisponibles sont signalés ; les valeurs ne sont pas simulées. Une relance en administrateur est proposée dans les paramètres.

## Tests

Les tests simulent `powercfg` et ne modifient pas les réglages de l'ordinateur. Ils couvrent les deux modes, les valeurs secteur et batterie, la réutilisation du profil dédié, la persistance du choix, la restauration après redémarrage, les modifications externes, les échecs partiels, la récupération et les anciennes sauvegardes.

## Origine et licence

Projet issu de [Universal x86 Tuning Utility](https://github.com/JamesCJ60/Universal-x86-Tuning-Utility), UXTU Team / JamesCJ60, distribué sous GNU GPL v3 ; voir `LICENSE`. Ce dépôt contient les sources utilisées par Mint. Les noms de dossier historiques sont conservés pour la compatibilité de la solution.

Les mesures matérielles utilisent [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor). Sa licence est fournie dans `LICENSE-LibreHardwareMonitor.txt` ; les avis des dépendances sont également distribués par leurs packages.

Mint est indépendant d'Outbyte Camomile et n'utilise ni son code ni ses ressources graphiques.
