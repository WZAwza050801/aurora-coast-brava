import bpy

ADDON_MODULE = "blender_mcp"
MCP_PORT = 9876

bpy.ops.preferences.addon_enable(module=ADDON_MODULE)
scene = bpy.context.scene
scene.blendermcp_port = MCP_PORT
scene.blendermcp_auto_start_server = True


def start_mcp_server():
    current_scene = bpy.context.scene
    if current_scene is None:
        return 0.5
    if not current_scene.blendermcp_server_running:
        bpy.ops.blendermcp.start_server()
    print(
        "Aurora Blender MCP: running={}, port={}".format(
            current_scene.blendermcp_server_running,
            current_scene.blendermcp_port,
        )
    )
    return None


bpy.app.timers.register(start_mcp_server, first_interval=1.0)
bpy.ops.wm.save_userpref()
print("Aurora Blender MCP: add-on enabled and user preferences saved")
