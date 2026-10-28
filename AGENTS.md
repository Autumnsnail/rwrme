# Unity project

- Unity version: 2022.3.62f2. Preserve existing user edits.
- Unity MCP endpoint: http://127.0.0.1:8088/mcp. Select this project's instance before changing editor state; the project name is rwrme.
- Start/recover the server using Tools/UnityMcp/Start-UnityMcp.ps1. On a fresh checkout run Tools/UnityMcp/Setup-UnityMcp.ps1 first (requires git, uv and network).
- The Unity Editor connects automatically through Assets/Editor/UnityMcp/UnityMcpConnection.cs. Manual connection: Tools > Codex > Connect Unity MCP.
- Use MCP/Unity Editor APIs to edit scenes, prefabs, components and assets. Preserve .meta files and GUIDs. Do not edit Library, Temp, obj or generated project files as source.
- After C# changes, wait for Unity compilation and check console errors before editor operations. Avoid a second Unity process on the same project.
- Do not enter Play Mode, save over a user's dirty scene, or replace their current scene merely to test connectivity. Use a temporary additive scene for object-creation smoke tests and close it without saving.
- MCP is pinned to v10.0.0 / commit d49ae2953580f3481beb1e084a1da2682f0b5610. Keep the Unity package and Python server versions aligned.
