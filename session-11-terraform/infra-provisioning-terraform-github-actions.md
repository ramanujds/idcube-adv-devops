# Infra Provisioning with Terraform and GitHub Actions

## Overview

This covers the end-to-end workflow of provisioning Azure infrastructure (AKS cluster) using Terraform, automated through GitHub Actions.

The infra repository structure:

```text
infra-repo-idcube/
├── .github/
│   └── workflows/
│       └── terraform.yml    ← GitHub Actions workflow
└── terraform/
    ├── main.tf              ← AKS cluster + resource group
    └── outputs.tf           ← Cluster name, resource group, connect command
```

---

## Why GitHub Actions + Terraform?

| Without CI/CD | With GitHub Actions + Terraform |
| --- | --- |
| Engineer runs `terraform apply` manually from laptop | Pipeline runs it automatically on merge to `main` |
| No record of who applied what | Every run is linked to a commit, PR, and actor |
| State file on someone's machine | State in remote Azure Blob backend |
| Credentials in environment variables on local machine | Credentials stored as GitHub Secrets, never in code |
| No review of infra changes before apply | PR triggers `plan` — reviewers see the diff before merge |

---

## Part 1 — Terraform Configuration

### Step 1 — `terraform/main.tf`

```hcl
terraform {
  required_version = ">= 1.3"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
  }

  backend "azurerm" {
    resource_group_name  = "tfstate-rg"
    storage_account_name = "idcubetfstate"
    container_name       = "tfstate"
    key                  = "idcube-aks.terraform.tfstate"
  }
}

provider "azurerm" {
  features {}
}

resource "azurerm_resource_group" "aks_rg" {
  name     = "idcube-aks"
  location = "South India"
}

resource "azurerm_kubernetes_cluster" "aks" {
  name                = "idcube-cluster"
  location            = azurerm_resource_group.aks_rg.location
  resource_group_name = azurerm_resource_group.aks_rg.name
  dns_prefix          = "idcube"

  sku_tier = "Free"

  default_node_pool {
    name                = "systempool"
    node_count          = 2
    vm_size             = "Standard_D2s_v3"
    type                = "VirtualMachineScaleSets"
    enable_auto_scaling = false
    os_disk_size_gb     = 30
  }

  identity {
    type = "SystemAssigned"
  }

  network_profile {
    network_plugin    = "kubenet"
    load_balancer_sku = "standard"
  }

  role_based_access_control_enabled = true

  tags = {
    environment = "dev"
    project     = "idcube"
  }
}
```

### Step 2 — `terraform/outputs.tf`

```hcl
output "cluster_name" {
  value = azurerm_kubernetes_cluster.aks.name
}

output "resource_group" {
  value = azurerm_resource_group.aks_rg.name
}

output "connect_command" {
  value = "az aks get-credentials --resource-group idcube-aks --name idcube-cluster"
}
```

---

## Part 2 — GitHub Actions Workflow

### Step 3 — `.github/workflows/terraform.yml`

```yaml
name: Terraform Infra Provisioning

on:
  push:
    branches:
      - main
    paths:
      - 'terraform/**'
  pull_request:
    branches:
      - main
    paths:
      - 'terraform/**'
  workflow_dispatch:

env:
  ARM_CLIENT_ID:       ${{ secrets.ARM_CLIENT_ID }}
  ARM_CLIENT_SECRET:   ${{ secrets.ARM_CLIENT_SECRET }}
  ARM_SUBSCRIPTION_ID: ${{ secrets.ARM_SUBSCRIPTION_ID }}
  ARM_TENANT_ID:       ${{ secrets.ARM_TENANT_ID }}

jobs:
  terraform:
    name: Terraform
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: terraform

    steps:

      - name: Checkout code
        uses: actions/checkout@v4

      - name: Setup Terraform
        uses: hashicorp/setup-terraform@v3
        with:
          terraform_version: 1.9.0

      - name: Terraform Format Check
        run: terraform fmt -check -recursive

      - name: Terraform Init
        run: terraform init

      - name: Terraform Validate
        run: terraform validate

      - name: Terraform Plan
        id: plan
        run: terraform plan -out=tfplan -no-color 2>&1 | tee plan.txt

      - name: Post Plan to PR
        if: github.event_name == 'pull_request'
        uses: actions/github-script@v7
        with:
          script: |
            const fs = require('fs');
            const plan = fs.readFileSync('terraform/plan.txt', 'utf8');
            const body = `### Terraform Plan\n\`\`\`\n${plan.slice(0, 60000)}\n\`\`\``;
            github.rest.issues.createComment({
              issue_number: context.issue.number,
              owner: context.repo.owner,
              repo: context.repo.repo,
              body
            });

      - name: Terraform Apply
        if: github.ref == 'refs/heads/main' && github.event_name == 'push'
        run: terraform apply -auto-approve tfplan

      - name: Connect to AKS
        if: github.ref == 'refs/heads/main' && github.event_name == 'push'
        run: |
          az login --service-principal \
            --username $ARM_CLIENT_ID \
            --password $ARM_CLIENT_SECRET \
            --tenant $ARM_TENANT_ID
          az aks get-credentials \
            --resource-group idcube-aks \
            --name idcube-cluster \
            --overwrite-existing
          kubectl get nodes
