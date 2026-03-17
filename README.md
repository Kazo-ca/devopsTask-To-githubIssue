# KazoAdoSync – Azure DevOps → GitHub Issue Sync

Outil d'automatisation qui synchronise les **WorkItems** (Type: Task) d'Azure DevOps vers des **Issues** GitHub, avec traçabilité complète via les tags `AB#`.

## 🏗️ Architecture

```
├── src/
│   └── KazoAdoSync.Cli/       # Application console .NET 10
│       ├── Program.cs          # Logique de synchronisation
│       └── KazoAdoSync.Cli.csproj
├── configurator/
│   └── index.html              # Générateur de config.yml (Tailwind)
├── template.md                 # Modèle d'issue GitHub
├── config.yml                  # Configuration YAML (à créer)
└── spec.md                     # Spécifications du projet
```

## ⚡ Démarrage rapide

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

1. Ouvrez `configurator/index.html` dans un navigateur pour générer votre `config.yml`.
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
dotnet run --project src/KazoAdoSync.Cli -- config.yml
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

## 📄 Licence

[MIT](LICENSE)
