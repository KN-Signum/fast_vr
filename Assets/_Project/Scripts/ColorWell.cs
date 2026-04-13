using UnityEngine;

[RequireComponent(typeof(Renderer))] 
public class ColorWell : MonoBehaviour
{
    public Color wellColor;

    private void Start()
    {
        Renderer rend = GetComponent<Renderer>();
        if (rend != null)
        {
            wellColor = rend.material.color;
        }
    }
}