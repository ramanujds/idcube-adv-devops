# Ingress with TLS Termination

---

## What is TLS Termination?

TLS termination means the SSL/TLS encryption is handled at the **ingress controller**, not inside your application pods.

```
Client  ──── HTTPS (encrypted) ────►  NGINX Ingress  ──── HTTP (plain) ────►  Pod
                                       (decrypts here)
```

**Why terminate at the ingress?**

- Pods don't need certificates or TLS config — simpler app code
- Certificate management is centralised in one place
- Traffic inside the cluster (pod-to-pod) is already on a private network
- Easier to rotate certificates without touching applications

---

## Two Approaches Covered

| Approach | Certificate Source | Use Case |
|---|---|---|
| **Self-signed cert** | `openssl` on your machine | Dev / local testing |
| **cert-manager + Let's Encrypt** | Automatic, free, trusted | Production |

---

## Prerequisites

Before starting, verify:

```bash
# NGINX Ingress controller is running
kubectl get pods -n ingress-nginx

# External IP is assigned
kubectl get svc -n ingress-nginx
INGRESS_IP=$(kubectl get svc nginx-ingress-ingress-nginx-controller \
  -n ingress-nginx \
  -o jsonpath='{.status.loadBalancer.ingress[0].ip}')
echo $INGRESS_IP

# Both services are deployed and Ready
kubectl get pods -l app=part-order-service
kubectl get pods -l app=part-inventory-service
```

---

# APPROACH 1 — Self-Signed Certificate (Dev / Testing)

---

## Step 1: Generate the Certificate

```bash
openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
  -keyout tls.key \
  -out tls.crt \
  -subj "/CN=api.idcube.local/O=idcube"
```

**What each flag does:**

| Flag | Meaning |
|---|---|
| `-x509` | Output a self-signed certificate (not a CSR) |
| `-nodes` | No passphrase on the private key (needed for automated use) |
| `-days 365` | Certificate valid for 1 year |
| `-newkey rsa:2048` | Generate a new 2048-bit RSA key pair |
| `-keyout tls.key` | Write private key to this file |
| `-out tls.crt` | Write certificate to this file |
| `-subj "/CN=api.idcube.local"` | Common Name — must match the hostname in the Ingress |

Verify the certificate:

```bash
openssl x509 -in tls.crt -text -noout | grep -E "Subject:|Not After"
# Subject: CN=api.idcube.local, O=idcube
# Not After : Jun 10 00:00:00 2027 GMT
```

---

## Step 2: Create the Kubernetes TLS Secret

Kubernetes stores the certificate and key as a Secret of type `kubernetes.io/tls`.

```bash
kubectl create secret tls idcube-tls \
  --cert=tls.crt \
  --key=tls.key
```

Verify:

```bash
kubectl get secret idcube-tls
```

```
NAME         TYPE                DATA   AGE
idcube-tls   kubernetes.io/tls   2      5s
```

The secret stores two keys:
- `tls.crt` — the certificate (base64 encoded)
- `tls.key` — the private key (base64 encoded)

To inspect (base64 decode the cert):

```bash
kubectl get secret idcube-tls \
  -o jsonpath='{.data.tls\.crt}' | base64 -d | openssl x509 -text -noout | grep "Subject:"
```

---

## Step 3: Apply the TLS Ingress

The manifest is at [tls-ingress.yaml](tls-ingress.yaml):

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: part-order-ingress-tls
  annotations:
    kubernetes.io/ingress.class: "nginx"
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
    nginx.ingress.kubernetes.io/force-ssl-redirect: "true"
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

**Key fields explained:**

```
spec.tls
  hosts: [api.idcube.local]   ← NGINX serves this cert for requests to this hostname
  secretName: idcube-tls      ← which Kubernetes secret holds the cert+key

annotations:
  ssl-redirect: "true"        ← HTTP requests get 308 redirect to HTTPS
  force-ssl-redirect: "true"  ← enforces redirect even if X-Forwarded-Proto is http

spec.rules
  host: api.idcube.local      ← must match what's in spec.tls.hosts
```

Apply:

```bash
kubectl apply -f tls-ingress.yaml

kubectl get ingress part-order-ingress-tls
```

```
NAME                     CLASS    HOSTS             ADDRESS          PORTS     AGE
part-order-ingress-tls   <none>   api.idcube.local  20.219.xx.xx    80, 443   15s
```

Note: `PORTS` now shows both `80` and `443`. Without TLS it only showed `80`.

---

## Step 4: Map the Hostname

Add the ingress IP to `/etc/hosts` on your machine:

```bash
sudo sh -c "echo '$INGRESS_IP api.idcube.local' >> /etc/hosts"

# Verify
grep api.idcube.local /etc/hosts
```

---

## Step 5: Test TLS

