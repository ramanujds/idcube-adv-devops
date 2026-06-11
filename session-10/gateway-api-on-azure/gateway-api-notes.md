# Ingress vs Gateway API — and Implementation on Azure

---

## Critical Context: Ingress NGINX Retirement

> The Kubernetes SIG Network announced the **retirement of Ingress NGINX** with maintenance ending **March 2026**.
> Microsoft provides support for the AKS application routing add-on (NGINX) through **November 2026**.
> **Gateway API is the official successor.** Start planning migration now.

---

## Part 1 — Ingress vs Gateway API

---

### What is Wrong with Ingress?

The Kubernetes Ingress resource was designed in 2015 for simple HTTP routing. It has not kept up with modern requirements.

**Core problems:**

```
┌─────────────────────────────────────────────────────────────────┐
│ Ingress Resource                                                  │
│                                                                   │
│  Only HTTP/HTTPS supported                                        │
│  No TCP, UDP, gRPC without vendor annotations                    │
│  Advanced features (auth, rate limiting, retries) need           │
│    non-standard annotations — every controller uses              │
│    different annotation names                                     │
│  One person (cluster admin) owns everything — no RBAC split      │
│  Not extensible — no clean way to add new capabilities           │
└─────────────────────────────────────────────────────────────────┘
```

**Annotation fragmentation example:**

The same "rewrite path" feature looks completely different across controllers:

```yaml
# NGINX Ingress
nginx.ingress.kubernetes.io/rewrite-target: /

# Traefik
traefik.ingress.kubernetes.io/rule-type: PathPrefix

# HAProxy
ingress.kubernetes.io/rewrite-target: /
```

These are not portable. Switching controllers means rewriting all your Ingress annotations.

---

### What is Gateway API?

Gateway API is the **official Kubernetes replacement for Ingress**, built by the SIG Network team. It is a set of CRDs (Custom Resource Definitions) that provide a standardized, role-oriented, and extensible framework for traffic management.

**Key design principles:**
- **Role-oriented**: different people own different resources
- **Portable**: same YAML works across any Gateway API-compliant controller
- **Expressive**: supports TCP, UDP, gRPC, TLS passthrough out of the box — no annotations needed
- **Extensible**: designed for vendor-specific extensions without polluting core spec

---

### Resource Model Comparison

```
Ingress API               Gateway API
────────────────────────────────────────────────────────────
IngressClass          →   GatewayClass   (who manages it — infra provider)
Ingress               →   Gateway        (the actual entry point — cluster operator)
                          HTTPRoute      (routing rules — app developer)
                          TCPRoute
                          GRPCRoute
                          TLSRoute
```

**Who owns what:**

```
Infrastructure Provider (Azure, NGINX, Istio)
  └── defines GatewayClass

Cluster Operator (platform team)
  └── creates Gateway (listeners, TLS, addresses)

Application Developer
  └── creates HTTPRoute (path rules, backends) in their own namespace
```

With Ingress, one person had to manage everything in a single object. With Gateway API, responsibilities are cleanly split.

---

### Side-by-Side: Same Routing Rule

**Ingress (old):**

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: part-order-ingress
  annotations:
    kubernetes.io/ingress.class: "nginx"
spec:
  rules:
    - http:
        paths:
          - path: /orders
            pathType: Prefix
            backend:
              service:
                name: part-order-service
                port:
                  number: 80
          - path: /inventory
            pathType: Prefix
            backend:
              service:
                name: part-inventory-service
                port:
                  number: 80
```

**Gateway API (new) — same result, two resources:**

```yaml
# Gateway: cluster operator creates this once
apiVersion: gateway.networking.k8s.io/v1
kind: Gateway
metadata:
  name: idcube-gateway
spec:
  gatewayClassName: approuting-istio   # or azure-alb-external
  listeners:
    - name: http
      port: 80
      protocol: HTTP
      allowedRoutes:
        namespaces:
          from: Same
---
# HTTPRoute: app team creates this per service
apiVersion: gateway.networking.k8s.io/v1
kind: HTTPRoute
metadata:
  name: part-services-route
spec:
  parentRefs:
    - name: idcube-gateway
  rules:
    - matches:
        - path:
            type: PathPrefix
            value: /orders
      backendRefs:
        - name: part-order-service
          port: 80
    - matches:
        - path:
            type: PathPrefix
            value: /inventory
      backendRefs:
        - name: part-inventory-service
          port: 80
