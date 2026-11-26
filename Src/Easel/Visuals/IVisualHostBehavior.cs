namespace Easel.Visuals;

/// <summary>A behavior registered on a visual element that needs the hosting <see cref="VisualHost"/>.</summary>
/// <remarks>
/// <para>
/// Register with <see cref="VisualElement.AddHostBehavior(IVisualHostBehavior)"/>. The element attaches the
/// behavior when it resolves a host through its ancestor chain and detaches it when that host changes or the
/// element is disposed, so the behavior never searches the tree for the host itself.
/// </para>
/// <para>
/// The same instance is not attached twice. A behavior may be registered before the element is parented; it is
/// attached as soon as the surface is available.
/// </para>
/// </remarks>
public interface IVisualHostBehavior
{
    /// <summary>Called when the element is hosted by <paramref name="host"/>.</summary>
    /// <param name="host">The visual host that now owns the element's surface.</param>
    void Attach(VisualHost host);

    /// <summary>Called when the element leaves the host it was attached to.</summary>
    void Detach();
}
