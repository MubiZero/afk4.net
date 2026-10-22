#!/usr/bin/env bash
# Гоняет одну из N частей тестового проекта, собранного заранее (--no-build).
#   scripts/test-shard.sh tests/AFK4.Platform.Api.Tests 2 3 [аргументы dotnet test]
# Классы делятся по кругу в порядке имён, поэтому новый тест сам попадает в какую-то часть, а
# ничего из набора не выпадает: сверка в конце требует, чтобы прогнано было не меньше тестов,
# чем назначено части. Строка теории с данными в списке одна, а в прогоне их несколько, поэтому
# сверка проверяет «не меньше», а не равенство. Недобор — красный прогон, а не молча пропущенные
# тесты.
set -euo pipefail

project=$1
index=$2
count=$3
shift 3

listing=$(mktemp)
trap 'rm -f "$listing"' EXIT
dotnet test --project "$project" --no-build --list-tests "$@" | sed -n 's/^  \([^ ].*\)$/\1/p' > "$listing"

# Класс — всё до последней точки перед именем метода; аргументы теории (после «(») отбрасываются.
# Строки «число класс» отсортированы по имени и раздаются по кругу.
assigned=$(sed -E 's/\(.*//; s/\.[^.]*$//' "$listing" | sort | uniq -c \
  | awk -v index_="$index" -v count="$count" 'NR % count == index_ % count { print $2 "\t" $1 }')

[ -n "$assigned" ] || { echo "Часть $index из $count пуста: в проекте меньше классов, чем частей." >&2; exit 1; }

classes=$(printf '%s\n' "$assigned" | cut -f1 | paste -sd ' ' -)
expected=$(printf '%s\n' "$assigned" | awk -F'\t' '{ n += $2 } END { print n }')
echo "Часть $index из $count: $(printf '%s\n' "$assigned" | wc -l | tr -d ' ') классов, ждём $expected тестов"

output=$(mktemp)
trap 'rm -f "$listing" "$output"' EXIT
# Имена классов без пробелов: каждое — отдельное значение --filter-class.
# shellcheck disable=SC2086
dotnet test --project "$project" --no-build --filter-class $classes "$@" | tee "$output"

actual=$(sed -n 's/^ *total: *\([0-9][0-9]*\)$/\1/p' "$output" | tail -1)
if [ "${actual:-0}" -lt "$expected" ]; then
  echo "Часть $index из $count прогнала ${actual:-0} тестов, а назначено не меньше $expected: фильтр не совпал со списком." >&2
  exit 1
fi
