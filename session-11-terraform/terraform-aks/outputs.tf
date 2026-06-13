output "cluster_name" {
  value = azurerm_kubernetes_cluster.aks.name
}

output "resource_group" {
  value = azurerm_resource_group.aks_rg.name
}

output "connect_command" {
  value = "az aks get-credentials --resource-group idcube-aks --name idcube-cluster"
}