```

---

### Feature Comparison Table

| Feature | Ingress | Gateway API |
|---|---|---|
| HTTP routing | Yes | Yes |
| HTTPS / TLS termination | Yes (via secret) | Yes (native, more expressive) |
| TCP/UDP routing | No — annotation hacks | Yes — TCPRoute, UDPRoute |
| gRPC routing | No | Yes — GRPCRoute |
| TLS passthrough | No | Yes — TLSRoute |
| Traffic splitting / canary | Annotation only (NGINX) | Native — weight per backendRef |
| Header-based routing | Annotation only | Native |
| Multi-namespace | Single namespace | Cross-namespace (with ReferenceGrant) |
| Role separation | None | GatewayClass / Gateway / Route |
| Portability | Low — annotations differ per controller | High — spec is standardized |
| Status | Stable (frozen, retiring) | GA since Kubernetes 1.28 |

---

### Traffic Splitting Example (Gateway API native — no annotations)

Canary deployment: send 10% of traffic to `v2`, 90% to `v1`:

```yaml
apiVersion: gateway.networking.k8s.io/v1
kind: HTTPRoute
metadata:
  name: part-order-canary
spec:
  parentRefs:
    - name: idcube-gateway
  rules:
    - matches:
        - path:
            type: PathPrefix
            value: /orders
      backendRefs:
        - name: part-order-service-v1
          port: 80
          weight: 90
        - name: part-order-service-v2
          port: 80
          weight: 10
```

With NGINX Ingress this requires a `canary` annotation on a second Ingress object — non-standard and controller-specific.

---

## Part 2 — Azure Options for Gateway API

There are **two distinct Azure implementations** of Gateway API on AKS:

| | App Routing add-on (Istio) | Application Gateway for Containers (AGC) |
|---|---|---|
| Underlying tech | Istio control plane | Azure Application Gateway for Containers |
| Network plugin | Any (kubenet works) | **Azure CNI or Azure CNI Overlay only** |
| Location | Inside AKS cluster | **Outside cluster** (Azure managed resource) |
| Azure integration | Limited | Deep — WAF, DDoS, Azure Monitor |
| Setup complexity | Simple (one flag) | More involved |
| GatewayClass | `approuting-istio` | `azure-alb-external` |
| Best for | Standard ingress, migrating from NGINX | Enterprise, WAF, multi-cluster |

---

## Part 3 — Option A: App Routing add-on with Gateway API (Istio)

This is the **easiest path** for teams currently using NGINX Ingress on AKS. It works with the `idcube-cluster` (kubenet).

### How it Works

```
Client
  ↓
Azure Load Balancer  (Azure provisions this automatically)
  ↓
Istio Proxy Pod  (managed by app routing add-on, in default namespace)
  ↓
Kubernetes Service → Pod
```

AKS manages the Istio control plane. You create `Gateway` and `HTTPRoute` objects. Istio proxy pods are automatically created per Gateway.

---


### Install Gateway API CRDs
Gateway API is not built-in. Install official CRDs:
```bash
kubectl apply -f https://github.com/kubernetes-sigs/gateway-api/releases/latest/download/standard-install.yaml
Verify:

kubectl get crds | grep gateway

```

```text

You should see:

gateways.gateway.networking.k8s.io
httproutes.gateway.networking.k8s.io
gatewayclasses.gateway.networking.k8s.io
Good. API is ready.

```



### Step 1: Enable the Add-on

For the existing `idcube-cluster`:

```bash
# 1. Install or update the preview extension
az extension add --name aks-preview
az extension update --name aks-preview

# 2. Run the explicit sub-command to enable the Istio Gateway API
az aks approuting gateway istio enable \
  --resource-group idcube-aks \
  --name idcube-cluster

```

> **Note:** The Istio service mesh add-on and the app routing Gateway API add-on cannot be enabled at the same time. If you have the service mesh add-on, disable it first.

Verify Istio control plane is running:

```bash
kubectl get pods -n aks-istio-system
```

```
NAME                      READY   STATUS    RESTARTS   AGE
istiod-54b4ff45cf-htph8   1/1     Running   0          3m15s
istiod-54b4ff45cf-wlvgd   1/1     Running   0          3m
```

Check the GatewayClass was created:

```bash
kubectl get gatewayclass
```

```
NAME                CONTROLLER                                 ACCEPTED
approuting-istio    approuting.networking.azure.io/istio       True
```

---

### Step 2: Create a Gateway

The manifest is at [gateway.yaml](gateway.yaml):

```yaml
apiVersion: gateway.networking.k8s.io/v1
kind: Gateway
metadata:
  name: idcube-gateway
  namespace: default
