#!/usr/bin/env bash
# Validates that required environment variables are set in .env before starting Docker Compose.
# Usage: ./scripts/check-env.sh

set -euo pipefail

ENV_FILE="${1:-.env}"

if [ ! -f "$ENV_FILE" ]; then
  echo "ERROR: $ENV_FILE not found."
  echo "Run: cp .env.example .env"
  echo "Then edit .env and set your passwords."
  exit 1
fi

REQUIRED_VARS=(
  "POSTGRES_PASSWORD"
  "RABBITMQ_DEFAULT_PASS"
  "JWT_SECRET"
)

ERRORS=0

for VAR in "${REQUIRED_VARS[@]}"; do
  VALUE=$(grep "^${VAR}=" "$ENV_FILE" 2>/dev/null | cut -d= -f2-)
  if [ -z "$VALUE" ]; then
    echo "ERROR: $VAR is not set in $ENV_FILE"
    ERRORS=$((ERRORS + 1))
  elif [ "$VALUE" = "changeme" ] || [ "$VALUE" = "changeme-must-be-at-least-32-characters-long" ]; then
    echo "WARNING: $VAR still has the default placeholder value. Set a real value before deploying."
  fi
done

if [ $ERRORS -gt 0 ]; then
  echo ""
  echo "$ERRORS required variable(s) missing. Edit $ENV_FILE and try again."
  exit 1
fi

echo ".env validation passed."
