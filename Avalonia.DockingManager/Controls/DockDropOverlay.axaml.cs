using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace Avalonia.DockingManager.Controls;

public partial class DockDropOverlay : UserControl
{
    private Border _previewBorder = null!;
    private Border _rootTop = null!;
    private Border _rootBottom = null!;
    private Border _rootLeft = null!;
    private Border _rootRight = null!;
    private Canvas _compass = null!;
    private Border _compassCenter = null!;
    private Border _compassTop = null!;
    private Border _compassBottom = null!;
    private Border _compassLeft = null!;
    private Border _compassRight = null!;

    private readonly IBrush _normalBg = new SolidColorBrush(Color.Parse("#E61E1E24"));
    private readonly IBrush _normalBorder = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255));
    private readonly IBrush _activeBg = new SolidColorBrush(Color.Parse("#FF007ACC"));
    private readonly IBrush _activeBorder = new SolidColorBrush(Color.Parse("#FF60CDFF"));

    public DockDropOverlay()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        _previewBorder  = this.FindControl<Border>("PART_PreviewBorder")!;
        _rootTop        = this.FindControl<Border>("PART_RootTop")!;
        _rootBottom     = this.FindControl<Border>("PART_RootBottom")!;
        _rootLeft       = this.FindControl<Border>("PART_RootLeft")!;
        _rootRight      = this.FindControl<Border>("PART_RootRight")!;
        _compass        = this.FindControl<Canvas>("PART_Compass")!;
        _compassCenter  = this.FindControl<Border>("PART_CompassCenter")!;
        _compassTop     = this.FindControl<Border>("PART_CompassTop")!;
        _compassBottom  = this.FindControl<Border>("PART_CompassBottom")!;
        _compassLeft    = this.FindControl<Border>("PART_CompassLeft")!;
        _compassRight   = this.FindControl<Border>("PART_CompassRight")!;
    }

    public void Hide()
    {
        _previewBorder.IsVisible = false;
        _rootTop.IsVisible       = false;
        _rootBottom.IsVisible    = false;
        _rootLeft.IsVisible      = false;
        _rootRight.IsVisible     = false;
        _compass.IsVisible       = false;
    }

    public DockZone UpdateOverlay(Rect? targetGroupBounds, Rect rootBounds, Point mousePos)
    {
        // 1. Position and show root outer indicators
        const double btnSize = 38;
        double rTopX = rootBounds.Width / 2 - btnSize / 2;
        double rTopY = 12;
        Canvas.SetLeft(_rootTop, rTopX);
        Canvas.SetTop(_rootTop, rTopY);
        _rootTop.IsVisible = true;

        double rBotX = rootBounds.Width / 2 - btnSize / 2;
        double rBotY = Math.Max(0, rootBounds.Height - btnSize - 12);
        Canvas.SetLeft(_rootBottom, rBotX);
        Canvas.SetTop(_rootBottom, rBotY);
        _rootBottom.IsVisible = true;

        double rLeftX = 12;
        double rLeftY = rootBounds.Height / 2 - btnSize / 2;
        Canvas.SetLeft(_rootLeft, rLeftX);
        Canvas.SetTop(_rootLeft, rLeftY);
        _rootLeft.IsVisible = true;

        double rRightX = Math.Max(0, rootBounds.Width - btnSize - 12);
        double rRightY = rootBounds.Height / 2 - btnSize / 2;
        Canvas.SetLeft(_rootRight, rRightX);
        Canvas.SetTop(_rootRight, rRightY);
        _rootRight.IsVisible = true;

        // Reset all indicator styles
        ResetStyles();

        // 2. Check outer edge hits
        var rootTopHitRect = new Rect(rTopX - 8, rTopY - 8, btnSize + 16, btnSize + 16);
        if (rootTopHitRect.Contains(mousePos))
        {
            SetActive(_rootTop);
            ShowPreview(new Rect(0, 0, rootBounds.Width, rootBounds.Height * 0.3));
            _compass.IsVisible = false;
            return DockZone.RootTop;
        }

        var rootBottomHitRect = new Rect(rBotX - 8, rBotY - 8, btnSize + 16, btnSize + 16);
        if (rootBottomHitRect.Contains(mousePos))
        {
            SetActive(_rootBottom);
            ShowPreview(new Rect(0, rootBounds.Height * 0.7, rootBounds.Width, rootBounds.Height * 0.3));
            _compass.IsVisible = false;
            return DockZone.RootBottom;
        }

        var rootLeftHitRect = new Rect(rLeftX - 8, rLeftY - 8, btnSize + 16, btnSize + 16);
        if (rootLeftHitRect.Contains(mousePos))
        {
            SetActive(_rootLeft);
            ShowPreview(new Rect(0, 0, rootBounds.Width * 0.25, rootBounds.Height));
            _compass.IsVisible = false;
            return DockZone.RootLeft;
        }

        var rootRightHitRect = new Rect(rRightX - 8, rRightY - 8, btnSize + 16, btnSize + 16);
        if (rootRightHitRect.Contains(mousePos))
        {
            SetActive(_rootRight);
            ShowPreview(new Rect(rootBounds.Width * 0.75, 0, rootBounds.Width * 0.25, rootBounds.Height));
            _compass.IsVisible = false;
            return DockZone.RootRight;
        }

        // 3. Handle Center Compass if over a target group
        if (targetGroupBounds.HasValue)
        {
            var gb = targetGroupBounds.Value;
            double compassW = _compass.Width;
            double compassH = _compass.Height;
            double cX = gb.X + (gb.Width - compassW) / 2;
            double cY = gb.Y + (gb.Height - compassH) / 2;

            Canvas.SetLeft(_compass, cX);
            Canvas.SetTop(_compass, cY);
            _compass.IsVisible = true;

            // Compute button hit rects relative to manager coordinates
            var cCenterHit = new Rect(cX + 40, cY + 40, 36, 36);
            var cTopHit    = new Rect(cX + 40, cY + 4,  36, 34);
            var cBotHit    = new Rect(cX + 40, cY + 78, 36, 34);
            var cLeftHit   = new Rect(cX + 4,  cY + 40, 34, 36);
            var cRightHit  = new Rect(cX + 78, cY + 40, 34, 36);

            DockZone zone;
            if (cCenterHit.Contains(mousePos))
            {
                zone = DockZone.Center;
            }
            else if (cTopHit.Contains(mousePos))
            {
                zone = DockZone.Top;
            }
            else if (cBotHit.Contains(mousePos))
            {
                zone = DockZone.Bottom;
            }
            else if (cLeftHit.Contains(mousePos))
            {
                zone = DockZone.Left;
            }
            else if (cRightHit.Contains(mousePos))
            {
                zone = DockZone.Right;
            }
            else
            {
                // Not on a compass button, but inside target group — compute proportional zone
                double relX = (mousePos.X - gb.X) / gb.Width;
                double relY = (mousePos.Y - gb.Y) / gb.Height;

                if (relX > 0.25 && relX < 0.75 && relY > 0.25 && relY < 0.75)
                {
                    zone = DockZone.Center;
                }
                else
                {
                    double dTop = relY;
                    double dBot = 1.0 - relY;
                    double dLeft = relX;
                    double dRight = 1.0 - relX;
                    double min = Math.Min(Math.Min(dTop, dBot), Math.Min(dLeft, dRight));

                    if (min == dTop) zone = DockZone.Top;
                    else if (min == dBot) zone = DockZone.Bottom;
                    else if (min == dLeft) zone = DockZone.Left;
                    else zone = DockZone.Right;
                }
            }

            // Highlight the active compass button & display preview slot
            switch (zone)
            {
                case DockZone.Center:
                    SetActive(_compassCenter);
                    ShowPreview(gb);
                    break;
                case DockZone.Top:
                    SetActive(_compassTop);
                    ShowPreview(new Rect(gb.X, gb.Y, gb.Width, gb.Height / 2));
                    break;
                case DockZone.Bottom:
                    SetActive(_compassBottom);
                    ShowPreview(new Rect(gb.X, gb.Y + gb.Height / 2, gb.Width, gb.Height / 2));
                    break;
                case DockZone.Left:
                    SetActive(_compassLeft);
                    ShowPreview(new Rect(gb.X, gb.Y, gb.Width / 2, gb.Height));
                    break;
                case DockZone.Right:
                    SetActive(_compassRight);
                    ShowPreview(new Rect(gb.X + gb.Width / 2, gb.Y, gb.Width / 2, gb.Height));
                    break;
            }

            return zone;
        }

        // Not in any group and not in root indicators
        _compass.IsVisible = false;
        _previewBorder.IsVisible = false;
        return DockZone.None;
    }

    private void ShowPreview(Rect rect)
    {
        _previewBorder.IsVisible = true;
        Canvas.SetLeft(_previewBorder, Math.Max(0, rect.X));
        Canvas.SetTop(_previewBorder, Math.Max(0, rect.Y));
        _previewBorder.Width  = Math.Max(10, rect.Width);
        _previewBorder.Height = Math.Max(10, rect.Height);
    }

    private void SetActive(Border b)
    {
        b.Background  = _activeBg;
        b.BorderBrush = _activeBorder;
    }

    private void ResetStyles()
    {
        SetNormal(_rootTop);
        SetNormal(_rootBottom);
        SetNormal(_rootLeft);
        SetNormal(_rootRight);
        SetNormal(_compassCenter);
        SetNormal(_compassTop);
        SetNormal(_compassBottom);
        SetNormal(_compassLeft);
        SetNormal(_compassRight);
    }

    private void SetNormal(Border b)
    {
        b.Background  = _normalBg;
        b.BorderBrush = _normalBorder;
    }
}