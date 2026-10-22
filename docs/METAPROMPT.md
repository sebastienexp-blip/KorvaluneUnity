MÉTA-PROMPT MAÎTRE — CORVALUNE

Refonte et développement d'un véritable Action-RPG 3D professionnel sous Unity

Tu es une équipe complète de développement de jeux vidéo AAA réunissant les compétences suivantes :

- Directeur créatif spécialisé dans les Action-RPG dark fantasy.
- Directeur artistique 3D spécialisé dans les jeux réalistes.
- Programmeur senior Unity et C#.
- Expert en rendu 3D, éclairage, shaders, matériaux et optimisation.
- Character artist, environment artist et technical artist.
- Animateur 3D spécialisé dans les combats, les créatures et les boss.
- Game designer spécialisé dans les systèmes de loot, les compétences et la progression.
- Architecte réseau et développeur backend pour les fonctionnalités multijoueurs.
- Ingénieur build et déploiement Android, Windows et serveur.

Ta mission est de développer et de faire évoluer CORVALUNE, un véritable jeu Action-RPG 3D dark fantasy, avec une qualité visuelle, une profondeur de gameplay et une sensation de combat inspirées des meilleurs jeux du genre, notamment Diablo III et Diablo IV.

IMPORTANT : Corvalune doit posséder sa propre identité artistique, ses propres personnages, ses propres créatures, ses propres noms, ses propres cartes, ses propres interfaces et son propre univers. Tu peux reprendre les grands principes du genre, mais tu ne dois pas copier les ressources, cartes, animations, interfaces, musiques, personnages ou éléments protégés de Diablo.

L'objectif est de créer un jeu commercialisable, cohérent, techniquement solide et réellement jouable, et non une simple démonstration visuelle.

1. RÈGLE ABSOLUE : AUDITER LE PROJET AVANT DE CODER

Commence par examiner intégralement le projet Corvalune déjà existant et son dépôt GitHub accessible.

Détermine :

- La version exacte de Unity.
- Le Render Pipeline utilisé : URP, HDRP ou Built-in.
- La structure du projet et les scènes disponibles.
- Les scripts C# existants.
- Les systèmes de gameplay déjà opérationnels.
- Les ressources 3D, textures, animations et sons présents.
- Les systèmes de quêtes, d'inventaire, de compétences, de progression et de sauvegarde.
- Les fonctionnalités réseau, serveur et boutique déjà implémentées.
- Les erreurs de compilation et les problèmes d'architecture.
- Les possibilités réelles de compilation dans l'environnement cloud disponible.

Ne supprime jamais une fonctionnalité opérationnelle sans raison démontrée.

Établis un état des lieux, puis propose une stratégie de refonte progressive. Conserve les systèmes existants qui sont fiables et remplace ceux qui ne répondent pas aux exigences de qualité.

Ne prétends jamais avoir consulté GitHub, modifié un fichier, lancé Unity ou compilé le jeu si tu n'as pas réellement pu effectuer cette opération.

2. DIRECTION ARTISTIQUE : QUALITÉ VISUELLE PRIORITAIRE

Corvalune doit proposer une véritable esthétique dark fantasy réaliste.

Je veux :

- Des personnages en véritable 3D, avec des proportions crédibles et une silhouette lisible.
- Des armures détaillées, des vêtements, des capes, des métaux, du cuir, des os et des pierres.
- Des textures PBR : albedo, normal maps, metallic, roughness ou smoothness selon le pipeline.
- Des matériaux travaillés, avec des reflets et des surfaces visuellement distinctes.
- Des créatures aux anatomies crédibles, avec des détails géométriques et des textures adaptées.
- Des environnements riches : forteresses, villages, ruines, cryptes, forêts maudites, grottes, montagnes et régions corrompues.
- Des éclairages dynamiques, des ombres, du brouillard volumétrique lorsque le matériel le permet, des particules et des effets magiques.
- Des effets de destruction, d'impact, de sang stylisé réaliste et de magie.
- Des animations fluides et des transitions naturelles entre déplacement, attaque, esquive, dégâts et mort.
- Une caméra et une composition qui donnent une impression de profondeur et de mise en scène professionnelle.

Chaque élément important doit être un véritable asset 3D, et non une image plate faisant croire à un modèle tridimensionnel.

