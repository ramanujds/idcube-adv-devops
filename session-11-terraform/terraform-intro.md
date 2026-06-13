# Step-by-Step: Getting Started with Terraform

## Step 1 — Install Terraform

**macOS (Homebrew):**
```bash
brew tap hashicorp/tap
brew install hashicorp/tap/terraform
terraform -v
```

**Ubuntu/Debian:**
```bash
sudo apt-get update && sudo apt-get install -y gnupg software-properties-common
wget -O- https://apt.releases.hashicorp.com/gpg | gpg --dearmor | sudo tee /usr/share/keyrings/hashicorp-archive-keyring.gpg
echo "deb [signed-by=/usr/share/keyrings/hashicorp-archive-keyring.gpg] https://apt.releases.hashicorp.com $(lsb_release -cs) main" | sudo tee /etc/apt/sources.list.d/hashicorp.list
sudo apt update && sudo apt install terraform
terraform -v
```

---

## Step 2 — Authenticate to Azure

```bash
az login
az account show          # verify active subscription
az account set --subscription "<subscription-id>"
```

Terraform picks up credentials from the `az` CLI session automatically via the `azurerm` provider.

---

## Step 3 — Create Your First Configuration File

Create a directory and a `main.tf`:

```bash
mkdir terraform-demo && cd terraform-demo
touch main.tf
```

Minimal `main.tf`:

```hcl
provider "azurerm" {
  features {}
}

resource "azurerm_resource_group" "rg" {
  name     = "demo-rg"
  location = "East US"
}
```

---

## Step 4 — Initialize

```bash
terraform init
```

What happens:
- Downloads the `azurerm` provider plugin into `.terraform/`
- Creates `.terraform.lock.hcl` (locks provider versions)

You must run `init` whenever you:
- Add a new provider
- Change backend configuration
- Clone a repo that contains Terraform code

---

## Step 5 — Plan

```bash
terraform plan
```

Output shows:
- `+` (green) = will be created
- `~` (yellow) = will be updated in place
- `-` (red) = will be destroyed

Read the plan carefully before applying. This is a dry run — nothing changes in Azure.

---

## Step 6 — Apply

```bash
terraform apply
```

Terraform prints the plan again and asks for confirmation:

```
Do you want to perform these actions? (yes/no)
```

Type `yes`. To skip the prompt (useful in CI):

```bash
terraform apply -auto-approve
```

After apply:
- `terraform.tfstate` is written/updated locally
- Real resources exist in Azure

---

## Step 7 — Inspect State

```bash
terraform show             # human-readable state
terraform state list       # list all tracked resources
terraform state show azurerm_resource_group.rg   # details of one resource
```

---

## Step 8 — Modify and Re-apply

Change a tag or add a resource in `main.tf`, then:

```bash
terraform plan    # see the diff
terraform apply   # apply only the changes
```

Terraform is idempotent — running apply when nothing changed does nothing.

---

## Step 9 — Destroy

```bash
terraform destroy
```

Destroys everything tracked in state. Useful for lab teardown and ephemeral environments.

---

# Why Terraform?

## The Real Problem Terraform Solves

Before Terraform, infrastructure was:

* Created manually from Azure Portal
* Or scripted using CLI / PowerShell
* Or partially automated but inconsistent

Problems:

* No version control
* No reproducibility
* No consistency across environments
* Manual errors
* Hard to scale

Now imagine you need:

* 3 environments (dev, qa, prod)
* VNet + Subnets
* AKS cluster
* App Service
* Storage
* RBAC
* Monitoring

Would you click everything in portal each time? Painful.

That’s where Terraform comes in.

---

## What is Terraform?

Terraform is an **Infrastructure as Code (IaC)** tool created by:

HashiCorp

It allows you to:

* Define infrastructure using code
* Version control it
* Reproduce environments
* Automate provisioning

Terraform is **cloud-agnostic**.

It works with:

* Azure
* AWS
* GCP
* Kubernetes
* GitHub
* Databases
* And many more

---

# Why Terraform in Azure (Instead of ARM / Bicep)?

