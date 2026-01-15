using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class CanvasPrecisePickTool
{
    const float TransparentAlphaThreshold = 0.001f;

    static CanvasPrecisePickTool()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    static void OnSceneGUI(SceneView sceneView)
    {
        Event e = Event.current;

        if (e.type != EventType.MouseDown || e.button != 0)
            return;

        if (!e.alt)
            return;

        GameObject picked = PickTopmostVisibleUIGraphicUnderMouse(sceneView, e.mousePosition);

        if (picked != null)
        {
            Selection.activeGameObject = picked;
            EditorGUIUtility.PingObject(picked);
            e.Use();
        }
    }

    static GameObject PickTopmostVisibleUIGraphicUnderMouse(SceneView sceneView, Vector2 mousePosition)
    {
        Vector2 mouseScreen = HandleUtility.GUIPointToScreenPixelCoordinate(mousePosition);
        Ray worldRay = HandleUtility.GUIPointToWorldRay(mousePosition);

        Canvas[] canvases = FindAllCanvases();
        if (canvases == null || canvases.Length == 0)
            return null;

        GameObject bestPick = null;
        int bestLayerValue = int.MinValue;
        int bestSortingOrder = int.MinValue;
        int bestRenderOrder = int.MinValue;

        foreach (Canvas canvas in canvases)
        {
            if (canvas == null || !canvas.isActiveAndEnabled)
                continue;

            RectTransform canvasRT = canvas.transform as RectTransform;
            if (canvasRT == null)
                continue;

            Camera eventCamera = GetEventCameraForCanvas(canvas, sceneView);

            if (!RectTransformUtility.RectangleContainsScreenPoint(canvasRT, mouseScreen, eventCamera))
                continue;

            Graphic[] graphics = canvas.GetComponentsInChildren<Graphic>(true);
            if (graphics == null || graphics.Length == 0)
                continue;

            GameObject canvasPick = null;
            for (int i = graphics.Length - 1; i >= 0; i--)
            {
                Graphic graphic = graphics[i];
                if (graphic == null)
                    continue;

                if (graphic.canvas != canvas)
                    continue;

                if (!IsPickableUIGraphic(graphic, mouseScreen, eventCamera, worldRay))
                    continue;

                canvasPick = GetSelectionTarget(graphic.gameObject);
                break;
            }

            if (canvasPick == null)
                continue;

            int layerValue = SortingLayer.GetLayerValueFromID(canvas.sortingLayerID);
            int sortingOrder = canvas.sortingOrder;
            int renderOrder = canvas.rootCanvas != null ? canvas.rootCanvas.renderOrder : canvas.renderOrder;

            if (bestPick == null || IsBetter(layerValue, sortingOrder, renderOrder, bestLayerValue, bestSortingOrder, bestRenderOrder))
            {
                bestPick = canvasPick;
                bestLayerValue = layerValue;
                bestSortingOrder = sortingOrder;
                bestRenderOrder = renderOrder;
            }
        }
        return bestPick;
    }
    static bool IsBetter(
        int layerValue,
        int sortingOrder,
        int renderOrder,
        int bestLayerValue,
        int bestSortingOrder,
        int bestRenderOrder)
    {
        if (layerValue != bestLayerValue) return layerValue > bestLayerValue;
        if (sortingOrder != bestSortingOrder) return sortingOrder > bestSortingOrder;
        if (renderOrder != bestRenderOrder) return renderOrder > bestRenderOrder;
        return false;
    }
    static bool IsPickableUIGraphic(Graphic graphic, Vector2 mouseScreen, Camera eventCamera, Ray worldRay)
    {
        if (graphic == null)
            return false;

        if (!graphic.isActiveAndEnabled)
            return false;

        if (graphic.canvasRenderer != null && graphic.canvasRenderer.cull)
            return false;

        float effectiveAlpha = GetEffectiveAlpha(graphic);
        if (effectiveAlpha <= TransparentAlphaThreshold)
            return false;

        RectTransform rt = graphic.rectTransform;
        if (rt == null)
            return false;

        if (!RayIntersectsRectTransformQuad(worldRay, rt))
            return false;

        return true;
    }

    static bool RayIntersectsRectTransformQuad(Ray ray, RectTransform rt)
    {
        if (rt == null)
            return false;

        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);

        return RayIntersectsTriangle(ray, corners[0], corners[1], corners[2]) ||
               RayIntersectsTriangle(ray, corners[0], corners[2], corners[3]);
    }

    static bool RayIntersectsTriangle(Ray ray, Vector3 v0, Vector3 v1, Vector3 v2)
    {
        const float epsilon = 1e-6f;
        Vector3 edge1 = v1 - v0;
        Vector3 edge2 = v2 - v0;
        Vector3 pvec = Vector3.Cross(ray.direction, edge2);
        float det = Vector3.Dot(edge1, pvec);
        if (det > -epsilon && det < epsilon)
            return false;

        float invDet = 1.0f / det;
        Vector3 tvec = ray.origin - v0;
        float u = Vector3.Dot(tvec, pvec) * invDet;
        if (u < 0f || u > 1f)
            return false;

        Vector3 qvec = Vector3.Cross(tvec, edge1);
        float v = Vector3.Dot(ray.direction, qvec) * invDet;
        if (v < 0f || u + v > 1f)
            return false;

        float t = Vector3.Dot(edge2, qvec) * invDet;
        return t >= 0f;
    }

    static bool IsDrawnAfter(Transform a, Transform b, Transform canvasRoot)
    {
        if (a == b)
            return false;

        if (a == null || b == null || canvasRoot == null)
            return false;

        if (!a.IsChildOf(canvasRoot) || !b.IsChildOf(canvasRoot))
            return false;

        System.Collections.Generic.List<int> pathA = new System.Collections.Generic.List<int>(16);
        System.Collections.Generic.List<int> pathB = new System.Collections.Generic.List<int>(16);

        BuildSiblingIndexPath(a, canvasRoot, pathA);
        BuildSiblingIndexPath(b, canvasRoot, pathB);

        int min = pathA.Count < pathB.Count ? pathA.Count : pathB.Count;
        for (int i = 0; i < min; i++)
        {
            int da = pathA[i];
            int db = pathB[i];
            if (da != db)
                return da > db;
        }
        return pathA.Count > pathB.Count;
    }

    static void BuildSiblingIndexPath(Transform t, Transform root, System.Collections.Generic.List<int> path)
    {
        path.Clear();
        Transform current = t;
        while (current != null && current != root)
        {
            path.Add(current.GetSiblingIndex());
            current = current.parent;
        }
        path.Reverse();
    }

    static float GetEffectiveAlpha(Graphic graphic)
    {
        if (graphic == null)
            return 0f;

        float alpha = graphic.color.a;
        Transform t = graphic.transform;
        while (t != null)
        {
            CanvasGroup cg = t.GetComponent<CanvasGroup>();
            if (cg != null)
                alpha *= cg.alpha;
            t = t.parent;
        }
        return alpha;
    }
    static Camera GetEventCameraForCanvas(Canvas canvas, SceneView sceneView)
    {
        if (canvas == null)
            return null;

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return sceneView != null ? sceneView.camera : null;

        if (canvas.worldCamera != null)
            return canvas.worldCamera;

        return sceneView != null ? sceneView.camera : null;
    }

    static GameObject GetSelectionTarget(GameObject graphicObject)
    {
        if (graphicObject == null)
            return null;

        Selectable selectable = graphicObject.GetComponentInParent<Selectable>();
        if (selectable != null)
            return selectable.gameObject;

        return graphicObject;
    }
    static Canvas[] FindAllCanvases()
    {
        #if UNITY_2023_1_OR_NEWER
            return Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        #else
            return Object.FindObjectsOfType<Canvas>(true);
        #endif
    }
}
