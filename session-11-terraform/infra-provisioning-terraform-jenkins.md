# Infra Provisioning with Terraform and Jenkins

## Overview

This covers the end-to-end workflow of provisioning Azure infrastructure (AKS cluster) using Terraform, automated through a Jenkins pipeline.

The repository structure mirrors a real GitOps setup:

```text
infra-repo-terraform-jenkins/
├── Jenkinsfile          ← Pipeline definition
└── terraform/
    ├── main.tf          ← AKS cluster + resource group
    └── outputs.tf       ← Cluster name, resource group, connect command
```

---

## Why Jenkins + Terraform?

| Without CI/CD | With Jenkins + Terraform |
| --- | --- |
| Engineer runs `terraform apply` manually | Pipeline runs it automatically on every merge |
| No audit trail of who applied what | Every pipeline run is logged with timestamp and git commit |
| Infra and app changes deploy at different speeds | Both go through the same automated gate |
| State file lives on someone's laptop | State lives in a shared remote backend |

---

## Part 1 — Terraform Configuration

### Step 1 — Review `terraform/main.tf`

```hcl
terraform {
  required_version = ">= 1.3"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
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

Key points:

* `required_version` pins the minimum Terraform version — prevents accidental use of an older CLI
* `version = "~> 3.0"` pins the provider to `3.x` — `~>` means "allow patch/minor updates but not major"
* `SystemAssigned` identity means Azure auto-creates a managed identity for the cluster — no client secrets to manage
* `kubenet` is the simpler network plugin; use `azure` (CNI) if you need pod-level NSG or advanced networking

### Step 2 — Review `terraform/outputs.tf`

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

Outputs are used by the pipeline's post-apply stage to print the `kubectl` connect command without hardcoding it elsewhere.

---

## Part 2 — Jenkins Pipeline

### Step 3 — Review `Jenkinsfile`

```groovy
pipeline {

    agent any

    tools {
        terraform 'terraform'
    }

    environment {
        RESOURCE_GROUP = "idcube-aks"
        CLUSTER_NAME   = "idcube-cluster"
        GITOPS_REPO_URL = "https://github.com/ramanujds/gitops-repo-idcube"
    }

    stages {

        stage('Checkout Infrastructure Code') {
            steps {
                echo 'Checking out infrastructure code from GitHub'
                git branch: 'main', url: 'https://github.com/ramanujds/infra-repo-idcube'
            }
        }

        stage('Check Azure CLI') {
            steps {
                echo 'Checking Azure CLI installation'
                sh 'export PATH=$PATH:/usr/local/bin:/opt/homebrew/bin && az --version'
            }
        }

        stage('Terraform Init') {
            steps {
                echo 'Initializing Terraform'
                dir('terraform') {
                    sh 'export PATH=$PATH:/usr/local/bin:/opt/homebrew/bin && terraform init'
                }
            }
        }

        stage('Terraform Validate') {
            steps {
                echo 'Validating Terraform configuration'
                dir('terraform') {
                    sh 'export PATH=$PATH:/usr/local/bin:/opt/homebrew/bin && terraform validate'
                }
            }
        }

        stage('Terraform Plan') {
            steps {
                echo 'Planning Terraform deployment'
                dir('terraform') {
                    sh 'export PATH=$PATH:/usr/local/bin:/opt/homebrew/bin && terraform plan -out=tfplan'
                }
            }
        }

        stage('Terraform Apply') {
            steps {
                echo 'Applying Terraform deployment'
                dir('terraform') {
                    sh 'export PATH=$PATH:/usr/local/bin:/opt/homebrew/bin && terraform apply -auto-approve tfplan'
                }
            }
        }

        stage('Connect to AKS Cluster') {
            steps {
                echo 'Connecting to AKS cluster'
                sh '''
                export PATH=$PATH:/usr/local/bin:/opt/homebrew/bin
                az aks get-credentials --resource-group ${RESOURCE_GROUP} --name ${CLUSTER_NAME} --overwrite-existing
                '''
            }
        }

    }

}
```

### Step 4 — What each stage does

| Stage | Purpose |
| --- | --- |
| Checkout Infrastructure Code | Pulls the latest infra repo from GitHub |
| Check Azure CLI | Sanity check — fails fast if `az` is not on the agent |
| Terraform Init | Downloads provider plugins, sets up backend |
| Terraform Validate | Checks HCL syntax without calling any cloud APIs |
| Terraform Plan | Computes the diff, saves plan to `tfplan` file |
| Terraform Apply | Applies the saved plan (`-auto-approve` skips prompt) |
| Connect to AKS Cluster | Writes kubeconfig on the Jenkins agent for `kubectl` use |

---

## Part 3 — Jenkins Setup

### Step 5 — Install Terraform on Jenkins

#### Option A — Install via Plugin (recommended)

1. Go to `Jenkins → Manage Jenkins → Tools`
2. Under **Terraform installations**, click **Add Terraform**
3. Name it `terraform` (must match `tools { terraform 'terraform' }` in Jenkinsfile)
4. Check **Install automatically** and pick the version

#### Option B — Install manually on the agent

```bash
# On the Jenkins agent machine
wget https://releases.hashicorp.com/terraform/1.9.0/terraform_1.9.0_linux_amd64.zip
unzip terraform_1.9.0_linux_amd64.zip
sudo mv terraform /usr/local/bin/
terraform -v
```

### Step 6 — Install Azure CLI on the Jenkins agent

```bash
curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash
az --version
```

### Step 7 — Configure Azure credentials in Jenkins

The pipeline needs to authenticate to Azure without a human running `az login`.

**Create a Service Principal:**

```bash
az ad sp create-for-rbac \
  --name "jenkins-terraform-sp" \
  --role Contributor \
  --scopes /subscriptions/<subscription-id>
