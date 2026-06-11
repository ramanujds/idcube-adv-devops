# SESSION 10 — Application Gateway + NGINX Ingress

---

## Session Goal

* Expose applications externally
* Configure ingress routing
* Understand request flow end-to-end
* Debug common ingress issues (502, 404, probes)

---

# Continuing Case Study

> Your services are running and images are secured in ACR.
>
> Now you need to expose APIs to users.
>
> Issues observed:
>
> * Users get **502 Bad Gateway**
> * Some APIs return **404 unexpectedly**
> * Health probes fail intermittently
>
> Your task:
> Configure ingress and debug traffic flow

---

# Target Architecture

```id="b0x9fa"
Client → App Gateway → Internal LB → NGINX Ingress → Service → Pod
```

---

# LAB 1 — Deploy NGINX Ingress Controller

---

## Install

```bash id="8g1g0g"
kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/main/deploy/static/provider/cloud/deploy.yaml
```

---

## Verify

```bash id="br9p8y"
kubectl get pods -n ingress-nginx
```

---

---

# LAB 2 — Create Basic Ingress

---

## Ingress YAML

```yaml id="0x8e6y"
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: order-ingress
spec:
  rules:
  - host: order.local
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: order-service
            port:
              number: 80
```

---

## Setup Hosts Entry

```bash id="9ww2b7"
<ingress-ip> order.local
```

---

## Tasks

* Access via browser or curl

---

---

# LAB 3 — Path-Based Routing

---

## Extend Ingress

```yaml id="3x0gmp"
- path: /inventory
  pathType: Prefix
  backend:
    service:
      name: inventory-service
      port:
        number: 80
```

---

## Tasks

* `/orders` → order-service
* `/inventory` → inventory-service

---

---

# LAB 4 — 404 Issue (Rewrite Problem)

---

## Inject Issue

* App expects `/orders`
* Ingress sends `/`

---

## Add Rewrite Annotation

```yaml id="a3mp04"
nginx.ingress.kubernetes.io/rewrite-target: /
```

---

## Tasks

* Observe 404
* Fix path mapping

---

---

# LAB 5 — 502 Bad Gateway

---

## Inject Issue

* Wrong service port

---

## Example

```yaml id="z8o5hx"
port:
  number: 9999
```

---

## Tasks

* Access API
* Observe 502

---

## Debug

```bash id="p6j33l"
kubectl describe ingress
kubectl get svc
kubectl get endpoints
```

---

## Fix

* Correct port

---

---

# LAB 6 — Health Probe Failure

---

## Scenario

* App Gateway / ingress health checks failing

---

## Inject Issue

* Wrong health path

---

## Tasks

* Fix probe path (`/health`)

---

---

# LAB 7 — Host Header Issue

---

## Scenario

* Backend expects specific host header

---

## Inject Issue

* Missing host configuration

---

## Fix

* Configure proper host in ingress

---

---

# LAB 8 — TLS Termination

---

## Create Secret

```bash id="tjq0d7"
kubectl create secret tls tls-secret \
  --cert=cert.crt \
  --key=cert.key
```

---

## Update Ingress

```yaml id="1p5r3g"
tls:
- hosts:
  - order.local
  secretName: tls-secret
```

---

## Tasks

* Access via HTTPS

---

---

# LAB 9 — App Gateway Integration (Concept + Optional Demo)

---

## Flow

* External traffic → App Gateway
* Internal routing → Ingress

---

## Tasks

* Understand layering
* Observe request flow

---

---

# LAB 10 — Combined Debug Scenario

---

## Scenario

* 502 error
* 404 on specific route
* Health probe failing

---

## Tasks

* Identify each issue
* Fix step-by-step

---

---

# Real Case Scenario

---

## Issue

* API accessible internally
* Not accessible externally

---

## Investigation

1. Check ingress
2. Check service
3. Check endpoints
4. Validate paths

---

## Root Causes

* Wrong port
* Path mismatch
* Missing host

---

## Resolution

* Fix ingress config
* Validate routing

---

---

# Validation Tasks

---

* Access APIs via ingress
* Verify routing works correctly
* Ensure no 502 / 404 errors

---

---

# Final Architecture After Session

```id="b3k6tt"
Client → App Gateway → Ingress → order-service → inventory-service
```

