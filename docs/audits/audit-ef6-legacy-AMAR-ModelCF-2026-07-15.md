# Audit modèle EF6 legacy — AMAR.ModelCF
 
> Audit factuel basé sur une lecture directe du code du repository `C:\AlphaMaintenance\AMAR.ModelCF` (et, ponctuellement, des projets siblings référencés — `AM.Shared`, `AMAR.Core`, `AMARImport` — quand nécessaire pour répondre à la section 5). Aucune supposition n'est faite sur la correspondance avec le vocabulaire métier du client sans preuve dans le code ; les cas non confirmés sont listés en section 7.
>
> Date de l'audit : 2026-07-15.
>
> Le glossaire technique (correspondance terme métier ↔ nom réel dans le code) a été extrait dans un fichier séparé : `glossaire-ef6-legacy-AMAR-ModelCF-2026-07-15.md`. Ce document évolue indépendamment de cet audit au fil des prochains échanges.
 
---
 
## 1. Vue d'ensemble du projet
 
### Structure des dossiers / namespaces
 
Le projet est un assembly .NET Framework 4.8 (`TargetFrameworkVersion=v4.8`), namespace racine `AMAR.ModelCF.Models` (sauf les migrations, en `AMAR.ModelCF.Migrations`). Arborescence :
 
```
AMAR.ModelCF/
├── Attributes/                 (1 fichier : validation attribute custom)
├── Migrations/                 (416 fichiers .cs/.Designer.cs/.resx — EF6 Code-First migrations)
├── Models/                     (~90 classes racine)
│   ├── GEDCND/                 (PVAnnulation — gestion électronique de documents / annulation)
│   ├── GeoTools/                (GTxxx — module cartographique "GeoTools" : coactivités, matériel fixe, plans)
│   ├── Inspection/               (ModelePV, ModelePVCellMapping — modèles de procès-verbaux)
│   ├── JobPack/                 (JobPack, Printer, PrinterLabel... — étiquettes/impression)
│   ├── Notifications/            (Notification, NotificationBaseElement)
│   ├── PGAZ/                    (module "prise gaz" — détecteurs de gaz, mesures, analyses)
│   ├── PID/                     (PID, AnimationPID — schémas P&ID et leur animation)
│   ├── PREP/                    (Gamme, TacheGamme, AppelOffre... — préparation de travaux / appels d'offres)
│   └── TacheMultiple/            (TacheMultiple, TypeTacheMultiple, ItemTacheMultiple, DataTacheMultiple*)
└── Properties/
```
 
