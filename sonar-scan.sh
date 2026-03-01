#!/bin/bash
# SonarQube analysis script for DigitalTwin
# Prerequisites: SonarQube running on localhost:9000, valid token with "Execute Analysis" permission
# Excludes Android build (MAUI) when Android SDK not installed - use ExcludeAndroidBuild=true

set -e
cd "$(dirname "$0")"

SONAR_TOKEN="${SONAR_TOKEN:-sqp_7cf60243a267d15be2702931b21bf4589bfd5894}"
SONAR_HOST="${SONAR_HOST:-http://localhost:9000}"
PROJECT_KEY="${PROJECT_KEY:-DigitalTwin}"

echo "=== 1. Starting SonarQube analysis ==="
dotnet sonarscanner begin /k:"$PROJECT_KEY" \
  /d:sonar.host.url="$SONAR_HOST" \
  /d:sonar.token="$SONAR_TOKEN" \
  /d:sonar.cs.opencover.reportsPaths="**/coverage.opencover.xml" \
  /d:sonar.exclusions="**/Migrations/**,doctor-portal/app/globals.css"

echo "=== 2. Building solution (MAUI without Android) ==="
dotnet build -p:ExcludeAndroidBuild=true

echo "=== 3. Running tests with code coverage ==="
dotnet test \
  --settings tests.runsettings \
  --collect:"XPlat Code Coverage" \
  --no-build

echo "=== 4. Ending SonarQube analysis ==="
dotnet sonarscanner end /d:sonar.token="$SONAR_TOKEN"

echo "=== Done ==="
