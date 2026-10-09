using dc;
using dc.h2d;
using dc.hxd;
using dc.pr;
using dc.ui;
using DeadCellsMultiplayerMod.Tools;
using DeadCellsMultiplayerMod.UI;
using HaxeProxy.Runtime;
using ModCore.Modules;
using ModCore.Utilities;

namespace DeadCellsMultiplayerMod;

/// <summary>
/// Non-pausing exit confirmation for the host while downed. The normal pause menu is disabled in
/// co-op because pausing the host also stalls the authoritative simulation seen by clients.
/// </summary>
internal static class DownedHostExitPrompt
{
    private const int KeyEscape = 27;
    private const int KeyEnter = 13;

    private static bool _isOpen;
    private static bool _confirmRequested;
    private static bool _cancelRequested;
    private static bool _escapeKeyHeld;
    private static bool _enterKeyHeld;
    private static dc.h2d.Object? _root;

    internal static bool HandleWindowEvent(Event e)
    {
        if (e?.kind == null)
            return false;

        if (e.kind.Index == EventKind.Indexes.EKeyUp)
        {
            if (e.keyCode == KeyEscape)
                _escapeKeyHeld = false;
            else if (e.keyCode == KeyEnter)
                _enterKeyHeld = false;
            return _isOpen;
        }

        if (e.kind.Index != EventKind.Indexes.EKeyDown ||
            ModEntry._net?.IsHost != true || !ModEntry.IsLocalPlayerDowned() || IsTextInputActive())
        {
            return false;
        }

        if (e.keyCode == KeyEscape)
        {
            if (_escapeKeyHeld)
                return _isOpen;
            _escapeKeyHeld = true;
            if (_isOpen)
                _cancelRequested = true;
            else
                _isOpen = true;
            return true;
        }

        if (_isOpen && e.keyCode == KeyEnter)
        {
            if (_enterKeyHeld)
                return true;
            _enterKeyHeld = true;
            _confirmRequested = true;
            return true;
        }

        return false;
    }

    internal static bool Update()
    {
        if (!_isOpen)
            return false;

        if (ModEntry._net?.IsHost != true || !ModEntry.IsLocalPlayerDowned())
        {
            Close();
            return false;
        }

        if (_cancelRequested)
        {
            Close();
            return false;
        }

        if (_confirmRequested)
        {
            Close();
            LobbySession.StopNetworkFromMenu();
            LobbySession.ForceExitToMainMenu();
            return true;
        }

        if (_root == null || _root.parent == null)
            Build();
        return false;
    }

    private static bool IsTextInputActive()
    {
        if (DeadCellsMultiplayerMod.MultiplayerModUI.Connection.ConnectionUI.IsTextPromptOpen())
            return true;

        var textInput = TextInputHandler.GetActiveTextInput();
        return textInput != null && TextInputHandler.IsTextInputActive(textInput);
    }

