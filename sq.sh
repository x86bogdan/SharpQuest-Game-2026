#!/usr/bin/env sh
# SharpQuest helper (game track). Usage from the top of your repository: sh sq.sh init | test | update | help
# MANAGED FILE: `sq update` and CI replace it with the official copy.
exec sh "$(dirname "$0")/SharpQuest/sq.sh" "$@"
