echo "Configuring ArgoCD application..."
kubectl apply -f k8s/argocd/production-app.yaml
echo "ArgoCD application configured!"