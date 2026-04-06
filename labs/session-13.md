# SESSION 12 — Terraform + GitOps (ArgoCD) 

---

## Session Goal

* Provision infrastructure using Terraform
* Deploy applications using GitOps (ArgoCD)
* Understand drift detection and reconciliation

---

# Continuing Case Study

> Your CI pipeline is working.
>
> But:
>
> * Infra changes are manual
> * Deployments are not fully traceable
> * Cluster state can drift from desired state
>
> Your task:
> Move to Infrastructure as Code + GitOps model

---

# LAB 1 — Terraform Setup (Baseline)

---

## Tools

* Terraform installed
* Cloud credentials configured

---

## Basic Terraform File

```hcl id="c2f6gn"
provider "azurerm" {
  features {}
}

resource "azurerm_resource_group" "rg" {
  name     = "k8s-rg"
  location = "East US"
}
```

---

## Tasks

```bash id="v21w9r"
terraform init
terraform plan
terraform apply
```

---

---

# LAB 2 — Create Kubernetes Resource via Terraform

---

## Example

```hcl id="gn1q9m"
resource "kubernetes_namespace" "dev" {
  metadata {
    name = "dev"
  }
}
```

---

## Tasks

* Apply configuration
* Verify namespace

---

---

# LAB 3 — Terraform State Management

---

## Tasks

* Observe state file
* Modify resource
* Run `terraform plan`

---

## Concepts

* Desired vs current state

---

---

# LAB 4 — Modular Terraform (Basic)

---

## Structure

* Separate modules:

    * networking
    * cluster
    * resources

---

## Tasks

* Refactor simple config into module

---

---

# LAB 5 — Install ArgoCD

---

## Install

```bash id="uvqzsm"
kubectl create namespace argocd
kubectl apply -n argocd -f https://raw.githubusercontent.com/argoproj/argo-cd/stable/manifests/install.yaml
```

---

## Access UI

```bash id="q9g8sy"
kubectl port-forward svc/argocd-server -n argocd 8080:443
```

---

---

# LAB 6 — Connect Git Repo to ArgoCD

---

## Repo Structure

* Contains Kubernetes manifests for:

    * order-service
    * inventory-service

---

## Tasks

* Register repo in ArgoCD
* Create application

---

---

# LAB 7 — Deploy via ArgoCD

---

## Create Application

* Point to Git repo
* Define namespace

---

## Tasks

* Sync application
* Verify deployment

---

---

# LAB 8 — Drift Detection

---

## Inject Drift

* Manually change deployment:

```bash id="rz35xq"
kubectl scale deployment order-service --replicas=5
```

---

## Tasks

* Observe ArgoCD UI

---

## Expected

* OutOfSync state

---

---

# LAB 9 — Auto Sync

---

## Enable Auto Sync

---

## Tasks

* Modify cluster manually
* Watch ArgoCD revert changes

---

---

# LAB 10 — Git-Based Deployment

---

## Change in Git

* Update image version

---

## Tasks

* Commit change
* Observe automatic deployment

---

---

# LAB 11 — Combined Flow (CI + GitOps)

---

## Flow

1. Jenkins builds image
2. Updates Git repo (Ops repo)
3. ArgoCD syncs changes

---

## Tasks

* Trigger pipeline
* Observe Git update
* Watch ArgoCD deploy

---

---

# LAB 12 — Failure Scenario

---

## Inject Issue

* Invalid YAML in Git

---

## Tasks

* Observe ArgoCD error
* Fix and re-sync

---

---

# Real Case Scenario

---

## Issue

* Cluster state differs from Git
* Manual changes causing instability

---

## Investigation

* Check ArgoCD status
* Compare Git vs cluster

---

## Root Causes

* Manual changes
* No GitOps enforcement

---

## Resolution

* Enforce Git as source of truth
* Enable auto-sync

---

---

# Validation Tasks

---

* Terraform successfully provisions resources
* ArgoCD deploys application from Git
* Drift detection works correctly
* Auto-sync restores desired state

---

---

# Final Architecture After Session

```id="r4u2tw"
Git → Terraform (infra)
Git → ArgoCD (apps)

Jenkins → updates Git → ArgoCD sync → Kubernetes
```


