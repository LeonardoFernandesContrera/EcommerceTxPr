#!/usr/bin/env bash

set -euo pipefail

environment_file="${1:-${COMPOSE_ENV_FILE:-}}"
smoke_response_file="$(mktemp)"

compose() {
    if [[ -n "$environment_file" ]]; then
        docker compose --env-file "$environment_file" "$@"
    else
        docker compose "$@"
    fi
}

cleanup() {
    local status=$?
    trap - EXIT INT TERM

    if [[ $status -ne 0 ]]; then
        echo "Compose smoke test failed; collecting diagnostics." >&2
        compose ps >&2 || true
        compose logs --no-color >&2 || true
    fi

    compose down --volumes --remove-orphans >/dev/null 2>&1 || true
    rm -f "$smoke_response_file"
    exit "$status"
}

trap cleanup EXIT INT TERM

fail() {
    echo "$1" >&2
    return 1
}

wait_for_healthy_service() {
    local service="$1"
    local timeout_seconds="$2"
    local deadline=$((SECONDS + timeout_seconds))

    while (( SECONDS < deadline )); do
        local container_id
        container_id="$(compose ps --quiet "$service")"

        if [[ -n "$container_id" ]]; then
            local state
            state="$(docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "$container_id")"

            case "$state" in
                healthy)
                    return 0
                    ;;
                unhealthy|exited|dead)
                    fail "Service '$service' entered terminal state '$state'."
                    ;;
            esac
        fi

        sleep 1
    done

    fail "Timed out waiting for service '$service' to become healthy."
}

wait_for_migrations() {
    local timeout_seconds="$1"
    local deadline=$((SECONDS + timeout_seconds))

    while (( SECONDS < deadline )); do
        local container_id
        container_id="$(compose ps --all --quiet migrations)"

        if [[ -n "$container_id" ]]; then
            local state
            local exit_code
            state="$(docker inspect --format '{{.State.Status}}' "$container_id")"
            exit_code="$(docker inspect --format '{{.State.ExitCode}}' "$container_id")"

            if [[ "$state" == "exited" ]]; then
                [[ "$exit_code" == "0" ]] \
                    || fail "Database migrations exited with code '$exit_code'."
                return 0
            fi
        fi

        sleep 1
    done

    fail "Timed out waiting for database migrations to complete."
}

wait_for_http() {
    local url="$1"
    local timeout_seconds="$2"
    local deadline=$((SECONDS + timeout_seconds))

    while (( SECONDS < deadline )); do
        if curl --fail --silent --output /dev/null "$url"; then
            return 0
        fi

        sleep 1
    done

    fail "Timed out waiting for '$url'."
}

wait_for_rabbitmq_resource() {
    local url="$1"
    local user_name="$2"
    local password="$3"
    local timeout_seconds="$4"
    local deadline=$((SECONDS + timeout_seconds))

    while (( SECONDS < deadline )); do
        if curl \
            --fail \
            --silent \
            --output /dev/null \
            --user "$user_name:$password" \
            "$url"; then
            return 0
        fi

        sleep 1
    done

    fail "Timed out waiting for RabbitMQ application topology."
}

compose config --quiet
compose up --detach --build

wait_for_healthy_service sqlserver 180
wait_for_healthy_service rabbitmq 120
wait_for_migrations 120
wait_for_healthy_service api 120

wait_for_http http://localhost:8080/health/live 60
wait_for_http http://localhost:8080/health/ready 60
wait_for_http http://localhost:8080/health 60

full_health="$(curl --fail --silent http://localhost:8080/health)"
jq --exit-status '
    .status == "Healthy"
    and .checks.sql.status == "Healthy"
    and .checks.rabbitmq.status == "Healthy"
' <<<"$full_health" >/dev/null \
    || fail "The full dependency health endpoint is not Healthy."

customer_status="$(curl \
    --silent \
    --output "$smoke_response_file" \
    --write-out '%{http_code}' \
    --header 'Content-Type: application/json' \
    --data '{"name":"Compose Smoke Customer","birthDate":"1990-01-02T00:00:00Z"}' \
    http://localhost:8080/api/customers)"
[[ "$customer_status" == "201" ]] \
    || fail "SQL-backed customer creation returned HTTP '$customer_status'."

rabbitmq_user="${RABBITMQ_DEFAULT_USER:-$(compose exec --no-TTY rabbitmq printenv RABBITMQ_DEFAULT_USER)}"
rabbitmq_password="${RABBITMQ_DEFAULT_PASS:-$(compose exec --no-TTY rabbitmq printenv RABBITMQ_DEFAULT_PASS)}"
rabbitmq_management_endpoint="$(compose port rabbitmq 15672)"
rabbitmq_management_port="${rabbitmq_management_endpoint##*:}"

wait_for_rabbitmq_resource \
    "http://localhost:${rabbitmq_management_port}/api/exchanges/%2F/ecommerce.events" \
    "$rabbitmq_user" \
    "$rabbitmq_password" \
    60
wait_for_rabbitmq_resource \
    "http://localhost:${rabbitmq_management_port}/api/queues/%2F/ecommerce.payment-events" \
    "$rabbitmq_user" \
    "$rabbitmq_password" \
    60

echo "Compose smoke test passed."
