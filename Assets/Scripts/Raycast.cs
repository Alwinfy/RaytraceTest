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
                    castResults[i] = new CastResult.Hit(0, hit.point);
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

            var vertices = new Vector3[5 * numResults];
            var triangles = new int[12 * (numResults - list.Count)];
            int vix = 0, tix = 0;
            foreach (var coll in list) {
                for (var i = 1; i < coll.points.Count; i++) {
                    var p1 = coll.points[i - 1];
                    var p2 = coll.points[i];
                    var rot = p2 - p1;    
                    var edge = new Vector2(-rot.y, rot.x).normalized * LineWidth;
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
                    if (i >= 2) {
                        vertices[vix + 4] = p1;
                        triangles[tix + 6] = vix + 0;
                        triangles[tix + 8] = vix - 2;
                        triangles[tix + 7] = vix + 4;
                        triangles[tix + 9] = vix + 1;
                        triangles[tix + 11] = vix - 3;
                        triangles[tix + 10] = vix + 4;
                        tix += 6;
                    }
                    vix += 5;
                    tix += 6;
                }
            }
            var mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.SetTriangles(triangles, 0, tix, 0, true, 0);
            GetComponent<MeshFilter>().mesh = mesh;
        }
        lastMouse = mouse;
    }
}
