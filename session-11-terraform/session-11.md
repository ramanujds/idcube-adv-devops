# SESSION 11 — Terraform

---

## Session Goal

* Provision infrastructure using Terraform
* Understand drift detection and reconciliation

---

## Continuing Case Study

> Your CI pipeline is working.
>
> But:
>
> * Infra changes are manual
> * Deployments are not fully traceable
> * Cluster state can drift from desired state
>
> Your task: Move to Infrastructure as Code + GitOps model

---

## LAB 1 — Terraform Basics: Docker Container

**Reference:** `terraform-docker-container/main.tf`

This is the safest first lab — no cloud credentials needed, just Docker running locally.

### Step 1 — Review the configuration

```hcl
terraform {
  required_providers {
    docker = {
      source  = "kreuzwerker/docker"
      version = "~> 3.0"
    }
  }
}

provider "docker" {}

resource "docker_image" "caddy" {
  name = "caddy:latest"
}

resource "docker_container" "caddy" {
  name  = "caddy"
  image = docker_image.caddy.name
  ports {
    internal = 80
    external = 8080
  }
}
```

### Step 2 — Initialize

```bash
cd terraform-docker-container
terraform init
```

Look at what `init` created:

```bash
ls -la
# .terraform/         ← provider binary downloaded here
# .terraform.lock.hcl ← provider version lock
```

### Step 3 — Plan

```bash
terraform plan
```

Expected output shows `+` for:

* `docker_image.caddy`
* `docker_container.caddy`

### Step 4 — Apply

```bash
terraform apply
```

Type `yes` when prompted. Verify:

```bash
docker ps | grep caddy
curl http://localhost:8080
```

### Step 5 — Observe State

```bash
terraform state list
terraform state show docker_container.caddy
```

### Step 6 — Destroy

```bash
terraform destroy
docker ps   # container is gone
```

**Key takeaway:** Terraform created and then destroyed real infrastructure declaratively. You never ran `docker run` or `docker rm` manually.

---

## LAB 2 — Azure VM with Networking

**Reference:** `terraform-azure-vm/main.tf`

### Prerequisites

```bash
az login
az account show
```

### Azure VM resource topology

```text
Resource Group
└── VNet (10.0.0.0/16)
    └── Subnet (10.0.1.0/24)
        └── NIC ──── NSG (Allow SSH:22, HTTP:80)
            └── Linux VM (Ubuntu 22.04, nginx on boot)
Public IP (Static) ──────────────────────────────────┘
```

### Step 1 — Initialize and plan

```bash
cd terraform-azure-vm
terraform init
terraform plan
```

Read the plan. Note the dependency chain:
`resource_group → vnet → subnet → nic → vm`

### Step 2 — Apply

```bash
terraform apply -auto-approve
```

### Step 3 — Check the output

```bash
terraform output public_ip
```

The VM runs an nginx startup script via `custom_data` (cloud-init):

```bash
curl http://<public-ip>    # should return nginx welcome page
```

### Step 4 — Observe drift

Manually add a tag to the resource group in Azure Portal, then:

```bash
terraform plan
```

Terraform detects the out-of-band change and shows what it would reset. This is **drift detection**.

### Step 5 — Destroy

```bash
terraform destroy -auto-approve
```

**Key takeaway:** Terraform builds the entire network stack in dependency order automatically. `depends_on` in the NIC resource is explicit only because that dependency cannot be inferred from a reference alone.

---

## LAB 3 — AKS Cluster via Terraform

**Reference:** `terraform-aks/main.tf` and `terraform-aks/outputs.tf`

### AKS resource topology

```text
Resource Group (idcube-aks)
└── AKS Cluster (idcube-cluster)
    └── System Node Pool (2 × Standard_D2s_v4)
        ├── SystemAssigned identity
        └── kubenet networking
```

### Step 1 — Initialize

```bash
cd terraform-aks
terraform init
```

### Step 2 — Plan and review

```bash
terraform plan
```

Note that the `azurerm_kubernetes_cluster` resource references the resource group via:

```hcl
location            = azurerm_resource_group.aks_rg.location
resource_group_name = azurerm_resource_group.aks_rg.name
```

Terraform infers the dependency automatically — no `depends_on` needed.

### Step 3 — Apply (takes ~5 minutes)

```bash
terraform apply -auto-approve
```

### Step 4 — Use the outputs

```bash
terraform output cluster_name
terraform output connect_command
```

Run the connect command to configure `kubectl`:

```bash
az aks get-credentials --resource-group idcube-aks --name idcube-cluster
kubectl get nodes
```

### Step 5 — State inspection

```bash
terraform state list
# azurerm_kubernetes_cluster.aks
# azurerm_resource_group.aks_rg

terraform state show azurerm_kubernetes_cluster.aks
```

### Step 6 — Destroy

```bash
terraform destroy -auto-approve
```

**Key takeaway:** A full AKS cluster with identity and networking configured in ~50 lines of HCL. `outputs.tf` separates the connection info cleanly from the resource definitions.

---

## LAB 4 — Kubernetes Resources via Terraform

**Reference:** `terraform-kubernetes-local/main.tf`

This manages Kubernetes objects (Deployments, Services) using the `kubernetes` provider against a local cluster.

### Prerequisites

```bash
kubectl config current-context   # should point to local cluster (Docker Desktop / minikube)
```

### What this config creates