```

Output:

```json
{
  "appId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "displayName": "jenkins-terraform-sp",
  "password": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "tenant": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
}
```

**Store as Jenkins credentials:**

1. Go to `Jenkins → Manage Jenkins → Credentials`
2. Add four **Secret text** credentials:

| Jenkins credential ID | Value |
| --- | --- |
| `ARM_CLIENT_ID` | `appId` from above |
| `ARM_CLIENT_SECRET` | `password` from above |
| `ARM_TENANT_ID` | `tenant` from above |
| `ARM_SUBSCRIPTION_ID` | your Azure subscription ID |

**Inject them in the Jenkinsfile environment block:**

```groovy
environment {
    RESOURCE_GROUP    = "idcube-aks"
    CLUSTER_NAME      = "idcube-cluster"
    ARM_CLIENT_ID     = credentials('ARM_CLIENT_ID')
    ARM_CLIENT_SECRET = credentials('ARM_CLIENT_SECRET')
    ARM_TENANT_ID     = credentials('ARM_TENANT_ID')
    ARM_SUBSCRIPTION_ID = credentials('ARM_SUBSCRIPTION_ID')
}
```

The `azurerm` provider picks up these four `ARM_*` env vars automatically.

### Step 8 — Create a Jenkins Pipeline Job

1. `New Item → Pipeline → OK`
2. Under **Pipeline**, select **Pipeline script from SCM**
3. SCM: Git, URL: `https://github.com/ramanujds/infra-repo-idcube`
4. Branch: `*/main`
5. Script Path: `Jenkinsfile`
6. Save and click **Build Now**

---

## Part 4 — Remote State Backend (Required for Teams)

By default Terraform writes state locally on the Jenkins agent — this is lost when the agent is recycled.

### Step 9 — Create Azure Storage for state

```bash
az group create --name tfstate-rg --location "South India"

az storage account create \
  --name idcubetfstate \
  --resource-group tfstate-rg \
  --sku Standard_LRS

az storage container create \
  --name tfstate \
  --account-name idcubetfstate
```

### Step 10 — Add backend block to `main.tf`

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
```

### Step 11 — Re-initialize to migrate state

```bash
terraform init -migrate-state
```

Now state is stored safely in Azure Blob Storage — shared across the team and locked during apply.

---

## Part 5 — Full Pipeline Flow

```text
Developer pushes to main
        │
        ▼
Jenkins webhook triggered
        │
        ▼
Stage: Checkout  ──→ git pull latest infra code
        │
        ▼
Stage: Check Azure CLI  ──→ verify tooling
        │
        ▼
Stage: Terraform Init  ──→ download provider, connect to remote backend
        │
        ▼
Stage: Terraform Validate  ──→ syntax check (fast, no API calls)
        │
        ▼
Stage: Terraform Plan  ──→ compute diff, write tfplan file
        │
        ▼
Stage: Terraform Apply  ──→ apply saved plan (-auto-approve)
        │
        ▼
Stage: Connect to AKS  ──→ az aks get-credentials writes kubeconfig
        │
        ▼
Next pipeline (app deploy) can now use kubectl
```

---

## Best Practices

### Terraform Best Practices

#### Pin provider and Terraform versions

```hcl
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

Prevents a version upgrade from silently breaking your config in CI.

**Never commit `terraform.tfstate` to Git**

Add this to `.gitignore`:

```text
**/.terraform/
*.tfstate
*.tfstate.backup
*.tfplan
.terraform.lock.hcl   # optionally — some teams commit this for reproducibility
```

**Use `terraform plan -out=tfplan` and apply the saved plan**

```bash
terraform plan -out=tfplan
terraform apply tfplan   # applies exactly what was planned — no surprises
```

If you run `terraform apply` without a saved plan, the actual changes could differ from what `plan` showed (e.g., if someone made a manual change between plan and apply).

**Use `prevent_destroy` on production clusters**

```hcl
resource "azurerm_kubernetes_cluster" "aks" {
  # ...
  lifecycle {
    prevent_destroy = true
  }
}
```

Prevents accidental `terraform destroy` from deleting the cluster.

#### Use variables for everything environment-specific

```hcl
variable "cluster_name" { default = "idcube-cluster" }
variable "node_count"   { default = 2 }
variable "vm_size"      { default = "Standard_D2s_v3" }
```

Then override per environment:

```bash
terraform apply -var="node_count=3" -var="vm_size=Standard_D4s_v3"
```

Or use a `terraform.tfvars` file per environment.

**Run `terraform fmt` before committing**

```bash
terraform fmt -recursive   # formats all .tf files
terraform fmt -check       # exits non-zero if formatting is wrong (use in CI)
```

---

### Jenkins Pipeline Best Practices

#### Store all credentials in Jenkins Credentials, never in code

Never hardcode service principal secrets, subscription IDs, or passwords in the Jenkinsfile or `.tf` files. Use `credentials('id')` binding.

**Use `dir('terraform')` to scope Terraform commands**

All `terraform` commands run inside the `terraform/` subdirectory. This keeps the pipeline portable and makes the directory structure explicit.

**PATH export on each `sh` step**

```groovy
sh 'export PATH=$PATH:/usr/local/bin:/opt/homebrew/bin && terraform init'
```

Jenkins agents may not inherit the full shell PATH. Explicitly extending it ensures `terraform` and `az` are found regardless of how the agent is started.

**Use `input` step for production applies (manual gate)**

```groovy
stage('Approval') {
    when {
        branch 'main'
    }
    steps {
        input message: 'Review the plan. Approve to apply?', ok: 'Apply'
    }
}
```

Place this between `Terraform Plan` and `Terraform Apply` in production pipelines.

#### Archive the plan output as a build artifact

```groovy
stage('Terraform Plan') {
    steps {
        dir('terraform') {
            sh 'terraform plan -out=tfplan -no-color 2>&1 | tee plan.txt'
        }
        archiveArtifacts artifacts: 'terraform/plan.txt'
    }
}
```

Every build stores the plan — useful for audits and debugging.

**Use `--overwrite-existing` when getting AKS credentials**

```bash
az aks get-credentials \
  --resource-group ${RESOURCE_GROUP} \
  --name ${CLUSTER_NAME} \
  --overwrite-existing
```

Prevents the pipeline from failing when a kubeconfig entry for the cluster already exists on the agent.

#### Separate infra repo from app repo

Keep Terraform code in a dedicated repo (`infra-repo`) and application code in a separate repo (`app-repo`). Infra changes go through the infra pipeline; app deployments go through the app pipeline. This:

* Prevents app commits from triggering infra applies
* Gives different teams different permissions on each repo
* Makes rollback of infra vs app changes independent

---

## Common Errors and Fixes

**`Error: No valid credential sources found for AWS Provider`**
→ Wrong provider. Make sure `ARM_*` env vars are set, not `AWS_*`.

**`Error acquiring the state lock`**
→ A previous apply is still running, or it crashed.
→ Check Azure Portal for a lease on the state blob, or use:

```bash
terraform force-unlock <lock-id>
```

**`az: command not found` in Jenkins**
→ Azure CLI not installed on the Jenkins agent, or not in PATH.
→ Add `export PATH=$PATH:/usr/local/bin` to the `sh` command.

**`terraform: command not found` in Jenkins**
→ Tool named `terraform` not configured in `Jenkins → Manage Jenkins → Tools`.
→ Verify the tool name in Jenkinsfile matches exactly: `tools { terraform 'terraform' }`.

**Apply succeeds but `kubectl get nodes` fails**
→ AKS cluster is provisioned but kubeconfig step failed.
→ Check the agent has `az` installed and the service principal has **Azure Kubernetes Service Cluster User Role** on the cluster.

**`Error: A resource with the ID already exists`**
→ Resource was created outside Terraform (portal/CLI) but is not in state.
→ Import it:

```bash
terraform import azurerm_resource_group.aks_rg \
  /subscriptions/<sub-id>/resourceGroups/idcube-aks
```
