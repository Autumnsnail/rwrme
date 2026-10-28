# rwrme 地图处理 CLI：agent 使用手册

这是独立 Python 工具，工作区为本目录 `CLI/`。读取 rwrme 使用的地图目录及 XML/SVG 模板，通过持久化会话编辑，再导出可被 rwrme 读取的地图源文件。工具不需要启动 Unity，不进入 Play Mode，也不修改原始地图或 Unity 场景。

## 1. 安装和启动

需要 Python 3.10+、Pillow、NumPy。已在 Python 3.11.9、Pillow 12.3.0、NumPy 2.4.6 上验证。

在仓库根目录执行：

```powershell
python -m pip install -r CLI/requirements.txt
python CLI/rwrmap.py --help
```

也可以在 `CLI` 工作区执行：

```powershell
python rwrmap.py --help
# 或
python -m rwrmap --help
```

Windows 提供 `CLI/rwrmap.cmd`。需要全局命令时可用 `python -m pip install -e CLI` 安装，然后调用 `rwrmap`。源码入口和安装入口功能相同；不安装包也可直接运行。依赖安装可能需要网络，工具执行本身不需要网络。

从任意工作目录调用时，用入口的绝对路径。`--session` 必须放在子命令前；其他输入/输出相对路径均相对于调用时的当前目录，不相对于会话文件。

## 2. 最短完整流程

下面命令在仓库根目录执行。会话名、导出目录必须由当前 agent 独占；示例导出目录如果已存在，请换一个名称。

```powershell
$mapCli = 'CLI/rwrmap.py'
$mapSession = 'CLI/.work/task-a.rwrmap'

python $mapCli --session $mapSession map-read Assets/map
python $mapCli --session $mapSession template-read Assets/templates
python $mapCli --session $mapSession status

python $mapCli --session $mapSession templates-list --type building --search BuildingFactory1
python $mapCli --session $mapSession item-add --type building --template BuildingFactory1 --id building_agent_a --x 1000 --y 1000 --width 40 --depth 30 --rotation 0 --layer 1 --set height=2

python $mapCli --session $mapSession pixel-set --target height --x 20 --y 30 --value 123
python $mapCli --session $mapSession pixel-set --target alpha --x 20 --y 30 --channel road --value 200
python $mapCli --session $mapSession items-list --type building --limit 0

python $mapCli --session $mapSession export CLI/.work/task-a-export --with-preview
```

`BuildingFactory1` 是本仓库真实模板名。其他模板文件可能没有它：务必先查询清单，再将返回的名称交给 `item-add`。

## 3. 会话与机器输出

每次成功编辑后，会话立即持久化，下一个 CLI 进程可继续使用。`.rwrmap` 是包含源地图快照、模板及版本信息的 ZIP 文件，不是游戏地图格式。`export` 才会产生普通地图目录。

- 必须同时成功执行 `map-read`、`template-read`，才允许创建、删除、移动、属性编辑或像素编辑；两种读取先后顺序不限。
- `templates-list` 只需要已读模板；`items-list`、`item-get`、`pixel-get`、`preview` 只需要已读地图。
- 会话默认路径为 `CLI/.work/default.rwrmap`。多个 agent 必须显式指定各自的 `--session`，避免混用。
- 会话带进程锁，写入使用临时文件与原子替换。失败命令不会提交部分编辑，`batch` 失败时整批不提交。
- 锁文件为 `<session>.lock`，包含 PID。异常杀进程可能遗留锁：先确认记录的进程已退出，再手工删除该锁文件，不要删除正在使用的锁。
- `map-read --replace` 会显式替换会话里的地图快照和地图编辑，但保留已加载模板。`template-read` 会替换已加载模板，不清空地图。

正常命令向 stdout 输出单行 UTF-8 JSON：

```json
{"ok":true,"command":"item-delete","revision":6,"result":{"deleted":"building_agent_a","type":"building"}}
```

错误退出码为 `2`，示例：

```json
{"ok":false,"error":{"code":"ValueError","message":"TEMPLATE_REQUIRED: run template-read first"}}
```

成功退出码 `0`，中断退出码 `130`。`--help`、`--version` 是面向人的文本输出。不要仅依据输出文本长度判断命令成功；检查退出码和 `ok`。

