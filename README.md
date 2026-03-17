# KazoAdoSync – Azure DevOps → GitHub Issue Sync

Outil d'automatisation qui synchronise les **WorkItems** (Type: Task) d'Azure DevOps vers des **Issues** GitHub, avec traçabilité complète via les tags `AB#`.

🌐 **Configurateur web** : [https://kazo-ca.github.io/devopsTask-To-githubIssue/](https://kazo-ca.github.io/devopsTask-To-githubIssue/)
**Presentation** : [https://kazo-ca.github.io/devopsTask-To-githubIssue/presentation.html](https://kazo-ca.github.io/devopsTask-To-githubIssue/presentation.html)
## 🏗️ Architecture

```
├── .github/
│   └── workflows/
│       ├── ado-sync.yml            # ⚡ Sync planifiée (cron toutes les 10 min)
│       ├── deploy-configurator.yml # 🌐 Déploiement GitHub Pages
│       └── publish-action.yml      # 📦 Publication de release
├── src/
│   └── KazoAdoSync.Cli/           # Application console .NET 10
│       ├── Program.cs              # Logique de synchronisation
│       └── KazoAdoSync.Cli.csproj
├── configurator/
│   └── index.html                  # Générateur de config.yml (Tailwind)
├── action.yml                      # Composite Action (usage externe)
├── template.md                     # Modèle d'issue GitHub
├── config.yml                      # Configuration YAML (exemple)
└── spec.md                         # Spécifications du projet
```

## ⚡ Utilisation dans votre dépôt (méthode recommandée)

Téléchargez le fichier [`examples/ado-sync.yml`](examples/ado-sync.yml) (également disponible dans chaque [release](../../releases/latest)) et placez-le dans `.github/workflows/` de votre dépôt.

```yaml
# .github/workflows/ado-sync.yml  ← copié depuis examples/ado-sync.yml
name: ADO → GitHub Issue Sync
on:
  schedule:
    - cron: '*/10 * * * *'   # Toutes les 10 minutes
  workflow_dispatch:
    inputs:
      dry-run:
        type: boolean
        default: false

jobs:
  sync:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: Kazo-ca/devopsTask-To-githubIssue@v1
        with:
          config-path: '.github/ado-config.yml'
          ado-org-url: ${{ secrets.ADO_ORG_URL }}
          ado-project: ${{ secrets.ADO_PROJECT }}
          gh-repo-owner: ${{ github.repository_owner }}
          gh-repo-name: ${{ github.event.repository.name }}
          ado-pat-token: ${{ secrets.ADO_PAT_TOKEN }}
          gh-pat-token: ${{ secrets.GH_PAT_TOKEN }}
          dry-run: ${{ inputs.dry-run || 'false' }}
```

## 🔐 Secrets requis

Créez ces secrets dans **Settings → Secrets and variables → Actions** de votre dépôt :

| Secret | Description |
|--------|-------------|
| `ADO_PAT_TOKEN` | Personal Access Token Azure DevOps avec scope **Work Items – Read & Write** |
| `GH_PAT_TOKEN` | Personal Access Token GitHub (Fine-grained) avec permission **Issues – Read & Write** |
| `ADO_ORG_URL` | URL de votre organisation ADO (ex: `https://dev.azure.com/mon-org`) |
| `ADO_PROJECT` | Nom du projet Azure DevOps |

> **💡 Astuce :** Utilisez le [Configurateur web](https://kazo-ca.github.io/devopsTask-To-githubIssue/) pour générer votre `config.yml` et obtenir les instructions de création des secrets.

## 📋 Référence `config.yml`

| Champ | Description | Défaut |
|-------|-------------|--------|
| `sync_settings.watch_tag` | Tag ADO à surveiller | `"AI-Ready"` |
| `sync_settings.processed_tag` | Tag ajouté après création de l'issue | `"IssueCreated"` |
| `sync_settings.issue_prefix` | Préfixe si le WI n'a pas de titre | `"Devops IA Task"` |
| `sync_settings.default_source_field` | Champ ADO source du contenu (`Notes` ou `Description`) | `"Notes"` |
| `sync_settings.github_label` | Label GitHub à attacher à l'issue | `"devops-task"` |
| `sync_settings.template_path` | Chemin vers le fichier template Markdown | `"template.md"` |
| `env_mapping.github_token_var` | Nom de la variable d'environnement du token GitHub | `"GH_PAT_TOKEN"` |
| `env_mapping.devops_token_var` | Nom de la variable d'environnement du token ADO | `"ADO_PAT_TOKEN"` |

## ⚡ Démarrage rapide (local)

### Prérequis
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Un **Personal Access Token** Azure DevOps (lecture/écriture WorkItems)
- Un **Personal Access Token** GitHub (écriture Issues)

### Installation

```bash
dotnet restore src/KazoAdoSync.Cli/KazoAdoSync.Cli.csproj
dotnet build src/KazoAdoSync.Cli/KazoAdoSync.Cli.csproj
```

### Configuration

1. Ouvrez `configurator/index.html` dans un navigateur (ou le [Configurateur web](https://kazo-ca.github.io/devopsTask-To-githubIssue/)) pour générer votre `config.yml`.
2. Placez le fichier `config.yml` à la racine du projet.
3. Configurez les variables d'environnement :

```bash
export ADO_PAT_TOKEN="votre-token-ado"
export GH_PAT_TOKEN="votre-token-github"
export ADO_ORG_URL="https://dev.azure.com/votre-org"
export ADO_PROJECT="votre-projet"
export GH_REPO_OWNER="votre-owner"
export GH_REPO_NAME="votre-repo"
```

### Exécution

```bash
# Synchronisation normale
dotnet run --project src/KazoAdoSync.Cli -- config.yml

# Mode dry-run (aucune modification, affiche ce qui serait créé)
dotnet run --project src/KazoAdoSync.Cli -- config.yml --dry-run
```

## 🔗 Activer l'intégration AB# entre Azure DevOps et GitHub

Pour que les liens `AB#123` deviennent **cliquables** dans les deux sens (GitHub ↔ Azure DevOps), vous devez activer la connexion GitHub dans votre organisation Azure DevOps :

### Étape 1 – Connecter GitHub à Azure DevOps

1. Dans Azure DevOps, allez dans **Project Settings** → **GitHub connections**.
2. Cliquez sur **Connect your GitHub account** (ou **New connection**).
3. Authentifiez-vous avec GitHub et autorisez l'accès au(x) dépôt(s) cible(s).

### Étape 2 – Vérifier l'intégration

Une fois la connexion établie :
- Tout commit, branche ou PR contenant `AB#123` dans son message sera automatiquement lié au WorkItem **123** dans Azure DevOps.
- Le WorkItem ADO affichera un lien vers le commit/PR GitHub dans l'onglet **Development**.
- L'issue GitHub affichera `AB#123` comme texte cliquable redirigeant vers ADO.

### Étape 3 – Bonnes pratiques

| Élément        | Convention                                      |
|----------------|------------------------------------------------|
| **Branche**    | `fix/AB#123-description-courte`                |
| **Commit**     | `AB#123 – description du changement`           |
| **Pull Request** | `AB#123 – titre de la PR`                    |

> **Note :** L'outil génère automatiquement ces consignes dans chaque issue créée (section « Règles de traçabilité »).

## 📋 Fonctionnement

1. **Polling** : Le CLI interroge Azure DevOps via WIQL pour les Tasks tagguées (ex: `AI-Ready`).
2. **Conversion** : Le contenu HTML est converti en Markdown via [ReverseMarkdown](https://github.com/mysticmind/reversemarkdown-net).
3. **Création** : Une issue GitHub est créée avec le titre `AB#{{WI_ID}} – titre` et le template de traçabilité.
4. **Déduplication** : Le tag `IssueCreated` est ajouté au WorkItem ADO, et un commentaire avec le lien GitHub est posté.
5. **Résilience** : Les erreurs sur un work item individuel sont loguées sans interrompre le traitement des suivants.

## 📄 Licence

[MIT](LICENSE)
