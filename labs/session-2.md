# SESSION 2 — Multi-Container Pods & Networking

---

## Session Goal

By the end, learners should:

* Understand **how containers collaborate inside a pod**
* Debug **service-to-service communication issues**
* Know when to use:

    * Sidecars
    * Init containers
* Clearly understand **Services, Endpoints, and traffic flow**

---

# Continuing Story

> Your optimized images are now deployed.
>
> But new issues appear:
>
> * Logs are inconsistent across services
> * `order-service` fails intermittently while calling `inventory-service`
> * Debugging is difficult due to lack of visibility
>
> Your job:
>
> * Improve observability using sidecars
> * Fix communication reliability

---

# Architecture Evolution

```id="c3q8cx"
Client → order-service → inventory-service
```

Now we enhance:

* Add logging sidecar
* Introduce Kubernetes Services

---

# LAB 1 — Deploy Basic Services 

---

## Setup

Deploy both apps:

* `order-service`
* `inventory-service`

---

## Tasks

1. Create Deployments
2. Expose using **ClusterIP services**

```yaml
apiVersion: v1
kind: Service
metadata:
  name: inventory-service
spec:
  selector:
    app: inventory
  ports:
    - port: 80
      targetPort: 3000
```

---

## Test

From order-service pod:

```bash
curl http://inventory-service/inventory
```

---

## Expected Learning

* Service DNS works
* Internal communication via service name

---

# LAB 2 — Break Communication 

---

## Inject Failure

* Change service selector incorrectly:

```yaml
selector:
  app: wrong-label
```

---

## Tasks

* Call inventory-service again
* Observe failure

---

## Debug Steps

```bash
kubectl get svc
kubectl get endpoints
kubectl describe svc inventory-service
```

---

## Key Observation

* Service exists
* But **no endpoints**

---


# LAB 3 — Multi-Container Pod (Sidecar Pattern)

---

## Problem

> Logs are scattered and hard to track.

---

## Solution

Add a **logging sidecar**

---

## Pod Example

```yaml
apiVersion: v1
kind: Pod
metadata:
  name: order-pod
spec:
  containers:
    - name: app
      image: order-service:v1
    - name: log-sidecar
      image: busybox
      command: ["sh", "-c", "tail -f /logs/app.log"]
```

---

## Tasks

* Deploy pod with sidecar
* Share volume between containers

---

## Enhancement

Add shared volume:

```yaml
volumes:
  - name: log-volume
    emptyDir: {}
```

---


# LAB 4 — Init Container (Dependency Check)

---

## Problem

> order-service starts before inventory-service → failures

---

## Solution

Use init container to block startup

---

## Example

```yaml
initContainers:
- name: wait-for-inventory
  image: busybox
  command: ['sh', '-c', 'until nslookup inventory-service; do sleep 2; done']
```

---

## Tasks

* Deploy with init container
* Restart pods

---

## Expected Outcome

* order-service waits properly

---


# LAB 5 — Service Types Exploration

---

## Topics

* ClusterIP
* NodePort
* LoadBalancer

---

## Tasks

1. Convert service to NodePort
2. Access from browser

---


# LAB 6 — Endpoint Deep Dive

---

## Tasks

```bash
kubectl get endpoints inventory-service
```

---

## Show

* Pod IPs mapped to service
* Load balancing behavior

---

## Advanced Discussion

* EndpointSlices
* Scale impact

---

# LAB 7 — DNS & Service Discovery

---

## Tasks

Inside pod:

```bash
nslookup inventory-service
```

---

## Explain

* CoreDNS role
* Internal DNS resolution

---


# Real Production Scenario

---

## Issue

> Orders API returning 500 randomly

---

## Investigation Flow

1. Check order-service logs
2. Try curl to inventory-service
3. Check service endpoints
4. Identify selector mismatch

---

## Root Cause

* Wrong label → no endpoints → connection failure

---

## Fix

* Correct selector
* Validate endpoints

---

# Final Architecture After Session

```id="ptc33v"
Client → order-service (sidecar + init) → inventory-service
```

---

