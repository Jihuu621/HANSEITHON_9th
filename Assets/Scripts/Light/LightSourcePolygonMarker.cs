using UnityEngine;

/// <summary>Creates a lightweight regular-polygon marker for a light source.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class LightSourcePolygonMarker : MonoBehaviour
{
    [SerializeField, Range(3, 8)] private int sides = 6;
    [SerializeField, Min(0.05f)] private float radius = 0.38f;
    [SerializeField] private Color color = new(0.08f, 0.09f, 0.11f, 1f);
    [SerializeField] private int sortingOrder = 25;

    private Mesh mesh;
    private Material material;

    private void Awake()
    {
        Build();
    }

    private void OnDestroy()
    {
        if (mesh != null)
            Destroy(mesh);
        if (material != null)
            Destroy(material);
    }

    private void Build()
    {
        MeshFilter filter = GetComponent<MeshFilter>();
        MeshRenderer renderer = GetComponent<MeshRenderer>();

        mesh = new Mesh { name = $"{sides}-Sided Light Marker" };
        Vector3[] vertices = new Vector3[sides + 1];
        int[] triangles = new int[sides * 3];
        vertices[0] = Vector3.zero;

        float startAngle = sides % 2 == 0 ? Mathf.PI / sides : Mathf.PI * 0.5f;
        for (int i = 0; i < sides; i++)
        {
            float angle = startAngle - Mathf.PI * 2f * i / sides;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;

            int triangle = i * 3;
            triangles[triangle] = 0;
            triangles[triangle + 1] = i + 1;
            triangles[triangle + 2] = (i + 1) % sides + 1;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        filter.sharedMesh = mesh;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        material = new Material(shader) { color = color };
        renderer.sharedMaterial = material;
        renderer.sortingOrder = sortingOrder;
    }
}
