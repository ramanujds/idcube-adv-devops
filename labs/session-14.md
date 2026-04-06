# SESSION 14 — DevSecOps (Security Gating + SBOM + VAPT Readiness) 

---

## Session Goal

* Integrate security into CI/CD pipeline
* Enforce quality gates and fail builds when needed
* Generate and manage SBOM
* Prepare pipelines for VAPT and compliance

---

# Final Case Study

> Your system is fully automated with CI/CD and GitOps.
>
> New requirement from client/security team:
>
> * Ensure code quality standards
> * No secrets in code
> * Provide SBOM for each release
> * Pipeline must be VAPT-ready
>
> Current issues:
>
> * Code with vulnerabilities getting deployed
> * Secrets exposed in repo
> * No visibility into dependencies
>
> Your task:
> Implement security gates across pipeline

---

# LAB 1 — SonarQube Setup & Integration

---

## Setup

* SonarQube running (Docker or VM)

---

## Pipeline Integration

Add stage:

```groovy id="8l9a7n"
stage('SonarQube Analysis') {
    steps {
        sh 'mvn sonar:sonar'
    }
}
```

---

## Tasks

* Run analysis
* View report in SonarQube UI

---

---

# LAB 2 — Quality Gates Enforcement

---

## Configure

* Set:

    * Code coverage threshold
    * Bug/vulnerability limits

---

## Pipeline Update

* Fail pipeline if quality gate fails

---

## Tasks

* Introduce code issue
* Run pipeline

---

## Observe

* Pipeline fails

---

---

# LAB 3 — Secret Exposure Scenario

---

## Inject Issue

* Add hardcoded secret in code:

```id="gph5bo"
password = "admin123"
```

---

## Tasks

* Run scan (Sonar or external tool)
* Detect exposed secret

---

## Fix

* Remove secret
* Use environment variables / secrets

---

---

# LAB 4 — Kubernetes Secret Best Practices

---

## Create Secret

```bash id="z3x2rf"
kubectl create secret generic db-secret \
  --from-literal=password=securepass
```

---

## Use in Deployment

```yaml id="w0r6c9"
env:
- name: DB_PASSWORD
  valueFrom:
    secretKeyRef:
      name: db-secret
      key: password
```

---

## Tasks

* Deploy and verify

---

---

# LAB 5 — SBOM Generation

---

## Tool

Use `syft`

---

## Command

```bash id="v2r7eq"
syft <image-name> -o spdx-json > sbom.json
```

---

## Tasks

* Generate SBOM for order-service
* Inspect dependencies

---

---

# LAB 6 — SBOM Formats

---

## Formats

* SPDX
* CycloneDX

---

## Tasks

* Generate both formats
* Compare outputs

---

---

# LAB 7 — SBOM in Pipeline

---

## Add Stage

```groovy id="q1o7va"
stage('Generate SBOM') {
    steps {
        sh 'syft <image> -o spdx-json > sbom.json'
    }
}
```

---

## Tasks

* Store SBOM as artifact

---

---

# LAB 8 — Vulnerability Awareness (Basic)

---

## Tool

Use `grype`

---

## Command

```bash id="r9f2l8"
grype <image-name>
```

---

## Tasks

* Scan image
* Identify vulnerabilities

---

---

# LAB 9 — VAPT-Ready Pipeline Design

---

## Required Stages

* Code scan (SonarQube)
* Dependency scan
* Image scan
* Secret scan
* SBOM generation

---

## Tasks

* Review pipeline
* Ensure all stages included

---

---

# LAB 10 — Pipeline Failure Scenario

---

## Inject Issues

* Vulnerable dependency
* Code issue
* Secret exposure

---

## Tasks

* Run pipeline
* Identify failure stage
* Fix issues

---

---

# LAB 11 — Combined End-to-End Secure Flow

---

## Flow

```id="o8z2d1"
Code → Jenkins → SonarQube → Build → SBOM → Scan → ACR → ArgoCD → Deploy
```

---

## Tasks

* Execute full secure pipeline
* Validate all gates

---

---

# Real Case Scenario

---

## Issue

* Production deployment rejected by client
* Missing SBOM
* Security vulnerabilities found

---

## Investigation

* Check pipeline stages
* Verify scan results
* Review SBOM

---

## Root Causes

* Missing security gates
* No dependency visibility
* Secrets exposed

---

## Resolution

* Add security stages
* Generate SBOM
* Enforce quality gates

---

---

# Validation Tasks

---

* Pipeline fails on vulnerabilities
* SBOM generated for each build
* No secrets in code
* Secure deployment achieved

---

---


