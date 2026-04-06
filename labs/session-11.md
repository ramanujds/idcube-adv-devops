# SESSION 11 — CI Pipeline with Jenkins 

---

## Session Goal

* Build a real CI pipeline using Jenkins
* Automate build → Docker → push → deploy
* Design multi-stage and multi-environment pipelines

---

# Continuing Case Study

> Your application is deployed and exposed.
>
> Current problem:
>
> * Developers build and deploy manually
> * No consistency across environments
> * Frequent human errors
>
> Your task:
> Automate the entire build and deployment pipeline

---

# LAB 1 — Setup Jenkins

---

## Options

* Docker-based Jenkins
* VM-based Jenkins

---

## Verify

* Access Jenkins UI
* Install required plugins:

    * Git
    * Pipeline
    * Docker

---

---

# LAB 2 — Connect Git Repository

---

## Setup

* Repo contains:

    * order-service
    * inventory-service

---

## Tasks

* Create Jenkins job
* Configure Git integration

---

---

# LAB 3 — Basic Pipeline (Build Only)

---

## Jenkinsfile

```groovy id="q0sm1a"
pipeline {
    agent any

    stages {
        stage('Build') {
            steps {
                sh 'mvn clean package'
            }
        }
    }
}
```

---

## Tasks

* Run pipeline
* Verify build success

---

---

# LAB 4 — Add Docker Build Stage

---

## Update Pipeline

```groovy id="db0npo"
stage('Docker Build') {
    steps {
        sh 'docker build -t order-service:v1 .'
    }
}
```

---

## Tasks

* Build Docker image from pipeline

---

---

# LAB 5 — Push to ACR

---

## Add Stage

```groovy id="69ytd0"
stage('Push Image') {
    steps {
        sh 'docker tag order-service:v1 <acr>/order-service:v1'
        sh 'docker push <acr>/order-service:v1'
    }
}
```

---

## Tasks

* Push image to registry

---

---

# LAB 6 — Deploy to Kubernetes

---

## Add Stage

```groovy id="c6s0vv"
stage('Deploy') {
    steps {
        sh 'kubectl apply -f deployment.yaml'
    }
}
```

---

## Tasks

* Deploy updated version
* Verify rollout

---

---

# LAB 7 — Multi-Stage Pipeline (Full Flow)

---

## Final Pipeline Flow

1. Checkout code
2. Build
3. Test (optional)
4. Docker build
5. Push image
6. Deploy

---

## Tasks

* Execute full pipeline
* Validate end-to-end

---

---

# LAB 8 — Multi-Environment Pipeline

---

## Environments

* Dev
* QA
* Prod

---

## Add Parameter

```groovy id="8ef9rs"
parameters {
    choice(name: 'ENV', choices: ['dev', 'qa', 'prod'])
}
```

---

## Tasks

* Deploy to different environments
* Use different configs

---

---

# LAB 9 — Versioning Strategy in Pipeline

---

## Use Build Number

```groovy id="c4r2gg"
env.BUILD_TAG = "build-${BUILD_NUMBER}"
```

---

## Tasks

* Tag images dynamically
* Deploy specific version

---

---

# LAB 10 — Pipeline Failure Scenario

---

## Inject Issue

* Break build (compile error)
* Wrong Dockerfile
* Invalid deployment YAML

---

## Tasks

* Observe failure stage
* Fix issue

---

---

# LAB 11 — Parallel Build (Optional)

---

## Example

```groovy id="m2k4jv"
parallel {
    stage('Build Order Service') {
        steps { sh 'mvn package' }
    }
    stage('Build Inventory Service') {
        steps { sh 'npm install' }
    }
}
```

---

## Tasks

* Run parallel builds

---

---

# LAB 12 — Combined Scenario

---

## Scenario

* Code pushed
* Pipeline runs
* Image built and deployed
* New version available via ingress

---

## Tasks

* Validate full flow
* Test API after deployment

---

---

# Real Case Scenario

---

## Issue

* Deployment failed after pipeline run
* Some stages passed, but app not working

---

## Investigation

* Check Jenkins logs
* Check image tag
* Check deployment YAML

---

## Root Causes

* Wrong image tag
* Deployment not updated
* Pipeline missing step

---

## Resolution

* Fix pipeline stages
* Ensure consistent tagging
* Validate deployment

---

---

# Validation Tasks

---

* Pipeline executes successfully
* Image pushed to registry
* Deployment updated in cluster
* Application accessible

---

---

# Final Architecture After Session

```id="1fsq9a"
Git → Jenkins → ACR → Kubernetes → Ingress → User
```


