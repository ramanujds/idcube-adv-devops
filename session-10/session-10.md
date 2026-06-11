# SESSION 10 — Ingress: NGINX & Application Gateway

---

## Session Goal

* Understand how Kubernetes Ingress works and why it replaces NodePort/LoadBalancer
* Install NGINX Ingress Controller on AKS using Helm
* Deploy both microservices and expose them via a single Ingress
* Configure path-based routing (`/orders`, `/inventory`, `/parts`)
* Debug common ingress failures: 502 Bad Gateway, 404, health probe failures
* Understand TLS termination at the ingress layer
* Understand Application Gateway layering for production traffic

---

## Context: Where We Are

After Session 9, images are in ACR and the cluster (`idcube-cluster`) is running. Services are currently exposed via `NodePort` — each gets a separate port on each node. This doesn't scale cleanly:

```
Current (NodePort — not production-ready):
  part-order-service     → NodeIP:30001
  part-inventory-service → NodeIP:30002

Target (Ingress — single entry point):
  <ingress-ip>/orders     → part-order-service
  <ingress-ip>/inventory  → part-inventory-service
  <ingress-ip>/parts      → part-inventory-service
```

---

## What is Ingress?

**Ingress** is a Kubernetes API object that defines HTTP/HTTPS routing rules.
**Ingress Controller** is the actual reverse proxy that reads those rules and routes traffic.

> Ingress is just config. Without a controller running, it does nothing.

```
Internet
    ↓
Ingress Controller Pod (NGINX)   ← reads Ingress rules from K8s API
    ↓
ClusterIP Service
    ↓
Application Pod
```

**Why use Ingress instead of LoadBalancer per service?**

| Approach | Cost | Routing |
|---|---|---|
| LoadBalancer per service | 1 Azure LB per service ($$$) | L4 only — IP:port |
| NodePort | Free but ugly — random ports | Not production |
| Ingress | 1 LB shared across all services | L7 — host + path routing, TLS |

---

## Architecture for This Session

```
Client
  ↓
Azure Load Balancer  (public IP — provisioned automatically by AKS)
  ↓
NGINX Ingress Controller Pod  (in namespace: ingress-nginx)
  ↓  (matches on path)
  ├── /orders  → part-order-service:80     → Pod :8080
  ├── /inventory → part-inventory-service:80 → Pod :8080
  ├── /parts   → part-inventory-service:80 → Pod :8080
  └── /        → part-order-service:80     → Pod :8080
```

---

## Manifest Files Reference

All manifests for this session live in [ingress-azure/apps/](ingress-azure/apps/) and [ingress-azure/ingress/](ingress-azure/ingress/).

| File | Purpose |
|---|---|
| `apps/part-order-deployment.yaml` | Order service Deployment |
| `apps/part-order-service.yaml` | Order service Service (NodePort → port 80) |
| `apps/part-inventory-deployment.yaml` | Inventory service Deployment (with probes) |
| `apps/part-inventory-service.yaml` | Inventory service Service (NodePort → port 80) |
| `apps/part-inventory-hpa.yml` | HPA: 2-5 replicas, CPU 70%, memory 80% |
| `ingress/part-order-ingress.yml` | Ingress: path routing for all services |

---

# LAB 1 — Install NGINX Ingress Controller

---

## Why Helm?

Helm is the Kubernetes package manager. Instead of applying 20+ YAML files manually (RBAC, Deployments, Services, ConfigMaps for the controller), Helm installs everything in one command and handles upgrades cleanly.

---

## Step 1: Add the Helm Repository

```bash
helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx
helm repo update
```

Expected:
```
"ingress-nginx" has been added to your repositories
Hang tight while we grab the latest from your chart repositories...
...Successfully got an update from the "ingress-nginx" chart repository
```

---

## Step 2: Install the Controller

```bash
helm install nginx-ingress ingress-nginx/ingress-nginx \
  --namespace ingress-nginx \
  --create-namespace
```