## 4. 读取地图和模板

```powershell
python CLI/rwrmap.py --session CLI/.work/a.rwrmap map-read Assets/map
python CLI/rwrmap.py --session CLI/.work/a.rwrmap template-read Assets/templates/vao0822.svg
```

地图读取接受目录或该目录里的 `objects.svg` 路径。目录必须包含：

| 文件 | 作用 |
| --- | --- |
| `objects.svg` | 图层、实例、路径、general 设置 |
| `terrain.cfg` | 高度图文件名、MaxHeight、PageWorldX、PageWorldZ |
| `Heightmap.image` 指向的 PNG | 8 位高度图，未配置时使用 `terrain5_heightmap.png` |
| `terrain5_combined_alpha.png` | 地层 RGBA 图 |
| `pre_terrain5_heightmap.png`（可选） | 可编辑高度底图；没有时从配置高度图初始化 |
| `pre_terrain5_combined_alpha.png`（可选） | 可编辑地层底图；没有时从 combined alpha 初始化 |

附带 XML、纹理、脚本及其他普通文件也会快照并导出。跳过 `.meta` 和隐藏路径；拒绝符号链接及超过 512 MiB 的快照，避免把工程根目录误当地图目录。现有源文件保持不变。

模板接受 `.xml`、`.svg` 或模板目录。目录选择规则与 `MapImporter.FindFirstTemplateInTemplatesDir` 一致：只看该目录直接包含的 XML/SVG 文件，按文件名不区分大小写排序，取第一个。必须存在根下 `inkscape:label="materials"` 的 `g` 层。

## 5. 查询可用模板

```powershell
python CLI/rwrmap.py --session CLI/.work/a.rwrmap templates-list --type mesh --search tree --limit 20
python CLI/rwrmap.py --session CLI/.work/a.rwrmap templates-list --type wall --limit 0
```

`--type` 支持：

| 类型 | rwrme 识别规则 | 用途 |
| --- | --- | --- |
| `mesh` | label 以 `#mesh` 开头 | 网格模板，包含依次解析的 `base_template` 继承 |
| `building` | label 以 `#material` 开头，desc 没有 `type` | 建筑材质；模板矩形本身是色板，不是建筑占地 |
| `wall` | label 以 `#wall_template` 开头 | 墙模板 |
| `decal` | id 以 `decal_template` 开头 | 贴花模板 |
| `post` | id 以 `post_template` 开头，含 `mesh_template` | 柱列模板 |
| `terrain` | label 以 `#terrain_layer` 开头，含 `alpha` | 地层定义，用于输出通道文件名；不作为实例创建 |

结果含 `name`、`type`、模板矩形 `size`、原始 `properties`、继承后的 `resolved`、`source_id`。不传 `--type` 返回所有已识别模板；`--search` 对名称做不区分大小写的包含查询。

与当前 rwrme 一样，网格只继承之前已出现的基础模板；后置基础模板会产生 warning，不会暗中改变导入顺序。原模板的全部材料 XML 会随导出嵌入，包括 CLI 未解释的定义。

列表默认 `--limit 100 --offset 0`，`next_offset` 为下一页；`--limit 0` 返回全部。地层文件默认映射位于结果 `alpha_files`，它与“模板里确实存在的 terrain 定义清单”分开返回。

## 6. 实例查询、创建、修改、删除

```powershell
python CLI/rwrmap.py --session CLI/.work/a.rwrmap items-list --limit 0
python CLI/rwrmap.py --session CLI/.work/a.rwrmap items-list --type mesh --layer layer1 --limit 50
python CLI/rwrmap.py --session CLI/.work/a.rwrmap item-get building_agent_a
```

实例列表包含类型、id、图层、desc 属性、SVG 属性和有效变换矩阵；矩形还包含世界 SVG 坐标的四角。支持 `--search` 搜索 id。建筑、平台、墙、柱列、网格、贴花、岩石、树、梯子、补给、箱子、出生点、载具、基地、越野路径、高度路径、地层路径均可列出。未识别形状以 `unknown` 列出并保留其 XML。

创建必须指定已读模板中的名称：

