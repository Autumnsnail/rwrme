## 地图编辑工具与 UI 使用说明

本说明文档针对 `ToolController` 及相关工具类、`UIManager` 和 `ToolHinter`，面向关卡 / 地图设计师，帮助在 Unity 场景中使用编辑器进行地图编辑。

---

### 工具控制器（ToolController）

#### 核心概念

- **当前工具**：`ToolController.currentTool`，决定鼠标点击/拖拽操作的含义。
- **选中物体**：`ToolController.miSelected`，当前被选中的 `MapItem`（建筑、平台、Base 等）。
- **拖拽可视化**：`ToolController.dragVisualizer`，用于画框选/路径/平台轮廓的 `LineRenderer`。


---

### 工具列表与快捷键

#### 数字键 1–0、F1–F8 对应工具

- **1**：选择工具 `SelecterTool`
  - 点击 `MapItem` 进行选中。
- **2**：Pin 工具 `PinTool`
  - 将一个标记物体移动到点击位置（一般用于调试或标记）。
- **3**：建筑矩形绘制工具 `DrawerTool`
  - 拖出矩形，创建一个建筑 `Building`。
- **4**：屋顶切换工具 `RoofChangerTool`
  - 点击建筑，在有屋顶 / 无屋顶之间切换。
- **5**：材质修改工具 `MaterialChangerTool`
  - 配合 UI 下拉框，更改建筑/墙/平台的材质或模板。
- **6**：建筑高度调整工具 `heightChanger`
  - 点击建筑，每次高度 ±2（通过 `setHeightSetter` 控制正负）。
- **7**：墙绘制工具 `WallDrawer`
  - 逐点点击画出墙路径，按 Space 完成生成 `Wall`。
- **8**：平台绘制工具 `PlatformDrawer`
  - 先画第一条边，再画第二条边，按 Space 完成生成 `Platform`。
- **9**：平台类型切换工具 `PlatformTypeChanger`
  - 在普通 / deck / bridge 三种平台模式间循环。
- **0**：平台底座墙模板工具 `PlatformBasewallChanger`
  - 改变平台的 `base_wall_template`。
- **F1**：平台高度设置工具 `PlatformHeightSetter`
  - 按 UI 输入的高度直接设置平台 `height`。
- **F2**：Base 绘制工具 `BaseTool`
  - 拖出矩形，在 `baseLayer` 中创建一个 `Base` 区域。
- **F3**：物体散布工具 `ItemScatter`
  - 放置出生点、岩石、梯子、车辆、补给点、箱子等（需预先配置类型）。
- **F4**：矩形橡皮工具 `Eraser`
  - 拖出矩形区域，批量删除某一类型的 `MeRect` 物体。
- **F5**：Mesh 散布工具 `MeshScatter`
  - 按模板名称放置 `MeMesh`。
- **F6**：地表材质刷 `TerrainMaterialPainter`
  - 在 `_Mask` 贴图上画圆形区域，改变地形材质分布。
- **F7**：高度刷 `HeightBush`
  - 在范围内将高度平滑推向目标高度（可调范围、硬度）。
- **F8**：高度抹平工具 `HeightSmudge`
  - 随鼠标移动对高度进行“涂抹式”平滑。

---

### 全局快捷键

- **鼠标左键**
  - 按下：`currentTool.startUse(...)`
  - 拖动：`currentTool.OnDragging(...)`
  - 抬起：`currentTool.EndUse()`
- **Delete**
  - 删除当前选中对象 `miSelected`（从 `MetaMap` 和场景同时删除）。
- **G / R / S**
  - 控制 `SideTool` 的模式，对当前选中的 `MeRect` 做：
  - **G**：Grab（平移）
  - **R**：Rotate（旋转）
  - **S**：Scale（缩放）
- **Space**
  - 部分工具中用于“完成当前操作”（如 `WallDrawer`、`PlatformDrawer`）。
- **Esc**
  - 取消当前操作 / 清空临时路径（如 `WallDrawer`、`PlatformDrawer`）。
- **Ctrl + C / Ctrl + V**
  - **复制建筑**：Ctrl+C 复制当前选中的 `Building`（仅建筑有效）。
  - **粘贴建筑**：Ctrl+V 在鼠标位置生成一份新建筑：
    - 自动生成新 `id`。
    - 图层默认根据点击位置下方物体层级 + 1。
    - 保留高度、材质、尺寸、屋顶等属性。

---

### 常用编辑场景示例

#### 绘制一个新建筑

1. 按 **3** 切换到 `DrawerTool`。
2. 在地形上按住左键拖出一个矩形区域，松开鼠标：
   - 会自动创建一个 `Building`：
     - 尺寸 = 拖拽框尺寸；
     - 朝向由拖拽方向决定；
     - 默认高度和材质写死在脚本中，可后续修改。
3. 若需要调整材质：
   - 在 UI 中选择建筑材质下拉框（见下一节），然后使用材质工具点击建筑。

#### 绘制一段墙

1. 按 **7** 切换到 `WallDrawer`。
2. 在地面上多次点击，每次点击添加一个转折点。
3. 按 **Space**：
   - 生成一条 `Wall`，`positionLine` 由所有点击位置组成。
   - 使用默认墙模板（如 `"GardenWall1"`）。

#### 绘制一个平台（桥 / 高台）

1. 按 **8** 切换到 `PlatformDrawer`。
2. 第一阶段（drawing=1）：
   - 点击一串点，表示平台一侧边线。
   - 按 **Space** 切换到第二阶段。
3. 第二阶段（drawing=2）：
   - 再点击一串点，表示另一侧边线，第二段边线要求在第一段行进方向的右侧。
   - 再按 **Space** 完成，生成一个 `Platform`。
4. 如需修改平台类型 / 高度 / 墙模板：
   - 按 **9** 切换桥/平台模式。
   - 使用 `PlatformHeightSetter`（F1）和 UI 输入高度。
   - 使用 `PlatformBasewallChanger` 修改底墙模板。




### 工具提示（ToolHinter）

#### 功能

- 在鼠标旁显示当前工具名 `ToolController.inste.currentTool.m_name`，方便使用者知道现在处于哪种编辑模式。


### 常见使用顺序建议

1. 导入地图（见主文档）。
2. 调用 `UIManager.updatebBT()` / `updateWT()` / `updateMTD()` 刷新下拉列表。
3. 使用数字键 / 功能键选择合适的工具。
4. 使用鼠标在主画面编辑建筑、平台、墙、物体和地形。
5. 通过 UI 面板切换不同编辑模式、修改属性。
7. 使用 `MapExporter.exportMap()` 导出地图成果。



## Rank
Rank是用于标志MapItem在layer中的子图层的位置
  有依赖于类型的固定Rank与导入时传入的客制化Rank