```bash
# HTTPS — works, -k skips certificate trust check (self-signed is not in OS trust store)
curl -k https://api.idcube.local/orders
curl -k https://api.idcube.local/inventory
curl -k https://api.idcube.local/parts

# HTTP → should redirect to HTTPS (308)
curl -v http://api.idcube.local/orders 2>&1 | grep "< HTTP\|Location"
# < HTTP/1.1 308 Permanent Redirect
# < Location: https://api.idcube.local/orders
```

In a browser: navigate to `https://api.idcube.local/orders` → you'll see a certificate warning (expected for self-signed). Click "Advanced → Proceed" to continue.

---

## Step 6: Verify NGINX Loaded the Certificate

```bash
# Check controller logs for the certificate being loaded
kubectl logs -n ingress-nginx \
  -l app.kubernetes.io/name=ingress-nginx \
  --tail=20 | grep -i "tls\|ssl\|cert"

# Inspect the certificate NGINX is serving
echo | openssl s_client \
  -connect $INGRESS_IP:443 \
  -servername api.idcube.local 2>/dev/null | openssl x509 -text -noout | grep -E "Subject:|Not After"
```

---

## Rotate / Update the Certificate

When the certificate expires or needs to be replaced:

```bash
# Generate new cert
openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
  -keyout tls-new.key -out tls-new.crt \
  -subj "/CN=api.idcube.local/O=idcube"

# Delete old secret and recreate (or patch)
kubectl delete secret idcube-tls
kubectl create secret tls idcube-tls --cert=tls-new.crt --key=tls-new.key

# NGINX picks up the new cert automatically within ~30s — no restart needed
```

---

# APPROACH 2 — cert-manager + Let's Encrypt (Production)

cert-manager is a Kubernetes operator that automates certificate issuance and renewal.

```
cert-manager watches Ingress objects with its annotation
  ↓
Sees cert-manager.io/cluster-issuer annotation
  ↓
Contacts Let's Encrypt ACME API
  ↓
Let's Encrypt sends an HTTP-01 challenge (hits /.well-known/acme-challenge/<token> on your domain)
  ↓
cert-manager responds to the challenge via a temporary pod
  ↓
Let's Encrypt validates domain ownership → issues certificate
  ↓
cert-manager stores it as a Kubernetes TLS Secret
  ↓
NGINX Ingress picks up the secret → HTTPS works
  ↓
cert-manager renews automatically before expiry (Let's Encrypt certs expire in 90 days)
```

---

## Step 1: Install cert-manager

```bash
helm repo add jetstack https://charts.jetstack.io
helm repo update

helm install cert-manager jetstack/cert-manager \
  --namespace cert-manager \
  --create-namespace \
  --set installCRDs=true
```

Verify all cert-manager pods are running:

```bash
kubectl get pods -n cert-manager
```

```
NAME                                       READY   STATUS
cert-manager-xxxxxxxxx-xxxxx              1/1     Running
cert-manager-cainjector-xxxxxxxxx-xxxxx   1/1     Running
cert-manager-webhook-xxxxxxxxx-xxxxx      1/1     Running
```

---

## Step 2: Create ClusterIssuers

The manifest is at [cert-manager-issuer.yaml](cert-manager-issuer.yaml).

```bash
# Edit the file first — replace <your-email> with a real email
kubectl apply -f cert-manager-issuer.yaml

# Verify both issuers are Ready
kubectl get clusterissuer
```

```
NAME                  READY   AGE
letsencrypt-prod      True    30s
letsencrypt-staging   True    30s
```

**Staging vs Production:**

| Issuer | Rate Limits | Certificate Trusted? | Use For |
|---|---|---|---|
| `letsencrypt-staging` | Very high | No (staging CA) | Testing the flow |
| `letsencrypt-prod` | 5 certs/domain/week | Yes (trusted by all browsers) | Production |

**Always test with staging first.** If something goes wrong you won't burn your production rate limit.

---

## Step 3: Point DNS to the Ingress IP

Let's Encrypt cannot issue a certificate for a `.local` hostname. You need a **real domain** with a DNS A record pointing to your NGINX LoadBalancer IP.

```
A record:   api.idcube.com  →  20.219.xx.xx  (your INGRESS_IP)
```

Set this in your DNS provider (Azure DNS, Cloudflare, Route53, etc.).

Verify DNS propagation:

```bash
nslookup api.idcube.com
# Should resolve to your INGRESS_IP
```

---

## Step 4: Apply the cert-manager Ingress

The manifest is at [cert-manager-tls-ingress.yaml](cert-manager-tls-ingress.yaml).

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: part-order-ingress-certmanager
  annotations:
    kubernetes.io/ingress.class: "nginx"
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
    cert-manager.io/cluster-issuer: "letsencrypt-prod"
spec:
  tls:
    - hosts:
        - api.idcube.com
      secretName: idcube-letsencrypt-tls
  rules:
    - host: api.idcube.com
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

```bash
kubectl apply -f cert-manager-tls-ingress.yaml
```

---

## Step 5: Watch Certificate Issuance

```bash
# Watch cert-manager create a Certificate object
kubectl get certificate -w
```

