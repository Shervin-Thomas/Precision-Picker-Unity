# Editor Tools

## Canvas Precise Pick Tool

**File:** `CanvasPrecisePickTool.cs`

This editor-only utility improves Scene view selection for Unity UI.

### Problem it solves
When UI contains overlapping panels/graphics (including transparent panels), Unity’s default Scene view clicking can select the outer panel or the canvas instead of the *visible* UI element you actually clicked.

This tool performs hit-testing against UI `RectTransform` geometry and selects the **topmost visible UI element under the cursor**.

### How to use
- In the **Scene** view, hold **Alt** (left or right) and **Left Click**.
- The clicked UI element becomes the active selection.

Notes:
- The click is only “consumed” when a UI element is found, so normal Scene view Alt-navigation still works when you click empty space.

### What it selects
- Works with most UI elements that have a `Graphic` (e.g., `Image`, `RawImage`, `Text`, `TMP` graphics, etc.).
- If the clicked visual is inside a `Selectable` (`Button`, `Toggle`, `Slider`, etc.), the tool selects the `Selectable` parent object.

### How it works (high-level)
- Collects `Canvas` objects in the scene.
- For each canvas, scans UI `Graphic` components in **reverse draw order** (topmost first).
- For each candidate, checks:
  - Active/enabled
  - Not culled
  - Effective alpha (includes parent `CanvasGroup`)
  - A SceneView-safe ray/quad hit test against the `RectTransform` world corners
- The first hit in that order is selected.

### Configuration
- `TransparentAlphaThreshold` controls what counts as “transparent enough” to ignore.

### Known limitations / optional enhancements
- This tool does not currently do per-pixel alpha hit testing for sprites (it uses rect geometry).
- If you use `Mask`/`RectMask2D` and want clicks to respect clipped/visible regions, the tool can be extended to include mask clipping checks.
- If you want to explicitly ignore certain container panels (by name/tag/layer/component), a filter can be added to skip those graphics.

### Installation / location
Place `CanvasPrecisePickTool.cs` under an `Assets/Editor/` folder (like it is now). Unity loads it automatically because it is an editor script.
