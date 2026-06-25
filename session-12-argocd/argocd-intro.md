# Argo CD is a popular GitOps tool for Kubernetes.

---

# 1. The Core Idea: Desired State vs Actual State

ArgoCD works on a very simple principle.

There are **two states**:

```
Desired State  → defined in Git
Actual State   → running in Kubernetes
```

ArgoCD continuously compares them inside **Kubernetes**.

Example:

```
Git
replicas: 3

Cluster
replicas: 1
```

ArgoCD detects drift and **reconciles the cluster**.

This process is called **state reconciliation**.

---

# 2. The Git Repository (Source of Truth)

In GitOps, **Git becomes the single source of truth**.

Example repo:

```
k8s-config
 ├── inventory-service
 │     deployment.yaml
 │     service.yaml
 ├── order-service
 └── gateway
```

Example deployment:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: inventory-service
spec:
  replicas: 2
  template:
    spec:
      containers:
        - name: inventory
          image: ramanuj/inventory-service:v2
```

ArgoCD reads these manifests.

---

# 3. ArgoCD Application

The core object in ArgoCD is an **Application**.

An **Application** tells ArgoCD:

```
Where is the Git repo?
Which path contains manifests?
Which Kubernetes cluster to deploy to?
Which namespace?
```

Example:

```yaml
apiVersion: argoproj.io/v1alpha1
kind: Application
metadata:
  name: inventory-service
spec:
  source:
    repoURL: https://github.com/company/k8s-config
    path: inventory-service
  destination:
    server: https://kubernetes.default.svc
    namespace: inventory
```

So an Application = **deployment definition**.

---

# 4. Sync Operation

ArgoCD performs **sync operations**.

Sync means:

```
Git → Kubernetes
```

Example:

```
Git image: v2
Cluster image: v1
```

ArgoCD sync will deploy **v2**.

---

## Manual Sync

Engineer clicks **SYNC** in UI.

```
Git change
   ↓
ArgoCD detects
   ↓
Manual sync triggered
```

---

## Auto Sync

Most production setups enable **auto-sync**.

Then ArgoCD deploys automatically when Git changes.

```
Git commit
   ↓
ArgoCD detects
   ↓
Automatic deployment
```

---

# 5. Sync Status

ArgoCD constantly reports the status of applications.

Three common states:

### Synced

```
Git = Cluster
```

Everything matches.

---

### OutOfSync

```
Git ≠ Cluster
```

Cluster needs update.

Example:

```
Git image = v2
Cluster image = v1
```

---

### Degraded

Deployment failed.

Example:

```
pod crashloop
missing secret
bad config
```

---

# 6. Self-Healing

ArgoCD can automatically fix manual changes.

Example:

Someone runs:

```bash
kubectl scale deployment inventory --replicas=10
```

But Git says:

```
replicas: 2
```

ArgoCD will **restore it back to 2**.

This prevents configuration drift.

---

# 7. Pruning

Sometimes resources are removed from Git.

Example:

```
Git
(no service.yaml)
```

But cluster still has the service.

ArgoCD can **prune unused resources** automatically.

```
Git removed resource
   ↓
ArgoCD deletes it from cluster
```

---

# 8. Multi-Cluster Support

ArgoCD can manage multiple clusters.

Example:

```
Dev cluster
QA cluster
Prod cluster
```

Deployment configuration:

```
Application → target cluster
```

Example:

```
inventory-dev
inventory-prod
```

Same repo, different environments.

---

# 9. Application Groups

For microservices platforms.

Example:

```
platform
 ├── inventory-service
 ├── order-service
 ├── gateway
 └── monitoring
```

Instead of managing each separately:

```
Parent App
   ↓
Child Apps
```

This pattern is called **App of Apps**.

Used heavily in large Kubernetes platforms.

---

# 10. ArgoCD UI

ArgoCD provides a visual dashboard.

You can see:

```
Applications
Sync status
Health status
Deployment history
```

Example view:

```
inventory-service
Status: Synced
Health: Healthy
```

This becomes a **central control panel for Kubernetes deployments**.

---

# 11. How It Fits Your Current Pipeline

Right now your pipeline likely looks like:

```
GitHub
   ↓
Jenkins
   ↓
Build Docker Image
   ↓
Push Image
   ↓
kubectl apply
```

With ArgoCD:

```
GitHub
   ↓
Jenkins
   ↓
Build Docker Image
   ↓
Push Image
   ↓
Update Kubernetes manifest in Git
   ↓
ArgoCD deploys automatically
```

Jenkins **stops talking to Kubernetes directly**.

---

# 12. Final GitOps Architecture

Typical enterprise setup:

```
Terraform
   ↓
Creates AKS

Jenkins
   ↓
Builds Docker images

GitOps Repo
   ↓
Helm charts / manifests

ArgoCD
   ↓
Deploys to Kubernetes
```

Infrastructure, CI, and CD are cleanly separated.

---
