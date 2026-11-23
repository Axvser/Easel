namespace Easel;

/// <summary>How the colours of an opacity-mask brush become coverage.</summary>
public enum OpacityMaskMode
{
    /// <summary>Use the brush's alpha channel; a black opaque brush still covers.</summary>
    Alpha,

    /// <summary>
    /// Use the brush's luminance multiplied by its alpha; white covers, black hides. Useful for a mask painted
    /// with an opaque black-to-white gradient, where <see cref="Alpha"/> would ignore the colour.
    /// </summary>
    Luminance,
}