    private static void Build()
    {
        var hudRoot = HUD.Class.ME?.root;
        if (hudRoot == null)
            return;

        var window = dc.hxd.Window.Class.getInstance();
        var screenW = window?.get_width() ?? 1280.0;
        var screenH = window?.get_height() ?? 720.0;
        var uiScale = System.Math.Max(UiScale.GetResolutionScale(), 1.0);
        var panelW = System.Math.Min(620.0 * uiScale, screenW - 40.0);
        var panelH = 250.0 * uiScale;
        var panelX = (screenW - panelW) * 0.5;
        var panelY = (screenH - panelH) * 0.5;

        _root = new dc.h2d.Object(hudRoot);
        hudRoot.addChild(_root);

        var dim = new Graphics(_root);
        var dimColor = 0x000000;
        var dimAlpha = 0.52;
        dim.beginFill(Ref<int>.From(ref dimColor), Ref<double>.From(ref dimAlpha));
        dim.drawRect(0, 0, screenW, screenH);
        dim.endFill();
        var dimInput = new Interactive(screenW, screenH, _root, null);
        dimInput.onClick = new HlAction<Event>(_ => { });

        var panel = new dc.h2d.Object(_root);
        panel.x = panelX;
        panel.y = panelY;
        var panelGraphics = new Graphics(panel);
        UiChrome.DrawContentCard(panelGraphics, 0, 0, panelW, panelH, 18.0 * uiScale, 0x14161F, 0x2A3A5E);

        AddCenteredText(panel, GetText.Instance.GetString("Leave multiplayer run?"), 0xF7FC65, 0.62 * uiScale,
            18.0 * uiScale, panelW, panelW);
        AddCenteredText(panel, GetText.Instance.GetString("Disconnect and return to the title screen?"), 0xE0E5EF, 0.43 * uiScale,
            86.0 * uiScale, panelW, panelW);
        AddCenteredText(panel, GetText.Instance.GetString("Press Enter or click Leave to confirm. Press Esc to cancel."), 0x9098A8, 0.34 * uiScale,
            126.0 * uiScale, panelW, panelW);

        var pad = 26.0 * uiScale;
        var gap = 18.0 * uiScale;
        var buttonY = panelH - 26.0 * uiScale - 58.0 * uiScale;
        var buttonW = (panelW - pad * 2.0 - gap) * 0.5;
        AddButton(panel, GetText.Instance.GetString("Leave multiplayer"), pad, buttonY, buttonW, 58.0 * uiScale,
            0xA84242, 0xD96363, () => _confirmRequested = true, uiScale);
        AddButton(panel, GetText.Instance.GetString("Cancel"), pad + buttonW + gap, buttonY,
            buttonW, 58.0 * uiScale, 0x2A3A5E, 0x3A4A6E, () => _cancelRequested = true, uiScale);
    }

    private static void AddCenteredText(
        dc.h2d.Object parent,
        string value,
        int color,
        double scale,
        double y,
        double regionX,
        double regionW)
    {
        var text = Assets.Class.makeText(value.AsHaxeString(), Tools.MultiColor.ColorFromHex("#ffffff"), false, parent);
        text.customScale = scale;
        text.onResize();
        text.textColor = color;
        double textW;
        try { textW = text.textWidth * (text.scaleX > 0.01 ? text.scaleX : scale); }
        catch { textW = value.Length * 7.0 * scale; }
        text.x = regionX + System.Math.Max(0, (regionW - textW) * 0.5);
        text.y = y;
    }

    private static void AddButton(
        dc.h2d.Object parent,
        string label,
        double x,
        double y,
        double w,
        double h,
        int face,
        int highlight,
        Action onClick,
        double uiScale)
    {
        var graphics = new Graphics(parent);
        UiChrome.DrawRaisedPlate(graphics, x, y, w, h, 10.0 * uiScale, 0x2A3A5E, face, highlight, enabled: true);

        var text = Assets.Class.makeText(label.AsHaxeString(), Tools.MultiColor.ColorFromHex("#ffffff"), false, parent);
        text.customScale = 0.64 * uiScale;
        text.onResize();
        text.textColor = 0xFFFFFF;
        double textW;
        try { textW = text.textWidth * (text.scaleX > 0.01 ? text.scaleX : text.customScale); }
        catch { textW = label.Length * 7.0 * uiScale; }
        double textH;
        try { textH = text.textHeight * (text.scaleY > 0.01 ? text.scaleY : text.customScale); }
        catch { textH = 24.0 * uiScale; }
        text.x = x + System.Math.Max(0, (w - textW) * 0.5);
        text.y = y + (h - textH) * 0.5;

        var hit = new Interactive(w, h, parent, null);
        hit.x = x;
        hit.y = y;
        hit.onClick = new HlAction<Event>(_ => onClick());
    }

    private static void Close()
    {
        try { _root?.remove(); } catch { }
        _root = null;
        _isOpen = false;
        _confirmRequested = false;
        _cancelRequested = false;
        _escapeKeyHeld = false;
        _enterKeyHeld = false;
    }
}
