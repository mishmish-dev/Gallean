using TorchSharp;
using static TorchSharp.torch.nn;

namespace Umbra;

internal sealed class TanhGelu : Module<torch.Tensor, torch.Tensor>
{
    // TorchScript exposes the native approximation on older TorchSharp versions too.
    private static readonly torch.jit.CompilationUnit Function = torch.jit.compile("""
        def gelu(input: Tensor) -> Tensor:
            return torch.gelu(input, approximate="tanh")

        """);

    public TanhGelu() : base(nameof(TanhGelu)) { }

    public override torch.Tensor forward(torch.Tensor input)
    {
        return Function.invoke<torch.Tensor>("gelu", input);
    }
}
