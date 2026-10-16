"""Shared JSON file adapter used by the standalone ForestGuardian algorithms."""
import json
import sys
from pathlib import Path


def read_input(path=None):
    return json.loads(Path(path).read_text(encoding="utf-8")) if path else json.load(sys.stdin)


def write_output(value, path=None):
    encoded = json.dumps(value, indent=2, sort_keys=True)
    if path:
        Path(path).write_text(encoded + "\n", encoding="utf-8")
    else:
        print(encoded)


def run(function):
    args = sys.argv[1:]
    try:
        source = read_input(args[0] if args else None)
        result = function(source)
        write_output(result, args[1] if len(args) > 1 else None)
    except (ValueError, KeyError, TypeError, IndexError) as exc:
        print(json.dumps({"error": str(exc)}), file=sys.stderr)
        raise SystemExit(2)
