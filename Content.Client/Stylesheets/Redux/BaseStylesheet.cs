﻿namespace Content.Client.Stylesheets.Redux;

public abstract partial class BaseStylesheet
{
    /// <summary>
    ///     The type used to describe a configuration for this stylesheet.
    ///     This will be constructed and passed into the constructor, and may be used for other things as well.
    /// </summary>
    /// <remarks>
    ///     Must be a constant, changes to this after construction will not be reflected.
    /// </remarks>
    public virtual Type StylesheetConfigType => typeof(NoConfig);

    public record NoConfig();

    protected object _config;

    /// <remarks>
    ///     This constructor will not access any virtual or abstract properties, so you can set them from your config.
    /// </remarks>
    protected BaseStylesheet(object config)
    {
        IoCManager.InjectDependencies(this);
        _config = config;
        Stylesheet = default!;
    }
}