This creates a dedicated `ingress-nginx` namespace and installs:
- The NGINX Ingress Controller Deployment
- A LoadBalancer Service that gets a public Azure IP
- RBAC roles so the controller can watch Ingress objects cluster-wide
- A ValidatingWebhookConfiguration for ingress validation

---

## Step 3: Verify the Installation

```bash
# Controller pod should be Running
kubectl get pods -n ingress-nginx

# LoadBalancer service — wait for EXTERNAL-IP to be assigned (takes 1-2 minutes on AKS)
kubectl get svc -n ingress-nginx
```

Expected output:
```
NAME                                          READY   STATUS    RESTARTS   AGE
nginx-ingress-ingress-nginx-controller-5f7d   1/1     Running   0          90s

NAME                                        TYPE           CLUSTER-IP    EXTERNAL-IP      PORT(S)
nginx-ingress-ingress-nginx-controller      LoadBalancer   10.0.45.12    20.219.xx.xx     80:31234/TCP,443:31235/TCP
```

Save the external IP — you will use it throughout this session:

```bash
INGRESS_IP=$(kubectl get svc nginx-ingress-ingress-nginx-controller \
  -n ingress-nginx \
  -o jsonpath='{.status.loadBalancer.ingress[0].ip}')
echo $INGRESS_IP
```

---

## Step 4: Check Controller Logs (Useful for Debugging Later)

```bash
kubectl logs -n ingress-nginx \
  -l app.kubernetes.io/name=ingress-nginx \
  --tail=50
```

---

# LAB 2 — Deploy the Services

---

## Deploy part-order-service

**[ingress-azure/apps/part-order-deployment.yaml](ingress-azure/apps/part-order-deployment.yaml)**

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: part-order-service
spec:
  replicas: 1
  selector:
    matchLabels:
      app: part-order-service
  template:
    metadata:
      labels:
        app: part-order-service
    spec:
      containers:
        - name: part-order-service
          image: ram1uj/part-order-service:latest
          imagePullPolicy: Always
          resources:
            requests:
              cpu: "200m"
              memory: "256Mi"
            limits:
              cpu: "500m"
              memory: "512Mi"
          env:
            - name: SPRING_PROFILES_ACTIVE
              value: "dev"
            - name: INVENTORY_SERVICE_URL
              value: "http://part-inventory-service"
          ports:
            - containerPort: 8080
```

**[ingress-azure/apps/part-order-service.yaml](ingress-azure/apps/part-order-service.yaml)**

```yaml
apiVersion: v1
kind: Service
metadata:
  name: part-order-service
spec:
  selector:
    app: part-order-service
  type: NodePort
  ports:
    - port: 80
      targetPort: 8080
```

Key points:
- Service listens on port **80**, forwards to pod port **8080**
- `INVENTORY_SERVICE_URL: http://part-inventory-service` — the order service calls inventory by its Kubernetes DNS name (ClusterIP)
- `NodePort` type is fine for now — the Ingress controller talks to it via ClusterIP anyway

---

## Deploy part-inventory-service

**[ingress-azure/apps/part-inventory-deployment.yaml](ingress-azure/apps/part-inventory-deployment.yaml)**

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: part-inventory-service
spec:
  replicas: 1
  selector:
    matchLabels:
      app: part-inventory-service
  template:
    metadata:
      labels:
        app: part-inventory-service
    spec:
      containers:
        - name: part-inventory-service
          image: ram1uj/part-inventory-service
          imagePullPolicy: Always
          resources:
            requests:
              cpu: "400m"
              memory: "256Mi"
            limits:
              cpu: "600m"
              memory: "512Mi"
          env:
            - name: SPRING_PROFILES_ACTIVE
              value: "dev"
          startupProbe:
            httpGet:
              path: /actuator/health/liveness
              port: 8080
            failureThreshold: 30
            periodSeconds: 10
          livenessProbe:
            httpGet:
              path: /actuator/health/liveness
              port: 8080
            initialDelaySeconds: 0
            periodSeconds: 10
            failureThreshold: 3
          readinessProbe:
            httpGet:
              path: /actuator/health/readiness
              port: 8080
            periodSeconds: 5
            failureThreshold: 3
          ports:
            - containerPort: 8080
