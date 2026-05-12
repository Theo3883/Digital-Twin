#!/usr/bin/env bash
set -euo pipefail

################################################################################
# WebAPI-only Redeploy Script
# Builds and redeploys only the DigitalTwin.WebAPI Container App
################################################################################

# Configuration (override via env vars)
readonly RG="${RG:-unibytes}"
readonly LOC="${LOC:-germanywestcentral}"
readonly ACR="${ACR:-unibytesacr}"
readonly ENV_NAME="${ENV_NAME:-unibytes-env}"
readonly APP_WEBAPI="${APP_WEBAPI:-unibytes-backend}"
readonly APP_PORTAL="${APP_PORTAL:-unibytes-frontend}"
readonly WEBAPI_CPU="${WEBAPI_CPU:-0.5}"
readonly WEBAPI_MEM="${WEBAPI_MEM:-1.0Gi}"
readonly WEBAPI_TARGET_PORT="${WEBAPI_TARGET_PORT:-8080}"
readonly MIN_REPLICAS="${MIN_REPLICAS:-0}"
readonly MAX_REPLICAS="${MAX_REPLICAS:-1}"

# Keep deployment in Europe and auto-fallback if Azure policy blocks a region.
readonly EU_REGION_CANDIDATES_DEFAULT="germanywestcentral,northeurope,westeurope,francecentral,swedencentral,switzerlandnorth,uksouth"
readonly EU_REGION_CANDIDATES="${EU_REGION_CANDIDATES:-$EU_REGION_CANDIDATES_DEFAULT}"

# Aiven / cloud PostgreSQL connection string (REQUIRED)
# Expected .NET/Npgsql format (recommended):
#   Host=...;Port=5432;Database=...;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true
# You may also pass any valid Npgsql connection string supported by your app.
readonly CONNECTION_STRING="${CONNECTION_STRING:-}"

# Application configuration
readonly JWT_ISSUER="${JWT_ISSUER:-DigitalTwin.WebAPI}"
readonly JWT_AUDIENCE="${JWT_AUDIENCE:-DigitalTwin.DoctorPortal}"
readonly JWT_KEY="${JWT_KEY:-}"
readonly GOOGLE_CLIENT_ID="${GOOGLE_CLIENT_ID:-}"
readonly GOOGLE_MOBILE_CLIENT_ID="${GOOGLE_MOBILE_CLIENT_ID:-}"
readonly DOCTOR_REGISTRATION_SECRET="${DOCTOR_REGISTRATION_SECRET:-change-me-registration-secret}"
readonly GEMINI_API_KEY="${GEMINI_API_KEY:-}"

# Colors
readonly GREEN='\033[0;32m'
readonly BLUE='\033[0;34m'
readonly YELLOW='\033[1;33m'
readonly RED='\033[0;31m'
readonly NC='\033[0m'

log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[OK]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1" >&2
}

REGION_IN_USE="$LOC"

