#!/usr/bin/env bash
# The backbone tests have to have run, not only passed (CLAUDE.md section 0, note
# decisions/2026-09-24-un-secondo-sviluppatore.md, addendum of 25 September).
#
# "dotnet test" going green proves that every test it found passed. It does not prove that the tests
# that guard the architecture were found: a test project's .csproj is a file a module has to touch
# (to reference the module), so `<Compile Remove="ArchitectureTests.cs" />` would silence them with a
# green build and a green core guard. This step runs the guarding classes on their own, straight from
# the built test assemblies, and compares the number of tests xunit ran with the number of [Fact]s
# and [Theory]s written in the source file -- a file only the maintainer changes.
#
# Usage: backbone-ran.sh <configuration>      (run after the build, from the repository root)

set -euo pipefail

configuration=${1:-Release}
failed=0

# assembly | class | source file
checks=(
  "tests/IvaoHub.UnitTests/bin/$configuration/net10.0/IvaoHub.UnitTests.dll|IvaoHub.UnitTests.ArchitectureTests|tests/IvaoHub.UnitTests/ArchitectureTests.cs"
  "tests/IvaoHub.IntegrationTests/bin/$configuration/net10.0/IvaoHub.IntegrationTests.dll|IvaoHub.IntegrationTests.ForkabilityXxDivisionTests|tests/IvaoHub.IntegrationTests/ForkabilityXxDivisionTests.cs"
)

for check in "${checks[@]}"; do
  IFS='|' read -r assembly class source <<<"$check"
  expected=$(grep -cE '^\s*\[(Fact|Theory)' "$source")
  echo "::group::$class ($expected tests written)"
  output=$(dotnet "$assembly" -class "$class" 2>&1) || true
  echo "$output"
  echo "::endgroup::"
  summary=$(echo "$output" | grep -E 'Total: [0-9]+' | tail -1 || true)
  total=$(echo "$summary" | sed -nE 's/.*Total: ([0-9]+).*/\1/p')
  errors=$(echo "$summary" | sed -nE 's/.*Errors: ([0-9]+).*/\1/p')
  failures=$(echo "$summary" | sed -nE 's/.*Failed: ([0-9]+).*/\1/p')
  if [[ -z $total ]]; then
    echo "::error::$class: no test summary; the assembly did not run"
    failed=1
  elif ((total != expected)); then
    echo "::error::$class: $total tests ran, $expected are written in $source -- something keeps them out of the build"
    failed=1
  elif ((errors != 0 || failures != 0)); then
    echo "::error::$class: $failures failed, $errors errors"
    failed=1
  else
    echo "$class: $total of $expected ran, all passed"
  fi
done

exit $failed
