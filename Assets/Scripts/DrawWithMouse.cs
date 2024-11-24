using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

enum DrawMode
{
    Pen,
    Eraser
}

enum Shape
{
    Dot,
    Square,
    Circle
}

public class DrawWithMouse : MonoBehaviour
{
    [Header("Properties")]
    [SerializeField] private float minDistance = 0.1f;

    [Header("Prefabs")]
    [SerializeField] private GameObject linePrefab;

    [Header("Colors")]
    [SerializeField] private FlexibleColorPicker penFcp;
    [SerializeField] private Slider widthSlider;
    [SerializeField] private FlexibleColorPicker bgFcp;

    [Header("Shape")] 
    [SerializeField] private Image shapeImage;
    [SerializeField] private Sprite dotSprite;
    [SerializeField] private Sprite squareSprite;
    [SerializeField] private Sprite circleSprite;

    [Header("Draw Mode")]
    [SerializeField] private Image drawModeImage;
    [SerializeField] private Sprite penSprite;
    [SerializeField] private Sprite eraserSprite;
    
    [Header("UI Elements")]
    [SerializeField] private GameObject[] uiElements; // Array to hold UI elements to hide
    
    private DrawMode currentDrawMode = DrawMode.Pen;
    private Shape currentShape = Shape.Dot;

    private LineRenderer currentLine;
    private Vector3 previousPosition;
    private Vector3 initialPosition;
    private Camera mainCamera;
    private GraphicRaycaster graphicRaycaster;
    private PointerEventData pointerEventData;
    private EventSystem eventSystem;
    private List<GameObject> lines = new List<GameObject>();
    private Stack<GameObject> undoStack = new Stack<GameObject>();
    private Stack<GameObject> redoStack = new Stack<GameObject>();
    private List<GameObject> eraserLines = new List<GameObject>();
    private Color currentColor = Color.black;
    private float currentWidth = 0.1f;
    private int deletedLines = 0;

    private void Awake()
    {
        mainCamera = Camera.main;
        eventSystem = EventSystem.current;
        graphicRaycaster = FindObjectOfType<GraphicRaycaster>();
    }

    private void Start()
    {
        widthSlider.value = linePrefab.GetComponent<LineRenderer>().startWidth;
        shapeImage.sprite = dotSprite;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            initialPosition = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            initialPosition.z = 0f;
            if (!IsPointerOverUI())
            {
                if (currentDrawMode == DrawMode.Pen)
                {
                    StartNewLine(currentColor);
                }
                else if (currentDrawMode == DrawMode.Eraser)
                {
                    StartNewLine(bgFcp.color);
                }
                redoStack.Clear(); // Clear the redo stack on a new action
            }
        }

        if (Input.GetMouseButton(0) && currentLine != null)
        {
            Vector3 currentPosition = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            currentPosition.z = 0f;
            
            if (Vector3.Distance(currentPosition, previousPosition) > minDistance)
            {
                switch (currentShape)
                {
                    case Shape.Square:
                        DrawSquare(initialPosition, currentPosition);
                        break;
                    case Shape.Circle:
                        DrawCircle(initialPosition, currentPosition);
                        break;
                    default:
                        currentLine.positionCount++;
                        currentLine.SetPosition(currentLine.positionCount - 1, currentPosition);
                        previousPosition = currentPosition;
                        break;
                }
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            currentLine = null;
        }
    }
    
    private void DrawSquare(Vector3 start, Vector3 end)
    {
        float width = end.x - start.x;
        float height = end.y - start.y;

        Vector3[] corners = new Vector3[]
        {
            new Vector3(start.x, start.y, start.z),
            new Vector3(start.x, start.y + height, start.z),
            new Vector3(start.x + width, start.y + height, start.z),
            new Vector3(start.x + width, start.y, start.z),
            new Vector3(start.x, start.y, start.z)
        };

        currentLine.positionCount = corners.Length;
        currentLine.SetPositions(corners);
    }
    
    private void DrawCircle(Vector3 start, Vector3 end)
    {
        float radius = Vector3.Distance(start, end) / 2;
        int segments = 36;
        float angleStep = 360f / segments;

        Vector3 center = (start + end) / 2;
        Vector3[] points = new Vector3[segments + 1];

        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Deg2Rad * i * angleStep;
            points[i] = new Vector3(
                center.x + Mathf.Cos(angle) * radius,
                center.y + Mathf.Sin(angle) * radius,
                center.z
            );
        }

