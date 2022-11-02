using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class Raycast : MonoBehaviour
{
    public float MaxDistance = 100;
    public int Span = 360;
    public float ConnectThreshold = 50;
    public float CapTheta = 18;
    public float LineWidth = 0.08f;
    bool lastMouse;

    // Start is called before the first frame update
    void Start()
    {
        var self = this;
        Camera.onPreRender += c => {
            if (self != null) {
                GetComponent<MeshRenderer>().material.SetVector("_Position", transform.position);
            }
        };
    }

    record MeshIx(int verts, int tris);

    // Update is called once per frame
    void Update()
    {
        var mouse = Input.GetMouseButtonDown(0);
        if (!lastMouse && mouse) {
            var castResults = new CastResult[Span];
            var numResults = 0;
            Vector2 pos = transform.position;
            for (int i = 0; i < Span; i++) {
                var angle = Mathf.PI * 2 * i / Span;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var hit = Physics2D.Raycast(pos, dir, MaxDistance);
                if (hit.collider != null) {
                    castResults[i] = new CastResult.Hit(hit.collider.gameObject.GetHashCode(), hit.point);
                    numResults++;
                } else {
                    castResults[i] = new CastResult.Miss();
                }
            }
            var list = new List<CastResult.Collection>();
            CastResult lastVal = null;
            float thresh = ConnectThreshold * Mathf.PI * 2 / Span;
            foreach (var cast in castResults) {
                var id = cast.Id();
                if (cast is CastResult.Hit hit) {
                    var threshMul = (hit.point - pos).magnitude;
                    if (list.Count == 0 || !(lastVal is CastResult res) || !res.IsNear(cast, thresh * threshMul)) {
                        list.Add(new CastResult.Collection(hit.colliderID, new List<Vector2>()));
                    }
                    list.Last().points.Add(hit.point);
                }
                lastVal = cast;
            }
            Debug.Log("[Ray] " + numResults + " vertices, " + list.Count + " sectors");
            var threshMulLast = (list.Last().points.Last() - pos).magnitude;
            if (list.Count > 1 && castResults[0].IsNear(castResults.Last(), thresh * threshMulLast)) {
                var last = list.Last().points;
                list.RemoveAt(list.Count - 1);
                list[0] = new CastResult.Collection(list[0].colliderID, new List<Vector2>(last.Concat(list[0].points)));
            }

            // initial buffers for vtx/tri
            var vertices = new Vector3[20 * numResults];
            var triangles = new int[60 * (numResults - list.Count)];
            var ix = new MeshIx(0, 0);
            foreach (var coll in list) {
                ix = buildLine(ix, coll.points, vertices, triangles);
            }
            var mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.SetTriangles(triangles, 0, ix.tris, 0, true, 0);
            GetComponent<MeshFilter>().mesh = mesh;
        }
        lastMouse = mouse;
    }
    MeshIx buildLine(MeshIx ix, List<Vector2> points, Vector3[] vertices, int[] triangles) {
        int vix = ix.verts, tix = ix.tris;
        var joinAngles = new float[points.Count];
        var joinOffsets = new float[points.Count];
        if (points.Count < 2) return ix;
        for (var i = 2; i < points.Count; i++) {
            var prev = points[i - 2];
            var curr = points[i - 1];
            var next = points[i];
            var left = prev - curr;
            var right = curr - next;
            var angle = joinAngles[i - 1] = Vector2.SignedAngle(left, right) * Mathf.PI / 180;
            joinOffsets[i - 1] = Mathf.Sin(angle) / (1 + Mathf.Cos(angle));
        }
        if (points.Count == 2) {
            var p1 = points[0];
            var p2 = points[1];
            var normal = (p2 - p1).normalized * LineWidth;
            var edge = new Vector2(-normal.y, normal.x);
            vertices[vix + 0] = p1 + edge;
            vertices[vix + 1] = p1 - edge;
            vertices[vix + 2] = p2 - edge;
            vertices[vix + 3] = p2 + edge;
            triangles[tix + 0] = vix + 0;
            triangles[tix + 1] = vix + 1;
            triangles[tix + 2] = vix + 2;
            triangles[tix + 3] = vix + 0;
            triangles[tix + 4] = vix + 2;
            triangles[tix + 5] = vix + 3;
            vix += 4;
            tix += 6;
        } else {
            for (var i = 1; i < points.Count; i++) {
                var p1 = points[i - 1];
                var p2 = points[i];
                // vector along p1 to p2
                var normal = (p2 - p1).normalized * LineWidth;
                // rotate by 90 to create the hexagon
                var edge = new Vector2(-normal.y, normal.x);
                var jlow = joinOffsets[i - 1];
                var jhigh = joinOffsets[i];
                // hexagon vtxs
                vertices[vix + 0] = (p1 + Mathf.Max(0, jlow) * normal) + edge;
                vertices[vix + 1] = p1;
                vertices[vix + 2] = (p1 + Mathf.Max(0, -jlow) * normal) - edge;
                vertices[vix + 3] = (p2 - Mathf.Max(0, -jhigh) * normal) - edge;
                vertices[vix + 4] = p2;
                vertices[vix + 5] = (p2 - Mathf.Max(0, jhigh) * normal) + edge;
                // hexagon fan
                for (var j = 1; j <= 4; j++) {
                    triangles[tix + 0] = vix + 0;
                    triangles[tix + 1] = vix + j;
                    triangles[tix + 2] = vix + j + 1;
                    tix += 3;
                }
                vix += 6;
                if (i >= 2) {
                    var p1Ix = vix - 5;
                    var sangle = joinAngles[i - 1];
                    var angle = Mathf.Abs(sangle);
                    var activeEdge = Mathf.Sign(sangle) * edge;
                    var joinStepsF = Mathf.Ceil(angle * 180 / (CapTheta * Mathf.PI));
                    var joinSteps = (int) joinStepsF;
                    if (joinSteps < 1) continue;
                    for (int j = 0; j <= joinSteps; j++) {
                        vertices[vix + j] = p1 - Rotate(activeEdge, -sangle * (j / joinStepsF));
                    }
                    for (int j = 0; j < joinSteps; j++) {
                        triangles[tix + 0] = p1Ix;
                        triangles[tix + 1] = vix + j;
                        triangles[tix + 2] = vix + j + 1;
                        tix += 3;
                    }
                    vix += joinSteps + 1;
                }
            }
        }
        System.Action<Vector2, Vector2> buildCap = (pos, prev) => {
            var joinStepsF = Mathf.Ceil(180 / CapTheta);
            var joinSteps = (int) joinStepsF;
            var edge = Vector2.Perpendicular((pos - prev).normalized) * LineWidth;
            vertices[vix++] = pos;
            for (int i = 0; i <= joinSteps; i++) {
                vertices[vix + i] = pos - Rotate(edge, Mathf.PI * (i / joinStepsF));
            }
            for (int j = 0; j < joinSteps; j++) {
                triangles[tix + 0] = vix - 1;
                triangles[tix + 1] = vix + j;
                triangles[tix + 2] = vix + j + 1;
                tix += 3;
            }
            vix += joinSteps + 1;
        };
        buildCap(points[0], points[1]);
        buildCap(points.Last(), points[points.Count - 2]);
        return new MeshIx(vix, tix);
    }
    static Vector2 Rotate(Vector2 val, float angle) {
        float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
        return new Vector2(val.x * cos - val.y * sin, val.y * cos + val.x * sin);
    }
}