spec:
  gatewayClassName: approuting-istio
  listeners:
    - name: http
      port: 80
      protocol: HTTP
      allowedRoutes:
        namespaces:
          from: Same
```

```bash
kubectl apply -f gateway.yaml

# Wait for gateway to be programmed (gets an external IP)
kubectl wait --for=condition=programmed gateways.gateway.networking.k8s.io idcube-gateway

# Get the external IP
GATEWAY_IP=$(kubectl get gateway idcube-gateway \
  -o jsonpath='{.status.addresses[0].value}')
echo $GATEWAY_IP
```

When you create a Gateway, the add-on automatically provisions:
- A **Deployment** of Istio proxy pods (2 replicas by default)
- A **LoadBalancer Service** with a public Azure IP
- An **HPA** (min 2, max 5, CPU 80%)
- A **PodDisruptionBudget** (min 1 available)

```bash
kubectl get deployment,svc,hpa,pdb | grep idcube-gateway
```

---

### Step 3: Create HTTPRoutes for the Services

The manifest is at [httproute-idcube-services.yaml](httproute-idcube-services.yaml):

```yaml
apiVersion: gateway.networking.k8s.io/v1
kind: HTTPRoute
metadata:
  name: part-services-route
  namespace: default
spec:
  parentRefs:
    - name: idcube-gateway
  rules:
    - matches:
        - path:
            type: PathPrefix
            value: /orders
      backendRefs:
        - name: part-order-service
          port: 80
    - matches:
        - path:
            type: PathPrefix
            value: /inventory
      backendRefs:
        - name: part-inventory-service
          port: 80
    - matches:
        - path:
            type: PathPrefix
            value: /parts
      backendRefs:
        - name: part-inventory-service
          port: 80
    - matches:
        - path:
            type: PathPrefix
            value: /
      backendRefs:
        - name: part-order-service
          port: 80
```

```bash
kubectl apply -f httproute-idcube-services.yaml

# Verify routes are accepted
kubectl get httproute part-services-route
```

```
NAME                   HOSTNAMES   AGE   ACCEPTED   PARENT
part-services-route                30s   True       idcube-gateway
```

---

### Step 4: Test the Gateway

```bash
GATEWAY_IP=$(kubectl get gateway idcube-gateway \
  -o jsonpath='{.status.addresses[0].value}')

curl http://$GATEWAY_IP/orders
curl http://$GATEWAY_IP/inventory
curl http://$GATEWAY_IP/parts
```

---

### Step 5: Add a Hostname

In production, add a `hostnames` field to scope the route to a specific domain:

```yaml
spec:
  parentRefs:
    - name: idcube-gateway
  hostnames:
    - api.idcube.com
  rules:
    ...
```

---

### Disable the Add-on (Cleanup)

```bash
az aks update \
  --resource-group idcube-aks \
  --name idcube-cluster \
  --disable-app-routing-istio

kubectl delete gateway idcube-gateway
kubectl delete httproute part-services-route
```

---

## Part 4 — Option B: Application Gateway for Containers (Enterprise)

Application Gateway for Containers (AGC) is a **fully managed Azure resource** outside the cluster. The ALB Controller inside AKS translates Gateway API/Ingress objects into AGC load balancing rules.

```
Client
  ↓
Application Gateway for Containers  (Azure-managed, outside AKS)
  ↓  (ALB Controller inside AKS reads Gateway API objects and configures AGC)
Kubernetes Service → Pod
```

### Differences from NGINX / App Routing

| | NGINX / App Routing | Application Gateway for Containers |
|---|---|---|
| Where it runs | Inside AKS (as pods) | Azure managed service (outside cluster) |
| Scaling | You manage (HPA) | Azure scales it automatically |
| WAF | Not native | Native (Azure WAF integration) |
| Connection draining | Limited | Full support |
| Backend health | Kubernetes probes | AGC-native health probes |
| mTLS | Via Istio | Native support |

---

### Prerequisite: Cluster Needs Azure CNI

> **Our `idcube-cluster` uses `kubenet`.** AGC requires **Azure CNI** or **Azure CNI Overlay**.

To use AGC you would need to create a new cluster with Azure CNI:

```bash
az aks create \
  --name idcube-cluster-agc \
  --resource-group idcube-aks \
  --location southindia \
  --node-count 2 \
  --node-vm-size Standard_D2s_v4 \
  --nodepool-name systempool \
  --network-plugin azure \
  --enable-oidc-issuer \
  --enable-workload-identity \
  --enable-gateway-api \
  --enable-application-load-balancer \
  --generate-ssh-keys \
  --tags environment=dev project=idcube \
  --dns-name-prefix idcube
