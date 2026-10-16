#!/usr/bin/env sh
# SharpQuest helper for Linux and macOS (game track). Usage: sh sq.sh init | test | update | report | help
# MANAGED FILE: `sq update` and CI replace it with the official copy.
#
# Finds dotnet even when it isn't on the PATH: on some lab PCs it only works inside Rider's terminal.

HERE=$(dirname "$0")
if [ ! -f "$HERE/tools/sq/sq.csproj" ]; then
  echo "sq: SharpQuest/tools/sq/sq.csproj is missing. Run: git checkout -- SharpQuest/tools" >&2
  exit 1
fi

DOTNET=$(command -v dotnet 2>/dev/null)
if [ -z "$DOTNET" ]; then
  for dir in "$DOTNET_ROOT" "$HOME/.dotnet" /usr/share/dotnet /usr/lib/dotnet /usr/lib64/dotnet /opt/dotnet /usr/local/share/dotnet; do
    if [ -n "$dir" ] && [ -x "$dir/dotnet" ]; then DOTNET="$dir/dotnet"; break; fi
  done
fi
if [ -z "$DOTNET" ]; then
  echo "sq: can't find dotnet. On a lab PC, run this in Rider's terminal (View > Tool Windows > Terminal)." >&2
  exit 127
fi

REAL=$(readlink -f "$DOTNET" 2>/dev/null || echo "$DOTNET")
if [ -z "$DOTNET_ROOT" ] && [ -d "$(dirname "$REAL")/sdk" ]; then
  DOTNET_ROOT=$(dirname "$REAL"); export DOTNET_ROOT
fi
PATH="$(dirname "$REAL"):$PATH"; export PATH

exec "$DOTNET" run --project "$HERE/tools/sq" -- "$@"
