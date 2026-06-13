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

