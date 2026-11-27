#!/usr/bin/env bash
#
# Provisiona o Recreio Musical no Azure App Service (Linux, .NET 10, plano B1)
# e cria a identidade federada usada pelo GitHub Actions (OIDC).
#
# Uso (Azure Cloud Shell ou máquina com Azure CLI autenticada):
#   bash scripts/azure-provision.sh
#
# Variáveis opcionais:
#   RG=rg-recreio-musical LOCATION=brazilsouth PLAN=plan-recreio-musical \
#   APP=recreio-musical-obj REPO=fnm0303/RecreioMusicalObj BRANCH=main \
#   bash scripts/azure-provision.sh
#
set -euo pipefail

RG="${RG:-rg-recreio-musical}"
LOCATION="${LOCATION:-brazilsouth}"
PLAN="${PLAN:-plan-recreio-musical}"
APP="${APP:-recreio-musical-obj}"
REPO="${REPO:-fnm0303/RecreioMusicalObj}"
BRANCH="${BRANCH:-main}"
GITHUB_APP_NAME="${GITHUB_APP_NAME:-recreio-musical-github}"

info() { printf '\n==> %s\n' "$*"; }
fail() { printf 'ERRO: %s\n' "$*" >&2; exit 1; }

command -v az >/dev/null 2>&1 || fail "Azure CLI não encontrado. Execute este script no Azure Cloud Shell."
az account show >/dev/null 2>&1 || fail "Faça 'az login' antes de executar este script."

info "Assinatura em uso"
az account show --query '{Nome:name, Assinatura:id, Tenant:tenantId}' -o table

if ! az webapp list-runtimes --os linux 2>/dev/null | grep -q 'DOTNETCORE|10.0'; then
    fail "O runtime .NET 10 não está disponível nesta nuvem/região. Tente LOCATION=eastus2."
fi

if [[ -z "${SPOTIFY_CLIENT_ID:-}" ]]; then
    read -r -p "Spotify Client ID: " SPOTIFY_CLIENT_ID
fi
if [[ -z "${SPOTIFY_CLIENT_SECRET:-}" ]]; then
    read -r -s -p "Spotify Client Secret: " SPOTIFY_CLIENT_SECRET
    printf '\n'
fi
if [[ -z "${ADMIN_PIN:-}" ]]; then
    read -r -s -p "PIN administrativo (6 a 12 dígitos): " ADMIN_PIN
    printf '\n'
fi

[[ -n "$SPOTIFY_CLIENT_ID" ]] || fail "Spotify Client ID não informado."
[[ -n "$SPOTIFY_CLIENT_SECRET" ]] || fail "Spotify Client Secret não informado."
[[ "$ADMIN_PIN" =~ ^[0-9]{6,12}$ ]] || fail "O PIN deve ter de 6 a 12 dígitos."

info "Resource group $RG ($LOCATION)"
az group create --name "$RG" --location "$LOCATION" --output none

if az appservice plan show --name "$PLAN" --resource-group "$RG" --output none 2>/dev/null; then
    info "App Service plan $PLAN já existe"
else
    info "Criando App Service plan Linux B1"
    az appservice plan create \
        --name "$PLAN" \
        --resource-group "$RG" \
        --location "$LOCATION" \
        --is-linux \
        --sku B1 \
        --output none
fi

if az webapp show --name "$APP" --resource-group "$RG" --output none 2>/dev/null; then
    info "Web App $APP já existe; atualizando a configuração"
else
    info "Criando Web App $APP"
    az webapp create \
        --name "$APP" \
        --resource-group "$RG" \
        --plan "$PLAN" \
        --runtime "DOTNETCORE|10.0" \
        --output none
fi

info "Aplicando configurações de produção"
az webapp config set \
    --name "$APP" \
    --resource-group "$RG" \
    --linux-fx-version "DOTNETCORE|10.0" \
    --always-on true \
    --ftps-state Disabled \
    --min-tls-version 1.2 \
    --http20-enabled true \
    --output none

az webapp update --name "$APP" --resource-group "$RG" --https-only true --output none

az webapp config set \
    --name "$APP" \
    --resource-group "$RG" \
    --generic-configurations '{"healthCheckPath": "/health"}' \
    --output none

