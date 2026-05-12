#!/usr/bin/env bash
set -euo pipefail

################################################################################
# Doctor Portal-only Redeploy Script
# Builds and redeploys only the doctor-portal Container App
################################################################################

## Prefer merged deploy env, then fall back to doctor-portal/.env.local
if [[ -f "./deploy-scripts/.env.local" ]]; then
    # shellcheck disable=SC1091
    set -a
    source ./deploy-scripts/.env.local
    set +a
elif [[ -f "./doctor-portal/.env.local" ]]; then
    # shellcheck disable=SC1091
    set -a
    source ./doctor-portal/.env.local
    set +a
fi

# Configuration (override via env vars)
readonly RG="${RG:-unibytes}"
readonly LOC="${LOC:-germanywestcentral}"
readonly ACR="${ACR:-unibytesacr}"
readonly ENV_NAME="${ENV_NAME:-unibytes-env}"
readonly APP_WEBAPI="${APP_WEBAPI:-unibytes-backend}"
readonly APP_PORTAL="${APP_PORTAL:-unibytes-frontend}"
readonly FRONTEND_DOCKERFILE="${FRONTEND_DOCKERFILE:-./doctor-portal/Dockerfile}"
readonly PORTAL_CPU="${PORTAL_CPU:-0.5}"
readonly PORTAL_MEM="${PORTAL_MEM:-1.0Gi}"
readonly PORTAL_TARGET_PORT="${PORTAL_TARGET_PORT:-3000}"
readonly MIN_REPLICAS="${MIN_REPLICAS:-0}"
readonly MAX_REPLICAS="${MAX_REPLICAS:-1}"

# Keep deployment in Europe and auto-fallback if Azure policy blocks a region.
readonly EU_REGION_CANDIDATES_DEFAULT="germanywestcentral,northeurope,westeurope,francecentral,swedencentral,switzerlandnorth,uksouth"
readonly EU_REGION_CANDIDATES="${EU_REGION_CANDIDATES:-$EU_REGION_CANDIDATES_DEFAULT}"

# Frontend auth configuration (REQUIRED)
readonly GOOGLE_CLIENT_ID="${GOOGLE_CLIENT_ID:-${Google__ClientId:-}}"
readonly GOOGLE_CLIENT_SECRET="${GOOGLE_CLIENT_SECRET:-${Google__ClientSecret:-}}"
readonly AUTH_SECRET="${AUTH_SECRET:-$(openssl rand -base64 32)}"

# API URLs (Prefer .env.local/env vars, otherwise auto-resolve from backend)
API_BASE_URL="${API_BASE_URL:-${NEXT_PUBLIC_API_URL:-}}"
NEXT_PUBLIC_API_URL="${NEXT_PUBLIC_API_URL:-$API_BASE_URL}"
export API_BASE_URL
export NEXT_PUBLIC_API_URL

# Optional doctor registration secret expected by portal env
readonly DOCTOR_REGISTRATION_SECRET="${DOCTOR_REGISTRATION_SECRET:-${Doctor__RegistrationSecret:-change-me-registration-secret}}"

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