* `part-inventory-deployment` — 1 replica, image `ram1uj/part-inventory-service:latest`
* `part-order-deployment` — 1 replica, env vars pointing to inventory service
* `part-gateway-deployment` — 1 replica, env vars pointing to both services
* NodePort Services for each (30080, 30081, 30082)

### Step 1 — Initialize and apply

```bash
cd terraform-kubernetes-local
terraform init
terraform apply -auto-approve
```

### Step 2 — Verify

```bash
kubectl get deployments
kubectl get services
kubectl get pods
```

### Step 3 — Modify and observe reconciliation

Change `replicas = 1` to `replicas = 2` for `part-inventory-deployment`, then:

```bash
terraform plan    # shows ~ update in-place
terraform apply -auto-approve
kubectl get pods  # now 2 pods for part-inventory
```

### Step 4 — Destroy

```bash
terraform destroy -auto-approve
kubectl get all   # everything removed
```

**Key takeaway:** The same `terraform plan / apply` workflow manages both cloud infra and Kubernetes objects. Any drift (e.g., someone running `kubectl scale`) is corrected on next apply.

---

## LAB 5 — Multi-Cloud with Modules (GCP + AWS)

**Reference:** `terraform-multi-cloud-gcp-aws/`

```text
terraform-multi-cloud-gcp-aws/
├── main.tf          ← calls both modules
├── outputs.tf       ← exposes both IPs
└── modules/
    ├── aws/main.tf  ← EC2 instance with Docker + part-inventory-service
    └── gcp/main.tf  ← GCP Compute Engine VM with nginx
```

### Module structure

`main.tf` (root module) calls child modules:

```hcl
module "gcp_vm" {
  source = "./modules/gcp"
}

module "aws_vm" {
  source = "./modules/aws"
}
```

`outputs.tf` exposes each module's output:

```hcl
output "gcp_instance_ip" {
  value = module.gcp_vm.instance_public_ip
}

output "aws_instance_ip" {
  value = module.aws_vm.instance_public_ip
}
```

### Step 1 — Prerequisites

```bash
# AWS credentials
aws configure   # or set AWS_ACCESS_KEY_ID / AWS_SECRET_ACCESS_KEY env vars

# GCP credentials
gcloud auth application-default login
```

### Step 2 — Initialize (downloads two providers)

```bash
cd terraform-multi-cloud-gcp-aws
terraform init
```

### Step 3 — Plan and apply

```bash
terraform plan
terraform apply -auto-approve
```

### Step 4 — Outputs

```bash
terraform output gcp_instance_ip
terraform output aws_instance_ip
```

**Key takeaway:** One `terraform apply` provisions resources across two cloud providers simultaneously. Modules isolate provider-specific code; the root just composes them.

---

## LAB 6 — Remote State Backend (Azure)

Local `terraform.tfstate` is dangerous in teams. Configure Azure Blob Storage as the backend.

### Step 1 — Create the storage account (one-time setup)

```bash
az group create --name tfstate-rg --location "East US"
az storage account create \
  --name tfstateaccount$RANDOM \
  --resource-group tfstate-rg \
  --sku Standard_LRS \
  --encryption-services blob

az storage container create \
  --name tfstate \
  --account-name <storage-account-name>
```

### Step 2 — Add backend configuration

```hcl
terraform {
  backend "azurerm" {
    resource_group_name  = "tfstate-rg"
    storage_account_name = "<storage-account-name>"
    container_name       = "tfstate"
    key                  = "dev.terraform.tfstate"
  }
}
```

### Step 3 — Re-initialize to migrate state

```bash
terraform init -migrate-state
```

Terraform uploads local state to Azure Blob Storage. State is now:

* Shared across the team
* Locked during `apply` (prevents concurrent writes)
* Versioned (blob versioning)

### Step 4 — Verify

```bash
az storage blob list \
  --container-name tfstate \
  --account-name <storage-account-name> \
  --output table
```

---

## Terraform Workflow Cheatsheet

| Command | What it does |
| --- | --- |
| `terraform init` | Download providers, set up backend |
| `terraform fmt` | Format `.tf` files to canonical style |
| `terraform validate` | Check configuration syntax |
| `terraform plan` | Show what will change (dry run) |
| `terraform apply` | Apply changes |
| `terraform apply -auto-approve` | Apply without interactive prompt |
| `terraform destroy` | Destroy all managed resources |
| `terraform state list` | List all resources in state |
| `terraform state show <resource>` | Inspect a specific resource's state |
| `terraform output` | Show all output values |
| `terraform workspace new <name>` | Create a new workspace |
| `terraform workspace select <name>` | Switch workspace |
| `terraform import <resource> <id>` | Import existing resource into state |

---

## Common Troubleshooting

**`Error: Provider not found`**
→ Run `terraform init` — provider plugin not downloaded yet.

**`Error: state lock`**
→ Another apply is running, or a previous apply crashed.
→ `terraform force-unlock <lock-id>` (get ID from error message)

**Plan shows destroy when you only added resources**
→ State file is out of sync. Check `terraform state list` vs real cloud resources.

**`terraform apply` times out on AKS**
→ AKS provisioning can take 5–10 minutes. Increase timeout with:

```hcl
timeouts {
  create = "30m"
}
```

**Resource already exists in Azure but not in state**
→ Import it: `terraform import azurerm_resource_group.rg /subscriptions/<sub>/resourceGroups/demo-rg`