```

**Probe explanation:**

| Probe | Path | What it does |
|---|---|---|
| `startupProbe` | `/actuator/health/liveness` | Gives Spring Boot up to 300s (30×10) to start. Liveness/readiness are paused until this passes. |
| `livenessProbe` | `/actuator/health/liveness` | If this fails 3 times, Kubernetes restarts the container |
| `readinessProbe` | `/actuator/health/readiness` | If this fails, pod is removed from Service endpoints (no traffic sent) |

**[ingress-azure/apps/part-inventory-service.yaml](ingress-azure/apps/part-inventory-service.yaml)**

```yaml
apiVersion: v1
kind: Service
metadata:
  name: part-inventory-service
spec:
  selector:
    app: part-inventory-service
  type: NodePort
  ports:
    - port: 80
      targetPort: 8080
```

---

## Apply All App Manifests

```bash
kubectl apply -f ingress-azure/apps/part-order-deployment.yaml
kubectl apply -f ingress-azure/apps/part-order-service.yaml
kubectl apply -f ingress-azure/apps/part-inventory-deployment.yaml
kubectl apply -f ingress-azure/apps/part-inventory-service.yaml

# Watch until both pods are Running
kubectl get pods -w
```

Expected (after ~60s for inventory due to startup probe):
```
NAME                                       READY   STATUS    RESTARTS
part-order-service-6b9f4d7c5-xk2p3        1/1     Running   0
part-inventory-service-7d5c9b8f4-mn3rs    1/1     Running   0
```

Check endpoints are populated (confirms pods are ready):
```bash
kubectl get endpoints part-order-service
kubectl get endpoints part-inventory-service
# Should show pod IPs, not <none>
```

---

# LAB 3 — Create the Ingress (Path-Based Routing)

---

## The Ingress YAML

**[ingress-azure/ingress/part-order-ingress.yml](ingress-azure/ingress/part-order-ingress.yml)**

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
          - path: /inventory
            pathType: Prefix
            backend:
              service:
                name: part-inventory-service
                port:
                  number: 80
          - path: /parts
            pathType: Prefix
            backend:
              service:
                name: part-inventory-service
                port:
                  number: 80
          - path: /orders
            pathType: Prefix
            backend:
              service:
                name: part-order-service
                port:
                  number: 80
          - path: /
            pathType: Prefix
            backend:
              service:
                name: part-order-service
                port:
                  number: 80
```

**Key points:**

- `annotations: kubernetes.io/ingress.class: "nginx"` — tells Kubernetes which controller owns this Ingress. Older syntax; newer clusters use `ingressClassName: nginx` in the spec instead.
- No `host:` field — this is a **wildcard** ingress. It matches any hostname hitting the ingress IP. Useful for dev/testing; in production you specify a hostname.
- `pathType: Prefix` — `/inventory` matches `/inventory`, `/inventory/`, `/inventory/123`, etc.
- Routes are evaluated **top to bottom** — `/` is last because it's a catch-all.

---

## Apply the Ingress

```bash
kubectl apply -f ingress-azure/ingress/part-order-ingress.yml

# Verify ingress was created and has an address
kubectl get ingress
```

Expected:
```
NAME                 CLASS    HOSTS   ADDRESS          PORTS   AGE
part-order-ingress   <none>   *       20.219.xx.xx     80      30s
```

The `ADDRESS` is the same IP as the NGINX LoadBalancer service.

---

## Test the Routing