```powershell
# 网格默认占地来自模板（包括已解析的继承）
python CLI/rwrmap.py --session CLI/.work/a.rwrmap item-add --type mesh --template bush2 --x 1000 --y 1000 --layer 1

# 建筑必须显式指定 SVG 占地，height 是整数建筑参数
python CLI/rwrmap.py --session CLI/.work/a.rwrmap item-add --type building --template BuildingFactory1 --x 1000 --y 1000 --width 40 --depth 30 --set height=2 --set roof_type=flat

# 墙和柱列使用至少两个绝对 SVG 点；下面模板名须先在清单中确认
python CLI/rwrmap.py --session CLI/.work/a.rwrmap item-add --type wall --template RuinWall2 --points '[[100,200],[120,200],[120,240]]' --set height=3.8
```

`item-add` 支持 `mesh/building/wall/decal/post` 五类。这对应当前源码真正加载的实例模板库。平台、基地、载具等没有同样的实例模板库，本版本对这些已有实例提供读取、移动、属性编辑和删除，不虚构模板。

- 矩形参数：`--x`、`--y`、`--width`、`--depth`、`--rotation`。`depth` 写 SVG `height`，建筑楼高通过 `--set height=整数` 指定。
- 旋转沿用 `MathOfRwrme.angleToTransform`：矩阵角度为 `-rotation`，绕矩形起点旋转。
- 墙/柱列：`--points '[[x,y],...]'` 或 `--points @points.json`，输出 `m/l` 相对路径。
- `--layer` 为大于等于 1 的数字，写入 `layerN` 下对应的子组，例如 `meshs`、`buildings`、`walls`。
- `--id` 可选，不指定则生成唯一 ID。指定时须以类型名开头，防止 rwrme 的 ID 前缀识别误判。
- `--set KEY=VALUE` 可重复，写入 desc。模板引用由 `--template` 决定，不能用 `--set` 指向另一个名称。
- 贴花默认用模板矩形宽高，必要时显式给 `--width/--depth`。Unity 有纹理资源时可能按贴图或 atlas 单元宽高比调整贴花显示占地；CLI 不加载 Ogre 资源库。

移动和属性编辑：

```powershell
python CLI/rwrmap.py --session CLI/.work/a.rwrmap item-edit building_agent_a --dx 20 --dy -10 --set height=3
python CLI/rwrmap.py --session CLI/.work/a.rwrmap item-delete building_agent_a
```

`dx/dy` 使用绝对 SVG 轴，保留原变换的旋转/缩放。平台的两条边一起移动/删除，实例主 ID 沿用 rwrme 从第一条 start 边读取的 ID，也接受 end 边 ID。已有属性保留，`--set` 仅覆盖指定键；未知 ID、重复 ID 歧义和无效模板名会报错。对高度/地层路径的移动直接改路径坐标，保留三次曲线，不添加 rwrme 不识别的单路径 transform。

## 7. 像素读写

```powershell
python CLI/rwrmap.py --session CLI/.work/a.rwrmap pixel-get --target height --x 100 --y 200
python CLI/rwrmap.py --session CLI/.work/a.rwrmap pixel-set --target height --x 100 --y 200 --value 128
python CLI/rwrmap.py --session CLI/.work/a.rwrmap pixel-set --target height --x 100 --y 200 --meters 5

# 填充 16×8 像素矩形，包含左上起点
python CLI/rwrmap.py --session CLI/.work/a.rwrmap pixel-set --target alpha --x 100 --y 200 --width 16 --height 8 --value '[0,255,0,255]'
python CLI/rwrmap.py --session CLI/.work/a.rwrmap pixel-set --target alpha --x 100 --y 200 --channel road --value 220
```

像素值必须是 `0..255` 整数；高度用单个整数，地层用 `[R,G,B,A]`。`--channel r/g/b/a/road` 可只修改一个通道，其余通道逐像素保留。高度的 `--meters` 通过 `MaxHeight` 换算并量化到 8 位，读回时报告实际 `meters` 与 `normalized`。

| 地层含义 | RGBA 或权重 |
| --- | --- |
| base | `[0,0,0,255]` |
| sand | `[255,0,0,255]`，独立权重为 R |
| grass | `[0,255,0,255]`，独立权重为 G |
| asphalt | `[0,0,255,255]`，独立权重为 B |
| road | `[0,0,0,0]`，独立权重为 `255-A` |