```

### Step 4 — What each step does

| Step | Runs on | Purpose |
| --- | --- | --- |
| Checkout code | PR + push | Pull latest code from the branch |
| Setup Terraform | PR + push | Install pinned Terraform version on the runner |
| Terraform Format Check | PR + push | Fail if any `.tf` file is not canonical-formatted |
| Terraform Init | PR + push | Download provider, connect to remote state backend |
| Terraform Validate | PR + push | Syntax + schema check (no API calls) |
| Terraform Plan | PR + push | Compute diff, save to `tfplan`, also write `plan.txt` |
| Post Plan to PR | PR only | Comment the plan output on the pull request |
| Terraform Apply | `main` push only | Apply the saved plan — only after PR is merged |
| Connect to AKS | `main` push only | Write kubeconfig for downstream kubectl usage |

The key design: `plan` runs on every PR so reviewers can see what will change before approving. `apply` only runs after merge to `main`.

---

## Part 3 — Azure Authentication

### Option A — Service Principal with Client Secret (simpler)

#### Step 5 — Create a Service Principal

```bash
az ad sp create-for-rbac \
  --name "github-actions-terraform-sp" \
  --role Contributor \
  --scopes /subscriptions/<subscription-id>
```

Output:

```json
{
  "appId":       "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "displayName": "github-actions-terraform-sp",
  "password":    "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "tenant":      "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
}
```

#### Step 6 — Add Secrets to GitHub

1. Go to **Repository → Settings → Secrets and variables → Actions**
2. Click **New repository secret** for each:

| Secret name | Value |
| --- | --- |
| `ARM_CLIENT_ID` | `appId` |
| `ARM_CLIENT_SECRET` | `password` |
| `ARM_TENANT_ID` | `tenant` |
| `ARM_SUBSCRIPTION_ID` | your subscription ID |

The `azurerm` provider reads these four `ARM_*` env vars automatically — no explicit `login` step needed for Terraform commands.

---

### Option B — OIDC / Workload Identity Federation (recommended for production)

This is the modern, secretless approach. Instead of storing a client secret, GitHub Actions exchanges a short-lived JWT (OIDC token) for an Azure access token. No secrets to rotate.

#### Step 7 — Create an App Registration

```bash
az ad app create --display-name "github-actions-oidc"
# note the appId from output
```

#### Step 8 — Create a Service Principal for the app

```bash
az ad sp create --id <appId>
```

#### Step 9 — Add a Federated Identity Credential

```bash
az ad app federated-credential create \
  --id <appId> \
  --parameters '{
    "name": "github-main-branch",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:ramanujds/infra-repo-idcube:ref:refs/heads/main",
    "audiences": ["api://AzureADTokenExchange"]
  }'
```

The `subject` field binds this credential to a specific repo and branch. Change it for PRs:

```bash
# For PR workflows
"subject": "repo:ramanujds/infra-repo-idcube:pull_request"
```

#### Step 10 — Assign Contributor role to the Service Principal

```bash
az role assignment create \
  --assignee <appId> \
  --role Contributor \
  --scope /subscriptions/<subscription-id>
```

#### Step 11 — Add three secrets to GitHub (no client secret needed)

| Secret name | Value |
| --- | --- |
| `ARM_CLIENT_ID` | `appId` |
| `ARM_TENANT_ID` | your tenant ID |
| `ARM_SUBSCRIPTION_ID` | your subscription ID |

#### Step 12 — Update the workflow for OIDC

```yaml
permissions:
  id-token: write    # required to request the OIDC JWT
  contents: read