```bash
INGRESS_IP=<your-ingress-ip>

# Default route → order service
curl http://$INGRESS_IP/

# Order service
curl http://$INGRESS_IP/orders

# Inventory service via /inventory
curl http://$INGRESS_IP/inventory

# Inventory service via /parts
curl http://$INGRESS_IP/parts
```

---

## Inspect How NGINX Sees the Rules

```bash
# Describe to see events and backend status
kubectl describe ingress part-order-ingress
```

Look for:
```
Rules:
  Host        Path         Backends
  ----        ----         --------
  *
              /inventory   part-inventory-service:80 (10.244.x.x:8080)
              /parts       part-inventory-service:80 (10.244.x.x:8080)
              /orders      part-order-service:80 (10.244.x.x:8080)
              /             part-order-service:80 (10.244.x.x:8080)
```

Backend pod IPs appear in brackets — if they show `<none>` or are missing, the service has no ready endpoints.

---

# LAB 4 — 404: Path Rewrite Issue

---

## Scenario

The app (order service) expects requests at `/orders/list`.
The ingress receives `GET /orders/list` and forwards it as-is — that works fine.

But if you configure the ingress path as `/api/orders` and the app expects `/orders`, the app gets `/api/orders` and returns 404 because it has no route for that prefix.

---

## Reproduce

Add a new path `/api/orders` to the ingress without a rewrite:

```yaml
- path: /api/orders
  pathType: Prefix
  backend:
    service:
      name: part-order-service
      port:
        number: 80
```

```bash
curl http://$INGRESS_IP/api/orders
# 404 — app has no route for /api/orders, only /orders
```

---

## Fix: rewrite-target Annotation

The `rewrite-target` annotation strips the matched prefix before forwarding:

```yaml
metadata:
  name: part-order-ingress
  annotations:
    kubernetes.io/ingress.class: "nginx"
    nginx.ingress.kubernetes.io/rewrite-target: /$2
spec:
  rules:
    - http:
        paths:
          - path: /api/orders(/|$)(.*)
            pathType: ImplementationSpecific
            backend:
              service:
                name: part-order-service
                port:
                  number: 80
```

**How this works:**
- `(/|$)(.*)` captures everything after `/api/orders`
- `rewrite-target: /$2` replaces the path with just the captured suffix
- `GET /api/orders/list` → forwarded as `GET /list` to the backend

For our services, since we use `Prefix` and the app paths match the ingress paths, no rewrite is needed. Keep the original simple form.

---

# LAB 5 — 502 Bad Gateway

---

## What 502 Means

The ingress controller reached the backend service but got no valid response — the connection was refused, timed out, or the service port was wrong.

---

## Inject the Issue

Edit the ingress to point to a wrong port:

```yaml
- path: /orders
  pathType: Prefix
  backend:
    service:
      name: part-order-service
      port:
        number: 9999   # wrong — service listens on 80
```

```bash
kubectl apply -f ingress-azure/ingress/part-order-ingress.yml
curl http://$INGRESS_IP/orders
# 502 Bad Gateway
```

---

## Debug Step by Step

### Step 1 — Check the Ingress

```bash
kubectl describe ingress part-order-ingress
```

Look at the Backends line:
```
/orders   part-order-service:9999 ()
```

Empty parentheses `()` means no endpoints resolved for port 9999 — the service has no such port.

### Step 2 — Check the Service

```bash
kubectl get svc part-order-service
```

```
NAME                 TYPE       CLUSTER-IP    EXTERNAL-IP   PORT(S)
part-order-service   NodePort   10.0.183.42   <none>        80:31xxx/TCP
```

Service exposes port **80**, not 9999.

### Step 3 — Check Endpoints

```bash
kubectl get endpoints part-order-service
```

```
NAME                 ENDPOINTS
part-order-service   10.244.1.5:8080
```

Endpoints are there (pod is healthy). Problem is only the wrong port in the Ingress.

