# Adding Existing Resources to Terraform

## The Problem

You have infrastructure that was created manually — through the Azure Portal, Azure CLI,
or some other tool — and now you want Terraform to manage it.

If you just write a `.tf` file describing that resource and run `terraform apply`,
Terraform will try to **create a duplicate** — it has no idea the resource already exists.

The solution is **`terraform import`** — it brings an existing resource into Terraform's
state file without recreating it.

---

## How Import Works

```text
Existing Azure resource
        │
        │  terraform import
        ▼
terraform.tfstate  ←── Terraform now "knows" about it
        │
        │  terraform plan
        ▼
Diff between .tf config and actual state
        │
        │  terraform apply
        ▼
Terraform manages it going forward
```

Import does **not** generate `.tf` code for you. You write the config, then import
the real resource into state so Terraform sees them as the same thing.

---

## General Workflow (Step by Step)

### Step 1 — Find the Azure Resource ID

Every Azure resource has a unique Resource ID. You need this for the import command.

**From Azure CLI:**

```bash
# Resource Group
az group show --name <name> --query id -o tsv

# AKS Cluster
az aks show --name <cluster-name> --resource-group <rg> --query id -o tsv

# Virtual Machine
az vm show --name <vm-name> --resource-group <rg> --query id -o tsv

# Virtual Network
az network vnet show --name <vnet-name> --resource-group <rg> --query id -o tsv

# Subnet
az network vnet subnet show \
  --name <subnet-name> \
  --vnet-name <vnet-name> \
  --resource-group <rg> \
  --query id -o tsv
```

**From Azure Portal:**
Go to the resource → **Properties** → copy the **Resource ID** field.

---

### Step 2 — Write the Terraform configuration block

Write a `resource` block in your `.tf` file that describes the existing resource.
The arguments don't have to be perfect yet — you'll fix them after import.

```hcl
resource "azurerm_resource_group" "rg" {
  name     = "idcube-aks"
  location = "South India"
}
```

### Step 3 — Initialize Terraform

```bash
terraform init
```

### Step 4 — Run `terraform import`

```bash
terraform import <resource_type>.<local_name> <azure_resource_id>
```

### Step 5 — Run `terraform plan`

```bash
terraform plan
```

After import, plan will show any differences between your `.tf` config and the real
resource's attributes. Fix those mismatches in your `.tf` file until plan shows
**No changes**.

### Step 6 — Commit the config and state

Once `terraform plan` shows no changes, the resource is fully under Terraform management.

---

## Example 1 — Import an Existing Resource Group

### Scenario

You created `idcube-aks` resource group from the Azure Portal and now want
Terraform to manage it.

### Find the Resource ID

```bash
az group show --name idcube-aks --query id -o tsv
# /subscriptions/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx/resourceGroups/idcube-aks
```

### Write the config (`main.tf`)

```hcl
provider "azurerm" {
  features {}
}

resource "azurerm_resource_group" "rg" {
  name     = "idcube-aks"
  location = "South India"
}
```

### Import

```bash
terraform import azurerm_resource_group.rg \
  /subscriptions/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx/resourceGroups/idcube-aks
```

Output:

```text
azurerm_resource_group.rg: Importing from ID "/subscriptions/.../resourceGroups/idcube-aks"...
azurerm_resource_group.rg: Import prepared!
  Prepared azurerm_resource_group for import
azurerm_resource_group.rg: Refreshing state...

Import successful!
```

### Verify

```bash
terraform plan
# Should show: No changes. Your infrastructure matches the configuration.
```

---

## Example 2 — Import an Existing AKS Cluster

### AKS Cluster scenario

You have an AKS cluster `idcube-cluster` in resource group `idcube-aks` that was
created via the Portal or `az aks create`, and you want Terraform to manage it.

### AKS resource ID

```bash
az aks show \
  --name idcube-cluster \
  --resource-group idcube-aks \
  --query id -o tsv
# /subscriptions/xxxx/resourceGroups/idcube-aks/providers/Microsoft.ContainerService/managedClusters/idcube-cluster
```

### Write a minimal config first

Start with the required fields only. You can fill in the rest after import.

