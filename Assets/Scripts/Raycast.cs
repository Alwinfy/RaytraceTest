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
    public float BasePower = 500;
    public float CapTheta = 18;
    public float LineWidth = 0.08f;

    public float FadeDelay = 2f;
    public float FadeSpeed = 10f;
    public Transform playerPos;

    private float lastFlash = 0;

    // Start is called before the first frame update
    void Start()
    {
        var self = this;
        Camera.onPreRender += c => {
            if (self != null) {
                var mat = GetComponent<MeshRenderer>().material;
                mat.SetVector("_Position", playerPos.position);
                mat.SetFloat("_Opacity", 1 / (1 + Mathf.Exp(FadeSpeed * (Time.time - lastFlash))));
            }
        };
    }

    record MeshIx(int verts, int tris);

    public void RunRaycast(Vector2 pos) {
            lastFlash = Time.time + FadeDelay;
            var castResults = new CastResult[Span];
            var numResults = 0;
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
            var lineGroups = new List<CastResult.Collection>();
            CastResult lastVal = null;
            float thresh = ConnectThreshold * Mathf.PI * 2 / Span;
            foreach (var cast in castResults) {
                var id = cast.Id();
                if (cast is CastResult.Hit hit) {
                    var threshMul = (hit.point - pos).magnitude;
                    if (lineGroups.Count == 0 || !(lastVal is CastResult res) || !res.IsNear(cast, thresh * threshMul)) {
                        lineGroups.Add(new CastResult.Collection(hit.colliderID, new List<Vector2>()));
                    }
                    lineGroups.Last().points.Add(hit.point);
                }
                lastVal = cast;
            }
            Debug.Log("[Ray] " + numResults + " vertices, " + lineGroups.Count + " sectors");
            var threshMulLast = (lineGroups.Last().points.Last() - pos).magnitude;
            if (lineGroups.Count > 1 && castResults[0].IsNear(castResults.Last(), thresh * threshMulLast)) {
                var last = lineGroups.Last().points;
                lineGroups.RemoveAt(lineGroups.Count - 1);
                lineGroups[0] = new CastResult.Collection(lineGroups[0].colliderID, new List<Vector2>(last.Concat(lineGroups[0].points)));
            }

            // initial buffers for vtx/tri
            var vertices = new Vector3[20 * numResults];
            // u: raw light, v: angle magnitude
            var vtxData = new Vector2[20 * numResults];
            var triangles = new int[60 * (numResults - lineGroups.Count)];
            var ix = new MeshIx(0, 0);
            foreach (var coll in lineGroups) {
                var points = coll.points;
                var vtxDataIn = new Vector2[points.Count];
                for (int i = 0; i < points.Count; i++) {
                    var tangent = (i == 0 ? points[i] : points[i - 1]) - (i == points.Count - 1 ? points[i] : points[i + 1]);
                    var offset = pos - points[i];
                    var illumination = Vector2.Dot(tangent.normalized, Vector2.Perpendicular(offset).normalized);
                    if ((i == 0 || i == points.Count - 1) && points.Count >= 3) illumination /= 2;
                    vtxDataIn[i] = new Vector2(illumination, BasePower / offset.sqrMagnitude);
                }
                ix = buildLine(ix, points, vertices, vtxDataIn, vtxData, triangles);
            }
            var mesh = new Mesh();
            mesh.SetVertices(vertices, 0, ix.verts);
            mesh.SetTriangles(triangles, 0, ix.tris, 0, true, 0);
            mesh.SetUVs(0, vtxData, 0, ix.verts);
            GetComponent<MeshFilter>().mesh = mesh;
    }
    MeshIx buildLine<T>(MeshIx ix, List<Vector2> points, Vector3[] vertices, T[] vtxDataIn, T[] vtxData, int[] triangles) {
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
            vtxData[vix + 0] = vtxDataIn[0];
            vtxData[vix + 1] = vtxDataIn[0];
            vtxData[vix + 2] = vtxDataIn[1];
            vtxData[vix + 3] = vtxDataIn[1];
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
                vtxData[vix + 0] = vtxDataIn[i - 1];
                vtxData[vix + 1] = vtxDataIn[i - 1];
                vtxData[vix + 2] = vtxDataIn[i - 1];
                vtxData[vix + 3] = vtxDataIn[i];
                vtxData[vix + 4] = vtxDataIn[i];
                vtxData[vix + 5] = vtxDataIn[i];
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
                        vtxData[vix + j] = vtxDataIn[i - 1];
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
        System.Action<int, Vector2> buildCap = (ix, prev) => {
            var pos = points[ix];
            var joinStepsF = Mathf.Ceil(180 / CapTheta);
            var joinSteps = (int) joinStepsF;
            var edge = Vector2.Perpendicular((pos - prev).normalized) * LineWidth;
            vertices[vix] = pos;
            vtxData[vix] = vtxDataIn[ix];
            vix++;
            for (int i = 0; i <= joinSteps; i++) {
                vertices[vix + i] = pos - Rotate(edge, Mathf.PI * (i / joinStepsF));
                vtxData[vix + i] = vtxDataIn[ix];
            }
            for (int j = 0; j < joinSteps; j++) {
                triangles[tix + 0] = vix - 1;
                triangles[tix + 1] = vix + j;
                triangles[tix + 2] = vix + j + 1;
                tix += 3;
            }
            vix += joinSteps + 1;
        };
        buildCap(0, points[1]);
        buildCap(points.Count - 1, points[points.Count - 2]);
        return new MeshIx(vix, tix);
    }
    static Vector2 Rotate(Vector2 val, float angle) {
        float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
        return new Vector2(val.x * cos - val.y * sin, val.y * cos + val.x * sin);
    }
}
