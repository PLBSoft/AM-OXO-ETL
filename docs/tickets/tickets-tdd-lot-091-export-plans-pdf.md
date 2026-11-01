# Tickets TDD — Lot 091 : export des plans en PDF

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Ouvert le 12/10 après
l'étude des images des fichiers clients et un POC LibreOffice sur les 14 fichiers de `tests/Fixtures`.
Ticket GitHub : [PLBSoft/AM-OXO-ETL#3](https://github.com/PLBSoft/AM-OXO-ETL/issues/3) (texte destiné
au client). **Travail dans la branche `3-export-plans-pdf`, jamais sur `main`.** Choix D1-D5 et D10 tranchés par Simon et le client
les 12 et 13/10, D7 le 31/10 ; propositions D6, D8, D9, D11-D14 validées par Simon le 31/10. Demande validée par
le client (ticket officiel) le 31/10. **Lot livré le 31/10 (091.1 à 091.9), voir §5.***

---

## 0. La demande

Les dossiers de MaD contiennent des plans (photos et schémas annotés). Le client veut les recevoir en PDF
dans OXO, rattachés au BE parent.

Réponses du client (12/10) :
- les PDF sont chargés comme médias **du BE parent** ;
- on **imprime la page**, annotations comprises, pas seulement les images ;
- mise en page imposée **1 page en largeur sur N pages** : celle des fichiers n'est pas fiable ;
- onglets concernés : **PLANS ISOLEMENT (Paysage)** et **PLANS PLATINAGE & JOINTS**.

---

## 1. Constat

Mesures faites le 12/10 sur les 14 fichiers de `tests/Fixtures`.

- **Où sont les plans.** Les 6 onglets lus par l'extraction et VISITE PREALABLE ne contiennent qu'un
  logo (même image de 3 568 o partout). Les plans sont dans `PLANS ISOLEMENT (Portrait)`,
  `PLANS ISOLEMENT (Paysage)` et `PLANS PLATINAGE & JOINTS`. Images de 36 Ko à 2,9 Mo (G6306B : 5,3 Mo
  d'images pour un fichier de 5,7 Mo, sous la limite de 10 Mo de l'API).
- **Les annotations sont des formes Excel.** Presque toutes les pages portent des zones de texte, bulles,
  flèches et groupes de formes posés sur la photo (jusqu'à 48 sur une page de PSA B, 20 flèches sur
  RANGEE N°1, groupes dans E6431A). Seul G6306B PLATINAGE a ses annotations incrustées dans l'image.
  Un PDF fait à partir des seules images est donc inutilisable : il faut un moteur qui reproduit
  l'impression d'un onglet.
- **Pas de notion de page dans le fichier.** Ni saut de page manuel, ni zone d'impression. Le client
  répète un en-tête de page (cartouche : logo, titre, référence, révision, date, n° de page) toutes les
  54 à 60 lignes. Les pages se reconstituent à partir de ces répétitions.
- **Pages vides fréquentes, pas forcément la première.** G6306B PLATINAGE : page 1 vide, 2 et 3 remplies ;
  G6306B Paysage : page 1 remplie, 4 vides ; LRS4504 PLATINAGE : 3 pages vides.
- **Onglet Portrait.** E3201B, E8582 et LRS4504 ont un Paysage vide et leurs plans d'isolement sur
  **Portrait**. D8570 et RANGEE N°1 ont aussi du contenu sur Portrait. Le client l'inclut (D5).
- **Noms d'onglets.** C7401 nomme son onglet `PLANS ISOLEMENT (Paysage) ` avec une espace finale et n'a
  pas d'onglet PLATINAGE.
- **Largeur.** Des annotations débordent à droite de la cartouche (C7401 : bulle « CONTROLE ZERO
  ENERGIE »). La largeur imprimée doit couvrir tous les dessins.

### POC LibreOffice (12/10)

Script `poc/prepare.ps1` (dossier `poc/` non versionné, exclu via `.git/info/exclude` sur le poste de
Simon), PDF obtenus dans `poc/libreoffice-pdf/`. Méthode :
1. copie du fichier, modification directe du XML de l'archive (pas de réenregistrement par une
   bibliothèque, pour ne perdre aucune forme) ;
