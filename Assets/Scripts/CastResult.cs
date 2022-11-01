using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface CastResult
{
    public class Miss : CastResult {} 
    public record Hit(int colliderID, Vector2 point) : CastResult;
    public record Collection(int colliderID, List<Vector2> points);
    public bool IsNear(CastResult other, float thresh) {
        return this is Hit th && other is Hit oh && th.colliderID == oh.colliderID && Vector2.Distance(th.point, oh.point) < thresh;
    }
    public int? Id() {
        return this is Hit th ? th.colliderID : null;
    }
}

namespace System.Runtime.CompilerServices
{
      internal static class IsExternalInit {}
}
