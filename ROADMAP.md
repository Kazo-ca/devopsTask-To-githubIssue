# KazoAdoSync – Feuille de Route & État de Production

Ce document trace l'état d'avancement du projet et les tâches restantes avant une mise en production complète.

---

## ✅ Ce qui est en place

### Infrastructure & Automation
- [x] **Workflow de sync planifié** (`.github/workflows/ado-sync.yml`) – cron toutes les 10 min + déclenchement manuel avec option `dry-run`
- [x] **Déploiement GitHub Pages** (`.github/workflows/deploy-configurator.yml`) – déploie `/configurator` automatiquement sur push vers `main`
- [x] **Publication de release** (`.github/workflows/publish-action.yml`) – compile et publie les binaires linux/win sur push d'un tag `v*.*.*`

### CLI (Program.cs)
- [x] Lecture de la configuration YAML (`config.yml`)
- [x] Connexion Azure DevOps (WIQL) + GitHub (Octokit)
- [x] Polling des work items tagged `AI-Ready` (configurable)
- [x] Extraction HTML → Markdown (ReverseMarkdown)
- [x] Récupération du contexte parent (User Story)
- [x] Titre d'issue avec préfixe `AB#{{WI_ID}}` obligatoire
- [x] Remplacement des placeholders dans le template
- [x] Déduplication par recherche GitHub avant création
- [x] Ajout du tag `IssueCreated` sur le WorkItem ADO
- [x] Commentaire ADO avec l'URL de l'issue GitHub créée
- [x] Mode `--dry-run` (aucune écriture, affichage de ce qui serait fait)
- [x] Gestion d'erreur par item (un échec ne stoppe pas les suivants)

### Action composite (`action.yml`)
- [x] Inputs documentés : `config-path`, `ado-org-url`, `ado-project`, `gh-repo-owner`, `gh-repo-name`, `ado-pat-token`, `gh-pat-token`, `dry-run`
- [x] Utilisable depuis n'importe quel dépôt via `uses: Kazo-ca/devopsTask-To-githubIssue@v1`

### Configurateur Web (`configurator/index.html`)
- [x] Génération du YAML à partir d'un formulaire Tailwind
- [x] Prévisualisation de l'issue GitHub (rendu Markdown)
- [x] Bouton "Copier" le YAML
- [x] Bouton "Télécharger config.yml"
- [x] Section d'aide pour la création des secrets `ADO_PAT_TOKEN` et `GH_PAT_TOKEN`

### Documentation (`README.md`)
- [x] Tableau de référence `config.yml`
- [x] Exemple de workflow consommateur complet
- [x] Tableau des secrets requis
- [x] Guide d'activation de l'intégration `AB#` dans Azure DevOps
- [x] Lien vers le configurateur web (GitHub Pages)
- [x] Documentation du mode `--dry-run`

---

## 🔲 Tâches restantes avant mise en production complète

### 🔴 Priorité Haute

- [ ] **Activer GitHub Pages** dans les paramètres du dépôt (`Settings → Pages → Source: GitHub Actions`) pour que `deploy-configurator.yml` puisse se déployer. Sans cette étape, le workflow échoue.
- [ ] **Créer les secrets** dans le dépôt cible :
  - `ADO_PAT_TOKEN` – Token Azure DevOps (Work Items: Read & Write)
  - `GH_PAT_TOKEN` – Token GitHub Fine-grained (Issues: Read & Write)
  - `ADO_ORG_URL` – URL de l'organisation ADO
  - `ADO_PROJECT` – Nom du projet ADO
- [ ] **Créer le label GitHub** `devops-task` dans le dépôt cible (ou modifier `github_label` dans `config.yml`). Si le label n'existe pas, la création d'issue échoue.
- [ ] **Publier la première release** en créant un tag `v1.0.0` pour que la `action.yml` soit disponible via `@v1`.

### 🟡 Priorité Moyenne

- [ ] **Tests unitaires** : Ajouter un projet de tests (ex: `src/KazoAdoSync.Tests`) avec des tests pour `ReplacePlaceholders`, `GetFieldString`, et la logique de déduplication. Aucune couverture de test n'existe actuellement.
- [ ] **Mise à jour du `config.yml` d'exemple** : Le champ `template_path` pointe vers `"template.md"` mais `spec.md` suggère `.github/issue-template.md`. Choisir une convention et la documenter.
- [ ] **Mettre à jour l'URL GitHub Pages dans README.md** : Remplacer le placeholder `https://kazo-ca.github.io/devopsTask-To-githubIssue/` par l'URL réelle une fois Pages activé.

### 🟢 Améliorations futures

- [ ] **Optimiser `action.yml`** : Utiliser le binaire pré-compilé de la release au lieu de `dotnet run` pour réduire le temps d'exécution (~30-60 secondes de compilation économisées à chaque run).
- [ ] **Gestion du rate-limiting GitHub Search** : L'API de recherche GitHub a des limites strictes (30 req/min). Ajouter un délai ou utiliser l'API GraphQL pour la déduplication à grande échelle.
- [ ] **Logging structuré** : Remplacer `Console.WriteLine` par un logger structuré (ex: `Microsoft.Extensions.Logging`) pour une meilleure observabilité dans GitHub Actions.
- [ ] **Support multi-projets** : Permettre la synchronisation de plusieurs projets ADO dans un seul run.
- [ ] **CHANGELOG.md** : Documenter les changements de version.

---

## 📊 Score de production-readiness estimé

| Domaine | État | Score |
|---------|------|-------|
| Logique métier (CLI) | Fonctionnel | ✅ 9/10 |
| Automation (Workflows) | Fonctionnel | ✅ 9/10 |
| Documentation | Complète | ✅ 9/10 |
| Configuration initiale (secrets, label, Pages) | **Manuelle requise** | ⚠️ 0/10 |
| Tests automatisés | Absents | ❌ 0/10 |
| Observabilité / Logging | Basique | 🟡 4/10 |

**Verdict : Le code est prêt pour un premier déploiement. Les 4 étapes manuelles de la section "Priorité Haute" doivent être effectuées par un administrateur du dépôt avant que la synchronisation ne fonctionne.**
