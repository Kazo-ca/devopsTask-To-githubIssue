# Spécifications : Automatisation de synchronisation ADO vers GitHub

## 1. Objectif
Créer une automatisation via **GitHub Actions** pour transformer des `WorkItems` (Type: Task) d'Azure DevOps en `Issues` GitHub. L'outil permet à un agent d'IA ou à un développeur de travailler sur une tâche spécifique avec tout le contexte nécessaire, sans créer de doublons.

## 2. Architecture Technique
* **Type :** GitHub Action exécutant un script **.NET 10**.
* **Mode :** Polling (Exécution planifiée par `cron` toutes les 5 à 15 minutes).
* **Authentification :**
    * `ADO_PAT_TOKEN` : Accès en lecture/écriture aux WorkItems.
    * `GH_PAT_TOKEN` : Accès en écriture pour créer des Issues (Compte de type "Bot").

## 3. Configuration (`config.yml`)
La configuration doit être flexible et stockée en YAML.

```yml
sync_settings:
  watch_tag: "AI-Ready"             # Le tag à surveiller dans ADO
  processed_tag: "IssueCreated"     # Tag ajouté à ADO après création de l'issue
  issue_prefix: "Devops IA Task"    # Préfixe du titre GitHub
  default_source_field: "Notes"     # Champ ADO source (par défaut: Notes)
  github_label: "devops-task"       # Label à ajouter à l'issue
  template_path: ".github/issue-template.md"
  
env_mapping:
  github_token_var: "GH_PAT_TOKEN"
  devops_token_var: "ADO_PAT_TOKEN"
```

## 4. Logique de Traitement (Workflow)

### A. Récupération & Filtrage
Le script interroge Azure DevOps via WIQL pour trouver les tâches ayant le `watch_tag` mais pas le `processed_tag`.

### B. Gestion du Contexte (Parent/Child)
* Si le champ `default_source_field` est vide : le script récupère la `Description` de la User Story parente.
* **Scope Restriction :** Le message d'issue doit explicitement mentionner que le scope est limité à la tâche pour éviter que l'IA ne déborde sur la User Story complète.

### C. Conversion
Le contenu HTML d'Azure DevOps est converti en **Markdown** pour une compatibilité totale avec GitHub Issues.

## 5. Modèle d'Issue (`template.md`)
Utilisation de mots-clés dynamiques pour la génération.

```md
### 🤖 Notification d'automatisation
Cette issue a été créée automatiquement à partir du WorkItem : **AB#{{WI_ID}}**

---

### 📋 Contexte de la User Story (Parent)
> {{PARENT_DESC}}

### 🎯 Tâche à réaliser (Scope)
**{{WI_DESC}}**

> [!IMPORTANT]
> Votre périmètre d'action est **STRICTEMENT LIMITÉ** à cette tâche spécifique. 
> Ne modifiez pas les éléments globaux de la User Story sauf instruction contraire dans la tâche.

---
*Lien ADO : [Consulter dans Azure DevOps]({{WI_URL}})*
```

## 6. Prévention des Doublons
1. Création de l'issue sur GitHub.
2. Ajout immédiat du `processed_tag` (ex: `IssueCreated`) sur le WorkItem ADO.
3. Ajout d'un commentaire dans ADO avec l'URL de l'issue GitHub pour traçabilité.