```hcl
resource "azurerm_resource_group" "aks_rg" {
  name     = "idcube-aks"
  location = "South India"
}

resource "azurerm_kubernetes_cluster" "aks" {
  name                = "idcube-cluster"
  location            = azurerm_resource_group.aks_rg.location
  resource_group_name = azurerm_resource_group.aks_rg.name
  dns_prefix          = "idcube"

  default_node_pool {
    name       = "systempool"
    node_count = 2
    vm_size    = "Standard_D2s_v3"
  }

  identity {
    type = "SystemAssigned"
  }
}
```

### Import the resource group first, then the cluster

```bash
# 1. Import the resource group
terraform import azurerm_resource_group.aks_rg \
  /subscriptions/xxxx/resourceGroups/idcube-aks

# 2. Import the AKS cluster
terraform import azurerm_kubernetes_cluster.aks \
  /subscriptions/xxxx/resourceGroups/idcube-aks/providers/Microsoft.ContainerService/managedClusters/idcube-cluster
```

### Run plan and fix mismatches

```bash
terraform plan
```

The plan will show fields where your config differs from the real cluster — for example,
`os_disk_size_gb`, `network_profile`, or `tags`. Update your `.tf` file to match until
plan shows no changes.

```bash
terraform plan
# No changes. Your infrastructure matches the configuration.
```

---

## Example 3 — Import an Existing Linux VM

### VM resource ID

```bash
az vm show \
  --name idcube-vm \
  --resource-group idcube-rg \
  --query id -o tsv
# /subscriptions/xxxx/resourceGroups/idcube-rg/providers/Microsoft.Compute/virtualMachines/idcube-vm
```

### VM config

```hcl
resource "azurerm_linux_virtual_machine" "vm" {
  name                = "idcube-vm"
  resource_group_name = "idcube-rg"
  location            = "South India"
  size                = "Standard_D2s_v3"
  admin_username      = "azureuser"

  network_interface_ids = [
    azurerm_network_interface.nic.id,
  ]

  os_disk {
    caching              = "ReadWrite"
    storage_account_type = "Standard_LRS"
  }

  source_image_reference {
    publisher = "Canonical"
    offer     = "0001-com-ubuntu-server-jammy"
    sku       = "22_04-lts"
    version   = "latest"
  }

  admin_password                  = "Password@123"
  disable_password_authentication = false
}
```

### Import

### VM import command

```bash
terraform import azurerm_linux_virtual_machine.vm \
  /subscriptions/xxxx/resourceGroups/idcube-rg/providers/Microsoft.Compute/virtualMachines/idcube-vm
```

---

## Example 4 — Import a Virtual Network and Subnet

VNet and subnet are separate resources in Terraform — import each one individually.

### VNet and subnet resource IDs

```bash
az network vnet show \
  --name idcube-vnet \
  --resource-group idcube-rg \
  --query id -o tsv

az network vnet subnet show \
  --name idcube-subnet \
  --vnet-name idcube-vnet \
  --resource-group idcube-rg \
  --query id -o tsv
```

### VNet and subnet import commands

```bash
terraform import azurerm_virtual_network.vnet \
  /subscriptions/xxxx/resourceGroups/idcube-rg/providers/Microsoft.Network/virtualNetworks/idcube-vnet

terraform import azurerm_subnet.subnet \
  /subscriptions/xxxx/resourceGroups/idcube-rg/providers/Microsoft.Network/virtualNetworks/idcube-vnet/subnets/idcube-subnet
```

---

## Terraform 1.5+ — `import` Block (Alternative to CLI)

From Terraform 1.5 onwards, you can declare imports inside your `.tf` file instead of
running a CLI command. This makes imports reproducible and version-controlled.

```hcl
import {
  to = azurerm_resource_group.rg
  id = "/subscriptions/xxxx/resourceGroups/idcube-aks"
}

import {
  to = azurerm_kubernetes_cluster.aks
  id = "/subscriptions/xxxx/resourceGroups/idcube-aks/providers/Microsoft.ContainerService/managedClusters/idcube-cluster"
}

resource "azurerm_resource_group" "rg" {
  name     = "idcube-aks"
  location = "South India"
}

resource "azurerm_kubernetes_cluster" "aks" {
  name                = "idcube-cluster"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  dns_prefix          = "idcube"
  # ...
}
```

