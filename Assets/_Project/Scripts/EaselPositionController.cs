using UnityEngine;

public sealed class EaselPositionController : MonoBehaviour
{
    [SerializeField] private Transform easelRoot;
    [SerializeField, Min(0.01f)] private float movementStep = 0.05f;
    [SerializeField, Min(0f)] private float horizontalLimit = 0.4f;
    [SerializeField, Min(0f)] private float verticalLimit = 0.35f;

    private Vector3 _initialPosition;

    private void Awake()
    {
        if (easelRoot == null)
        {
            Debug.LogError("EaselPositionController has no easel root assigned.");
            enabled = false;
            return;
        }

        _initialPosition = easelRoot.position;
    }

    public void MoveLeft() => Move(-movementStep, 0f);

    public void MoveRight() => Move(movementStep, 0f);

    public void MoveUp() => Move(0f, movementStep);

    public void MoveDown() => Move(0f, -movementStep);

    private void Move(float horizontalDelta, float verticalDelta)
    {
        if (easelRoot == null)
            return;

        Vector3 position = easelRoot.position;
        position.x = _initialPosition.x + Mathf.Clamp(
            position.x - _initialPosition.x + horizontalDelta,
            -horizontalLimit,
            horizontalLimit
        );
        position.y = _initialPosition.y + Mathf.Clamp(
            position.y - _initialPosition.y + verticalDelta,
            -verticalLimit,
            verticalLimit
        );
        easelRoot.position = position;
    }
}
