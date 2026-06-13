provider "kubernetes" {
  config_path = "~/.kube/config"
}

resource "kubernetes_deployment" "part-inventory-deployment" {
  metadata {
    name = "part-inventory-deployment"
  }

  spec {
    replicas = 1

    selector {
      match_labels = {
        app = "part-inventory"
      }
    }

    template {
      metadata {
        labels = {
          app = "part-inventory"
        }
      }

      spec {
        container {
          name  = "part-inventory-container"
          image = "ram1uj/part-inventory-service:latest"

          port {
            container_port = 8080
          }
        }
      }
    }
  }
}

resource "kubernetes_deployment" "part-order-deployment" {
  metadata {
    name = "part-order-deployment"
  }

  spec {
    replicas = 1

    selector {
      match_labels = {
        app = "part-order"
      }
    }

    template {
      metadata {
        labels = {
          app = "part-order"
        }
      }

      spec {
        container {
          name  = "part-order-container"
          image = "ram1uj/part-order-service:latest"

          env {
            name  = "INVENTORY_SERVICE_URL"
            value = "http://part-inventory-service:8080"
          }
          env {
            name  = "SPRING_PROFILES_ACTIVE"
            value = "dev"
          }

          port {
            container_port = 8080
          }
        }
      }
    }
  }
}

resource "kubernetes_deployment" "part-gateway-deployment" {
  metadata {
    name = "part-gateway-deployment"
  }

  spec {
    replicas = 1

    selector {
      match_labels = {
        app = "part-gateway"
      }
    }

    template {
      metadata {
        labels = {
          app = "part-gateway"
        }
      }

      spec {
        container {
          name  = "part-gateway-container"
          image = "ram1uj/part-gateway-service:latest"
          env {
            name  = "PART_INVENTORY_SERVICE_URL"
            value = "http://part-inventory-service:8080"
          }
          env {
            name  = "PART_ORDER_SERVICE_URL"
            value = "http://part-order-service:8080"
          }

          port {
            container_port = 8080
          }
        }
      }
    }
  }
}


resource "kubernetes_service" "part-inventory-service" {
  metadata {
    name = "part-inventory-service"
  }

  spec {
    selector = {
      app = "part-inventory"
    }
    type = "NodePort"

    port {
      port        = 8080
      target_port = 8080
      node_port   = 30081
    }
  }
}

resource "kubernetes_service" "part-order-service" {
  metadata {
    name = "part-order-service"
  }

  spec {
    selector = {
      app = "part-order"
    }
    type = "NodePort"

    port {
      port        = 8080
      target_port = 8080
      node_port   = 30082
    }
  }
}

resource "kubernetes_service" "part-gateway-service" {
  metadata {
    name = "part-gateway-service"
  }

  spec {
    selector = {
      app = "part-gateway"
    }
    type = "NodePort"

    port {
      port        = 8080
      target_port = 8080
      node_port   = 30080
    }
  }
}
