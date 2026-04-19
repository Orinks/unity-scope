#!/usr/bin/env bash
# Build UnityScope BepInEx plugin (cross-platform wrapper around dotnet build).
#
# Usage:
#   ./build.sh release "/path/to/game"
#   ./build.sh debug   "/path/to/game"
#   ./build.sh                             # uses $UNITYSCOPE_GAMEDIR if set

set -euo pipefail

CONFIG="Debug"
case "${1:-}" in
  release|Release) CONFIG="Release"; shift ;;
  debug|Debug)     CONFIG="Debug";   shift ;;
esac

GAMEDIR="${1:-${UNITYSCOPE_GAMEDIR:-}}"
if [[ -z "$GAMEDIR" ]]; then
  echo "ERROR: GameDir required."
  echo "  ./build.sh release \"/path/to/game\""
  echo "  export UNITYSCOPE_GAMEDIR=/path/to/game && ./build.sh release"
  exit 1
fi

echo "Building UnityScope ($CONFIG) for: $GAMEDIR"
dotnet build src/UnityScope.Runtime/UnityScope.Runtime.csproj -c "$CONFIG" -p:GameDir="$GAMEDIR"

if [[ "$CONFIG" == "Release" ]]; then
  echo
  echo "Built and deployed to: $GAMEDIR/BepInEx/plugins/UnityScope/"
fi
