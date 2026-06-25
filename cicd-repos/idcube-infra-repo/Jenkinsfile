pipeline {

	agent any

	tools {
		terraform 'terraform'
	}

	environment {
		RESOURCE_GROUP = "idcube-aks"
		CLUSTER_NAME = "idcube-cluster"
		GITOPS_REPO_URL = "https://github.com/ramanujds/gitops-repo-idcube"
	}

	stages {

		stage('Checkout Infrastructure Code') {
			steps {
				echo 'Checking out infrastructure code from GitHub'
				git branch: 'main', url: 'https://github.com/ramanujds/idcube-infra-repo'
			}

		}

		stage('Check Azure CLI') {
			steps {
				echo 'Checking Azure CLI installation'
				sh 'export PATH=$PATH:/usr/local/bin:/opt/homebrew/bin && az --version'
			}
		}

		stage('Terraform Init') {
			steps {
				echo 'Initializing Terraform'
				dir('terraform') {
					sh 'export PATH=$PATH:/usr/local/bin:/opt/homebrew/bin && terraform init'
				}
			}
		}

		stage('Terraform Validate') {
			steps {
				echo 'Validating Terraform configuration'

				dir('terraform') {
					sh 'export PATH=$PATH:/usr/local/bin:/opt/homebrew/bin && terraform validate'
				}
			}
		}

		stage('Terraform Plan') {
			steps {
				echo 'Planning Terraform deployment'

				dir('terraform') {
					sh 'export PATH=$PATH:/usr/local/bin:/opt/homebrew/bin && terraform plan -out=tfplan'
				}
			}
		}

		stage('Terraform Apply') {
			steps {
				echo 'Applying Terraform deployment'

				dir('terraform') {
					sh 'export PATH=$PATH:/usr/local/bin:/opt/homebrew/bin && terraform apply -auto-approve tfplan'
				}
			}
		}


		stage('Connect to AKS Cluster') {
			steps {
				echo 'Connecting to AKS cluster'
				sh '''
				export PATH=$PATH:/usr/local/bin:/opt/homebrew/bin
				az aks get-credentials --resource-group ${RESOURCE_GROUP} --name ${CLUSTER_NAME} --overwrite-existing
				'''
			}
		}

		

	}

}