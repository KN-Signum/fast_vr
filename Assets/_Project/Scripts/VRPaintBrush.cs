using UnityEngine;

public class VRPaintBrush : MonoBehaviour
{
    [Header("Ustawienia Pędzla")]
    public Transform brushTip;
    public float brushReach = 0.2f;
    public int brushSize = 5;
    public Color paintColor = Color.red;

    [Header("Ustawienia Płótna")]
    public Renderer targetCanvas; // <--- NOWE: Tutaj przypiszemy nasze płótno!
    public Texture2D startingImage;
    public int textureWidth = 1024;
    public int textureHeight = 1024;

    private Texture2D canvasTexture;
    private bool isDrawing = false;
    private Vector2 lastDrawPos;

    void Start()
    {
        // Inicjalizujemy płótno OD RAZU po starcie gry, zanim gracz w ogóle ruszy ręką
        if (targetCanvas != null)
        {
            SetupCanvas(targetCanvas);
        }
        else
        {
            Debug.LogWarning("Hej! Zapomniałeś przypisać Target Canvas w skrypcie pędzla!");
        }
    }

    void Update()
    {
        if (Physics.Raycast(brushTip.position, brushTip.forward, out RaycastHit hit, brushReach))
        {
            // Sprawdzamy, czy uderzyliśmy w płótno ORAZ czy to jest to konkretne płótno
            if (hit.collider.CompareTag("Canvas") && hit.collider.GetComponent<Renderer>() == targetCanvas)
            {
                Vector2 uvPos = hit.textureCoord;
                int pixelX = (int)(uvPos.x * textureWidth);
                int pixelY = (int)(uvPos.y * textureHeight);
                Vector2 currentPos = new Vector2(pixelX, pixelY);

                if (!isDrawing)
                {
                    DrawPoint(pixelX, pixelY);
                    isDrawing = true;
                }
                else
                {
                    DrawLine(lastDrawPos, currentPos);
                }

                lastDrawPos = currentPos;
                canvasTexture.Apply(); 
            }
            else isDrawing = false;
        }
        else isDrawing = false;
    }

    void SetupCanvas(Renderer rend)
    {
        if (startingImage != null)
        {
            textureWidth = startingImage.width;
            textureHeight = startingImage.height;
            canvasTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
            canvasTexture.SetPixels(startingImage.GetPixels());
        }
        else 
        {
            canvasTexture = new Texture2D(textureWidth, textureHeight);
            Color[] whitePixels = new Color[textureWidth * textureHeight];
            for (int i = 0; i < whitePixels.Length; i++) whitePixels[i] = Color.white;
            canvasTexture.SetPixels(whitePixels);
        }

        canvasTexture.Apply();
        rend.material.mainTexture = canvasTexture;
    }

    void DrawLine(Vector2 start, Vector2 end)
    {
        int x0 = (int)start.x; int y0 = (int)start.y;
        int x1 = (int)end.x; int y1 = (int)end.y;
        int dx = Mathf.Abs(x1 - x0); int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1; int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            DrawPoint(x0, y0);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 < dx) { err += dx; y0 += sy; }
        }
    }

    void DrawPoint(int x, int y)
    {
        for (int i = -brushSize; i <= brushSize; i++)
        {
            for (int j = -brushSize; j <= brushSize; j++)
            {
                if (x + i >= 0 && x + i < textureWidth && y + j >= 0 && y + j < textureHeight)
                {
                    canvasTexture.SetPixel(x + i, y + j, paintColor);
                }
            }
        }
    }
}