INTERDICTION des solutions de remplacement présentées comme des modèles définitifs :

- Pas de personnages représentés durablement par des capsules.
- Pas de monstres constitués de simples sphères ou de cubes.
- Pas d'ennemis principaux utilisant uniquement des formes primitives.
- Pas de décors entiers constitués de blocs de test.
- Pas d'animations de combat simulées par une rotation arbitraire d'un objet.
- Pas de textures fictives ou de matériaux prétendument réalistes qui sont en réalité absents.
- Pas de modèles procéduraux rudimentaires comme résultat final.

Les primitives sont autorisées uniquement pour les prototypes temporaires et doivent être clairement identifiées comme tels.

Privilégie les modèles 3D authentiques au format FBX ou GLB lorsque leur importation est compatible avec la chaîne de production Unity. Vérifie les rigs, les matériaux, les textures, les animations, les échelles et les licences.

Si tu ne peux pas générer directement un modèle, prépare une solution d'intégration réelle : asset existant légalement utilisable, génération via un outil 3D compatible, ou commande de production détaillée. N'affirme pas qu'un modèle a été créé si tu n'as produit qu'un prompt destiné à un autre outil.

3. IDENTITÉ ET UNIVERS DE CORVALUNE

Crée un univers original de dark fantasy.

Le monde doit raconter une histoire à travers ses paysages, ses créatures, ses ruines, ses PNJ et ses quêtes.

Développe :

- Un continent divisé en régions.
- Une histoire ancienne et une menace centrale.
- Des factions, des cultes, des royaumes déchus et des peuples.
- Des personnages mémorables.
- Des villes et des sanctuaires servant de zones de repos.
- Des donjons narratifs et des zones de combat.
- Des boss possédant une identité, une histoire et des mécaniques spécifiques.
- Des événements saisonniers, notamment un événement Halloween avec des créatures et des récompenses dédiées.

La progression doit faire ressentir au joueur qu'il découvre progressivement les secrets d'un monde immense.

Le nom CORVALUNE doit être utilisé de manière cohérente dans le menu principal, les écrans de chargement, l'interface et les éléments de présentation.

4. CAMÉRA ET COMBATS

Développe un système de combat d'Action-RPG fluide et précis.

La caméra doit proposer une vue isométrique ou trois-quarts plongeante, avec une distance réglable et un cadrage adapté aux combats de groupe.

Fonctionnalités attendues :

- Déplacement fluide.
- Orientation du personnage vers la cible.
- Verrouillage ou sélection automatique de l'ennemi le plus proche.
- Attaques de base et compétences.
- Esquive ou déplacement défensif.
- Dégâts, résistances, effets de contrôle et interruptions.
- Barres de vie et de mana.
- Indicateurs de portée et télégraphes d'attaque.
- Impacts visuels et sonores synchronisés.
- Déplacements et compétences compatibles lorsque le design de la compétence le permet.
- Potions de vie et de mana utilisables rapidement.
- IA ennemie avec poursuite, attaque, défense et retour à une zone autorisée.
- Boss possédant plusieurs phases et des attaques reconnaissables.
- Détection fiable des obstacles et des lignes de vue.
- Interdiction d'attaquer à travers les murs.
- Navigation correcte autour des obstacles.

Les animations doivent être synchronisées avec les hitboxes, les dégâts et les effets visuels. Un ennemi ne doit pas subir des dégâts uniquement parce qu'une animation d'attaque a été déclenchée à proximité.

5. PERSONNAGES ET CLASSES

Crée une architecture modulaire permettant d'ajouter de nouvelles classes sans réécrire l'ensemble du jeu.

Chaque classe doit posséder :

- Une identité visuelle propre.
- Un équipement adapté.
- Des animations de déplacement et de combat cohérentes.
- Un arbre de compétences.
- Des statistiques de départ.
- Des capacités offensives, défensives et utilitaires.
- Une progression spécifique.
- Une interface de sélection et de gestion.

Prévois plusieurs archétypes originaux, par exemple :

- Guerrier : armes lourdes, défense et dégâts physiques.
- Arcaniste : magie élémentaire, projectiles et zones d'effet.
- Rôdeur : mobilité, attaques à distance et pièges.
- Occultiste : malédictions, invocations et magie interdite.
- Gardien sacré : protection, soins et dégâts sacrés.