validate_required() {
    log_info "Validating required secrets..."

    local missing=()

    if [[ -z "$GOOGLE_CLIENT_ID" ]]; then
        missing+=("GOOGLE_CLIENT_ID")
    fi

    if [[ -z "$GOOGLE_CLIENT_SECRET" ]]; then
        missing+=("GOOGLE_CLIENT_SECRET")
    fi

    if [[ ${#missing[@]} -gt 0 ]]; then
        log_error "Missing required secrets:"
        for v in "${missing[@]}"; do
            log_error "  - $v"
        done
        exit 1
    fi

    if [[ ! -f "$FRONTEND_DOCKERFILE" ]]; then
        log_error "Frontend Dockerfile not found: $FRONTEND_DOCKERFILE"
        log_info "Create a Dockerfile for doctor-portal or set FRONTEND_DOCKERFILE to the correct path."
        exit 1
    fi

    log_success "Required configuration validated"
}

validate_required
ensure_prerequisites
ensure_resource_group
ensure_acr
ensure_containerapp_env

# Get ACR login server
log_info "Getting ACR login server..."
ACR_LOGIN_SERVER=$(az acr show --name "$ACR" --resource-group "$RG" --query loginServer -o tsv)
log_success "ACR login server: $ACR_LOGIN_SERVER"

# Resolve backend URL if not explicitly provided
if [[ -z "$API_BASE_URL" ]]; then
    log_info "Resolving WebAPI URL from Azure..."
    webapi_url=$(az containerapp show --name "$APP_WEBAPI" --resource-group "$RG" --query properties.configuration.ingress.fqdn -o tsv 2>/dev/null || echo "")
    
    if [[ -z "$webapi_url" ]]; then
        if [[ -n "${BACKEND_URL:-}" ]]; then
            webapi_url="${BACKEND_URL#https://}"
            webapi_url="${webapi_url%/}"
        fi
    fi

    if [[ -n "$webapi_url" ]]; then
        API_BASE_URL="https://$webapi_url"
        NEXT_PUBLIC_API_URL="https://$webapi_url"
        log_success "Resolved WebAPI URL: $API_BASE_URL"
    else
        log_warning "Could not resolve WebAPI URL. Frontend will be deployed without API URL vars."
    fi
else
    log_info "Using provided API_BASE_URL: $API_BASE_URL"
fi

# Resolve portal URL for NEXTAUTH_URL
portal_url=$(az containerapp show --name "$APP_PORTAL" --resource-group "$RG" --query properties.configuration.ingress.fqdn -o tsv 2>/dev/null || echo "")

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

# Build args
build_args=""
if [[ -n "$API_BASE_URL" ]]; then
    build_args="--build-arg NEXT_PUBLIC_API_URL=$NEXT_PUBLIC_API_URL --build-arg API_BASE_URL=$API_BASE_URL"
    log_info "Building with API URL: $API_BASE_URL"
else
    log_warning "Building without API URL build args"
fi

# Build and push image
log_info "Building and pushing doctor-portal image..."
full_image="$ACR_LOGIN_SERVER/doctor-portal:latest"

if docker buildx build \
    --platform linux/amd64 \
    --push \
    --tag "$full_image" \
    --file "$FRONTEND_DOCKERFILE" \
    $build_args \
    "./doctor-portal"; then
    log_success "doctor-portal image built and pushed"
else
    log_error "Failed to build doctor-portal image"
    exit 1
fi

# Update secrets
log_info "Updating doctor-portal secrets..."
acr_user=$(az acr credential show --name "$ACR" --query username -o tsv)
acr_password=$(az acr credential show --name "$ACR" --query "passwords[0].value" -o tsv)

if az containerapp show --name "$APP_PORTAL" --resource-group "$RG" >/dev/null 2>&1; then
    az containerapp secret set \
        --name "$APP_PORTAL" \
        --resource-group "$RG" \
        --secrets \
            "google-client-secret=$GOOGLE_CLIENT_SECRET" \
            "auth-secret=$AUTH_SECRET" \
            "doctor-registration-secret=$DOCTOR_REGISTRATION_SECRET" \
        --output none 2>/dev/null || true

    log_info "Updating doctor-portal Container App..."
    if [[ -n "$API_BASE_URL" && -n "$portal_url" ]]; then
        az containerapp update \
            --name "$APP_PORTAL" \
            --resource-group "$RG" \
            --image "$full_image" \
            --set-env-vars \
                "NEXT_PUBLIC_API_URL=$NEXT_PUBLIC_API_URL" \
                "API_BASE_URL=$API_BASE_URL" \
                "NEXTAUTH_URL=https://$portal_url" \
                "GOOGLE_CLIENT_ID=$GOOGLE_CLIENT_ID" \
                "GOOGLE_CLIENT_SECRET=secretref:google-client-secret" \
                "AUTH_SECRET=secretref:auth-secret" \
                "AUTH_TRUST_HOST=true" \
                "NEXT_PUBLIC_DOCTOR_SECRET_REQUIRED=true" \
                "Doctor__RegistrationSecret=secretref:doctor-registration-secret" \
            --output none 2>/dev/null || {
                log_warning "Update had issues, but may still have applied."
            }
    else
        log_warning "Missing resolved URLs, updating image and env vars..."
        az containerapp update \
            --name "$APP_PORTAL" \
            --resource-group "$RG" \
            --image "$full_image" \
            --set-env-vars \
                "NEXT_PUBLIC_API_URL=$NEXT_PUBLIC_API_URL" \
                "API_BASE_URL=$API_BASE_URL" \
                "GOOGLE_CLIENT_ID=$GOOGLE_CLIENT_ID" \
                "GOOGLE_CLIENT_SECRET=secretref:google-client-secret" \
                "AUTH_SECRET=secretref:auth-secret" \
                "AUTH_TRUST_HOST=true" \
                "NEXT_PUBLIC_DOCTOR_SECRET_REQUIRED=true" \
                "Doctor__RegistrationSecret=secretref:doctor-registration-secret" \
            --output none 2>/dev/null || {
                log_warning "Update had issues, but may still have applied."
            }
    fi
else
    log_info "Creating doctor-portal Container App..."
    create_env_vars=(
        "GOOGLE_CLIENT_ID=$GOOGLE_CLIENT_ID"
        "GOOGLE_CLIENT_SECRET=secretref:google-client-secret"
        "AUTH_SECRET=secretref:auth-secret"
        "AUTH_TRUST_HOST=true"
        "NEXT_PUBLIC_DOCTOR_SECRET_REQUIRED=true"
        "Doctor__RegistrationSecret=secretref:doctor-registration-secret"
    )

    if [[ -n "$API_BASE_URL" ]]; then
        create_env_vars+=("NEXT_PUBLIC_API_URL=$NEXT_PUBLIC_API_URL")
        create_env_vars+=("API_BASE_URL=$API_BASE_URL")
    fi

    if [[ -n "$portal_url" ]]; then
        create_env_vars+=("NEXTAUTH_URL=https://$portal_url")
    fi

    az containerapp create \
        --name "$APP_PORTAL" \
        --resource-group "$RG" \
        --environment "$ENV_NAME" \
        --image "$full_image" \
        --ingress external \
        --target-port "$PORTAL_TARGET_PORT" \
        --cpu "$PORTAL_CPU" \
        --memory "$PORTAL_MEM" \
        --min-replicas "$MIN_REPLICAS" \
        --max-replicas "$MAX_REPLICAS" \
        --registry-server "$ACR_LOGIN_SERVER" \
        --registry-username "$acr_user" \
        --registry-password "$acr_password" \
        --secrets \
            "google-client-secret=$GOOGLE_CLIENT_SECRET" \
            "auth-secret=$AUTH_SECRET" \
            "doctor-registration-secret=$DOCTOR_REGISTRATION_SECRET" \
        --env-vars "${create_env_vars[@]}" \
        --output none

    # After first creation we can set a proper NEXTAUTH_URL from the assigned FQDN.
    portal_url=$(az containerapp show --name "$APP_PORTAL" --resource-group "$RG" --query properties.configuration.ingress.fqdn -o tsv 2>/dev/null || echo "")
    if [[ -n "$portal_url" ]]; then
        az containerapp update \
            --name "$APP_PORTAL" \
            --resource-group "$RG" \
            --set-env-vars "NEXTAUTH_URL=https://$portal_url" \
            --output none 2>/dev/null || true
    fi

    log_success "Created doctor-portal Container App"
fi

log_success "doctor-portal redeployed successfully"
echo ""
echo "Doctor Portal URL: https://$portal_url"
if [[ -n "$webapi_url" ]]; then
    echo "WebAPI URL: https://$webapi_url"
fi
echo ""