```

Key differences from the Session 9 cluster command:
- `--network-plugin azure` instead of `kubenet`
- `--enable-oidc-issuer` — required for workload identity
- `--enable-workload-identity` — ALB controller authenticates to Azure via this
- `--enable-gateway-api` — installs the Gateway API CRDs (managed by AKS)
- `--enable-application-load-balancer` — installs the ALB Controller

---

### Step 1: Register Resource Providers (One-Time)

```bash
SUBSCRIPTION_ID=$(az account show --query id --output tsv)
az account set --subscription $SUBSCRIPTION_ID

az provider register --namespace Microsoft.ContainerService
az provider register --namespace Microsoft.Network
az provider register --namespace Microsoft.NetworkFunction
az provider register --namespace Microsoft.ServiceNetworking

# Install CLI extensions
az extension add --name alb
az extension add --name aks-preview
```

---

### Step 2: Enable on Existing Cluster (Azure CNI Required)

If the existing cluster already uses Azure CNI, first enable workload identity:

```bash
az aks update \
  --resource-group idcube-aks \
  --name idcube-cluster \
  --enable-oidc-issuer \
  --enable-workload-identity \
  --no-wait

# Then enable Gateway API and ALB Controller
az aks update \
  --name idcube-cluster \
  --resource-group idcube-aks \
  --enable-gateway-api \
  --enable-application-load-balancer
```

---

### Step 3: Verify ALB Controller

```bash
kubectl get pods -n kube-system | grep alb-controller
```

```
NAME                                 READY   STATUS    RESTARTS   AGE
alb-controller-6648c5d5c-sdd9t       1/1     Running   0          4d6h
alb-controller-6648c5d5c-au234       1/1     Running   0          4d6h
```

Two replicas for HA. Check the GatewayClass:

```bash
kubectl get gatewayclass azure-alb-external -o yaml
```

Look for:
```yaml
status:
  conditions:
    - message: Valid GatewayClass
      reason: Accepted
      status: "True"
      type: Accepted
```

---

### Step 4: Create ApplicationLoadBalancer Resource

This ties the Gateway to an Azure subnet:

```yaml
apiVersion: alb.networking.azure.io/v1
kind: ApplicationLoadBalancer
metadata:
  name: idcube-alb
  namespace: default
spec:
  associations:
    - /subscriptions/<sub-id>/resourceGroups/idcube-aks/providers/Microsoft.Network/virtualNetworks/<vnet>/subnets/<subnet>
```

---

### Step 5: Create Gateway Using azure-alb-external

```yaml
apiVersion: gateway.networking.k8s.io/v1
kind: Gateway
metadata:
  name: idcube-gateway-agc
  namespace: default
  annotations:
    alb.networking.azure.io/alb-name: idcube-alb
    alb.networking.azure.io/alb-namespace: default
spec:
  gatewayClassName: azure-alb-external
  listeners:
    - name: http
      port: 80
      protocol: HTTP
      allowedRoutes:
        namespaces:
          from: Same
```

---

### Step 6: Create HTTPRoute

Same HTTPRoute format as Option A — this is the power of Gateway API portability:

```yaml
apiVersion: gateway.networking.k8s.io/v1
kind: HTTPRoute
metadata:
  name: part-services-route
spec:
  parentRefs:
    - name: idcube-gateway-agc
  rules:
    - matches:
        - path:
            type: PathPrefix
            value: /orders
      backendRefs:
        - name: part-order-service
          port: 80
    - matches:
        - path:
            type: PathPrefix
            value: /inventory
      backendRefs:
        - name: part-inventory-service
          port: 80
```

> The HTTPRoute is identical regardless of whether you use `approuting-istio` or `azure-alb-external`. Only the `parentRefs.name` changes. This is Gateway API portability in practice.

---

### AGC Resources Created in Azure Portal

After setup, check the node resource group (`MC_idcube-aks_idcube-cluster_southindia`):

- **Managed Identity**: `applicationloadbalancer-idcube-cluster` with these roles:
  - Network Contributor (on MC resource group)
  - AppGw for Containers Configuration Manager (on MC resource group)
  - Reader (on MC resource group)
- **Subnet**: `aks-appgateway` with delegation for `Microsoft.ServiceNetworking/TrafficController`

---

## Part 5 — Migrating from Ingress to Gateway API

### The ingress2gateway Tool (GA: March 2026)

A CLI tool that converts Ingress manifests to Gateway API equivalents:

```bash
# Install
go install sigs.k8s.io/ingress2gateway@latest