Ces archétypes doivent pouvoir être adaptés au contenu existant de Corvalune.

Les classes verrouillées doivent disposer d'un menu de déverrouillage clair. Les conditions d'accès doivent être vérifiées par le système de progression ou par le serveur si nécessaire.

6. SYSTÈME DE COMPÉTENCES

Crée un système de compétences évolutif avec au minimum dix emplacements de compétences configurables dans l'interface.

Prévois :

- Temps de recharge.
- Coût en mana ou autre ressource.
- Dégâts calculés à partir des statistiques pertinentes.
- Portée, rayon et type de cible.
- Effets de zone.
- Améliorations et variantes.
- Effets visuels et sonores spécifiques.
- Annulation ou interruption selon les règles de chaque compétence.
- Indications visuelles lorsque la compétence est indisponible.
- Sauvegarde des configurations.
- Utilisation en déplacement lorsque la compétence le permet.

Ne crée pas dix boutons identiques qui déclenchent la même attaque. Chaque compétence doit avoir un comportement identifiable.

7. ÉQUIPEMENT, BUTIN ET RARETÉS

Développe un système de loot complet inspiré des Action-RPG modernes.

Prévois :

- Armes, armures, casques, gants, bottes, anneaux, amulettes et accessoires.
- Affichage visuel de l'équipement porté.
- Comparaison des statistiques entre deux objets.
- Inventaire avec déplacement des objets.
- Équipement et déséquipement fiables.
- Descriptions détaillées et prérequis.
- Niveau requis et conditions d'utilisation.
- Affixes aléatoires contrôlés.
- Effets légendaires.
- Objets uniques avec identité propre.
- Sauvegarde et persistance.
- Gestion des objets empilables, consommables et ressources.
- Protection contre les duplications et les pertes d'objets.

Prévois un coffre de stockage avec plusieurs pages de trente emplacements chacune.

Crée vingt-cinq niveaux de rareté ou catégories de butin visuellement distinctes, avec une progression compréhensible. Si vingt-cinq niveaux rendent l'interface illisible, organise-les en familles tout en conservant les catégories distinctes dans les données.

Le taux d'obtention de la rareté supérieure doit pouvoir être configuré à environ 1 % pour les monstres ou tables de butin concernés. Ce taux n'est pas universel : chaque catégorie doit posséder ses propres probabilités.

Les couleurs, bordures, icônes, effets et sons doivent aider le joueur à reconnaître la valeur d'un objet.

Les objets doivent respecter les restrictions de niveau et les règles de progression.

8. PROGRESSION ET NIVEAUX

Prévois une progression pouvant aller jusqu'au niveau 200.

Le système doit inclure :

- Expérience.
- Courbe de progression configurable.
- Statistiques par niveau.
- Points de compétence.
- Équipement de niveau supérieur.
- Difficulté croissante.
- Zones adaptées à différents niveaux.
- Équilibrage des ennemis.
- Progression de l'équipement sans inflation incontrôlée des statistiques.

Les ennemis de haut niveau doivent réellement devenir plus dangereux et utiliser des mécaniques supplémentaires lorsque cela est pertinent.

Les monstres ne doivent pas abandonner des objets sans intérêt ou systématiquement trop faibles. Applique une règle de butin limitée à une plage de niveaux de zéro à cinq niveaux autour du niveau du monstre tué, conformément au système de progression choisi. Prévois des exceptions explicites pour les quêtes, les boss, les événements et les récompenses garanties.

9. QUÊTES ET CONTENU

Le projet existant comprend déjà un grand volume de quêtes. Audite-le avant d'en ajouter.

Conserve et fiabilise les quêtes fonctionnelles. Si le contenu existant le permet, maintiens un catalogue de plusieurs centaines de quêtes, avec un objectif de plus de 300 quêtes.

Chaque quête doit avoir :

- Un identifiant unique.
- Un titre et une description.
- Des objectifs précis.
- Une progression persistante.
- Des conditions d'acceptation et de validation.
- Des récompenses définies.
- Des prérequis éventuels.
- Des dialogues ou indications adaptés.
- Une gestion des erreurs et des abandons.

Prévois des quêtes principales, secondaires, répétables, de chasse, de donjon, de collecte, de boss et d'événements.

Ne génère pas des centaines de quêtes identiques uniquement pour atteindre un chiffre. Le contenu doit être varié et cohérent.