读像素可用 `--stage pre/reference/baked`：

- `pre`（默认）：当前可编辑底图。
- `reference`：导入快照中的标准图，像素编辑不改变它。
- `baked`：当前 pre 加上路径后的结果，与导出使用同一烘焙函数。

任意离散像素批量修改：

```json
[
  {"x":100,"y":200,"value":128},
  {"x":101,"y":200,"value":129}
]
```

```powershell
python CLI/rwrmap.py --session CLI/.work/a.rwrmap pixel-patch --target height CLI/examples/height-patch.json
```

`pixel-patch` 的 JSON 是以上数组；地层单通道可在每项增加 `"channel":"road"`。整批先校验，再应用，越界或非法值不会留下部分编辑。同一像素出现多次时按数组顺序应用。

## 8. 坐标和路径烘焙细节

不要把 SVG 坐标直接当 PNG 像素坐标。所有像素命令默认 PNG 左上角原点，x 向右、y 向下；`--origin bottom-left` 对应 Unity `Texture2D.GetPixels` 的方向。地层图与高度图分辨率通常不同。

源码目前硬编码 SVG 基准为 2048。CLI 保留这项行为，不根据 SVG width 猜测另一套坐标：

```text
Unity X = SVG x / 2
Unity Z = (2048 - SVG y) / 2

高度图 Unity 索引：
hx = clamp(int(Unity X / PageWorldX * resolution), 0, resolution-1)
hy = clamp(int(Unity Z / PageWorldZ * resolution), 0, resolution-1)
PNG y = resolution - 1 - hy

地层图 Unity 像素（含源码中的 +512 偏移）：
px = (SVG x / 2) * imageWidth / 2048
py = ((2048 - SVG y) / 2 + 512) * imageHeight / 2048
对应整数行的 PNG y = imageHeight - 1 - int(py)
```

地层栅格采样使用像素中心；不要自行移除 `+512` 或改成整个 SVG 铺满地层 PNG。

导出路径遵循当前源码：

- 三次曲线每段 24 次采样，去除重合点。
- 高度路径使用平头、斜接折角的描边多边形，斜接长度上限为半宽的 8 倍。
- `height_paths` 中的 `height_delta/height/height_offset` 是 **0..1 高度域**，不是世界米数。`raise/lower` 作用于当前结果，`set` 写绝对值，`offset` 总是相对原 pre 底图；最后 clamp 到 0..1。
- 地层路径使用到折线的距离，权重为 `(1-distance/halfWidth)^hardness`，对 RGBA 一起插值。`material_index` 0–4 对应上表。
- rwrme 的高度/地层路径导入只应用所在组的 transform，忽略单条路径自己的 transform。CLI 的烘焙保持一致；带单路径 transform 的此类路径禁止通过 `item-edit` 移动，以免产生误导。
- pre 和 final 是分离的数据。再次导出从 pre 重新烘焙，不会把路径效果不断叠加。缺少 pre 的旧图，按 rwrme 规则从 final 初始化；如果 final 原先已带相同路径效果，用户应提供正确 pre 底图。

## 9. 导出和预览

```powershell
python CLI/rwrmap.py --session CLI/.work/a.rwrmap export CLI/.work/export-a --with-preview --preview-size 2048
python CLI/rwrmap.py --session CLI/.work/a.rwrmap preview CLI/.work/preview-a.png --size 1024 --overlay-items
```

导出到**新的目录**，拒绝覆盖既有目录、源地图、模板、会话或 Unity Assets。准备好全部内容后才发布输出目录。

导出包含：

- 保留原有实例、未知 XML、图层和 general 设置的 `objects.svg`，materials 层替换为本次加载的模板。每个实例的 ID 保留。
- 当前 `pre_` 高度/地层底图。
- 烘焙后的 `terrain5_heightmap.png`；如果 terrain.cfg 配置了不同高度文件名，也同步写入该文件，避免配置悬空。
- `terrain5_combined_alpha.png`，以及模板 index 1–4 指定的分层 PNG，默认是 sand/grass/asphalt/road；road 为反向 alpha。
- 原有 `terrain.cfg`、`map_config.xml`、其他附带文件。不会重建 map_config，从而保留未知配置项。
- `--with-preview` 重新生成 `map_view_ls.png`。没有该选项时，旧预览随快照复制，结果会明确提醒它未刷新。