2. seul l'onglet visé reste visible ;
3. zone d'impression = pages non vides du client, une plage par page ; colonnes jusqu'au maximum entre la
   cartouche et le bord droit des dessins ;
4. mise en page « ajuster à 1 page en largeur et 1 en hauteur » **par plage** : une page du client donne
   une page PDF ;
5. conversion `soffice --headless --convert-to pdf`, avec un profil utilisateur dédié
   (`-env:UserInstallation`).

Résultats : 23 PDF pour 13 dossiers, 37 pages, aucune page vide, 84 à 760 Ko par PDF, environ 3 s par
onglet (9,5 s au premier lancement). Rendu vérifié sur 7 PDF : bulles, cadenas, flèches, groupes, texte
pivoté et cartouches corrects ; texte sélectionnable. LibreOffice recompresse les photos (G6306B : 5,3 Mo
d'images → 760 Ko), net à l'écran.

Écart rencontré : « 1 page en largeur, hauteur libre » faisait déborder chaque page de quelques lignes
sur une page presque vide. D'où l'étape 4.

---

## 2. Décisions

| # | Question | Décision |
| :--- | :--- | :--- |
| D1 | Qui extrait les plans | **AM-OXO-ETL** (Simon, 12/10). Le legacy ne doit pas connaître la structure du fichier source ; il récupère les PDF par l'API. |
| D2 | Contenu du PDF | **La page imprimée**, annotations comprises (client, 12/10). |
| D3 | Rattachement | **BE parent** = l'équipement du dossier (client, 12/10). Fait côté legacy. |
| D4 | Mise en page | **1 page en largeur**, une page du client = une page PDF, pages vides retirées (client, 12/10 ; découpage précisé par le POC). |
| D5 | Onglets imprimés | **Les 3 onglets** : `PLANS ISOLEMENT (Portrait)`, `PLANS ISOLEMENT (Paysage)`, `PLANS PLATINAGE & JOINTS` (client, 13/10 : Portrait ajouté). Rendu de Portrait vérifié le 13/10 : 10 pages pour 5 dossiers ; LRS4504 Portrait identique à la capture d'aperçu Excel transmise par le client. Les 14 dossiers produisent au moins un PDF (47 pages). |
| D6 | Paramétrage | **Validé (31/10)** : **une seule liste dans le profil d'import**, `PlanSheetNames` (noms d'onglets à imprimer, comparés sans espaces de début/fin ni casse). Liste vide = fonction désactivée. Profil standard : les 3 onglets de D5. Le reste (détection des pages, mise en page, retrait des pages vides) est **fixe dans le code** : un seul modèle de dossier connu, rien ne justifie de le paramétrer (YAGNI). À rouvrir si un client a une autre mise en page. |
| D7 | Moteur de rendu | **LibreOffice en ligne de commande** (Simon, 31/10 : comparaison avec Excel satisfaisante, licence MPL 2.0 acceptée) derrière une interface Application `IPlanSheetPdfRenderer`. Remplaçable par Syncfusion XlsIO (licence du client) si besoin un jour, sans toucher au reste. Écartés : PDFsharp seul (ne sait pas imprimer un onglet, D2) ; piloter Excel sur le serveur (non pris en charge par Microsoft sans utilisateur, blocages sur boîte de dialogue, dépend d'une imprimante, licence floue). |
| D8 | Préparation du classeur | **Validé (31/10)** : modification directe du XML avec le SDK OpenXML (`DocumentFormat.OpenXml`, MIT, déjà présent par ClosedXML, à référencer explicitement), jamais par un réenregistrement ClosedXML (risque de perte des formes, non vérifié). |
| D9 | Détection des pages | **Validé (31/10)** : une page commence à chaque image **identique** (même contenu) à celle ancrée dans les 4 premières lignes de l'onglet (le logo de la cartouche). Une page est non vide si une image autre que ce logo ou une forme y est ancrée. Sans répétition détectée : l'onglet entier forme une seule page s'il a du contenu. Le critère « image de 3 568 o » du POC n'est pas retenu (fragile). |
| D10 | Découpage en fichiers | **Un PDF par page** (client, 13/10). Mise en œuvre : un seul rendu par onglet (le lancement de LibreOffice coûte l'essentiel des 3 s), puis découpage du PDF page par page avec PDFsharp (MIT, 6.2.4, cible `net10.0`). Écarté : un rendu par page (47 lancements au lieu de 26 sur les fichiers de test). Nom proposé : `{nom du classeur généré}_{onglet}_p{n}.pdf`. |
| D11 | Quand générer | **Validé (31/10)** : **pendant `POST /api/oxo/process`**, après la génération du classeur, en best-effort : un échec est journalisé (Error) et le classeur est renvoyé normalement. Pas de PDF pour un fichier rejeté (pas de BE parent). Coût : environ 3 s par onglet. Écarté : génération à la demande (le legacy les demande juste après de toute façon, et le fichier source peut avoir été purgé, lot 090). |
| D12 | Exécutions simultanées | **Validé (31/10)** : une conversion à la fois (verrou dans l'implémentation), délai maximal de 60 s par onglet, profil LibreOffice dans un dossier temporaire propre au service. |
| D13 | Archivage | **Validé (31/10)** : PDF écrits dans l'arborescence d'archive à côté du fichier cible, listés dans `GeneratedFileRecord.Plans` (collection détenue, nouvelle table). Supprimés par la purge du lot 090 comme les autres fichiers. |
| D14 | Exposition | **Validé (31/10)** : `GET api/generated-files/{id}/plans` (liste : onglet, nom de fichier, nombre de pages, taille, disponibilité) et `GET api/generated-files/{id}/plans/{index}` (`application/pdf`, 404 si purgé ou absent). Pas de changement de la réponse de `POST /api/oxo/process`. Téléchargement aussi depuis `/generated-files` dans BlazorAdmin. |

---

## 3. Tickets

Un commit par ticket, dans la branche du lot. Tests filtrés sur la classe en cours ; en fin de lot, les
projets touchés.

### 091.0 — Prérequis (aucun code) — **fait le 31/10**

- Comparaison des PDF d'essai avec un export Excel : satisfaisante (Simon).
- Licence MPL 2.0 de LibreOffice : acceptée (programme appelé, pas intégré au code).
- Demande validée par le client.

### 091.1 — Onglets de plans dans le profil d'import (Domain + Infrastructure)

- `ImportProfile.PlanSheetNames` : dernier paramètre facultatif des constructeurs (vide par défaut), même
  forme que `TacheMultipleTypeLabels` (lot 067). Validation par un `ValidatePlanSheetName` statique
  réutilisé par l'éditeur : non vide, 31 caractères au plus (limite Excel), unique sans casse ni espaces
  de début/fin. Valeurs stockées sans espaces de début/fin. Codes `DomainErrorCode` + `.resx` EN/FR.
- EF : collection primitive (colonne JSON, comme `DefaultTableaux`), migration générée par
  `dotnet ef migrations add`.
- `DefaultProfileSeeder` : les 3 onglets de D5.
- Tests : validations, aller-retour EF (InMemory), contenu du profil standard,
  `DomainErrorMessages…LocalizationTests`.

### 091.2 — Édition et vue « Détails » (BlazorAdmin)

- `ImportProfileEditor.razor` : liste « Onglets de plans à imprimer en PDF », même présentation et mêmes
  outils que Tableaux/Applications (`StringItemDraft`, `ListItemEditState`, `DraftListActions`,
  conversion par `ImportProfileDraftMapper`).
- `ImportProfileDescriptionBuilder` : une phrase dans la section générale ; catalogue figé (tests +
  ticket 078 §5) mis à jour.
- Tests : ajout, modification, suppression, doublon refusé, ligne en attente (règles de
  `recommandations-tickets-tdd.md` §9), aller-retour du mapper sur le profil standard.

### 091.3 — Détection des pages d'un onglet (Application, logique pure)

- Modèle d'entrée minimal décrivant les ancrages d'un onglet (ligne de début, ligne et colonne de fin,
  type image/forme, clé de contenu de l'image) et les cellules fusionnées de la cartouche ; sortie : liste
  de plages imprimables (lignes, dernière colonne).
- Tests sans fichier : pages reconstituées à partir du logo répété ; page vide retirée (première, dernière,
  toutes) ; aucune répétition → une seule plage ou aucune ; largeur étendue au dessin le plus à droite ;
  une image différente du logo dans la cartouche ne crée pas de page.

### 091.4 — Préparation du classeur à imprimer (Infrastructure)

- Lecture des ancrages par le SDK OpenXML vers le modèle du 091.3, puis écriture d'une copie : onglet
  visé seul visible et actif, zone d'impression = plages du 091.3, mise en page 1 × 1 par plage, sauts de
  page supprimés. Onglet cherché sans espaces de début/fin ni casse.
- Tests sur les fichiers réels : plages attendues pour G6306B (Platinage : 2 pages ; Paysage : 1),
  D8570 (Paysage : 7), C7401 (espace finale, largeur jusqu'à la bulle de droite), LRS4504 Platinage
  (aucune page) ; parties de dessin inchangées dans la copie (aucune forme perdue).

### 091.5 — Rendu PDF par LibreOffice (Infrastructure)

- `IPlanSheetPdfRenderer` (Application) ; `LibreOfficePlanSheetPdfRenderer` (Infrastructure) :
  `soffice --headless --convert-to pdf`, profil dédié, verrou, délai maximal (D12). Chemin de `soffice`
  en configuration (`PlanRendering:SofficePath`).
- Découpage du PDF de l'onglet en un PDF par page (PDFsharp, D10), dans l'implémentation Infrastructure.
- Tests d'intégration sur 3 fichiers réels (nombre de PDF attendu, une page chacun, PDF valides), en catégorie
  `LibreOffice` : ils échouent avec un message clair si LibreOffice est absent. Délai dépassé et échec
  du processus testés avec un faux exécutable.

### 091.6 — Génération et archivage pendant le traitement (Application + Infrastructure)

- `ProcessOxoFileService` : après la génération du classeur, pour chaque onglet de `PlanSheetNames`
  présent et non vide, préparation + rendu + écriture dans l'archive (`IGeneratedFileWriter`), best-effort
  (D11). Rien pour un fichier rejeté.
- `GeneratedFileRecord.Plans` (collection détenue : onglet, nom, chemin relatif, nombre de pages, taille),
  migration ; purge du lot 090 étendue aux PDF.
- Tests : PDF archivés pour un fichier accepté ; aucun pour un rejet ; échec du rendu → classeur renvoyé
  quand même, erreur journalisée ; liste vide → rien.

### 091.7 — Exposition par l'API (WebAPI)

- Routes de D14, `GeneratedFileSummaryResponse` enrichi de la liste (sans chemin serveur, comme
  `SourceFilePath`).
- Tests HTTP (`WebApplicationFactory`) : liste, téléchargement, 404 index inconnu, 404 après purge,
  aucun champ `FilePath` dans le JSON, clé API exigée.

### 091.8 — Téléchargement dans `/generated-files` (BlazorAdmin)

- Une colonne « Plans » (nombre + liens), même mécanisme que les téléchargements source/cible,
  « Fichier expiré » après purge.

### 091.9 — Documentation et déploiement

- `CLAUDE.md` (section API et état du lot), guide de déploiement : installation de LibreOffice sur le
  serveur, `PlanRendering:SofficePath`, vérification manuelle.
- Consigne pour la session legacy : lire la liste des plans et les charger comme médias du BE parent.

---

## 4. Hors périmètre

- Côté legacy : téléchargement des PDF et création des médias (dépôt séparé, consigne en 091.9).
- Pages de test `/import-profiles/test`, `/export-profiles/test` et `/api-test` : pas de génération de
  PDF.
- Paramétrage de la détection des pages ou de la mise en page (D6).
- Rattachement d'un plan à un élément enfant ou à une tâche (pas d'information exploitable dans le
  fichier).
- Extraction des images brutes.
- Rejet d'un fichier sans plans : un dossier sans plan reste valide.

---

## 5. Résultat (31/10)

Un commit par ticket sur `3-export-plans-pdf` : 091.1 `74a907f`, 091.2 `71fc28e`, 091.3 `12c257a`,
091.4 `583d34e`, 091.5 `b43352f`, 091.6 `8ec69b6`, 091.7 `5099800`, 091.8 `8ab6c6a`, correctif `2423e85`,
091.9 (ce document, CLAUDE.md, guide de déploiement).

- **Correctif (D12)** : deux processus utilisant le même profil LibreOffice entrent en conflit (le second
  confie sa conversion au premier et se termine sans PDF). Vu en lançant toute la solution (tests
  Infrastructure et WebAPI en parallèle) ; même risque en production pendant un recyclage IIS avec
  chevauchement des processus. Un fichier verrou à côté du profil sérialise les conversions entre
  processus (attente bornée à 2 × `TimeoutSeconds`).

- **Pages** : la règle D9 (logo identifié par son contenu, pas sa taille) redonne exactement les pages du
  POC sur les 14 fichiers (47 pages). Les dessins enveloppés dans `mc:AlternateContent` sont lus une
  seule fois (la copie `mc:Fallback` est ignorée), ce que le POC ne faisait pas.
- **Classeur préparé** : seuls `workbook.xml` et l'onglet sont réécrits ; tous les autres éléments du
  fichier (dessins, images) sont identiques octet pour octet, vérifié sur 3 fichiers.
- **Rendu réel** vérifié par les tests : G6306B Platinage 2 PDF, D8570 Paysage 7, LRS4504 Portrait 1,
  chaque PDF d'une page ; de bout en bout par une vraie requête HTTP (G6306B avec les profils standard :
  3 PDF).
- **Index des plans (D14)** : position dans la liste, à partir de 0, ordre onglet puis page (les lignes
  d'une collection détenue n'ont pas d'ordre propre). Le legacy n'a pas à le calculer : il suit
  `DownloadUrl`.
- **Écart avec le ticket 091.6** : `GeneratedFilePlan` porte le numéro de page plutôt qu'un « nombre de
  pages » (toujours 1 avec un PDF par page).
- **Constaté en passant, corrigé en 091.1** : dupliquer un profil d'import perdait ses libellés de
  tâches multiples (lot 067).
- **Tests à LibreOffice** : catégorie `LibreOffice` (4 tests) ; ils échouent avec un message clair sur un
  poste sans LibreOffice. Pour les exclure : `dotnet test --filter "Category!=LibreOffice"`.

## 6. Consigne pour la session legacy

À transmettre à la session Claude Code du dépôt legacy (application MVC5) :

> L'API OXO ETL imprime désormais les plans des dossiers en PDF (une page = un PDF) pour chaque fichier
> accepté par `POST /api/oxo/process`. Aucun changement de la requête ni de la réponse de cette route.
> Après un traitement réussi, lire l'en-tête `X-Generated-File-Id`, puis appeler
> `GET api/generated-files/{id}/plans` (même clé `X-Api-Key`). Réponse : un tableau
> `[{ index, sheetName, pageNumber, fileName, sizeBytes, available, downloadUrl }]`, éventuellement vide
> (dossier sans plan, profil sans onglet de plans, ou échec d'impression côté API, non bloquant).
> Pour chaque élément `available = true`, télécharger `downloadUrl` (relatif à l'URL de base de l'API,
> `application/pdf`, nom de fichier dans `fileName`) et le charger comme **média du BE parent**
> (l'équipement créé à partir du classeur). La même liste figure dans `plans` de
> `GET api/generated-files/{id}`. Une page purgée (rétention, lot 090) répond 404 et
> `available = false`. Prévoir un délai : le traitement prend environ 3 s de plus par onglet de plans.
