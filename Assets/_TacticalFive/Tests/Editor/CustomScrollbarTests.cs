using NUnit.Framework;
using UnityEngine.UIElements;

/// <summary>
/// F7-04: una sola abstracción de scroll. El binding debe ser idempotente dentro de
/// la misma visita para no acumular handlers al reconstruir listas.
/// </summary>
public class CustomScrollbarTests
{
    static (VisualElement viewport, VisualElement content, VisualElement track, VisualElement thumb) Parts()
        => (new VisualElement(), new VisualElement(), new VisualElement(), new VisualElement());

    [Test]
    public void Bind_IsIdempotentPerViewport()
    {
        var (viewport, content, track, thumb) = Parts();

        var first = CustomScrollbar.Bind(viewport, content, track, thumb);
        var second = CustomScrollbar.Bind(viewport, content, track, thumb);

        Assert.That(first, Is.Not.Null);
        Assert.That(second, Is.SameAs(first), "No debe reenlazar el mismo viewport en la misma visita");
    }

    [Test]
    public void BindGeneric_PreservesTheControllerHandleType()
    {
        var (viewport, content, track, thumb) = Parts();

        var first = CustomScrollbar.Bind<TestHandle>(viewport, content, track, thumb);
        var second = CustomScrollbar.Bind<TestHandle>(viewport, content, track, thumb);

        Assert.That(first, Is.TypeOf<TestHandle>());
        Assert.That(second, Is.SameAs(first));
    }

    class TestHandle : CustomScrollbar.Handle { }
}
