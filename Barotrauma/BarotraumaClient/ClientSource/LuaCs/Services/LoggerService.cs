using Microsoft.Xna.Framework;

namespace Barotrauma.LuaCs.Services;

public partial class LoggerService : ILoggerService, IClientLoggerService
{
    private GUIFrame _overlayFrame;
    private GUITextBlock _textBlock;
    private double _showTimer = 0;
    
    
    private void CreateOverlay(string message)
    {
        _overlayFrame = new GUIFrame(new RectTransform(new Vector2(0.4f, 0.03f), null), null, new Color(50, 50, 50, 100))
        {
            CanBeFocused = false
        };

        GUILayoutGroup layout =
            new GUILayoutGroup(
                new RectTransform(new Vector2(0.8f, 0.8f), _overlayFrame.RectTransform, Anchor.CenterLeft), false,
                Anchor.Center);

        _textBlock = new GUITextBlock(new RectTransform(new Vector2(1f, 0f), layout.RectTransform), message);
        _overlayFrame.RectTransform.MinSize = new Point((int)(_textBlock.TextSize.X * 1.2), 0);

        layout.Recalculate();
    }

    public void AddToGUIUpdateList()
    {
        if (_overlayFrame != null && Timing.TotalTime <= _showTimer)
        {
            _overlayFrame.AddToGUIUpdateList();
        }
    }

    public void ShowErrorOverlay(string message, float time = 5f, float duration = 1.5f)
    {
        if (Timing.TotalTime <= _showTimer)
        {
            return;
        }

        CreateOverlay(message);

        _overlayFrame.Flash(Color.Red, duration, true);
        _showTimer = Timing.TotalTime + time;
    }
}
