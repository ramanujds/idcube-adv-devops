# What is Ingress in Kubernetes?

In simple words:

**Ingress = HTTP/HTTPS traffic router for your cluster**

Without Ingress:

* You expose services using `NodePort` or `LoadBalancer`
* Each service gets a different port
* Not clean
* Not production friendly

With Ingress:

* Single entry point (like API Gateway)
* Routes traffic based on:

    * Hostname (example.com)
    * Path (/api, /app)
* Supports SSL termination

Think of it as:

```
Client → Ingress Controller → Service → Pod
```

Important:
Ingress is just a configuration object.
You MUST have an Ingress Controller running.

---

# What is an Ingress Controller?

An Ingress Controller is the actual reverse proxy that implements the Ingress rules.

Most popular one:

* NGINX based controller

Common controller:

* NGINX Ingress Controller

---

# Architecture Overview

```
Browser
   ↓
Ingress Controller (NGINX Pod)
   ↓
K8s Service
   ↓
Pods
```

In Docker Desktop:

* Everything runs inside same node
* No external cloud load balancer
* But Docker Desktop has built-in support for NGINX Ingress

---

# Enable Ingress in Docker Desktop

### Step 1: Enable Kubernetes

Docker Desktop → Settings → Kubernetes → Enable

---

### Step 2: Enable Ingress Controller

Docker Desktop includes NGINX Ingress.

Run:

```bash
kubectl get pods -n ingress-nginx
```

If not present, enable from:
Docker Desktop → Settings → Kubernetes → Enable "Ingress"

---

# Practical Implementation Example

Let’s build:

* Two Spring Boot apps (or simple nginx apps)
* Expose both via Ingress
* Access using paths

---

## Step 1: Create Two Deployments

### app1.yaml

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: app1
spec:
  replicas: 1
  selector:
    matchLabels:
      app: app1
  template:
    metadata:
      labels:
        app: app1
    spec:
      containers:
        - name: app1
          image: nginx
          ports:
            - containerPort: 80
---
apiVersion: v1
kind: Service
metadata:
  name: app1-service
spec:
  selector:
    app: app1
  ports:
    - port: 80
      targetPort: 80
```

---

### app2.yaml

Same but rename to app2.

Apply:

```bash
kubectl apply -f app1.yaml
kubectl apply -f app2.yaml
```

---

## Step 2: Create Ingress Resource

### ingress.yaml

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: my-ingress
spec:
  ingressClassName: nginx
  rules:
    - host: myapp.local
      http:
        paths:
          - path: /app1
            pathType: Prefix
            backend:
              service:
                name: app1-service
                port:
                  number: 80
          - path: /app2
            pathType: Prefix
            backend:
              service:
                name: app2-service
                port:
                  number: 80
```

Apply:

```bash
kubectl apply -f ingress.yaml
```

---

# Configure Local DNS

Since we are not in cloud, we map host manually.

Edit your hosts file:

Mac/Linux:

```
/etc/hosts
```

Add:

```
127.0.0.1 myapp.local
```

---

# Test It

Open browser:

```
http://myapp.local/app1
http://myapp.local/app2
```

Boom — both apps routed via single Ingress.

---

# What Actually Happened Internally?

Let’s break it like an architect:

1. Browser hits `myapp.local`
2. Traffic goes to NGINX Ingress controller
3. It checks:
    * Host match?
    * Path match?
4. Routes to correct service
5. Service forwards to Pod

Clean Layer 7 routing.

---

# Advanced Concepts You Should Know

### SSL Termination

Using TLS secret:

```bash
kubectl create secret tls my-tls \
  --cert=cert.crt \
  --key=key.key
```

Then reference in ingress.

---

### Path Rewriting

Using annotation:

```yaml
metadata:
  annotations:
    nginx.ingress.kubernetes.io/rewrite-target: /
```

---

### Ingress vs Gateway API

Ingress is older model.
Gateway API is modern replacement.

But Ingress is still heavily used.

---

# Real World Example

Microservices:

```
api.company.com/user
api.company.com/order
api.company.com/payment
```

Single Ingress routes all traffic internally.

Much cleaner than exposing 10 NodePorts.

---
