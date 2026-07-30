# Demande à Claude Code — État des lieux technique du projet
 
> **Usage** : ce fichier est une trame réutilisable. À chaque fois qu'un état des
> lieux est nécessaire (nouveau chantier, reprise après une pause, onboarding
> d'une nouvelle fonctionnalité...), copier ce fichier, remplir les champs
> `[À COMPLETER]`, puis le donner tel quel à Claude Code comme prompt/consigne.
 
---
 
## Métadonnées de la demande
 
- **Date de la demande** : [À COMPLETER — ex. 2026-07-14]
- **Demandeur** : [À COMPLETER]
- **Contexte / raison de la demande** : [À COMPLETER — ex. "avant de démarrer
  l'écran de gestion des profils d'import"]
- **Solution/repo concerné** : [À COMPLETER — chemin ou nom du repo]
- **Version/commit de référence** : [À COMPLETER — branche, tag ou hash de commit]
---
 
## Consigne pour Claude Code
 
Tu es Claude Code et tu as accès au repository de la solution. Produis un
document Markdown intitulé **"État des lieux technique"** destiné à être lu
par Claude AI (dans une autre session, sans accès au code) afin qu'il puisse
proposer des évolutions cohérentes avec l'existant.
 
Le document doit être factuel, basé sur une lecture réelle du code (pas de
suppositions), concis mais complet, et structuré avec les sections suivantes :
 
### 1. Structure de la solution / des projets
- Arborescence des projets (Domain, Application, Infrastructure, Web API,
  Blazor, tests, etc.)
- Dépendances entre projets (qui référence qui) et sens de câblage
  (Clean Architecture : Domain au centre, dépendances qui pointent vers
  l'intérieur)
- Points d'entrée (Program.cs / Startup) et enregistrement des services (DI)
### 2. Conventions déjà adoptées
- Convention de nommage (fichiers, classes, namespaces)
- Organisation des dossiers : par feature ou par couche technique
- Pattern Repository / Unit of Work (présent ? générique ? spécifique par
  entité ?)
- Gestion des erreurs : exceptions custom, Result pattern, ProblemDetails...
- Mapping (AutoMapper, Mapster, manuel...)
- Validation (FluentValidation, Data Annotations...)
### 3. Modèle EF Core existant
- Entités déjà présentes (Identity, Logs, autres)
- DbContext(s) actuel(s) et leur configuration
- Stratégie de migrations (nommage, dossier, comment elles sont générées/appliquées)
- Conventions de mapping (Fluent API vs Data Annotations)
### 4. Authentification / autorisation
- Mécanisme utilisé (Identity, JWT, Azure AD, autre)
- Comment c'est câblé dans l'API et dans Blazor
- Gestion des rôles/claims/policies
- Points d'extension prévus ou à prévoir pour de nouveaux écrans/profils
### 5. Conventions de tests
- Structure des projets de test (unitaires, intégration, par couche/feature)
- Frameworks utilisés (xUnit/NUnit, FluentAssertions, Moq/NSubstitute...)
- Helpers, builders, fixtures déjà en place
- Conventions de nommage des tests
### 6. ADR (Architecture Decision Records)
- Lister les ADR existants (emplacement, format)
- Résumer chacun en 2-3 lignes (décision + raison)
- Si aucun ADR n'existe, le mentionner explicitement
---
 
## Format de sortie attendu
 
- Un seul fichier Markdown, nommé `etat-des-lieux-technique-[AAAA-MM-JJ].md`
- Sections numérotées comme ci-dessus, avec sous-titres `##`/`###`
- Extraits de code courts (quelques lignes) uniquement quand ils illustrent
  une convention, pas de copie de fichiers entiers
- Mentionner explicitement les zones "non standard" ou incohérentes si
  Claude Code en détecte
- Terminer par une section **"Non couvert / incertain"** listant ce qui
  n'a pas pu être déterminé avec certitude à partir du code
---
 
## Check-list avant envoi à Claude AI
 
- [ ] Le document a bien été généré à partir du code réel (pas de mémoire/suppositions)
- [ ] Toutes les 6 sections sont présentes
- [ ] La section "Non couvert / incertain" est renseignée (même si vide)
- [ ] Le fichier est daté et versionné (commit de référence noté en en-tête)
