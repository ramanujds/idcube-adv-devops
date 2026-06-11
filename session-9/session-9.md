# SESSION 9 — ACR & Container Supply Chain

---

## Session Goal

* Provision an AKS cluster (CLI, Portal, and Terraform)
* Work with a private container registry (Azure Container Registry)
* Build and push all three microservices to ACR
* Configure authentication (SPN / Managed Identity)
* Implement image tagging and governance strategy
* Debug and fix image pull failures in Kubernetes

---

## Project Details (Used Throughout This Session)

| Setting | Value |
|---|---|
| Resource Group | `idcube-aks` |
| AKS Cluster Name | `idcube-cluster` |
| ACR Name | `idcubeacr` |
| Location | `South India` (`southindia`) |
| Node VM Size | `Standard_D2s_v4` |
| Node Count | 2 |
| Identity | SystemAssigned (Managed Identity) |
| Network Plugin | kubenet |
| SKU Tier | Free |

---

## SETUP — Provision the AKS Cluster

Three approaches are covered: **Azure CLI**, **Azure Portal (Console)**, and **Terraform**.

---

## Option A — Azure CLI

### Step 1: Login and Set Subscription

```bash
az login
az account list --output table
az account set --subscription "<your-subscription-id>"
```

### Step 2: Create the Resource Group

```bash
az group create \
  --name idcube-aks \
  --location southindia
```

Verify:

```bash
az group show --name idcube-aks --query location --output tsv
# southindia
```

### Step 3: Create the AKS Cluster

```bash
az aks create \
  --name idcube-cluster \
  --resource-group idcube-aks \
  --location southindia \
  --node-count 2 \
  --node-vm-size Standard_D2s_v4 \
  --nodepool-name systempool \
  --os-disk-size-gb 30 \
  --network-plugin kubenet \
  --load-balancer-sku standard \
  --enable-managed-identity \
  --generate-ssh-keys \
  --tags environment=dev project=idcube \
  --dns-name-prefix idcube
```

**Flag-by-flag explanation:**

| Flag | Value | Why |
|---|---|---|
| `--node-count` | `2` | Two nodes for resilience — pods can spread across both |
| `--node-vm-size` | `Standard_D2s_v4` | 2 vCPU, 8 GB RAM, premium SSD support |
| `--nodepool-name` | `systempool` | System pool runs kube-system components |
| `--os-disk-size-gb` | `30` | Minimum for system + container image layers |
| `--network-plugin` | `kubenet` | Simpler networking, no VNet pre-provisioning needed |
| `--load-balancer-sku` | `standard` | Required for outbound SNAT and multiple front-ends |
| `--enable-managed-identity` | — | SystemAssigned identity — no service principal credentials to manage |
| `--generate-ssh-keys` | — | Creates SSH keypair for node access if needed |
| `--dns-name-prefix` | `idcube` | API server FQDN: `idcube-<hash>.southindia.azmk8s.io` |

This takes **5–10 minutes**. Watch progress:

```bash
# While waiting, monitor in another terminal
watch az aks show \
  --name idcube-cluster \
  --resource-group idcube-aks \
  --query provisioningState \
  --output tsv
# Running → Succeeded
```

### Step 4: Connect kubectl to the Cluster

```bash
az aks get-credentials \
  --resource-group idcube-aks \
  --name idcube-cluster

# Verify connection
kubectl get nodes
```

Expected output:

```
NAME                               STATUS   ROLES    AGE   VERSION
aks-systempool-12345678-vmss000000  Ready    <none>   3m    v1.29.x
aks-systempool-12345678-vmss000001  Ready    <none>   3m    v1.29.x
```

Both nodes `Ready`, `Standard_D2s_v4` under the hood (VMSS-backed).

---

## Option B — Azure Portal (Console)

### Step 1: Navigate to AKS

