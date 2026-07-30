# Demande à Claude Code — Compte rendu d'avancement
 
> **Usage** : trame réutilisable. Copier, remplir les `[À COMPLETER]`, donner tel quel à Claude Code.
> Le compte rendu produit est un instantané daté, jamais mis à jour après coup.
 
---
 
## Métadonnées
 
- **Période couverte** : [À COMPLETER]
- **Compte rendu précédent** : [À COMPLETER — ce qu'il annonçait]
- **Commits / branche à examiner** : [À COMPLETER]
- **Point de vigilance imposé** : [À COMPLETER, ou « aucun ». Un défaut trouvé en test manuel
  n'apparaît dans aucun commit : s'il n'est pas nommé ici, il sera absent du rapport.]
---
 
## Le lecteur
 
Le destinataire est le **client** : expert industriel des fichiers Excel, habitué aux classeurs
complexes des années 2000. Ni développeur, ni informaticien.
 
- **Son vocabulaire Excel et métier est le sien : l'employer, précisément.** Onglet, cellule, case
  fusionnée, mise en forme, colonne, case à cocher, classeur. Une paraphrase vague sera moins claire
  qu'un terme exact. Ce qui est banni, c'est le vocabulaire du **métier du logiciel**.
- **Il cesse de lire au-delà d'une certaine longueur.** Une page maximum.
Adresse directe pour ce qui lui appartient : « vos fichiers », « vos dossiers ».
 
---
 
## Consigne pour Claude Code
 
Mission de lecture et de rédaction. **Aucun fichier de code modifié.**
 
Sources : journal des commits, tickets livrés, documents d'état. **Ne pas les recopier** : ce sont
des sources, pas un plan.
 
### Ton
 
**C'est un compte rendu.** On rapporte ce qui a été fait. On ne justifie pas, on ne rassure pas, on
ne met pas en valeur.
 
- Pas de justification d'un choix, d'un retard ou d'un incident.
- Pas d'effet d'annonce, pas de tournure emphatique, pas de superlatif.
- Pas de commentaire sur la qualité du travail fourni.
- Une période peu spectaculaire donne un compte rendu court. C'est le résultat attendu, pas un
  problème à compenser.
### Règle de sélection
 
Chaque élément rapporté répond à : **qu'est-ce que le lecteur peut constater, ou pourquoi
devrait-il s'en soucier ?** Ce qui n'a pas d'effet perceptible tient en une ligne, ou ne figure pas.
 
Deux choses méritent d'être dites au-delà des fonctionnalités livrées :
- **le principe retenu quand il évite un redéveloppement futur** (ex. : la lecture d'un type de
  fichier est paramétrable, pas figée dans le programme) ;
- **le travail mené sur ses fichiers réels**, quand c'est le cas.
### Interdits
 
- Numéros de lots, noms de fichiers, de classes, de routes, de bibliothèques, de tables.
- Anglicismes techniques.
- Jargon de méthode : TDD, architecture, migration, tickets, refactorisation, dette technique.
- Promesse de date non engagée.
### Indicateurs chiffrés
 
Jamais comme accomplissement. Admis **au plus une fois**, reformulé en garantie pour le client :
« plus de 600 contrôles automatiques protègent le service contre une régression involontaire lors
des évolutions futures ». Si la reformulation ne vient pas naturellement, retirer le chiffre.
 
### Structure
 
**Titre** — `AM-OXO-ETL — Compte rendu d'avancement ([période])`
 
**Ouverture, deux phrases** — ce qu'annonçait le rapport précédent, puis ce à quoi la période a été
consacrée.
 
**Deux à quatre sections**, regroupées par effet pour l'utilisateur, jamais par découpage technique.
Titres descriptifs. Si la période correspond à des phases déjà nommées auprès du client, reprendre
cette numérotation. Puces concrètes, une idée par puce. Un point d'attention peut être placé dans sa
section, puis repris en « À confirmer ».
 
**Point de vigilance** *(si la période en comporte un)* — ce qui a été constaté, ce qui est fait.
Sans justification ni formule rassurante.
 
**En résumé** — deux ou trois phrases.
 
**À confirmer** — décisions attendues du client uniquement. Chacune indique ce qu'elle débloque.
Si rien n'est attendu, l'écrire.
 
**Prochaines étapes** — sans promesse de délai.
 
### Livré, en cours, en attente
 
Distinguer les trois. Un travail en cours au moment de la rédaction **n'est pas annoncé comme
acquis** : l'existence d'un ticket ne prouve pas la livraison. Vérifier l'état réel dans le
repository.
 
### Glossaire imposé
 
| Ne jamais écrire | Écrire |
| :--- | :--- |
| profil d'import / d'export | profil de lecture / profil d'écriture |
| fichier source OXO, dossier de MAD | le dossier, vos dossiers |
| fichier cible, workbook généré | le fichier de sortie |
| feuille, worksheet | **onglet** |
| merged cells | cases fusionnées |
| API, endpoint, microservice, pipeline | le service, le traitement |
| Blazor, back-office, interface web | les écrans d'administration |
| AvancementRecette, application legacy | l'ancienne application |
| utilisateur avec le rôle Admin | administrateur |
| utilisateur sans rôle | utilisateur |
| authentification, Identity, rôles, autorisation | l'accès à l'application |
| déploiement, mise en production | la mise en service |
 
Un terme nouveau se met entre guillemets à sa première occurrence, puis s'emploie sans.
**Tout terme traduit hors de ce tableau : le signaler en fin de réponse, hors du compte rendu.**
 
---
 
## Format de sortie
 
- Un fichier, `compte-rendu-avancement-[AAAA-MM-JJ].md`
- **Une page.** Si ça déborde, retirer des éléments — ne pas comprimer la formulation.
- Phrases courtes, voix active. Aucun renvoi à un document technique.
## Check-list
 
- [ ] Ton de compte rendu : aucune justification, aucune mise en valeur
- [ ] Un lecteur non informaticien comprend chaque phrase
- [ ] Vocabulaire Excel et métier précis ; vocabulaire du logiciel absent
- [ ] Aucun numéro de lot, nom de fichier, de classe ou de bibliothèque
- [ ] Au plus un indicateur chiffré, formulé comme une garantie
- [ ] Livré / en cours / en attente distingués
- [ ] Point de vigilance présent, ou période sans
- [ ] « À confirmer » : décisions du client uniquement
- [ ] Glossaire respecté ; termes hors tableau signalés
- [ ] Une page
