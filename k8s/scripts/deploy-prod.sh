echo " Deploying applications to production environment..."
kubectl apply -k k8s/overlays/production/

echo " Deploying monitoring to production environment..."
kubectl apply -k k8s/overlays/production/monitoring/

echo " Production deployment completed!"