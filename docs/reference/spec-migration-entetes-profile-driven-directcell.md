# Spec de conception — migration des cellules d'en-tête vers le profil (`DirectCell` profile-driven)

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`, catégorie 1).
Conception figée le 27/07 avec Simon (§2). Complète `modele-domaine-import-profile.md` et
`spec-extraction-fichier-source-oxo.md`. Sert de référence aux lots 047 (moteur) et 048 (UI).*

## 1. Problème

Le principe directeur du projet est « profile-driven, jamais de règle d'extraction codée en dur
dans le code du service ». Plusieurs lectures de cellules d'en-tête violent aujourd'hui ce principe :
leurs **coordonnées** et une partie de leur **transformation** sont codées en dur dans les services
Application (audit qualité Domain/Application du 25/07, `modele-domaine-import-profile.md` §2) :

- **PROCEDURE** (`ProcedureExtractionService`) : `nomMAD` lu en `M2:O2` puis retrait du préfixe
  repère ; `Designation` construite en `"Rév {P2:Q2} du {R2:T2}"` avec la date reformatée en
  `dd/MM/yyyy`. Coordonnées `M2:O2`, `P2:Q2`, `R2:T2` en dur.
- **AUTRES JOINTS TOUCHES / DIVERS** : écho de repère lu en `N6` — coordonnée en dur.

La primitive Domain prévue pour porter ce genre de lecture, **`DirectCell(sheet, range)`**, existe
mais n'est **jamais construite** hors de ses propres tests (audit Domain du 25/07 §3.1) : les
services court-circuitent le profil en lisant des plages codées en dur.

## 2. Décisions actées (Simon, 27/07)

1. **Profile-driven, on câble `DirectCell`.** Les coordonnées d'en-tête passent dans le profil (seed
   inclus) ; les services les lisent depuis le profil actif. `DirectCell` est conservé et enfin
   utilisé (fin de son statut de code mort).
2. **Rattachement par feuille.** Les règles d'en-tête vivent sur `SheetExtractionRule` (une par
   feuille), à côté du `RepeatingBlockLocator` existant — jamais dans une liste transverse au niveau
   `ImportProfile`. Raison : `M2:O2` n'a de sens que sur PROCEDURE, `N6` que sur AJT/DIVERS ; rester
   par-feuille est cohérent avec tout le reste du modèle et évite d'embrouiller l'utilisateur final.
3. **Transformation portée par le profil, mais en modèle PLAT** (pas l'arbre `TextTransform`
   récursif). Le client a explicitement demandé de pouvoir tout configurer sans développeur — donc
   gabarit et format de date doivent être éditables dans le profil. Mais on refuse l'usine à gaz :
   pas de persistance d'arbre `Concat`/`Literal`/`FieldRef` récursif. À la place, deux
   représentations plates, toutes en colonnes texte, triviales à persister et à éditer (voir §3).
4. **Configurable côté client + seedé.** Le seed fournit un modèle de départ (exemple fonctionnel) ;
   l'édition dans l'éditeur de profil Blazor rend la promesse « configurable sans développeur »
   réelle. L'édition n'est donc pas optionnelle : c'est le livrable du lot 048.
5. **Invariant de non-régression absolu.** Les 3 fixtures réelles (C7401, D8570, G6306B) produisent
   un résultat d'extraction **identique** avant/après. Ce chantier ne change aucune valeur extraite —
   il déplace l'origine de la configuration (code → profil).

## 3. Modèle retenu (plat)

Sur chaque `SheetExtractionRule`, deux petites collections nouvelles :

- **Champs directs** — un `HeaderFieldRule` par cellule d'en-tête lue :
  - `Name` (identifiant logique du champ, ex. `nomMAD`, `revision`, `dateRev`, `repereEcho`)
  - `Cell` : un `DirectCell(sheet, range)` (la coordonnée, enfin portée par le profil)
  - `StripReperePrefix` (bool, défaut `false`) : si vrai, retire le `ReperePrefix` du profil de la
    valeur lue (couvre `nomMAD`) — réutilise le préfixe déjà paramétré, pas une nouvelle valeur.
  - `DateFormat` (`string?`, défaut `null`) : si renseigné, la valeur lue (supposée être une date)
    est reformatée selon ce pattern .NET (ex. `dd/MM/yyyy`) — couvre `dateRev`.
- **Champs composés** — un `HeaderCompositeRule` par champ dérivé d'un gabarit :
  - `Name` (ex. `Designation`)
  - `Template` : un gabarit texte à placeholders nommés, ex. `"Rév {revision} du {dateRev}"`, où
    chaque `{placeholder}` référence le `Name` d'un `HeaderFieldRule` de la même feuille.

Volontairement **limité** à ces deux besoins concrets (retrait de préfixe, format de date, gabarit).
On n'introduit **pas** de système de transformation général : YAGNI, et c'est ce qui garde le modèle
éditable simplement. Toute transformation supplémentaire future se décidera au cas par cas.

**Conséquence assumée** : l'arbre `TextTransform` récursif (`Concat`/`Literal`/`FieldRef`/
`SubstringAfter`/`RawValue`) n'est **plus utilisé** par ce modèle. Il n'est pas supprimé par les
lots 047/048 (pour ne pas élargir leur périmètre) ; son éventuel retrait sera un nettoyage séparé,
une fois confirmé qu'aucun autre usage ne subsiste — même logique d'arbitrage que pour `DirectCell`.

## 4. Découpage en lots

- **Lot 047 — moteur (headless).** Domaine (`HeaderFieldRule`/`HeaderCompositeRule` sur
  `SheetExtractionRule`) + résolveur d'en-tête (Application) + persistance EF Core + migration +
  extension du `DefaultProfileSeeder` avec les valeurs actuelles + bascule des services (PROCEDURE,
  AJT/DIVERS) pour lire depuis le profil + non-régression des 3 fixtures. **Aucune UI.** À l'issue de
  047, le comportement est identique mais la configuration vient du profil seedé — le POC reste
  déployable.
- **Lot 048 — édition Blazor.** Édition des règles d'en-tête (champs directs + gabarits) dans
  `ImportProfileEditor.razor` et ses sous-formulaires : IDs stables, tests bUnit, resx, parité de
  patron avec les sous-formulaires existants. Dépend du modèle stabilisé par 047.

## 5. Points à confirmer en investigation (lot 047)

- **Nature réelle de la cellule de date** (`R2:T2`) : vraie date Excel (`DateTime`) que l'on reformate,
  ou texte déjà formaté ? Ce chantier ne configure que le format de **sortie** (`DateFormat`) ; le
  parsing d'un format d'**entrée** est hors périmètre (YAGNI) et n'est acceptable que si la source est
  bien une date. À confirmer avant de figer le comportement de `DateFormat`.
- **Valeurs de non-régression** : relever, depuis les tests d'intégration existants sur les 3
  fixtures, les valeurs exactes attendues de `nomMAD`, `Designation` et de l'écho de repère, pour
  verrouiller les assertions de non-régression.

## 6. Hors périmètre / non retenu

- Système de transformation général / arbre `TextTransform` récursif persisté — **explicitement
  écarté** au profit du modèle plat (§3).
- Suppression des types `TextTransform` devenus inutilisés — nettoyage séparé, pas dans 047/048.
- Parsing d'un format de date d'**entrée** — seul le format de sortie est configurable.
- Le mapping d'alias `MapTypeTacheMultipleAlias` (`"MAD"→"TM_PROC_MAD"`, audit Application §2) — autre
  hardcode, distinct, non couvert ici sauf décision séparée.
- Toute modification des valeurs extraites — invariant de non-régression (§2.5).
