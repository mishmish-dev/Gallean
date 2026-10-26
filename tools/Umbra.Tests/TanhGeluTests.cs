using TorchSharp;

namespace Umbra.Tests;

public sealed class TanhGeluTests
{
    [Test]
    public void ApproximationTest()
    {
        using TanhGelu activation = new();
        using torch.Tensor input = torch.tensor([-2.0f, 0.0f, 2.0f]);

        using torch.Tensor result = activation.call(input);

        Assert.That(result.data<float>().ToArray(), Is.EqualTo(new[] { -0.0454023f, 0.0f, 1.9545977f }).Within(1e-6));
    }
}