Les modules `GEDCND`, `GeoTools`, `Inspection`, `JobPack`, `Notifications`, `PGAZ`, `PID`, `PREP` sont des domaines fonctionnels périphériques (cartographie, impression, gaz, appels d'offres...) qui ne recoupent pas directement le périmètre MAD/REL/BE/Point/TM demandé. Ils ne sont pas détaillés ici au-delà de ce qui touche aux entités centrales.
 
### DbContext
 
Un seul DbContext : **`AMARDbContext`** ([Models/AMARDbContext.cs](Models/AMARDbContext.cs)), défini comme :
 
```csharp
public partial class AMARDbContext : IdentityDbContext<AMARUser>, IAMARDbContext
```
 
- Hérite de `IdentityDbContext<AMARUser>` (ASP.NET Identity — gestion des utilisateurs/rôles est donc fusionnée dans la même base).
- Implémente une interface `IAMARDbContext` ([Models/IAMARDbContext.cs](Models/IAMARDbContext.cs)) qui expose la quasi-totalité des `DbSet<>` — probablement pour permettre l'injection de dépendance / le mock dans les couches supérieures (`AMAR.Core`, etc.).
- Expose **plus de 100 `DbSet<>`**, couvrant tout le domaine applicatif (pas seulement BE/Point/TM). Les noms de propriété ne suivent **pas** une convention unique : `BaseElementSet`, `pointSet` (minuscule), `BaseElements` (au pluriel, mais typé `DbSet<Accessoire>` et non `DbSet<BaseElement>` !), `ActionSet` (pour `PGAZAction`), `Frequences`, `Applications`... Extrait révélateur :
```csharp
public DbSet<Accessoire> BaseElements { get; set; }   // nom trompeur : ce n'est PAS le DbSet de BaseElement
...
public DbSet<BaseElement> BaseElementSet { get; set; } // le vrai DbSet générique de BaseElement
```
 
- Constructeurs multiples : `AMARDbContext()` (connection string nommée `"AMAREntities"`), `AMARDbContext(string connectionString)`, `AMARDbContext(DbConnection connection)`. Une méthode statique `Create()` retourne `new AMARDbContext()`.
- `SaveChanges()` / `SaveChangesAsync()` sont surchargées pour intercepter `DbEntityValidationException` et lever une exception détaillée listant les champs en échec de validation.
### Mode de configuration : Fluent API vs Data Annotations
 
**Mixte, avec une répartition claire des responsabilités** :
- La très grande majorité des entités utilisent des **Data Annotations** directement sur les classes (`[Key]`, `[Required]`, `[Index]`, `[ForeignKey]`, `[StringLength]`, `[Display]`, `[NotMapped]`...).
- La **Fluent API** (`OnModelCreating`, ~590 lignes) n'est utilisée que pour les cas que les Data Annotations ne savent pas exprimer : relations plusieurs-à-plusieurs avec table de jointure explicite et sans propriété de navigation croisée, désactivation globale de conventions, et quelques relations un-à-plusieurs à clé étrangère explicite avec contrôle du cascade delete.
Deux lignes de tête de `OnModelCreating` désactivent deux conventions globales EF6 :
 
```csharp
modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();
modelBuilder.Conventions.Remove<OneToManyCascadeDeleteConvention>();
```
 
(voir section 4 pour les conséquences).
 
### Stratégie de connexion
 
- Provider : **SQL Server** (`System.Data.SqlClient`), confirmé par `App.config` :
  ```xml
  <add name="AMAREntities" connectionString="Data Source=.\MSSQLSERVER2016;Initial Catalog=AM-VB2018;Integrated Security=SSPI" providerName="System.Data.SqlClient" />
  ```
  Le nom de connexion string conventionnel est **`AMAREntities`**. D'autres `App.config` du solution (ex. `AMAR.Cmd`) pointent vers d'autres bases (`WebMCP-Tests`, `WebMCP-develop2016`) sous le même nom — plusieurs environnements/bases historiques coexistent, aucun nom de base n'est donc "canonique".
- Le constructeur statique force le chargement de `EntityFramework.SqlServer.dll` (contournement d'un problème classique de copie de DLL en Code-First pur).
### Pattern Repository / Unit of Work
 
**Absent du projet `AMAR.ModelCF` lui-même** (ce projet ne contient que le modèle EF6 — pas de couche d'accès aux données). En revanche, un pattern Repository existe bel et bien dans des projets siblings de la même solution :
- `AM.DataLayer.Interfaces` (interfaces génériques : `IBaseRepository`, `IBaseIntRepository`, `IBaseGuidRepository`, `ISyncRepository`, `ITombstoneRepository`, `IMetaRepository`, `ITableauRepository`).
- `AMAR.Core.Interfaces/DataLayer` (interfaces spécifiques par entité — **143 interfaces `IxxxRepository`** au total dans la solution, ex. `IBaseElementRepository`, `IEquipementRepository`).
- Ces interfaces sont consommées par une couche métier `AMAR.Core/BusinessLayer` (voir section 5), qui elle-même s'appuie in fine sur `AMARDbContext`.
Il n'y a donc pas d'Unit of Work explicite identifié à ce niveau ; le pattern est Repository-par-entité au-dessus du DbContext.
 
---
 
## 2. Entités métier centrales
 
### MAD / REL — pas d'entité dédiée dans le modèle actuel
 
**Constat central et important pour le mapping ETL : il n'existe aucune classe/table nommée "MAD", "REL", "Dossier", "MiseADisposition" ou "RemiseEnLigne" en tant qu'entité de tête.** Ce que le client appelle "MAD"/"REL" ne correspond pas à un concept de premier niveau dans ce modèle de données — recherche exhaustive (`grep` insensible à la casse sur tout le repo) :
 
- Le seul champ portant littéralement "MiseADisposition" est **`Isolement.PositionALaMiseADisposition`** ([Models/Isolement.cs:45-50](Models/Isolement.cs)), une clé étrangère vers la table de référence `PositionPoseDepose` :
  ```csharp
  [Display(Name = "MADPosition", ResourceType = typeof(AM.i18n.PipelineIsolation))]
  [AMMapping]
  public PositionPoseDepose PositionALaMiseADisposition { get; set; }
  [ForeignKey("PositionALaMiseADisposition")]
  public int? PositionALaMiseADispositionId { get; set; }
  ```
  `Isolement` est une sous-classe de `BaseElement` (voir ci-dessous) portant les champs métier d'isolement de tuyauterie (LOTO). Elle possède 3 champs de position analogues : `PositionALaPose`, `PositionALaDepose`, `PositionALaMiseADisposition`, tous FK vers la même table `PositionPoseDepose` (simple table `Nom` unique).
  → **"MAD" = un état/position d'un point d'isolement au moment de la mise à disposition**, pas un objet "dossier" distinct.
- **Aucun champ "REL"/"RemiseEnLigne" équivalent n'existe aujourd'hui.**
- Historique éclairant (migrations) : jusqu'en 2021, `BaseElement` portait deux colonnes booléennes **`CorrespondanceLOTOMAD`** et **`CorrespondanceLOTOREL`**, supprimées par la migration [`202109211533596_RemoveLOTOMADAndLOTOREL.cs`](Migrations/202109211533596_RemoveLOTOMADAndLOTOREL.cs) :
  ```csharp
  public override void Up()
  {
      DropColumn("dbo.BaseElement", "CorrespondanceLOTOMAD");
      DropColumn("dbo.BaseElement", "CorrespondanceLOTOREL");
  }
  ```
  Ce sont donc bien les seules traces historiques de "MAD" et "REL" comme concepts explicites du modèle — remplacées depuis par le système `PositionPoseDepose` (2023, migration `IsolementPositionALaMiseADisposition`), qui ne couvre que MAD, pas REL.
**Conclusion pour l'ETL** : le nouveau microservice ne pourra probablement pas écrire "un dossier MAD" ou "un dossier REL" dans une table dédiée de ce modèle — ces notions, si elles doivent être représentées, devront vraisemblablement être portées par les champs existants de `BaseElement`/`Isolement`/`Point`/`TacheMultiple` (repère, positions, tâches), pas par une nouvelle entité de tête déjà prévue à cet effet. Voir section 7.
 
### BE (Base Élément) — une seule classe, hiérarchie TPH, auto-référence many-to-many
 
`BaseElement` ([Models/BaseElement.cs](Models/BaseElement.cs)) hérite de `SyncBase` et est **la seule classe "BE"** — il n'y a **pas** deux classes séparées "BE parent" et "BE enfant". La notion parent/enfant est modélisée comme une **relation many-to-many auto-référencée** sur cette unique classe :
 
```csharp
[JsonIgnore] public virtual ICollection<BaseElement> ChildSet { get; set; }
[JsonIgnore] [AMMapping] public virtual ICollection<BaseElement> ParentSet { get; set; }
```
 
configurée en Fluent API :
```csharp
modelBuilder.Entity<BaseElement>()
    .HasMany(b => b.ChildSet)
    .WithMany(c => c.ParentSet)
    .Map(m => { m.MapLeftKey("BaseElement_ID"); m.MapRightKey("BaseElement_ID1"); });
```
→ table de jointure **`dbo.BaseElementBaseElement`** (clé composite `BaseElement_ID`/`BaseElement_ID1`). **Un `BaseElement` peut donc avoir plusieurs parents et plusieurs enfants** (ce n'est pas un arbre strict à un seul parent).
 
**Mapping d'héritage : Table Per Hierarchy (TPH).** `BaseElement` a 4 sous-classes concrètes, qui partagent **toutes la même table SQL `dbo.BaseElement`**, distinguées par une colonne cachée `Discriminator` (`nvarchar(128)`, non nullable) ajoutée automatiquement par EF6 :
- `Accessoire` ([Models/Accessoire.cs](Models/Accessoire.cs)) — aucun champ propre.
- `Equipement` ([Models/Equipement.cs](Models/Equipement.cs)) — ajoute `ClasseTuyauterie`, `DateTravaux`.
- `Isolement` ([Models/Isolement.cs](Models/Isolement.cs)) — champs LOTO/isolement (positions pose/dépose/MAD, besoins grue/échafaudage/calorifuge/masque, risque amiante, brides...).
- `PointSingulier` ([Models/PointSingulier.cs](Models/PointSingulier.cs)) — champs de contrôle non destructif / soudure (CND RT, ressuage, tag soudure, diamètre soudure...) **+ un bloc de champs spécifiques au projet "YARA"** (`IndiceRevision`, `LeadPreparateur`, `Preparateur`, `PrioritePrepa`, `ReferenceAOTA`, `ReferenceScopeTA`, `ServiceDemandeur`...) — signe d'un modèle déjà personnalisé par client historique, à ne pas généraliser.
⚠️ **Piège de nommage à signaler explicitement** : `PointSingulier` est une **sous-classe de `BaseElement`** (un "BE"), **pas** une sous-classe ni une variante de la classe `Point` décrite plus bas. Les deux concepts ("Point" au sens ligne de tableau de signature, et "PointSingulier" au sens élément de tuyauterie inspecté) portent un nom proche mais n'ont aucun lien de code.
 
Champs clés de `BaseElement` (tous mappés sur la même table, donc **nullable** pour les sous-classes qui ne les utilisent pas) :
 
| Champ | Type | Contrainte |
|---|---|---|
| `ID` | int, identity | `[Key]` |
| `Repere` | string | `[Required]` — le "tag" |
| `NewRepere` | string | optionnel |
| `Designation` | string | optionnel |
| `TypeElementID` | int | FK requise vers `TypeElement` |
| `LocalisationId` | int | **non-nullable** depuis la migration `202201151624193_BaseElementLocalisationNonNullable` |
| `BaseElementCommunId` | int? | FK optionnelle vers `BaseElementCommun` |
| `Visible` | bool | défaut `true` |
| `IsReadOnly` | bool | + collection `ReadOnlyHistorySet` (voir `ElementReadOnlyHistory`) |
| `NumeroSAP`, `NumeroPID`, `NumeroIsometrique`, `NumeroChrono`, `NumeroSerieTag1/2`, `NumeroAvis` | string | identifiants externes |
| `Diametre`, `PressionService`, `TemperatureService`, `Fluide`, `Serie`, `SpecTuyauterie`, `Elevation` | — | caractéristiques tuyauterie |
| `TexteLibre` à `TexteLibre5` | string | champs libres génériques |
| `AvancementPlanning` | decimal | avancement calculé/stocké |
| `Recurrent` | bool | |
| `DatePrioriteDebut`/`Fin` | string (⚠ pas `DateTime`) | |
 
`BaseElementCommun` ([Models/BaseElementCommun.cs](Models/BaseElementCommun.cs)) : table séparée servant à **regrouper plusieurs `BaseElement`** sous un nom commun (`Nom`, unique), avec un flag `IsGroupSignature` — relation 1 (`BaseElementCommun`) → N (`BaseElement`).
 
`TypeElement` ([Models/TypeElement.cs](Models/TypeElement.cs)) : table de référence — `Code` (unique), `Nom` (unique), `CategorieId` (FK requise vers `Categorie`), `Visible`. **Les valeurs possibles ("type élément") ne sont pas codées en dur** : ce sont des lignes de données créées via l'administration applicative. `Categorie` porte elle-même `Couleur`, `Icone`, `ClasseMateriel` (enums/lookup), `Nom` unique.
 
`Application` ([Models/Application.cs](Models/Application.cs)) : table de référence liée en many-to-many à `BaseElement`, `TypeElement` et `TypePoint`. Représente les "modules"/domaines auxquels un élément participe. Le code d'import legacy `AMARImport/ImportEquipement.cs` construit dynamiquement des instances `Application` nommées `"FDT"`, `"GT"`, `"INSPECT"`, `"LOTO"`, `"MCP"`, `"SAT"`, `"LIST"` — **ce sont des exemples observés dans du code d'import, pas des valeurs figées dans le modèle** ; les valeurs réelles dépendent de la base cible.
 
### Points — entité distincte, pas une sous-classe de BaseElement
 
`Point` ([Models/Point.cs](Models/Point.cs)) hérite de `SyncBase` et représente **la cellule d'intersection (BaseElement × Colonne)** dans un tableau de suivi/signature — ce n'est **pas** un sous-type de `BaseElement`.
 
```csharp
[Index("IX_BaseElementIdAndColonneId", IsUnique = true, Order = 1)]
[ForeignKey("BaseElement")] public int BaseElementId { get; set; }
[Index("IX_BaseElementIdAndColonneId", IsUnique = true, Order = 2)]
[ForeignKey("Colonne")] public int ColonneId { get; set; }
```
 
→ **un seul `Point` possible par couple (BaseElement, Colonne)** (index unique composite). Champs : `Etat` (enum `PointEtat`), `Avancement` (decimal %), `Valide` (bool), `Auto` (bool), `TypeCreationPoint` (enum), `BlocageDeconsignationCommun`, `BlocagePriorite`, `IsNotified`/`DateNotified`, + collections `CommentaireSet`, `MediaSet`, `SignatureSet`.
 
`Colonne` ([Models/Colonne.cs](Models/Colonne.cs)) représente une **étape/colonne de workflow** (pas une "colonne Excel") : `Nom` (unique), `NomCourt`, `Position`, `Priorite`, `Poids`, `Mode` (enum `ModeColonne`), `Visible`, FK optionnelle vers `TypeTacheMultiple` (une colonne peut exiger la complétion d'une tâche multiple), FK requise vers `TypePoint`, styles LOTO (`StyleColonneLOTO*`), signataires (`SignataireSet` many-to-many), auto-référence many-to-many via `ColonneAssociation` (parent/enfant de colonnes).
 
`TypePoint` ([Models/TypePoint.cs](Models/TypePoint.cs)) : table de référence — `Abreviation` (unique) et `Nom` (unique), `SignataireUnique` (bool). Un commentaire dans le code prévient : *"il ne faut plus utiliser Abreviation comme identifiant [...] mais le Guid Système"* — signe d'une migration de convention en cours dans l'équipe legacy.
 
**Les libellés cités par le client ("PROLOCK VANNES", "CONTRÔLE ETANCHÉITÉS") n'apparaissent nulle part dans le code** (recherche exhaustive, voir section 7) : ce sont très probablement des **valeurs de données** de `Colonne.Nom` et/ou `TypePoint.Nom`, stockées en base et gérées via l'IHM d'administration — pas des identifiants de schéma.
 
Structures associées (probablement des vues/agrégats matérialisés, à confirmer) :
- `Tableau` ([Models/Tableau.cs](Models/Tableau.cs)) : `Code` (≤8 car., unique), `Nom` (unique), `Mode` (enum `ModeTableau`), `AccessRestriction` (enum).
- `ColonneTableau` : association `Colonne` ↔ `Tableau`, index unique composite (`ColonneId`,`TableauGuid`).
- `BaseElementTableau` : association `BaseElement` ↔ `Tableau`, index unique composite (`BaseElementId`,`TableauGuid`), avec des compteurs dénormalisés `PointCount`/`PointSignedCount`.
- `BaseElementTableauColonneTableau` : association ternaire (`BaseElementTableau` × `ColonneTableau`), index unique composite, compteurs `ChildPointCount`/`ChildPointSignedCount`.
### TM (Tâches Multiples) et PTM (type de tâche multiple)
 
Quatre classes coopèrent, toutes dans `Models/TacheMultiple/` :
 
1. **`TypeTacheMultiple`** ([TacheMultiple/TypeTacheMultiple.cs](Models/TacheMultiple/TypeTacheMultiple.cs)) = le **PTM** (type/modèle de tâche multiple). `Code`, `Nom`, `TypeValidation` (enum `TypeValidationTacheMultiple` — détermine quel champ de valeur de `TacheMultiple` fait foi), `RessourceId` (FK optionnelle), `IsSortable`, `BlockingOnBool`/`BlockingOnOKNotOkNa`/`BlockingOnMessage`, et un très grand nombre de paires couleur/libellé (`Background*`/`Color*`/`Lib*`) selon le mode de validation (bool, texte, OK/NOK, numérique physique, date d'opération) — centralisées par défaut dans la classe statique [`TypeTacheMultipleDefaults`](Models/TacheMultiple/TypeTacheMultipleDefaults.cs).
2. **`ItemTacheMultiple`** ([TacheMultiple/ItemTacheMultiple.cs](Models/TacheMultiple/ItemTacheMultiple.cs)) = définit un **champ de donnée** appartenant à un `TypeTacheMultiple` : `Nom`, `Cle` (champ clé), `IsValidation`, `Type` (enum `TypeItemTacheMultiple` : Bool/Text/DateTime/Integer/Decimal/Percentage/Ressource), `Position`, `Largeur` (grille Bootstrap 0-12), `Increment`/`UseIncrement`, FK vers `TypeTacheMultiple`.
3. **`TacheMultiple`** ([TacheMultiple/TacheMultiple.cs](Models/TacheMultiple/TacheMultiple.cs)) = **l'instance** de tâche multiple, rattachée à un `BaseElement` (`BaseElementId`, nullable) et à un `TypeTacheMultiple` (`TypeTacheMultipleGuid`, requis). Porte les valeurs "génériques" : `OkNotOkNa`, `Termine`, `Texte`, `ValeurNumeriquePhysiquePrevue`/`Realisee`, `DateOperation`, `Avancement`, `DateValidation`, `TypeCreationTacheMultiple` (⚠ `int` brut, ne réutilise pas l'enum `TypeCreationPoint`), `User` (FK `AMARUser`).
4. **`DataTacheMultiple`** ([TacheMultiple/DataTacheMultiple.cs](Models/TacheMultiple/DataTacheMultiple.cs)) = classe abstraite (**TPH**, table unique `dbo.DataTacheMultiple` + colonne `Discriminator`), une ligne par (`TacheMultiple`, `ItemTacheMultiple`) — 6 sous-classes concrètes, chacune avec une propriété `Value` de type différent, mappées sur des colonnes physiques partagées `Value`/`Value1`/`Value2`/`Value3`/`Value4` :
   - `DataTacheMultipleBool` → colonne `Value` (bit)
   - `DataTacheMultipleDateTime` → colonne `Value1` (datetime)
   - `DataTacheMultipleDecimal` → colonne `Value2` (decimal 18,2)
   - `DataTacheMultipleInteger` → colonne `Value3` (int)
   - `DataTacheMultipleText` → colonne `Value4` (string)
   - `DataTacheMultipleRessource` → **pas** de colonne `Value*` ; utilise une FK propre `RessourceId` vers `Ressource`.
**Aucune trace littérale** de "TM PROCEDURE MAD" / "TM PROCEDURE REL" dans le code (recherche exhaustive) : si cette distinction existe côté métier, elle est très probablement portée par la valeur de `TypeTacheMultiple.Code`/`Nom` (donnée en base), pas par un champ de schéma dédié.
 
### TRAVAUX COMPLET / TRAVAUX DETAIL
 
**Aucune classe ni table portant ces noms n'a été trouvée** dans l'ensemble de la solution (recherche insensible à la casse sur tous les `.cs`). Deux familles de classes contiennent le mot "Travaux" mais ne correspondent pas :
- `GTTravaux`, `GTTravauxTache`, `GTTravauxCoactivite`, `GTTypeTravaux` (module `GeoTools`) : configurent des **critères géographiques de coactivité/zones à risque** sur une carte — domaine différent (visualisation cartographique, pas suivi de travaux).
- Le module `PREP` (`Gamme`, `TacheGamme`, `TacheBibliotheque`, `AppelOffre`) modélise une **bibliothèque de tâches / appel d'offres** rattachée à un `BaseElement` — conceptuellement plus proche d'un "plan de travaux", mais sans terminologie "COMPLET"/"DETAIL" dans le code.
→ Correspondance **non établie**, à traiter en section 7.
 
### Profil d'import déjà présent côté legacy
 
Contrairement à l'hypothèse de départ ("peu probable"), **un mécanisme de profil d'import Excel existe bel et bien** et est activement maintenu (le dernier commit du repo, `#4342`, l'étend) :
 
- **`ProfilXLSX`** ([Models/ProfilXLSX.cs](Models/ProfilXLSX.cs)) : un profil nommé — `Nom`, `Entity` (nom libre de l'entité cible, ex. `"TachePlanning"`), `SheetName`, `LigneNomColonne`/`LigneLibelleColonne`/`LigneData` (numéros de lignes d'en-tête/libellés/données).
- **`ColonneXLSX`** ([Models/ColonneXLSX.cs](Models/ColonneXLSX.cs)) : une colonne de mapping par profil — `Position`, `Titre`, `Propriete` (chemin de propriété .NET cible), `TypeValueXLSX`/`TypeEntity`, `Cle` (champ clé), `Required`, `Import` (inclus/exclus), `Unique`, `SousPropriete`.
C'est **le concept existant le plus proche** de ce que le nouveau microservice devra produire. La liste des entités actuellement supportées par ce moteur est centralisée dans `AM.Shared/Constant/ProfilXLSXMapping.cs` (projet sibling) :
 
```
Accessoire, CalibrationCertificate, Categorie, ClasseMateriel, ColonneTableau, Commentaire,
ConfigurationPriseGaz, Couleur, ElementTuyauterie, Equipement, Forme, FrequenceByDay, GasDetector,
GTTypeMaterielFixe, Icone, ImportExcelAO, Isolement, JobPackEnfants, JobPackParent,
JobPackPointEnfant, JobPackPointParent, Ligne, LockPoint, Point, PointSingulier, PriseGaz,
Signataire, Tableau, TachePlanningExport, TachePlanningImport, TypeElement, TypeMesure
```
 
**Absence notable, importante pour l'ETL** : ni `BaseElement` générique, ni `TacheMultiple`, `ItemTacheMultiple`, `TypeTacheMultiple`, `DataTacheMultiple`, ni `BaseElementCommun` n'apparaissent dans cette liste. Le moteur d'import XLSX existant **ne sait pas importer de données de Tâches Multiples aujourd'hui** — seulement `Equipement`/`Isolement`/`PointSingulier`/`Accessoire` (les 4 sous-types concrets de BE) et `Point`.
 
---
 
## 3. Relations et cardinalités
 
| Relation | Cardinalité | Mécanisme | Contrainte |
|---|---|---|---|
| BaseElement ↔ BaseElement (parent/enfant) | **N ↔ N** (pas 1-N) | Table de jointure `dbo.BaseElementBaseElement`, clé composite | — |
| BaseElement (1) → Point (N) | 1-N | FK `Point.BaseElementId` | — |
| Colonne (1) → Point (N) | 1-N | FK `Point.ColonneId` | **Index unique composite** (BaseElementId, ColonneId) : 1 seul Point par (BE, Colonne) |
| BaseElement (1) → TacheMultiple (N) | 1-N, FK nullable | `TacheMultiple.BaseElementId` (int?) | — |
| TypeTacheMultiple (1) → TacheMultiple (N) | 1-N, FK requise | `TacheMultiple.TypeTacheMultipleGuid` | — |
| TypeTacheMultiple (1) → ItemTacheMultiple (N) | 1-N | `ItemTacheMultiple.TypeTacheMultipleGuid` | — |
| TacheMultiple × ItemTacheMultiple → DataTacheMultiple | chaque `DataTacheMultiple` référence les deux (`TacheMultipleGuid` + `ItemTacheMultipleGuid`) | PK = `Guid` propre | **Aucun index unique composite trouvé** sur (TacheMultipleGuid, ItemTacheMultipleGuid) dans le code lu — la garantie "une seule valeur par item par tâche" n'est pas visible au niveau schéma, à vérifier côté base réelle |
| Colonne (0..1) → TypeTacheMultiple | FK optionnelle | `Colonne.TypeTacheMultipleGuid` | une colonne de workflow peut exiger une TM spécifique |
| BaseElement (N) ↔ Lot (N) | N-N | Table `LotBaseElement` | Index unique composite (LotGuid, BaseElementId) |
| BaseElement (N) ↔ Application (N) | N-N | Table de jointure implicite EF | — |
| BaseElement (N) ↔ Gamme | **deux relations distinctes** : (a) FK simple optionnelle `Gamme.BaseElementId` ; (b) M2M `GammeConcernedSet`/`BaseElementConcernedSet` via table `GammeBaseElementConcerned` | ⚠️ deux mécanismes qui coexistent sans que leur différence fonctionnelle soit documentée dans le modèle | à clarifier avec l'équipe legacy si le microservice doit toucher les Gammes |
| BaseElement.LocalisationId | 1-N (BE→Localisation) | FK **requise** (non nullable depuis 2022) | tout BE doit avoir une localisation |
| BaseElement.TypeElementID | 1-N | FK **requise** | tout BE doit avoir un type |
| BaseElement.Repere | — | `[Required]` string | clé "métier" utilisée pour la recherche (`FindByRepere` dans le code d'import existant) — pas de contrainte d'unicité SQL trouvée dans le modèle lui-même (à vérifier en base) |
| TypeElement.Code / Nom | — | `[Index(IsUnique = true)]` sur les deux | |
| Categorie.Nom | — | unique | |
| TypePoint.Abreviation / Nom | — | unique sur les deux | |
| BaseElementCommun.Nom | — | unique | |
| Tableau.Code (≤8) / Nom | — | unique sur les deux | |
| Colonne.Nom | — | unique | |
| ColonneTableau (ColonneId, TableauGuid) | — | index unique composite | |
| BaseElementTableau (BaseElementId, TableauGuid) | — | index unique composite | |
| BaseElementTableauColonneTableau (BaseElementTableauGuid, ColonneTableauGuid) | — | index unique composite | |
| LockPoint | 4 FK requises vers BaseElement×2 et Colonne×2 | non-cascade explicite (`WillCascadeOnDelete(false)`) | validation custom empêchant un point de se verrouiller lui-même ([Attributes/LockedPointDifferentLockedByPointAttribute.cs](Attributes/LockedPointDifferentLockedByPointAttribute.cs)) |
 
**Champs obligatoires structurants pour l'ETL** (ce que le microservice devra impérativement fournir ou résoudre côté cible) : `BaseElement.Repere`, `BaseElement.TypeElementID` (→ nécessite un `TypeElement` existant ou à créer), `BaseElement.LocalisationId` (→ nécessite une `Localisation` résolue), `Discriminator` implicite (le type concret BE à créer : `Equipement`/`Isolement`/`PointSingulier`/`Accessoire`), `Point.BaseElementId` + `Point.ColonneId` (les deux requis, couple unique), `TacheMultiple.TypeTacheMultipleGuid` (requis).
 
---
 
## 4. Conventions de mapping et migrations
 
### Migrations Code-First
 
- Dossier `Migrations/`, **416 fichiers** (`.cs` + `.Designer.cs` + `.resx` par migration, sauf les plus anciennes qui n'ont que `.resx`/`.cs`).
- Nommage : `{yyyyMMddHHmmss}_{DescriptionPascalCase}.cs` (ex. `202606100946411_TypeTacheMultiple_Texte_Background_Color.cs`).
- L'historique visible commence à **`202105171111148_InitialMigration2005171309.cs`** (17/05/2021) — tout l'historique antérieur a été "squashé" dans cette migration initiale unique, qui crée l'intégralité du schéma alors existant.
- **`AutomaticMigrationsEnabled = false`** ([Migrations/Configuration.cs](Migrations/Configuration.cs)) : migrations générées et appliquées manuellement (workflow `Add-Migration` / `Update-Database` classique EF6), pas de migration automatique en prod. `Seed()` est vide — **aucune donnée de référence n'est semée par le code**.
- Les migrations les plus récentes au moment de l'audit datent de juin 2026 (`202606100946411_...`) — développement actif et continu du schéma (une bonne dizaine de migrations sur les 2 derniers mois avant l'audit, portant surtout sur `TypeTacheMultiple`/`Colonne`/`Tableau`).
### Conventions de nommage table / colonne
 
- **Table = nom de classe, singulier** (convention `PluralizingTableNameConvention` explicitement désactivée) : `dbo.BaseElement`, `dbo.Point`, `dbo.TacheMultiple`, `dbo.DataTacheMultiple`... **Les noms de propriété `DbSet<>` dans `AMARDbContext` ne reflètent pas forcément ce nom** (voir section 1 — `BaseElements` ≠ table `BaseElement`).
- **Aucune convention unique de nommage de clé primaire** à travers le modèle : `ID` (`BaseElement`, `Colonne`), `Id` (`Categorie` via `ID` aussi en fait, `BaseElementCommun.Id`), `Guid` (classes héritant de `SyncBaseGuid`), `XxxId` explicite (`TypePointId`, `ProfilXLSXId`, `LocalisationId`...). Un même modèle mélange donc PK entière auto-incrémentée et PK `Guid`.
- Deux classes de base pour la "synchronisation" : `SyncBase` (PK `int` implicite via `[Key]` ailleurs sur la classe dérivée + `Guid` de sync, `SyncID` optionnel) et `SyncBaseGuid` (PK = `Guid`, + `DateCreated`/`DateModified`). Le choix entre les deux ne suit pas de règle explicite documentée dans le code — semble être un choix fait au cas par cas selon l'entité.
### Cascade delete
 
`OneToManyCascadeDeleteConvention` est retirée globalement → **aucune suppression en cascade par défaut**, chaque relation un-à-plusieurs configurée explicitement en Fluent API le fait avec `.WillCascadeOnDelete(false)` (toutes les occurrences trouvées dans `OnModelCreating` : `GTCoactivite.Ressource1/2`, `PhaseTache1/2`, `Couple1/2` ; `TacheGammeTacheGamme` ×3 ; `LockPoint` ×4). **Aucune occurrence de `WillCascadeOnDelete(true)` trouvée** dans le code Fluent explicite — le modèle est conçu pour éviter les suppressions en cascade involontaires.
 
### Héritage TPH (Table Per Hierarchy)
 
Deux hiérarchies utilisent TPH avec colonne `Discriminator` (`nvarchar(128)`) :
1. `BaseElement` → `Accessoire`/`Equipement`/`Isolement`/`PointSingulier`, toutes dans `dbo.BaseElement`.
2. `DataTacheMultiple` → 6 sous-classes, toutes dans `dbo.DataTacheMultiple`.
Conséquence pratique : la table `dbo.BaseElement` est **très large** (~90 colonnes visibles dans la migration initiale) et la plupart des colonnes sont **nullables** car spécifiques à un seul sous-type.
 
### Enums (valeurs contrôlées "en dur" dans le code, définies dans le projet sibling `AM.Shared/Enum`)
 
| Enum | Valeurs | Utilisé par |
|---|---|---|
| `PointEtat` | NonSignePlanningPartiel=0, NonSignePlanningComplet=1, SignePlanningPartiel=2, SignePlanningComplet=3, Refused=4, SansDroits=5, SansEnfants=6 | `Point.Etat` |
| `TypeCreationPoint` | Manuel=0, AutoImportTP=1, ManualBatchCreation=2 | `Point.TypeCreationPoint` |
| `TypeValidationTacheMultiple` | Pourcentage=1, Non_terminé_terminé=2, Ok_NotOK_NA=3, Media=5, Physique=6, Inventaire=7, DateOperation=8, Texte=9 (⚠ valeur 4 absente — probablement supprimée historiquement) | `TypeTacheMultiple.TypeValidation` |
| `TypeItemTacheMultiple` | Bool=10, Text=20, DateTime=30, Integer=40, Decimal=50, Percentage=60, Ressource=70 | `ItemTacheMultiple.Type` |
| `OKNotOkNa` | NonRenseigné=0, OK=1, NOTOK=2, NA=3 | `TacheMultiple.OkNotOkNa` |
 
### Tables de référence "ouvertes" (valeurs contrôlées mais gérées comme des DONNÉES, pas des enums)
 
`TypeElement`, `TypePoint`, `Categorie`, `Application`, `PositionPoseDepose`, `TypeTacheMultiple`, `Colonne`, `ClasseMateriel`, `Loc1`/`Loc2`/`Loc3` : ce sont des **tables** alimentées via l'IHM d'administration, pas des listes figées dans le code C#. **Le nouveau microservice ETL ne peut donc pas supposer une liste fixe de "types élément"/"types de point"/"types de PTM" — ces valeurs doivent être récupérées depuis la base cible réelle (ou fournies par le métier), pas déduites du code.**
 
### Attributs custom transverses (`AM.Shared.Attributes`, projet sibling)
 
- `[AMImportable]` (sur la classe) : *"Indique au moteur d'import export qu'il faut proposer cette classe"* — marque les entités éligibles au moteur d'import/export générique.
- `[AMMapping]` (sur la propriété) : *"Indique [...] qu'il s'agit d'une propriété à proposer dans le mapping import/export [et] dans les colonnes propriétés"* — marque les champs proposés dans l'UI de mapping.
Ces deux attributs sont posés de façon large sur la plupart des entités du domaine central (`BaseElement`, `BaseElementCommun`, `Equipement`, `Isolement` via héritage, `TypeElement`, `Categorie`, `Application`, `Colonne` (commenté, désactivé — `//[AMImportable]`), `Tableau`, `ColonneTableau`, `BaseElementTableau`) — ils constituent un signal fiable de "ce que le moteur d'import legacy considère comme important", même quand l'entité n'apparaît pas encore dans `ProfilXLSXMapping` (section 2). **Notablement, `TacheMultiple` porte `[AMImportable]` mais son nom n'apparaît pas dans `ProfilXLSXMapping`** — signe d'un import prévu/en cours de développement mais pas encore branché.
 
---
 
## 5. Points d'intégration déjà existants
 
### `NewApiPingService` / `ExcelProcessingClientService`
 
Recherche exhaustive (`grep` récursif) sur l'ensemble des projets présents sur la machine (`C:\AlphaMaintenance\*`, hors `bin`/`obj`) : **aucune occurrence de ces deux noms n'a été trouvée dans le code source**. Soit ils n'existent pas dans ce solution, soit ils vivent dans un dépôt non présent sur cette machine, soit ils ont été renommés. Impossible de les décrire — voir section 7.
 
### Mécanisme d'import existant : deux chemins de code coexistent
 
**1. `AMARImport/ImportEquipement.cs`** (projet sibling `AMARImport`) : un importeur ClosedXML "à la main", lisant deux feuilles (`"TYPE EQPT"`, `"LISTE EQPT"`) et construisant des `Equipement`/`TypeElement`/`Categorie`/`Localisation` **en mémoire, dans des `Dictionary<string, T>`**. Le commentaire de tête de la classe le dit explicitement : *"Le résultat de l'import est stocké dans des dictionnaires, pas dans la base de données. C'était une tentative pour essayer de ne pas faire grossir la classe AmarManager."* → ce chemin **n'écrit pas directement en base** ; semble être un outil ponctuel ou une étape intermédiaire, pas le pipeline de référence.
 
**2. `AMAR.Core/BusinessLayer/ImportExport/*`** (projet sibling `AMAR.Core`) : famille de services `ImportExportXxxService<TEntity> : ImportExportService<TEntity>`, un par entité (`ImportExportBaseElementService`, `ImportExportEquipementService`, `ImportExportCategorieService`, `ImportExportClasseMaterielService`, `ImportExportCommentaireService`, `ImportExportCouleurService`, `ImportExportGTTypeMaterielFixeService`, `ImportExportIconeService`, `ImportExportJobPack*Service` ×4, `ImportExportLoc1/2/3Service`, `ImportExportLocalisationService`, `ImportExportPositionPoseDeposeService`, `ImportExportTachePlanningService`, `ImportExportTypeElementService`, `ImportLockPointService`). Chaîne de code :
 
```
Fichier XLSX (ClosedXML, IXLRow)
  → ImportExportXxxService<TEntity>          (AMAR.Core.BusinessLayer.ImportExport)
      - piloté par ProfilXLSX / ColonneXLSX chargés via IProfilXLSXService
  → IXxxRepository                            (AMAR.Core.Interfaces.DataLayer / AM.DataLayer.Interfaces)
  → AMARDbContext                             (AMAR.ModelCF)
```
 
Exemple représentatif (`ImportExportBaseElementService`, [AMAR.Core/BusinessLayer/ImportExport/ImportExportBaseElementService.cs](../AMAR.Core/BusinessLayer/ImportExport/ImportExportBaseElementService.cs)) :
 
```csharp
internal class ImportExportBaseElementService : ImportExportService<BaseElement>, IImportExportBaseElementService
{
    private readonly IBaseElementRepository _baseElementRepository;
    ...
    public override Task<List<ResultViewModel>> ImportAsync(string fullPath)
    {
        throw new NotImplementedException("A développer en moteur v3 (utilise le moteur v2).");
    }
}
```
 
⚠️ **Point important** : plusieurs méthodes-clés (`ImportAsync`, `CreateEntity`, `Get`) de ces services **lèvent `NotImplementedException`** avec le commentaire *"A développer en moteur v3 (utilise le moteur v2)"* — signe que le moteur d'import est **en cours de refonte (v2 → v3)** au moment de l'audit, et que le chemin d'exécution réellement actif ("v2") n'a pas été localisé dans ce périmètre d'audit (probablement ailleurs dans `AMAR.Core` ou dans le contrôleur MVC appelant). **Il n'existe donc pas de pipeline unique, complet et stable à imiter tel quel.**
 
### Conséquence directe pour le microservice ETL
 
Le contrat le plus proche et réutilisable aujourd'hui est le **couple `ProfilXLSX`/`ColonneXLSX`** (nom de profil, feuille, lignes d'en-tête, et par colonne : titre / propriété cible / type / requis / clé / unique). Mais la liste des entités qu'il couvre actuellement (`AM.Shared.Constant.ProfilXLSXMapping`) **ne comprend pas** `BaseElement` générique, `TacheMultiple`, `ItemTacheMultiple`, `TypeTacheMultiple`, `DataTacheMultiple`, ni `BaseElementCommun` — précisément les concepts au cœur de la description client (MAD/REL/TM/PTM). Étendre cette liste (ou construire un chemin d'écriture parallèle direct vers `AMARDbContext`/les repositories) sera probablement nécessaire.
 
---
 
## 6. Non couvert / incertain
 
- **MAD vs REL comme "dossiers" distincts** : non tranchable depuis le code seul. Le modèle actuel ne porte qu'une notion de *position* (`Isolement.PositionALaMiseADisposition`, FK vers `PositionPoseDepose`) ; aucun équivalent "REL" actuel ; les anciens booléens `CorrespondanceLOTOMAD`/`CorrespondanceLOTOREL` ont été supprimés en 2021. Si "MAD"/"REL" doivent devenir des concepts de premier niveau pour l'ETL, cela suppose une extension du modèle qui n'existe pas encore.
- **"TRAVAUX COMPLET" / "TRAVAUX DETAIL"** : aucune correspondance de code trouvée. `GTTravaux*` (module GeoTools) et `Gamme`/`TacheGamme` (module PREP) sont les seuls candidats "proches" par le nom, mais rien ne confirme un lien avec la terminologie du client.
- **"PROLOCK VANNES", "CONTRÔLE ETANCHÉITÉS", "TM PROCEDURE MAD", "TM PROCEDURE REL"** : zéro occurrence littérale dans tout le code source (y compris les ressources `.resx` des migrations). Ce sont très probablement des valeurs de données (`Colonne.Nom`, `TypePoint.Nom`, `TypeTacheMultiple.Code`/`Nom`) présentes uniquement dans la base SQL Server réelle — à confirmer par une requête directe sur la base cible, pas déductible du code.
- **`NewApiPingService` / `ExcelProcessingClientService`** : introuvables dans l'ensemble du solution présent sur la machine. Impossible de décrire leur éventuel usage du modèle EF6.
- **Effectivité réelle du cascade delete** : seules les relations Fluent API explicites de `OnModelCreating` ont été vérifiées (toutes en `WillCascadeOnDelete(false)`). Le comportement des relations définies uniquement par convention/Data Annotations (FK simples un-à-plusieurs sans configuration Fluent explicite) n'a pas été vérifié contre le SQL généré pour chacune d'entre elles.
- **Logique métier hors modèle de données** : le calcul de `Point.Etat`, la façon dont `TacheMultiple.Avancement` remonte vers `BaseElement.AvancementPlanning`, les déclencheurs d'écriture dans `ElementReadOnlyHistory`, et plus généralement toute règle de validation/orchestration vivent probablement dans les contrôleurs MVC ou dans `AMAR.Core` (hors du périmètre de ce modèle de données) — non auditées ici.
- **État réel du moteur d'import "v2" vs "v3"** : plusieurs services `ImportExportXxxService` ont des méthodes non implémentées renvoyant vers un "moteur v2" non localisé dans cet audit. Le chemin de code réellement exécuté en production pour l'import Excel actuel n'a pas été confirmé de bout en bout.
- **Différence fonctionnelle entre les deux relations BaseElement↔Gamme** (`Gamme.BaseElementId` simple vs `GammeBaseElementConcerned` many-to-many) : non documentée dans le modèle, non tracée dans le code métier/UI.
- **Unicité du couple (TacheMultipleGuid, ItemTacheMultipleGuid) sur `DataTacheMultiple`** : aucun index unique composite trouvé dans le code lu pour garantir l'unicité logique "une valeur par item par tâche" — à vérifier directement sur le schéma SQL Server réel.
- **Unicité de `BaseElement.Repere`** : utilisé comme clé de recherche métier dans le code existant, mais aucune contrainte `[Index(IsUnique = true)]` n'a été trouvée sur ce champ dans `BaseElement.cs` — à vérifier en base (risque de doublons si non contraint).