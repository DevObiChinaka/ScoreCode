using System.IO;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace ScoreCode.Client.Helpers;

/// <summary>
/// Renders HTML/CSS answers as a live preview in a WebView2 instead of printing
/// the raw source as text. HTML is rendered as-is; CSS is rendered against a
/// small sample document so the styles are actually visible.
/// </summary>
public static class HtmlPreview
{
    /// <summary>True for languages that should be previewed rather than executed.</summary>
    public static bool IsPreviewLanguage(string language) =>
        language.Equals("html", StringComparison.OrdinalIgnoreCase) ||
        language.Equals("css", StringComparison.OrdinalIgnoreCase);

    /// <summary>Build the document to render for the given source + language.</summary>
    public static string Build(string code, string language)
    {
        if (language.Equals("html", StringComparison.OrdinalIgnoreCase))
            return code;

        // CSS-only answers have no markup of their own — render them against a
        // representative sample so common selectors (tags, .box, button, …) show.
        return "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><style>\n"
             + code +
             "\n</style></head><body>\n"
             + "<h1>Heading 1</h1>\n"
             + "<h2>Heading 2</h2>\n"
             + "<p>Sample paragraph with a <a href=\"#\">link</a> and <strong>bold</strong> text.</p>\n"
             + "<button>Button</button>\n"
             + "<ul><li>List item one</li><li>List item two</li></ul>\n"
             + "<div class=\"box\">A div with class <code>box</code></div>\n"
             + "</body></html>";
    }

    /// <summary>
    /// Ensure the WebView2 is initialised, then render the built preview document.
    /// Reuses the same user-data folder the caller's editor uses so the process
    /// keeps a single WebView2 user-data folder.
    /// </summary>
    public static async Task RenderAsync(WebView2 web, string code, string language, string userDataSubfolder)
    {
        if (web.CoreWebView2 is null)
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ScoreCode", userDataSubfolder);
            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: folder);
            await web.EnsureCoreWebView2Async(env);
            web.CoreWebView2.Settings.AreDevToolsEnabled = false;
            web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        }

        web.CoreWebView2.NavigateToString(Build(code, language));
    }
}