预览是 CPU 俯视战术图，默认 2048×2048，支持 64–2048，内部 2 倍采样。采用 `MapExporter.exportMapViewPic` 的水色、B/A 地层遮罩、10 米等高线、基地框、树密度、平台、建筑、墙与岩石绘制顺序。默认水位为源码 `MetaTerrain.waterHeight=2` 米，独立 `preview` 命令可用 `--water-level` 覆盖；`--overlay-items` 给其他矩形实例增加橙色轮廓，便于检查位置。

预览不是 Unity 3D 截图；Pillow 的线段、边界滤波和树密度插值与 Unity 实现存在像素级差异，不承诺与 Unity 导出图逐字节相同。地形烘焙的 float32/量化边界也可能有 1 个灰度级差异。

本工具输出 rwrme 编辑器使用的地图源数据，不运行地图目录中的 RWR 批处理、不转换 Ogre 模型，也不生成游戏外部工具的派生包。导入 Unity 时请使用项目已有导入流程，并遵守上级 AGENTS.md。

## 10. 原子批处理

为减少多进程启动和重复保存，推荐 agent 把多项修改放进 JSON 数组，每项是一个子命令的 argv 数组，例如 [examples/edit-batch.json](examples/edit-batch.json)：

```powershell
python CLI/rwrmap.py --session CLI/.work/a.rwrmap batch CLI/examples/edit-batch.json
```

所有步骤共用同一会话，失败整批回滚，成功只提交一次。批内允许读取/查询/编辑，禁止嵌套 batch、切换会话、export、preview。文件输出应在批处理成功后单独执行。

参数采用 argv 数组，因此带空格的描述值不需要嵌套 shell 引号。文件路径仍相对于命令的当前工作目录。批处理中自定义 ID 建议固定且唯一，便于后续步骤引用。

## 11. 兼容边界与验收

- 支持标准 8 位 PNG 高度图；明确拒绝 16 位/浮点高度图，避免静默降精度。
- 高度图必须为方形，与 rwrme 当前栅格逻辑一致。pre 和对应 final 的尺寸必须一致。
- 路径支持 M/L/H/V/C/S/Q/T/Z 及相对形式。需要参与烘焙的未知路径命令会报错；不会静默丢掉路径后导出错误地图。
- list 会保留并列出其他 `layerN.*` 场景层中的对象；rwrme 默认只启用 `default` 后缀，实际可见性仍由原图层和游戏配置决定。
- 更换模板后，调用方要确认已有对象仍有相应模板/外部资源。CLI 不下载缺失模型或纹理。

自动测试：

```powershell
python -m unittest discover -s CLI/tests -v
python CLI/tests/verify_real_map.py
```

前者使用可独立计算结果的小地图，覆盖状态门禁、模板分类/继承、五类创建、成对平台删除、坐标、通道、路径模式、持久化、错误回滚、锁、导出保留和 PNG 预览。后者使用当前 `Assets/map` 与 `Assets/templates`，执行完整命令链，重新读取导出图，校验输入文件哈希未变化，生成 `.work/verification-*` 验收产物。

实现依据均来自本工作区源码：

| CLI 模块 | 参考源码 |
| --- | --- |
| `model.py` | `Assets/Scripts/MapImporter.cs` 的 `importTemplates/importObjects`；`MapExporter.cs` 的各类 SVG 写入 |
| `geometry.py` | `Assets/Scripts/others/MathOfRwrme.cs`、`SvgPathParser.cs`、`MetaMap.cs` 的 `MePathCurve` |
| `terrain.py` | `MetaMap.cs` 的 `TerrainPathRasterizer`；`MapExporter.cs` 的高度/地层导出 |
| `preview.py` | `MapExporter.cs` 的 `exportMapViewPic` 及相关绘制函数 |
| `session.py` | CLI 自有快照、锁和原子文件提交；不依赖 Unity 的活动场景 |
