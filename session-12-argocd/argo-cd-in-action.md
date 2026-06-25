## Using Argo CD Step-by-Step

Below is a **practical step-by-step flow** that fits perfectly with the pipeline you already built:

* Jenkins builds image
* pushes to DockerHub
* ArgoCD deploys to **Kubernetes**

---

# 1. Install ArgoCD CLI

The ArgoCD CLI lets you manage apps from the terminal and integrate with CI/CD pipelines.

## On Mac

**Using Homebrew (recommended):**

```bash
brew install argocd
```

**Manual download (Intel):**

```bash
curl -sSL -o argocd-darwin-amd64 \
  https://github.com/argoproj/argo-cd/releases/latest/download/argocd-darwin-amd64

chmod +x argocd-darwin-amd64
sudo mv argocd-darwin-amd64 /usr/local/bin/argocd
```

**Manual download (Apple Silicon M1/M2/M3):**

```bash
curl -sSL -o argocd-darwin-arm64 \
  https://github.com/argoproj/argo-cd/releases/latest/download/argocd-darwin-arm64

chmod +x argocd-darwin-arm64
sudo mv argocd-darwin-arm64 /usr/local/bin/argocd
```

## On Linux

```bash
curl -sSL -o argocd-linux-amd64 \
  https://github.com/argoproj/argo-cd/releases/latest/download/argocd-linux-amd64

chmod +x argocd-linux-amd64
sudo mv argocd-linux-amd64 /usr/local/bin/argocd
```

## Verify installation

```bash
argocd version
```

## Why install the ArgoCD CLI?

The ArgoCD UI is sufficient for browsing, but the CLI unlocks things the UI cannot do easily:

| Use case | Without CLI | With CLI |
| --- | --- | --- |
| Trigger sync from Jenkins/GitHub Actions | Not possible | `argocd app sync <app>` in pipeline |
| Script bulk operations across many apps | Manual clicks | Loop through `argocd app list` |
| Wait for sync to finish in CI | Not possible | `argocd app wait <app> --sync` |
| Rollback to a previous version | Tricky via UI | `argocd app rollback <app> <revision>` |
| Create/delete apps programmatically | Not possible | `argocd app create` / `argocd app delete` |
| Debug sync errors in terminal | Requires browser | `argocd app get <app>` |

## Common CLI commands

Login to your ArgoCD server:

```bash
argocd login localhost:8080 --username admin --password <password> --insecure
```

List apps:

```bash
argocd app list
```

Sync an app manually:

```bash
argocd app sync inventory-service-dev
```

Check app status:

```bash
argocd app get inventory-service-dev
```

---

# 2. Install ArgoCD in AKS

First create a namespace.

```bash
kubectl create namespace argocd
```

Install ArgoCD using the official manifest.

```bash
kubectl apply -n argocd \
-f https://raw.githubusercontent.com/argoproj/argo-cd/stable/manifests/install.yaml
```

Check pods:

```bash
kubectl get pods -n argocd
```

You should see components like:

```
argocd-server
argocd-repo-server
argocd-application-controller
```

---

# 2. Access ArgoCD UI

Expose the service.

```bash
kubectl port-forward svc/argocd-server -n argocd 8080:443
```

Open browser:

```
http://localhost:8080
```

---

# 3. Get ArgoCD Admin Password

Retrieve initial password.

```bash
kubectl get secret argocd-initial-admin-secret \
-n argocd \
-o jsonpath="{.data.password}" | base64 --decode
```

Login:

```
username: admin
password: <decoded password>
```

You now see the **ArgoCD dashboard**.

---

# 4. GitOps Repository Structure

The GitOps repo (`gitops-repo-argocd-basic`) uses plain Kubernetes manifests with the **App of Apps** pattern:

```
gitops-repo-argocd-basic/
├── manifests/
│   ├── inventory/
│   │   ├── deployment.yaml
│   │   └── service.yaml
│   └── order/
│       ├── deployment.yaml
│       └── service.yaml
├── apps/
│   ├── inventory-app.yaml
│   └── order-app.yaml
└── root-app.yaml
```

`manifests/inventory/deployment.yaml`:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: part-inventory-service
  namespace: dev
  labels:
    app: part-inventory-service
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
          image: ram1uj/part-inventory-service:latest
          imagePullPolicy: Always
          ports:
            - containerPort: 8080
          env:
            - name: SPRING_PROFILES_ACTIVE
              value: "dev"
            - name: MYSQL_HOST
              value: "mysql"
            - name: MYSQL_PORT
              value: "3306"
            - name: MYSQL_DATABASE
              value: "part_inventory_db"
            - name: MYSQL_USER
              value: "root"
            - name: MYSQL_PASSWORD
              value: "password"
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
          resources:
            requests:
              cpu: "400m"
              memory: "256Mi"
            limits:
              cpu: "600m"
              memory: "512Mi"