### Step 4 — Check Controller Logs

```bash
kubectl logs -n ingress-nginx \
  -l app.kubernetes.io/name=ingress-nginx \
  --tail=30
```

You will see lines like:
```
upstream "default-part-order-service-9999" invalid because port 9999 was not found
```

### Fix

```yaml
port:
  number: 80   # corrected
```

```bash
kubectl apply -f ingress-azure/ingress/part-order-ingress.yml
curl http://$INGRESS_IP/orders
# 200 OK
```

---

## Other 502 Causes

| Cause | Signal |
|---|---|
| Pod is not ready | `kubectl get endpoints` shows `<none>` |
| Pod is crashing | `kubectl get pods` shows CrashLoopBackOff |
| App panics on first request | `kubectl logs <pod>` |
| Wrong containerPort | Pod runs but connection refused — check app is listening on 8080 |

---

# LAB 6 — Health Probe Failures

---

## Scenario

After deploying the inventory service, pods keep restarting. The order service shows pods flapping between Ready and Not Ready.

---

## Understand the Probe Chain

For `part-inventory-service`, the probe chain is:

```
Pod starts
  ↓
startupProbe fires every 10s (up to 30 failures = 5 min max wait)
  → hits /actuator/health/liveness
  → until it returns 200, liveness/readiness probes are suspended
  ↓
startupProbe passes
  ↓
livenessProbe fires every 10s
  → hits /actuator/health/liveness
  → 3 consecutive failures → container is killed and restarted
  ↓
readinessProbe fires every 5s
  → hits /actuator/health/readiness
  → 3 consecutive failures → pod removed from Service endpoints (no traffic)
```

---

## Inject Issue: Wrong Probe Path

Change the liveness probe path to something that doesn't exist:

```yaml
livenessProbe:
  httpGet:
    path: /health   # wrong — Spring Boot uses /actuator/health/liveness
    port: 8080
  initialDelaySeconds: 0
  periodSeconds: 10
  failureThreshold: 3
```

After apply, wait ~30 seconds:

```bash
kubectl get pods -w
```

```
NAME                                     READY   STATUS    RESTARTS
part-inventory-service-7d5c-mn3rs        0/1     Running   0
part-inventory-service-7d5c-mn3rs        0/1     Running   1   ← restarted
part-inventory-service-7d5c-mn3rs        0/1     Running   2
```

---

## Debug

```bash
kubectl describe pod <inventory-pod-name>
```

Events section:
```
Warning  Unhealthy  10s (x3)  kubelet  Liveness probe failed:
  HTTP probe failed with statuscode: 404
Warning  Killing    8s         kubelet  Container inventory killed (liveness probe failed)
```

```bash
# Check what paths the app actually exposes
kubectl exec <inventory-pod> -- curl -s localhost:8080/actuator/health
# {"status":"UP",...}

kubectl exec <inventory-pod> -- curl -s localhost:8080/actuator/health/liveness
# {"status":"UP"}
```

---

## Fix

Restore the correct probe paths:

```yaml
livenessProbe:
  httpGet:
    path: /actuator/health/liveness
    port: 8080
readinessProbe:
  httpGet:
    path: /actuator/health/readiness
    port: 8080
```

```bash
kubectl apply -f ingress-azure/apps/part-inventory-deployment.yaml
kubectl rollout status deployment/part-inventory-service
```

---

## Probe Tuning Guidelines

| Probe | Recommended for slow-starting apps |
|---|---|
| `startupProbe` | Set `failureThreshold` high (e.g. 30) to give Spring Boot time to start |
| `livenessProbe` | Keep `failureThreshold: 3` — don't make it too sensitive |
| `readinessProbe` | Shorter `periodSeconds` (5s) so traffic stops quickly if app is overloaded |

---

# LAB 7 — Host-Based Routing

---

## Scenario

Right now the ingress routes based only on path (no hostname check). In production, you typically have:

```
api.idcube.com/orders     → order service
api.idcube.com/inventory  → inventory service
```

---

## Update Ingress to Use a Hostname

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: part-order-ingress
  annotations:
    kubernetes.io/ingress.class: "nginx"
spec:
  rules:
    - host: api.idcube.local
      http:
        paths:
          - path: /inventory
            pathType: Prefix
            backend:
              service:
                name: part-inventory-service
                port:
                  number: 80
          - path: /parts
            pathType: Prefix
            backend:
              service:
                name: part-inventory-service
                port:
                  number: 80
          - path: /orders
            pathType: Prefix
            backend:
              service:
                name: part-order-service
                port:
                  number: 80
          - path: /
            pathType: Prefix
            backend:
              service:
                name: part-order-service
                port:
                  number: 80
```

---

## Map the Hostname Locally

On your machine (Mac/Linux):

```bash
sudo nano /etc/hosts
```

Add:

```
20.219.xx.xx   api.idcube.local
```

Now test:

```bash
curl http://api.idcube.local/orders
curl http://api.idcube.local/inventory

# Without the right Host header — should get 404 from NGINX
curl http://$INGRESS_IP/orders
```

**Why the second curl fails:** when a `host:` field is specified in the ingress rule, NGINX only routes traffic that has the matching `Host:` header. A bare IP request has no matching rule → NGINX returns its default 404.

---

## Common Issue: Missing or Wrong Host Header

If a client calls the API using the IP directly (not the DNS name), it won't match. Always test with the correct hostname:

```bash
# Explicit host header
curl -H "Host: api.idcube.local" http://$INGRESS_IP/orders
```

---

# LAB 8 — TLS Termination

---

## Concept

TLS is terminated **at the ingress controller**. The connection from the client to NGINX is encrypted. NGINX decrypts it and forwards plain HTTP to the backend pods. Pods don't need TLS certificates.

```
Client --HTTPS--> NGINX Ingress (TLS terminated) --HTTP--> Pod
```

---

## Step 1: Generate a Self-Signed Certificate (Dev/Test)

```bash
openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
  -keyout tls.key \
  -out tls.crt \
  -subj "/CN=api.idcube.local/O=idcube"
```

---

## Step 2: Create a Kubernetes TLS Secret

```bash
kubectl create secret tls idcube-tls \
  --cert=tls.crt \
  --key=tls.key
```

Verify:

```bash
kubectl get secret idcube-tls
# TYPE: kubernetes.io/tls
```

---

## Step 3: Update Ingress to Enable TLS

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: part-order-ingress
  annotations:
    kubernetes.io/ingress.class: "nginx"
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
spec:
  tls:
    - hosts:
        - api.idcube.local
      secretName: idcube-tls
  rules:
    - host: api.idcube.local
      http:
        paths:
          - path: /inventory
            pathType: Prefix
            backend:
              service:
                name: part-inventory-service
                port:
                  number: 80
          - path: /parts
            pathType: Prefix
            backend:
              service:
                name: part-inventory-service
                port:
                  number: 80
          - path: /orders
            pathType: Prefix
            backend:
              service:
                name: part-order-service
                port:
                  number: 80
          - path: /
            pathType: Prefix
            backend:
              service:
                name: part-order-service
                port:
                  number: 80
```

Key additions:
- `spec.tls` — ties the secret to the host
- `ssl-redirect: "true"` — NGINX will redirect HTTP → HTTPS automatically

---

## Step 4: Test HTTPS

```bash
# -k skips certificate verification (self-signed cert is not trusted by default)
curl -k https://api.idcube.local/orders

# Verify redirect
curl -v http://api.idcube.local/orders 2>&1 | grep "< HTTP"
# HTTP/1.1 308 Permanent Redirect  ← redirected to HTTPS
```

---

## Production TLS: cert-manager