# Convert existing cluster Ingress objects
ingress2gateway print \
  --providers=ingress-nginx \
  --namespace=default

# Output is ready-to-apply Gateway + HTTPRoute YAML
```

Supports 30+ common NGINX annotations including CORS, backend TLS, regex matching, path rewrites.

---

### Manual Migration Map

```
Old (Ingress)                          New (Gateway API)
─────────────────────────────────────────────────────────────────
annotations:                           HTTPRoute spec (no annotations)
  kubernetes.io/ingress.class          gatewayClassName on Gateway
  nginx.ingress.kubernetes.io/...      native spec fields

spec.rules[].host                      HTTPRoute.spec.hostnames[]
spec.rules[].http.paths[]              HTTPRoute.spec.rules[].matches[]
spec.rules[].backend.service           HTTPRoute.spec.rules[].backendRefs[]
spec.tls[]                             Gateway.spec.listeners[].tls
```

---

## Part 6 — Azure Application Gateway (Classic AGIC)

> This is the **original** Azure Application Gateway Ingress Controller — different from Application Gateway for Containers.

Classic Azure Application Gateway is a regional L7 load balancer that sits in front of AKS. The AGIC (Application Gateway Ingress Controller) runs as a pod in AKS and reads Ingress objects, then configures the Azure Application Gateway.

```
Client
  ↓
Azure Application Gateway  (L7 LB, WAF, SSL offload — in your VNet)
  ↓  AGIC reads Ingress rules, programs App Gateway
AKS Pod
```

**Why teams used it:**
- WAF (Web Application Firewall) out of the box
- Azure-native — visible in the portal
- Shared with non-Kubernetes workloads

**Why teams are moving away:**
- AGIC only supports the legacy Ingress API (not Gateway API)
- Application Gateway for Containers is the strategic successor
- Slower propagation of Ingress rule changes

**AGIC vs AGC:**

| | AGIC (Classic) | Application Gateway for Containers |
|---|---|---|
| API support | Ingress only | Ingress + Gateway API |
| Propagation speed | Seconds to minutes | Subsecond |
| Protocol support | HTTP, HTTPS | HTTP, HTTPS, WebSocket, gRPC |
| Strategic direction | Maintenance mode | Active investment |

---

## Summary: Which to Use

```
Are you on AKS?  YES
  │
  ├── Do you need WAF / deep Azure integration?
  │     YES → Application Gateway for Containers (AGC)
  │            Requires Azure CNI — may need cluster recreation
  │
  ├── Standard ingress, migrating from NGINX?
  │     → App Routing add-on with Gateway API (Istio)
  │       Works with kubenet (idcube-cluster)
  │       Simplest migration path
  │
  └── Already using Istio service mesh?
        → Istio Gateway API (Istio add-on)
          Cannot run alongside app routing Gateway API add-on

Timeline:
  Now             → Plan migration from Ingress
  Nov 2026        → NGINX Ingress loses Azure support
  Target state    → Gateway API (either option above)
```

---

## Files in This Directory

| File | Purpose |
|---|---|
| [gateway.yaml](gateway.yaml) | Gateway resource using `approuting-istio` GatewayClass |
| [httproute-idcube-services.yaml](httproute-idcube-services.yaml) | HTTPRoute for all idcube services |

---

Sources:
- [Kubernetes Ingress vs Gateway API: What to Use in 2026](https://oneuptime.com/blog/post/2026-02-20-kubernetes-ingress-vs-gateway-api/view)
- [AKS App Routing Gateway API with Istio — Microsoft Docs](https://learn.microsoft.com/en-us/azure/aks/app-routing-gateway-api)
- [Application Gateway for Containers Quickstart — Microsoft Docs](https://learn.microsoft.com/en-us/azure/application-gateway/for-containers/quickstart-deploy-application-gateway-for-containers-alb-controller-addon)
- [From Ingress to Gateway API — Microsoft Community Hub](https://techcommunity.microsoft.com/blog/azurearchitectureblog/from-ingress-to-gateway-api-a-pragmatic-path-forward-and-why-it-matters-now/4489779)
- [Announcing Ingress2Gateway 1.0 — kubernetes.io](https://kubernetes.io/blog/2026/03/20/ingress2gateway-1-0-release/)
- [AKS Application Routing add-on NGINX update — AKS Engineering Blog](https://blog.aks.azure.com/2025/11/13/ingress-nginx-update)
