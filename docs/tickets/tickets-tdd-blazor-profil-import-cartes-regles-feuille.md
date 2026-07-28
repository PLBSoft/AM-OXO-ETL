# Tickets TDD — Lot P : séparation visuelle des règles de feuille (vue résumé, Profils d'import)

*Document vivant (pas de suffixe de date, voir `convention-nommage-documents.md`). Indépendant des
Lots N (`tickets-tdd-blazor-profil-import-lisibilite-plages-excel.md`) et O
(`tickets-tdd-blazor-profil-import-boutons-champs-bloc.md`) — ce lot ne touche que le conteneur
global de chaque règle de feuille dans la vue résumé, pas le contenu déjà traité par N/O (plages
Excel, boutons de champ, labels de section). Peut être fait avant, après, ou en parallèle de N/O.*

**Périmètre : uniquement `ImportProfileEditor.razor`, vue résumé des règles de feuille (Profils
d'import).** Même remarque que N/O : la même correction sera probablement pertinente côté
`ExportProfileEditor.razor`, mais **reportée à un lot ultérieur**.

**Note transverse (décision explicite, pas un oubli)** : l'ensemble des règles UI/UX actées dans
N, O et P (lisibilité des plages Excel, boutons icône, cartes de règle, alignement des boutons —
voir `convention-ui-blazor-alignement-boutons.md`) est **applicable telle quelle à
`ExportProfileEditor.razor`**, mais volontairement **réalisé dans un second temps**, une fois
validé côté client sur les profils d'import qui servent de modèle. Ne pas anticiper ce travail
côté export tant que ce point n'a pas été explicitement rouvert.

---

## P0. Constat

Retour client sur capture d'écran réelle (21/07, écran `Modifier le profil d'import`, après
application de N et O) : les règles de feuille (`PROCEDURE`, `ISOLEMENT`, `PLATINES`...) sont
listées à la suite les unes des autres, séparées uniquement par les mêmes traits fins que ceux
utilisés *entre chaque champ à l'intérieur* d'une règle. Rien ne distingue visuellement où une
règle se termine et où la suivante commence — le titre de section (`ISOLEMENT -- ligne de début
19, pas 7, champ d'arrêt 'Identification'`) a le même traitement typographique discret que le
reste du contenu.

**Décision actée** : encadrer chaque règle de feuille dans une carte visuellement distincte
(bordure propre, espacement net entre les cartes — pas un simple trait), avec un en-tête de
feuille mis en valeur (nom de la feuille en plus grand/plus marqué, métadonnées ligne de
début/pas/champ d'arrêt en texte secondaire à côté, pas sur la même ligne de poids égal).

---

## P1. Carte par règle de feuille

- Chaque règle de feuille (dans la vue résumé, pas dans le panneau d'édition — voir note ci-
  dessous) est enveloppée dans un conteneur visuellement distinct : bordure fine, coins
  arrondis, un espacement vertical net entre deux cartes consécutives (nettement supérieur à
  l'espacement interne entre deux champs de la même carte, pour que l'œil perçoive clairement la
  frontière).
- En-tête de carte : nom de la feuille en taille de police plus marquée (ex. niveau titre `h4`/
  équivalent), avec les métadonnées (`ligne de début`, `pas`, `champ d'arrêt`) juste à côté ou en
  dessous, en texte secondaire discret — plus de ligne unique où tout a le même poids visuel.
- Le contenu existant à l'intérieur de la carte (liste des champs de bloc avec plage Excel
  monospace, labels + listes "Colonnes inconditionnelles"/"Règles de point conditionnelles" issus
  du Lot O2, boutons `Modifier`/`Supprimer` de la règle) **ne change pas** — seul le conteneur
  autour et le traitement de l'en-tête changent.
- **Alignement des boutons de règle** : `modify-sheet-rule-button-{i}` et
  `delete-sheet-rule-button-{i}`, en bas de chaque carte, doivent être **alignés à droite** du
  conteneur — application de la convention générale actée dans
  `convention-ui-blazor-alignement-boutons.md`. Aucun changement d'id, de comportement ni de
  texte, uniquement leur position horizontale dans la carte.
- **Point à vérifier par Claude Code avant implémentation** : la règle actuellement en cours de
  modification (panneau d'édition en haut de l'écran) n'apparaît, d'après les captures observées,
  pas une seconde fois dans la vue résumé pendant qu'elle est éditée — à confirmer contre le code
  réel. Si c'est bien le cas, ce lot ne touche que les règles *non* en cours d'édition ; si une
  règle en cours d'édition apparaît malgré tout dans la vue résumé, le comportement à adopter
  (carte quand même, ou masquage) doit être signalé comme point ouvert plutôt que décidé
  silencieusement.

### Tests (bUnit)
- Rendu de la vue résumé avec plusieurs règles (profil OXO seedé, 6 feuilles) : chaque règle est
  enveloppée dans un conteneur identifiable (ex. classe CSS dédiée, une occurrence par règle) —
  assertion sur le nombre de conteneurs égal au nombre de règles, pas sur un rendu visuel.
- Le nom de la feuille est rendu dans un élément distinct des métadonnées (deux sélecteurs
  séparés), cohérent avec l'approche déjà utilisée pour séparer nom de champ / plage Excel en
  O1.
- Non-régression : le contenu interne de chaque carte (champs de bloc, labels O2, boutons
  Modifier/Supprimer de règle) reste identique à l'existant — réutiliser les assertions déjà en
  place pour N/O plutôt que les dupliquer.

---

## P2. Alignement de `save-profile-button` (formulaire racine)

Distinct de P1 : `save-profile-button` (et son bouton `Annuler` associé, si un id existe déjà
pour lui) appartient au formulaire racine de la page (`Nom du profil`/`Préfixe de repère`/`Nom du
type d'élément d'équipement`), pas à une carte de règle de feuille — techniquement hors du
périmètre "carte" de P1, mais nommé explicitement par le client et gouverné par la même
convention générale (`convention-ui-blazor-alignement-boutons.md`).

- Aligner `save-profile-button` (et `Annuler` s'il existe) à droite de son conteneur, même
  principe qu'en P1 : aucun changement d'id, de comportement ni de texte, uniquement la position
  horizontale.

### Tests (bUnit)
- Non-régression fonctionnelle pure : clic sur `save-profile-button` déclenche toujours la même
  action qu'avant (pas de nouveau test de positionnement CSS, cohérent avec l'absence de tests de
  ce type ailleurs dans le projet — vérification visuelle suffisante pour un simple changement
  d'alignement).

---

## Hors périmètre (explicitement reporté)

- **`ExportProfileEditor.razor`** — même traitement probable, lot séparé après validation client
  sur import.
- **Panneau d'édition d'une règle** (formulaire "Nom de la feuille"/"Ligne de début"/"Pas"/"Champ
  d'arrêt" + liste éditable des champs de bloc) — traité par le Lot O1, non retouché ici.
- **Contenu des champs de bloc / colonnes inconditionnelles / règles de point conditionnelles** —
  déjà traité par N/O, non retouché ici, seul le conteneur autour change.
- **Ordre d'affichage des règles de feuille** — inchangé, hors périmètre.

---

## Note d'efficacité d'implémentation

1. Vérifier d'abord le point ouvert (règle en cours d'édition présente ou non dans la vue résumé)
   par lecture directe du code — évite de découvrir le cas en cours de développement.
2. Un seul changement de markup/CSS pour l'en-tête + le conteneur de carte, appliqué à toutes les
   règles de la vue résumé de façon identique (pas de traitement spécial par feuille).
3. Vérifier la suite bUnit complète de `ImportProfileEditor` après coup pour confirmer l'absence
   de régression sur les assertions déjà en place pour N/O.
