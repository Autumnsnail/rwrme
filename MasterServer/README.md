# Masterserver 分支协作

这是独立的地图快照仓库，采用 Git 式的提交、分支、快进和三方合并语义，不是 Git 协议服务器，也不连接 GitHub。同步单位是地图目录内的文件；不包含 Unity 场景、预制体、`.meta` 和本地连接凭据。模板和游戏素材需由协作者使用相同版本。

## 本机启动

需要 Python 3.10 或更新版本，无第三方 Python 依赖。在项目根目录执行：

```powershell
Copy-Item MasterServer/config.example.json MasterServer/config.json
python -c "import secrets; print(secrets.token_hex(32))"
```

为每位协作者分别生成令牌，在 `config.json` 中设置：

- `projects`：允许的项目 ID（例如 `my-map`）。
- `users`：用户 ID -> 用户令牌、可访问的项目列表。同一项目的用户可以创建分支、推送和合并。
- `max_snapshot_bytes`：每个快照的最大解码字节数，默认 64 MiB；Unity 客户端当前上限同为 64 MiB、4096 文件。

用户 ID 限 1–64 个 ASCII 字母、数字、点、下划线、连字符，首字符为字母或数字。用户 ID 一旦用于协作应保持稳定；重启服务以应用用户/权限配置变更。`config.json` 和数据库均已加入本目录的忽略规则。

```powershell
python MasterServer/server.py --config MasterServer/config.json --database MasterServer/data/repository.sqlite3
```

默认监听 `127.0.0.1:8090`。`GET /health` 返回健康状态。配置中的每个项目自动创建空的 `main` 分支。

## 部署

```sh
docker build -t rwrme-masterserver MasterServer
docker volume create rwrme-maps
docker run -d --name rwrme-masterserver --restart unless-stopped \
  -p 127.0.0.1:8090:8090 \
  -v "$PWD/MasterServer/config.json:/config/config.json:ro" \
  -v rwrme-maps:/data \
  rwrme-masterserver
```

客户端允许任意远程地址使用 HTTP 或 HTTPS，Unity 项目设置允许正式构建使用 HTTP。服务器地址省略协议时默认 HTTP，例如 `mc3.rhymc.com:35469` 会使用 `http://mc3.rhymc.com:35469`；显式填写 `https://` 仍使用 HTTPS。服务端默认提供 HTTP，也可通过 HTTPS 反向代理连接 8090 端口。代理需允许约 130 MiB 的请求体及至少 120 秒超时。配置文件应仅由部署管理员读取。镜像以 UID 10001 运行；若使用宿主目录代替命名卷，确保其对 UID 10001 可写。备份服务数据时先停止服务再备份整个数据目录（包含 SQLite WAL 文件），或者使用 SQLite 在线备份 API。

## 编辑器/游戏中使用

运行地图编辑场景后，`OnlyUI` 左下角原有三个按钮上方增加 **协作 / 历史**。节点图、连接设置及确认框均来自可编辑的 Unity UI 预制体资产，沿用原有按钮尺寸、精灵、灰色弹窗和共享 Canvas 缩放规则。