az webapp config appsettings set \
    --name "$APP" \
    --resource-group "$RG" \
    --settings \
        ASPNETCORE_ENVIRONMENT=Production \
        APP_SVC_RUN_FROM_COPY=true \
        TZ=America/Sao_Paulo \
        Spotify__Market=BR \
        "ConnectionStrings__Playlist=Data Source=/home/data/recreio-musical.db" \
        "Spotify__ClientId=$SPOTIFY_CLIENT_ID" \
        "Spotify__ClientSecret=$SPOTIFY_CLIENT_SECRET" \
        "Admin__Pin=$ADMIN_PIN" \
    --output none

az webapp log config \
    --name "$APP" \
    --resource-group "$RG" \
    --application-logging filesystem \
    --level information \
    --output none

info "Criando identidade federada do GitHub Actions (OIDC)"
APP_ID="$(az ad app list --display-name "$GITHUB_APP_NAME" --query '[0].appId' -o tsv)"
if [[ -z "$APP_ID" ]]; then
    APP_ID="$(az ad app create --display-name "$GITHUB_APP_NAME" --query appId -o tsv)"
fi

az ad sp create --id "$APP_ID" --output none >/dev/null 2>&1 || true
SP_OBJECT_ID="$(az ad sp show --id "$APP_ID" --query id -o tsv)"

SUBJECT="repo:${REPO}:ref:refs/heads/${BRANCH}"
if ! az ad app federated-credential list --id "$APP_ID" --query "[?subject=='$SUBJECT'].name" -o tsv | grep -q .; then
    FEDERATED_FILE="$(mktemp)"
    cat > "$FEDERATED_FILE" <<EOF
{
  "name": "github-${BRANCH}",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "$SUBJECT",
  "audiences": ["api://AzureADTokenExchange"]
}
EOF
    az ad app federated-credential create \
        --id "$APP_ID" \
        --parameters @"$FEDERATED_FILE" \
        --output none
    rm -f "$FEDERATED_FILE"
fi

WEBAPP_ID="$(az webapp show --name "$APP" --resource-group "$RG" --query id -o tsv)"
if ! az role assignment list --assignee "$SP_OBJECT_ID" --scope "$WEBAPP_ID" --query '[].id' -o tsv | grep -q .; then
    az role assignment create \
        --assignee-object-id "$SP_OBJECT_ID" \
        --assignee-principal-type ServicePrincipal \
        --role "Website Contributor" \
        --scope "$WEBAPP_ID" \
        --output none
fi

SUB_ID="$(az account show --query id -o tsv)"
TENANT_ID="$(az account show --query tenantId -o tsv)"
URL="https://${APP}.azurewebsites.net"

if command -v gh >/dev/null 2>&1 && gh auth status >/dev/null 2>&1; then
    info "Gravando secrets e variable no GitHub ($REPO)"
    gh secret set AZURE_CLIENT_ID --repo "$REPO" --body "$APP_ID"
    gh secret set AZURE_TENANT_ID --repo "$REPO" --body "$TENANT_ID"
    gh secret set AZURE_SUBSCRIPTION_ID --repo "$REPO" --body "$SUB_ID"
    gh variable set AZURE_WEBAPP_NAME --repo "$REPO" --body "$APP"

    info "Disparando o workflow de deploy"
    if ! gh workflow run azure-deploy.yml --repo "$REPO" --ref "$BRANCH"; then
        printf 'Aviso: não foi possível disparar o workflow; rode manualmente em Actions.\n' >&2
    fi
else
    info "Cadastre manualmente no GitHub (Settings > Secrets and variables > Actions)"
    cat <<EOF
Secrets:
  AZURE_CLIENT_ID=$APP_ID
  AZURE_TENANT_ID=$TENANT_ID
  AZURE_SUBSCRIPTION_ID=$SUB_ID
Variable:
  AZURE_WEBAPP_NAME=$APP
EOF
fi

cat <<EOF

==> Provisionamento concluído

URL: $URL
Saúde: $URL/health

Próximos passos:
  1. O workflow .github/workflows/azure-deploy.yml já está na branch '$BRANCH'.
     Se o deploy não iniciar sozinho, rode Actions > Deploy to Azure App Service
     > Run workflow (ou: gh workflow run azure-deploy.yml --ref $BRANCH).
  2. Acompanhe em GitHub > Actions e em:
     az webapp log tail --name $APP --resource-group $RG
  3. Depois do primeiro deploy, confira $URL/health e abra a página em dois celulares.

Banco: /home/data/recreio-musical.db (backups diários em /home/data/backups)
Para trocar o PIN depois: az webapp config appsettings set -g $RG -n $APP --settings Admin__Pin=NOVO_PIN
EOF
