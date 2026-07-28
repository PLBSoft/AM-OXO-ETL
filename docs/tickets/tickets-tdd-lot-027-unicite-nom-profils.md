# Tickets TDD — Lot 027 : unicité du nom des profils d'import/export

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Déclenché par
une capture d'écran client (`/export-profiles`, 24/07) montrant 3 profils nommés à l'identique
`"Profil OXO standard (Copie)"`, produits par le bouton Dupliquer sans contrôle d'unicité — voir
Lot J1/F1 pour le comportement actuel du bouton. Décision d'architecture actée en chat : le nom
identifie le profil pour l'utilisateur humain, il doit donc être unique, côté `ImportProfile`
**et** `ExportProfile` symétriquement.*

**Décisions actées avec Simon** :
- Comparaison **normalisée** : `Trim()` + insensible à la casse (`OrdinalIgnoreCase` côté .NET,
  collation insensible à la casse côté index SQL Server).
- Bouton Dupliquer : collision résolue par **suffixe auto-incrémenté** (`"X (Copie)"` →
  `"X (Copie 2)"` → `"X (Copie 3)"`...), jamais de blocage silencieux ni de renommage manuel
  imposé à l'utilisateur pour ce cas précis.
- Double barrière : contrainte DB (défense en profondeur, races conditions) + vérification
  explicite dans le Store (message métier localisé, expérience utilisateur correcte).
- **Longueur max du nom : 60 caractères** (`ImportProfile`/`ExportProfile`), profitant de la
  modification du modèle pour ce lot. Contrairement à l'unicité, c'est un invariant intrinsèque à
  l'entité (ne dépend pas de l'état du Store) : validé dans le **constructeur Domain**, même
  emplacement et même mécanisme que la validation "nom vide" déjà en place (voir Lot A3/F1/J2),
  pas dans le Store comme 27.1-27.4.

**Hors périmètre explicite** :
- Pas de validation d'unicité au niveau du constructeur Domain (`ImportProfile`/`ExportProfile`) —
  ce n'est pas un invariant intrinsèque à l'entité, il dépend de l'état du Store, donc la
  responsabilité vit en Application/Infrastructure, pas en Domain.
