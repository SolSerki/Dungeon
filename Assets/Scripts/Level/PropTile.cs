using UnityEngine;
using UnityEngine.Tilemaps;

// Tile normal que además copia el espejado/rotación del tile al GameObject que
// instancia (Unity solo lo posiciona en la celda). Así la luz de una antorcha
// volteada queda del lado correcto.
// Opcionalmente anima el sprite (como un AnimatedTile) sin perder el prefab
// instanciado: AnimatedTile de Tilemap Extras no tiene campo de GameObject.
[CreateAssetMenu(menuName = "2D/Tiles/Prop Tile")]
public class PropTile : Tile
{
    [Header("Animación (opcional)")]
    [Tooltip("Frames de la animación. Vacío = sprite estático.")]
    [SerializeField] private Sprite[] animatedSprites;
    [Tooltip("Frames por segundo.")]
    [SerializeField, Min(0.01f)] private float animationSpeed = 12f;
    [Tooltip("Arranca cada celda en un frame distinto para que no titilen todas a la vez.")]
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

    // Hash de la celda: el desfase es aleatorio entre celdas pero estable entre
    // recargas (no cambia cada vez que se refresca el tilemap).
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

    // matrix.rotation no sirve con espejados: devuelve un giro de 180° en Y que,
    // sumado a la escala negativa, anula el flip. Se descompone a mano en 2D:
    // espejado en X + giro en Z.
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