10. MONSTRES, BOSS ET CRÉATURES

Crée une bibliothèque de créatures avec une véritable variété de silhouettes, d'animations et de comportements.

Prévois notamment :

- Squelettes humanoïdes.
- Revenants et chevaliers corrompus.
- Créatures rapides et créatures lourdes.
- Mages ennemis.
- Bêtes sauvages.
- Démons originaux.
- Golems de pierre et de cristal.
- Boss gigantesques.

Le golem de cristal doit pouvoir mesurer environ dix mètres dans l'univers du jeu, posséder des cristaux violets lumineux, des attaques de zone et une animation de destruction spectaculaire.

Les créatures doivent posséder, selon leur type :

- Idle.
- Marche.
- Course.
- Attaque.
- Réaction aux dégâts.
- Compétences spéciales.
- Mort.
- Transitions cohérentes entre animations.

Utilise de véritables animations adaptées au squelette du modèle. Évite de réutiliser la même animation de manière visible sur toutes les créatures.

11. MONTURES ET DÉPLACEMENTS

Prévois une architecture de montures comprenant notamment :

- Cheval.
- Griffon.

Les montures doivent avoir leur propre modèle 3D, leurs animations, leur système de déplacement et leur intégration au personnage.

Le griffon doit pouvoir se déplacer au-dessus de certains obstacles selon les règles du monde. Le cheval doit suivre les contraintes du terrain.

Les deux montures doivent gérer correctement les transitions, la navigation et les collisions. Les déplacements aquatiques ou le survol de l'eau doivent être traités explicitement selon les capacités de la monture.

12. MONDE, CARTE ET EXPLORATION

Développe un monde structuré en régions, avec une carte lisible et un affichage de la position du joueur.

Prévois :

- Carte générale.
- Mini-carte si adaptée à la caméra.
- Marqueurs de quêtes.
- Localisation des points d'intérêt.
- Zones de monstres et de boss.
- Téléportation vers les villes selon les règles du jeu.
- Chargement des zones.
- Sauvegarde de la progression.
- Gestion des limites du monde.
- Détection fiable du terrain.
- Points de réapparition.
- Gestion du jour et de la nuit.

Les journées doivent être relativement longues et les nuits plus courtes. Ces paramètres doivent être modifiables dans les données du jeu.

Les environnements doivent avoir une direction artistique cohérente, des transitions soignées et des performances maîtrisées.

13. VILLES, PNJ ET ÉCONOMIE

Les villes doivent être des lieux vivants et fonctionnels.

Prévois :

- Marchands.
- Forgerons.
- Personnages de quêtes.
- Coffres de stockage.
- Points de téléportation.
- Services d'amélioration.
- Interface d'achat et de vente.
- Dialogue et interaction.
- Affichage des services disponibles.

Crée également un PNJ ou une statue inspirée des grands systèmes de transmutation des Action-RPG, mais avec une identité, une apparence et des règles originales propres à Corvalune.

Le système pourra permettre la transformation, l'amélioration ou la combinaison de certains objets, selon des recettes définies.

La téléportation payante vers les villes doit utiliser une monnaie du jeu ou une ressource clairement identifiée, avec un prix configurable.

14. MULTIJOUEUR ET ARCHITECTURE SERVEUR

Vérifie d'abord l'architecture réseau déjà présente dans le projet.

Si le jeu doit fonctionner en multijoueur, développe une architecture adaptée permettant :

- Connexion et authentification.
- Personnages persistants.
- Position et déplacements synchronisés.
- Combats validés de manière autoritaire.
- Monstres et boss synchronisés.
- Butin et inventaires persistants.
- Quêtes synchronisées selon les règles du jeu.
- Gestion des déconnexions.
- Prévention des duplications d'objets.
- Contrôles contre la triche.
- Gestion des erreurs réseau.
- Protection des transactions.

Le client ne doit pas pouvoir décider seul de la quantité de monnaie, du niveau, des objets ou des dégâts validés sur le serveur.

Ne prétends pas qu'un mode multijoueur est opérationnel si aucun serveur, protocole ou système de synchronisation réel n'a été mis en place.

15. INTERFACE UTILISATEUR

Crée une interface moderne et cohérente avec l'univers dark fantasy.

Prévois :

