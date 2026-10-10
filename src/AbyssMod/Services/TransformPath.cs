#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace AbyssMod.Services;

internal static class TransformPath
{
    internal static string? Build(Transform? transform)
    {
        if (transform == null)
            return null;
        var parts = new Stack<string>();
        for (var current = transform; current != null; current = current.parent)
            parts.Push(current.name);
        return string.Join("/", parts);
    }
}