jobs:
  terraform:
    runs-on: ubuntu-latest
    steps:

      - name: Azure Login via OIDC
        uses: azure/login@v2
        with:
          client-id:       ${{ secrets.ARM_CLIENT_ID }}
          tenant-id:       ${{ secrets.ARM_TENANT_ID }}
          subscription-id: ${{ secrets.ARM_SUBSCRIPTION_ID }}

      - name: Setup Terraform
        uses: hashicorp/setup-terraform@v3

      # Terraform picks up the OIDC token via ARM_* env vars
      # Set use_oidc = true in the provider or pass ARM_USE_OIDC
      - name: Terraform Init
        run: terraform init
        env:
          ARM_USE_OIDC:        true
          ARM_CLIENT_ID:       ${{ secrets.ARM_CLIENT_ID }}
          ARM_TENANT_ID:       ${{ secrets.ARM_TENANT_ID }}
          ARM_SUBSCRIPTION_ID: ${{ secrets.ARM_SUBSCRIPTION_ID }}
```

#### Why OIDC is better than a client secret

* No long-lived credential to store, rotate, or leak
* Token is valid only for the duration of the job
* Credential is bound to a specific repo/branch — another repo cannot use it

---

## Part 4 — Remote State Backend

Local state on the runner is lost when the runner terminates. Always use a remote backend.

### Step 13 — Create the Azure storage resources

```bash
az group create --name tfstate-rg --location "South India"

az storage account create \
  --name idcubetfstate \
  --resource-group tfstate-rg \
  --sku Standard_LRS \
  --allow-blob-public-access false

az storage container create \
  --name tfstate \
  --account-name idcubetfstate
```

### Step 14 — Backend block in `main.tf`

```hcl
backend "azurerm" {
  resource_group_name  = "tfstate-rg"
  storage_account_name = "idcubetfstate"
  container_name       = "tfstate"
  key                  = "idcube-aks.terraform.tfstate"
}
```

### Step 15 — Grant the Service Principal access to the storage account

```bash
az role assignment create \
  --assignee <appId> \
  --role "Storage Blob Data Contributor" \
  --scope /subscriptions/<sub>/resourceGroups/tfstate-rg/providers/Microsoft.Storage/storageAccounts/idcubetfstate
```

The SP needs both `Contributor` (for AKS) and `Storage Blob Data Contributor` (for state).

---

## Part 5 — Full Workflow Flow

```text
Developer opens a PR against main
            │
            ▼
Workflow triggers on: pull_request
            │
    ┌───────┴────────┐
    │                │
  fmt check       validate
    │                │
    └───────┬────────┘
            │
         terraform plan
            │
            ▼
   Plan posted as PR comment
            │
  Reviewer reads the plan output
            │
       PR approved + merged
            │
            ▼
Workflow triggers on: push to main
            │
         terraform init
            │
         terraform plan -out=tfplan
            │
         terraform apply tfplan
            │
         az aks get-credentials
            │
         kubectl get nodes
            ▼
        Done — AKS cluster live
```

---

## Best Practices

### Terraform Best Practices

#### Pin versions in `required_providers` and `setup-terraform`

```hcl
# main.tf
terraform {
  required_version = ">= 1.3, < 2.0"
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
  }
}
```

```yaml
# workflow
- uses: hashicorp/setup-terraform@v3
  with:
    terraform_version: 1.9.0
```

Keeps local and CI Terraform versions in sync. Prevents surprise breakage from a major provider upgrade.

#### Always use `plan -out` and apply the saved plan

```yaml
- name: Terraform Plan
  run: terraform plan -out=tfplan

- name: Terraform Apply
  run: terraform apply -auto-approve tfplan
```

Applying a saved plan guarantees that exactly what was reviewed gets applied — nothing can change between plan and apply.

#### Never commit `terraform.tfstate` or `.terraform/`

`.gitignore` for infra repos:

```text
**/.terraform/
*.tfstate
*.tfstate.backup
*.tfplan
```

Committing state exposes resource IDs, IPs, and sometimes secrets. Always use a remote backend.

#### Use `terraform fmt -check` as a gate

```yaml
- name: Terraform Format Check
  run: terraform fmt -check -recursive
```

Fails the pipeline if any `.tf` file is not formatted. This keeps diffs clean and prevents style debates in review.

#### Use `prevent_destroy` on critical resources

```hcl
resource "azurerm_kubernetes_cluster" "aks" {
  lifecycle {
    prevent_destroy = true
  }
}
```

Prevents a misconfigured plan from destroying the cluster. The pipeline would fail with an explicit error rather than silently tearing down production infra.

---

### GitHub Actions Best Practices for Azure

#### Use OIDC instead of client secrets

Client secrets expire, can leak in logs, and require rotation. OIDC tokens are ephemeral and scoped to the exact job — nothing to store or rotate.

See Part 3, Option B for the full setup.

#### Set minimal permissions on the workflow job

```yaml
permissions:
  id-token: write   # needed for OIDC token exchange
  contents: read    # needed to checkout code
