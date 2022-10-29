using System.ComponentModel;
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using System;

namespace Scopa.Editor
{
    public class HotspotEditor : EditorWindow
    {

        [MenuItem("Scopa/Hotspot Atlas Editor", false, 999)]
        private static void ShowWindow()
        {
            var window = GetWindow<HotspotEditor>();
            window.titleContent = new GUIContent("HotspotEditor");
            window.Show();
        }

        public static ScopaMaterialConfig editingHotspotTexture;

        [OnOpenAssetAttribute(1)]
        public static bool OpenHotspotTexture(int instanceID, int line)
        {
            var obj = EditorUtility.InstanceIDToObject(instanceID);
            if (obj is ScopaMaterialConfig)
            {
                UpdateTarget(obj as ScopaMaterialConfig);
                ShowWindow();
                return true;
            }
            return false;
        }

        private static void UpdateTarget(ScopaMaterialConfig target)
        {
            editingHotspotTexture = target;
            size = editingHotspotTexture.hotspotTexture.width;
            Init();
        }

        private static void Init()
        {
            zoomGridSize = new int[6];
            gridLevel = 0;
            var value = 8;
            for (int i = 0; i < zoomGridSize.Length; i++)
            {
                while (size % value != 0) value++;
                zoomGridSize[i] = value;
                value++;
            }
        }

        float zoomScale = 1.0f;
        int grid = 8;
        Vector2 panPosition = new Vector2(0, 21);

        private void OnEnable()
        {
            Undo.undoRedoPerformed += Repaint;
            wantsMouseMove = true;
        }
        private void OnDisable()
        {
            Undo.undoRedoPerformed -= Repaint;
        }


        [NonSerialized] private Rect toolWindowRect = new Rect(10, 0, 260f, 0f);

        private static int size = 512;//1024 / 2;
        private static int[] zoomGridSize;
        private static int gridLevel = 0;
        private Rect textureRect = new Rect(1, 1, size, size);
        private Vector2 _zoomCoordsOrigin = Vector2.zero;
        private Rect _zoomArea;
        private int gridStep;
        private bool consumeNextMouseInteraction;
        private void OnGUI()
        {
            if (zoomGridSize != null)
                grid = zoomGridSize[gridLevel];

            gridStep = size / grid;
            _zoomArea = new Rect(0f, 0, position.width, position.height);
            consumeNextMouseInteraction = toolWindowRect.Contains(Event.current.mousePosition);

            EditorZoomArea.Begin(zoomScale, _zoomArea);
            var drawRect = new Rect(-_zoomCoordsOrigin.x, -_zoomCoordsOrigin.y, size, size);
            if (editingHotspotTexture)
                EditorGUI.DrawPreviewTexture(drawRect, editingHotspotTexture.hotspotTexture, null, ScaleMode.StretchToFill, 0);
            DrawGrid(gridStep, drawRect);
            HandleRectCreate(drawRect);
            EditorZoomArea.End();

            HandleZoom();
            ToolWindow();
        }

        private void DrawGrid(int gridStep, Rect textureRect)
        {
            var tempColor = Handles.color;
            Handles.color = new Color(1, 1, 1, 0.7f);
            for (int i = 0; i < size + gridStep; i += gridStep)
            {
                Handles.DrawLine(new Vector2(textureRect.x + i, textureRect.y), new Vector2(textureRect.x + i, textureRect.y + size));
            }
            for (int i = 0; i < size + gridStep; i += gridStep)
            {
                Handles.DrawLine(new Vector2(textureRect.x, textureRect.y + i), new Vector2(textureRect.x + size, textureRect.y + i));
            }
            Handles.color = tempColor;
        }

        private void ToolWindow()
        {
            toolWindowRect.y = position.height - toolWindowRect.height - 10;
            BeginWindows();
            toolWindowRect = GUILayout.Window(32, toolWindowRect, DrawWindow, "Tool");
            EndWindows();
        }