region_list() {
    local first="$1"
    printf '%s\n' "$first" ${EU_REGION_CANDIDATES//,/ } | awk 'NF && !seen[$0]++'
}

is_region_policy_error() {
    local err_file="$1"
    grep -q "RequestDisallowedByAzure" "$err_file"
}

check_command() {
    if ! command -v "$1" >/dev/null 2>&1; then
        log_error "Missing required command: $1"
        return 1
    fi
    return 0
}

ensure_prerequisites() {
    log_info "Checking local prerequisites..."
    check_command az || exit 1
    check_command docker || exit 1

    if ! az account show >/dev/null 2>&1; then
        log_error "Azure CLI is not logged in. Run: az login"
        exit 1
    fi

    az extension add --name containerapp --upgrade --allow-preview true >/dev/null 2>&1 || true
    log_success "Prerequisites ready"
}

ensure_resource_group() {
    log_info "Ensuring resource group: $RG"
    if az group show --name "$RG" >/dev/null 2>&1; then
        REGION_IN_USE=$(az group show --name "$RG" --query location -o tsv)
        log_success "Resource group already exists"
        return
    fi

    local created=0
    while IFS= read -r candidate; do
        local err_file
        err_file=$(mktemp)
        if az group create --name "$RG" --location "$candidate" --output none 2>"$err_file"; then
            REGION_IN_USE="$candidate"
            created=1
            rm -f "$err_file"
            log_success "Created resource group in $REGION_IN_USE"
            break
        fi

        if is_region_policy_error "$err_file"; then
            log_warning "Region '$candidate' blocked by policy for resource group, trying next EU region..."
            rm -f "$err_file"
            continue
        fi

        cat "$err_file" >&2
        rm -f "$err_file"
        exit 1
    done < <(region_list "$REGION_IN_USE")

    if [[ $created -ne 1 ]]; then
        log_error "Could not create resource group in any candidate EU region."
        exit 1
    fi
}

ensure_acr() {
    log_info "Ensuring Azure Container Registry: $ACR"
    if az acr show --name "$ACR" --resource-group "$RG" >/dev/null 2>&1; then
        log_success "ACR already exists"
        return
    fi

    local created=0
    while IFS= read -r candidate; do
        local err_file
        err_file=$(mktemp)
        if az acr create \
            --resource-group "$RG" \
            --name "$ACR" \
            --sku Basic \
            --admin-enabled true \
            --location "$candidate" \
            --output none 2>"$err_file"; then
            REGION_IN_USE="$candidate"
            created=1
            rm -f "$err_file"
            log_success "Created ACR in $REGION_IN_USE"
            break
        fi

        if is_region_policy_error "$err_file"; then
            log_warning "Region '$candidate' blocked by policy for ACR, trying next EU region..."
            rm -f "$err_file"
            continue
        fi

        cat "$err_file" >&2
        rm -f "$err_file"
        exit 1
    done < <(region_list "$REGION_IN_USE")

    if [[ $created -ne 1 ]]; then
        log_error "Could not create ACR in any candidate EU region."
        exit 1
    fi
}

ensure_containerapp_env() {
    log_info "Ensuring Container Apps environment: $ENV_NAME"
    if az containerapp env show --name "$ENV_NAME" --resource-group "$RG" >/dev/null 2>&1; then
        log_success "Container Apps environment already exists"
        return
    fi

    local created=0
    while IFS= read -r candidate; do
        local err_file
        err_file=$(mktemp)
        if az containerapp env create \
            --name "$ENV_NAME" \
            --resource-group "$RG" \
            --location "$candidate" \
            --output none 2>"$err_file"; then
            REGION_IN_USE="$candidate"
            created=1
            rm -f "$err_file"
            log_success "Created Container Apps environment in $REGION_IN_USE"
            break
        fi

        if is_region_policy_error "$err_file"; then
            log_warning "Region '$candidate' blocked by policy for Container Apps env, trying next EU region..."
            rm -f "$err_file"
            continue
        fi

        cat "$err_file" >&2
        rm -f "$err_file"
        exit 1
    done < <(region_list "$REGION_IN_USE")

    if [[ $created -ne 1 ]]; then
        log_error "Could not create Container Apps environment in any candidate EU region."
        exit 1
    fi
}

# Validate required configuration
if [[ -z "$CONNECTION_STRING" ]]; then
    log_error "CONNECTION_STRING is required (Aiven PostgreSQL connection string)."
    log_info "Example:"
    log_info "  export CONNECTION_STRING='Host=<aiven-host>;Port=5432;Database=<db>;Username=<user>;Password=<pass>;SSL Mode=Require;Trust Server Certificate=true'"
    exit 1
fi

if [[ -z "$JWT_KEY" ]]; then
    log_error "JWT_KEY is required."
    log_info "Example: export JWT_KEY='your-strong-32-char-secret-or-longer'"
    exit 1
fi

ensure_prerequisites
ensure_resource_group
ensure_acr
ensure_containerapp_env

# Get ACR login server
log_info "Getting ACR login server..."
ACR_LOGIN_SERVER=$(az acr show --name "$ACR" --resource-group "$RG" --query loginServer -o tsv)
log_success "ACR login server: $ACR_LOGIN_SERVER"

# Login to ACR
log_info "Logging in to ACR..."
az acr login --name "$ACR" >/dev/null 2>&1
log_success "Logged in to ACR"

# Setup buildx builder
log_info "Setting up Docker buildx..."
if ! docker buildx inspect digitaltwin-builder >/dev/null 2>&1; then
    docker buildx create --name digitaltwin-builder --use >/dev/null 2>&1
else
    docker buildx use digitaltwin-builder >/dev/null 2>&1
fi
log_success "Buildx ready"

# Build and push image
log_info "Building and pushing WebAPI image..."
full_image="$ACR_LOGIN_SERVER/webapi:latest"

if docker buildx build \
    --platform linux/amd64 \
    --push \
    --tag "$full_image" \
    --file "./API/DigitalTwin.WebAPI/Dockerfile" \
    "./API" \
    >/dev/null 2>&1; then
    log_success "WebAPI image built and pushed"
else
    log_error "Failed to build WebAPI image"
    exit 1
fi

# Resolve doctor portal URL for CORS
log_info "Resolving doctor portal URL for CORS..."
portal_url=$(az containerapp show --name "$APP_PORTAL" --resource-group "$RG" --query properties.configuration.ingress.fqdn -o tsv 2>/dev/null || echo "")
if [[ -z "$portal_url" && -n "${PORTAL_URL:-}" ]]; then
    portal_url="${PORTAL_URL#https://}"
    portal_url="${portal_url%/}"
fi

if [[ -z "$portal_url" ]]; then
    log_warning "Could not resolve doctor portal URL. Using placeholder CORS origin."
    portal_url="doctor-portal.placeholder.azurecontainerapps.io"
fi

# Update secrets
log_info "Updating WebAPI secrets..."
acr_user=$(az acr credential show --name "$ACR" --query username -o tsv)
acr_password=$(az acr credential show --name "$ACR" --query "passwords[0].value" -o tsv)

if az containerapp show --name "$APP_WEBAPI" --resource-group "$RG" >/dev/null 2>&1; then
    az containerapp secret set \
        --name "$APP_WEBAPI" \
        --resource-group "$RG" \
        --secrets \
            "cloud-db-connection=$CONNECTION_STRING" \
            "jwt-key=$JWT_KEY" \
            "doctor-registration-secret=$DOCTOR_REGISTRATION_SECRET" \
            "gemini-api-key=$GEMINI_API_KEY" \
        --output none 2>/dev/null || true

    log_info "Updating WebAPI Container App..."
    az containerapp update \
        --name "$APP_WEBAPI" \
        --resource-group "$RG" \
        --image "$full_image" \
        --set-env-vars \
            "ASPNETCORE_ENVIRONMENT=Production" \
            "ConnectionStrings__CloudDb=secretref:cloud-db-connection" \
            "Jwt__Issuer=$JWT_ISSUER" \
            "Jwt__Audience=$JWT_AUDIENCE" \
            "Jwt__Key=secretref:jwt-key" \
            "Google__ClientId=$GOOGLE_CLIENT_ID" \
            "Google__MobileClientId=$GOOGLE_MOBILE_CLIENT_ID" \
            "Cors__Origins__0=https://$portal_url" \
            "Doctor__RegistrationSecret=secretref:doctor-registration-secret" \
            "GEMINI_API_KEY=secretref:gemini-api-key" \
        --output none 2>/dev/null || {
            log_warning "Update had issues, but may still have applied."
        }
else
    log_info "Creating WebAPI Container App..."
    az containerapp create \
        --name "$APP_WEBAPI" \
        --resource-group "$RG" \
        --environment "$ENV_NAME" \
        --image "$full_image" \
        --ingress external \
        --target-port "$WEBAPI_TARGET_PORT" \
        --cpu "$WEBAPI_CPU" \
        --memory "$WEBAPI_MEM" \
        --min-replicas "$MIN_REPLICAS" \
        --max-replicas "$MAX_REPLICAS" \
        --registry-server "$ACR_LOGIN_SERVER" \
        --registry-username "$acr_user" \
        --registry-password "$acr_password" \
        --secrets \
            "cloud-db-connection=$CONNECTION_STRING" \
            "jwt-key=$JWT_KEY" \
            "doctor-registration-secret=$DOCTOR_REGISTRATION_SECRET" \
            "gemini-api-key=$GEMINI_API_KEY" \
        --env-vars \
            "ASPNETCORE_ENVIRONMENT=Production" \
            "ConnectionStrings__CloudDb=secretref:cloud-db-connection" \
            "Jwt__Issuer=$JWT_ISSUER" \
            "Jwt__Audience=$JWT_AUDIENCE" \
            "Jwt__Key=secretref:jwt-key" \
            "Google__ClientId=$GOOGLE_CLIENT_ID" \
            "Google__MobileClientId=$GOOGLE_MOBILE_CLIENT_ID" \
            "Cors__Origins__0=https://$portal_url" \
            "Doctor__RegistrationSecret=secretref:doctor-registration-secret" \
            "GEMINI_API_KEY=secretref:gemini-api-key" \
        --output none
    log_success "Created WebAPI Container App"
fi

# Output app URL
webapi_url=$(az containerapp show --name "$APP_WEBAPI" --resource-group "$RG" --query properties.configuration.ingress.fqdn -o tsv 2>/dev/null || echo "")

log_success "WebAPI redeployed successfully"
echo ""
if [[ -n "$webapi_url" ]]; then
    echo "WebAPI URL: https://$webapi_url"
    echo "API Base URL: https://$webapi_url/api"
else
    echo "WebAPI URL: (not available)"
fi
echo ""
