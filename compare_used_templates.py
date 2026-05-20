#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
统计 used_templates 日志中的名称，哪些在 objects.svg 全文里搜不到。

搜索方式：与编辑器 Ctrl+F 相同，对 SVG 全文做区分大小写的字面量子串匹配。
日志：跳过 # 注释、[分组] 行，其余每行一个名称，不分组输出。

默认对比 Assets/templates/objects.svg（材质模板库）。
导出后的 Assets/map/objects.svg 已合并地图实例，通常会全部命中。
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent

DEFAULT_SVG = SCRIPT_DIR / "Assets" / "templates" / "objects.svg"
DEFAULT_LOG = SCRIPT_DIR / "used_templates - 副本.log"


def parse_names_from_log(path: Path) -> list[str]:
    names: list[str] = []
    seen: set[str] = set()
    for raw in path.read_text(encoding="utf-8").splitlines():
        line = raw.strip()
        if not line or line.startswith("#"):
            continue
        if line.startswith("[") and line.endswith("]"):
            continue
        if line in seen:
            continue
        seen.add(line)
        names.append(line)
    return names


def main() -> int:
    parser = argparse.ArgumentParser(
        description="used_templates 中有、objects.svg 全文搜不到的名称（字面量）"
    )
    parser.add_argument("--log", type=Path, default=DEFAULT_LOG, help="used_templates 日志")
    parser.add_argument(
        "--svg",
        type=Path,
        default=DEFAULT_SVG,
        help="objects.svg（默认 templates 材质库；导出地图用 Assets/map/objects.svg）",
    )
    parser.add_argument("-o", "--output", type=Path, default=None, help="写入结果文件")
    args = parser.parse_args()

    if not args.log.is_file():
        print(f"错误: 找不到日志 {args.log}", file=sys.stderr)
        return 1
    if not args.svg.is_file():
        print(f"错误: 找不到 SVG {args.svg}", file=sys.stderr)
        return 1

    names = parse_names_from_log(args.log)
    svg_text = args.svg.read_text(encoding="utf-8", errors="replace")

    missing: list[str] = []
    for name in names:
        if name not in svg_text:
            missing.append(name)

    lines = [
        "# 在 objects.svg 全文中未搜到的名称（字面量，区分大小写）",
        f"# log: {args.log}",
        f"# svg: {args.svg}",
        f"# 共 {len(names)} 个，未命中 {len(missing)} 个",
        "",
    ]
    lines.extend(missing if missing else ["(无)"])
    report = "\n".join(lines) + "\n"

    print(report, end="")
    if args.output:
        args.output.write_text(report, encoding="utf-8")
        print(f"已写入: {args.output}", file=sys.stderr)

    return 0 if not missing else 2


if __name__ == "__main__":
    raise SystemExit(main())
