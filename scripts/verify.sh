#!/usr/bin/env bash
# Локальные ворота — те же, что в .github/workflows/pr-verification.yml, только на этой машине и
# сразу. Смысл в скорости обратной связи: дождаться GitHub — это пять-десять минут на правку,
# из которых половина уходит на очередь и установку инструментов, уже стоящих здесь.
#
# Чего этот прогон НЕ доказывает (и о чём честно пишет в конце):
#   • Windows-часть — WPF-оболочки, агент и их тесты собираются здесь, но не запускаются;
#     `Build And Test Windows` в CI остаётся единственной их проверкой.
#   • Версии инструментов здесь свои. CI прибит к bun 1.3.14 и flutter 3.41.6.
#
# Использование:
#   scripts/verify.sh              — только затронутые дорожки (как «Detect Relevant Changes»)
#   scripts/verify.sh --all        — все дорожки
#   scripts/verify.sh --fast       — без медленного хвоста (сборка Flutter web, сквозной сценарий)
#   scripts/verify.sh dotnet web   — только названные дорожки
set -uo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."

[ -d "$HOME/.dotnet" ] && PATH="$HOME/.dotnet:$PATH"
export PATH

# Тестовая база: имя обязано оканчиваться на _test — иначе тесты откажутся её трогать, и прогон
# станет зелёным на пропущенных проверках. Порт 5433, чтобы не столкнуться с рабочим Postgres.
PG_HOST=${AFK4_LOCAL_PG_HOST:-127.0.0.1}
PG_PORT=${AFK4_LOCAL_PG_PORT:-5433}
PG_DB=${AFK4_LOCAL_PG_DB:-afk4_test}
PG_USER=${AFK4_LOCAL_PG_USER:-postgres}
PG_CONTAINER=${AFK4_LOCAL_PG_CONTAINER:-afk4-test-pg}

lanes=()
fast=0
all=0
for argument in "$@"; do
  case "$argument" in
    --all) all=1 ;;
    --fast) fast=1 ;;
    dotnet|web|flutter) lanes+=("$argument") ;;
    -h|--help) sed -n '2,16p' "$0"; exit 0 ;;
    *) echo "Неизвестный аргумент: $argument" >&2; exit 2 ;;
  esac
done

