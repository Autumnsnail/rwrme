#!/usr/bin/env python3
"""Run from any working directory: python /path/to/CLI/rwrmap.py ..."""
import json
import sys

try:
    from rwrmap.__main__ import main
except ImportError as error:
    print(json.dumps({"ok": False, "error": {"code": "DEPENDENCY_MISSING", "message": str(error),
          "hint": "python -m pip install -r CLI/requirements.txt"}}))
    sys.exit(2)

if __name__ == "__main__":
    sys.exit(main())
