# SESSION 9 — ACR & Container Supply Chain 

---

## Session Goal

* Work with a private container registry
* Configure authentication (SPN / Managed Identity)
* Implement image tagging and governance
* Debug image pull failures

---

# Continuing Case Study

> Your applications are stable and monitored.
>
> New requirements:
>
> * Move images to private registry
> * Secure image access
> * Ensure compliance and traceability
>
> Issues observed:
>
> * Pods failing with image pull errors
> * Unauthorized access errors
>
> Your task:
> Secure and manage container supply chain

---

# LAB 1 — Push Images to ACR (Baseline)

---

## Setup

* Build images:

    * `order-service`
    * `inventory-service`

---

## Login to ACR

```bash id="n9ty6n"
az acr login --name <acr-name>
```

---

## Tag & Push

```bash id="95wnxf"
docker tag order-service:v1 <acr-name>.azurecr.io/order-service:v1
docker push <acr-name>.azurecr.io/order-service:v1
```

---

## Validate

* Check repository in ACR

---

---

# LAB 2 — Deploy Using Private Registry

---

## Update Deployment

```yaml id="p0rqm6"
image: <acr-name>.azurecr.io/order-service:v1
```

---

## Tasks

* Deploy to cluster
* Verify pod status

---

---

# LAB 3 — Unauthorized Image Pull (401)

---

## Inject Issue

* Do not configure authentication

---

## Observe

* Pod status: `ImagePullBackOff`

---

## Debug

```bash id="1nmf6j"
kubectl describe pod <pod-name>
```

---

## Expected

* 401 Unauthorized error

---

---

# LAB 4 — Fix Using Service Principal (SPN)

---

## Create SPN

```bash id="5n7b5o"
az ad sp create-for-rbac --name acr-sp --role acrpull --scopes <acr-id>
```

---

## Create Kubernetes Secret

```bash id="kz28uv"
kubectl create secret docker-registry acr-secret \
  --docker-server=<acr-name>.azurecr.io \
  --docker-username=<appId> \
  --docker-password=<password>
```

---

## Update Deployment

```yaml id="b0c2ju"
imagePullSecrets:
- name: acr-secret
```

---

## Verify

* Pods pull image successfully

---

---

# LAB 5 — Managed Identity (AKS Recommended)

---

## Setup

* Attach ACR to AKS

```bash id="p8zv4o"
az aks update -n <aks-name> -g <rg> --attach-acr <acr-name>
```

---

## Tasks

* Remove imagePullSecret
* Redeploy

---

## Observe

* Pods pull images without secret

---

---

# LAB 6 — Image Tagging Strategy

---

## Tags to Create

```bash id="pfq0px"
order-service:v1
order-service:v2
order-service:build-101
```

---

## Tasks

* Deploy using specific tag
* Simulate rollback

---

---

# LAB 7 — Break Tag (Image Not Found)

---

## Inject Issue

```yaml id="6u6ydy"
image: <acr-name>.azurecr.io/order-service:v99
```

---

## Observe

* ImagePullBackOff

---

## Debug

* Event shows image not found

---

## Fix

* Correct tag

---

---

# LAB 8 — ACR Import (No External Pull)

---

## Command

```bash id="1v6mti"
az acr import --name <acr-name> --source docker.io/library/nginx:latest --image nginx:latest
```

---

## Tasks

* Use imported image
* Deploy in cluster

---

---

# LAB 9 — Retention Strategy

---

## Scenario

* Too many unused images

---

## Tasks

* Identify old tags
* Discuss cleanup strategy

---

---

# LAB 10 — Base Image Governance

---

## Scenario

* Security team restricts external images

---

## Tasks

* Mirror base images into ACR
* Use only approved images

---

---

# LAB 11 — Combined Scenario

---

## Scenario

* Deployment failing due to:

    * Unauthorized pull
    * Wrong tag
    * Missing permissions

---

## Tasks

* Identify issue
* Fix step-by-step

---

---

# Real Case Scenario

---

## Issue

* Deployment fails after moving to private registry
* Some pods work, others fail

---

## Investigation

```bash id="dtrcsl"
kubectl describe pod
```

---

## Root Causes

* Missing permissions
* Incorrect tag
* Misconfigured identity

---

## Resolution

* Configure proper auth
* Fix image tags
* Validate ACR access

---

---

# Validation Tasks

---

* Images pulled successfully from ACR
* No unauthorized errors
* Correct version deployed

---

---

# Final Architecture After Session

```id="r4q3dc"
ACR → AKS → order-service → inventory-service
```

---