# Что менялось относительно main — те же префиксы, что у CI. Логика одна: дорожка, которую
# изменения не задели, не запускается, и это не «пропущено», а «нечего проверять».
detect_lanes() {
  local base changed
  base=$(git merge-base HEAD origin/main 2>/dev/null || git merge-base HEAD main 2>/dev/null || echo '')
  if [ -z "$base" ]; then echo "dotnet web flutter"; return; fi
  changed=$( { git diff --name-only "$base"...HEAD; git diff --name-only; git ls-files --others --exclude-standard; } | sort -u)
  [ -z "$changed" ] && { echo ""; return; }

  local run_dotnet='' run_web='' run_flutter=''
  while IFS= read -r path; do
    case "$path" in
      .github/workflows/*|installers/*|scripts/*|src/*|tests/*|AFK4.sln|.config/dotnet-tools.json|Directory.Build.props|Directory.Packages.props|global.json|NuGet.config)
        run_dotnet=dotnet ;;
    esac
    case "$path" in
      packages/*|src/AFK4.PlatformControl.Web/*|src/AFK4.OrganizationAdmin.Web/*|src/AFK4.Customer.Web/*|src/AFK4.SetupWizard.Web/*|src/AFK4.Player.Shell.Web/*|package.json|bun.lock|bunfig.toml|.github/workflows/pr-verification.yml)
        run_web=web ;;
    esac
    case "$path" in
      locales/*|packages/i18n/*|src/afk4_customer_app/*|.github/workflows/pr-verification.yml)
        run_flutter=flutter ;;
    esac
  done <<< "$changed"
  echo "$run_dotnet $run_web $run_flutter"
}

if [ ${#lanes[@]} -eq 0 ]; then
  if [ "$all" = 1 ]; then
    lanes=(dotnet web flutter)
  else
    read -r -a lanes <<< "$(detect_lanes)"
  fi
fi
if [ ${#lanes[@]} -eq 0 ]; then
  echo "Изменений в проверяемых дорожках нет. Нечего прогонять."
  exit 0
fi

logs=$(mktemp -d "${TMPDIR:-/tmp}/afk4-verify.XXXXXX")
started=$(date +%s)

step() { printf '\n\033[1m▸ %s\033[0m\n' "$1"; }

# ── Postgres ────────────────────────────────────────────────────────────────────────────────────
# Без базы тесты молча пропускают всё денежное и все миграции, поэтому её отсутствие — отказ, а
# не предупреждение: зелёный прогон на пропущенных проверках хуже красного.
ensure_postgres() {
  if pg_isready -h "$PG_HOST" -p "$PG_PORT" >/dev/null 2>&1; then return 0; fi
  if command -v docker >/dev/null 2>&1 && docker ps -a --format '{{.Names}}' | grep -qx "$PG_CONTAINER"; then
    docker start "$PG_CONTAINER" >/dev/null
    for _ in $(seq 1 30); do
      pg_isready -h "$PG_HOST" -p "$PG_PORT" >/dev/null 2>&1 && return 0
      sleep 1
    done
  fi
  cat >&2 <<EOF
Тестовая база на $PG_HOST:$PG_PORT не отвечает. Поднять одноразовую:

  docker run -d --name $PG_CONTAINER -p 127.0.0.1:$PG_PORT:5432 \\
    -e POSTGRES_DB=$PG_DB -e POSTGRES_USER=$PG_USER \\
    -e POSTGRES_HOST_AUTH_METHOD=trust postgres:17-alpine
EOF
  return 1
}

# ── Дорожки ─────────────────────────────────────────────────────────────────────────────────────
lane_dotnet() {
  set -e
  # EnableWindowsTargeting: WPF-проекты решения на macOS иначе не собираются вовсе, а собрать их
  # стоит — половина поломок видна уже на компиляции.
  dotnet build AFK4.sln --nologo -v q -p:EnableWindowsTargeting=true -p:NuGetAudit=false

  local connection="Host=$PG_HOST;Port=$PG_PORT;Database=$PG_DB;Username=$PG_USER"
  export AFK4_REQUIRE_POSTGRES_TESTS=1
  export AFK4_POS_POSTGRES_TEST_CONNECTION_STRING="$connection"
  export AFK4_RESERVATION_POSTGRES_TEST_CONNECTION_STRING="$connection"
  export AFK4_COMMERCE_TEST_POSTGRES="$connection"
  export AFK4_PLATFORM_ADMIN_POSTGRES_TEST_CONNECTION_STRING="$connection"

  # Всё, что запускается вне Windows. За бортом остаются WPF-оболочки (их наборы вовсе не
  # собираются на macOS) и агент: половина его тестов зовёт signtool и именованные каналы, то
  # есть краснеет здесь не по делу. Оба набора остаются воротами CI, и об этом сказано в конце.
  for project in \
    tests/AFK4.Platform.Api.Tests tests/AFK4.Shared.Contracts.Tests \
    tests/AFK4.BuildingBlocks.Tests tests/AFK4.Localization.Tests \
    tests/AFK4.SetupWizard.Tests tests/AFK4.Update.Publisher.Tests; do
    echo "── $project"
    dotnet test "$project" --no-build --nologo -v q -p:NuGetAudit=false
  done
}

lane_web() {
  set -e
  # Список дословно повторяет CI: рабочая область, забытая здесь, — это не «пока не покрыта», а
  # набор тестов, который краснеет, и никто об этом не узнаёт.
  for dir in \
    packages/formatting packages/i18n packages/money \
    packages/tokens packages/ui \
    src/AFK4.PlatformControl.Web src/AFK4.OrganizationAdmin.Web \
    src/AFK4.Customer.Web src/AFK4.SetupWizard.Web \
    src/AFK4.Player.Shell.Web; do
    echo "── test $dir"
    (cd "$dir" && bun run test)
  done

  # Сборка — это ещё и проверка типов: `bun test` типы не смотрит, и ошибка доезжает до CI.
  for dir in \
    src/AFK4.PlatformControl.Web src/AFK4.OrganizationAdmin.Web \
    src/AFK4.Customer.Web src/AFK4.SetupWizard.Web \
    src/AFK4.Player.Shell.Web; do
    echo "── build $dir"
    (cd "$dir" && bun run build)
  done
}

lane_flutter() {
  set -e
  cd src/afk4_customer_app
  flutter pub get
  flutter analyze
  flutter test
  if [ "$fast" = 0 ]; then
    flutter test integration_test -d flutter-tester
    flutter build web --release
  fi
}

# ── Общий шаг: сгенерированная локализация ──────────────────────────────────────────────────────
# Идёт до дорожек и не параллельно с ними: генератор переписывает те самые файлы, которые
# читают веб-тесты.
if printf '%s\n' "${lanes[@]}" | grep -qx -e web -e flutter; then
  step "Локализация сгенерирована заново"
  if ! (cd packages/i18n && bun run gen) >"$logs/i18n.log" 2>&1 \
     || ! git diff --exit-code -- locales src/afk4_customer_app/lib/l10n packages/i18n/src >>"$logs/i18n.log" 2>&1; then
    tail -30 "$logs/i18n.log"
    echo "Каталог правили без перегенерации: запусти (cd packages/i18n && bun run gen) и закоммить результат." >&2
    exit 1
  fi
  echo "ок"
fi

if printf '%s\n' "${lanes[@]}" | grep -qx dotnet; then
  step "Тестовая база"
  ensure_postgres || exit 1
  echo "ок: $PG_HOST:$PG_PORT/$PG_DB"
fi

# ── Прогон ──────────────────────────────────────────────────────────────────────────────────────
# Дорожки идут разом, но сборки уступают: тесты в happy-dom живут таймерами, и рядом с
# `flutter build web --release` и сборкой решения их таймеры голодают так, что краснеет здоровый
# тест. Уступчивость дешевле, чем разбираться каждый раз, машина это или код.
declare -a pids=() names=()
for lane in "${lanes[@]}"; do
  step "Дорожка $lane — пошла"
  if [ "$lane" = web ]; then
    ( "lane_$lane" ) >"$logs/$lane.log" 2>&1 &
  else
    ( nice -n 10 bash -c "$(declare -f "lane_$lane"); fast=$fast; PG_HOST=$PG_HOST; PG_PORT=$PG_PORT; PG_DB=$PG_DB; PG_USER=$PG_USER; lane_$lane" ) >"$logs/$lane.log" 2>&1 &
  fi
  pids+=($!) ; names+=("$lane")
done

failed=()
for index in "${!pids[@]}"; do
  if wait "${pids[$index]}"; then
    printf '\033[32m✓ %s\033[0m (%s)\n' "${names[$index]}" "$logs/${names[$index]}.log"
  else
    failed+=("${names[$index]}")
    printf '\033[31m✗ %s\033[0m\n' "${names[$index]}"
  fi
done

for lane in "${failed[@]}"; do
  step "Хвост $lane"
  tail -40 "$logs/$lane.log"
done

elapsed=$(( $(date +%s) - started ))
printf '\n%s за %dм %02dс. Логи: %s\n' \
  "$([ ${#failed[@]} -eq 0 ] && echo 'Всё зелено' || echo "Красное: ${failed[*]}")" \
  $((elapsed / 60)) $((elapsed % 60)) "$logs"

if [ ${#failed[@]} -eq 0 ]; then
  echo "Не проверено здесь: Windows-часть — оболочки, агент и их тесты (собираются, но не запускаются). Это ворота CI."
  [ "$fast" = 1 ] && echo "Пропущено по --fast: сквозной сценарий приложения и сборка Flutter web."
fi

[ ${#failed[@]} -eq 0 ]