1. Go to [portal.azure.com](https://portal.azure.com)
2. Search for **"Kubernetes services"** in the top search bar
3. Click **+ Create** → **Create a Kubernetes cluster**

### Step 2: Basics Tab

| Field | Value |
|---|---|
| Subscription | Your subscription |
| Resource group | `idcube-aks` (create new if needed) |
| Cluster preset configuration | Dev/Test |
| Kubernetes cluster name | `idcube-cluster` |
| Region | **(Asia Pacific) South India** |
| Availability zones | None (for dev) |
| AKS pricing tier | Free |
| Kubernetes version | Leave at default (latest stable) |
| Automatic upgrade | Disabled |

### Step 3: Node Pools Tab

Click on the default `agentpool` → **Edit**:

| Field | Value |
|---|---|
| Node pool name | `systempool` |
| Mode | System |
| OS SKU | Ubuntu Linux |
| Node size | **Standard_D2s_v4** (click "Change size" → search D2s_v4) |
| Scale method | Manual |
| Node count | **2** |
| OS disk type | Managed disk |
| OS disk size | 30 GiB |
| Max pods per node | 110 |

Click **Update**.

### Step 4: Networking Tab

| Field | Value |
|---|---|
| Network configuration | Kubenet |
| DNS name prefix | `idcube` |
| Network policy | None |
| Load balancer | Standard |

### Step 5: Integrations Tab

Skip ACR attachment for now (we will use CLI after creation).

Leave Container monitoring: **Disabled** (to minimize cost in dev).

### Step 6: Review + Create

Click **Review + create** → review the summary → click **Create**.

Deployment takes **5–10 minutes**. You will see a deployment progress screen.

### Step 7: Connect After Portal Creation

Once provisioned, click **Go to resource** → click **Connect** button at the top → copy the `az aks get-credentials` command shown.

```bash
az aks get-credentials \
  --resource-group idcube-aks \
  --name idcube-cluster
kubectl get nodes
```

---

## Option C — Terraform

The Terraform configuration lives at [session-9/terraform-aks/](terraform-aks/).

### Files

**[terraform-aks/main.tf](terraform-aks/main.tf)**

```hcl
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
    vm_size    = "Standard_D2s_v4"

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

  tags = {
    environment = "dev"
    project     = "idcube"
  }
}
```

**[terraform-aks/outputs.tf](terraform-aks/outputs.tf)**

```hcl
output "cluster_name" {
  value = azurerm_kubernetes_cluster.aks.name
}

output "resource_group" {
  value = azurerm_resource_group.aks_rg.name
}

output "connect_command" {
  value = "az aks get-credentials --resource-group idcube-aks --name idcube-cluster"
}
```

### Run Terraform

```bash
cd session-9/terraform-aks

# Initialize providers
terraform init

# Preview what will be created (dry run)
terraform plan

# Create the cluster
terraform apply
# Type "yes" when prompted

# After apply succeeds, get the connect command from outputs
terraform output connect_command
# az aks get-credentials --resource-group idcube-aks --name idcube-cluster

# Connect
az aks get-credentials --resource-group idcube-aks --name idcube-cluster
kubectl get nodes
```

### Tear Down (Save Cost)

```bash
terraform destroy
# Type "yes" — deletes the cluster AND resource group
```

---

## Verify Cluster Before Proceeding

Run these checks before starting the labs:

```bash
# Both nodes in Ready state
kubectl get nodes -o wide

# System pods running (kube-system namespace)
kubectl get pods -n kube-system

# Cluster info
kubectl cluster-info

# Node capacity
kubectl describe node | grep -A5 "Capacity:"
# Each Standard_D2s_v4 node: 2 CPU, 8Gi RAM
```

---

## Context: Where We Are

Our three microservices are running in AKS, currently pulling images from DockerHub (`ram1uj/`):

| Service | Language | Current Image |
|---|---|---|
| `part-order-service` | Java (Spring Boot) | `ram1uj/part-order-service:latest` |
| `part-inventory-service` | .NET (ASP.NET Core) | `ram1uj/part-inventory-service` |
| `part-inventory-service-node` | Node.js | `ram1uj/part-inventory-service-node` |

**Problem with public DockerHub images:**
- No access control — anyone can pull
- DockerHub rate limits can break deployments
- No audit trail of who deployed what
- External dependency violates many enterprise security policies

**Goal:** Move all images to a private Azure Container Registry (ACR) and configure AKS to pull securely.

---

## CONCEPTS — Azure Container Registry

---

## What is ACR?

Azure Container Registry (ACR) is a managed, private Docker registry hosted in Azure. It is the equivalent of DockerHub but private and integrated with the Azure ecosystem.

```
DockerHub (public)          Azure Container Registry (private)
docker.io/ram1uj/img  →    myacr.azurecr.io/img
```

**Key features:**
- Private by default — no unauthenticated pulls
- Geo-replication support
- Vulnerability scanning (Microsoft Defender for Containers)
- Webhook triggers for CI/CD pipelines
- ACR Tasks for cloud-based image builds

---

## ACR SKUs

| SKU | Storage | Features | Use Case |
|---|---|---|---|
| Basic | 10 GB | Core features | Dev/test |
| Standard | 100 GB | Webhooks, geo-rep | Production |
| Premium | 500 GB | Private Link, zone redundancy | Enterprise |

For this session we use **Basic** (sufficient for learning).

---

## ACR Authentication Methods

```
Method              When to Use
─────────────────────────────────────────────────────
Admin credentials   Quick local testing only (insecure, disabled by default)
Service Principal   CI/CD pipelines, external systems
Managed Identity    AKS (recommended — no secrets to manage)
Azure AD tokens     Interactive developer access (az acr login)
```

---

## LAB 1 — Create ACR and Push Images

---

## Step 1: Create the Resource Group and ACR

```bash
# Variables — set these once and reuse
ACR_NAME="partserviceacr"       # must be globally unique, alphanumeric only
RG="rg-aks-training"
LOCATION="eastus"

# Create ACR
az acr create \
  --name $ACR_NAME \
  --resource-group $RG \
  --sku Basic \
  --admin-enabled false
```

Verify it was created:

```bash
az acr show --name $ACR_NAME --query loginServer --output tsv
# Expected output: partserviceacr.azurecr.io
```

---

## Step 2: Authenticate to ACR

```bash
az acr login --name $ACR_NAME
# Login Succeeded
```

This stores a short-lived token in your Docker credential store. It uses your Azure AD identity — no password needed.

---

## Step 3: Build and Push All Three Services

### 3a. Java Order Service

```bash
cd apps/part-order-service-java

# Build image locally
docker build -t part-order-service:v1 .

# Tag for ACR
docker tag part-order-service:v1 $ACR_NAME.azurecr.io/part-order-service:v1

# Push to ACR
docker push $ACR_NAME.azurecr.io/part-order-service:v1
```

### 3b. .NET Inventory Service

```bash
cd apps/part-inventory-service-dotnet

# Build from the directory containing Dockerfile
docker build -t part-inventory-service:v1 .

docker tag part-inventory-service:v1 $ACR_NAME.azurecr.io/part-inventory-service:v1
docker push $ACR_NAME.azurecr.io/part-inventory-service:v1
```

### 3c. Node.js Inventory Service

```bash
cd apps/part-inventory-service-node

docker build -t part-inventory-service-node:v1 .

docker tag part-inventory-service-node:v1 $ACR_NAME.azurecr.io/part-inventory-service-node:v1
docker push $ACR_NAME.azurecr.io/part-inventory-service-node:v1
```

---

## Step 4: Verify Images in ACR

```bash
# List all repositories
az acr repository list --name $ACR_NAME --output table

# List tags for a specific repo
az acr repository show-tags \
  --name $ACR_NAME \
  --repository part-order-service \
  --output table
```

Expected output:
```
Name
─────────────
v1
```

---

## LAB 2 — Update Deployments to Use ACR Images

---

## Update kubernetes-yamls/part-order-deployment.yaml

Change the image field from DockerHub to ACR:

```yaml
# Before
image: ram1uj/part-order-service:latest

# After
image: partserviceacr.azurecr.io/part-order-service:v1
```

Full updated container spec:

```yaml
containers:
  - name: part-order-service
    image: partserviceacr.azurecr.io/part-order-service:v1
    imagePullPolicy: Always
```

## Update kubernetes-yamls/part-inventory-deployment.yaml

```yaml
containers:
  - name: part-inventory-service
    image: partserviceacr.azurecr.io/part-inventory-service:v1
    imagePullPolicy: Always
```

## Apply Updated Manifests

```bash
kubectl apply -f kubernetes-yamls/part-order-deployment.yaml
kubectl apply -f kubernetes-yamls/part-inventory-deployment.yaml

# Watch pod status
kubectl get pods -w
```

At this point pods will enter `ImagePullBackOff` — this is expected! AKS cannot pull from a private ACR without authentication. We will fix this in Lab 3 and Lab 4.

---

## LAB 3 — Observe and Debug ImagePullBackOff (401 Unauthorized)

---

## What You Will See

```bash
kubectl get pods
```

Output:
```
NAME                                    READY   STATUS             RESTARTS   AGE
part-order-service-6d8f9b7d4-xk9p2     0/1     ImagePullBackOff   0          2m
part-inventory-service-7c5d8b6f9-mn3r  0/1     ImagePullBackOff   0          2m
```

---

## Debug Step 1: Describe the Pod

```bash
kubectl describe pod part-order-service-6d8f9b7d4-xk9p2
```

Look at the **Events** section at the bottom:

```
Events:
  Type     Reason     Age                From               Message
  ----     ------     ----               ----               -------
  Normal   Scheduled  3m                 default-scheduler  Successfully assigned default/...
  Normal   Pulling    2m (x3 over 3m)    kubelet            Pulling image "partserviceacr.azurecr.io/part-order-service:v1"
  Warning  Failed     2m (x3 over 3m)    kubelet            Failed to pull image "partserviceacr.azurecr.io/part-order-service:v1":
                                                            rpc error: code = Unknown desc = failed to pull and unpack image:
                                                            ... 401 Unauthorized
  Warning  BackOff    90s (x4 over 2m)   kubelet            Back-off pulling image "partserviceacr.azurecr.io/part-order-service:v1"
```

**Key signals:**
- `Failed to pull image` → authentication or image-not-found issue
- `401 Unauthorized` → credentials missing or incorrect
- `ImagePullBackOff` → Kubernetes is backing off retries (exponential backoff: 10s → 20s → 40s...)

---

## Debug Step 2: Check Node Pull Access

If you have access to a node (or via kubectl exec on a running pod), you can test:

```bash
# From a pod with curl available
kubectl run debug --image=curlimages/curl --rm -it -- sh
curl -I https://partserviceacr.azurecr.io/v2/
# 401 confirms registry exists but no auth
```

---

## Root Cause

AKS nodes have no credentials to pull from the private ACR. Two fix paths:

1. **Service Principal** → create a Kubernetes `imagePullSecret` (works anywhere)
2. **Managed Identity** → attach ACR to AKS cluster (recommended for Azure)

---

## LAB 4 — Fix Using Service Principal (imagePullSecret)

This approach works in any Kubernetes cluster, not just AKS.

---

## Step 1: Get ACR Resource ID

```bash
ACR_ID=$(az acr show --name $ACR_NAME --query id --output tsv)
echo $ACR_ID
# /subscriptions/<sub-id>/resourceGroups/rg-aks-training/providers/Microsoft.ContainerRegistry/registries/partserviceacr
```

---

## Step 2: Create a Service Principal with AcrPull Role

```bash
# Create SPN and capture credentials
SP_PASSWD=$(az ad sp create-for-rbac \
  --name "acr-pull-sp" \
  --role acrpull \
  --scopes $ACR_ID \
  --query password \
  --output tsv)

SP_APPID=$(az ad sp list \
  --display-name "acr-pull-sp" \
  --query "[0].appId" \
  --output tsv)

echo "App ID: $SP_APPID"
echo "Password: $SP_PASSWD"
```

**What this does:**
- Creates a Service Principal (an Azure AD application identity)
- Grants it the built-in `AcrPull` role scoped only to this ACR
- `AcrPull` allows pull but not push — principle of least privilege

---

## Step 3: Create Kubernetes imagePullSecret

```bash
kubectl create secret docker-registry acr-pull-secret \
  --docker-server=$ACR_NAME.azurecr.io \
  --docker-username=$SP_APPID \
  --docker-password=$SP_PASSWD \
  --docker-email=admin@example.com
```

Verify the secret was created:

```bash
kubectl get secret acr-pull-secret
kubectl describe secret acr-pull-secret
```

The secret stores a base64-encoded Docker config JSON with the credentials.

---

## Step 4: Reference the Secret in Deployments

Update `kubernetes-yamls/part-order-deployment.yaml`:

```yaml
spec:
  template:
    spec:
      imagePullSecrets:
        - name: acr-pull-secret
      containers:
        - name: part-order-service
          image: partserviceacr.azurecr.io/part-order-service:v1
```

Do the same for `part-inventory-deployment.yaml`.

---

## Step 5: Apply and Verify

```bash
kubectl apply -f kubernetes-yamls/part-order-deployment.yaml
kubectl apply -f kubernetes-yamls/part-inventory-deployment.yaml

kubectl get pods -w
```

Expected output after ~30-60 seconds:
```
NAME                                    READY   STATUS    RESTARTS   AGE
part-order-service-7b9c4d8f5-lp7qx     1/1     Running   0          45s
part-inventory-service-6f8d5c9b4-rk2m  1/1     Running   0          43s
```

---

## Downside of This Approach

- The Service Principal password **expires** (default: 1 year)
- You must **rotate the secret** before expiry and re-create the Kubernetes secret
- The password is stored in Kubernetes etcd as a base64 string (not truly encrypted unless etcd encryption is configured)
- Management overhead for multiple namespaces

---

## LAB 5 — Fix Using Managed Identity (AKS-Recommended)

Managed Identity eliminates the need for any secret. AKS can authenticate to ACR using its own Azure identity.

---

## Step 1: Attach ACR to AKS

```bash
AKS_NAME="aks-training"

az aks update \
  --name $AKS_NAME \
  --resource-group $RG \
  --attach-acr $ACR_NAME
```

**What this does behind the scenes:**
- Finds the Managed Identity (or Service Principal) used by the AKS node pool
- Grants it the `AcrPull` role on the ACR
- No secrets are created — authentication is done via Azure's token service at pull time

---

## Step 2: Remove imagePullSecrets from Deployments

```yaml
# Remove this block entirely from the deployment spec:
# imagePullSecrets:
#   - name: acr-pull-secret
```

Updated deployment:

```yaml
spec:
  template:
    spec:
      containers:
        - name: part-order-service
          image: partserviceacr.azurecr.io/part-order-service:v1
          imagePullPolicy: Always
```

---

## Step 3: Apply and Verify

```bash
kubectl apply -f kubernetes-yamls/part-order-deployment.yaml
kubectl apply -f kubernetes-yamls/part-inventory-deployment.yaml

kubectl get pods -w
# Pods should reach Running state without any imagePullSecrets
```

---

## Verify the Role Assignment

```bash
az role assignment list \
  --scope $ACR_ID \
  --query "[?roleDefinitionName=='AcrPull']" \
  --output table
```

---

## SPN vs Managed Identity — When to Use Which

| Scenario | Approach |
|---|---|
| AKS pulling from ACR in the same Azure subscription | Managed Identity (`--attach-acr`) |
| AKS in a different subscription than ACR | Service Principal with imagePullSecret |
| Non-Azure Kubernetes (EKS, GKE, on-prem) | Service Principal with imagePullSecret |
| CI/CD pipeline pushing images | Service Principal with `AcrPush` role |

---

## LAB 6 — Image Tagging Strategy

---

## Why Tagging Matters

Using `latest` as an image tag is an anti-pattern in production:
- You cannot tell which code version is running
- `latest` can silently change between pulls
- Rolling back requires knowing the previous `latest` (you don't)

---

## Recommended Tagging Strategy

Use **multiple tags** on the same image digest:

```
partserviceacr.azurecr.io/part-inventory-service:v1.2.3          ← semantic version
partserviceacr.azurecr.io/part-inventory-service:v1.2            ← minor version alias
partserviceacr.azurecr.io/part-inventory-service:build-0042      ← CI build number
partserviceacr.azurecr.io/part-inventory-service:git-a3f9bc1     ← git short SHA
partserviceacr.azurecr.io/part-inventory-service:stable          ← environment alias
```

---

## Apply Multiple Tags

```bash
# Build once
docker build -t part-order-service:local .

# Tag with multiple identifiers (all point to the same image digest)
docker tag part-inventory-service:local $ACR_NAME.azurecr.io/part-order-service:v1.2.3
docker tag part-inventory-service:local $ACR_NAME.azurecr.io/part-order-service:v1.2
docker tag part-inventory-service:local $ACR_NAME.azurecr.io/part-order-service:build-$(date +%Y%m%d%H%M)
docker tag part-inventory-service:local $ACR_NAME.azurecr.io/part-order-service:git-$(git rev-parse --short HEAD)

# Push all tags
docker push $ACR_NAME.azurecr.io/part-order-service --all-tags
```

---

## Simulate a Rollback Using Tags

```bash
# Current deployment is on v1.2.3
kubectl set image deployment/part-order-service \
  part-order-service=$ACR_NAME.azurecr.io/part-order-service:v1.2.3

# Something is wrong — roll back to v1.1.0
kubectl set image deployment/part-order-service \
  part-order-service=$ACR_NAME.azurecr.io/part-order-service:v1.1.0

# Verify rollout
kubectl rollout status deployment/part-order-service
```

This is instant because the image is already in ACR. No rebuild needed.

---

## LAB 7 — Break Tag Scenario (Image Not Found)

---

## Inject the Issue

Update `part-order-deployment.yaml` to reference a non-existent tag:

```yaml
image: partserviceacr.azurecr.io/part-order-service:v99
```

Apply:

```bash
kubectl apply -f kubernetes-yamls/part-order-deployment.yaml
kubectl get pods -w
```

---

## Observe

```
NAME                                  READY   STATUS             RESTARTS
part-order-service-5f7d9b8c6-nq4rs   0/1     ErrImagePull       0
part-order-service-5f7d9b8c6-nq4rs   0/1     ImagePullBackOff   0
```

---

## Debug

```bash
kubectl describe pod part-order-service-5f7d9b8c6-nq4rs
```

Events section:
```
Warning  Failed   10s   kubelet   Failed to pull image "partserviceacr.azurecr.io/part-order-service:v99":
                                  rpc error: code = NotFound
                                  manifest for partserviceacr.azurecr.io/part-order-service:v99 not found:
                                  manifest unknown
```

**Key difference:**
- `401 Unauthorized` → authentication problem
- `manifest unknown` / `not found` → tag does not exist in the registry

---

## Verify Available Tags

```bash
az acr repository show-tags \
  --name $ACR_NAME \
  --repository part-order-service \
  --output table
```

Confirm `v99` is not there. Pick the correct tag and fix the deployment:

```bash
kubectl set image deployment/part-order-service \
  part-order-service=$ACR_NAME.azurecr.io/part-order-service:v1

kubectl rollout status deployment/part-order-service
```

---

## ImagePullBackOff vs ErrImagePull

| Status | Meaning |
|---|---|
| `ErrImagePull` | Active attempt to pull just failed |
| `ImagePullBackOff` | Kubernetes is waiting before retrying (exponential backoff) |

Both indicate a pull failure. `ImagePullBackOff` just means Kubernetes has already retried multiple times. Describe the pod to see the actual error in Events.

---

## LAB 8 — ACR Import (Mirror External Images)

---

## Why Import External Images?

In many enterprise environments:
- Egress traffic to public registries is blocked or audited
- `docker.io` rate limits can fail CI/CD pipelines
- Security team requires all images to be scanned before use

The solution: **import** external images into ACR so all pulls stay within Azure.

---

## Import nginx from DockerHub

```bash
az acr import \
  --name $ACR_NAME \
  --source docker.io/library/nginx:1.27-alpine \
  --image nginx:1.27-alpine
```

---

## Import Our Base Images

Our Dockerfiles use these base images — import them to reduce external dependency:

```bash
# .NET runtime (used by part-inventory-service-dotnet)
az acr import \
  --name $ACR_NAME \
  --source mcr.microsoft.com/dotnet/aspnet:10.0-alpine \
  --image dotnet/aspnet:10.0-alpine

# Node.js runtime (used by part-inventory-service-node)
az acr import \
  --name $ACR_NAME \
  --source docker.io/library/node:20-bookworm-slim \
  --image node:20-bookworm-slim
```

---

## Use Imported Image in a Deployment

```yaml
containers:
  - name: nginx-sidecar
    image: partserviceacr.azurecr.io/nginx:1.27-alpine
```

Now the pull never leaves your Azure network.

---

## LAB 9 — Retention and Cleanup Strategy

---

## Problem: Registry Storage Grows Without Bound

Every CI build pushes a new tag. After months of deployments:

```bash
az acr repository show-tags \
  --name $ACR_NAME \
  --repository part-order-service \
  --orderby time_desc \
  --output table
```

You might have hundreds of tags for `build-XXXXXXXX` that are no longer deployed anywhere.

---

## Identify Old / Untagged Images

```bash
# List manifests with last update time
az acr repository show-manifests \
  --name $ACR_NAME \
  --repository part-order-service \
  --orderby time_asc \
  --output table
```

---

## Manual Cleanup

```bash
# Delete a specific tag
az acr repository delete \
  --name $ACR_NAME \
  --image part-order-service:build-20240101 \
  --yes

# Delete an entire repository
az acr repository delete \
  --name $ACR_NAME \
  --repository part-order-service-old \
  --yes
```

---

## Automated Cleanup with ACR Retention Policy (Premium SKU)

For Premium ACR, you can set a retention policy to automatically purge untagged manifests:

```bash
# Enable retention — delete untagged manifests after 7 days
az acr config retention update \
  --name $ACR_NAME \
  --type UntaggedManifests \
  --days 7 \
  --status enabled
```

---

## Cleanup Script (Basic/Standard SKU)

```bash
# Purge images older than 30 days that are not tagged with a semantic version pattern
az acr run \
  --registry $ACR_NAME \
  --cmd "acr purge \
    --filter 'part-order-service:build-.*' \
    --ago 30d \
    --untagged" \
  /dev/null
```

---

## LAB 10 — Base Image Governance

---

## Scenario

Your security team has issued a policy:
> "No container may pull base images from public registries. All base images must be sourced from `partserviceacr.azurecr.io` and be scanned first."

---

## Step 1: Import All Required Base Images

```bash
# Java build image
az acr import \
  --name $ACR_NAME \
  --source docker.io/library/maven:3.9.11-eclipse-temurin-25 \
  --image maven:3.9.11-eclipse-temurin-25

# Java runtime (distroless)
az acr import \
  --name $ACR_NAME \
  --source gcr.io/distroless/java25:nonroot \
  --image distroless/java25:nonroot

# .NET SDK build image
az acr import \
  --name $ACR_NAME \
  --source mcr.microsoft.com/dotnet/sdk:10.0 \
  --image dotnet/sdk:10.0

# .NET runtime
az acr import \
  --name $ACR_NAME \
  --source mcr.microsoft.com/dotnet/aspnet:10.0-alpine \
  --image dotnet/aspnet:10.0-alpine

# Node.js
az acr import \
  --name $ACR_NAME \
  --source docker.io/library/node:20-bookworm-slim \
  --image node:20-bookworm-slim
```

---

## Step 2: Update Dockerfiles to Reference ACR

### Java Order Service — Dockerfile

```dockerfile
# FROM maven:3.9.11-eclipse-temurin-25 AS build  ← original
FROM partserviceacr.azurecr.io/maven:3.9.11-eclipse-temurin-25 AS build
WORKDIR /app

COPY pom.xml .
RUN --mount=type=cache,target=/root/.m2 \
    mvn dependency:go-offline -B

COPY src ./src
RUN --mount=type=cache,target=/root/.m2 \
    mvn clean package -DskipTests

# FROM gcr.io/distroless/java25  ← original
FROM partserviceacr.azurecr.io/distroless/java25:nonroot
WORKDIR /app
COPY --from=build /app/target/*.jar app.jar
USER nonroot
ENTRYPOINT ["java", "-jar", "app.jar"]
```

### .NET Inventory Service — Dockerfile

```dockerfile
# FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build  ← original
FROM partserviceacr.azurecr.io/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY PartInventoryService.DotNet/PartInventoryService.DotNet.csproj PartInventoryService.DotNet/
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore PartInventoryService.DotNet/PartInventoryService.DotNet.csproj

COPY PartInventoryService.DotNet/. PartInventoryService.DotNet/
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish PartInventoryService.DotNet/PartInventoryService.DotNet.csproj \
    -c Release -o /app/publish --no-self-contained /p:UseAppHost=false

# FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime  ← original
FROM partserviceacr.azurecr.io/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
RUN addgroup -S appgroup && adduser -S appuser -G appgroup
ENV ASPNETCORE_URLS=http://+:8080
ENV PORT=8080
COPY --from=build /app/publish ./
USER appuser
EXPOSE 8080
ENTRYPOINT ["dotnet", "PartInventoryService.DotNet.dll"]
```

---

## Step 3: Rebuild and Push (Now Fully Internal)

```bash
# Build with ACR Tasks (builds happen inside Azure, no external internet needed on your machine)
az acr build \
  --registry $ACR_NAME \
  --image part-order-service:v2 \
  --file apps/part-order-service-java/Dockerfile \
  apps/part-order-service-java/
```

`az acr build` runs the Docker build inside ACR itself — no local Docker daemon needed, and the build environment already has access to your ACR base images.

---

## LAB 11 — Combined Failure Scenario

---

## Scenario

A colleague deployed a new version of both services. The cluster is now degraded:

```
NAME                                    READY   STATUS             RESTARTS
part-order-service-7f9c4d8b5-xk2pm     0/1     ImagePullBackOff   0
part-inventory-service-5d8b7c6f4-rn9qs 0/1     CrashLoopBackOff   1
```

One pod has a pull error. The other started but is crashing. You need to investigate and fix both.

---

## Investigation

```bash
# Check order service pod — image pull issue
kubectl describe pod part-order-service-7f9c4d8b5-xk2pm
```

Events show:
```
Failed to pull image "partserviceacr.azurecr.io/part-order-service:v3":
manifest unknown: manifest tagged by "v3" is not found
```

→ Tag `v3` was never pushed.

```bash
# Check what tags exist
az acr repository show-tags \
  --name $ACR_NAME \
  --repository part-order-service \
  --output table

# Fix: roll back to last known good tag
kubectl set image deployment/part-order-service \
  part-order-service=$ACR_NAME.azurecr.io/part-order-service:v1
```

```bash
# Check inventory service pod — runtime crash
kubectl logs part-inventory-service-5d8b7c6f4-rn9qs
```

Logs reveal a missing environment variable causing startup failure. Fix:

```bash
kubectl describe deployment part-inventory-service
# Identify missing env var

# Patch the deployment
kubectl set env deployment/part-inventory-service SPRING_PROFILES_ACTIVE=dev
```

---

## Fix Checklist

When a pod fails, always check in this order:

1. `kubectl get pods` → identify the pod name and status
2. `kubectl describe pod <name>` → read Events section at the bottom
3. `kubectl logs <name>` → read application logs (if the container started)
4. `kubectl logs <name> --previous` → logs from the previous (crashed) container
5. Fix the root cause (wrong tag, missing auth, bad config)
6. `kubectl rollout status deployment/<name>` → confirm recovery

---

## Real Case Scenario

---

## Situation

Production alert fires at 2 AM: `part-order-service` is unavailable. On-call engineer opens the cluster.

```bash
kubectl get pods -n production
```

```
NAME                                  READY   STATUS             RESTARTS
part-order-service-6b7d9c8f5-pq3rs   0/1     ImagePullBackOff   0
part-order-service-6b7d9c8f5-mn4xt   0/1     ImagePullBackOff   0
```

Both replicas failing. Old pods were terminated during the rolling update.

---

## Investigation

```bash
kubectl describe pod part-order-service-6b7d9c8f5-pq3rs -n production
```

Events:
```
Warning  Failed  2m (x5)  kubelet  Failed to pull image: 401 Unauthorized
```

This is an auth error, not a missing tag.

---

## Root Causes Found

1. **The CI pipeline rotated the Service Principal password** but the Kubernetes `acr-pull-secret` was never updated. The old credentials are now invalid.

2. **Managed Identity was not used** for the production cluster — it was set up with SPN-based imagePullSecrets.

---

## Resolution

```bash
# Get new SPN credentials from the pipeline team or regenerate
az ad sp credential reset --name "acr-pull-sp" --query password -o tsv

# Re-create the secret with fresh credentials
kubectl delete secret acr-pull-secret -n production
kubectl create secret docker-registry acr-pull-secret \
  --docker-server=$ACR_NAME.azurecr.io \
  --docker-username=$SP_APPID \
  --docker-password=$NEW_PASSWD \
  -n production

# Force pod replacement to pick up the new secret
kubectl rollout restart deployment/part-order-service -n production
kubectl rollout status deployment/part-order-service -n production
```

---

## Long-Term Fix

Migrate production to Managed Identity to eliminate this class of failure permanently:

```bash
az aks update \
  --name $AKS_NAME \
  --resource-group $RG \
  --attach-acr $ACR_NAME

# Remove imagePullSecrets from production deployments
kubectl patch deployment part-order-service -n production \
  --type=json \
  -p='[{"op":"remove","path":"/spec/template/spec/imagePullSecrets"}]'
```

---

## Validation Checklist

After completing all labs, verify:

```bash
# All pods running
kubectl get pods

# Confirm images are from ACR (not DockerHub)
kubectl get pods -o jsonpath='{range .items[*]}{.spec.containers[*].image}{"\n"}{end}'
# Should show: partserviceacr.azurecr.io/...

# Confirm no imagePullSecrets needed (Managed Identity path)
kubectl get deployment part-order-service -o jsonpath='{.spec.template.spec.imagePullSecrets}'
# Should return empty (no secrets needed)

# Verify ACR repositories and tags
az acr repository list --name $ACR_NAME --output table
az acr repository show-tags --name $ACR_NAME --repository part-order-service --output table
az acr repository show-tags --name $ACR_NAME --repository part-inventory-service --output table
```

---

## Summary: ImagePullBackOff Diagnosis Guide

```
Pod Status: ImagePullBackOff or ErrImagePull
│
├─ kubectl describe pod → Events section
│
├─ "401 Unauthorized"
│   ├─ No imagePullSecrets configured?     → Add imagePullSecrets
│   ├─ imagePullSecret credentials expired? → Rotate SPN, recreate secret
│   └─ Managed Identity not attached?      → az aks update --attach-acr
│
├─ "manifest unknown" / "not found"
│   ├─ Typo in tag?                        → az acr repository show-tags
│   └─ Image never pushed to ACR?          → docker build & push, or az acr build
│
└─ "i/o timeout" / "connection refused"
    ├─ Network policy blocking egress?     → Check NetworkPolicy objects
    └─ ACR firewall rules?                 → Check ACR network settings
```

---

## Final Architecture After Session 9

```
                  ┌─────────────────────────────────┐
                  │  Azure Container Registry (ACR)  │
                  │  partserviceacr.azurecr.io        │
                  │                                   │
                  │  ├── part-order-service:v1        │
                  │  ├── part-inventory-service:v1    │
                  │  ├── part-inventory-node:v1       │
                  │  ├── nginx:1.27-alpine (mirrored) │
                  │  └── dotnet/aspnet:10.0 (mirrored)│
                  └────────────────┬────────────────┘
                                   │ Managed Identity (no secrets)
                  ┌────────────────▼────────────────┐
                  │       AKS Cluster               │
                  │                                  │
                  │  ┌────────────────────────┐     │
                  │  │  part-order-service    │     │
                  │  │  (Java / Spring Boot)  │     │
                  │  └────────────┬───────────┘     │
                  │               │ HTTP             │
                  │  ┌────────────▼───────────┐     │
                  │  │  part-inventory-service│     │
                  │  │  (.NET / ASP.NET Core) │     │
                  │  └────────────────────────┘     │
                  └─────────────────────────────────┘
```

**Key improvements made this session:**
- Images moved from public DockerHub to private ACR
- Authentication via Managed Identity (zero secrets to manage)
- Immutable versioned tags replace `latest`
- Base images mirrored into ACR (no external registry dependency)
- Retention policy prevents unbounded storage growth