Run `terraform plan` — Terraform handles the import as part of the plan/apply cycle.
After apply, remove the `import {}` blocks (they're no longer needed).

---

## Terraform 1.5+ — `generate config` (Auto-generate `.tf` from existing resource)

If you don't want to write the config manually, Terraform can generate it for you.

```bash
terraform plan -generate-config-out=generated.tf
```

This writes a `generated.tf` file with the full resource block populated from the real
resource. Review and clean up the generated file, then run `terraform apply`.

> This feature is experimental in 1.5/1.6 and stabilises in later versions.
> Always review generated output — it may include read-only attributes that need to be removed.

---

## Quick Reference — Common Azure Resource Import IDs

| Resource | Import ID format |
| --- | --- |
| Resource Group | `/subscriptions/<sub>/resourceGroups/<name>` |
| AKS Cluster | `/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.ContainerService/managedClusters/<name>` |
| Linux VM | `/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.Compute/virtualMachines/<name>` |
| Virtual Network | `/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.Network/virtualNetworks/<name>` |
| Subnet | `/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.Network/virtualNetworks/<vnet>/subnets/<name>` |
| Public IP | `/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.Network/publicIPAddresses/<name>` |
| Network Interface | `/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.Network/networkInterfaces/<name>` |
| NSG | `/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.Network/networkSecurityGroups/<name>` |
| Storage Account | `/subscriptions/<sub>/resourceGroups/<rg>/providers/Microsoft.Storage/storageAccounts/<name>` |

---

## Best Practices

### Import resources in dependency order

If resource B depends on resource A, import A first. Terraform needs A in state
before it can validate B's reference to it.

```bash
# Correct order
terraform import azurerm_resource_group.rg   <rg-id>
terraform import azurerm_virtual_network.vnet <vnet-id>
terraform import azurerm_subnet.subnet         <subnet-id>
terraform import azurerm_network_interface.nic <nic-id>
terraform import azurerm_linux_virtual_machine.vm <vm-id>
```

### Always run `terraform plan` after each import

Check for mismatches immediately after importing each resource. Fixing one resource at
a time is easier than untangling all mismatches at the end.

### Do not run `terraform apply` until plan shows no changes

If `terraform plan` shows changes after import, applying them will modify or potentially
recreate the resource. Fix the `.tf` config to match the real resource first.

### Use `ignore_changes` for fields managed outside Terraform

Some fields are managed by Azure automatically (e.g., auto-scaling node counts, rotation
timestamps). Tell Terraform to stop tracking them:

```hcl
resource "azurerm_kubernetes_cluster" "aks" {
  lifecycle {
    ignore_changes = [
      default_node_pool[0].node_count,   # managed by autoscaler
      tags["LastModified"],              # set by an external process
    ]
  }
}
```

### Never manually edit `terraform.tfstate`

If state gets out of sync, use `terraform state` commands — not direct file edits:

```bash
terraform state list                      # see what's in state
terraform state show <resource>           # inspect one resource
terraform state rm <resource>             # remove from state (does not delete real resource)
terraform state mv <old> <new>            # rename a resource in state
```

### Use `terraform state rm` to stop managing a resource without deleting it

If you want Terraform to forget about a resource (hand it back to manual management)
without destroying it:

```bash
terraform state rm azurerm_kubernetes_cluster.aks
```

Terraform removes it from state. The real AKS cluster is untouched.

---

## Common Errors

### `Error: A resource with the ID already exists`

Terraform found the resource in state but you're trying to import it again.
Check `terraform state list` — it may already be imported.

### `Error: Cannot import non-existent remote object`

The Azure Resource ID is wrong or the resource does not exist.
Double-check with `az <resource> show ...` before importing.

### Plan shows `~ update` after import

Your `.tf` config has a value that differs from the real resource.
Common culprits:

* `location` casing — Azure returns `"southindia"` but you wrote `"South India"` (both are accepted — use `terraform plan` output to see the exact value)
* `vm_size` — check the exact SKU name with `az vm list-sizes`
* `os_disk_size_gb` — if not set explicitly, Azure assigns a default; add it to your config

### Plan shows `-/+` (destroy and recreate) after import

Some fields are `ForceNew` in the provider — changing them destroys and recreates the
resource. Do not change those fields in your `.tf` config after import. Match the
real value exactly.
