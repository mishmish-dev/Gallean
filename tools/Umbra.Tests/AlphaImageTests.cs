using TorchSharp;

namespace Umbra.Tests;

public sealed class AlphaImageTests
{
    [Test]
    public void AntialiasedResizeTest()
    {
        using IDisposable scope = torch.NewDisposeScope();
        using torch.Tensor hint = torch.tensor([1.0f, 0, 0, 0, 0, 0, 0, 0, 0]).reshape(1, 1, 3, 3);
        using torch.Tensor colors = hint.expand(1, 3, 3, 3).contiguous();

        using torch.Tensor result = CorridorKey.AlphaImage.Prepare(colors, hint, imageSize: 1);

        Assert.That(result.shape, Is.EqualTo(new long[] { 1, 4, 1, 1 }));
        Assert.That(result[0, 3, 0, 0].ToSingle(), Is.EqualTo(4.0 / 49.0).Within(1e-6));
        Assert.That(result[0, 0, 0, 0].ToSingle(), Is.EqualTo((4.0 / 49.0 - 0.485) / 0.229).Within(1e-6));
    }
}
