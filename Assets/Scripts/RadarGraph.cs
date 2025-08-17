using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class RadarGraph : MaskableGraphic
{
    [Range(0, 1f)] public float stream, voltage, freeze, chaos, air = 0f;
    [Header("Look")]
    public float radius = 80f;
    public float outline = 2f;
    public Color outlineColor = new Color(1,1,1,0.2f);

    public void SetValues(float stream, float voltage, float freeze, float chaos, float air)
    {
        this.stream = Mathf.Clamp01(stream);
        this.voltage = Mathf.Clamp01(voltage);
        this.freeze = Mathf.Clamp01(freeze);
        this.chaos = Mathf.Clamp01(chaos);
        this.air = Mathf.Clamp01(air);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        // 5 axes around a circle (0 at top, clockwise)
        float[] v = { stream, voltage, freeze, chaos, air };
        const int N = 5;
        Vector2[] axes = new Vector2[N];

        for (int i = 0; i < N; i++)
        {
            float ang = Mathf.Deg2Rad * (90f - i * 360f / N);
            axes[i] = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
        }

        // outline star
        for (int i = 0; i < N; i++)
            AddLine(vh, Vector2.zero, axes[i] * radius, outlineColor, outline);

        // polygon fill
        int start = vh.currentVertCount;
        for (int i = 0; i < N; i++)
        {
            Vector2 p = axes[i] * (radius * Mathf.Clamp01(v[i]));
            UIVertex vert = UIVertex.simpleVert;
            vert.color = color;
            vert.position = p;
            vh.AddVert(vert);
        }

        // center
        UIVertex c = UIVertex.simpleVert; c.color = color; c.position = Vector2.zero;
        int ci = vh.currentVertCount;
        vh.AddVert(c);

        for (int i = 0; i < N; i++)
        {
            int a = start + i;
            int b = start + ((i + 1) % N);
            vh.AddTriangle(ci, a, b);
        }
    }

    void AddLine(VertexHelper vh, Vector2 a, Vector2 b, Color col, float thick)
    {
        Vector2 n = (b - a).normalized;
        Vector2 t = new Vector2(-n.y, n.x) * (thick * 0.5f);
        int v0 = vh.currentVertCount;

        UIVertex V(Vector2 p) { var v = UIVertex.simpleVert; v.color = col; v.position = p; return v; }

        vh.AddVert(V(a - t)); // 0
        vh.AddVert(V(a + t)); // 1
        vh.AddVert(V(b + t)); // 2
        vh.AddVert(V(b - t)); // 3
        vh.AddTriangle(v0 + 0, v0 + 1, v0 + 2);
        vh.AddTriangle(v0 + 2, v0 + 3, v0 + 0);
    }
}