Azure has native IaC:

* ARM Templates
* Bicep

But Terraform is preferred when:

### Multi-cloud strategy

If your company uses:

* AWS + Azure
* Or hybrid

Terraform gives one language for all.

### Better state management

Terraform maintains state (we’ll discuss this soon).

### Cleaner syntax

HCL (HashiCorp Configuration Language) is easier than ARM JSON.

---

# Basic Terraform Concepts (Very Important)

Let’s understand core concepts clearly.

---

## 1 Provider

A provider is a plugin that talks to a cloud platform.

For Azure:

```hcl
provider "azurerm" {
  features {}
}
```

Provider tells Terraform:

> “Hey, we are creating resources in Azure.”

Other examples:

* aws
* google
* kubernetes

---

## 2 Resource

A resource is something you want to create.

Example: Azure Resource Group

```hcl
resource "azurerm_resource_group" "rg" {
  name     = "demo-rg"
  location = "East US"
}
```

Structure:

```
resource "<PROVIDER>_<TYPE>" "<LOCAL_NAME>" {
   arguments
}
```

Example:

* azurerm_resource_group
* azurerm_virtual_network
* azurerm_kubernetes_cluster

Think of resource as:

> A real cloud object.

---

## 3 Variables

Instead of hardcoding values:

```hcl
variable "location" {
  default = "East US"
}
```

Use:

```hcl
location = var.location
```

Why?

* Reusable code
* Different values per environment

Very useful for dev/qa/prod setups.

---

## 4 Output

After deployment, you may want:

* Public IP
* Endpoint
* Connection string

Example:

```hcl
output "resource_group_name" {
  value = azurerm_resource_group.rg.name
}
```

---

## 5 State File (Very Important Concept)

Terraform keeps track of infrastructure using a file:

```
terraform.tfstate
```

It stores:

* What resources exist
* Their IDs
* Their configuration

Why needed?

Because Terraform works using:

### Desired State vs Current State

Workflow:

1. You define desired state in .tf files
2. Terraform checks current state (cloud + tfstate)
3. It calculates difference
4. Applies only required changes

This is called:

> Execution Plan

---

## 6 Terraform Workflow

Very important for interviews and teaching.

### Step 1 – Initialize

```bash
terraform init
```

* Downloads provider plugins
* Initializes backend

---

### Step 2 – Plan

```bash
terraform plan
```

Shows:

* What will be created
* What will change
* What will be destroyed

Safe step.

---

### Step 3 – Apply

```bash
terraform apply
```

Actually creates infrastructure.

---

### Step 4 – Destroy (optional)

```bash
terraform destroy
```

Deletes everything defined.

Very useful for:

* Labs
* Temporary environments

---

# Important Terraform Concepts for Real Projects


## 4.1 Backend (Remote State)

By default:

* State stored locally

In real projects:

* Store in Azure Storage Account

Why?

* Team collaboration
* State locking
* Centralized management

In Azure:

* Blob Storage backend is used

---

## 4.2 Modules

Think of modules as reusable components.

Example:

* A VNet module
* An AKS module
* An App Service module

Like functions in programming.

Good DevOps architecture:

* Separate networking
* Separate compute
* Separate monitoring

---

## 4.3 Dependency Graph

Terraform automatically understands:

If:

* AKS depends on Resource Group
* VNet depends on Resource Group

It creates a graph and provisions in correct order.

You rarely need to manually define dependencies.

---

# How Terraform Works Internally 

When you run:

```
terraform apply
```

Internally:

1. Parses HCL
2. Builds dependency graph
3. Compares state
4. Calls Azure REST APIs via provider
5. Updates state file

So Terraform is NOT:

* A scripting tool
* Not imperative

It is:

> Declarative

You define WHAT.
Terraform decides HOW.

---

# Why DevOps Engineers Love Terraform

Because it enables:

* Infrastructure versioning (Git)
* CI/CD for infra
* Repeatable environments
* Blue/Green infra
* Disaster recovery recreation
* Immutable infrastructure