- Pas de renommage automatique proposé à l'utilisateur en cas de collision sur Créer/Modifier
  (seul le cas Dupliquer bénéficie de l'auto-incrémentation ; sur Créer/Modifier, l'utilisateur
  choisit lui-même un autre nom après le message d'erreur).
- Pas de migration de données pour dédupliquer les profils déjà en base avec des noms identiques
  (ex. les 3 "Profil OXO standard (Copie)" de la capture) ni pour raccourcir d'éventuels noms
  déjà trop longs — la base réelle ne contient que des données de test, elle sera **supprimée et
  recréée** à partir des migrations + du seed (`DefaultProfileSeeder`, Lot M) après ce lot, pour
  les profils d'import comme d'export. Voir Note d'efficacité, point 6.
- Pas de traitement de `DefaultProfileSeeder` (Lot M) : les noms seedés
  (`"Profil OXO standard"` pour import et export) sont uniques par construction dès le premier
  seed ; aucune collision possible avec lui-même (idempotence par `Guid` stable, pas par nom).

---

## 27.0. Domain — invariant longueur max 60 caractères sur `Name`

**Comportement attendu**, symétrique `ImportProfile`/`ExportProfile` :
- Constructeur(s) (tous ceux qui reçoivent `Name`, y compris le constructeur "préservant l'Id"
  utilisé en édition — voir F3/J2) : valide `Name.Trim().Length <= 60` **en plus** de la
  validation "non vide/blanc" déjà en place. Le `Trim()` de la vérification de longueur est
  cohérent avec la normalisation actée pour 27.2 (des espaces en début/fin ne comptent pas dans la
  limite perçue par l'utilisateur).
- Violation → `DomainValidationException` avec un nouveau `DomainErrorCode` dédié par entité
  (`ImportProfile_NameTooLong`, `ExportProfile_NameTooLong`), même mécanisme que
  `ImportProfile_EmptyEquipementTypeElementNom` — pas de nouveau type d'exception, le
  vocabulaire existant couvre déjà ce cas.
- Colonne DB : `Name` déjà mappée en `nvarchar` — vérifier/ajuster la longueur de colonne EF Core
  (`HasMaxLength(60)`) dans `ImportProfileConfiguration`/`ExportProfileConfiguration`, migration
  incluse dans celle de 27.2 (une seule migration pour l'index unique **et** la longueur de colonne,
  pas deux migrations séparées pour le même lot).
- Blazor (`ImportProfileEditor.razor`/`ExportProfileEditor.razor`) : attribut HTML `maxlength="60"`
  sur `#import-profile-name-input`/`#export-profile-name-input` — confort de saisie (défense en
  profondeur, comme pour 27.2, le constructeur Domain reste la seule source de vérité en cas de
  contournement du champ HTML).

**Tests** (Domain) :
- Nom de exactement 60 caractères → accepté (limite inclusive, pas d'erreur "off by one").
- Nom de 61 caractères → `DomainValidationException`/`DomainErrorCode` attendu.
- Nom de 65 caractères mais avec espaces en début/fin ramenant le `Trim()` à 60 → accepté (le
  test vérifie que la validation porte bien sur la longueur *après* `Trim()`, pas avant).

**Tests** (bUnit) :
- Tentative de sauvegarde avec un nom de 61+ caractères (contournement possible de l'attribut
  HTML via manipulation directe du modèle en test) → message d'erreur localisé affiché via
  `BusinessExceptionLocalizer`, pas de navigation, `SaveAsync` jamais appelé.

---

## 27.1. Domain/Application — nouvelle exception `ProfileNameAlreadyExistsException`

**Comportement attendu** :
- Nouvelle exception dans `ExcelETL.Application` (pas `ExcelETL.Domain` — dépend de l'état du
  Store, n'est pas un invariant de construction), ex.
  `ExcelETL.Application.Extraction.Exceptions.ProfileNameAlreadyExistsException` (ou emplacement
  miroir côté export), portant le nom en collision.
- Distincte de `DomainValidationException`/`DomainErrorCode` (qui restent réservés aux invariants
  de construction d'entité) mais interceptée par `BusinessExceptionLocalizer` au même titre, pour
  rester cohérent avec le seul point d'affichage d'erreur déjà en place côté éditeurs Blazor — pas
  un deuxième mécanisme d'affichage d'erreur à maintenir.

**Tests** (Application) :
- Construction de l'exception expose bien le nom en collision (propriété lisible, utile au
  message localisé).

---

## 27.2. Infrastructure — contrainte DB + vérification dans les Stores

**Comportement attendu** :
- `ImportProfileConfiguration`/`ExportProfileConfiguration` : index unique sur `Name`
  (`HasIndex(x => x.Name).IsUnique()`), migration EF Core dédiée. Collation insensible à la casse
  cohérente avec la vérification applicative (à vérifier contre la collation par défaut de la base
  cible ; documenter si une collation explicite doit être précisée dans la migration).
- `EfImportProfileStore.SaveAsync`/`EfExportProfileStore.SaveAsync` : avant insert (nouveau `Id`)
  ou update (`Id` existant), recherche d'un profil dont `Name.Trim()` égale (insensible casse) le
  nom soumis, **`Id` courant exclu de la recherche en mode update**. Collision détectée → lève
  `ProfileNameAlreadyExistsException` **avant** toute écriture (pas de dépendance à la contrainte
  DB pour ce chemin nominal — la contrainte DB est un filet de sécurité pour les races conditions
  concurrentes, pas le mécanisme principal de retour d'erreur).

**Tests** (Infrastructure, contre le vrai provider EF Core InMemory, jamais mocké — même
pattern que `EfImportProfileStoreTests`/`EfExportProfileStoreTests` existants) :
- `SaveAsync` avec un nom déjà existant (nouveau `Id`) → lève `ProfileNameAlreadyExistsException`,
  aucune entité insérée.
- `SaveAsync` en mode update sur son propre `Id` avec son propre nom inchangé → aucune exception
  (ne pas se collisionner avec soi-même).
- `SaveAsync` en mode update avec le nom d'un **autre** profil existant → lève l'exception.
- Comparaison normalisée : `"Profil A"` vs `"  profil a  "` → détecté comme collision.
- Deux noms réellement distincts → aucune exception, les deux profils coexistent.

---

## 27.3. Blazor — affichage de l'erreur dans les éditeurs

**Comportement attendu**, symétrique `ImportProfileEditor.razor`/`ExportProfileEditor.razor` :
- `SaveProfileAsync` catch `ProfileNameAlreadyExistsException` au même point que les autres
  violations Domain déjà catchées (`BusinessExceptionLocalizer.TryLocalize(ex)`), affiche le
  message localisé, **ne navigue pas**, le formulaire reste rempli tel que saisi (pas de perte de
  saisie utilisateur).
- Nouvelles clés resx (EN/FR) : `ImportProfileEditor_DuplicateName`,
  `ExportProfileEditor_DuplicateName`.

**Tests** (bUnit, réels stores + EF Core InMemory, pas de mock — cohérent avec le pattern déjà en
place pour les tests d'édition F3/J2) :
- Tentative de sauvegarde avec un nom déjà pris (autre profil existant en base) → message d'erreur
  localisé affiché, pas de navigation, `SaveAsync` en échec proprement catché (pas d'exception non
  gérée qui remonte au composant).
- Sauvegarde en édition avec le nom **inchangé** du profil en cours d'édition → succès normal (pas
  de faux positif sur soi-même).

---

## 27.4. Blazor — bouton Dupliquer : suffixe auto-incrémenté

**Comportement attendu**, symétrique `ImportProfiles.razor`/`ExportProfiles.razor` :
- Au clic sur Dupliquer, avant `SaveAsync` : calcule un nom candidat en partant du suffixe actuel
  (`"{Name} (Copie)"`), puis vérifie sa disponibilité contre les profils déjà chargés
  (`GetAllAsync()`, déjà en mémoire côté liste — pas d'aller-retour réseau supplémentaire) ; si
  pris, incrémente (`"(Copie 2)"`, `"(Copie 3)"`, ...) jusqu'à trouver un nom libre.
- Ce calcul rend `27.2`/`27.3` non observables dans le flux normal de duplication (le nom soumis est
  toujours déjà disponible) — la vérification Store reste un filet de sécurité pour les autres
  flux (Créer/Modifier manuels), pas contournée pour autant.

**Tests** (bUnit, réels stores + EF Core InMemory) :
- Un seul profil existant → Dupliquer produit `"{Name} (Copie)"` (comportement actuel inchangé,
  non-régression).
- Profil **et** sa `"(Copie)"` existent déjà → nouveau clic sur Dupliquer (sur l'original ou sur
  la copie) produit `"{Name} (Copie 2)"`.
- Trois niveaux de collision déjà présents (`(Copie)`, `(Copie 2)`) → nouveau Dupliquer produit
  `"(Copie 3)"`.

---

## Note d'efficacité d'implémentation

Ordre recommandé pour Claude Code, afin de minimiser les allers-retours et le travail refait :
1. **27.0** en tout premier (invariant Domain pur, zéro dépendance, même pattern qu'un invariant
   déjà 2 fois répété dans le code) — le plus rapide et le plus isolé du lot.
2. **27.1** ensuite (exception seule, isolée, aucune dépendance) — base des tickets suivants.
3. **27.2** ensuite (Store + migration) — c'est le cœur métier du lot, testable indépendamment de
   Blazor via les tests Infrastructure existants comme gabarit. **Une seule migration EF Core**
   pour cette étape : index unique (27.2) **et** `HasMaxLength(60)` (27.0) en même temps, pas deux
   migrations séparées pour le même lot.
4. **27.3** et **27.4** en parallèle logique une fois 27.2 vert — 27.3 est un simple ajout de catch
   symétrique à un pattern déjà 3 fois répété dans le code (F3/J2/autres validations), 27.4 est un
   calcul côté composant sans nouvelle dépendance Store. L'attribut HTML `maxlength="60"` de 27.0
   peut être posé en même temps que 27.3/27.4 (même fichiers Razor touchés, évite une deuxième passe
   sur les mêmes composants).
5. Lancer la suite complète (`dotnet test`) à la fin de 27.4 seulement, pas après chaque sous-ticket
   — les sous-tickets touchent des couches disjointes (Domain/Application/Infrastructure/Blazor×2),
   peu de risque d'interférence entre eux qui justifierait une vérification intermédiaire complète.
6. **Migration sur la base réelle** : la base actuelle ne contient que des données de test (pas de
   données de production) — pas de nettoyage manuel de doublons de noms ou de noms trop longs à
   prévoir. La base réelle peut être **supprimée et recréée** à partir des migrations + du seed
   (`DefaultProfileSeeder`, Lot M) après application de cette migration, aussi bien pour les
   profils d'import que d'export. Aucune procédure de dédoublonnage/troncature n'est nécessaire
   dans ce ticket.