        [NonSerialized] private Vector2 downPosition;
        [NonSerialized] private bool isDown;
        private Vector2 ConvertScreenCoordsToZoomCoords(Vector2 screenCoords)
        {
            return (screenCoords - _zoomArea.TopLeft()) / zoomScale + _zoomCoordsOrigin;
        }

        struct HandlesColorScope : IDisposable
        {
            public Color previousColor;

            public HandlesColorScope(Color color)
            {
                this.previousColor = Handles.color;
                Handles.color = color;
            }

            public void Dispose()
            {
                Handles.color = this.previousColor;
            }
        }

        private void HandleRectCreate(Rect textureRect)
        {
            var e = Event.current;

            if (editingHotspotTexture)
            {
                UnityEngine.Random.InitState(3412);
                foreach (var rect in editingHotspotTexture.rects)
                {
                    var drawRect = new Rect(rect);
                    drawRect.position -= _zoomCoordsOrigin;
                    using (new HandlesColorScope(Color.green))
                    {
                        Handles.DrawLine(drawRect.TopLeft(), drawRect.TopRight());
                        Handles.DrawLine(drawRect.TopLeft(), drawRect.BottomLeft());
                        Handles.DrawLine(drawRect.BottomLeft(), drawRect.BottomRight());
                        Handles.DrawLine(drawRect.BottomRight(), drawRect.TopRight());
                    }
                    var c = new Color(UnityEngine.Random.Range(0, 1f), UnityEngine.Random.Range(0, 1f), UnityEngine.Random.Range(0, 1f));
                    c.a = 0.3f;
                    EditorGUI.DrawRect(drawRect, c);
                }
            }

            var snappedPosition = SnapToGrid(e.mousePosition + _zoomCoordsOrigin, gridStep, gridStep) - _zoomCoordsOrigin;
            if (editingHotspotTexture && isDown)
            {
                var currentMouse = snappedPosition;
                var down = SnapToGrid(downPosition + _zoomCoordsOrigin, gridStep, gridStep) - _zoomCoordsOrigin;

                var width = currentMouse.x - downPosition.x;
                var height = currentMouse.y - downPosition.y;
                var rect = new Rect(down.x, down.y, width, height);

                // rect.position = SnapToGrid(rect.position + _zoomCoordsOrigin, gridStep, gridStep) - _zoomCoordsOrigin;
                rect.width = SnapToGrid(rect.width, gridStep);
                rect.height = SnapToGrid(rect.height, gridStep);

                if (rect.width != 0 && rect.height != 0)
                {
                    using (new HandlesColorScope(Color.cyan))
                    {
                        Handles.DrawLine(rect.TopLeft(), rect.TopRight());
                        Handles.DrawLine(rect.TopLeft(), rect.BottomLeft());
                        Handles.DrawLine(rect.BottomLeft(), rect.BottomRight());
                        Handles.DrawLine(rect.BottomRight(), rect.TopRight());
                        // Handles.DrawSolidDisc(rect.BottomRight(), Vector3.forward, 5 / zoomScale);
                    }

                    Repaint();

                    if (e.type == EventType.MouseLeaveWindow || e.type == EventType.MouseUp)
                    {
                        isDown = false;

                        var rect2 = new Rect(rect);
                        rect2.center += _zoomCoordsOrigin;

                        rect2.position = SnapToGrid(rect2.position, gridStep, gridStep);
                        rect2.width = SnapToGrid(rect2.width, gridStep);
                        rect2.height = SnapToGrid(rect2.height, gridStep);

                        // Debug.Log(rect2);
                        Undo.RecordObject(editingHotspotTexture, "Add Rect");
                        editingHotspotTexture.rects.Add(rect2);
                    }
                }
            }
            // Debug.Log(e.type);
            int controlId = GUIUtility.GetControlID(FocusType.Passive);

            using (new HandlesColorScope(new Color(0, 1, 1, 0.5f)))
                Handles.DrawSolidDisc(snappedPosition, Vector3.forward, 5 / zoomScale);

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (Event.current.button == 0 && !consumeNextMouseInteraction)
                    {
                        isDown = true;
                        downPosition = Event.current.mousePosition;
                        GUIUtility.hotControl = controlId;
                        e.Use();
                    }
                    break;
                case EventType.MouseMove:
                    Repaint();
                    break;
            }
            if (GUIUtility.hotControl == controlId && Event.current.rawType == EventType.MouseUp)
            {
                isDown = false;
            }
        }

