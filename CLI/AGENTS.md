# rwrme 地图 CLI 工作区

- 使用工具前读 [README.md](README.md)，特别是坐标、`pre_` 底图和导出说明。
- 入口：`python CLI/rwrmap.py --session CLI/.work/<任务名>.rwrmap <命令>`。这里的路径以仓库根为当前目录；也可使用绝对路径，从任意工作目录启动。
- 每个 agent/任务使用独立会话。先 `map-read` 和 `template-read`，二者都成功后才能编辑。
- 常用命令：`templates-list`、`items-list`、`item-get`、`item-add`、`item-edit`、`item-delete`、`pixel-get`、`pixel-set`、`pixel-patch`、`batch`、`export`、`preview`。
- 命令返回 UTF-8 JSON；检查退出码和 `ok`。列表默认分页 100 条，`--limit 0` 返回全部；跟随 `next_offset` 读取后续页。
- 创建实例前从已加载模板清单取得真实名称，禁止猜测模板名。`building` 是材质模板，创建建筑必须提供占地 `--width`、`--depth`。
- 实例位置使用 SVG 坐标；像素命令默认以 PNG 左上角为原点。地层道路权重为 `255-A`，可以直接使用 `--channel road`。
- 像素编辑写 `pre_`；导出和预览会烘焙 `height_paths`/`material_paths`。路径覆盖区域的最终像素不一定等于底图像素，使用 `pixel-get --stage baked` 检查。
- 输出放 `CLI/.work/` 或其他新的非 Assets 目录。不要直接改 Unity 资源、覆盖源地图、修改 `.meta` 或保存用户场景。需导入 Unity 时遵守上级 AGENTS.md 的 Editor API 约定。
- 修改工具后运行 `python -m unittest discover -s CLI/tests -v`；真实样例验收运行 `python CLI/tests/verify_real_map.py`。
- `.work/` 是忽略的运行产物目录；不要提交会话、测试地图或生成预览。