```

By default `permissions` is `write-all`. Scope it down so a compromised workflow token cannot push code or create releases.

#### Scope the Service Principal to the minimum required role

Avoid `Owner`. Use:

* `Contributor` on the subscription (or specific resource group) for Terraform resources
* `Storage Blob Data Contributor` on the tfstate storage account

```bash
az role assignment create \
  --assignee <appId> \
  --role Contributor \
  --scope /subscriptions/<sub>/resourceGroups/idcube-aks
```

Scoping to a resource group rather than the whole subscription limits blast radius if the credential is compromised.

#### Gate apply on branch protection

In GitHub:

1. **Settings → Branches → Add rule** for `main`
2. Enable **Require pull request reviews before merging**
3. Enable **Require status checks to pass** — add the `Terraform` job as a required check

This enforces the plan-review-apply cycle: you cannot merge (and trigger apply) unless the plan job passed and a reviewer approved.

#### Use `workflow_dispatch` for manual re-runs

```yaml
on:
  push:
    branches: [main]
  workflow_dispatch:
```

`workflow_dispatch` adds a **Run workflow** button in the GitHub UI. Useful for re-applying state after a manual change or recovering from a failed apply.

#### Store environment-specific configuration in GitHub Environments

For multiple environments (dev, staging, prod):

1. Go to **Settings → Environments → New environment**
2. Create `dev`, `staging`, `prod`
3. Store `ARM_*` secrets per environment (each points to a different subscription or resource group)
4. Add **required reviewers** to `prod` — this pauses the workflow until a human approves

```yaml
jobs:
  terraform-prod:
    environment: prod    # waits for approval before running
    runs-on: ubuntu-latest
```

#### Never echo or print secrets

GitHub Actions automatically masks values that match registered secrets, but avoid explicit `echo $ARM_CLIENT_SECRET` or printing the `az` login output. Use `-no-color` with Terraform to keep plan output clean for PR comments:

```yaml
run: terraform plan -out=tfplan -no-color 2>&1 | tee plan.txt
```

#### Use `paths` filter to avoid unnecessary runs

```yaml
on:
  push:
    branches: [main]
    paths:
      - 'terraform/**'
```

Without `paths`, every commit (even a README fix) triggers the Terraform workflow. Scoping to `terraform/**` prevents unnecessary plan/apply cycles.

---

## Comparison: Jenkins vs GitHub Actions for Terraform

| Aspect | Jenkins | GitHub Actions |
| --- | --- | --- |
| Setup effort | High (install Jenkins, plugins, agents) | Low (built into GitHub) |
| Credential storage | Jenkins Credentials Manager | GitHub Secrets / Environments |
| Azure auth best practice | Service Principal + secret | OIDC (no secrets) |
| Plan on PR | Manual scripting | `if: github.event_name == 'pull_request'` |
| Manual approval | `input` step in Jenkinsfile | GitHub Environments with required reviewers |
| Audit trail | Jenkins build log | GitHub Actions run linked to commit + actor |
| Self-hosted runners | Jenkins agents | GitHub-hosted or self-hosted runners |
| Cost | Infrastructure cost for Jenkins server | Free for public repos; per-minute for private |

---

## Common Errors and Fixes

### `Error: AADSTS700016: Application not found`

→ `ARM_CLIENT_ID` is wrong or the app was deleted.
→ Verify with: `az ad app show --id $ARM_CLIENT_ID`

### `Error: AuthorizationFailed — does not have permission`

→ Service Principal missing the `Contributor` role on the target scope.
→ Re-run the `az role assignment create` command with the correct scope.

### `Error: state blob is already locked`

→ A previous run crashed while holding the state lock.
→ Go to Azure Portal → Storage Account → tfstate container → find the lease on the `.tfstate` blob → break the lease, or:

```bash
terraform force-unlock <lock-id>
```

### OIDC: `Error: failed to get token — subject claim mismatch`

→ The federated credential `subject` does not match the workflow trigger.
→ For a PR workflow use `repo:org/repo:pull_request`; for main-branch use `repo:org/repo:ref:refs/heads/main`.

### `terraform: command not found` on runner

→ `hashicorp/setup-terraform` step is missing or placed after the Terraform commands.
→ Always place `setup-terraform` before any `terraform` commands.

### Apply succeeds but `kubectl get nodes` fails with `Unauthorized`

→ The Service Principal does not have the `Azure Kubernetes Service Cluster User Role` on the cluster.

```bash
AKS_ID=$(az aks show --name idcube-cluster --resource-group idcube-aks --query id -o tsv)
az role assignment create \
  --assignee <appId> \
  --role "Azure Kubernetes Service Cluster User Role" \
  --scope $AKS_ID
```