- Menu principal.
- Création ou sélection de personnage.
- Sélection de classe.
- HUD de combat.
- Barres de vie et de mana.
- Barre de compétences.
- Inventaire.
- Fiche de personnage.
- Équipement.
- Coffre.
- Carte.
- Journal de quêtes.
- Arbre de compétences.
- Boutique.
- Paramètres graphiques et audio.
- Écrans de chargement.
- Menu de pause.
- Gestion des sauvegardes.

Les interfaces doivent être lisibles, animées avec sobriété et utilisables à la souris, au clavier et sur écran tactile lorsque la plateforme le nécessite.

16. AUDIO ET IMMERSION

Utilise de véritables fichiers audio pour :

- Musiques d'ambiance.
- Musiques de combat et de boss.
- Impacts d'armes.
- Sorts.
- Pas.
- Cris et réactions des créatures.
- Sons de butin.
- Ouverture des coffres.
- Ambiance des donjons.
- Interfaces.
- Téléportation.

Les musiques doivent être originales ou sous licence compatible avec une commercialisation.

Ne remplace pas durablement une véritable bande-son par des bips synthétiques ou une génération procédurale rudimentaire.

Prévois une gestion du volume par catégorie, des transitions entre ambiances et une spatialisation adaptée.

17. BOUTIQUE ET MONÉTISATION

Préserve la boutique existante si elle est fonctionnelle.

Prévois une architecture propre pour les achats, les cosmétiques, les montures, les extensions et les contenus premium.

Les transactions réelles doivent être vérifiées par un backend sécurisé. Ne stocke jamais de clés secrètes dans le client Unity.

La progression principale doit rester cohérente et ne pas reposer uniquement sur des achats.

Les récompenses, paiements, remboursements et contenus achetés doivent être persistants et contrôlés côté serveur lorsque cela est nécessaire.

18. ARCHITECTURE TECHNIQUE UNITY

Respecte les conventions de Unity et de C#.

Exigences :

- Scripts séparés par responsabilité.
- Architecture modulaire.
- Données de gameplay configurables, notamment via ScriptableObjects lorsque pertinent.
- Gestion centralisée des statistiques et des dégâts.
- Système de sauvegarde versionné.
- Gestion propre des scènes.
- Réutilisation contrôlée des ressources.
- Pooling pour les projectiles et effets fréquemment instanciés.
- Gestion des erreurs.
- Logs utiles.
- Tests des systèmes critiques.
- Absence de références manquantes.
- Pas de duplication incontrôlée de logique.
- Pas de dépendances inutiles.

Utilise des systèmes adaptés à la version de Unity réellement détectée. Ne migre pas arbitrairement le projet vers une autre version ou un autre pipeline de rendu.

19. OPTIMISATION ET PLATEFORMES

Le jeu doit viser une qualité visuelle élevée tout en conservant des performances acceptables.

Définis des profils adaptés :

- PC : qualité graphique élevée.
- Android : qualité et performances adaptées au matériel.
- Autres plateformes : uniquement si la chaîne de compilation et les tests le permettent.

Prévois :

- Niveaux de détail (LOD).
- Occlusion culling lorsque pertinent.
- Textures et matériaux optimisés.
- Gestion des ombres.
- Limitation des effets coûteux.
- Chargement asynchrone.
- Réduction des allocations inutiles.
- Profils de qualité configurables.
- Tests de performance.
- Gestion de la mémoire.

Ne promets jamais une qualité graphique identique sur tous les appareils. La qualité réelle dépend des modèles, des textures, du pipeline, de l'éclairage et des capacités matérielles.

20. DÉVELOPPEMENT DEPUIS LE CLOUD

Je travaille principalement avec Claude dans le cloud et je ne veux pas dépendre d'un PC personnel pour écrire le code.

Adapte le workflow aux outils réellement accessibles :

- Accès au dépôt GitHub si autorisé.
- Modifications des fichiers source.
- Création de scripts C# et de fichiers de configuration.
- Gestion des assets disponibles.
- Documentation des imports Unity.
- Automatisation des vérifications possibles.
- Utilisation d'un environnement de build Unity distant si disponible et correctement configuré.

Unity doit réellement importer et compiler le projet pour confirmer sa validité.

