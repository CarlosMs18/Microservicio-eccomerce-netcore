echo " Deploying applications to development environment..."
kubectl apply -k k8s/overlays/development/

echo " Deploying monitoring to development environment..."
kubectl apply -k k8s/overlays/development/monitoring/

echo " Development deployment completed!"