For real certificates (Let's Encrypt), use cert-manager:

```bash
# Install cert-manager
helm repo add jetstack https://charts.jetstack.io
helm install cert-manager jetstack/cert-manager \
  --namespace cert-manager \
  --create-namespace \
  --set installCRDs=true
```

Then annotate the ingress:

```yaml
annotations:
  cert-manager.io/cluster-issuer: "letsencrypt-prod"
```

cert-manager handles certificate issuance and renewal automatically.

---

# LAB 9 — HPA: Auto-scaling the Inventory Service

---

The inventory service already has an HPA defined.

**[ingress-azure/apps/part-inventory-hpa.yml](ingress-azure/apps/part-inventory-hpa.yml)**

```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: part-inventory-hpa
spec:
  maxReplicas: 5
  minReplicas: 2
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: part-inventory-service
  metrics:
    - type: Resource
      resource:
        name: cpu
        target:
          type: Utilization
          averageUtilization: 70
    - type: Resource
      resource:
        name: memory
        target:
          type: Utilization
          averageUtilization: 80
```

---

## Apply and Watch

```bash
kubectl apply -f ingress-azure/apps/part-inventory-hpa.yml

kubectl get hpa -w
```

```
NAME                   REFERENCE                        TARGETS           MINPODS   MAXPODS   REPLICAS
part-inventory-hpa     Deployment/part-inventory-service   12%/70%, 30%/80%    2         5         2
```

- HPA keeps at least **2 replicas** at all times
- Scales up to **5** when CPU > 70% or memory > 80%
- The ingress automatically routes to all ready replicas via the Service

---

## Load Test to Trigger Scale

```bash
# Run a quick load test (requires hey or k6)
hey -n 10000 -c 50 http://$INGRESS_IP/inventory

# Watch HPA react
kubectl get hpa part-inventory-hpa -w
kubectl get pods -l app=part-inventory-service -w
```

---

# LAB 10 — Combined Debug Scenario

---

## Scenario

A release was deployed. Multiple things are broken simultaneously:

```bash
kubectl get pods
```

```
NAME                                     READY   STATUS             RESTARTS
part-order-service-6b9c4d8f5-xk2p3      1/1     Running            0
part-inventory-service-7d5c9b8f4-mn3r   0/1     Running            5   ← crashing
```

```bash
curl http://$INGRESS_IP/orders
# 200 OK

curl http://$INGRESS_IP/inventory
# 502 Bad Gateway
```

---

## Systematic Investigation

### Check 1 — Is the inventory pod healthy?

```bash
kubectl describe pod <inventory-pod>
```

Events:
```
Warning  Unhealthy   5s   kubelet  Liveness probe failed: HTTP probe failed: 404
Warning  Killing     3s   kubelet  Container killed due to liveness probe failure
```

Wrong liveness probe path — fixed in Lab 6. Apply the correct deployment.

### Check 2 — After pod stabilizes, still 502?

```bash
kubectl get endpoints part-inventory-service
# NAME                      ENDPOINTS
# part-inventory-service    <none>   ← readiness probe failing, pod not in endpoints
```

Readiness probe is also wrong. Fix both probes, redeploy.

### Check 3 — Now 404 on /inventory?

```bash
kubectl describe ingress part-order-ingress
```

```
/inventory   part-inventory-service:8080 (10.244.1.6:8080)
```

Port `8080` in the Ingress — but the service listens on `80`! The ingress is bypassing the service and going directly to port 8080, which may or may not work depending on the backend. Fix: set ingress backend port to `80`.

---

## Fix Order

1. Fix probe paths in deployment → `kubectl apply`
2. Wait for pods to become Ready → `kubectl get pods -w`
3. Fix ingress backend port → `kubectl apply`
4. Verify endpoints populated → `kubectl get endpoints`
5. Test all routes → `curl`

---

# Real Case Scenario

---

## Situation

API is accessible from inside the cluster but external clients get 502.

```bash
# From inside cluster (works)
kubectl run test --image=curlimages/curl --rm -it -- \
  curl http://part-order-service/orders

# From external (fails)
curl http://$INGRESS_IP/orders
# 502
```

---

## Investigation

```bash
# Step 1: Is the ingress address correct?
kubectl get ingress
# Check ADDRESS field matches your INGRESS_IP

# Step 2: Describe ingress — are backends populated?
kubectl describe ingress part-order-ingress
# Look for pod IPs in the backend list

# Step 3: Check controller logs for this specific request
kubectl logs -n ingress-nginx \
  -l app.kubernetes.io/name=ingress-nginx \
  --tail=100 | grep "order"

# Step 4: Check the service endpoints
kubectl get endpoints part-order-service
```

---

## Root Cause Found

Controller logs show:
```
dial tcp 10.244.1.5:8080: connect: connection refused
```

The pod is Running but the app inside crashed — it started, but threw an exception after a few seconds. No liveness probe was configured for the order service, so Kubernetes doesn't know it's dead.

```bash
kubectl logs part-order-service-6b9c4d8f5-xk2p3 --tail=30
# NullPointerException: INVENTORY_SERVICE_URL not set
```

The environment variable was accidentally removed from the deployment.

---

## Fix

```bash
kubectl set env deployment/part-order-service \
  INVENTORY_SERVICE_URL=http://part-inventory-service

kubectl rollout status deployment/part-order-service
curl http://$INGRESS_IP/orders
# 200 OK
```

---

# Ingress Debugging Quick Reference

```
502 Bad Gateway
├─ kubectl describe ingress → backend port wrong?
├─ kubectl get endpoints → <none>? → pod not ready
├─ kubectl logs -n ingress-nginx → connection refused?
└─ kubectl logs <app-pod> → app crashed?

404 Not Found
├─ Path doesn't match any ingress rule → check rules order
├─ App doesn't have a route for that path → need rewrite-target
└─ Host header mismatch → ingress has host: field, request uses IP

Probe Failures (CrashLoopBackOff / Not Ready)
├─ kubectl describe pod → Liveness/Readiness probe failed
├─ curl from inside pod → test the actual path
└─ Fix path in deployment, apply, rollout restart

No EXTERNAL-IP on Ingress Controller Service
└─ AKS: wait 1-2 min for Azure to provision LB
   kubectl get svc -n ingress-nginx -w
```

---

# Validation Checklist

```bash
# 1. Controller running
kubectl get pods -n ingress-nginx

# 2. External IP assigned
kubectl get svc -n ingress-nginx

# 3. Both app pods Ready
kubectl get pods

# 4. Endpoints populated (no <none>)
kubectl get endpoints

# 5. Ingress shows backend pod IPs
kubectl describe ingress part-order-ingress

# 6. All routes respond correctly
curl http://$INGRESS_IP/
curl http://$INGRESS_IP/orders
curl http://$INGRESS_IP/inventory
curl http://$INGRESS_IP/parts

# 7. HPA maintaining minimum replicas
kubectl get hpa
```

---

# Final Architecture After Session 10

```
Internet
  ↓
Azure Load Balancer  (public IP, provisioned by AKS for NGINX controller)
  ↓
NGINX Ingress Controller  (namespace: ingress-nginx)
  │  ingress class: nginx
  │  TLS terminated here (cert from idcube-tls secret)
  │
  ├── /orders  ──────────────────→  part-order-service:80  →  Pod :8080
  │                                  (Spring Boot, Java)
  │                                  ↓ calls via ClusterIP
  ├── /inventory ─────────────────→  part-inventory-service:80  →  Pod :8080
  ├── /parts    ─────────────────→  part-inventory-service:80  →  Pod :8080
  └── /         ─────────────────→  part-order-service:80  →  Pod :8080
                                     HPA: 2-5 replicas
                                     CPU > 70% → scale up
```