```
NAME                     READY   SECRET                    AGE
idcube-letsencrypt-tls   False   idcube-letsencrypt-tls    10s
idcube-letsencrypt-tls   True    idcube-letsencrypt-tls    45s   ← issued
```

If it stays `False`, investigate:

```bash
# Check the Certificate object for events
kubectl describe certificate idcube-letsencrypt-tls

# Check the CertificateRequest
kubectl get certificaterequest

# Check cert-manager controller logs
kubectl logs -n cert-manager \
  -l app=cert-manager \
  --tail=50
```

---

## Step 6: Test with Real Certificate

```bash
# No -k needed — Let's Encrypt cert is trusted by all OS/browsers
curl https://api.idcube.com/orders
curl https://api.idcube.com/inventory

# Inspect the certificate
echo | openssl s_client \
  -connect api.idcube.com:443 2>/dev/null | openssl x509 -text -noout | \
  grep -E "Issuer:|Subject:|Not After"
# Issuer: C=US, O=Let's Encrypt, CN=R11
# Subject: CN=api.idcube.com
# Not After: Sep 08 00:00:00 2026 GMT  ← 90 days, cert-manager renews at 60 days
```

---

## How cert-manager Renewal Works

Let's Encrypt certificates are valid for **90 days**. cert-manager renews them automatically at **60 days** (30 days before expiry). No manual action needed.

```bash
# Check the certificate expiry and renewal time
kubectl describe certificate idcube-letsencrypt-tls | grep -A5 "Renewal Time\|Not After"
```

---

# Debugging TLS Issues

---

## Certificate Not Issued (stays False)

```bash
kubectl describe certificate <name>
kubectl describe certificaterequest <name>
kubectl describe order <name>     # ACME order object
kubectl describe challenge <name> # HTTP-01 challenge object
```

Common causes:

| Symptom | Cause | Fix |
|---|---|---|
| `dial tcp: lookup api.idcube.com: no such host` | DNS not set up | Add A record in DNS provider |
| `http: server gave HTTP response to HTTPS client` | Port 80 not reachable | Check firewall/NSG allows port 80 |
| `too many certificates already issued` | Hit production rate limit | Use staging issuer to test |
| Challenge pod errors | cert-manager can't create temp pod | Check RBAC, network policies |

---

## Wrong Certificate Served (cert mismatch)

```bash
# Check which secret NGINX is using
kubectl describe ingress <name> | grep "tls"

# Check the secret exists and has the right hostname
kubectl get secret <secret-name> -o jsonpath='{.data.tls\.crt}' | \
  base64 -d | openssl x509 -noout -text | grep "Subject Alternative Name" -A2
```

---

## Browser Shows "Not Secure" After Cert-Manager Issues Cert

NGINX caches the old (or empty) certificate for a short time. Wait 30-60 seconds, then hard-refresh the browser. If still wrong:

```bash
kubectl rollout restart deployment -n ingress-nginx
```

---

## 308 Redirect Loop

If `ssl-redirect` is set but traffic reaches NGINX over HTTP (e.g. behind an Azure Application Gateway that terminates TLS before NGINX), NGINX sees HTTP and redirects again:

```bash
# Tell NGINX to trust the X-Forwarded-Proto header from the upstream proxy
# Add to ingress annotations:
nginx.ingress.kubernetes.io/configuration-snippet: |
  if ($http_x_forwarded_proto = "https") {
    set $ssl_redirect 0;
  }
```

Or use `use-forwarded-headers: "true"` in the NGINX ConfigMap.

---

# Files in This Directory

| File | Purpose |
|---|---|
| [tls-ingress.yaml](tls-ingress.yaml) | Ingress with self-signed TLS secret (`idcube-tls`) |
| [cert-manager-issuer.yaml](cert-manager-issuer.yaml) | Let's Encrypt ClusterIssuers (staging + prod) |
| [cert-manager-tls-ingress.yaml](cert-manager-tls-ingress.yaml) | Ingress using cert-manager annotation for automatic cert |

---

# Quick Reference

```bash
# Generate self-signed cert
openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
  -keyout tls.key -out tls.crt -subj "/CN=api.idcube.local/O=idcube"

# Create TLS secret
kubectl create secret tls idcube-tls --cert=tls.crt --key=tls.key

# Apply TLS ingress (self-signed)
kubectl apply -f tls-ingress.yaml

# Test HTTPS
curl -k https://api.idcube.local/orders

# Install cert-manager (production path)
helm install cert-manager jetstack/cert-manager \
  --namespace cert-manager --create-namespace --set installCRDs=true

# Apply issuers
kubectl apply -f cert-manager-issuer.yaml

# Apply cert-manager ingress
kubectl apply -f cert-manager-tls-ingress.yaml

# Watch cert issuance
kubectl get certificate -w

# Inspect served cert
echo | openssl s_client -connect api.idcube.local:443 \
  -servername api.idcube.local 2>/dev/null | openssl x509 -noout -text | grep "Subject\|Not After"
```
