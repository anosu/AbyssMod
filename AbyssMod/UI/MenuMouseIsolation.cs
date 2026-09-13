using UnityEngine;

namespace AbyssMod.UI;

/// <summary>游戏层输入屏蔽状态；只读取原始按键用于松键等待，不修改 Unity 的输入 API。</summary>
internal static class MenuMouseIsolation
{
    private static int _cleanFrame = -1;
    private static int _closeFrame;

    internal static bool IsOpen { get; private set; }
    internal static bool BlocksGame { get; private set; }

    internal static void Begin()
    {
        IsOpen = true;
        BlocksGame = true;
        _cleanFrame = -1;
    }

    internal static void End()
    {
        IsOpen = false;
        BlocksGame = true;
        _closeFrame = Time.frameCount;
        _cleanFrame = -1;
    }

    internal static void Update()
    {
        if (!BlocksGame || IsOpen)
            return;

        bool mouseActivity = false;
        for (int i = 0; i < 7; i++)
        {
            var key = (KeyCode)((int)KeyCode.Mouse0 + i);
            mouseActivity |= Input.GetKey(key) || Input.GetKeyDown(key) || Input.GetKeyUp(key);
        }
        var scroll = Input.mouseScrollDelta;
        mouseActivity |= scroll.x != 0 || scroll.y != 0 || Input.touchCount != 0;
        Advance(Time.frameCount, mouseActivity);
    }

    internal static void Advance(int frame, bool mouseActivity)
    {
        if (!BlocksGame || IsOpen)
            return;
        if (mouseActivity)
        {
            _cleanFrame = -1;
            return;
        }
        if (_cleanFrame < 0)
            _cleanFrame = frame;
        else if (frame > _cleanFrame && frame > _closeFrame)
            BlocksGame = false;
    }

    internal static void Reset()
    {
        IsOpen = BlocksGame = false;
        _cleanFrame = -1;
    }
}
