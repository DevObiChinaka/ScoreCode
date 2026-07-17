using Microsoft.AspNetCore.Components;

namespace ScoreCode.Web.Components;

/// <summary>Inline nav icons. Stroke follows the link's current colour.</summary>
public static class Ico
{
    private static MarkupString S(string body) => new(
        "<svg viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" " +
        "stroke-linecap=\"round\" stroke-linejoin=\"round\">" + body + "</svg>");

    public static MarkupString Home => S(
        "<path d=\"M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z\"/>" +
        "<polyline points=\"9 22 9 12 15 12 15 22\"/>");

    public static MarkupString Chart => S(
        "<line x1=\"6\" y1=\"20\" x2=\"6\" y2=\"15\"/>" +
        "<line x1=\"12\" y1=\"20\" x2=\"12\" y2=\"8\"/>" +
        "<line x1=\"18\" y1=\"20\" x2=\"18\" y2=\"4\"/>");

    public static MarkupString Building => S(
        "<path d=\"M3 21h18\"/><path d=\"M5 21V5a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2v16\"/>" +
        "<path d=\"M9 7h.01M15 7h.01M9 11h.01M15 11h.01M9 15h.01M15 15h.01\"/>");

    public static MarkupString Users => S(
        "<path d=\"M17 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2\"/>" +
        "<circle cx=\"9.5\" cy=\"7\" r=\"3.5\"/>" +
        "<path d=\"M22 21v-2a4 4 0 0 0-3-3.87\"/><path d=\"M16 3.13a4 4 0 0 1 0 7.75\"/>");

    public static MarkupString Cap => S(
        "<path d=\"M22 10L12 5 2 10l10 5 10-5z\"/>" +
        "<path d=\"M6 12v5c3 2.5 9 2.5 12 0v-5\"/>");

    public static MarkupString File => S(
        "<path d=\"M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z\"/>" +
        "<polyline points=\"14 2 14 8 20 8\"/>");

    public static MarkupString Cog => S(
        "<circle cx=\"12\" cy=\"12\" r=\"3\"/>" +
        "<path d=\"M19.4 15a1.7 1.7 0 0 0 .34 1.87l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.7 1.7 0 0 0-2.87 1.2V21a2 2 0 0 1-4 0v-.09A1.7 1.7 0 0 0 9 19.24a1.7 1.7 0 0 0-1.87.34l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06A1.7 1.7 0 0 0 4.76 15H4.6a2 2 0 0 1 0-4h.09A1.7 1.7 0 0 0 6 9a1.7 1.7 0 0 0-.34-1.87l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06A1.7 1.7 0 0 0 11 4.76V4.6a2 2 0 0 1 4 0v.09A1.7 1.7 0 0 0 18 6l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06A1.7 1.7 0 0 0 19.24 12v.6z\"/>");
}