        currentLine.positionCount = points.Length;
        currentLine.SetPositions(points);
    }

    private void StartNewLine(Color color)
    {
        GameObject newLineObject = Instantiate(linePrefab);

        if (currentDrawMode == DrawMode.Pen)
        {
            lines.Add(newLineObject);
        }
        else
        {
            eraserLines.Add(newLineObject);
            lines.Add(newLineObject);
        }
        
        currentLine = newLineObject.GetComponent<LineRenderer>();
        currentLine.positionCount = 0;  // Start with no points
        previousPosition = Vector3.positiveInfinity; // Use a large initial value to ensure the first point is always set
        
        currentLine.startColor = currentLine.endColor = color;
        currentLine.startWidth = currentLine.endWidth = currentWidth;
    }

    private bool IsPointerOverUI()
    {
        pointerEventData = new PointerEventData(eventSystem);
        pointerEventData.position = Input.mousePosition;
        List<RaycastResult> results = new List<RaycastResult>();
        graphicRaycaster.Raycast(pointerEventData, results);
        return results.Count > 0;
    }

    public void Undo()
    {
        if (lines.Count > 0)
        {
            deletedLines = 1;
            GameObject lastLine = lines[lines.Count - 1];
            undoStack.Push(lastLine);
            lines.RemoveAt(lines.Count - 1);
            lastLine.SetActive(false); // Deactivate instead of destroying for Redo functionality
        }
    }

    public void Redo()
    {
        if (undoStack.Count > 0)
        {
            for (int i = 0; i < deletedLines; i++)
            {            
                GameObject lastUndoneLine = undoStack.Pop();
                redoStack.Push(lastUndoneLine);
                lines.Add(lastUndoneLine);
                lastUndoneLine.SetActive(true); // Reactivate the line
            }

            deletedLines = 1;
        }
    }

    public void DeleteAll()
    {
        /*
        foreach (GameObject line in lines)
        {
            Destroy(line);
        }
        foreach (GameObject line in eraserLines)
        {
            Destroy(line);
        }
        lines.Clear();
        eraserLines.Clear();
        undoStack.Clear();
        redoStack.Clear();
        */
        
        if (lines.Count > 0)
        {
            deletedLines = 0;
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                deletedLines++;
                GameObject lastLine = lines[i];
                undoStack.Push(lastLine);
                lines.RemoveAt(i);
                lastLine.SetActive(false); // Deactivate instead of destroying for Redo functionality
            }
        }
    }

    public void ApplyPenColorAndWidth()
    {
        currentColor = penFcp.color;
        currentWidth = widthSlider.value;
    }

    public void ApplyBgColor()
    {
        mainCamera.backgroundColor = bgFcp.color;
        foreach (GameObject eraserLine in eraserLines)
        {
            LineRenderer lineRenderer = eraserLine.GetComponent<LineRenderer>();
            if (lineRenderer != null)
            {
                lineRenderer.startColor = lineRenderer.endColor = bgFcp.color;
            }
        }
    }
    
    public void SwitchDrawMode()
    {
        currentDrawMode = currentDrawMode == DrawMode.Pen ? DrawMode.Eraser : DrawMode.Pen;
        drawModeImage.sprite = currentDrawMode == DrawMode.Pen ? penSprite : eraserSprite;
    }
    
    public void SetShapeToDot()
    {
        currentShape = Shape.Dot;
        shapeImage.sprite = dotSprite;
    }

    public void SetShapeToSquare()
    {
        currentShape = Shape.Square;
        shapeImage.sprite = squareSprite;
    }

    public void SetShapeToCircle()
    {
        currentShape = Shape.Circle;
        shapeImage.sprite = circleSprite;
    }
    
    public void SaveImage()
    {
        StartCoroutine(CaptureAndSave());
    }
    
    private IEnumerator CaptureAndSave()
    {
        // Hide UI elements
        foreach (GameObject uiElement in uiElements)
        {
            uiElement.SetActive(false);
        }
        
        yield return new WaitForEndOfFrame();

        // Создаем RenderTexture
        RenderTexture renderTexture = new RenderTexture(Screen.width, Screen.height, 24);
        mainCamera.targetTexture = renderTexture;
        mainCamera.Render();

        // Создаем Texture2D из RenderTexture
        RenderTexture.active = renderTexture;
        Texture2D texture2D = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        texture2D.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        texture2D.Apply();

        // Сбрасываем настройки камеры и RenderTexture
        mainCamera.targetTexture = null;
        RenderTexture.active = null;
        Destroy(renderTexture);

        // Конвертируем изображение в PNG или JPG
        byte[] bytes = texture2D.EncodeToPNG(); // Для JPG используйте EncodeToJPG()
        Destroy(texture2D);

        // Сохраняем файл
        string directoryPath = Application.persistentDataPath;
        string fileName = $"Drawing_{DateTime.Now.ToString("dd.MM.yyyy_HH-mm-ss")}.png"; // Для JPG используйте .jpg
        string filePath = Path.Combine(directoryPath, fileName);

        // Создаем директорию, если она не существует
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        File.WriteAllBytes(filePath, bytes);

        Debug.Log($"Image saved at {filePath}");
        
        // Show UI elements again
        foreach (GameObject uiElement in uiElements)
        {
            uiElement.SetActive(true);
        }
    }
}