```

`manifests/inventory/service.yaml`:

```yaml
apiVersion: v1
kind: Service
metadata:
  name: part-inventory-service
  namespace: dev
  labels:
    app: part-inventory-service
spec:
  selector:
    app: part-inventory-service
  type: ClusterIP
  ports:
    - port: 80
      targetPort: 8080
```

Push this repo to GitHub.

---

# 5. Create ArgoCD Applications (App of Apps Pattern)

The **App of Apps** pattern lets a single root Application manage all child Applications in Git.

**Child Application — `apps/inventory-app.yaml`:**

```yaml
apiVersion: argoproj.io/v1alpha1
kind: Application
metadata:
  name: inventory-service-dev
  namespace: argocd
spec:
  project: default
  source:
    repoURL: https://github.com/ramanujds/gitops-repo-argocd-basic
    targetRevision: HEAD
    path: manifests/inventory
  destination:
    server: https://kubernetes.default.svc
    namespace: dev
  syncPolicy:
    automated:
      prune: true
      selfHeal: true
    syncOptions:
      - CreateNamespace=true
```

**Child Application — `apps/order-app.yaml`:**

```yaml
apiVersion: argoproj.io/v1alpha1
kind: Application
metadata:
  name: order-service-dev
  namespace: argocd
spec:
  project: default
  source:
    repoURL: https://github.com/ramanujds/gitops-repo-argocd-basic
    targetRevision: HEAD
    path: manifests/order
  destination:
    server: https://kubernetes.default.svc
    namespace: dev
  syncPolicy:
    automated:
      prune: true
      selfHeal: true
    syncOptions:
      - CreateNamespace=true
```

**Root Application — `root-app.yaml`:**

```yaml
apiVersion: argoproj.io/v1alpha1
kind: Application
metadata:
  name: root-app
  namespace: argocd
spec:
  project: default
  source:
    repoURL: https://github.com/ramanujds/gitops-repo-argocd-basic
    targetRevision: HEAD
    path: apps
  destination:
    server: https://kubernetes.default.svc
    namespace: argocd
  syncPolicy:
    automated:
      prune: true
      selfHeal: true
```

Bootstrap the entire setup by applying only the root app:

```bash
kubectl apply -f root-app.yaml
```

ArgoCD detects the `apps/` folder, creates both child Applications, and syncs all manifests automatically.

---

# 6. Deploy Application

Once the root app is applied:

```
root-app (watches apps/)
   ↓
inventory-service-dev  →  manifests/inventory/
order-service-dev      →  manifests/order/
```

ArgoCD applies to the `dev` namespace:

```
manifests/inventory/deployment.yaml
manifests/inventory/service.yaml
manifests/order/deployment.yaml
manifests/order/service.yaml
```

Verify:

```bash
kubectl get pods -n dev
kubectl get svc -n dev
```

Both services should be running in the `dev` namespace.

---

# 7. Integrate with Jenkins Pipeline

Your Jenkins pipeline already pushes images.

Now modify pipeline to **update the image tag in Git** instead of running `kubectl`.

Example stage:

```groovy
stage('Update GitOps Repo') {
    steps {
        sh '''
        git clone https://github.com/ramanujds/gitops-repo-argocd-basic
        cd gitops-repo-argocd-basic/manifests/inventory

        sed -i "s|image: ram1uj/part-inventory-service:.*|image: ram1uj/part-inventory-service:${BUILD_NUMBER}|" deployment.yaml

        git commit -am "Deploy inventory version ${BUILD_NUMBER}"
        git push
        '''
    }
}
```

Pipeline becomes:

```
Jenkins
   ↓
Build Docker Image
   ↓
Push Image
   ↓
Update manifest in Git
   ↓
ArgoCD detects change
   ↓
Deployment updated
```

---

# 8. Enable Auto Sync

In the Application YAML we used:

```yaml
syncPolicy:
  automated:
    prune: true
    selfHeal: true
```

This enables:

| Feature     | What it does                   |
|-------------|--------------------------------|
| Auto deploy | Git change triggers deployment |
| Self heal   | fixes manual cluster changes   |
| Prune       | deletes removed resources      |

---

# 9. Verify Deployment

Check pods in the `dev` namespace:

```bash
kubectl get pods -n dev
```

Check services:

```bash
kubectl get svc -n dev
```

Expected output:

```
NAME                      TYPE        CLUSTER-IP     PORT(S)   AGE
part-inventory-service    ClusterIP   10.0.x.x       80/TCP    ...
part-order-service        ClusterIP   10.0.x.x       80/TCP    ...
```

The order service connects to inventory via `http://part-inventory-service` within the cluster.

---

# 10. Final Architecture

Your system now becomes:

```
Terraform
   ↓
Creates AKS

Jenkins
   ↓
Build Docker Image
   ↓
Push Image

GitOps Repo
   ↓
Kubernetes manifests

ArgoCD
   ↓
Deploys to AKS
```

This is the **modern Kubernetes CI/CD architecture** used in most companies.

---
