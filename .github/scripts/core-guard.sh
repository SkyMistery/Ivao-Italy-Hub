#!/usr/bin/env bash
# The core guard (CLAUDE.md section 0, note decisions/2026-09-24-un-secondo-sviluppatore.md).
#
# Reads the files a contributor's pull request changes, one per line as "status<TAB>path<TAB>previous path", and
# sorts them in three:
#
#   maintainer only  the rules, the plan, the pipeline, the architecture tests, another module: a contributor never
#                    changes them, and the check fails whatever else the pull request carries;
#   core             the shared code every module stands on: allowed only as the extension of a mechanism, so the
#                    pull request must add a decision note under docs/internal/decisions/ that says why;
#   everything else  the contributor's own module and documents.
#
# It is a signal for the reviewer, not the lock: the lock is that only the maintainer merges. It runs from main
# (pull_request_target), so a branch cannot loosen the guard that judges it.
#
# Usage: core-guard.sh < files.tsv      (exit 1 when the pull request has to change)
# Try it locally (git's one-letter statuses mapped to the words of the API the workflow passes, or a new decision note
# would count as an edited one):
#   git diff --name-status origin/main... | awk -F'\t' 'BEGIN { w["A"]="added"; w["M"]="modified"; w["D"]="removed";
#     w["T"]="changed"; w["R"]="renamed"; w["C"]="copied" } { print w[substr($1, 1, 1)] "\t" $NF "\t" (NF == 3 ? $2 : "") }' |
#     bash .github/scripts/core-guard.sh

set -euo pipefail

# The contributor's own module. A file named after it is new work even under tests/ or web/e2e/.
OWN='[Tt]raining'

# Generated files a module changes just by existing, and the explicit lists a module is registered in.
is_allowed() {
  [[ $1 =~ ^IvaoHub\.sln$ ]] ||
  [[ $1 =~ ^src/IvaoHub\.Web/(IvaoHub\.Web\.csproj|Modules\.cs)$ ]] ||
  [[ $1 =~ ^web/src/modules/index\.ts$ ]] ||
  [[ $1 =~ ^tests/[^/]+/[^/]+\.csproj$ ]] ||
  [[ $1 =~ ^web/src/shared/api/schema\.d\.ts$ ]] ||
  [[ $1 =~ ^web/src/routeTree\.gen\.ts$ ]]
}

is_maintainer_only() {
  local status=$1 path=$2
  [[ $path =~ ^\.github/ ]] ||
  [[ $path =~ ^(CLAUDE\.md|CONTRIBUTING\.md|LICENSE|NOTICE|global\.json)$ ]] ||
  [[ $path =~ ^\.claude/ ]] ||
  [[ $path =~ ^docs/internal/(0[0-6]-|HANDOFF\.md$|demo-m1\.md$) ]] ||
  [[ $path =~ ^docs/internal/decisions/ && $status != added ]] ||
  [[ $path =~ ^tests/IvaoHub\.UnitTests/ArchitectureTests\.cs$ ]] ||
  # Another module. A module never references another one (CLAUDE.md section 2).
  [[ $path =~ ^src/IvaoHub\.Modules\.FlightOps/ ]] ||
  [[ $path =~ ^web/src/modules/flightops/ ]] ||
  [[ $path =~ ^locales/[^/]+/flightops\.json$ ]]
}

is_core() {
  local status=$1 path=$2
  [[ $path =~ ^src/IvaoHub\.(Core|Web)/ ]] ||
  [[ $path =~ ^web/src/(app|blocks|features|routes|shared|styles|test)/ ]] ||
  [[ $path =~ ^web/src/[^/]+$ ]] ||
  [[ $path =~ ^web/(scripts/|[^/]+$) ]] ||
  # The core's own language files; a module's copies (locales/<lang>/<module>.json) are generated.
  [[ $path =~ ^locales/[^/]+/(common|errors|mail|seed)\.json$ ]] ||
  [[ $path =~ ^config/(security\.json|division\.xx\.json)$ ]] ||
  [[ $path =~ ^(Directory\.Build\.props|Directory\.Packages\.props|docker-compose\.yml|\.editorconfig|\.gitattributes|\.gitignore)$ ]] ||
  [[ $path =~ ^docs/UI-GUIDELINES\.md$ ]] ||
  # A shared test, fixture, spec, tool or seed that already exists: adding one is fine, changing one is not.
  [[ $path =~ ^(tests|web/e2e|tools|seed)/ && $status != added && ! $path =~ $OWN ]]
}

maintainer=()
core=()
notes=()

while IFS=$'\t' read -r status path previous || [[ -n ${status:-} ]]; do
  [[ -z ${path:-} ]] && continue
  if [[ $status == added && $path =~ ^docs/internal/decisions/[^/]+\.md$ ]]; then
    notes+=("$path")
  fi
  for p in "$path" ${previous:+"$previous"}; do
    is_allowed "$p" && continue
    if is_maintainer_only "$status" "$p"; then
      maintainer+=("$p")
    elif is_core "$status" "$p"; then
      core+=("$p")
    fi
  done
done

out=${GITHUB_STEP_SUMMARY:-/dev/stdout}
list() { for x in "$@"; do printf -- '- `%s`\n' "$x"; done; }
failed=0

{
  echo "## Core guard"
  echo
  if ((${#maintainer[@]})); then
    failed=1
    echo "### ❌ Files only the maintainer changes"
    echo
    echo "A contributor's pull request never touches these (CLAUDE.md section 0). Take them out of the branch;"
    echo "if one of them really has to change, ask the maintainer in a comment."
    echo
    list "${maintainer[@]}"
    echo
  fi
  if ((${#core[@]})); then
    if ((${#notes[@]})); then
      echo "### ⚠️ Core files, justified by a new decision note"
      echo
      echo "The reviewer reads these first. Notes added:"
      echo
      list "${notes[@]}"
    else
      failed=1
      echo "### ❌ Core files without a new decision note"
      echo
      echo "Changing the core is extending a mechanism (CLAUDE.md section 5, case b or c): it needs a note added"
      echo "under docs/internal/decisions/ in this pull request, and its own pull request before the module code."
    fi
    echo
    echo "Core files touched:"
    echo
    list "${core[@]}"
    echo
  fi
  if ((!${#maintainer[@]} && !${#core[@]})); then
    echo "✅ Only the contributor's module and documents."
  fi
} >>"$out"

exit $failed