1. 点击 **连接设置**，填写服务器 URL、项目 ID、用户 ID、对应令牌。令牌仅保存在会话内存中。
2. 默认面板以提交节点图为主，显示说明、短 ID、作者、时间、末端节点以及真实的分叉/合并连线；当前基础节点有绿色外圈。单击仅选择，双击或点击 **载入所选节点** 才改变地图。历史较多时点击 **加载更早提交**。
3. 编辑后点击 **提交地图**。提交始终基于当前已载入地图的基础节点，不取尚未载入的选中节点，也不自动改用远程最新节点。历史节点可以直接提交，无需先创建或命名分支。
4. 基础节点没有后续提交时，服务端直接续接新节点；如果任意工作线上已经有该节点的后续，就从原基础节点自动开叉，保留原有后续。两人同时从同一节点提交时，先完成者续接，后完成者自动分叉。提交成功后自动定位到自己的新节点，之后可继续正常提交。
5. 切换前保存当前地图到本地工作副本；各工作线和历史节点独立缓存。返回时恢复尚未提交的修改。**获取最新节点** 才明确使用远程版本替换当前副本，替换前先备份；**返回工作副本** 返回节点跳转前所在工作线的副本。
6. 合并时，先单击节点并点击 **标记合并 A**，再单击另一个节点并点击 **合并 A 与所选节点**。A 在节点图上用橙色外圈标记；再次点击同一标记按钮可取消。两个节点均无需先载入本地地图。
7. 合并向导显示 `A + B → 新节点`、共同基础、自动处理文件数和冲突列表。SVG 冲突按元素 ID 三栏对照共同基础、A、B；变化属性带圆点。可逐项 **采用 A / B / 共同基础**、**删除此项**，或 **手动修改** XML/文本后点击 **应用手动修改**。手动元素修改必须保留原 ID，可单独指定父图层；PNG 文件显示图片对照，其他二进制文件显示大小及摘要并选择版本。较大的图片或文本仅显示摘要，仍可选择原版本。
8. 每次取舍后由服务端重新校验，所有冲突解决后才启用 **创建并载入合并节点**。合并总会生成新节点，父节点为 A、B，原节点、后续节点及原工作线指向均保留；即使一方是另一方的祖先也不快进。当前未提交修改先另存工作副本，不参与这次合并。网络重试不会重复创建同一次合并；若节点已经创建但载入失败，可继续载入同一结果。
9. 日常无需管理分支。需要时展开 **工作线管理**，可以搜索、切换或手动创建命名工作线。

例如已有 `A → B`，另一位协作者仍基于 `A` 编辑并提交 `C` 时，结果为 `A → B` 与 `A → C`。`C` 的父节点仍是 `A`，不会覆盖 `B`，也不会擅自把 `B` 的内容带入 `C`。

工作副本位于 Unity `Application.persistentDataPath/collaboration/workspaces/`，按地图目录、服务器、项目、工作线和历史节点隔离。自动分叉成功后，已提交的源快照会标记为已提交，避免再次恢复为待提交修改；其他工作副本不受影响。覆盖文件前的完整备份位于 `collaboration/backups/`。`.meta` 原样保留；地图载入失败时显示重新载入按钮，阻止旧地图状态覆盖新文件。

自动分叉的检查、快照写入及工作线更新在同一服务端事务内完成。原有显式 `push` API 继续检查 `expectedHead`；新客户端使用 `submit`。这些操作不切换本机源码的 Git 分支。

UI 资产与维护入口：

- `Assets/Resources/UI/CollaborationPanel.prefab`：完整控件层级、分支行与提交节点模板、连线组件及 Inspector 引用。
- `Assets/Resources/UI/CollaborationFont.asset`：独立中文字形图集，不依赖用户机器的系统字体文件。
- 当前场景内的实例：`OnlyUI/CollaborationUI`。
- Unity 菜单 `Tools > Rwrme > Rebuild Collaboration UI`：通过 Unity API 重新生成预制体，保留资产 GUID。
- Unity 菜单 `Tools > Rwrme > Install Collaboration UI In Open Scene`：在无用户未保存修改的场景中安装 UI。
- `CollaborationUiSmokeTest.Run()`：在独立预览场景验证控件引用、可选工作线管理、提交图、节点跳转、历史节点直接提交、已提交缓存隔离、合并向导、三栏对照、PNG 预览及字体，并生成 `artifacts/collaboration-ui.png` / `artifacts/collaboration-merge.png`；不切换用户运行模式，也不保存测试场景。

`objects.svg` 使用按 SVG 元素 **ID** 的三方合并，所有人都可以修改同一文件：

- 双方修改不同 ID 的元素，或分别新增、删除不同元素时，自动合并。
- 同一 ID 的元素双方改成相同内容时直接接受；改成不同内容（包括不同属性）或一方删除、另一方修改时，返回冲突。
- 元素移动到不同图层会作为该元素的修改处理；图层的子元素列表单独合并，不会因为其他元素增删而使整层冲突。
- `desc`、`title` 属于对应元素的数据。旧导出器按列表位置生成的描述 ID、平台分组 ID 和建筑/基地标签会规范化，避免无关元素因导出序号变化产生冲突。
- XML 缩进、属性顺序和等价命名空间前缀变化不算修改；合并结果保留图层结构和元素顺序。元素冲突逐 ID 处理；顺序、父子关系或 XML 结构冲突在向导中明确标记为完整 SVG 文件冲突，需要选择完整版本或手动修正。选择完整文件时应核对其中其他元素。无效 XML 或重复 ID 不允许提交。