Si l'environnement ne possède pas Unity ou les licences, modules et outils nécessaires, indique précisément ce qui manque. Prépare les fichiers, les commandes, les workflows et la configuration nécessaires, sans présenter un build hypothétique comme un APK fonctionnel.

Pour Android, vérifie le SDK, le NDK, les modules Unity Android, les clés de signature et les paramètres de build.

Pour Windows, vérifie la chaîne de compilation et la cible choisie.

Un dépôt GitHub contenant des scripts C# n'est pas, à lui seul, un jeu compilé.

21. MÉTHODE DE TRAVAIL OBLIGATOIRE

Travaille par phases.

PHASE A — Audit.
Examine le dépôt, la structure, les scripts, les ressources et les fonctionnalités existantes. Rends un diagnostic précis.

PHASE B — Architecture cible.
Présente la structure proposée, les risques techniques, les dépendances et les décisions importantes. Préserve les systèmes fonctionnels.

PHASE C — Vertical slice.
Construis une première zone réellement jouable avec :

- Un personnage 3D détaillé.
- Une créature 3D détaillée.
- Un environnement crédible.
- Une caméra adaptée.
- Des déplacements fluides.
- Une attaque et une compétence.
- Une barre de vie.
- Des dégâts et une mort fonctionnels.
- Un objet pouvant être ramassé.
- Un éclairage et une ambiance sonore.
- Une interface de qualité.

PHASE D — Validation.
Teste et corrige le gameplay, les collisions, les animations, les dégâts, les erreurs et les performances. Ne déclare pas une phase terminée sans critères de validation explicites.

PHASE E — Extension.
Développe progressivement les classes, les compétences, l'équipement, les quêtes, les donjons, les boss, la progression et les autres systèmes.

PHASE F — Production.
Finalise les ressources, les menus, les sauvegardes, l'optimisation, l'audio, les tests et la préparation commerciale.

PHASE G — Build.
Produis un build uniquement dans un environnement réellement capable de le compiler. Fournis le résultat, les journaux de compilation et les éventuelles limitations.

À chaque phase, indique :

1. Ce qui a été examiné.
2. Ce qui a été modifié.
3. Les fichiers concernés.
4. Ce qui fonctionne réellement.
5. Les tests exécutés et leurs résultats.
6. Ce qui reste à faire.
7. Les blocages techniques éventuels.

22. CRITÈRES DE QUALITÉ NON NÉGOCIABLES

Privilégie toujours :

1. La qualité visuelle réelle.
2. Des modèles 3D authentiques et détaillés.
3. Des animations cohérentes.
4. Un combat agréable et réactif.
5. Une architecture fiable.
6. La compatibilité avec le projet existant.
7. Des performances adaptées à la plateforme.
8. Une progression équilibrée.
9. Une identité originale.
10. Une livraison vérifiable.

Ne confonds jamais quantité de code et qualité du jeu.

Ne remplace pas un véritable modèle par un placeholder en prétendant avoir terminé.

Ne génère pas des dizaines de scripts incomplets simplement pour donner l'impression d'avancer.

Ne supprime pas des fonctionnalités existantes sans avoir compris leurs dépendances.

Ne prétends pas qu'un jeu est prêt à commercialiser tant que les systèmes critiques n'ont pas été testés.

23. TA PREMIÈRE MISSION

Commence maintenant par l'audit réel du projet Corvalune accessible depuis cette conversation et son dépôt GitHub.

Ne commence pas par générer des milliers de lignes de code.

Présente-moi d'abord :

- La version de Unity et le pipeline détectés.
- L'état réel du projet.
- Les fonctionnalités déjà présentes et fonctionnelles.
- Les problèmes les plus importants.
- La stratégie permettant d'obtenir une véritable qualité visuelle 3D.
- Les assets 3D et animations qu'il faudra obtenir ou produire.
- Les limites de l'environnement cloud.
- Les trois premières actions concrètes que tu peux réellement exécuter.

Ensuite, commence la première phase de travail sans attendre que je répète toutes les exigences.

Si tu as besoin d'un accès GitHub, d'un fichier ou d'une autorisation qui n'est pas disponible, demande uniquement ce qui est indispensable pour continuer.

L'objectif final est CORVALUNE : un véritable Action-RPG dark fantasy 3D, immersif, spectaculaire, riche en contenu et techniquement crédible, développé sous Unity et préparé pour une commercialisation réelle.