﻿using Content.Client.Stylesheets.Redux.Fonts;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Reflection;
using Robust.Shared.Sandboxing;
using static Content.Client.Stylesheets.Redux.StylesheetHelpers;

namespace Content.Client.Stylesheets.Redux;

public abstract partial class BaseStylesheet : IStyleResources
{
    [Dependency] protected readonly ISandboxHelper SandboxHelper = default!;
    [Dependency] protected readonly IReflectionManager ReflectionManager = default!;
    [Dependency] protected internal readonly IResourceCache ResCache = default!;

    public Stylesheet Stylesheet { get; init; }

    public abstract FontStack BaseFont { get; }

    /// <summary>
    ///     Get the style rules for the given font stack, with the provided sizes.
    ///     Does not set the 'default' font, but does create rules for every combination of font kind and font size.
    ///     This is intended for font sizes you think will be common/generally useful, for less common usecases prefer specifying the font explicitly.
    /// </summary>
    /// <param name="prefix">The prefix for the style classes, if any.</param>
    /// <param name="stack">Font stack to use</param>
    /// <param name="sizes">A set of styleclasses and the associated size of font to use.</param>
    /// <returns>A rules list containing all combinations.</returns>
    /// <remarks>Use <see cref="M:Content.Client.Stylesheets.Redux.BaseStylesheet.GetFontClass(Content.Client.Stylesheets.Redux.Fonts.FontStack.FontKind,System.String)"/> to get the appropriate styleclass for a font choice.</remarks>
    // god xmldoc refs are long ^^^
    // lmao
    protected StyleRule[] GetRulesForFont(string? prefix, FontStack stack, List<(string?, int)> sizes)
    {
        var rules = new List<StyleRule>();

        foreach (var (name, size) in sizes)
        {
            foreach (var kind in stack.AvailableKinds)
            {
                var builder = E().Class(GetFontClass(kind, prefix));

                if (name is not null)
                    builder.Class(name);

                builder.Prop(Label.StylePropertyFont, stack.GetFont(size, kind));

                rules.Add(builder);
            }
        }

        return rules.ToArray();
    }

    /// <summary>
    ///     Returns the appropriate styleclass for the given font configuration.
    /// </summary>
    /// <param name="kind"></param>
    /// <param name="prefix"></param>
    /// <returns></returns>
    public static string GetFontClass(FontKind kind, string? prefix = null)
    {
        var kindStr = kind.ToString().ToLowerInvariant();
        return prefix is null ? $"font-{kindStr}" : $"{prefix}-{kindStr}";
    }
}
