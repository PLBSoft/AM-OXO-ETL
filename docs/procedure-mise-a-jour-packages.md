# Procédure — Mise à jour des packages NuGet

> **Usage** : ce fichier est une trame réutilisable, au même titre que
> `etat-des-lieux-technique-TEMPLATE.md`. À chaque fois qu'une revue des
> dépendances est souhaitée (périodiquement, ou avant un jalon important),
> copier ce fichier tel quel comme prompt/consigne pour Claude Code.
>
> Ce document est un **document vivant** (catégorie 1 de
> `convention-nommage-documents.md`) : il décrit la procédure elle-même et
> n'est jamais daté ni modifié en place pour y accumuler un historique.
> Chaque exécution produit en sortie un **instantané daté** distinct
> (voir §5 ci-dessous), qui lui reste figé.

---

## 1. Contraintes non négociables (rappel)

Ces règles proviennent de CLAUDE.md et des principes établis du projet
AM-OXO-ETL. Elles s'appliquent à cette procédure sans exception :

- **Licence OSS stricte** : toute dépendance, nouvelle ou mise à jour, doit
  rester MIT ou Apache 2.0. Aucune dépendance commerciale, même en version
  d'essai ou "community edition" à limitation. En cas de doute sur une
  licence, ne pas mettre à jour le paquet concerné et le signaler dans le
  rapport plutôt que de trancher seul.
- **FluentAssertions bloqué en v7.x** : la v8+ est interdite (bascule vers
  une licence commerciale). Ne jamais monter ce paquet au-delà de la
  dernière version 7.x disponible, même si `dotnet list package --outdated`
  le signale comme obsolète.
- **Aucune modification de comportement métier.** Cette procédure ne touche
  qu'aux versions de dépendances (fichiers `.csproj`, éventuellement du code
  d'adaptation strictement nécessaire à la compatibilité d'API). Si une
  montée de version majeure implique un changement de comportement observable,
  s'arrêter et documenter l'écart dans le rapport plutôt que de l'absorber
  silencieusement.
- **`legacy/` hors périmètre.** `NewApiPingService` et
  `ExcelProcessingClientService` (+ leurs `.Tests`) sont en .NET Framework 4.8,
  un écosystème NuGet différent avec ses propres contraintes de compatibilité.
  Ne pas les inclure dans cette procédure ; ils font l'objet d'une revue
  séparée si besoin.
- **Base de référence obligatoire avant de commencer** : la suite de tests
  complète doit être verte avant toute mise à jour. Si ce n'est pas le cas,
  s'arrêter et le signaler — ne pas mettre à jour des packages sur une base
  déjà rouge.

---

## 2. Étapes

1. **Lister les paquets obsolètes** sur chaque projet `src/` et `tests/` de
   la solution `.NET 10` (hors `legacy/`) :
   ```
   dotnet list package --outdated
   ```
   sur chaque `.csproj`, ou globalement si `ExcelETL.slnx`/le fichier
   solution le permet.

2. **Classer chaque paquet obsolète** :
   - Montée mineure/patch → généralement sûre, à traiter en priorité.
   - Montée majeure → vérifier le changelog/les notes de version avant
     toute action ; identifier les breaking changes potentiels.
   - Paquet dont la nouvelle version change de licence ou dont la licence
     est incertaine → **ne pas mettre à jour**, lister dans le rapport en
     "laissé en l'état" avec la raison.

3. **Mettre à jour un paquet (ou un groupe cohérent de paquets liés, ex. les
   packages `Microsoft.EntityFrameworkCore.*` ensemble) à la fois.** Ne pas
   grouper des mises à jour sans lien entre elles dans un même commit/pas.

4. **Exécuter la suite de tests complète après chaque mise à jour** :
   ```
   dotnet test ExcelETL.slnx
   ```
   (Et séparément `ExcelETL.Hosting.Tests`, hors solution — voir
   `etat-avancement-global-2026-07-22.md` pour ce point connu.)

5. **En cas d'échec** : revenir en arrière sur ce paquet précis (rollback
   immédiat), documenter l'incompatibilité rencontrée dans le rapport, et
   passer au paquet suivant plutôt que de forcer une correction de code non
   sollicitée.

6. **Répéter** jusqu'à épuisement des paquets obsolètes traitables.

---

## 3. Hors périmètre explicite

- Toute modification de logique métier, même mineure, pour "faire passer"
  une montée de version majeure — si une adaptation de code est nécessaire,
  elle doit rester strictement mécanique (signature d'API, namespace
  renommé) et être signalée, pas silencieusement absorbée.
- Le projet `legacy/` (.NET Framework 4.8).
- L'ajout de toute nouvelle dépendance non demandée explicitement par
  ailleurs (cette procédure met à jour l'existant, n'en introduit pas de
  nouveau).
- La résolution du trou de solution connu (`ExcelETL.Hosting` et
  `ExcelProcessingClientService` absents du fichier solution) — hors sujet
  ici, déjà tracé ailleurs.

---

## 4. Note d'efficacité d'implémentation

- Commencer par les paquets les plus "en bas" de la Clean Architecture
  (`Domain` n'a aucune dépendance — rien à faire ici) puis remonter vers
  `Infrastructure`/`WebAPI`/`BlazorAdmin`, pour isoler plus facilement
  l'origine d'un test rouge.
- Grouper les paquets `Microsoft.*` d'une même famille (EF Core, ASP.NET
  Core) en un seul pas de mise à jour : ils sont généralement versionnés
  ensemble et tester paquet par paquet dans ce cas gaspillerait des cycles
  de test sans bénéfice de diagnostic.
- Ne pas relancer la suite de tests complète après un simple changement de
  patch version sur un paquet `Abstractions`-only (faible risque) si le
  temps presse — mais le mentionner explicitement dans le rapport plutôt que
  de le passer sous silence.

---

## 5. Sortie attendue

Un nouveau fichier **instantané daté** (catégorie 2), nommé :

```
rapport-mise-a-jour-packages-AAAA-MM-JJ.md
```

contenant :
- La liste des paquets mis à jour (nom, version avant → après).
- La liste des paquets volontairement laissés en l'état, avec la raison
  (licence, breaking change trop risqué, etc.).
- Le nombre de tests verts avant et après (ex. "613/613 avant, 613/613
  après" — ou le chiffre à jour au moment de l'exécution).
- Toute anomalie rencontrée et son traitement (rollback, contournement,
  signalement).

Ce rapport ne remplace pas ce document de procédure : il en est une trace
d'exécution, figée à sa date, jamais mise à jour a posteriori.
