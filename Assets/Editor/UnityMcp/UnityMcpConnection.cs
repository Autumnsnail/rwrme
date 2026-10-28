using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Services;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

[InitializeOnLoad]
internal static class UnityMcpConnection
{
    private const string DisabledKey = "Rwrme.UnityMcp.Disabled";
    private static bool connecting;

    static UnityMcpConnection()
    {
        if (!Application.isBatchMode && !EditorPrefs.GetBool(DisabledKey, false))
            EditorApplication.update += ConnectWhenReady;
    }

    private static void ConnectWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        EditorApplication.update -= ConnectWhenReady;
        Connect();
    }

    [MenuItem("Tools/Codex/Connect Unity MCP")]
    private static async void Connect()
    {
        if (connecting) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.update += ConnectWhenReady;
            return;
        }
        connecting = true;
        try
        {
            EditorPrefs.SetBool(DisabledKey, false);
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string script = Path.Combine(projectRoot, "Tools/UnityMcp/Start-UnityMcp.ps1");
            using (var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + script + "\"",
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                CreateNoWindow = true
            }))
            {
                while (!process.HasExited) await Task.Delay(250);
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("MCP server startup failed. Run Tools/UnityMcp/Start-UnityMcp.ps1 for details.");
            }
            EditorConfigurationCache.Instance.SetUseHttpTransport(true);
            EditorConfigurationCache.Instance.SetHttpTransportScope("local");
            HttpEndpointUtility.SaveLocalBaseUrl("http://127.0.0.1:8088");
            if (!MCPServiceLocator.Bridge.IsRunning && !await MCPServiceLocator.Bridge.StartAsync())
                throw new InvalidOperationException("Unity MCP bridge failed to connect.");
            var result = await MCPServiceLocator.Bridge.VerifyAsync();
            if (!result.Success) throw new InvalidOperationException(result.Message);
            Debug.Log("[Codex] Unity MCP connected at http://127.0.0.1:8088/mcp");
        }
        catch (Exception exception) { Debug.LogWarning("[Codex] " + exception.Message); }
        finally { connecting = false; }
    }

    [MenuItem("Tools/Codex/Disconnect Unity MCP")]
    private static async void Disconnect()
    {
        EditorPrefs.SetBool(DisabledKey, true);
        await MCPServiceLocator.Bridge.StopAsync();
    }
}
