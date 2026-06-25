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


resource "azurerm_resource_group" "rg" {
  location = "South India"
  name     = "idcube-rg"
}

resource "azurerm_virtual_network" "vnet" {
  location            = azurerm_resource_group.rg.location
  name                = "idcube-vnet"
  resource_group_name = azurerm_resource_group.rg.name
  address_space       = ["10.0.0.0/16"]
}

resource "azurerm_subnet" "subnet" {
  name                 = "idcube-subnet"
  resource_group_name  = azurerm_resource_group.rg.name
  virtual_network_name = azurerm_virtual_network.vnet.name
  address_prefixes     = ["10.0.1.0/24"]
}



# Resource Group
resource "azurerm_resource_group" "aks_rg" {
  name     = "idcube-aks"
  location = "South India"
}

# AKS Cluster
resource "azurerm_kubernetes_cluster" "aks" {
  name                = "idcube-cluster"
  location            = azurerm_resource_group.aks_rg.location
  resource_group_name = azurerm_resource_group.aks_rg.name
  dns_prefix          = "idcube"

  sku_tier = "Free"

  default_node_pool {
    name       = "systempool"
    node_count = 2
    vm_size    = "Standard_D2s_v3"

    type = "VirtualMachineScaleSets"

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
  oidc_issuer_enabled = true

  tags = {
    environment = "dev"
    project     = "idcube"
  }
}
