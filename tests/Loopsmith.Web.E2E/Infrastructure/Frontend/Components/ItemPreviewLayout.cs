namespace Loopsmith.Web.E2E.Infrastructure.Frontend.Components;

/// <summary>A name's preview: its title, its box, the name's box, and the screen's size.</summary>
public sealed record ItemPreviewLayout(string Title, ScreenBox Box, ScreenBox Name, double ScreenWidth, double ScreenHeight);
