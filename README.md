基本功能（被认为）全部完成

接下来的几个周到几年时间会被用于生活质量更新

等待RWR2

## Codex 与 Unity MCP

本项目固定使用 MCP for Unity 10.0.0，Unity 打开后会自动启动本机服务并连接。
服务地址为 http://127.0.0.1:8088/mcp；Codex 配置在 .codex/config.toml。

首次接入后，在 VS Code 执行 Developer: Reload Window，并新建 Codex 会话以加载 MCP 工具。
保持 Unity 项目打开。手动连接或断开：Unity 菜单 Tools > Codex。

VS Code 的 Terminal > Run Task 提供以下任务：

- Unity MCP: Start server：恢复本机服务。
- Unity MCP: Check connection：读取当前场景、检查控制台错误。
- Unity MCP: Install dependencies：新电脑或新检出项目时下载固定版本源码并安装依赖，需要 git、uv 和网络。

也可以在项目根目录运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/UnityMcp/Setup-UnityMcp.ps1
.\.unity-mcp-source\Server\.venv\Scripts\python.exe Tools/UnityMcp/Check-UnityMcp.py
```

为验证编辑能力，在检查命令末尾添加 --smoke。它会在临时附加场景创建立方体、
添加 Rigidbody 并清理，不保存或替换当前场景；请在编辑模式运行。

日志在 Library/UnityMcp/server.stdout.log 和 server.stderr.log。
Python 服务源码和虚拟环境位于已忽略的 .unity-mcp-source/。
接入 MCP 提供的编辑器功能取决于插件接口与 Codex 权限，不代表任意桌面窗口都能自动操作。

## Masterserver collaboration

See [deployment and branch collaboration guide](MasterServer/README.md) for user configuration, Docker deployment, branch push/pull/merge, and mapitem author IDs.
