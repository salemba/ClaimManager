# Plan de Déploiement et de Reprise d'Activité (PRA) - ClaimManager

**Titre :** Déploiement de la version 3.3 et Test de Charge 12 000 Utilisateurs
**Date :** 22 Mai 2026
**Auteur :** GitHub Copilot
**Statut :** Proposition
**Branche Git de référence :** `feat-J4-HA`

## 1. Objectifs

Ce plan a pour but de décrire les procédures pour :
1.  **Déployer** en production les nouvelles fonctionnalités de la branche `feat-J4-HA`.
2.  **Valider** la performance et la stabilité de l'application avec un test de charge simulant 12 000 utilisateurs concurrents.
3.  **Assurer** la continuité de service via un Plan de Reprise d'Activité (PRA) en cas d'échec critique du déploiement ou d'incident majeur.

## 2. Prérequis au Déploiement

Avant de démarrer le déploiement, les conditions suivantes doivent être remplies :
-   [ ] La branche `feat-J4-HA` a été entièrement testée et validée en environnement de pré-production (Staging).
-   [ ] Toutes les régressions ont été corrigées.
-   [ ] La version a été figée (`code freeze`) et une `release tag` a été créée dans Git (ex: `v3.3.0`).
-   [ ] L'infrastructure de production (serveurs, base de données, load balancer) est prête.
-   [ ] L'environnement de test de charge, isolé mais identique à la production, est provisionné.
-   [ ] Les outils de monitoring et d'alerting (ex: Prometheus, Grafana, Sentry) sont configurés pour surveiller la nouvelle version.

## 3. Plan de Déploiement (Séquence des opérations)

**Fenêtre de maintenance estimée :** 2 heures (ex: Samedi de 22h00 à 00h00)

| Étape | Action                                                 | Responsable      | Outil(s)             | Validation                                            |
| :---- | :----------------------------------------------------- | :--------------- | :------------------- | :---------------------------------------------------- |
| 1     | **Communication : Début de la maintenance**            | Chef de projet   | E-mail, Slack        | Notification envoyée à toutes les parties prenantes   |
| 2     | **Activation du mode maintenance de l'application**    | Équipe Ops       | Nginx/Load Balancer  | Le site affiche une page de maintenance             |
| 3     | **Sauvegarde complète de la base de données (Backup)** | Équipe Ops       | `pg_dump`            | Fichier de backup créé, horodaté et stocké en lieu sûr |
| 4     | **Application des migrations de la base de données**   | Lead Développeur | `dotnet ef database update` | Les nouvelles tables/colonnes sont présentes        |
| 5     | **Déploiement des artefacts de l'API (.NET)**          | Équipe Ops       | CI/CD (Jenkins, etc.) | L'API v3.3.0 est en ligne (vérification du endpoint de version) |
| 6     | **Déploiement des fichiers du Frontend (React)**       | Équipe Ops       | CI/CD (Jenkins, etc.) | Les nouveaux assets sont servis par le serveur web    |
| 7     | **Redémarrage des services applicatifs**               | Équipe Ops       | `systemctl` / `docker` | Les services redémarrent sans erreur dans les logs  |
| 8     | **Tests de fumée (Smoke Tests) internes**              | Équipe QA        | Script de tests      | Connexion, accès au dashboard, création d'un sinistre |
| 9     | **Désactivation du mode maintenance**                  | Équipe Ops       | Nginx/Load Balancer  | Le site est de nouveau accessible publiquement      |
| 10    | **Surveillance active post-déploiement**               | Tous             | Grafana, Sentry      | Aucune hausse anormale du CPU/RAM ou du taux d'erreurs |
| 11    | **Communication : Fin de la maintenance**              | Chef de projet   | E-mail, Slack        | Notification de succès envoyée                      |

---

## 4. Plan de Test de Charge (12 000 Utilisateurs)

Ce test sera réalisé sur l'environnement de **pré-production**, après le déploiement sur celui-ci et avant le déploiement en production.

| Étape | Action                                                        | Outil(s)           | Critères de succès                                                                                              |
| :---- | :------------------------------------------------------------ | :----------------- | :-------------------------------------------------------------------------------------------------------------- |
| 1     | **Définition des scénarios utilisateurs**                     | -                  | Scénarios créés : Connexion, Consultation Dashboard, Recherche, Création de sinistre, Mise à jour de sinistre. |
| 2     | **Scripting des scénarios de test**                           | JMeter / Gatling / K6 | Scripts validés avec 1 utilisateur.                                                                           |
| 3     | **Exécution du test de charge progressif**                    | JMeter / Gatling / K6 | Montée en charge de 0 à 12 000 utilisateurs sur 30 minutes.                                                    |
| 4     | **Maintien de la charge nominale**                            | JMeter / Gatling / K6 | Maintien de 12 000 utilisateurs pendant 1 heure.                                                              |
| 5     | **Analyse des résultats**                                     | Grafana, JMeter    | - **Temps de réponse moyen < 500ms**<br/>- **Taux d'erreur < 0.1%**<br/>- **Utilisation CPU < 80%**          |

---

## 5. Plan de Reprise d'Activité (PRA) - Procédure de Rollback

Ce plan est à activer **uniquement** en cas d'échec critique pendant la fenêtre de déploiement (ex: Étape 8 échoue, ou une hausse massive d'erreurs à l'étape 10).

| Étape | Action                                                  | Responsable      | Objectif                                                  |
| :---- | :------------------------------------------------------ | :--------------- | :-------------------------------------------------------- |
| 1     | **Décision de Rollback**                                | Chef de Projet   | Confirmer l'échec critique et la nécessité de revenir en arrière. |
| 2     | **Communication : Incident et activation du PRA**       | Chef de Projet   | Informer les parties prenantes de l'activation du plan. |
| 3     | **Ré-activation du mode maintenance (si désactivé)**    | Équipe Ops       | Isoler l'application du trafic utilisateur.               |
| 4     | **Restauration de la base de données depuis la sauvegarde** | Équipe Ops       | Restaurer la BDD à son état exact avant l'étape 4 du déploiement. |
| 5     | **Déploiement de la version précédente de l'application (API & Frontend)** | Équipe Ops | Redéployer les artefacts de la release précédente (ex: v3.2.0). |
| 6     | **Redémarrage des services**                            | Équipe Ops       | Assurer que l'ancienne version démarre correctement.      |
| 7     | **Tests de fumée sur la version restaurée**             | Équipe QA        | Valider que l'application est de nouveau fonctionnelle.   |
| 8     | **Désactivation du mode maintenance**                   | Équipe Ops       | Rendre l'application de nouveau disponible.               |
| 9     | **Communication : Fin d'incident, service restauré**    | Chef de Projet   | Informer de la résolution de l'incident.                  |
| 10    | **Analyse post-mortem de l'incident**                   | Tous             | Comprendre la cause de l'échec pour ne pas le reproduire. |

---
