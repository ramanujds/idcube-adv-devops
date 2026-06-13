# Core building blocks of Terraform:

---

# Providers

### What is a Provider?

A provider is a plugin that allows Terraform to talk to a platform.

Examples:

* Azure → `azurerm`
* AWS → `aws`
* Kubernetes → `kubernetes`
* GitHub → `github`

Without a provider, Terraform can’t create anything.

Example:

```hcl
provider "azurerm" {
  features {}
}
```

What happens internally:

1. `terraform init` downloads the provider binary.
2. Provider authenticates.
3. Provider calls cloud APIs.
4. Provider translates Terraform config → API calls.

So provider = API bridge.

Think:

Terraform = Brain
Provider = Hands that call APIs

---

# State

This is where most confusion starts.

Terraform is declarative.
You define desired state.

But how does Terraform know what already exists?

→ It stores information in a file called:

```
terraform.tfstate
```

State contains:

* Resource IDs
* Current attributes
* Dependency mapping

Why state exists:

Because Terraform must compare:

Current real infrastructure
vs
Your configuration files

Then it computes a diff.

That’s what `terraform plan` shows.

If state is lost:

* Terraform thinks nothing exists
* It will try to recreate everything

So state is Terraform’s memory.

---

# Backend

By default:

State is stored locally.

That’s fine for personal projects.

But in real DevOps teams?

Local state = disaster.

Why?

* No collaboration
* No locking
* Risk of corruption
* No versioning

So we configure a backend.

Backend = where state is stored.

Example Azure backend:

```hcl
terraform {
  backend "azurerm" {
    resource_group_name  = "tfstate-rg"
    storage_account_name = "tfstateaccount"
    container_name       = "tfstate"
    key                  = "dev.terraform.tfstate"
  }
}
```

Now state is:

* Stored in Azure Blob Storage
* Locked during apply
* Shared across team
* Safer

Backend manages state storage.

State = data
Backend = storage location

---

# Variables

Variables make Terraform reusable.

Without variables:

```hcl
location = "South India"
```

Hardcoded.

With variables:

```hcl
variable "location" {
  default = "South India"
}
```

Use it:

```hcl
location = var.location
```

Now you can:

* Change environment
* Change VM size
* Parameterize deployments

Variables help with:

* Environment separation
* Reusability
* Cleaner code

Think of them like function parameters in programming.

---

# Modules

Modules are reusable Terraform components.

If you repeat:

* VNet creation
* VM creation
* AKS setup

Across projects, that’s messy.

Instead, you create a module.

Example:

```
modules/network
modules/vm
```

Then call it:

```hcl
module "network" {
  source   = "../../modules/network"
  location = var.location
}
```

Modules allow:

* Code reuse
* Clean separation
* Standardization
* Enterprise patterns

Root module = main project
Child modules = reusable components

Think:

Modules are like functions or classes in programming.

---

# Workspaces

Workspaces allow multiple state files in the same configuration.

Default workspace:

```
default
```

Create new workspace:

```bash
terraform workspace new dev
terraform workspace new prod
```

Each workspace:

* Has its own state
* Uses same code
* Different infrastructure

Useful for:

* Dev
* QA
* Prod

But important:

Workspaces are not a full environment strategy.

In real projects, we often prefer:

* Separate folders per environment
* Separate backend keys

Workspaces are good for:

* Simple separation
* Temporary environments

Not ideal for complex enterprise setups.

---

# Locals

Locals let you compute and name intermediate values without exposing them as variables.

```hcl
locals {
  env    = "dev"
  prefix = "idcube-${local.env}"
}

resource "azurerm_resource_group" "rg" {
  name     = "${local.prefix}-rg"
  location = "South India"
}
```

Use locals when:

* You repeat the same expression in many places
* You want to avoid magic strings
* You build composite names from multiple inputs

---

# Data Sources

Data sources let Terraform read existing infrastructure (not managed by this config).

```hcl
data "azurerm_resource_group" "existing" {
  name = "pre-existing-rg"
}

resource "azurerm_virtual_network" "vnet" {
  resource_group_name = data.azurerm_resource_group.existing.name
  location            = data.azurerm_resource_group.existing.location
  name                = "my-vnet"
  address_space       = ["10.0.0.0/16"]
}
```

Rule of thumb:

* `resource` = Terraform creates and owns it
* `data` = already exists, Terraform only reads it

---

# depends_on

Terraform builds a dependency graph automatically from resource references.
Use `depends_on` only when the dependency is not expressed through a reference.

```hcl
resource "azurerm_network_interface" "nic" {
  # ...
  depends_on = [azurerm_subnet.subnet, azurerm_public_ip.public_ip]
}
```

Example from `terraform-azure-vm/main.tf` — the NIC explicitly waits for subnet
and public IP to be ready because it needs both before it can be associated.

---

# count and for_each

## count — create N copies

```hcl
resource "azurerm_resource_group" "rg" {
  count    = 3
  name     = "rg-${count.index}"
  location = "East US"
}
```

## for_each — create one per map/set entry

```hcl
variable "envs" {
  default = { dev = "East US", prod = "West US" }
}

resource "azurerm_resource_group" "rg" {
  for_each = var.envs
  name     = "rg-${each.key}"
  location = each.value
}
```

`for_each` is preferred over `count` for named resources — removing an item
from the middle of a `count` list destroys and recreates everything after it.

---

# lifecycle

Controls how Terraform handles resource changes.

```hcl
resource "azurerm_kubernetes_cluster" "aks" {
  # ...
  lifecycle {
    prevent_destroy       = true          # fail apply if this resource would be destroyed
    ignore_changes        = [tags]        # don't update tags if changed outside Terraform
    create_before_destroy = true          # spin up replacement before tearing down old one
  }
}
```

Use `prevent_destroy = true` on production databases and clusters.
Use `ignore_changes` for fields managed by external systems (e.g., auto-scaling node counts).

---

# How Everything Connects

When you run:

```bash
terraform init
terraform plan
terraform apply
```

Terraform does this:

1. Load configuration
2. Load variables and locals
3. Load state from backend
4. Initialize provider
5. Compare desired vs current
6. Build dependency graph
7. Apply changes via provider (in parallel where safe)
8. Update state

Every concept plays a role:

| Concept         | Role                                             |
|-----------------|--------------------------------------------------|
| Provider        | API bridge to the cloud                          |
| Resource        | A real cloud object to manage                    |
| Data            | Read-only reference to existing infra            |
| State           | Terraform's memory                               |
| Backend         | Where state is stored                            |
| Variables       | Input parameters                                 |
| Locals          | Computed intermediate values                     |
| Outputs         | Values exposed after apply                       |
| Modules         | Reusable building blocks                         |
| Workspaces      | Multiple isolated state environments             |
| count/for_each  | Create multiple resource instances               |
| lifecycle       | Fine-grained control over create/update/destroy  |

