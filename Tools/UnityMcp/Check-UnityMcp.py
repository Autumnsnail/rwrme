"""Check the project's MCP connection. Add --smoke for an isolated editor write test."""
import argparse
import asyncio
import json
from urllib.request import urlopen

from fastmcp import Client

URL = "http://127.0.0.1:8088"
SMOKE_CODE = """
if (EditorApplication.isPlayingOrWillChangePlaymode)
    throw new System.InvalidOperationException("Stop Play Mode before running the smoke test.");
var original = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var dirtyBefore = original.isDirty;
var rootsBefore = original.rootCount;
var sceneCountBefore = UnityEngine.SceneManagement.SceneManager.sceneCount;
var temporary = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
    UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
    UnityEditor.SceneManagement.NewSceneMode.Additive);
try
{
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(temporary);
    var cube = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube);
    cube.name = "__CodexConnectionSmokeTest__";
    var body = cube.AddComponent<UnityEngine.Rigidbody>();
    if (body == null || cube.scene != temporary)
        throw new System.InvalidOperationException("Object/component creation failed.");
    UnityEngine.Object.DestroyImmediate(cube);
}
finally
{
    UnityEngine.SceneManagement.SceneManager.SetActiveScene(original);
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(temporary, true);
}
if (original.isDirty != dirtyBefore || original.rootCount != rootsBefore ||
    UnityEngine.SceneManagement.SceneManager.sceneCount != sceneCountBefore)
    throw new System.InvalidOperationException("Scene state changed unexpectedly.");
return "PASS: temporary scene, cube, Rigidbody, deletion and cleanup; original scene preserved.";
"""

def unpack(result):
    if result.is_error:
        raise RuntimeError(str(result))
    data = result.data
    if data is None and result.content:
        data = json.loads(result.content[0].text)
    if isinstance(data, dict) and data.get("success") is False:
        raise RuntimeError(json.dumps(data, ensure_ascii=False))
    return data

async def main(smoke):
    with urlopen(URL + "/api/instances", timeout=5) as response:
        instances = json.load(response)["instances"]
    matches = [item for item in instances if item["project"] == "rwrme"]
    if len(matches) != 1:
        raise RuntimeError("Expected exactly one rwrme Unity instance; open the project and connect it.")
    instance = matches[0]
    async with Client(URL + "/mcp") as client:
        unpack(await client.call_tool("set_active_instance", {
            "instance": instance["project"] + "@" + instance["hash"]
        }))
        tools = await client.list_tools()
        print("MCP tools:", len(tools))
        before = unpack(await client.call_tool("manage_scene", {"action": "get_active"}))
        print("Active scene:", json.dumps(before, ensure_ascii=False))
        if smoke:
            result = unpack(await client.call_tool("execute_code", {
                "action": "execute", "code": SMOKE_CODE, "safety_checks": True
            }))
            print("Smoke test:", json.dumps(result, ensure_ascii=False))
            after = unpack(await client.call_tool("manage_scene", {"action": "get_active"}))
            if before["data"] != after["data"]:
                raise RuntimeError("Active scene state differs after smoke test.")
        errors = unpack(await client.call_tool("read_console", {
            "action": "get", "types": ["error"], "count": 10
        }))
        print("Console errors:", json.dumps(errors, ensure_ascii=False))
        if errors.get("data"):
            raise RuntimeError("Unity console contains errors; inspect them before continuing.")

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--smoke", action="store_true")
    asyncio.run(main(parser.parse_args().smoke))
