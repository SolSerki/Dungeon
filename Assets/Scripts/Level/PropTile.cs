using UnityEngine;
using UnityEngine.Tilemaps;

// Regular tile that also copies the tile's flip/rotation to the GameObject it
// instantiates (Unity only positions it in the cell). That way a flipped torch's
// light ends up on the right side.
// Optionally animates the sprite (like an AnimatedTile) without losing the instantiated
// prefab: Tilemap Extras' AnimatedTile has no GameObject field.
[CreateAssetMenu(menuName = "2D/Tiles/Prop Tile")]
public class PropTile : Tile
{
    [Header("Animation (optional)")]
    [Tooltip("Animation frames. Empty = static sprite.")]
    [SerializeField] private Sprite[] animatedSprites;
    [Tooltip("Frames per second.")]
    [SerializeField, Min(0.01f)] private float animationSpeed = 12f;
    [Tooltip("Starts each cell on a different frame so they don't all flicker in sync.")]
    [SerializeField] private bool randomizeStartFrame = true;

    public override bool GetTileAnimationData(Vector3Int position, ITilemap tilemap, ref TileAnimationData tileAnimationData)
    {
        if (animatedSprites == null || animatedSprites.Length == 0)
            return false;

        tileAnimationData.animatedSprites = animatedSprites;
        tileAnimationData.animationSpeed = animationSpeed;
        tileAnimationData.animationStartTime = randomizeStartFrame
            ? StartFrameFor(position, animatedSprites.Length) / animationSpeed
            : 0f;
        return true;
    }

    // Cell hash: the offset is random across cells but stable across
    // reloads (it doesn't change every time the tilemap refreshes).
    private static int StartFrameFor(Vector3Int position, int frameCount)
    {
        unchecked
        {
            int h = position.x * 73856093 ^ position.y * 19349663 ^ position.z * 83492791;
            return (h & int.MaxValue) % frameCount;
        }
    }

    public override bool StartUp(Vector3Int position, ITilemap tilemap, GameObject go)
    {
        if (go != null)
            ApplyTileMatrix(go.transform, tilemap.GetTransformMatrix(position));
        return base.StartUp(position, tilemap, go);
    }

    // matrix.rotation doesn't work with flips: it returns a 180° turn around Y that,
    // combined with the negative scale, cancels the flip. It's decomposed by hand in 2D:
    // flip on X + rotation on Z.
    private static void ApplyTileMatrix(Transform target, Matrix4x4 matrix)
    {
        Vector2 axisX = matrix.GetColumn(0);
        Vector2 axisY = matrix.GetColumn(1);
        bool mirrored = axisX.x * axisY.y - axisX.y * axisY.x < 0f;
        if (mirrored) axisX = -axisX;

        float angle = Mathf.Atan2(axisX.y, axisX.x) * Mathf.Rad2Deg;
        target.localRotation = Quaternion.Euler(0f, 0f, angle);
        target.localScale = new Vector3(mirrored ? -axisX.magnitude : axisX.magnitude, axisY.magnitude, 1f);
    }
}
