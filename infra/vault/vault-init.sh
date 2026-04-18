#!/bin/sh
# vault-init.sh
# Initializes all application secrets in HashiCorp Vault (dev mode).
# Runs once at container startup via docker-compose healthcheck entrypoint.

set -e

VAULT_ADDR="${VAULT_ADDR:-http://vault:8200}"
VAULT_TOKEN="${VAULT_TOKEN:-root-token}"

echo "==> Waiting for Vault to be ready..."
until curl -sf "${VAULT_ADDR}/v1/sys/health" > /dev/null; do
  sleep 1
done
echo "==> Vault is ready."

export VAULT_ADDR VAULT_TOKEN

# ── secret/infra — shared infrastructure credentials ─────────────────────────
vault kv put secret/infra \
  postgres-password="admin123" \
  rabbitmq-user="admin" \
  rabbitmq-password="admin123" \
  redis-password="redis123" \
  grafana-admin-password="admin123"

echo "[OK] secret/infra written"

# ── secret/auth — JWT signing key and token config ───────────────────────────
vault kv put secret/auth \
  jwt-secret="K3r1t3r10m-Sup3rS3cr3t-JWT-K3y-2026-$RANDOM$RANDOM" \
  jwt-issuer="kriteriom-api-gateway" \
  jwt-audience="kriteriom-services" \
  jwt-expiry-minutes="60"

echo "[OK] secret/auth written"

# ── secret/credits — Credits service connection strings ──────────────────────
vault kv put secret/credits \
  db-connection="Host=postgres;Port=5432;Database=credits_db;Username=admin;Password=admin123" \
  redis-connection="redis:6379,password=redis123" \
  rabbitmq-host="rabbitmq" \
  rabbitmq-user="admin" \
  rabbitmq-password="admin123"

echo "[OK] secret/credits written"

# ── secret/audit — Audit service connection strings ──────────────────────────
vault kv put secret/audit \
  db-connection="Host=postgres;Port=5432;Database=audit_db;Username=admin;Password=admin123" \
  rabbitmq-host="rabbitmq" \
  rabbitmq-user="admin" \
  rabbitmq-password="admin123"

echo "[OK] secret/audit written"

# ── secret/risk — Risk service config ────────────────────────────────────────
vault kv put secret/risk \
  redis-connection="redis:6379,password=redis123" \
  rabbitmq-host="rabbitmq" \
  rabbitmq-user="admin" \
  rabbitmq-password="admin123" \
  credits-api-url="http://credits-api:5001"

echo "[OK] secret/risk written"

# ── secret/batch — Batch processor config ────────────────────────────────────
vault kv put secret/batch \
  db-connection="Host=postgres;Port=5432;Database=credits_db;Username=admin;Password=admin123" \
  rabbitmq-host="rabbitmq" \
  rabbitmq-user="admin" \
  rabbitmq-password="admin123" \
  credits-api-url="http://credits-api:5001" \
  hangfire-user="admin" \
  hangfire-password="Hangf1r3@2026"

echo "[OK] secret/batch written"

# ── secret/services — internal inter-service API keys ────────────────────────
vault kv put secret/services \
  risk-to-credits-api-key="risk-svc-$(cat /dev/urandom | tr -dc 'a-z0-9' | head -c 16)" \
  batch-to-credits-api-key="batch-svc-$(cat /dev/urandom | tr -dc 'a-z0-9' | head -c 16)" \
  audit-read-api-key="audit-read-$(cat /dev/urandom | tr -dc 'a-z0-9' | head -c 16)"

echo "[OK] secret/services written"

# ── Verify all paths ──────────────────────────────────────────────────────────
echo ""
echo "==> Verifying secrets..."
for path in secret/infra secret/auth secret/credits secret/audit secret/risk secret/batch secret/services; do
  result=$(vault kv get -format=json "$path" 2>/dev/null | grep '"request_id"' | wc -l)
  if [ "$result" -gt 0 ]; then
    echo "    [✓] $path"
  else
    echo "    [✗] $path — MISSING!"
    exit 1
  fi
done

echo ""
echo "==> Vault initialization complete."
echo ""
echo "    Access Vault UI: http://localhost:8200 (Token: root-token)"
echo ""
echo "    Secrets initialized:"
echo "      secret/infra     — PostgreSQL, RabbitMQ, Redis, Grafana passwords"
echo "      secret/auth      — JWT signing key, issuer, audience, expiry"
echo "      secret/credits   — Credits service: DB, Redis, RabbitMQ"
echo "      secret/audit     — Audit service: DB, RabbitMQ"
echo "      secret/risk      — Risk service: Redis, RabbitMQ, Credits API URL"
echo "      secret/batch     — Batch processor: DB, RabbitMQ, Hangfire credentials"
echo "      secret/services  — Inter-service API keys"