        private Vector2 SnapToGrid(Vector2 p, int xSize, int ySize)
        {
            var x = Math.Round(p.x / xSize) * xSize;
            var y = Math.Round(p.y / ySize) * ySize;
            return new Vector2((int)x, (int)y);
        }

        private int SnapToGrid(float p, int size)
        {
            var x = Math.Round(p / size) * size;
            return (int)x;
        }

        private void HandleZoom()
        {
            var e = Event.current;
            switch (e.type)
            {
                case EventType.ScrollWheel:
                    Vector2 screenCoordsMousePos = Event.current.mousePosition;
                    Vector2 delta = Event.current.delta;
                    Vector2 zoomCoordsMousePos = ConvertScreenCoordsToZoomCoords(screenCoordsMousePos);
                    float zoomDelta = -delta.y / 50.0f;
                    float oldZoom = zoomScale;
                    zoomScale += zoomDelta;
                    zoomScale = Mathf.Clamp(zoomScale, 1 / 25f, 5f);
                    _zoomCoordsOrigin += (zoomCoordsMousePos - _zoomCoordsOrigin) - (oldZoom / zoomScale) * (zoomCoordsMousePos - _zoomCoordsOrigin);

                    e.Use();
                    break;
                case EventType.MouseDrag:
                    if (Event.current.button == 2)
                    {
                        Vector2 delta2 = Event.current.delta;
                        delta2 /= zoomScale;
                        _zoomCoordsOrigin -= delta2;
                        e.Use();
                    }
                    break;
            }
        }

        private void DrawWindow(int id)
        {
            using (new EditorGUI.DisabledGroupScope(true))
                EditorGUILayout.IntField("Grid Level", grid);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("<"))
                {
                    gridLevel--;
                }
                if (GUILayout.Button(">"))
                {
                    gridLevel++;
                }
                if (zoomGridSize != null)
                {
                    gridLevel = Mathf.Clamp(gridLevel, 0, zoomGridSize.Length - 1);
                    grid = zoomGridSize[gridLevel];
                }
            }
        }
    }
    
    // Thanks to http://martinecker.com/martincodes/unity-editor-window-zooming/
    // Helper Rect extension methods
    public class EditorZoomArea
    {
        private const float kEditorWindowTabHeight = 21.0f;
        private static Matrix4x4 _prevGuiMatrix;

        public static Rect Begin(float zoomScale, Rect screenCoordsArea)
        {
            GUI.EndGroup();

            Rect clippedArea = screenCoordsArea.ScaleSizeBy(1.0f / zoomScale, screenCoordsArea.TopLeft());
            clippedArea.y += kEditorWindowTabHeight;
            GUI.BeginGroup(clippedArea);

            _prevGuiMatrix = GUI.matrix;
            Matrix4x4 translation = Matrix4x4.TRS(clippedArea.TopLeft(), Quaternion.identity, Vector3.one);
            Matrix4x4 scale = Matrix4x4.Scale(new Vector3(zoomScale, zoomScale, 1.0f));
            GUI.matrix = translation * scale * translation.inverse * GUI.matrix;

            return clippedArea;
        }

        public static void End()
        {
            GUI.matrix = _prevGuiMatrix;
            GUI.EndGroup();
            GUI.BeginGroup(new Rect(0.0f, kEditorWindowTabHeight, Screen.width, Screen.height));
        }
    }
}