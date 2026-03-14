using UnityEngine;

public class VRPaintBrush : MonoBehaviour
{
    [Header("Ustawienia Pędzla")]
    public Transform brushTip;
    public float brushReach = 0.2f;
    public int brushSize = 5; // Grubość pędzla w pikselach
    public Color paintColor = Color.red;

    [Header("Ustawienia Płótna (Rozdzielczość)")]
    public int textureWidth = 1024;
    public int textureHeight = 1024;

    private Texture2D canvasTexture;
    private Renderer canvasRenderer;
    private bool isDrawing = false;
    private Vector2 lastDrawPos;

    void Update()
    {
        // Puszczamy laser z pędzla
        if (Physics.Raycast(brushTip.position, brushTip.forward, out RaycastHit hit, brushReach))
        {
            if (hit.collider.CompareTag("Canvas"))
            {
                // 1. INICJALIZACJA TEKSTURY (tylko za pierwszym uderzeniem w dane płótno)
                if (canvasTexture == null || canvasRenderer != hit.collider.GetComponent<Renderer>())
                {
                    SetupCanvas(hit.collider.GetComponent<Renderer>());
                }

                // 2. PROJEKCJA 3D na 2D (To odpowiada Twojemu mapowaniu w Godocie!)
                // hit.textureCoord zwraca nam pozycję uderzenia od 0.0 do 1.0 (X i Y)
                Vector2 uvPos = hit.textureCoord;
                
                // 3. KONWERSJA NA PIKSELE
                int pixelX = (int)(uvPos.x * textureWidth);
                int pixelY = (int)(uvPos.y * textureHeight);
                Vector2 currentPos = new Vector2(pixelX, pixelY);

                // 4. RYSOWANIE LINII (lub pojedynczego punktu, jeśli to początek)
                if (!isDrawing)
                {
                    DrawPoint(pixelX, pixelY); // Zaczynamy nowe pociągnięcie (is_new_stroke z Godota)
                    isDrawing = true;
                }
                else
                {
                    DrawLine(lastDrawPos, currentPos); // Łączymy stary punkt z nowym
                }

                lastDrawPos = currentPos;
                
                // 5. AKTUALIZACJA TEKSTURY (To odpowiada queue_redraw() w Godocie)
                canvasTexture.Apply(); 
            }
            else
            {
                isDrawing = false; // Pędzel dotyka czegoś innego
            }
        }
        else
        {
            isDrawing = false; // Pędzel wisi w powietrzu
        }
    }

    // --- FUNKCJE POMOCNICZE ---

    // Tworzy czystą teksturę na płótnie
    void SetupCanvas(Renderer rend)
    {
        canvasRenderer = rend;
        // Tworzymy nową, czystą teksturę 2D
        canvasTexture = new Texture2D(textureWidth, textureHeight);
        
        // Wypełniamy ją na biało (lub przezroczysto)
        Color[] whitePixels = new Color[textureWidth * textureHeight];
        for (int i = 0; i < whitePixels.Length; i++) whitePixels[i] = Color.white;
        canvasTexture.SetPixels(whitePixels);
        canvasTexture.Apply();

        // Podmieniamy główną teksturę materiału płótna na naszą nową
        canvasRenderer.material.mainTexture = canvasTexture;
    }

    // Rysuje linię między dwoma pikselami (Algorytm Bresenhama)
    void DrawLine(Vector2 start, Vector2 end)
    {
        int x0 = (int)start.x;
        int y0 = (int)start.y;
        int x1 = (int)end.x;
        int y1 = (int)end.y;

        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
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

    // Maluje "kwadratowy" ślad pędzla wokół danego piksela
    void DrawPoint(int x, int y)
    {
        for (int i = -brushSize; i <= brushSize; i++)
        {
            for (int j = -brushSize; j <= brushSize; j++)
            {
                // Zabezpieczenie przed wyjściem poza teksturę
                if (x + i >= 0 && x + i < textureWidth && y + j >= 0 && y + j < textureHeight)
                {
                    canvasTexture.SetPixel(x + i, y + j, paintColor);
                }
            }
        }
    }
}