冲突返回如 `objects.svg#building42rwrme_u_alice_...` 的具体元素 ID，目标分支及数据库保持原样。PNG 等其他文件仍按文件执行三方合并。

## mapitem ID

所有创建路径（工具、复制粘贴、预制件、导入时替换无效/重复 ID）统一调用 `MetaMap.getNewItemId`：

```text
building42rwrme_u_alice_8cb51a170b7b42c0922b278f48c4602f
```

格式为旧前缀/序号 + `rwrme_u_用户ID_随机UUID`。追加 UUID 能避免同一用户在多台机器或不同分支中生成相同 ID。离线未配置身份时使用持久化的 `local-UUID` 用户 ID；连接远程时仍须显式填写已登记的用户 ID。现有合法且不重复的 ID 完整保留，包括旧版不带用户后缀的 ID。

## API 与测试

`POST /v1/repository`，JSON 请求必须包含 `operation`、`project`、`userId`、`branch`，HTTP 头为 `Authorization: Bearer <该用户的令牌>`。

- `connect`：返回分支列表（name、head、parent）和目标分支 head；parent 表示创建来源。
- `history`：可选 `limit`（1–500，默认 200）、`cursor`（上页 `nextCursor`）。返回该项目的 `commits: [{id, parents, author, message, created}]`、`branches` 和 `nextCursor`（空字符串表示结束）；`created` 为 Unix 秒，合并提交包含两个父节点。
- `create_branch`：`newBranch`、可选 `revision`，从指定提交创建；未传 `revision` 时从当前 `branch` 的 head 创建。
- `submit`：必须显式传 `baseRevision`（已载入基础节点的完整 ID，空字符串表示尚无基础节点）、`message`、`files: [{path, content}]`。服务端检查整个项目中基础节点是否已有子提交，有则自动分叉，无则沿该节点所在末端继续。返回 `head`、实际 `branch`、`baseRevision` 和 `forked`；客户端必须采用返回的工作线及新基准，不能假定还是原工作线。content 为 Base64；快照为完整文件列表，缺少的旧文件视为删除。
- `push`：兼容原调用方，使用 `expectedHead` 检查目标工作线是否变化，过期仍返回 409。新节点提交流程使用 `submit`。
- `pull`：可选 `revision`，读取指定节点。返回 `head`（远程分支头）、`revision`（实际读取节点）和完整文件列表；读取历史不会修改分支头。
- `merge_preview`：`leftRevision`、`rightRevision`、可选 `resolutions`。只读计算并返回 `merge: {left,right,ancestor,previewId,unresolved,automaticFiles,fileCount,conflicts}`。冲突项包含不透明 `key`、`path`、`element`、`kind`、`resolved`、`choice`。
- `merge_detail`：相同节点、当前 `resolutions`、`previewId`、`conflictKey`。返回 `mergeDetail`，含共同基础、A、B 的存在状态、属性、文本或 PNG 预览及是否允许手动编辑/删除。
- `merge_commit`：相同节点、`previewId`、`resolutions`、`message`、32 位十六进制 `requestId`。`resolutions` 为 `[{key,choice,content,parent}]`；choice 是 `left/right/base/delete/custom`，自定义 content 是 UTF-8 文本的 Base64（最大 1 MiB），parent 用于手动 SVG 元素的父图层。必须解决所有冲突。服务端重新计算和校验后，在新的内部工作线上创建 `[leftRevision,rightRevision]` 双父节点，返回 `head` 和 `branch`；使用同一 requestId 重试相同请求返回原结果，改变请求内容会被拒绝。
- `merge`：保留原有显式分支调用兼容性，使用 `expectedHead`、`sourceBranch`；新可视化节点合并使用上面的三个接口。

更新现有部署时同步本目录的 `server.py` / `svg_merge.py` / `merge_plan.py` 并重启服务，保留原 `config.json` 与数据库。节点 UI 需要支持 `history` / `revision` / `submit` / `merge_preview` / `merge_detail` / `merge_commit` 的新版服务端。旧服务端不支持 `submit` 时客户端提示升级，不会退回覆盖式推送。

提交记录保存作者、说明、时间和父提交；内容按 SHA-256 去重，分支更新和内容写入通过 SQLite 事务原子提交。路径遍历、大小写重复路径和跨项目访问被拒绝。

```powershell
python -m unittest discover -s MasterServer -v
```
